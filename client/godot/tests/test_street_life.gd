extends "res://tests/test_base.gd"
## AD-181: a rua de verdade do motor e as pessoas andando por ela. Formas de tests/contracts/shape.city-subscribe.json
## (`livingState.roads`, `lot.access`, `npc.route`) e shape.visual-delta.json (`roadUpserts`/`roadRemoved`, eventos).

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Fx := preload("res://tests/fixtures.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Style := preload("res://Scenes/render/street_style.gd")
const Grid := preload("res://Scenes/grid.gd")
const Shortcuts := preload("res://Scenes/shortcuts.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Validator := preload("res://Contract/shape_validator.gd")
const Pieces := preload("res://Scenes/render/street_pieces.gd")

const TRAVEL := 1


static func road(x: int, y: int, surface: int, users: int, extra := {}) -> Dictionary:
	var body := {"cell": {"x": x, "y": y}, "city": {"value": "city-a"}, "surface": surface, "users": users, "main": false,
		"square": false, "wear": users * 12, "useBand": 2 if users >= 30 else 1, "planned": false}
	body.merge(extra, true)
	return body


## Cidade do motor da AD-176: rua de terra usada, trilha, reserva do traçado sem uso, trecho em obra e prédio com acesso.
static func city(access := "stair") -> Dictionary:
	var env := O.city()
	env.payload.livingState["roads"] = [road(1, 4, 1, 40), road(2, 4, 0, 12),
		road(3, 4, 0, 0, {"planned": true, "wear": 0, "useBand": 0}),
		road(5, 4, 1, 50, {"work": {"surface": 3, "progressPct": 40, "repair": false, "payer": "frontage", "workId": 1, "workerIds": [7]}})]
	env.payload.buildings[0]["lot"] = {"croft": [], "frontPath": [{"x": 1, "y": 3}], "street": {"x": 1, "y": 4}, "access": access}
	return env


static func transport(snapshot := city()) -> Object:
	var fake = O.transport()
	fake.on("GET /visual/subscribe?scope=City", {"status": 200, "body": snapshot})
	return fake


static func city_app(tree: SceneTree, snapshot := city()) -> Node:
	var app = await T.make_app(tree, transport(snapshot))
	await app.enter_scope("City", "city-a")
	app.world_view.npcs.clock.paused = true # o teste dita a hora do mundo
	return app


static func delta(tick: int, payload: Dictionary) -> Dictionary:
	var envelope := O.delta(payload)
	envelope.payload.atMs = tick * 3600000
	return envelope


static func walk_payload(id: int, cells: Array, started: int, arrival: int) -> Dictionary:
	return {"id": {"value": id}, "location": {"x": cells[0].x, "y": cells[0].y}, "currentAction": TRAVEL,
		"city": {"value": "city-a"}, "actionStartedAtMs": started * 3600000, "route": cells, "routeArrivalAtMs": arrival * 3600000}


static func xy(list: Array) -> Array:
	return list.map(func(c): return {"x": c[0], "y": c[1]})


func test_fixtures_follow_the_contract_shapes() -> void:
	var delta_shape: Dictionary = Validator.load_shape("visual-delta").payload
	var city_shape: Dictionary = Validator.load_shape("city-subscribe").payload.livingState
	eq(Validator.validate(city().payload.livingState.roads, city_shape.roads), [], "ruas do retrato")
	eq(Validator.validate([road(5, 4, 1, 1, {"work": {"surface": 2, "progressPct": 3, "repair": true, "payer": "treasury", "workId": 2, "workerIds": []}})],
		delta_shape.roadUpserts), [], "roadUpserts")
	eq(Validator.validate([{"x": 1, "y": 2}], delta_shape.roadRemoved), [], "roadRemoved")
	var walker := walk_payload(7, xy([[3, 3], [2, 3]]), 10, 12)
	eq(Validator.validate(walker.route, delta_shape.npcUpserts[0]["route?"]), [], "rota do NPC")


func test_roads_arrive_with_the_scope_and_planned_reserve_is_not_a_street() -> void:
	var fake = transport()
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	var ground = app.world_view.ground
	check(app.store.roads_live, "o escopo mandou `livingState.roads`")
	eq(app.store.roads.size(), 4, "as quatro células do retrato")
	check(ground.drawn_roads.has(Vector2i(1, 4)) and ground.drawn_roads.has(Vector2i(5, 4)), "rua usada e obra desenhadas")
	check(not ground.drawn_roads.has(Vector2i(3, 4)) and ground.reserved.has(Vector2i(3, 4)), "reserva sem uso não é rua")
	eq(ground.entity_at(Vector2i(3, 4)), {}, "nem é clicável como rua")
	var before: int = fake.count_calls("GET /visual/subscribe?scope=World")
	app.road_sync._process(40.0)
	await tree.process_frame
	eq(fake.count_calls("GET /visual/subscribe?scope=World"), before, "com rua por delta, a releitura de 30 s não roda")
	Shortcuts.handle(app, _key(KEY_P))
	app.reading.plan.queue_redraw()
	await tree.process_frame
	check(app.reading.plan.reserved.has(Vector2i(3, 4)), "a planta mostra a reserva como traço discreto")
	app.queue_free()


func test_street_is_born_changes_and_disappears_by_delta_with_a_smooth_transition() -> void:
	var app = await city_app(tree)
	var ground = app.world_view.ground
	var node: Node3D = app.world_view.building_nodes[4]
	app.stream.delta_applied.emit(delta(11, {"roadUpserts": [road(4, 5, 0, 15), road(1, 4, 3, 40, {"condition": 900})],
		"roadRemoved": [{"x": 2, "y": 4}]}))
	app.world_view._process(0.016)
	check(ground.drawn_roads.has(Vector2i(4, 5)), "rua nova nasce sem recarregar")
	eq(int(ground.drawn_roads[Vector2i(1, 4)].surface), 3, "terra batida virou pedra")
	check(not ground.drawn_roads.has(Vector2i(2, 4)), "a trilha que o motor tirou sumiu")
	check(ground.anims.has(Vector2i(4, 5)) and ground.anims.has(Vector2i(1, 4)) and ground.anims[Vector2i(2, 4)].leaving,
		"nascer, mudar e sumir são transições: %s" % [ground.anims.keys()])
	check(float(ground._shown[Vector2i(4, 5)][0]) < Style.style_of(ground.drawn_roads[Vector2i(4, 5)]).width, "começa estreita")
	for i in 6:
		ground.step(0.2)
	eq(ground.anims, {}, "transição acaba")
	check(is_equal_approx(float(ground._shown[Vector2i(4, 5)][0]), float(Style.style_of(ground.drawn_roads[Vector2i(4, 5)]).width)), "largura final")
	check(not ground._shown.has(Vector2i(2, 4)), "a que sumiu não fica desenhada")
	check(is_instance_valid(node) and app.world_view.building_nodes[4] == node, "nada recarregou")
	app.queue_free()


func test_use_band_wear_and_condition_change_the_ground() -> void:
	var store_road := func(extra: Dictionary) -> Dictionary:
		var parsed = preload("res://Store/city_ground.gd").road(road(1, 1, 1, 0, extra))
		return parsed
	var quiet: Dictionary = Style.style_of(store_road.call({"useBand": 0, "wear": 10}))
	var busy: Dictionary = Style.style_of(store_road.call({"useBand": 4, "wear": 1500}))
	check(busy.width > quiet.width, "mais passagens, mais larga")
	check(quiet.tufts > busy.tufts, "pouco pisada, o capim volta na beira")
	check(Color(quiet.color) != Color(busy.color), "o pisoteio muda a cor do chão")
	var paved: Dictionary = Style.style_of(store_road.call({"surface": 3, "condition": 950}))
	var broken: Dictionary = Style.style_of(store_road.call({"surface": 3, "condition": 150}))
	check(paved.broken == 0.0 and broken.broken > 0.5, "calçamento estragado")
	var lines := "\n".join(Style.lines_of(store_road.call({"surface": 3, "condition": 150})))
	check("Calçamento: 15% de condição · estragado" in lines, lines)


func test_street_work_shows_progress_sheet_and_becomes_marker_and_timeline_entry() -> void:
	var app = await city_app(tree)
	await app.inspect(Registry.ref(Registry.STREET, "5,4"))
	var text: String = app.inspector.text
	check("Em obra: obra nova para pedra · 40% feito" in text, text)
	check("as casas de frente" in text and "1 pessoa(s)" in text, text)
	var hover: String = app.selection.describe(Registry.ref(Registry.STREET, "5,4"))
	check("em obra: pedra · 40%" in hover and "passagens no último mês" in hover, hover)
	var pile_40 := _pile_height(Style.style_of(app.store.roads[Vector2i(5, 4)]))
	app.stream.delta_applied.emit(delta(11, {"roadUpserts": [road(5, 4, 1, 50, {"work": {"surface": 3, "progressPct": 80,
		"repair": true, "payer": "treasury", "workerIds": [7, 8]}})],
		"events": [{"eventId": 900, "kind": 101, "label": "Começou uma obra de calçamento na rua", "atMs": 11 * 3600000,
			"involved": {"recordOnly": false, "cityId": "city-a"}}]}))
	await app.inspect(Registry.ref(Registry.STREET, "5,4"))
	text = app.inspector.text
	check("conserto para pedra · 80%" in text and "o tesouro da cidade" in text and "2 pessoa(s)" in text, text)
	eq(app.inspector.link_refs.filter(func(r): return r.kind == Registry.PERSON).map(func(r): return int(r.id)), [7, 8],
		"gancho: quem trabalha na obra vira link quando o motor mandar")
	check(app.world_view.events.positions.has(900), "evento de obra sem lugar vira marcador na obra")
	eq(Grid.to_cell(app.world_view.events.positions[900]), Vector2i(5, 4), "na célula da obra")
	check(app.registry.events.has(900), "e entra na linha do tempo")
	check(_pile_height(Style.style_of(app.store.roads[Vector2i(5, 4)])) < pile_40, "a pilha de material baixa conforme a obra anda")
	app.stream.delta_applied.emit(delta(12, {"roadUpserts": [road(5, 4, 3, 50, {"condition": 1000})],
		"events": [{"eventId": 901, "kind": 102, "label": "Uma rua ficou calçada", "atMs": 12 * 3600000, "involved": {"recordOnly": false}}]}))
	app.world_view._process(0.016)
	eq(Grid.to_cell(app.world_view.events.positions.get(901, Vector3.ZERO)), Vector2i(5, 4), "fim da obra: marcador onde ela acabou")
	check(Style.work_of(app.world_view.ground.drawn_roads[Vector2i(5, 4)]).is_empty(), "obra acabou: sem pilha nem estacas")
	app.queue_free()


## Altura da pilha de material desenhada numa célula em obra (somando as caixas da pilha).
static func _pile_height(style: Dictionary) -> float:
	var sizes: Array = []
	var record := func(_at: Vector3, size: Vector3, color: Color) -> void:
		if color != Style.WORK_STYLE.stake and size.x < 0.3:
			sizes.append(size.y)
	Pieces.work(record, Vector2i(5, 4), Vector3.ZERO, style)
	return sizes.reduce(func(a, b): return a + b, 0.0)


func test_house_access_is_drawn_and_told_in_the_sheet() -> void:
	var pieces := {}
	for access in ["street", "alley", "stair", "roof", "none", "tunnel"]:
		var app = await city_app(tree, city(access))
		pieces[access] = app.world_view.ground.lot_pieces
		await app.inspect(Registry.ref(Registry.BUILDING, 4))
		var text: String = app.inspector.text
		match access:
			"stair":
				check("Como se chega: por uma escada encostada" in text, text)
			"roof":
				check("passarela" in text, text)
			"alley":
				check("viela" in text and "viela" in app.selection.describe(Registry.ref(Registry.BUILDING, 4)), text)
			"none":
				check("⚠ Como se chega: SEM ACESSO" in text, text)
			"tunnel":
				check("passagem (tunnel)" in text, "acesso desconhecido não quebra: " + text)
		app.queue_free()
		await tree.process_frame
	for access in ["alley", "stair", "roof"]:
		check(pieces[access] > pieces.street, "%s desenhado como tal (%s)" % [access, pieces])
	eq(pieces.none, pieces.street, "sem acesso: nada inventado no chão (a ficha alerta)")


func test_person_walks_the_route_and_arrives_on_the_arrival_tick() -> void:
	var app = await city_app(tree)
	var npcs = app.world_view.npcs
	var cells := xy([[3, 3], [2, 3], [1, 3], [0, 3]])
	app.stream.delta_applied.emit(delta(11, {"npcUpserts": [walk_payload(7, cells, 10, 13)]}))
	var actor = npcs.nodes[7]
	check(actor.walk != null, "rota do motor: o boneco anda por ela")
	npcs.clock.tick = 11.5
	npcs.advance(0.016, Vector3.ZERO, 40.0, false, false)
	check(actor.position.distance_to(Vector3(2.0, 0, 3.5)) < 0.2, "na metade do tempo, na metade do caminho: %s" % actor.position)
	check(absf(actor.position.z - 3.5) > 0.05, "anda pela direita da rua (desvio visual)")
	var screen: Vector2 = app.rig.camera.unproject_position(actor.global_position + Vector3(0, 0.6, 0))
	eq(app.selection.nearest_npc(screen), 7, "o clique pega o corpo onde ele está desenhado")
	NpcActor.frozen = true # pausa
	var held: Vector3 = actor.position
	npcs.clock.tick = 12.5
	npcs.advance(0.5, Vector3.ZERO, 40.0, false, false)
	eq(actor.position, held, "pausa congela")
	NpcActor.frozen = false
	npcs.clock.tick = 13.0
	npcs.advance(0.016, Vector3.ZERO, 40.0, false, false)
	check(actor.position.distance_to(Grid.to_world(Vector2i(0, 3))) < 0.01, "chega no tick da chegada: %s" % actor.position)
	app.stream.delta_applied.emit(delta(13, {"npcUpserts": [{"id": {"value": 7}, "location": {"x": 0, "y": 3}, "currentAction": 2,
		"actionStartedAtMs": 13 * 3600000}]}))
	check(actor.walk == null and actor.cell == Vector2i(0, 3), "o motor o moveu: fim da rota, comportamento de sempre")
	check(actor.position.distance_to(Grid.to_world(Vector2i(0, 3))) < 0.4, "sem salto ao chegar")
	app.queue_free()


func test_high_speed_and_pause_follow_the_world_clock() -> void:
	var app = await city_app(tree)
	var npcs = app.world_view.npcs
	app.stream.delta_applied.emit(delta(11, {"npcUpserts": [walk_payload(8, xy([[4, 3], [4, 4], [4, 5], [5, 5], [5, 4]]), 11, 15)]}))
	var actor = npcs.nodes[8]
	npcs.clock.set_status(11, 200.0, false) # 200×
	npcs.advance(0.05, Vector3.ZERO, 40.0, false, false)
	eq(npcs.clock.tick, 12.0, "nunca mais de um tick à frente do servidor")
	for tick in [12, 13, 14, 15]:
		app.stream.delta_applied.emit(delta(tick, {}))
		npcs.advance(0.05, Vector3.ZERO, 40.0, false, false)
	check(actor.walk.done and actor.position.distance_to(Grid.to_world(Vector2i(5, 4))) < 0.01, "a 200× chega e não quebra")
	npcs.clock.set_status(15, 1.0, true)
	var tick_before: float = npcs.clock.tick
	npcs.advance(3.0, Vector3.ZERO, 40.0, false, false)
	eq(npcs.clock.tick, tick_before, "pausado o relógio da rota para")
	app.queue_free()


func test_selected_or_followed_route_is_drawn_and_follow_tracks_the_walker() -> void:
	var app = await city_app(tree)
	app.stream.delta_applied.emit(delta(11, {"npcUpserts": [walk_payload(7, xy([[3, 3], [3, 4], [3, 5]]), 11, 13),
		walk_payload(8, xy([[4, 3], [5, 3]]), 11, 12)]}))
	var line = app.world_view.route_line
	await app.select_npc(7)
	app._highlight(7)
	app.world_view.route_line.step(0.0, [app.world_view.npcs.selected, -1], app.world_view.npc_nodes, true)
	eq(line.drawn_for, [7], "rota de quem está selecionado")
	check(line.segments >= 3, "linha no chão até a chegada (%d traços)" % line.segments)
	app.follow(Registry.ref(Registry.PERSON, 8))
	app._process(0.016)
	app.world_view.route_line.step(0.0, [app.world_view.npcs.selected, app.world_view.followed_npc], app.world_view.npc_nodes, true)
	eq(line.drawn_for, [7, 8], "e de quem é seguido")
	app.director.step(0.016)
	eq(app.rig.following, app.world_view.npc_nodes[8], "F acompanha o boneco pela rota")
	app.queue_free()


func test_plan_shows_people_works_accesses_and_heat_map() -> void:
	var app = await city_app(tree)
	app.stream.delta_applied.emit(delta(11, {"npcUpserts": [walk_payload(7, xy([[3, 3], [2, 3]]), 11, 12)]}))
	Shortcuts.handle(app, _key(KEY_P))
	var plan = app.reading.plan
	plan.queue_redraw()
	await tree.process_frame
	check(plan.people.has(7) and plan.people.has(8), "a planta mostra as pessoas: %s" % [plan.people.keys()])
	eq(plan.entity_at_point(plan.people[8]), Registry.ref(Registry.PERSON, 8), "clique no ponto abre a pessoa")
	Shortcuts.handle(app, _key(KEY_H))
	check(app.world_view.ground.heat, "H liga o mapa de calor do pisoteio")
	plan.queue_redraw()
	await tree.process_frame
	eq(app.world_view.ground.top_wear, 600, "escala pelo mais pisado")
	Shortcuts.handle(app, _key(KEY_H))
	check(not app.world_view.ground.heat, "H de novo desliga")
	app.queue_free()


func test_unknown_fields_and_odd_values_are_tolerated() -> void:
	var app = await city_app(tree)
	app.stream.delta_applied.emit(delta(11, {
		"roadUpserts": [road(0, 0, 9, 3, {"mud": 2, "useBand": 99, "work": {"surface": 7, "progressPct": 250, "crew": "x"}}),
			{"cell": null}, "lixo"],
		"roadRemoved": [{"x": 40, "y": 40}, null],
		"npcUpserts": [{"id": {"value": 8}, "location": {"x": 4, "y": 3}, "currentAction": TRAVEL, "actionStartedAtMs": 11 * 3600000,
			"route": [{"x": 4, "y": 3}, "?", {"y": 1}], "routeArrivalAtMs": 12 * 3600000, "gait": "manco"}]}))
	app.world_view._process(0.016)
	var odd: Dictionary = app.store.roads[Vector2i(0, 0)]
	eq(odd.extra.get("mud"), 2, "campo desconhecido guardado")
	eq([odd.use_band, odd.work.progress], [4, 100], "valores fora da faixa presos na faixa")
	check(app.world_view.npcs.nodes[8].walk == null, "rota com menos de duas células válidas: não anda")
	await app.inspect(Registry.ref(Registry.STREET, "0,0"))
	check("Passagem" in app.inspector._title.text, app.inspector._title.text)
	app.queue_free()


func test_season_reaches_streets_and_crofts() -> void:
	var colors := []
	for climate in [{}, {"month": 1, "seasonIndex": 3, "seasonKey": "winter", "temperatureC": -6.0, "regionId": 0,
			"cropYearFactorMilli": 1000, "drought": false, "weather": {"precipitation": "none", "ground": "snow", "snowCm": 8.0}}]:
		var snapshot := city()
		snapshot.payload.livingState.climate = climate if not climate.is_empty() else snapshot.payload.livingState.climate
		var app = await city_app(tree, snapshot)
		var ground = app.world_view.ground
		colors.append([ground._season(Style.SURFACES[1].color, true), ground._season(Style.CROFT_COLOR, false)])
		app.queue_free()
		await tree.process_frame
	eq(colors[0][0], Style.SURFACES[1].color, "verão sem seca: a rua como é")
	check(colors[1][0].get_luminance() > colors[0][0].get_luminance() + 0.1, "neve cobre a rua no inverno: %s" % [colors])
	check(colors[1][1] != colors[0][1], "o quintal muda com a estação")


static func _key(code: Key) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = true
	return event
