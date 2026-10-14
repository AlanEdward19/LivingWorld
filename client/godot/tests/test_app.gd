extends "res://tests/test_base.gd"

const App := preload("res://Scenes/app.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")


func _interior_envelope() -> Dictionary:
	var cells: Array = []
	for y in 3:
		for x in 3:
			cells.append({"cell": {"x": x, "y": y}, "material": 3})
	var env := Fx.snapshot(0, {
		"id": {"value": 4}, "city": {"value": "city-a"}, "buildingTypeId": -1, "occupancyModeled": true,
		"layout": {"floors": 2, "floorLayouts": [{"level": 0, "cells": cells}, {"level": 1, "cells": cells}]},
		"occupants": [{"npc": {"value": 1}, "floor": {"value": 1}, "localCell": {"x": 1, "y": 1}}],
	}, "Interior")
	env.scope.kind = 2
	return env


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": [{"actionType": 1, "label": "Dormindo"}]})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 0, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": _world()})
	transport.on("GET /visual/subscribe?scope=Interior", {"status": 200, "body": _interior_envelope()})
	return transport


func _world() -> Dictionary:
	var env := Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")
	env.scope.kind = 0
	return env


func _app(transport) -> Node:
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	return app


func test_enter_subscribes_and_dims_exterior() -> void:
	var transport := _transport()
	var app = await _app(transport)
	eq(app.stage.dim_rect.visible, false, "mundo aberto: sem escurecer")

	await app.enter_building(4)

	check(transport.calls.any(func(c): return c.begins_with("GET /visual/subscribe?scope=Interior&refId=4")), "assina o escopo interior do prédio 4")
	eq(app.stage.dim_rect.visible, true, "exterior escurecido e desfocado")
	eq(app.stage.active, true)
	eq(app.stage.interior_view.floor_count, 2)
	app.queue_free()


func _city() -> Dictionary:
	var env := Fx.snapshot(0, {
		"id": {"value": "city-a"}, "name": "Alfa", "location": {"x": 1, "y": 1}, "aggregatePool": {}, "residents": [],
		"pendingResidentIds": [], "buildings": [], "layers": {}, "portals": [],
		"indicators": {"population": 3, "wealth": 1, "health": 1, "inequality": 0, "economy": 1, "housing": 0},
		"livingState": {"npcs": []}, "bounds": {"x": 0, "y": 0, "width": 2, "height": 2}, "boundsAreDerived": true}, "City")
	env.scope.kind = 1
	return env


func _interior_delta(from_seq: int, to_seq: int, entered: Array, moved: Array, exited: Array) -> Dictionary:
	var env := Fx.delta(from_seq, to_seq)
	env.scope.kind = 2
	env.payload = {"tick": 10 + to_seq, "buildingId": {"value": 4}, "entered": entered, "moved": moved, "changedFloor": [], "exited": exited}
	return env


func test_interior_delta_updates_occupants_without_polling() -> void:
	var transport = _transport()
	var app = await _app_with_transport(transport)
	await app.enter_building(4)
	var calls_before: int = transport.count_calls("GET /visual/subscribe?scope=Interior")
	eq(app.stage.interior_view.occupant_nodes.keys(), [1], "o snapshot trouxe um ocupante")

	app.stream.delta_applied.emit(_interior_delta(0, 1, [
		{"npc": {"value": 2}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}, "action": 2}], [], []))
	eq(app.stage.interior_view.occupant_nodes.keys().size(), 2, "quem entrou aparece")

	app.stream.delta_applied.emit(_interior_delta(1, 2, [], [
		{"npc": {"value": 2}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}, "action": 1}], []))
	eq(app.stage.interior_view.occupant_nodes[2].action, 1, "mudar de ação atualiza o NPC")

	app.stream.delta_applied.emit(_interior_delta(2, 3, [], [], [{"value": 1}]))
	eq(app.stage.interior_view.occupant_nodes.keys(), [2], "quem saiu some")
	eq(transport.count_calls("GET /visual/subscribe?scope=Interior"), calls_before, "nenhuma releitura do snapshot")
	app.queue_free()


func test_interior_delta_moves_an_occupant_to_another_floor() -> void:
	var transport = _transport()
	var app = await _app_with_transport(transport)
	await app.enter_building(4)
	var actor = app.stage.interior_view.occupant_nodes[1]
	eq(actor.get_meta("floor"), 1, "o snapshot põe o ocupante 1 no andar 1")

	var env := _interior_delta(0, 1, [], [], [])
	env.payload.changedFloor = [{"npc": {"value": 1}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}, "action": null}]
	app.stream.delta_applied.emit(env)

	eq(app.store.interior.occupants[0].floor.value, 0, "a store registra o andar novo")
	eq(app.stage.interior_view.occupant_nodes[1].get_meta("floor"), 0, "a cena também")
	eq(app.stage.interior_view.occupant_nodes[1].position.y, 0.0, "e a altura do ocupante muda")
	app.queue_free()


