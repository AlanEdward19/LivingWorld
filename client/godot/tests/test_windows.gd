extends "res://tests/test_base.gd"

const App := preload("res://Scenes/app.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")


func _app() -> Node:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var detail := Fx.npc_inspect([])
	detail.body["culture"] = {"id": 1}
	detail.body["employer"] = {"value": 4}
	transport.on("GET /npcs/7", detail)
	transport.on("GET /narratives/biographies/7", {"status": 200, "body": {"npcId": 7, "prose": "x", "claims": []}})
	transport.on("GET /narratives/reports", {"status": 200, "body": []})
	transport.on("GET /cities/city-a/treasury", {"status": 200, "body": {"cityId": "city-a", "balance": 0, "fromDay": 0, "toDay": 0, "months": []}})
	transport.on("GET /workplaces/4/trades", {"status": 200, "body": {"workplaceId": 4, "fromDay": 0, "toDay": 0, "days": []}})
	transport.on("GET /cultures/1/technologies", {"status": 200, "body": []})
	var app := App.new()
	app.admin_enabled = true
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	return app


func _open_count(app: Node) -> int:
	var n := 0
	for window in app._info_windows():
		if window.is_open:
			n += 1
	return n


func test_opening_an_info_window_closes_the_others() -> void:
	var app := await _app()
	await app.select_npc(7)

	await app.open_brain(7)
	eq(_open_count(app), 1)
	await app.open_biography(7)
	eq([app.brain_panel.is_open, app.narrative.is_open], [false, true], "narrativa abre e o cérebro fecha")
	await app.open_trades(4)
	eq([app.narrative.is_open, app.economy.is_open], [false, true], "economia abre e a narrativa fecha")
	app.city_panel.show_city({"id": "city-a", "name": "Alfa", "indicators": {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}})
	await app.open_treasury("city-a")
	eq(_open_count(app), 1, "tesouro substitui as vendas")
	await app._show_technologies(1)
	eq([app.economy.is_open, app.technology.is_open], [false, true])
	app._toggle_admin()
	eq([app.technology.is_open, app.admin_window.is_open], [false, true], "o console fecha as demais")
	app._toggle_reports()
	await tree.process_frame
	eq([app.admin_window.is_open, app.narrative.is_open, app.narrative.mode], [false, true, "reports"])
	eq(_open_count(app), 1, "nunca duas ao mesmo tempo")
	app.queue_free()


func test_conversation_and_the_player_hud_are_not_closed_by_info_windows() -> void:
	var app := await _app()
	app.conversation.is_open = true
	app.conversation.visible = true
	await app.open_brain(7)
	eq(app.conversation.is_open, true, "a conversa fica numa faixa própria, embaixo")
	app.conversation.is_open = false
	app.queue_free()


func test_narrow_screens_let_windows_use_the_width_and_wide_ones_sit_between_the_side_panels() -> void:
	var app := await _app()

	app.layout_windows(1024.0)
	for window in app._info_windows():
		eq(window.offset_left, 8.0, "tela curta: janela ocupa a largura")
		check(window.custom_minimum_size.y >= 220.0, "altura mínima")
	app.layout_windows(1600.0)
	for window in app._info_windows():
		eq(window.offset_left, 310.0, "tela larga: entre os painéis laterais")
	app.queue_free()
