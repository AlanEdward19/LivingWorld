extends "res://tests/test_base.gd"
## Pedidos do dono ao ver o cliente (AD-171 itens 14–16): prédio que cobre fica translúcido, NPCs que dividem a célula se
## espalham e quem se cruza desvia, e o delta lido igual com chave ausente ou `null`.

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Store := preload("res://Store/world_store.gd")
const NpcRenderer := preload("res://Scenes/render/npc_renderer.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Registry := preload("res://Store/entity_registry.gd")


func test_people_sharing_a_cell_spread_out_and_the_click_takes_the_drawn_body() -> void:
	eq(NpcRenderer.spread_offsets([5]), {5: Vector3.ZERO}, "sozinho: no centro da célula")
	var offsets := NpcRenderer.spread_offsets([3, 11, 19])
	for a in offsets:
		for b in offsets:
			if a != b:
				check(offsets[a].distance_to(offsets[b]) > 0.15, "ninguém um dentro do outro")
	eq(NpcRenderer.spread_offsets([3, 11, 19]), offsets, "estável por id")
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	app.stream.delta_applied.emit(O.delta({"npcUpserts": [{"id": {"value": 8}, "location": {"x": 3, "y": 3}, "currentAction": 8}]}))
	for i in 60:
		app.world_view.npcs.advance(0.05, Vector3.ZERO, 40.0, false, false)
	var ana: Node3D = app.world_view.npc_nodes[7]
	var beto: Node3D = app.world_view.npc_nodes[8]
	eq(app.store.npcs[8].cell, Vector2i(3, 3), "a célula é a do servidor")
	check(ana.position.distance_to(beto.position) > 0.3, "na mesma célula, cada um no seu lugar: %s" % ana.position.distance_to(beto.position))
	var camera: Camera3D = app.rig.camera
	var at: Vector2 = camera.unproject_position(beto.global_position + Vector3(0, 0.6, 0))
	eq(app.selection.nearest_npc(at, 8.0), 8, "o clique pega o corpo onde ele está desenhado")
	app.queue_free()


func test_people_crossing_veer_to_their_right_instead_of_passing_through() -> void:
	var a := NpcActor.new()
	var b := NpcActor.new()
	tree.root.add_child(a)
	tree.root.add_child(b)
	a.setup(1, Vector2i(0, 0), 4, null)
	b.setup(2, Vector2i(1, 0), 4, null)
	a.move_to(Vector2i(1, 0), 1.0)
	b.move_to(Vector2i(0, 0), 1.0)
	a.advance_motion(0.5)
	b.advance_motion(0.5)
	check(absf(a.position.z - b.position.z) > 0.25, "no meio do cruzamento estão lado a lado: %s %s" % [a.position, b.position])
	a.advance_motion(0.6)
	check(a.position.is_equal_approx(Vector3(1.5, 0, 0.5)), "e chegam ao lugar certo: %s" % a.position)
	a.queue_free()
	b.queue_free()


func test_a_building_covering_the_selected_person_turns_translucent_and_back() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	await app.select_npc(7)
	var camera: Camera3D = app.rig.camera
	var view_dir := -camera.global_transform.basis.z
	var behind: Vector3 = app.world_view.building_center(4) + Vector3(0, 0.6, 0) + view_dir * 2.0
	var ana: Node3D = app.world_view.npc_nodes[7]
	ana.position = behind - Vector3(0, 0.6, 0)
	app.selection.update_fade({})
	check(app.world_view.buildings.is_faded(4), "o prédio na frente da pessoa selecionada abre")
	var mesh: GeometryInstance3D = app.world_view.building_nodes[4].find_children("*", "GeometryInstance3D", true, false)[0]
	check(is_equal_approx(mesh.transparency, app.world_view.buildings.FADE), "translucidez por instância (sem material novo)")
	ana.position = app.world_view.building_center(4) - view_dir * 6.0
	app.selection.update_fade({})
	check(not app.world_view.buildings.is_faded(4), "volta ao normal quando deixa de cobrir")
	eq(mesh.transparency, 0.0)
	var own: Array = app.selection.focus_points(Registry.ref(Registry.BUILDING, 4))
	check(own.any(func(p): return int(p[1]) == 4), "o próprio prédio em foco não cobre a si mesmo")
	app.queue_free()


func test_yards_surround_each_building_and_the_gap_between_buildings_is_street() -> void:
	var store := Store.new()
	store.apply_snapshot(T.city())
	var cells: Dictionary = preload("res://Scenes/render/terrain_renderer.gd").settlement_cells(store)
	eq(cells[Vector2i(1, 1)], preload("res://Scenes/render/terrain_renderer.gd").YARD_COLOR, "sob o prédio (posição absoluta)")
	eq(cells[Vector2i(0, 0)], preload("res://Scenes/render/terrain_renderer.gd").YARD_COLOR, "quintal em volta")
	eq(cells[Vector2i(5, 5)], preload("res://Scenes/render/terrain_renderer.gd").STREET_COLOR, "longe do prédio: rua")


func test_absent_keys_and_explicit_nulls_read_the_same() -> void:
	for style in ["absent", "null"]:
		var store := Store.new()
		store.apply_snapshot(O.city())
		var npc := {"id": {"value": 7}, "location": {"x": 2, "y": 2}}
		var building := {"id": {"value": 9}, "buildingTypeId": 3, "location": {"x": 4, "y": 0}}
		var process := {"id": 80, "kind": "rest"}
		var crop := {"id": 75, "plot": {"x": 5, "y": 1}, "stage": "planted"}
		var city := {"id": {"value": "city-a"}, "bounds": {"x": 0, "y": 0, "width": 6, "height": 6}}
		var event := {"tick": 3, "kind": 1, "label": "x"}
		if style == "null":
			for key in ["currentAction", "relocationDestination", "city", "appearance", "observable", "extraordinary"]:
				npc[key] = null
			for key in ["floors", "groundFootprint", "value", "lodging", "orientation", "cityId"]:
				building[key] = null
			for key in ["targetId", "progress", "descriptorKey", "location", "remainingHours", "quality"]:
				process[key] = null
			for key in ["resourceId", "resourceName", "fraction", "thirsty", "ownerId", "farmId", "expectedHarvest", "standing"]:
				crop[key] = null
			for key in ["name", "population", "location", "density"]:
				city[key] = null
			for key in ["eventId", "location", "involved"]:
				event[key] = null
		store.apply_delta(O.delta({"npcUpserts": [npc], "buildingUpserts": [building], "processUpserts": [process],
			"cropUpserts": [crop], "cityUpserts": [city], "events": [event], "moved": [{"npcId": {"value": 8}}]}))
		eq(store.npcs[7].action, Store.NO_ACTION, "%s: ação nula" % style)
		eq(store.npcs[7].city, "city-a", "%s: cidade de antes" % style)
		eq(store.npcs[7].obs.name, "Ana", "%s: observável de antes" % style)
		eq(store.buildings[9].floors, 1, "%s: andares padrão" % style)
		eq(store.processes[80].progress, 0.0, "%s: progresso padrão" % style)
		eq(store.processes[80].has_cell, false)
		eq(store.crops[75].owner_id, -1, "%s: lavoura sem dono" % style)
		eq(store.cities["city-a"].population, 0)
		eq(store.npcs[8].cell, Vector2i(4, 3), "%s: `moved` sem lugar não move" % style)
