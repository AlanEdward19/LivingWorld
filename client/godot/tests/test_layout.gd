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


## AD-197s: encarnado, a translucidez é a do braço de mola; a conta ortográfica do espectador (que ignora o prédio em que o corpo está)
## rodava a cada quadro e apagava a dela: o prédio do corpo ficava opaco na cara da câmera.
func test_embodied_person_inside_a_building_keeps_it_open_every_frame() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	await app.select_npc(7)
	var ana: Node3D = app.world_view.npc_nodes[7]
	var box: AABB = app.world_view.building_nodes[4].get_meta("box")
	ana.position = Vector3(box.get_center().x, 0.0, box.get_center().z)
	app.rig.set_mode(app.rig.THIRD)
	app.rig.follow(ana)
	for i in 3:
		app.rig._process(0.016)
		app.selection.update_fade({}) # o app chama isto todo quadro
	check(app.world_view.buildings.is_faded(4), "o prédio do corpo continua aberto depois da conta do espectador")
	check(app.world_view.buildings.opened.has(4), "e o telhado abre")
	var roof: GeometryInstance3D = app.world_view.building_nodes[4].find_children("Roof*", "GeometryInstance3D", true, false)[0]
	check(roof.transparency >= app.world_view.buildings.ROOF_OPEN - 0.001, "o telhado quase some (%s)" % roof.transparency)
	# a pessoa nova muda o estado do prédio: ele é redesenhado e a translucidez e o telhado aberto têm de sobreviver ao desenho novo
	app.world_view.buildings.upsert(app.store.buildings[4], {"function": "forge", "closed": true})
	var redrawn: GeometryInstance3D = app.world_view.building_nodes[4].find_children("Roof*", "GeometryInstance3D", true, false)[0]
	check(app.world_view.buildings.is_faded(4) and redrawn.transparency >= app.world_view.buildings.ROOF_OPEN - 0.001, "redesenhado, o prédio segue aberto (%s)" % redrawn.transparency)
	app.rig.set_mode(app.rig.ISO)
	app.selection.update_fade({})
	check(not app.world_view.buildings.is_faded(4), "ao sair do personagem, volta ao normal")
	eq(redrawn.transparency, 0.0)
	app.queue_free()


func test_croft_only_where_the_engine_sent_one_and_party_walls_have_no_gap() -> void:
	# AD-177: quintal só com `lot.croft`; casa sem quintal e em parede-meia desenhadas como são (nada entre elas)
	var store := Store.new()
	var env := T.city()
	var buildings: Array = env.payload.buildings
	buildings[0]["lot"] = {"croft": [{"x": 3, "y": 1}, {"x": 3, "y": 2}], "frontPath": [{"x": 1, "y": 3}], "street": {"x": 1, "y": 4}}
	buildings.append({"id": {"value": 5}, "buildingTypeId": 3, "location": {"x": 0, "y": 1}, "orientation": 0, "floors": 1,
		"groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 2}], "value": 100, "lot": null})
	buildings.append({"id": {"value": 6}, "buildingTypeId": 3, "location": {"x": 0, "y": 0}, "orientation": 0, "floors": 1,
		"groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 2}], "value": 100, "lot": {"croft": [], "frontPath": [], "street": null}})
	store.apply_snapshot(env)
	var ground := preload("res://Scenes/render/ground_renderer.gd").new()
	ground.store = store
	ground.rebuild()
	eq(ground.lot_cells.keys().filter(func(c): return ground.lot_cells[c].kind == "croft"), [Vector2i(3, 1), Vector2i(3, 2)],
		"quintal só nas células que o motor mandou")
	eq(ground.lot_cells.get(Vector2i(1, 3), {}).get("kind", ""), "path", "caminho da porta")
	for cell in [Vector2i(0, 0), Vector2i(0, 1), Vector2i(2, 0), Vector2i(4, 4), Vector2i(5, 5)]:
		eq(ground.entity_at(cell), {}, "sem quintal inventado em %s" % cell)
	check(ground.lot_pieces >= 2 + 4 + 1, "chão do quintal, cerca e caminho: %d peças" % ground.lot_pieces)
	eq(store.buildings[5].lot, {}, "`lot` nulo = sem lote")
	eq(store.buildings[6].lot.street, null, "`street` nulo não quebra")
	eq(preload("res://Scenes/inspector_describe.gd").party_walls(store, 5), [4, 6], "parede-meia com os vizinhos colados")
	ground.free()


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


## AD-197s2: na 1ª pessoa quem está a menos de 0,6 m do olho não se desenha (só o desenho); ao afastar ou sair, volta. Na 3ª pessoa por cima
## do telhado o próprio corpo leva o anel.
func test_first_person_hides_whoever_is_at_the_eye_and_top_view_marks_the_own_body() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	var ana: Node3D = app.world_view.npc_nodes[7]
	var beto: Node3D = app.world_view.npc_nodes[8]
	ana.position = Vector3(5.5, 0, 5.5)
	beto.position = Vector3(5.5 + app.rig.near_person() * 0.5, 0, 5.5)
	app.world_view.npcs.cell_members.clear()
	app.world_view.npcs.cell_members[Vector2i(5, 5)] = [7, 8]
	app.player_hud.npc_id = 7
	app.player_hud.is_active = true
	app.rig.follow(ana)
	app.rig.set_mode(app.rig.FIRST)
	app._player_view_effects()
	check(not beto.visible, "quem está colado no olho some")
	check(ana.visible, "o próprio corpo não é tocado")
	beto.position = Vector3(5.5 + app.rig.near_person() * 2.0, 0, 5.5)
	app._player_view_effects()
	check(beto.visible, "afastou: volta a se desenhar")
	beto.position = Vector3(5.5 + app.rig.near_person() * 0.5, 0, 5.5)
	app._player_view_effects()
	app.rig.set_mode(app.rig.THIRD)
	var cam: Vector3 = app.rig.camera.global_position
	var toward := Vector3(cam.x - ana.position.x, 0, cam.z - ana.position.z).normalized()
	beto.position = ana.position - toward * app.rig.near_person() * 0.8 # na frente do corpo, longe da câmera
	app._player_view_effects()
	check(beto.visible, "em 3ª pessoa, quem está à frente do corpo continua à vista")
	beto.position = ana.position + toward * app.rig.near_person() * 0.8 # #224: entre a câmera e o corpo
	app._player_view_effects()
	check(not beto.visible, "e quem tapa o corpo do lado da câmera some")
	beto.position = Vector3(5.5 + app.rig.near_person() * 2.0, 0, 5.5)
	app.rig._opened = {4: true}
	app._player_view_effects()
	check(ana.marked, "de cima, dentro de casa, o próprio corpo leva o anel")
	app.rig._opened = {}
	app._player_view_effects()
	check(not ana.marked, "fora da casa o anel some")
	app.player_hud.is_active = false
	app.queue_free()
