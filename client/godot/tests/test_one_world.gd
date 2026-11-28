extends "res://tests/test_base.gd"
## #249 (M3 da fatia jogável): um mundo só. Entrar e sair da casa não refaz a cidade nem troca a figura do jogador; a casa tem
## assinatura própria e pode chegar antes da porta; trocar de escopo ou receber o snapshot de novo aplica a diferença.

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Fx := preload("res://tests/fixtures.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const BODY := 7


func _interior(occupant: int = BODY) -> Dictionary:
	var cells: Array = []
	for y in 3:
		for x in 3:
			cells.append({"cell": {"x": x, "y": y}, "material": 3})
	var env := Fx.snapshot(0, {
		"id": {"value": 4}, "city": {"value": "city-a"}, "buildingTypeId": -1, "occupancyModeled": true,
		"layout": {"floors": 1, "floorLayouts": [{"level": 0, "cells": cells}]},
		"occupants": [{"npc": {"value": occupant}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}}],
	}, "Interior")
	env.scope.kind = 2
	return env


## Um app no modo jogador, dono do corpo 7, na cidade.
func _player_app() -> Node:
	var fake := O.transport()
	fake.on("GET /visual/subscribe?scope=Interior", {"status": 200, "body": _interior()})
	var app = await T.make_app(tree, fake)
	await tree.create_timer(0.5).timeout
	app.rig.city_exit_size = 1000000.0
	await app.enter_scope("City", "city-a")
	app.player_hud.is_active = true
	app.player_hud.npc_id = BODY
	app.rig.set_mode(app.rig.THIRD)
	app.rig.follow(app.world_view.npc_nodes[BODY])
	return app


func _ids(nodes: Dictionary) -> Dictionary:
	var out := {}
	for id in nodes:
		out[id] = nodes[id].get_instance_id()
	return out


func test_the_player_figure_is_the_same_instance_before_inside_and_after_the_house() -> void:
	var app = await _player_app()
	var before: Node3D = app.world_view.npc_nodes[BODY]
	var before_id := before.get_instance_id()
	await app._on_player_placed(4, 0)
	eq(app.stage.active, true, "dentro")
	eq(app.player_actor().get_instance_id(), before_id, "dentro da casa o corpo é a mesma instância da rua")
	eq(app.world_view.npc_nodes[BODY].get_instance_id(), before_id, "o mundo segue com o mesmo boneco")
	check(before.visible, "e ele nunca some")
	eq(app.rig.following.get_instance_id(), before_id, "a câmera segue o mesmo alvo")
	await app._on_player_placed(-1, 0)
	eq(app.stage.active, false, "fora")
	eq(app.player_actor().get_instance_id(), before_id, "depois de sair, a mesma instância")
	eq(app.rig.following.get_instance_id(), before_id, "e a câmera segue a mesma")
	app.queue_free()


func test_the_city_is_not_rebuilt_when_entering_and_leaving_a_house() -> void:
	var app = await _player_app()
	var fake = app.stream.transport
	var npc_nodes := _ids(app.world_view.npc_nodes)
	var building_nodes := _ids(app.world_view.buildings.nodes)
	var city_calls: int = fake.count_calls("GET /visual/subscribe?scope=City")
	check(not npc_nodes.is_empty() and not building_nodes.is_empty(), "preparado: a cidade tem gente e prédios")
	await app._on_player_placed(4, 0)
	await app._on_player_placed(-1, 0)
	eq(fake.count_calls("GET /visual/subscribe?scope=City"), city_calls, "a cidade não é pedida de novo")
	eq(app.stream.scope_kind, "City", "a assinatura da cidade seguiu a mesma")
	eq(app.store.kind, "City", "o escopo desenhado é o mesmo")
	for id in npc_nodes:
		eq(app.world_view.npc_nodes[id].get_instance_id(), npc_nodes[id], "o boneco %s continua o mesmo" % id)
	for id in building_nodes:
		eq(app.world_view.buildings.nodes[id].get_instance_id(), building_nodes[id], "o prédio %s continua o mesmo" % id)
	app.queue_free()


