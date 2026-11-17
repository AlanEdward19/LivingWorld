extends "res://tests/test_base.gd"
## AD-177: o cliente desenha a cidade que o motor diz (camada `Roads` e `lot` do prédio, formas de
## tests/contracts/shape.world-subscribe.json e city-subscribe.json) e ajuda a ler a cidade (Tab, raio-x, planta, rótulos).

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Store := preload("res://Store/world_store.gd")
const Ground := preload("res://Store/city_ground.gd")
const Style := preload("res://Scenes/render/street_style.gd")
const GroundRenderer := preload("res://Scenes/render/ground_renderer.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Grid := preload("res://Scenes/grid.gd")
const Shortcuts := preload("res://Scenes/shortcuts.gd")


static func road(x: int, y: int, surface: int, users: int, main := false, square := false, extra := {}) -> Dictionary:
	var body := {"cell": {"x": x, "y": y}, "city": {"value": "city-a"}, "surface": surface, "users": users, "main": main,
		"square": square}
	body.merge(extra, true)
	return body


static func world_with_roads(extra_roads: Array = []) -> Dictionary:
	var env := T.world()
	env.payload.layers["Roads"] = {"isModeled": true, "payload": [road(1, 4, 1, 9, true), road(2, 4, 1, 9, true),
		road(3, 4, 0, 2), road(2, 5, 0, 20, false, true), road(5, 4, 3, 30), road(4, 4, 2, 12), road(5, 0, 1, 3),
		road(0, 5, 9, 1, false, false, {"access": "escada", "paving": {"progress": 0.5}})] + extra_roads}
	return env


static func city_with_lot() -> Dictionary:
	var env := O.city()
	env.payload.buildings[0]["lot"] = {"croft": [{"x": 3, "y": 1}, {"x": 3, "y": 2}], "frontPath": [{"x": 1, "y": 3}],
		"street": {"x": 1, "y": 4}, "hedge": "desconhecido"}
	return env


static func transport(world: Dictionary = world_with_roads()) -> Object:
	var fake = O.transport()
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": world})
	fake.on("GET /visual/subscribe?scope=City", {"status": 200, "body": city_with_lot()})
	return fake


static func city_app(tree: SceneTree, fake = null) -> Node:
	var app = await T.make_app(tree, fake if fake != null else transport())
	await app.enter_scope("City", "city-a")
	return app


func test_roads_layer_is_read_in_the_contract_shape_and_unknown_fields_are_tolerated() -> void:
	var store := Store.new()
	store.apply_snapshot(world_with_roads([{"cell": null}, "lixo", {"x": 7, "y": 7}]))
	eq(store.roads.size(), 9, "cada célula da camada vira rua; entrada sem célula é ignorada")
	var main: Dictionary = store.roads[Vector2i(1, 4)]
	eq([main.city, main.surface, main.users, main.main, main.square], ["city-a", 1, 9, true, false], "campos do contrato")
	eq(store.roads[Vector2i(2, 5)].square, true, "largo")
	var odd: Dictionary = store.roads[Vector2i(0, 5)]
	eq(odd.extra.get("access"), "escada", "campo desconhecido guardado em extra (AD-176 entra por street_style)")
	eq(Style.style_of(odd).name, Style.NEUTRAL.name, "superfície desconhecida cai no desenho neutro")
	eq(store.roads[Vector2i(7, 7)].surface, -1, "forma antiga (x/y) ainda lida, sem superfície")
	eq(Ground.roads({"isModeled": false, "payload": [road(1, 1, 1, 1)]}), {}, "camada não modelada: sem rua")
	eq(Ground.roads(null), {}, "camada ausente: sem rua")
	eq(Ground.lot(null), {}, "lote nulo")
	eq(Ground.lot({"croft": null, "frontPath": [], "street": null}), {"croft": [], "front_path": [], "street": null, "access": ""},
		"listas vazias (acesso ausente = servidor sem a rua de verdade)")


func test_each_surface_has_its_own_look_and_main_square_and_users_show() -> void:
	var looks := {}
	for surface in [0, 1, 2, 3]:
		var style := Style.style_of({"surface": surface, "users": 6})
		looks[[style.color, style.detail, style.width]] = true
	eq(looks.size(), 4, "trilha, terra batida, cascalho e pedra distinguíveis")
	check(Style.style_of({"surface": 0, "users": 1}).width < Style.style_of({"surface": 1, "users": 1}).width, "trilha estreita")
	check(Style.style_of({"surface": 0, "users": 1}).jitter > 0.0, "trilha irregular")
	check(Style.style_of({"surface": 1, "users": 24}).width > Style.style_of({"surface": 1, "users": 1}).width, "mais gente, mais larga")
	check(Style.style_of({"surface": 1, "users": 6, "main": true}).width > Style.style_of({"surface": 1, "users": 6}).width, "principal mais larga")
	eq(Style.style_of({"surface": 0, "users": 1, "square": true}).width, 1.0, "largo ocupa a célula")
	eq(Style.title_of({"surface": 1, "main": true}), "Rua principal de terra batida")
	eq(Style.title_of({"surface": 77}), "Passagem", "superfície desconhecida")


func test_ground_draws_streets_only_where_roads_say_and_lot_variants_from_deltas_do_not_break() -> void:
	var other := road(7, 7, 1, 1)
	other.city = {"value": "city-b"}
	var app = await city_app(tree, transport(world_with_roads([other])))
	var ground = app.world_view.ground
	eq(app.store.roads.size(), 9, "a camada tem a rua de outra cidade")
	eq(ground.drawn_roads.size(), 8, "na cidade: as ruas dela (a de outra cidade/fora dos limites não)")
	for cell in ground.drawn_roads:
		eq(ground.drawn_roads[cell].surface, app.store.roads[cell].surface, "superfície certa em %s" % cell)
	eq(ground.entity_at(Vector2i(4, 1)), {}, "fora de rua e de lote: terreno")
	app.stream.delta_applied.emit(O.delta({"buildingUpserts": [
		{"id": {"value": 21}, "buildingTypeId": 3, "location": {"x": 0, "y": 0}, "floors": 1, "groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 2}], "lot": null},
		{"id": {"value": 22}, "buildingTypeId": 3, "location": {"x": 5, "y": 2}, "floors": 1, "groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 2}], "lot": {"croft": [], "frontPath": [], "street": null}},
		{"id": {"value": 23}, "buildingTypeId": 3, "location": {"x": 4, "y": 0}, "floors": 1, "groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 2}],
			"lot": {"croft": [{"x": 4, "y": 1}], "frontPath": [], "street": {"x": 5, "y": 0}, "kind": "novo"}}]}))
	app.world_view._process(0.016)
	eq(ground.entity_at(Vector2i(4, 1)), Registry.ref(Registry.BUILDING, 23), "o delta trouxe o quintal: aparece sem recarregar")
	eq(ground.entity_at(Vector2i(1, 0)), {}, "lote nulo: nada em volta")
	check(app.world_view.building_nodes.has(21) and app.world_view.building_nodes.has(22), "lote nulo/vazio não quebra o prédio")
	await app.inspector.inspect(Registry.ref(Registry.BUILDING, 22))
	check("Sem quintal" in app.inspector.text and "Não dá para nenhuma rua" in app.inspector.text, app.inspector.text)
	app.queue_free()


func test_street_and_lot_sheets_and_short_hover() -> void:
	var app = await city_app(tree)
	await app.inspect(Registry.ref(Registry.STREET, "1,4"))
	var text: String = app.inspector.text
	check("terra batida" in text and "9 pessoas" in text and "rua principal" in text, text)
	eq(app.inspector.link_refs.map(func(r): return Registry.key(r)), ["city:city-a", "building:4"], "cidade e o prédio cuja porta dá aqui")
	eq(app.inspector._title.text, "Rua principal de terra batida")
	check(app.inspector.focus_button.visible, "Ir até lá")
	await app.inspect(Registry.ref(Registry.STREET, "2,5"))
	check("largo" in app.inspector.text, app.inspector.text)
	var hover: String = app.selection.describe(Registry.ref(Registry.STREET, "3,4"))
	check(hover.begins_with("Trilha de capim pisado · 2 pessoas") and "clique" in hover, hover)
	await app.inspect(Registry.ref(Registry.BUILDING, 4))
	text = app.inspector.text
	check("Quintal: 2 células, cercado" in text, text)
	check("Dá para a rua em 1,4 (rua principal de terra batida) por um caminho de 1 célula" in text, text)
	check(app.inspector.link_refs.any(func(r): return Registry.key(r) == "street:1,4"), "link para a rua da frente")
	check("lote: quintal de 2 células · caminho de 1 até a rua" in app.selection.describe(Registry.ref(Registry.BUILDING, 4)), "dica do prédio com o lote")
	app.queue_free()


func test_clicking_a_street_cell_opens_its_sheet() -> void:
	var app = await city_app(tree)
	var picker := StaticBody3D.new() # o colisor do chão (terreno) que o raio do clique acerta
	picker.set_meta("ground", true)
	eq(app.selection.from_hit({"collider": picker, "position": Grid.to_world(Vector2i(5, 0))}), Registry.ref(Registry.STREET, "5,0"),
		"clique no chão de uma rua: ficha da rua")
	eq(app.selection.from_hit({"collider": picker, "position": Grid.to_world(Vector2i(3, 2)) + Vector3(0.3, 0, 0.3)}),
		Registry.ref(Registry.BUILDING, 4), "no quintal: a ficha do prédio dono do lote")
	eq(app.selection.from_hit({"collider": picker, "position": Grid.to_world(Vector2i(4, 2))}), {}, "terreno comum: nada")
	await app.selection.pick(app.rig.camera.unproject_position(Grid.to_world(Vector2i(-40, -40))), false)
	picker.free()
	app.queue_free()


func test_tab_cycles_the_buildings_stacked_under_the_cursor() -> void:
	var app = await city_app(tree)
	var camera: Camera3D = app.rig.camera
	var view_dir := -camera.global_transform.basis.z
	var back := Grid.to_world(Vector2i(4, 0), 0.5)
	var front_cell := Grid.to_cell(back - view_dir * (3.0 / -view_dir.y))
	app.stream.delta_applied.emit(O.delta({"buildingUpserts": [
		{"id": {"value": 31}, "buildingTypeId": 3, "location": {"x": 4, "y": 0}, "floors": 1, "groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 1}]},
		{"id": {"value": 32}, "buildingTypeId": 3, "location": {"x": front_cell.x, "y": front_cell.y}, "floors": 5,
			"groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 1}]}]}))
	var screen: Vector2 = camera.unproject_position(back)
	var stack: Array = app.reading.stack_at(screen)
	check(stack.find(32) >= 0 and stack.find(31) > stack.find(32), "pilha do mais perto ao mais longe: %s" % [stack])
	var chosen: int = app.reading.cycle(screen)
	eq(chosen, stack[1], "Tab vai para o de trás")
	check(app.world_view.buildings.is_faded(stack[0]) and app.world_view.buildings.passes_mouse(stack[0]), "o da frente abre e o mouse atravessa")
	app.selection.update_fade({})
	check(app.world_view.buildings.is_faded(stack[0]), "continua aberto enquanto o cursor está na pilha")
	for i in stack.size() - 1:
		app.reading.cycle(screen)
	eq(app.reading.picked(), stack[0], "dá a volta")
	eq(app.reading.cycle(screen, -1), stack[stack.size() - 1], "Shift+Tab / Alt+roda para cima volta")
	app.reading.on_hover(Vector2(-500, -500))
	eq(app.reading.pushed, {}, "o cursor sai da pilha: acabou a alternância")
	app.queue_free()


func test_xray_turns_every_building_translucent_but_keeps_it_clickable() -> void:
	var app = await city_app(tree)
	Shortcuts.handle(app, _key(KEY_X))
	var renderer = app.world_view.buildings
	check(renderer.xray, "X liga o raio-x")
	for child in renderer.nodes[4].find_children("*", "GeometryInstance3D", true, false):
		check(child.transparency >= renderer.XRAY - 0.001, "%s translúcido" % child.name)
		if child.name.begins_with("Roof"):
			check(child.transparency >= renderer.XRAY_ROOF - 0.001, "telhado quase some")
	check(not renderer.passes_mouse(4), "no raio-x o prédio continua clicável")
	app.stream.delta_applied.emit(O.delta({"buildingUpserts": [{"id": {"value": 41}, "buildingTypeId": 3, "location": {"x": 5, "y": 2},
		"floors": 1, "groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 1}]}]}))
	check(renderer.nodes[41].find_children("*", "GeometryInstance3D", true, false)[0].transparency > 0.5, "prédio novo já nasce translúcido")
	Shortcuts.handle(app, _key(KEY_X))
	eq(renderer.nodes[4].find_children("*", "GeometryInstance3D", true, false)[0].transparency, 0.0, "X de novo volta ao normal")
	app.queue_free()


func test_plan_shows_streets_lots_and_buildings_with_legend_and_esc_closes_it() -> void:
	var app = await city_app(tree)
	Shortcuts.handle(app, _key(KEY_P))
	var plan = app.reading.plan
	check(plan.visible, "P abre a planta")
	eq(plan.entity_at(Vector2i(2, 4)), Registry.ref(Registry.STREET, "2,4"), "rua")
	eq(plan.entity_at(Vector2i(1, 1)), Registry.ref(Registry.BUILDING, 4), "prédio")
	eq(plan.entity_at(Vector2i(3, 1)), Registry.ref(Registry.BUILDING, 4), "quintal leva ao prédio")
	eq(plan.entity_at(Vector2i(4, 2)), {}, "terreno comum")
	check(plan.area.has_point(Vector2i(0, 5)) and plan.area.has_point(Vector2i(5, 5)), "a planta cobre a cidade: %s" % plan.area)
	plan.queue_redraw()
	await tree.process_frame
	var cell_center: Vector2 = plan.cell_rect(Vector2i(2, 4)).get_center()
	eq(plan.cell_at(cell_center), Vector2i(2, 4), "tela -> célula")
	plan.update_tooltip(cell_center)
	check(app.tooltip.visible and "Rua principal" in app.tooltip_label.text, "dica na planta")
	Shortcuts.escape(app)
	check(not plan.visible, "Esc fecha a planta antes de tudo")
	app.queue_free()


func test_roads_refresh_without_reloading_the_scene() -> void:
	var fake = transport()
	var app = await city_app(tree, fake)
	var node: Node3D = app.world_view.building_nodes[4]
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": world_with_roads([road(4, 2, 1, 5)])})
	await app.road_sync.refresh()
	eq(app.road_sync.refreshes, 1, "a camada mudou")
	app.world_view._process(0.016)
	eq(app.world_view.ground.entity_at(Vector2i(4, 2)), Registry.ref(Registry.STREET, "4,2"), "rua nova desenhada")
	check(is_instance_valid(node) and app.world_view.building_nodes[4] == node, "sem recarregar a cidade")
	await app.road_sync.refresh()
	eq(app.road_sync.refreshes, 1, "mesma camada: nada muda")
	var before: int = fake.count_calls("GET /visual/subscribe?scope=World")
	app.stream.delta_applied.emit(O.delta({"buildingUpserts": [{"id": {"value": 51}, "buildingTypeId": 3, "location": {"x": 5, "y": 1},
		"floors": 1, "groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 2}], "lot": {"croft": [], "frontPath": [], "street": {"x": 6, "y": 1}}}]}))
	app.road_sync._process(1.0)
	app.road_sync._process(1.1)
	await tree.process_frame
	eq(fake.count_calls("GET /visual/subscribe?scope=World"), before + 1, "lote com rua desconhecida pede a camada logo")
	app.queue_free()


func test_building_labels_are_readable_up_close_and_hidden_from_afar() -> void:
	var app = await city_app(tree)
	var labels = app.world_view.building_labels
	eq(labels.labels[4].text, "Padaria", "função/tipo de local do prédio")
	labels.set_zoom(20.0, true)
	check(not labels.labels[4].visible, "de longe não polui")
	labels.set_zoom(8.0, true)
	check(labels.labels[4].visible, "de perto aparece")
	labels.set_zoom(8.0, false)
	check(not labels.labels[4].visible, "no mapa-múndi não")
	app.queue_free()


static func _key(code: Key) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = true
	return event
