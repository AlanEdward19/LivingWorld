extends "res://tests/test_base.gd"
## #243: o padrão único de extração, no cliente. A árvore é estado do motor (`GET /world/trees`: só as tocadas): o cliente desenha inteira,
## cortada (copa menor) ou toco (sem copa, tronco baixo) e deixa de pôr árvore onde o motor diz toco; "+1 Madeira" aparece quando a unidade
## sai; a ação principal do E é a do alvo sob a mira (a árvore), e a pessoa segue na roda.

const Scenery := preload("res://Scenes/scenery.gd")
const Feedback := preload("res://Scenes/feedback.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const BodyBusy := preload("res://Scenes/body_busy.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const S := preload("res://tests/test_street_life.gd")
const Validator := preload("res://Contract/shape_validator.gd")


func _scenery() -> Array:
	var terrain := {}
	for x in range(0, 24):
		for y in range(0, 24):
			terrain[Vector2i(x, y)] = 1
	var scenery := Scenery.new()
	tree.root.add_child(scenery)
	scenery.build(terrain, {}, [])
	var cells: Array = []
	for x in range(0, 24):
		for y in range(0, 24):
			if scenery.stage_at(Vector2i(x, y)) != "":
				cells.append(Vector2i(x, y))
	return [scenery, cells]


func _tree(cell: Vector2i, left: int, capacity: int, stage: String) -> Dictionary:
	return {"x": cell.x, "y": cell.y, "left": left, "capacity": capacity, "stage": stage}


func test_the_scenery_draws_the_stage_the_engine_says() -> void:
	# o renderizador de teste (sem tela) não guarda as transformações das instâncias: confere o estado que as define (estágio e fator de copa)
	var made := _scenery()
	var scenery = made[0]
	var cells: Array = made[1]
	check(cells.size() >= 3, "há árvores pela regra do cliente")
	var a: Vector2i = cells[0]
	var b: Vector2i = cells[1]
	var c: Vector2i = cells[2]
	eq(scenery.stage_at(a), "whole", "sem nada do motor, inteira")
	eq(scenery.crown_factor_at(a), 1.0)
	scenery.apply_trees([_tree(a, 0, 8, "stump"), _tree(b, 4, 8, "cut")])
	eq(scenery.stage_at(a), "stump", "o motor disse toco")
	eq(scenery.stage_at(b), "cut", "o motor disse cortada")
	eq(scenery.stage_at(c), "whole", "a que o motor não citou segue inteira")
	eq(scenery.crown_factor_at(a), 0.0, "toco: sem copa")
	check(scenery.crown_factor_at(b) > Scenery.CUT_CROWN_MIN and scenery.crown_factor_at(b) < 1.0, "cortada: copa menor, proporcional ao que resta (%s)" % scenery.crown_factor_at(b))
	scenery.apply_trees([_tree(b, 6, 8, "cut")])
	check(scenery.crown_factor_at(b) > 0.7 and scenery.crown_factor_at(b) < 1.0, "com mais madeira a copa é maior")
	eq(scenery.stage_at(a), "whole", "o toco saiu da lista: cresceu de volta")
	eq(scenery.crown_factor_at(a), 1.0, "a copa voltou")
	scenery.apply_trees([_tree(Vector2i(900, 900), 0, 8, "stump")]) # onde a regra do cliente não põe árvore: nada a mudar
	eq(scenery.stage_at(Vector2i(900, 900)), "", "sem árvore ali")
	scenery.queue_free()


func test_a_rebuilt_terrain_keeps_the_engine_stumps_and_the_season_does_not_undo_them() -> void:
	var made := _scenery()
	var scenery = made[0]
	var a: Vector2i = made[1][0]
	scenery.apply_trees([_tree(a, 0, 8, "stump")])
	var terrain := {}
	for x in range(0, 24):
		for y in range(0, 24):
			terrain[Vector2i(x, y)] = 1
	scenery.build(terrain, {}, [])
	eq(scenery.stage_at(a), "stump", "o terreno refeito reaplica o estado do motor")
	scenery.apply_season({"weather": {"foliage": {"fraction": 0.0, "phase": "bare", "turning": 0.0}}})
	check(scenery.crown_scale < 1.0, "a folhagem do motor encolheu as copas")
	eq(scenery.crown_factor_at(a), 0.0, "e o toco segue sem copa (a conta da copa multiplica o fator do toco)")
	scenery.queue_free()


func test_the_app_asks_the_engine_for_the_cut_trees_and_draws_them() -> void:
	var app = await S.city_app(tree)
	var made := _scenery() # o app de teste não traz terreno: põe uma cena com árvores no renderizador
	var scenery = made[0]
	app.world_view.terrain.scenery = scenery
	var cell: Vector2i = made[1][0]
	app.api.transport.on("GET /world/trees", {"status": 200, "body": {"trees": [_tree(cell, 0, 8, "stump")]}})
	await app.refresh_trees()
	eq(scenery.stage_at(cell), "stump", "o toco do motor aparece no cliente")
	app.api.transport.on("GET /world/trees", {"status": 200, "body": {"trees": []}})
	await app.refresh_trees()
	eq(scenery.stage_at(cell), "whole", "a árvore que voltou inteira some da lista e volta a ser desenhada")
	scenery.queue_free()
	app.queue_free()


func test_plus_one_wood_shows_the_moment_a_unit_reaches_the_hands() -> void:
	var name_of := func(id: int) -> String: return "Madeira" if id == 3 else "recurso %d" % id
	eq(Feedback.carry_gains({"carrying": null}, {"carrying": {"resourceId": 3, "quantity": 1}}, name_of).map(func(l): return l.text), ["+1 Madeira"], "mãos vazias, uma unidade")
	eq(Feedback.carry_gains({"carrying": {"resourceId": 3, "quantity": 1}}, {"carrying": {"resourceId": 3, "quantity": 2}}, name_of).map(func(l): return l.text), ["+1 Madeira"], "uma a mais")
	eq(Feedback.carry_gains({"carrying": {"resourceId": 3, "quantity": 1}}, {"carrying": {"resourceId": 3, "quantity": 3}}, name_of).map(func(l): return l.text), ["+2 Madeira"], "duas entre uma leitura e outra")
	eq(Feedback.carry_gains({"carrying": {"resourceId": 3, "quantity": 2}}, {"carrying": {"resourceId": 3, "quantity": 2}}, name_of), [], "nada novo, nada de aviso")
	eq(Feedback.carry_gains({"carrying": {"resourceId": 3, "quantity": 2}}, {"carrying": null}, name_of), [], "guardar na despensa não é ganho")
	var fb = Feedback.new()
	fb.resource_name_of = name_of
	eq(fb.note_sheet({"money": 5, "carrying": null}), 0, "a primeira ficha só guarda a referência")
	eq(fb.note_sheet({"money": 5, "carrying": {"resourceId": 3, "quantity": 1}}), 1, "a unidade que saiu vira aviso sem esperar ação armada")
	eq(fb.note_sheet({"money": 5, "carrying": {"resourceId": 3, "quantity": 1}}), 0, "a mesma ficha não repete")
	fb.free()


func test_the_bar_says_how_many_units_are_out() -> void:
	var busy := BodyBusy.new()
	busy.note_pose({"busy": {"action": "gather", "untilMs": 1000000, "totalMs": 500000, "remainingMs": 300000, "units": 5, "done": 2}}, 0.0, 1.0)
	eq(busy.text(0.0), "Cortando lenha — 40% · 2 de 5 · faltam 5 min", "40% de 5 unidades: duas já saíram")
	busy.note_pose({"busy": {"action": "work", "untilMs": 1000000, "totalMs": 500000, "remainingMs": 300000}}, 0.0, 1.0)
	check(String(busy.text(0.0)).find(" de ") < 0, "ação sem unidades não fala em unidades")


func _offer(kind: String, id: int, x: float, y: float, verbs: Array, extra: Dictionary = {}) -> Dictionary:
	var offer := {"kind": kind, "id": id, "label": kind, "verbs": verbs, "x": int(floor(x)), "y": int(floor(y)), "resources": []}
	offer.merge(extra)
	return offer


func test_e_picks_the_target_under_the_aim_and_the_person_stays_in_the_wheel() -> void:
	# o corpo em (10,10); a árvore a leste (11,10.3), uma pessoa parada junto dela, ao norte (10,8.6)
	var tree_offer := _offer("nature", 1, 11.0, 10.3, ["gather"], {"fx": 11.0, "fy": 10.3, "resources": [{"resourceId": 3, "quantity": 4, "price": 0}]})
	var person := _offer("person", 77, 10.0, 8.0, ["talk"])
	var offers := [person, tree_offer]
	var entries := PlayerHud.menu_entries(offers, func(id: int) -> String: return "Madeira")
	var body := Vector2(10.0, 10.0)
	var toward_tree := PlayerFrame.game_entries(entries, offers, body, Vector2(1, 0))
	eq(toward_tree[0].kind, "nature", "olhando a árvore, o E corta lenha")
	var toward_person := PlayerFrame.game_entries(entries, offers, body, Vector2(0, -1))
	eq(toward_person[0].kind, "person", "olhando a pessoa, o E conversa")
	var no_aim := PlayerFrame.game_entries(entries, offers, body)
	eq(no_aim[0].kind, "nature", "sem olhar, a ordem de sempre: a coisa antes da pessoa")
	var elsewhere := PlayerFrame.game_entries(entries, offers, body, Vector2(-1, 0))
	eq(elsewhere[0].kind, "nature", "nada à frente: a ordem de sempre")
	for list in [toward_tree, toward_person, no_aim]:
		eq(list.size(), 2, "a roda segue com todas as linhas")
		check(list.any(func(e): return e.kind == "person") and list.any(func(e): return e.kind == "nature"), "a pessoa e a árvore ficam na roda")


func _hud(transport: FakeTransport) -> Array:
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	return [hud, transport]


func test_the_prompt_follows_the_camera_between_a_tree_and_a_person_next_to_it() -> void:
	var made: Array = await _hud(FakeTransport.new())
	var hud = made[0]
	var look := [Vector2(1, 0)] # a lambda captura por valor: o olhar mora num array que o teste muda
	hud.aim_look = func() -> Vector2: return look[0]
	hud.fine = Vector2(10.0, 10.0)
	var offers := [_offer("person", 77, 10.0, 8.0, ["talk"], {"label": "Caio"}),
		_offer("nature", 1, 11.0, 10.3, ["gather"], {"fx": 11.0, "fy": 10.3, "resources": [{"resourceId": 3, "quantity": 4, "price": 0}]})]
	hud.set_reach_hint(offers)
	check(String(hud.reach_hint.text).find("Cortar lenha") >= 0, "olhando a árvore, a dica é cortar lenha: %s" % hud.reach_hint.text)
	look[0] = Vector2(0, -1) # a câmera gira para a pessoa
	hud._refresh_reach_hint(0.016)
	check(String(hud.reach_hint.text).find("Conversar") >= 0, "girou para a pessoa: a dica muda sem nova leitura do motor: %s" % hud.reach_hint.text)
	look[0] = Vector2(1, 0)
	hud._refresh_reach_hint(0.016)
	check(String(hud.reach_hint.text).find("Cortar lenha") >= 0, "e volta à árvore: %s" % hud.reach_hint.text)
	made[0].frame.queue_free()
	made[0].queue_free()


func test_the_world_trees_shape_is_in_the_contract() -> void:
	var shape = Validator.load_shape("world-trees")
	check(shape != null, "o contrato world-trees existe")
	if shape != null:
		eq(Validator.validate({"trees": []}, shape), [], "a resposta vazia vale")
