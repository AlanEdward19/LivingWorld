extends "res://tests/test_base.gd"
## #258 (sobras do sandbox): o cliente desenha o que o motor diz do entorno da casa. A rua pavimentada e a calçada vêm da camada `Roads` (superfície do motor);
## o quintal limpo, de `clear` em `GET /world/trees` (sem árvore onde o motor não aceita); o abajur acende só se o motor diz que a luz está ligada (índice da
## peça em `lit` do retrato e em `lights` do delta do interior); o uso de ligar e desligar vem do catálogo (`toggle`).

const Scenery := preload("res://Scenes/scenery.gd")
const Style := preload("res://Scenes/render/street_style.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")
const Store := preload("res://Store/world_store.gd")
const S := preload("res://tests/test_street_life.gd")
const G := preload("res://tests/test_city_ground.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")


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


# --- rua e calçada: a superfície é do motor ---------------------------------------------------------------------------------------------------

func test_the_paved_street_and_the_gravel_sidewalk_the_engine_says_are_drawn() -> void:
	var roads: Array = []
	for x in range(0, 8):
		roads.append(G.road(x, 12, 3, 4, true)) # a rua principal pavimentada (pedra)
		roads.append(G.road(x, 13, 2, 4)) # a calçada (cascalho)
	var app = await G.city_app(tree, G.transport(G.world_with_roads(roads)))
	var ground = app.world_view.ground

	for x in range(0, 8):
		check(ground.drawn_roads.has(Vector2i(x, 12)), "rua pavimentada em %d,12" % x)
		check(ground.drawn_roads.has(Vector2i(x, 13)), "calçada em %d,13" % x)
	eq(Style.style_of(ground.drawn_roads[Vector2i(3, 12)]).name, "pedra", "a rua é de pedra (a superfície do motor)")
	eq(Style.style_of(ground.drawn_roads[Vector2i(3, 13)]).name, "cascalho", "a calçada é de cascalho")
	check(Style.style_of(ground.drawn_roads[Vector2i(3, 12)]).color != Style.style_of(ground.drawn_roads[Vector2i(3, 13)]).color, "rua e calçada se distinguem")
	check(ground.road_pieces > 16, "a malha de ruas tem as peças das duas faixas (%d)" % ground.road_pieces)
	app.queue_free()


# --- quintal limpo: sem árvore onde o motor não aceita ----------------------------------------------------------------------------------------

func test_the_scenery_hides_the_trees_in_the_zones_the_engine_clears_and_brings_them_back() -> void:
	var made := _scenery()
	var scenery = made[0]
	var cells: Array = made[1]
	check(cells.size() >= 3, "há árvores pela regra do cliente")
	var inside: Vector2i = cells[0]
	var outside: Vector2i = cells[cells.size() - 1]
	eq(scenery.stage_at(inside), "whole", "sem zona do motor, a árvore da regra aparece")

	scenery.apply_clear([{"x": inside.x - 1, "y": inside.y - 1, "w": 3, "h": 3}])
	eq(scenery.stage_at(inside), "cleared", "dentro da zona limpa o motor não aceita árvore")
	eq(scenery.crown_factor_at(inside), 0.0, "sem copa")
	if absi(outside.x - inside.x) > 1 or absi(outside.y - inside.y) > 1:
		eq(scenery.stage_at(outside), "whole", "fora da zona a árvore segue")
	check(scenery.hidden_count() >= 1, "ao menos uma árvore escondida")

	scenery.apply_trees([{"x": inside.x, "y": inside.y, "left": 0, "capacity": 8, "stage": "stump"}]) # o toco do motor não ressuscita a árvore escondida
	eq(scenery.stage_at(inside), "cleared", "zona limpa vence")

	scenery.apply_clear([])
	eq(scenery.stage_at(inside), "whole", "sem a zona a árvore volta")
	eq(scenery.hidden_count(), 0)
	scenery.queue_free()


func test_a_rebuilt_terrain_keeps_the_cleared_zones() -> void:
	var made := _scenery()
	var scenery = made[0]
	var a: Vector2i = made[1][0]
	scenery.apply_clear([{"x": a.x, "y": a.y, "w": 1, "h": 1}])
	var terrain := {}
	for x in range(0, 24):
		for y in range(0, 24):
			terrain[Vector2i(x, y)] = 1
	scenery.build(terrain, {}, [])
	eq(scenery.stage_at(a), "cleared", "o terreno refeito reaplica a zona do motor")
	scenery.queue_free()


func test_the_app_asks_the_engine_which_zones_have_no_trees() -> void:
	var app = await S.city_app(tree)
	var made := _scenery()
	var scenery = made[0]
	app.world_view.terrain.scenery = scenery
	var cell: Vector2i = made[1][0]
	app.api.transport.on("GET /world/trees", {"status": 200, "body": {"trees": [], "clear": [{"x": cell.x, "y": cell.y, "w": 2, "h": 2}]}})
	await app.refresh_trees()
	eq(scenery.stage_at(cell), "cleared", "o quintal limpo do motor aparece no cliente")
	app.api.transport.on("GET /world/trees", {"status": 200, "body": {"trees": []}}) # servidor sem o campo: nada limpo
	app._trees_json = ""
	await app.refresh_trees()
	eq(scenery.stage_at(cell), "whole", "sem zonas, volta a regra de antes")
	app.queue_free()


