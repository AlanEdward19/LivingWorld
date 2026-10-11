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
