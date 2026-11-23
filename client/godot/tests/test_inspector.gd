extends "res://tests/test_base.gd"
## Inspetor genérico (ADR-0047, etapa 1): links, histórico, atualização sozinho, nomes e ficha de prédio/processo/objeto.

const App := preload("res://Scenes/app.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")
const Registry := preload("res://Store/entity_registry.gd")


static func world() -> Dictionary:
	var env := Fx.snapshot(0, {
		"width": 8, "height": 8, "cities": [{"id": {"value": "city-a"}, "name": "Alfa", "location": {"x": 2, "y": 2},
			"population": 3, "bounds": {"x": 0, "y": 0, "width": 6, "height": 6}}], "externalNpcs": [], "activeEvents": [],
		"portals": [], "layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")
	env.scope.kind = 0
	return env


static func city() -> Dictionary:
	var footprint: Array = []
	for y in 2:
		for x in 2:
			footprint.append({"cell": {"x": x, "y": y}, "material": 2 if x == 0 and y == 0 else 1})
	var env := Fx.snapshot(0, {
		"id": {"value": "city-a"}, "name": "Alfa", "location": {"x": 2, "y": 2}, "aggregatePool": {}, "residents": [],
		"pendingResidentIds": [], "layers": {}, "portals": [], "boundsAreDerived": true,
		"buildings": [{"id": {"value": 4}, "buildingTypeId": 3, "location": {"x": 1, "y": 1}, "orientation": 0, "floors": 2,
			"groundFootprint": footprint, "value": 900, "locationIsDerived": false}],
		"indicators": {"population": 3, "wealth": 1, "health": 1, "inequality": 0, "economy": 1, "housing": 0},
		"livingState": {"npcs": [
			{"id": {"value": 7}, "location": {"x": 3, "y": 3}, "currentAction": 2, "city": {"value": "city-a"}},
			{"id": {"value": 8}, "location": {"x": 4, "y": 3}, "currentAction": 8, "city": {"value": "city-a"}}],
			"processes": [{"id": 50, "kind": "crop", "targetId": 3, "progress": 0.5, "descriptorKey": "plant-crop",
				"location": {"x": 5, "y": 5}, "remainingHours": 30, "quality": null}]},
		"bounds": {"x": 0, "y": 0, "width": 6, "height": 6}}, "City")
	env.scope.kind = 1
	return env


static func person(name: String, extra: Dictionary = {}) -> Dictionary:
	var body := {"name": name, "ageYears": 31, "health": 90, "hunger": 80, "thirst": 70, "sleep": 60, "social": 50, "lod": 0,
		"sex": 0, "profession": {"id": 1}, "cognitionTrace": [], "currentLocation": {"x": 3, "y": 3},
		"personality": {"extroversion": 50, "agreeableness": 50, "conscientiousness": 50, "emotionalStability": 50,
			"openness": 50, "ambition": 50, "loyalty": 50, "altruism": 50, "impulsivity": 50, "riskAversion": 50}}
	body.merge(extra, true)
	return {"status": 200, "body": body}


static func transport() -> Object:
	var fake := FakeTransport.new()
	fake.on("GET /visual/catalog/actions", {"status": 200, "body": [{"actionType": 2, "label": "Trabalhando"}, {"actionType": 8, "label": "Furtando"}]})
	fake.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "second": 5 * 3600, "tickDeltaSeconds": 3600, "year": 0}})
	fake.on("GET /world/info", {"status": 200, "body": {"name": "w", "periodId": "p1", "technologyAxes": []}})
	fake.on("GET /periods/p1/catalog", {"status": 200, "body": {"professionIds": [1], "professionNames": {"1": "Lavrador"},
		"skillIds": [], "skillNames": {}, "descriptors": {"resource": [], "buildingType": [{"id": 3, "name": "Padaria"}]}}})
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": world()})
	fake.on("GET /visual/subscribe?scope=City", {"status": 200, "body": city()})
	fake.on("GET /npcs/7", person("Ana", {"motherId": {"value": 9}, "spouse": {"value": 8}, "household": {"value": 3},
		"employer": {"value": 4}, "city": {"value": "city-a"}}))
	fake.on("GET /npcs/8", person("Beto"))
	fake.on("GET /npcs/9", person("Cida"))
	fake.on("GET /npcs?", {"status": 200, "body": {"total": 3, "items": [
		{"id": {"value": 7}, "name": "Ana"}, {"id": {"value": 8}, "name": "Beto"}, {"id": {"value": 9}, "name": "Cida"}]}})
	return fake


