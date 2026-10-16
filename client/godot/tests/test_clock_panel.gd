extends "res://tests/test_base.gd"

const Panels := preload("res://Scenes/panels.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")


func test_buttons_call_routes_and_show_status() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /simulation/", {"status": 200, "body": null})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": true, "ticksPerSecond": 2.0, "tick": 48, "year": 0}})
	var panel := Panels.ClockPanel.new()
	var api := ApiClient.new(transport)
	api.validate_shapes = false
	panel.api = api

	await panel.command("pause")
	await panel.command("resume")
	await panel.set_speed(4.0)
	await panel.command("step")

	var posts: Array = transport.calls.filter(func(c): return c.begins_with("POST"))
	eq(posts, ["POST /simulation/pause", "POST /simulation/resume", "POST /simulation/speed", "POST /simulation/step"])
	check("PAUSADO" in panel.status_text and "tick 48" in panel.status_text, "estado vem de /simulation/status: " + panel.status_text)
	panel.free()
