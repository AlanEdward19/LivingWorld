extends "res://tests/test_base.gd"

const NarrativeWindow := preload("res://Scenes/narrative_window.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")

const BELIEFS := [
	{"reportId": 1, "distortedMagnitude": 0.8123, "distortedTick": 12, "seed": "A praga levou muitos", "distanceFromFact": 0.35},
	{"reportId": 2, "distortedMagnitude": 0.2, "distortedTick": 30, "seed": "O rei caiu", "distanceFromFact": 1.8}]


func _window(transport, validate: bool = false) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	var window := NarrativeWindow.new()
	window.api = api
	window.date_of = func(tick: int) -> String: return "dia %d" % tick
	return window


func test_beliefs_call_the_beliefs_route_with_the_city_and_never_the_truth_route() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /history/beliefs", {"status": 200, "body": BELIEFS})
	var window := _window(transport)

	await window.show_beliefs("city a", "Alfa")

	eq(transport.calls, ["GET /history/beliefs?communityId=city%20a"], "só /history/beliefs, com a cidade codificada")
	eq(window._title.text, "O que Alfa acredita")
	eq(window.period_option.visible, false)
	window.free()


func test_each_belief_shows_text_magnitude_date_and_distance_bar() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /history/beliefs", {"status": 200, "body": BELIEFS})
	var window := _window(transport)

	await window.show_beliefs("city-a", "Alfa")

	eq(window.belief_rows.size(), 2)
	eq(window.belief_rows[0].text, "A praga levou muitos")
	eq(window.belief_rows[0].detail, "magnitude 0.81 · dia 12", "magnitude distorcida e data do calendário")
	eq(window.belief_rows[0].bar.value, 35.0, "distância do fato proporcional")
	eq(window.belief_rows[1].bar.value, 100.0, "distância acima de 1 é limitada")
	window.free()


func test_empty_error_and_404_states() -> void:
	var transport := FakeTransport.new()
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	transport.on("GET /history/beliefs", {"status": 200, "body": []})
	await window.show_beliefs("city-a", "Alfa")
	eq(window.empty_text, "Alfa ainda não guarda nenhuma crença sobre o passado.")
	eq(window.belief_rows.size(), 0)

	transport.on("GET /history/beliefs", {"status": 404, "body": null})
	await window.show_beliefs("city-a", "Alfa")
	check("erro 404" in window.empty_text, window.empty_text)
	eq(warnings.size(), 1, "cidade sumida avisa")

	transport.on("GET /history/beliefs", {"status": 500, "body": null})
	await window.show_beliefs("city-a", "Alfa")
	eq(warnings.size(), 2)
	eq(window.is_open, true)
	window.free()


func test_a_body_outside_the_frozen_shape_is_a_warning_not_a_list() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /history/beliefs", {"status": 200, "body": [{"reportId": 1, "seed": "x"}]})
	var window := _window(transport, true)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	await window.show_beliefs("city-a", "Alfa")

	eq(warnings.size(), 1)
	eq(window.belief_rows.size(), 0)
	window.free()


func test_only_the_beliefs_route_is_read() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /history/beliefs", {"status": 200, "body": BELIEFS})
	var window := _window(transport)
	await window.show_beliefs("city-a", "Alfa")
	window.close()
	eq(transport.calls.filter(func(c: String) -> bool: return not c.begins_with("GET /history/beliefs")), [], "só leitura de crenças")
	window.free()


func _find_button(node: Node, text: String) -> Button:
	for child in node.get_children():
		if child is Button and child.text == text:
			return child
		var found := _find_button(child, text)
		if found != null:
			return found
	return null


func test_city_sheet_button_opens_the_beliefs_of_that_city() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	transport.on("GET /history/beliefs", {"status": 200, "body": BELIEFS})
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	app.city_panel.show_city({"id": "city-a", "name": "Alfa", "indicators": {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}})

	_find_button(app.city_panel, "O que a cidade acredita").pressed.emit()
	await tree.process_frame

	eq(app.narrative.mode, "beliefs")
	eq(transport.count_calls("GET /history/beliefs?communityId=city-a"), 1)
	check("Ano 0" in app.narrative.belief_rows[0].detail, "data pelo calendário do app: " + app.narrative.belief_rows[0].detail)
	app.queue_free()