static func make_app(tree: SceneTree, fake) -> Node:
	var app := App.new()
	tree.root.add_child(app)
	await app.start(fake, false)
	return app


func test_person_links_use_names_and_navigate_with_history() -> void:
	var fake := transport()
	var app = await make_app(tree, fake)
	await app._load_names()
	await app.select_npc(7)

	var kinds: Array = app.inspector.link_refs.map(func(r): return Registry.key(r))
	eq(kinds, ["person:9", "person:8", "household:3", "workplace:4", "city:city-a"], "mãe, cônjuge, casa, trabalho e cidade viram links")
	check("Lavrador" in app.npc_panel.details_text, "profissão pelo nome do catálogo: " + app.npc_panel.details_text)
	await app.inspector.open_link(0)
	eq(app.inspector.current, Registry.ref(Registry.PERSON, 9), "o link abre a mãe")
	eq(app.inspector.link_refs.map(func(r): return Registry.key(r)), ["person:7"], "e ela já mostra a filha conhecida")
	await app.inspector.back()
	eq(app.inspector.current, Registry.ref(Registry.PERSON, 7), "Voltar retorna à ficha anterior")
	eq(app.inspector.back_button.disabled, true, "sem mais histórico")
	app.queue_free()


func test_open_sheet_refreshes_only_when_the_world_hour_changes() -> void:
	var fake := transport()
	var app = await make_app(tree, fake)
	await app.select_npc(7)
	var before: int = fake.count_calls("GET /npcs/7")

	await app.inspector.refresh(true)
	eq(fake.count_calls("GET /npcs/7"), before, "mesma hora do mundo: cache, sem nova leitura")
	app._on_clock_status({"second": 6 * 3600, "ticksPerSecond": 1.0, "isPaused": false, "year": 0})
	await app.inspector.refresh(true)
	eq(fake.count_calls("GET /npcs/7"), before + 1, "hora nova: a ficha aberta se atualiza")
	app.queue_free()


func test_building_click_opens_a_sheet_and_entering_is_an_action() -> void:
	var fake := transport()
	fake.on("GET /visual/subscribe?scope=Interior", {"status": 200, "body": interior_body()})
	var app = await make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	check("duplo clique: entrar" in app.describe_hit({"building_id": 4}), app.describe_hit({"building_id": 4}))
	check("Padaria" in app.describe_hit({"building_id": 4}), "tipo de prédio pelo nome do período")

	await app.inspect(Registry.ref(Registry.BUILDING, 4))
	eq(app.stage.active, false, "clique simples NÃO entra")
	check("Edifício 4 · 2 andares" in app.inspector.text and "Valor estimado: 900" in app.inspector.text, app.inspector.text)
	eq(app.inspector.enter_button.visible, true)
	app.inspector.enter_button.pressed.emit()
	await tree.process_frame
	await tree.process_frame
	eq(app.stage.active, true, "Entrar é ação da ficha")
	await app.inspector.refresh()
	eq(app.inspector.link_refs.map(func(r): return Registry.key(r)), ["person:7"], "quem está dentro vira link")
	app.queue_free()


