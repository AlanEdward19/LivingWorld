extends "res://tests/test_base.gd"
## AD-193: o cliente mostra a obra de verdade. Formas de tests/contracts/shape.city-subscribe.json (`livingState.works`,
## `buildings[].work`) e shape.visual-delta.json (`workUpserts`/`workRemoved`, `actionTarget` `work`, `involved.workId`).

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Validator := preload("res://Contract/shape_validator.gd")
const Works := preload("res://Store/work_book.gd")

const CANTEIRO := 12
const WORK := 21


static func footprint(door := true) -> Array:
	var cells: Array = []
	for y in 2:
		for x in 2:
			cells.append({"cell": {"x": x, "y": y}, "material": 2 if door and x == 0 and y == 0 else 1})
	return cells


static func work_view(progress: float) -> Dictionary:
	return {"id": WORK, "kind": "casa", "location": {"x": 4, "y": 0}, "progress": progress, "stalled": false, "crew": 1, "helpers": 1,
		"offerPm": 1100, "startedAtMs": 2 * 3600000}


static func canteiro(progress: float, with_work := true) -> Dictionary:
	var body := {"id": {"value": CANTEIRO}, "cityId": {"value": "city-a"}, "buildingTypeId": -1, "location": {"x": 4, "y": 0},
		"orientation": 0, "floors": 1, "groundFootprint": footprint(), "value": 0}
	if with_work:
		body["work"] = work_view(progress)
	return body


static func work(progress: float, extra := {}) -> Dictionary:
	var body := {"id": WORK, "kind": "casa", "cityId": "city-a", "location": {"x": 4, "y": 0}, "progress": progress,
		"buildingId": CANTEIRO, "buildingTypeId": -1, "stalled": false, "payer": "casa", "ownerId": 8, "householdId": 3,
		"crew": [8], "helpers": [7], "onSite": [7], "hoursTotal": 480, "hoursLeft": int(480 * (1.0 - progress)), "hoursByCrew": 40,
		"hoursByHelpers": 60, "haulHoursLeft": 0, "paid": 90, "spent": 30, "offerPm": 1100, "startedAtMs": 2 * 3600000, "lastWorkedAtMs": 4 * 3600000,
		"materials": []}
	body.merge(extra, true)
	return body


## A cidade do inspetor com um canteiro de casa a 30% e a Ana (7) trabalhando no lote dela.
static func city() -> Dictionary:
	var env := O.city()
	env.payload.buildings.append(canteiro(0.3))
	env.payload.livingState["works"] = [work(0.3)]
	env.payload.livingState.npcs[0]["actionTarget"] = {"kind": "work", "id": str(WORK)}
	env.payload.livingState.npcs[0]["location"] = {"x": 4, "y": 0}
	return env


static func city_app(tree: SceneTree, snapshot := city()) -> Node:
	var fake = O.transport()
	fake.on("GET /visual/subscribe?scope=City", {"status": 200, "body": snapshot})
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	return app


static func delta(tick: int, payload: Dictionary) -> Dictionary:
	var envelope := O.delta(payload)
	envelope.payload.atMs = tick * 3600000
	return envelope


static func names_in(node: Node) -> Dictionary:
	var out := {}
	for child in node.find_children("*", "", true, false):
		var base := String(child.name).rstrip("0123456789").trim_suffix("@")
		out[base] = int(out.get(base, 0)) + 1
	return out


func test_fixtures_follow_the_contract_shapes() -> void:
	var city_shape: Dictionary = Validator.load_shape("city-subscribe").payload.livingState
	var delta_shape: Dictionary = Validator.load_shape("visual-delta").payload
	eq(Validator.validate(city().payload.livingState.works, city_shape.works), [], "obras do retrato")
	eq(Validator.validate([work(0.5, {"halt": "tempo", "materials": [{"resourceId": 3, "name": "madeira", "needed": 20, "placed": 5}]})],
		delta_shape.workUpserts), [], "workUpserts")
	eq(Validator.validate([WORK], delta_shape.workRemoved), [], "workRemoved")
	eq(Validator.validate([canteiro(0.3)], delta_shape.buildingUpserts), [], "canteiro com `work`")