# --- abajur: a luz ligada é estado do motor ---------------------------------------------------------------------------------------------------

func _factory() -> Object:
	var toggles := [{"id": "on", "name": "Ligar", "toggle": "light:on"}, {"id": "off", "name": "Desligar", "toggle": "light:off"}]
	return ObjectFactory.new([
		{"id": "lamp", "shape": [{"shape": "box", "size": [0.3, 1.0, 0.3], "offset": [0, 0.5, 0], "color": "4a3f35"}],
			"light": {"color": "ffd9a0", "rangeM": 7.0, "energy": 1.6, "heightM": 1.3}, "uses": toggles},
		{"id": "old_lamp", "shape": [{"shape": "box", "size": [0.3, 1.0, 0.3], "offset": [0, 0.5, 0], "color": "4a3f35"}],
			"light": {"color": "ffd9a0", "rangeM": 7.0, "energy": 1.6, "heightM": 1.3}}])


func test_the_catalog_says_which_objects_have_a_switch() -> void:
	var factory := _factory()
	eq(factory.switchable("lamp"), true, "o abajur tem uso de ligar e desligar")
	eq(factory.switchable("old_lamp"), false, "o objeto de luz sem esses usos segue o escurecer de antes")
	eq(factory.switchable("nada"), false)


func test_the_lamp_lights_only_by_the_engine_state_not_by_the_hour() -> void:
	var factory := _factory()
	var view := InteriorView.new()
	tree.root.add_child(view)
	var holder := Node3D.new()
	view.add_child(holder)
	view._lit_piece = 11
	view._lit_snapshot = false
	view._add_lamp(holder, factory.light_of("lamp"), factory.switchable("lamp"))
	var lamp: OmniLight3D = holder.get_node("Lamp")
	view.set_lamp_level(1.0) # noite com gente em casa: o abajur ligável NÃO acende por isso
	eq(lamp.visible, false, "o motor não ligou: apagado")
	eq(view.lit_count(), 0)

	view.set_lit([11.0]) # o JSON traz número como real
	eq(lamp.visible, true, "o motor disse que a peça 11 está ligada")
	check(is_equal_approx(lamp.light_energy, 1.6), "com a intensidade do catálogo")
	eq(view.lit_count(), 1)
	view.set_lit([3, 5])
	eq(lamp.visible, false, "outra peça ligada: esta apagada")
	view.set_lit([])
	eq(view.lit_count(), 0)
	view.queue_free()


func test_the_snapshot_light_is_the_start_and_the_old_lamp_still_follows_the_night() -> void:
	var factory := _factory()
	var view := InteriorView.new()
	tree.root.add_child(view)
	var holder := Node3D.new()
	view.add_child(holder)
	view._lit_piece = 4
	view._lit_snapshot = true # o retrato da casa já diz que a luz está ligada
	view._add_lamp(holder, factory.light_of("lamp"), true)
	view.set_lit(null) # nenhum delta ainda: vale o retrato
	eq(view.lit_count(), 1, "acesa pelo retrato")
	var old := Node3D.new()
	view.add_child(old)
	view._lit_piece = 6
	view._lit_snapshot = false
	view._add_lamp(old, factory.light_of("old_lamp"), false)
	view.set_lamp_level(1.0)
	eq((old.get_node("Lamp") as OmniLight3D).visible, true, "o sem uso de ligar acende ao escurecer (servidor sem o uso)")
	view.set_lamp_level(0.0)
	eq((old.get_node("Lamp") as OmniLight3D).visible, false)
	eq(view.lit_count(), 1, "e o ligável não muda pela hora")
	view.queue_free()


func test_the_interior_delta_carries_the_lit_pieces() -> void:
	var store := Store.new()
	store.interior = {"occupants": []}
	store.apply_delta({"payload": {"entered": [], "moved": [], "changedFloor": [], "exited": [], "lights": [11, 12]}})
	eq(store.interior.get("lights"), [11, 12], "o conjunto de luzes ligadas, como o motor disse")
	store.apply_delta({"payload": {"entered": [], "moved": [], "changedFloor": [], "exited": []}})
	eq(store.interior.get("lights"), [11, 12], "sem o campo, nada muda")


# --- comprar na banca: o motor responde sem `outcome` ----------------------------------------------------------------------------------------

func test_the_purchase_reply_with_a_null_outcome_is_a_done_text_not_a_crash() -> void:
	var entry := {"text": "Banca: Comprar Comida (5)", "kind": "stall", "id": 0, "verb": "buy", "resource": 1}
	var text := PlayerHud.done_text(entry, {"kind": "stall", "verb": "buy", "quantity": 2, "value": 10, "outcome": null, "note": null})
	eq(text, "✓ Banca: Comprar Comida · 2 un. por 10", "a troca de banca vem com `outcome` nulo")