static func interior_body() -> Dictionary:
	var cells: Array = []
	for y in 3:
		for x in 3:
			cells.append({"cell": {"x": x, "y": y}, "material": 3})
	var env := Fx.snapshot(0, {"id": {"value": 4}, "city": {"value": "city-a"}, "buildingTypeId": 3, "occupancyModeled": true,
		"layout": {"floors": 1, "floorLayouts": [{"level": 0, "cells": cells}]},
		"occupants": [{"npc": {"value": 7}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}, "action": 2, "usingObject": "stool"}],
		"objects": {"format": 1, "furniture": [{"id": "stool", "variation": 0}], "catalog": [{"id": "stool", "category": "furniture",
			"variationSeed": 0, "usePoints": [{"action": 2, "offset": [0, 0, 0], "facing": 0}],
			"shape": [{"shape": "box", "size": [0.4, 0.4, 0.4], "offset": [0, 0.2, 0], "color": "#8b5a2b", "symmetry": "none"}]}]}}, "Interior")
	env.scope.kind = 2
	return env


func test_process_and_crop_are_drawn_and_inspectable() -> void:
	var app = await make_app(tree, transport())
	await app.enter_scope("City", "city-a")
	eq(app.world_view.processes.instance_count(), 1, "a lavoura do retrato vira um canteiro")
	app.stream.delta_applied.emit(Fx.delta(0, 1, {"processUpserts": [{"id": 60, "kind": "rest", "targetId": 7, "progress": 0.25,
		"descriptorKey": "rest", "location": {"x": 3, "y": 3}, "remainingHours": 5, "quality": 0.8}]}))
	eq(app.world_view.processes.instance_count(), 3, "descanso vira barra de progresso (fundo + preenchimento)")

	await app.inspect(Registry.ref(Registry.PROCESS, 60))
	check("Descanso · 25%" in app.inspector.text and "Qualidade 80%" in app.inspector.text, app.inspector.text)
	eq(app.inspector.link_refs, [Registry.ref(Registry.PERSON, 7)], "quem descansa vira link")
	await app.inspect(Registry.ref(Registry.PROCESS, 50))
	check("Plantio · 50%" in app.inspector.text, app.inspector.text)
	app.stream.delta_applied.emit(Fx.delta(1, 2, {"processRemoved": [50]}))
	await app.inspector.refresh()
	check("terminou" in app.inspector.text, "o processo encerrado se atualiza sozinho: " + app.inspector.text)
	eq(app.world_view.processes.instance_count(), 2)
	app.queue_free()


func test_interior_people_and_objects_are_clickable() -> void:
	var fake := transport()
	fake.on("GET /visual/subscribe?scope=Interior", {"status": 200, "body": interior_body()})
	var app = await make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	await app.enter_building(4)
	var camera: Camera3D = app.stage.camera()
	var actor: Node3D = app.stage.interior_view.occupant_nodes[7]
	var point: Vector2 = camera.unproject_position(actor.global_position + Vector3(0, 0.6, 0))
	eq(app.selection.hit_at(point + Vector2(4, 4)), Registry.ref(Registry.PERSON, 7), "clique perto da pessoa no interior")
	check("Ana" in app.describe_hit(Registry.ref(Registry.PERSON, 7)) or "NPC 7" in app.describe_hit(Registry.ref(Registry.PERSON, 7)))

	var objects: Array = app.stage.interior_view.visible_objects()
	eq(objects.size(), 1, "a peça do catálogo é selecionável")
	var object_point: Vector2 = camera.unproject_position(objects[0].position)
	var hit: Dictionary = app.selection.hit_at(object_point)
	if hit.get("kind") == Registry.PERSON: # a pessoa usa o banco: está em cima dele
		app.stage.interior_view.occupant_nodes[7].visible = false
		hit = app.selection.hit_at(object_point)
	eq(hit, Registry.ref(Registry.OBJECT, "stool"))
	await app.inspect(hit)
	check("furniture" in app.inspector.text and "Trabalhando" in app.inspector.text, "categoria e para que serve: " + app.inspector.text)
	eq(app.inspector.link_refs, [Registry.ref(Registry.PERSON, 7)], "quem usa o objeto agora")
	app.queue_free()