func test_the_site_is_drawn_as_a_work_that_rises_with_the_fraction_done() -> void:
	var app = await city_app(tree)
	var node: Node3D = app.world_view.buildings.nodes[CANTEIRO]
	eq(int(node.get_meta("work_id", -1)), WORK, "o canteiro é desenhado como obra, não como casa pronta")
	var parts := names_in(node)
	check(parts.has("Wall") and parts.has("Scaffold") and parts.has("Foundation"), "alicerce, paredes subindo e andaime: %s" % [parts])
	check(not parts.has("Roof") and not parts.has("Rafter"), "sem telhado a 30%%: %s" % [parts])
	var wall: MeshInstance3D = node.find_children("Wall*", "MeshInstance3D", true, false)[0]
	check(absf((wall.mesh as BoxMesh).size.y - 0.3) < 0.01, "parede a 3/10 do andar: %s" % (wall.mesh as BoxMesh).size.y)
	var other: Node3D = app.world_view.buildings.nodes[4]
	check(not other.has_meta("work_id"), "o prédio pronto continua prédio")


func test_progress_redraws_only_when_the_drawn_tenth_changes_and_the_finished_house_appears() -> void:
	var app = await city_app(tree)
	var before: Node3D = app.world_view.buildings.nodes[CANTEIRO]
	var redrawn := [0]
	app.store.buildings_changed.connect(func(_ids): redrawn[0] += 1)
	app.stream.delta_applied.emit(delta(11, {"workUpserts": [work(0.33)], "buildingUpserts": [canteiro(0.33)]}))
	check(app.world_view.buildings.nodes[CANTEIRO] == before, "mesmo décimo: o canteiro não é refeito")
	eq(redrawn[0], 0, "prédio reenviado só porque a obra andou não refaz chão nem rótulos")
	app.stream.delta_applied.emit(delta(12, {"workUpserts": [work(0.9)], "buildingUpserts": [canteiro(0.9)]}))
	var late: Node3D = app.world_view.buildings.nodes[CANTEIRO]
	check(late != before, "outro décimo: redesenha")
	check(names_in(late).has("Rafter"), "a armação do telhado no último quinto: %s" % [names_in(late)])
	app.stream.delta_applied.emit(delta(13, {"workRemoved": [WORK], "buildingUpserts": [canteiro(1.0, false)]}))
	var done: Node3D = app.world_view.buildings.nodes[CANTEIRO]
	check(not done.has_meta("work_id") and names_in(done).has("Roof"), "pronta: a casa passa a existir na tela")
	check(app.store.works.is_empty(), "a obra saiu da store")


func test_material_carried_to_the_lot_shows_as_a_pile_that_shrinks() -> void:
	var queued := work(0.2, {"kind": "predio-3", "payer": "cidade", "materials": [{"resourceId": 3, "name": "madeira", "needed": 20, "placed": 4}]})
	var snapshot := city()
	snapshot.payload.livingState.works = [queued]
	var app = await city_app(tree, snapshot)
	var heap: int = names_in(app.world_view.buildings.nodes[CANTEIRO]).get("Material", 0)
	check(heap > 0, "material da fila no lote")
	var hauling := queued.duplicate(true)
	hauling.haulHoursLeft = 12
	app.stream.delta_applied.emit(delta(11, {"workUpserts": [hauling]}))
	eq(int(names_in(app.world_view.buildings.nodes[CANTEIRO]).get("Material", 0)), 0, "ainda carregando: nada no lote")
	eq(Works.pile_fraction(work(0.2)), 0.0, "casa: o motor não conta o material no lote, nada é desenhado")


func test_the_site_sheet_says_who_builds_for_whom_what_is_left_and_why_it_stopped() -> void:
	var snapshot := city()
	snapshot.payload.livingState.works = [work(0.3, {"halt": "tempo"})]
	var app = await city_app(tree, snapshot)
	await app.inspect(Registry.ref(Registry.BUILDING, CANTEIRO))
	var text: String = app.inspector.text
	for piece in ["Canteiro de obra", "casa", "30%", "Faltam 336 h", "Parada: chuva forte", "1 diarista", "1 ajudante", "No lote agora: Ana"]:
		check(piece in text, "'%s' na ficha: %s" % [piece, text])
	var refs: Array = app.inspector.link_refs.map(func(r): return Registry.key(r))
	for key in ["work:%d" % WORK, "person:8", "household:3", "person:7"]:
		check(key in refs, "link %s: %s" % [key, refs])
	await app.inspect(Registry.ref(Registry.WORK, WORK))
	check("Obra nº %d" % WORK in app.inspector.text, app.inspector.text)
	check(app.point_of(Registry.ref(Registry.WORK, WORK)) != Vector3.INF, "Ir até lá leva ao canteiro")