func _app_with_transport(transport) -> Node:
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	return app


func test_exit_returns_to_the_previous_city_scope() -> void:
	var transport = _transport()
	transport.on("GET /visual/subscribe?scope=City", {"status": 200, "body": _city()})
	var app = await _app(transport)
	await app.enter_scope("City", "city-a")
	await app.enter_building(4)
	var before: int = transport.count_calls("GET /visual/subscribe?scope=City")

	await app.exit_building()

	eq(transport.count_calls("GET /visual/subscribe?scope=City"), before + 1, "volta à CIDADE, não ao mundo")
	eq(app.store.kind, "City")
	eq(app.list_panel.city_filter, "city-a", "a lista segue o escopo atual")
	app.queue_free()


func test_offline_shows_banner_text_and_recovers() -> void:
	var transport := FakeTransport.new()
	var attempts: Array = []
	transport.on("GET /visual/subscribe", func(_p):
		attempts.append(1)
		if attempts.size() < 3:
			return {"status": 0, "body": null}
		return {"status": 200, "body": _world()})
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 0, "year": 0}})
	var app := App.new()
	tree.root.add_child(app)
	var seen: Array = []
	await app.start(transport, false, func(seconds: float):
		seen.append("%s|%s|%s" % [app.banner.visible, app.banner.text, seconds]))

	check(seen[0].begins_with("true|sem conexão"), "o aviso 'sem conexão' aparece: " + seen[0])
	check("nova tentativa em 1 s" in seen[0], "mostra a espera: " + seen[0])
	check("em 2 s" in seen[1], seen[1])
	eq(app.banner.visible, false, "o aviso some ao reconectar")
	app.queue_free()


func test_exit_restores_exterior_and_scope() -> void:
	var transport := _transport()
	var app = await _app(transport)
	await app.enter_building(4)
	var before: int = transport.count_calls("GET /visual/subscribe?scope=World")

	await app.exit_building()

	eq(app.stage.dim_rect.visible, false, "exterior restaurado")
	eq(app.stage.active, false)
	eq(transport.count_calls("GET /visual/subscribe?scope=World"), before + 1, "volta à assinatura anterior (Mundo)")
	eq(app.store.kind, "World")
	app.queue_free()


func test_calendar_comes_from_world_calendar_route() -> void:
	var transport = _transport()
	transport.on("GET /world/calendar", {"status": 200, "body": {"daysPerMonth": 10, "hoursPerDay": 12, "hoursPerMonth": 120, "hoursPerYear": 480, "monthsPerYear": 4}})
	var app = await _app(transport)

	eq(app.day_cycle.hours_per_day, 12.0, "o dia do céu usa o calendário do servidor")
	eq(app.day_cycle.days_per_month, 10)
	eq(app.day_cycle.months_per_year, 4)
	app.queue_free()


func test_camera_scope_is_published_to_the_observation_registry() -> void:
	var transport = _transport()
	transport.on("GET /visual/subscribe?scope=City", {"status": 200, "body": _city()})
	transport.on("POST /observation/scope", {"status": 200, "body": null})
	var app = await _app(transport)
	var sent: Array = transport.bodies["POST /observation/scope"]
	eq(sent.back().scope, {"kind": "World"}, "mundo-múndi")

	await app.enter_scope("City", "city-a")
	eq(sent.back().scope, {"kind": "City", "cityId": "city-a"}, "cidade em foco")
	check(String(sent.back().sourceId).begins_with("godot-"), "fonte estável da sessão")

	await app.enter_building(4)
	eq(sent.back().scope, {"kind": "Building", "cityId": "city-a", "buildingId": "4"}, "interior aberto")

	await app.release_observation()
	eq(sent.back().scope, null, "sair retira a fonte")
	app.queue_free()


func test_open_brain_panel_refreshes_on_clock_tick_and_dates_use_the_server_calendar() -> void:
	var transport = _transport()
	transport.on("GET /npcs/7", Fx.npc_inspect([Fx.trace_entry(10, 0)]))
	var app = await _app(transport)
	await app.open_brain(7)
	var before: int = transport.count_calls("GET /npcs/7")

	app._on_clock_status({"tick": 55, "ticksPerSecond": 1.0, "isPaused": false, "year": 0})
	await app.get_tree().process_frame

	eq(transport.count_calls("GET /npcs/7"), before + 1, "a hora nova do relógio atualiza o painel aberto")
	app.day_cycle.set_calendar({"hoursPerDay": 10, "daysPerMonth": 5, "monthsPerYear": 2})
	eq(app._date_text(123), "Ano 1 · Mês 1 · Dia 3 · 07:00", "data pelo calendário do servidor, não 24 h/30 dias")
	app.queue_free()
