extends "res://tests/test_base.gd"
## Etapa 3 do ADR-0047: ficha por `GET /entities/{kind}/{id}`, crime como registro (sem cena e, se `recordOnly`, sem texto
## narrado), linha do tempo, "próximo evento", modo assistir, espólio/objeto/animal/lavoura inspecionáveis.

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Registry := preload("res://Store/entity_registry.gd")

const NARRATED := "texto narrado que nunca pode aparecer"


static func crime_events() -> Array:
	return [
		{"eventId": 41, "atMs": 12 * 3600000, "kind": 9, "label": NARRATED, "location": {"x": 3, "y": 3},
			"involved": {"authorId": 8, "victimId": 7, "caseId": 5, "outcome": "preso", "recordOnly": true}},
		{"eventId": 42, "atMs": 13 * 3600000, "kind": 8, "label": "Furto na feira", "location": {"x": 4, "y": 3},
			"involved": {"authorId": 8, "victimId": 9, "caseId": 6, "outcome": "fugiu", "recordOnly": false}},
		{"eventId": 43, "atMs": 14 * 3600000, "kind": 2, "label": "Casamento", "location": null, "involved": {"subjectId": 7, "otherId": 8}},
	]


func test_record_only_crime_shows_only_the_structured_record_everywhere() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	app.stream.delta_applied.emit(O.delta({"events": crime_events()}))
	for text in app.toasts.history:
		check(not NARRATED in text, "aviso sem texto narrado: " + text)
	check(app.toasts.history.has("Furto na feira"), "crime comum: o fato aparece")
	for i in 3:
		check(not NARRATED in app.timeline.row_text(i), "linha do tempo sem texto narrado")
	check("Registro de crime · caso nº 5 · preso" in app.timeline.row_text(2), app.timeline.row_text(2))
	await app.inspect(Registry.ref(Registry.EVENT, 41))
	check(not NARRATED in app.inspector.text, app.inspector.text)
	check("Só registro" in app.inspector.text, app.inspector.text)
	var keys: Array = app.inspector.link_refs.map(func(r): return Registry.key(r))
	eq(keys.slice(0, 2), ["person:8", "person:7"], "autor e vítima clicáveis")
	check(not NARRATED in app.selection.describe(Registry.ref(Registry.EVENT, 41)))
	await app.inspect(Registry.ref(Registry.EVENT, 42))
	check("Furto na feira" in app.inspector.text and "Desfecho: fugiu" in app.inspector.text, app.inspector.text)
	eq(app.world_view.events.count(), 2, "os dois com lugar viram marcador; o sem lugar fica só na linha do tempo")
	app.queue_free()


func test_next_event_and_timeline_click_move_the_camera_and_open_the_sheet() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	app.stream.delta_applied.emit(O.delta({"events": crime_events()}))
	eq(app.timeline.rows, [43, 42, 41], "o mais novo em cima")
	app.next_event()
	await tree.process_frame
	eq(app.inspector.current, Registry.ref(Registry.EVENT, 43), "sem visitado: vai ao mais recente")
	eq(app.director.target, Registry.ref(Registry.PERSON, 7), "e segue quem está envolvido")
	app.goto_event(41)
	await tree.process_frame
	eq(app.inspector.current, Registry.ref(Registry.EVENT, 41))
	app.next_event()
	await tree.process_frame
	eq(app.director.current_event, 42, "próximo depois do visitado")
	app.timeline.event_chosen.emit(41)
	await tree.process_frame
	eq(app.director.current_event, 41, "clique na linha do tempo")
	app.queue_free()


func test_watch_mode_follows_unseen_events_then_busy_people() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	app.toggle_watch()
	eq(app.director.watching, true)
	app.director.watch_step(0.1)
	await tree.process_frame
	eq(app.director.target, Registry.ref(Registry.PERSON, 7), "sem evento: alguém fazendo algo (Ana trabalha)")
	app.stream.delta_applied.emit(O.delta({"events": crime_events()}))
	app.director.watch_step(4.0)
	await tree.process_frame
	eq(app.director.current_event, 43, "evento novo: o mais novo ainda não visto")
	app.director.watch_step(8.1)
	await tree.process_frame
	eq(app.director.current_event, 42, "depois, o próximo não visto")
	check("assistindo" in app.director.label(), app.director.label())
	app.toggle_watch()
	eq(app.director.watching, false)
	app.queue_free()