func test_the_world_stays_visible_and_alive_while_the_house_is_in_focus() -> void:
	var app = await _player_app()
	await app._on_player_placed(4, 0)
	eq(app.world_view.visible, true, "o mundo segue desenhado")
	eq(app.world_view.process_mode, Node.PROCESS_MODE_INHERIT, "e processando")
	eq(app.world_view.buildings.nodes[4].visible, false, "só o prédio da casa em foco sai do desenho (a casa o substitui)")
	var other: Node3D = app.world_view.npc_nodes[8]
	var from_cell: Vector2i = app.store.npcs[8].cell
	app.stream.delta_applied.emit(Fx.delta(0, 1, {"npcUpserts": [{"id": {"value": 8}, "location": {"x": 5, "y": 3}, "currentAction": 8,
		"city": null}]}))
	check(app.store.npcs[8].cell != from_cell, "a rua continua recebendo deltas com a casa em foco")
	eq(app.world_view.npc_nodes[8].get_instance_id(), other.get_instance_id(), "e o boneco da rua é o mesmo")
	await app._on_player_placed(-1, 0)
	eq(app.world_view.buildings.nodes[4].visible, true, "ao sair o prédio volta")
	app.queue_free()


func test_the_body_is_never_a_second_figure_and_people_inside_are_drawn_once() -> void:
	var app = await _player_app()
	app.player_hud.fine = Vector2(1.5, 1.5)
	app.store.interior = {}
	await app._on_player_placed(4, 0)
	var view = app.stage.interior_view
	check(not view.occupant_nodes.has(BODY), "o corpo não tem boneco da casa")
	# outro morador dentro da casa: o interior o desenha, o mundo não desenha o boneco dele por cima
	var inside = _interior(8)
	app.session.on_snapshot(inside)
	check(view.occupant_nodes.has(8), "quem está dentro é desenhado pela casa")
	eq(app.world_view.npc_nodes[8].visible, false, "e o boneco do mundo da mesma pessoa fica de fora")
	await app._on_player_placed(-1, 0)
	eq(app.world_view.npc_nodes[8].visible, true, "ao sair o mundo o desenha de novo")
	app.queue_free()


## #249: com o corpo chegando à porta, a casa já está assinada e lida; o passo de entrada só a mostra (nada a pedir à rede).
func test_the_house_is_loaded_before_the_door_and_appears_in_the_entry_step() -> void:
	var app = await _player_app()
	var fake = app.stream.transport
	app.player_hud.fine = Vector2(0.5, 0.5) # a 2 células do prédio 4 (cell 1,1; 2x2)
	app.session.warm_interior_near_body(1.0)
	for i in 6:
		await tree.process_frame
	eq(app.session._warm_building, 4, "o prédio perto do corpo foi assinado")
	eq(int(app.store.interior.get("building", -1)), 4, "o interior já está na loja")
	eq(app.stage.active, false, "mas a casa ainda não é mostrada")
	eq(app.session.interior_building, -1, "e não vira foco sozinha")
	var asked: int = fake.count_calls("GET /visual/subscribe?scope=Interior")
	app.player_hud.fine = Vector2(1.5, 1.5)
	await app._on_player_placed(4, 0)
	eq(app.stage.active, true, "no passo de entrada a casa aparece")
	eq(fake.count_calls("GET /visual/subscribe?scope=Interior"), asked, "sem pedir nada à rede nesse passo")
	check(app.stage.interior_view.floor_nodes.size() > 0, "com piso e paredes montados")
	app.queue_free()


func test_the_warm_house_is_dropped_when_the_body_walks_away() -> void:
	var app = await _player_app()
	app.player_hud.fine = Vector2(0.5, 0.5)
	app.session.warm_interior_near_body(1.0)
	for i in 6:
		await tree.process_frame
	eq(app.session._warm_building, 4, "assinada perto")
	app.player_hud.fine = Vector2(5.9, 5.9) # a mais de 3 células do prédio e depois do alcance de manter
	app.store.buildings[4].cell = Vector2i(1, 1)
	app.session.building_distance(app.store.buildings[4], app.player_hud.fine)
	check(app.session.building_distance(app.store.buildings[4], app.player_hud.fine) > app.session.WARM_REACH, "preparado: longe")
	app.player_hud.fine = Vector2(30.5, 30.5)
	app.session.warm_interior_near_body(1.0)
	eq(app.session._warm_building, -1, "longe, a casa deixa de ser assinada")
	eq(app.interior_stream.state, "idle", "e a assinatura do interior fecha")
	app.queue_free()


