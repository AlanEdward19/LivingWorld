extends "res://tests/test_base.gd"
## AD-188 blocos 2 a 5: rota com saída explícita e trechos por cima (AD-185), acesso do lote pela geometria e pelo estado,
## motivo nos eventos, fundador da cidade, motivos do cérebro, alvo da seleção, corpo do prédio pela função e animais que
## deslizam entre deltas. Formas de tests/contracts/shape.city-subscribe.json, visual-delta.json e entity-city.json.

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const S := preload("res://tests/test_street_life.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Parse := preload("res://Store/observable_parse.gd")
const Dress := preload("res://Scenes/render/building_dress.gd")
const Describe := preload("res://Scenes/inspector_describe.gd")
const BrainPanel := preload("res://Scenes/brain_panel.gd")
const Validator := preload("res://Contract/shape_validator.gd")


func test_route_departed_tick_is_the_start_and_entering_mid_trip_lands_on_the_route() -> void:
	var app = await S.city_app(tree)
	var walker := S.walk_payload(9, S.xy([[0, 3], [1, 3], [2, 3], [3, 3], [4, 3]]), 15, 20)
	walker["routeDepartedTick"] = 10
	walker["observable"] = O.observable("Caio")
	app.world_view.npcs.clock.tick = 15.0
	app.stream.delta_applied.emit(S.delta(15, {"npcUpserts": [walker]}))
	var actor = app.world_view.npcs.nodes[9]
	eq(actor.walk.start_tick, 10.0, "a saída é a `routeDepartedTick`, não o início da ação")
	check(absf(actor.position.x - 2.5) < 0.3, "entrou no escopo na metade da viagem: já no meio da rota (x=%.2f)" % actor.position.x)
	app.queue_free()


func test_route_over_cells_walk_on_the_roof_of_the_building_below() -> void:
	var app = await S.city_app(tree)
	var walker := S.walk_payload(9, S.xy([[0, 1], [1, 1], [2, 1], [3, 1]]), 0, 30)
	walker["routeDepartedTick"] = 0
	walker["routeOver"] = [1, 2, 99]
	walker["observable"] = O.observable("Caio")
	eq(Validator.validate(walker.routeOver, Validator.load_shape("visual-delta").payload.npcUpserts[0]["routeOver?"]), [], "forma")
	app.stream.delta_applied.emit(S.delta(1, {"npcUpserts": [walker]}))
	var info: Dictionary = app.store.npcs[9]
	eq(info.route_over, [1, 2], "índice fora da rota descartado")
	eq(info.departed, 0)
	var walk = app.world_view.npcs.nodes[9].walk
	eq([walk.points[0].y, walk.points[1].y, walk.points[2].y], [0.0, 2.0, 2.0], "por cima do sobrado de 2 andares (prédio 4)")
	walk.step(10.0)
	check(walk.position.y > 1.0, "no meio da viagem anda no telhado")
	app.queue_free()


func test_lot_access_geometry_state_and_neighbour_are_drawn_and_listed() -> void:
	var snapshot := S.city("stair")
	snapshot.payload.buildings[0].lot["accessCells"] = [{"x": 1, "y": 0}, {"x": 0, "y": 0}]
	snapshot.payload.buildings[0].lot["accessViaBuildingId"] = 4
	snapshot.payload.buildings[0].lot["accessState"] = "reserved"
	var app = await S.city_app(tree, snapshot)
	var lot: Dictionary = app.store.buildings[4].lot
	eq([lot.access_cells, lot.access_via, lot.access_state], [[Vector2i(1, 0), Vector2i(0, 0)], 4, "reserved"])
	var with_geometry: int = app.world_view.ground.lot_pieces
	lot.access_cells = []
	app.world_view.ground.rebuild()
	check(with_geometry > app.world_view.ground.lot_pieces, "a geometria desenha laje, escada, traço e estacas")
	lot.access_cells = [Vector2i(1, 0), Vector2i(0, 0)]
	var built := Describe.lot(app.store, 4)
	var text := "\n".join(built.lines)
	check("2 células da soleira" in text and "ainda sem uso" in text, text)
	check(built.links.any(func(l): return l[0] == "Passa pelo vizinho"), "link do vizinho")
	app.queue_free()


func test_event_motive_shows_in_title_and_sheet_but_never_in_record_only() -> void:
	var raw := {"eventId": 5, "kind": 30, "label": "Herança repartida", "tick": 3, "involved": {"subjectId": 7, "otherId": 8,
		"motive": "xara", "recordOnly": false}}
	var event := Parse.event(raw, 0)
	eq(event.involved.motive, "xara")
	var registry := Registry.new()
	check("leva o nome de quem morreu" in registry.event_title(event), registry.event_title(event))
	registry.remember_event(event)
	var sheet := Describe.event(registry, 5, func(_t): return "dia 1")
	check("Motivo: leva o nome de quem morreu" in String(sheet.text), sheet.text)
	var hidden := Parse.event({"eventId": 6, "kind": 31, "label": "x", "tick": 3, "involved": {"motive": "ciume", "recordOnly": true, "caseId": 2}}, 0)
	check(not "ciúme" in registry.event_title(hidden), "registro só de crime não mostra motivo")
	eq(Registry.motive_text("algo-novo"), "algo novo", "motivo desconhecido vai como veio")


func test_city_founder_and_brain_motives() -> void:
	var founder := Describe.founder({"state": {"founderId": 12, "founderName": "Dora"}})
	eq(founder.line, "Fundada por Dora, que liderou a saída")
	eq(founder.link[1], Registry.ref(Registry.PERSON, 12))
	eq(Describe.founder({"state": {"founderId": null}}), {}, "cidade semeada: sem fundador")
	var panel := BrainPanel.new()
	panel.name_of = func(id: int) -> String: return {5: "Ana", 6: "Bia"}.get(id, "?")
	var text := panel.motives_text([{"term": 3, "motive": "procurar-companhia", "otherId": 5},
		{"term": 4, "motive": "acolher-crianca", "otherId": 6}, {"term": 3, "motive": "procurar-companhia", "otherId": -1}])
	check("companhia: Ana" in text and "acolhida (Bia)" in text and "vai ao largo" in text, text)
	panel.free()


func test_selected_person_gets_a_dashed_line_to_the_engine_target() -> void:
	var app = await S.city_app(tree)
	app.world_view.highlight_npc(7)
	app.store.npcs[7].intent_target = "building:4"
	app.world_view.route_line.rebuild([7], app.world_view.npcs.nodes)
	check(app.world_view.route_line.target_drawn != Vector3.INF, "tracejado até o prédio do alvo")
	app.store.npcs[7].intent_target = ""
	app.store.npcs[7].target = {}
	app.world_view.route_line.rebuild([7], app.world_view.npcs.nodes)
	eq(app.world_view.route_line.target_drawn, Vector3.INF, "sem alvo do motor, sem linha")
	app.queue_free()


func test_building_body_follows_function_and_windows_follow_the_engine_light() -> void:
	var smithy := Dress.body_of({"function": "workplace", "location_type_name": "Ferraria", "workers": [1]})
	var temple := Dress.body_of({"function": "workplace", "location_type_name": "Templo", "workers": []})
	check(float(temple.pitch) > float(smithy.pitch), "templo de telhado mais alto que a ferraria")
	eq(smithy.people, 1)
	# AD-189: a luz é o `lit` do motor (gente acordada dentro depois do pôr do sol), não uma conta de janelas por pessoa
	eq(Dress.lit_windows({"function": "workplace", "workers": [1, 2, 3], "lit": true}, 6), 6, "o motor diz aceso: todas")
	eq(Dress.lit_windows({"function": "dwelling", "residents": [1, 2, 3], "lit": false}, 6), 0, "todos dormindo: escura")
	eq(Dress.lit_windows({}, 6), 0, "sem estado do motor: escura")
	var app = await S.city_app(tree)
	var node: Node3D = app.world_view.building_nodes[4]
	eq(app.world_view.buildings.lit_count(4), 0, "o fixture não diz aceso")
	app.stream.delta_applied.emit(O.delta({"buildingStateUpserts": [O.building_state_lit(4, true)]}))
	check(app.world_view.buildings.lit_count(4) > 0, "o motor acendeu: as janelas acendem")
	check(is_instance_valid(node) and app.world_view.building_nodes[4] == node, "acender não redesenha o prédio")
	app.stream.delta_applied.emit(O.delta({"buildingStateUpserts": [O.building_state_lit(4, false)]}))
	eq(app.world_view.buildings.lit_count(4), 0, "foram dormir: apaga")
	app.queue_free()


func test_animals_glide_between_deltas_instead_of_jumping() -> void:
	var app = await S.city_app(tree)
	var fauna = app.world_view.fauna
	var start: Vector3 = fauna.drawn[90]
	app.stream.delta_applied.emit(O.delta({"animalUpserts": [{"id": 90, "species": "rabbit", "position": {"x": 2, "y": 5},
		"energy": 60, "carriesDisease": false}]}))
	check(fauna.moving.has(90), "o coelho desliza")
	eq(fauna.drawn[90], start, "ainda onde estava no quadro do delta")
	fauna.step(fauna.GLIDE / 2.0)
	check(fauna.drawn[90].x > start.x + 0.1 and fauna.drawn[90].x < start.x + 0.9, "no meio do caminho")
	fauna.step(fauna.GLIDE)
	check(not fauna.moving.has(90) and absf(fauna.drawn[90].x - start.x - 1.0) < 0.01, "chegou na célula nova")
	app.stream.delta_applied.emit(O.delta({"animalUpserts": [{"id": 90, "species": "rabbit", "position": {"x": 9, "y": 5},
		"energy": 60, "carriesDisease": false}]}))
	check(not fauna.moving.has(90), "salto longo não desliza")
	app.queue_free()