func test_building_sheet_uses_the_inspection_route_with_owner_workers_and_stock() -> void:
	var fake = O.transport()
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	await app.inspect(Registry.ref(Registry.BUILDING, 4))
	eq(fake.count_calls("GET /entities/building/4"), 1, "uma leitura da rota de inspeção")
	eq(app.inspector._title.text, "Padaria do Beto", "nome real do servidor")
	check("Função: local de trabalho (Padaria)" in app.inspector.text, app.inspector.text)
	check("Estoque: 30 Trigo" in app.inspector.text, app.inspector.text)
	check("Venda de pão" in app.inspector.text, "história recente: " + app.inspector.text)
	var keys: Array = app.inspector.link_refs.map(func(r): return Registry.key(r))
	check(keys.has("person:8") and keys.has("person:7") and keys.has("workplace:4"), str(keys))
	check(app.world_view.building_nodes[4].get_node_or_null("Dress/Sign") != null, "placa pela função")
	app.queue_free()


func test_person_sheet_comes_from_the_route_with_all_children_and_real_workplace_name() -> void:
	var fake = O.transport()
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	await app.select_npc(7)
	eq(fake.count_calls("GET /npcs/7"), 0, "a ficha vem de /entities/npc (e alimenta o cache de /npcs)")
	var keys: Array = app.inspector.link_refs.map(func(r): return Registry.key(r))
	check(keys.has("person:11") and keys.has("person:12"), "todos os filhos: " + str(keys))
	check(keys.has("building:4"), "prédio do trabalho")
	eq(app.registry.title_of(Registry.ref(Registry.WORKPLACE, 4)), "Padaria do Beto", "nome real do local de trabalho")
	eq(app.registry.title_of(Registry.ref(Registry.PERSON, 11)), "Caio", "nome do filho pelo link do servidor")
	check("grávida" in app.inspector.extra_text, "corpo visível na ficha: " + app.inspector.extra_text)
	await app.api.npc_detail(7)
	eq(fake.count_calls("GET /npcs/7"), 0, "cérebro e seguir usam o mesmo cache")
	app.queue_free()


func test_crop_animal_leaving_and_invented_object_are_drawn_and_inspectable() -> void:
	var fake = O.transport()
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	await app.inspect(Registry.ref(Registry.CROP, 70))
	check("Lavoura de Trigo · crescendo 50%" in app.inspector.text and "Colheita esperada: 120" in app.inspector.text, app.inspector.text)
	check(app.inspector.link_refs.has(Registry.ref(Registry.PERSON, 8)), "dono da lavoura")
	check(app.world_view.fauna.positions.has(90), "coelho desenhado")
	await app.inspect(Registry.ref(Registry.ANIMAL, 90))
	check("Coelho" in app.inspector.text, app.inspector.text)
	check(app.world_view.items.positions.has("leaving:30"), "espólio no chão")
	await app.inspect(Registry.ref(Registry.LEAVING, 30))
	eq(app.inspector.link_refs[0], Registry.ref(Registry.PERSON, 9), "de quem era")
	check(app.world_view.items.positions.has("item:501"), "objeto à venda fora de casa")
	await app.inspect(Registry.ref(Registry.ITEM, 501))
	eq(fake.count_calls("GET /designs/77"), 1, "liga a /designs/{id}")
	check("à venda" in app.inspector.text.to_lower() and "carry" in app.inspector.text, app.inspector.text)
	check(app.inspector.link_refs.has(Registry.ref(Registry.PERSON, 8)), "inventor")
	await app.request_design(77)
	eq(app.world_view.items.look_of(77, "cylinder", 2).color, Color("#3366cc"), "cor do material do design")
	var point: Vector3 = app.point_of(Registry.ref(Registry.CROP, 70))
	check(point != Vector3.INF, "Ir até lá")
	app.queue_free()


func test_snapshot_outside_the_contract_says_so_instead_of_hanging_on_connecting() -> void:
	var app := T.App.new()
	tree.root.add_child(app)
	await app.start(O.transport(), true) # validação ligada: o retrato mínimo do teste não cumpre o contrato do mundo
	check(app.banner.visible and "fora do contrato" in app.banner.text, app.banner.text)
	check(not app.contract_errors.is_empty())
	app.queue_free()