## Snapshot do mesmo escopo (o canal reenvia ao abrir; volta da casa): aplica a diferença; quem continua é a mesma instância.
func test_a_second_snapshot_of_the_same_scope_applies_the_difference() -> void:
	var app = await _player_app()
	var nodes := _ids(app.world_view.npc_nodes)
	var buildings := _ids(app.world_view.buildings.nodes)
	var again := O.city()
	again.payload.livingState.npcs[1]["location"] = {"x": 5, "y": 4} # o 8 andou
	again.payload.livingState.npcs.append({"id": {"value": 9}, "location": {"x": 2, "y": 4}, "currentAction": 2, "city": {"value": "city-a"}})
	app.session.on_snapshot(again)
	eq(app.world_view.npc_nodes[7].get_instance_id(), nodes[7], "quem não mudou é a mesma instância")
	eq(app.world_view.npc_nodes[8].get_instance_id(), nodes[8], "quem andou também (ele só vai à célula nova)")
	check(app.world_view.npc_nodes.has(9), "quem chegou nasce")
	eq(app.world_view.buildings.nodes[4].get_instance_id(), buildings[4], "o prédio igual não é refeito")
	var gone := O.city()
	gone.payload.livingState.npcs.remove_at(1)
	app.session.on_snapshot(gone)
	check(not app.world_view.npc_nodes.has(8), "quem saiu do escopo sai do desenho")
	eq(app.world_view.npc_nodes[7].get_instance_id(), nodes[7], "e o resto segue igual")
	app.queue_free()


## Terreno e rios são os mesmos entre escopos: a malha do chão não é refeita (nem o mundo → cidade → mundo a refaz).
func test_the_terrain_mesh_survives_a_scope_switch_when_the_terrain_is_the_same() -> void:
	var fake := O.transport()
	var world := T.world()
	var entries: Array = []
	for x in 8:
		for y in 8:
			entries.append({"key": {"x": x, "y": y}, "value": {"id": 1}})
	world.payload.layers = {"Terrain": {"isModeled": true, "payload": entries}}
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": world})
	var app = await T.make_app(tree, fake)
	await tree.create_timer(0.5).timeout
	var root = app.world_view.terrain.terrain_root
	check(root != null, "preparado: terreno desenhado")
	var rev: int = app.store.terrain_rev
	await app.enter_scope("City", "city-a")
	eq(app.world_view.terrain.terrain_root, root, "entrar na cidade não refaz o chão")
	await app.enter_scope("World", "")
	eq(app.world_view.terrain.terrain_root, root, "voltar ao mundo também não")
	eq(app.store.terrain_rev, rev, "o terreno é o mesmo")
	app.queue_free()


## #249: uma cidade grande não é desenhada num quadro só: o que falta entra por fatias, do mais perto da câmera para o mais longe.
func test_a_big_city_is_drawn_in_slices_nearest_first_instead_of_in_one_frame() -> void:
	var app = await _player_app()
	var buildings = app.world_view.buildings
	var footprint: Array = []
	for y in 2:
		for x in 2:
			footprint.append({"cell": Vector2i(x, y), "material": 1})
	for i in 400:
		app.store.buildings[100 + i] = {"id": 100 + i, "type": 3, "cell": Vector2i(10 + (i % 20) * 3, 10 + (i / 20) * 3), "orientation": 0, "floors": 1,
			"footprint": footprint, "value": 100, "lodging": null, "lot": {}, "work": {}}
	buildings.rebuild(app.store, Vector3(10, 0, 10))
	check(buildings.pending_count() > 0, "nem tudo foi desenhado no mesmo quadro")
	var first_done: int = buildings.nodes.size()
	for i in 600:
		if buildings.pending_count() == 0:
			break
		await tree.process_frame
	eq(buildings.pending_count(), 0, "e a fila acaba")
	eq(buildings.nodes.size(), 401, "todos os prédios acabam desenhados (400 novos + o da cidade)")
	check(first_done < 401, "a primeira fatia ficou parcial")
	app.queue_free()