func test_whoever_works_on_the_lot_faces_the_site_with_a_hammer() -> void:
	var app = await city_app(tree)
	var actor = app.world_view.npc_nodes[7]
	check(actor.site_work, "o motor diz que a Ana trabalha na obra")
	check(actor.face_point != Vector3.INF, "vira para o canteiro")
	app.stream.delta_applied.emit(delta(11, {"npcUpserts": [{"id": {"value": 7}, "location": {"x": 3, "y": 3}, "currentAction": 2,
		"city": {"value": "city-a"}, "actionStartedAtMs": 11 * 3600000, "actionTarget": {"kind": "workplace", "id": "4"}}]}))
	check(not actor.site_work, "de volta ao ofício: a ferramenta do ofício")


func test_work_events_link_to_the_work_and_name_what_it_raises() -> void:
	var app = await city_app(tree)
	var event := {"id": 5, "label": "Começou uma obra", "kind": 0, "tick": 3, "cell": Vector2i(4, 0), "has_cell": true,
		"involved": {"workId": WORK, "cityId": "city-a", "outcome": "casa"}, "record_only": false}
	var links: Array = app.registry.event_links(event).map(func(l): return Registry.key(l[1]))
	check("work:%d" % WORK in links, "evento de obra abre a obra: %s" % [links])
	check(app.registry.event_title(event).ends_with("— casa"), app.registry.event_title(event))


func test_the_day_lost_comes_from_the_world_log_not_from_the_state() -> void:
	var app = await city_app(tree)
	var seen := []
	app.store.events_arrived.connect(func(events): seen.append_array(events))
	app.stream.delta_applied.emit(delta(11, {"workUpserts": [work(0.3, {"halt": "tempo"})]}))
	eq(seen.size(), 0, "o estado sozinho não inventa evento")
	app.stream.delta_applied.emit(delta(12, {"events": [{"atMs": 12 * 3600000, "kind": 0, "label": "Uma obra perdeu o dia pelo tempo",
		"location": {"x": 4, "y": 0}, "eventId": 77, "involved": {"workId": WORK, "cityId": "city-a", "outcome": "casa", "motive": "chuva"}}]}))
	eq(seen.size(), 1, "o evento do log chega")
	var event: Dictionary = app.registry.events.get(77, {})
	check("work:%d" % WORK in app.registry.event_links(event).map(func(l): return Registry.key(l[1])), "e abre a obra")


func test_house_material_on_the_lot_is_the_pile() -> void:
	var snapshot := city()
	snapshot.payload.livingState.works = [work(0.2, {"materials": [{"resourceId": 3, "name": "madeira", "needed": 20, "placed": 4, "onSite": 6}]})]
	var app = await city_app(tree, snapshot)
	check(int(names_in(app.world_view.buildings.nodes[CANTEIRO]).get("Material", 0)) > 0, "madeira no lote")
	var empty := work(0.2, {"materials": [{"resourceId": 3, "name": "madeira", "needed": 20, "placed": 4, "onSite": 0}]})
	app.stream.delta_applied.emit(delta(11, {"workUpserts": [empty]}))
	eq(int(names_in(app.world_view.buildings.nodes[CANTEIRO]).get("Material", 0)), 0, "o lote vazio não tem pilha")


func test_the_city_snapshot_without_work_on_the_building_still_draws_the_site() -> void:
	var snapshot := city()
	snapshot.payload.buildings[1].erase("work") # o retrato da cidade traz o prédio sem `work`; a obra vem em `livingState.works`
	var app = await city_app(tree, snapshot)
	eq(int(app.world_view.buildings.nodes[CANTEIRO].get_meta("work_id", -1)), WORK, "o canteiro do retrato é obra")