## #249: com a câmera do jogador fora da casa, a parede que fica entre ela e o corpo é translúcida (vê-se por ela); a do fundo segue opaca.
func test_the_wall_between_the_camera_and_the_body_turns_translucent() -> void:
	# #226: piso de (1,1) a (3,1) e as paredes como o motor manda, em arestas (não mais células de parede).
	var cells: Array = []
	var walls: Array = []
	for x in range(1, 4):
		cells.append({"cell": {"x": x, "y": 1}, "material": 3})
		walls.append({"a": {"x": x, "y": 0}, "b": {"x": x, "y": 1}, "material": 1})
		walls.append({"a": {"x": x, "y": 1}, "b": {"x": x, "y": 2}, "material": 1})
	walls.append({"a": {"x": 0, "y": 1}, "b": {"x": 1, "y": 1}, "material": 1})
	walls.append({"a": {"x": 3, "y": 1}, "b": {"x": 4, "y": 1}, "material": 1})
	var view := InteriorView.new()
	tree.root.add_child(view)
	view.immersive = true
	view.show_interior({"building": 1, "floors": 1, "layout": [{"level": 0, "cells": cells, "walls": walls, "doors": []}], "occupants": []}, Vector2i(0, 0), ActionLabels.new())
	var actor := NpcActor.new()
	actor.setup(1, Vector2i(3, 1), -1, ActionLabels.new())
	tree.root.add_child(actor)
	actor.position = Vector3(3.5, 0, 1.5)
	view.guide_body(1, actor, 0)
	view.set_view_direction(Vector3.ZERO)
	view.update_near(Vector3(-6.0, 1.0, 1.5)) # do lado de fora, pelo oeste
	var shaded := 0
	var solid := 0
	for wall in view._walls:
		if wall.get("lintel", false):
			continue
		if (wall.node as MeshInstance3D).transparency > 0.8:
			shaded += 1
		elif (wall.node as MeshInstance3D).transparency < 0.05:
			solid += 1
	check(shaded >= 1, "a parede entre a câmera e o corpo ficou translúcida")
	check(solid >= 1, "as outras seguem opacas (a planta ainda se lê)")
	view.update_near(Vector3(3.5, 3.0, 1.5)) # câmera alta sobre o corpo: nenhuma parede no caminho
	for wall in view._walls:
		if not wall.get("lintel", false):
			check((wall.node as MeshInstance3D).transparency < 0.8, "câmera por cima: parede nenhuma entre a câmera e o corpo")
	view.release_guided()
	view.queue_free()
	actor.queue_free()


## #249: o corpo do jogador atravessa a troca cidade -> mundo na MESMA figura: sair do fluxo da cidade não a descarta e o mundo, ao
## trazê-lo, a reconhece (nenhum quadro sem figura, nenhum boneco novo).
func test_the_player_figure_crosses_a_city_to_world_switch_as_the_same_instance() -> void:
	var app = await _player_app()
	var body: Node3D = app.world_view.npc_nodes[BODY]
	body.driven = true
	var id := body.get_instance_id()
	app.stream.delta_applied.emit(Fx.delta(0, 1, {"npcRemoved": [{"value": BODY}]})) # o corpo saiu do fluxo da cidade
	check(is_instance_valid(body) and app.world_view.npc_nodes.get(BODY) == body, "a figura segue desenhada depois de sair da cidade")
	var world := T.world()
	world.payload.externalNpcs = [{"id": {"value": BODY}, "location": {"x": 7, "y": 7}}]
	app.session.on_snapshot(world)
	eq(app.world_view.npc_nodes[BODY].get_instance_id(), id, "o mundo reconhece o mesmo corpo")
	check(not body.has_meta("orphan"), "e ele deixa de ser tratado como sobra")
	app.queue_free()
