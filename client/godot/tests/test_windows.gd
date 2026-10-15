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


func test_windows_keep_a_minimum_height_and_the_same_margin_on_any_viewport() -> void:
	var app := await _app()
	for window in app._info_windows():
		check(window.custom_minimum_size.y >= 220.0, "altura mínima")
		eq(window.offset_left, 310.0, "margem fixa: o canvas tem sempre 1600 unidades ou mais de largura")
	app.queue_free()


func test_every_entry_closes_the_window_that_was_open_and_the_player_hud_stays() -> void:
	var app := await _app()
	app.city_panel.show_city({"id": "city-a", "name": "Alfa", "indicators": {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}})
	await app.select_npc(7)
	var entries: Array = [
		["brain", func(): await app.open_brain(7)],
		["biografia", func(): await app.open_biography(7)],
		["cronica", func(): await app.open_chronicle("city-a")],
		["crencas", func(): await app.open_beliefs("city-a")],
		["tesouro", func(): await app.open_treasury("city-a")],
		["vendas", func(): await app.open_trades(4)],
		["tecnologias", func(): await app._show_technologies(1)],
		["console", func(): app._toggle_admin()],
		["relatos", func(): app._toggle_reports()],
	]
	app.player_hud.visible = true
	for first in entries:
		for second in entries:
			if first[0] == second[0]:
				continue
			await first[1].call()
			await tree.process_frame
			await second[1].call()
			await tree.process_frame
			eq(_open_count(app), 1, "%s depois de %s: só uma aberta" % [second[0], first[0]])
	eq(app.player_hud.visible, true, "o HUD do jogador não é uma janela de informação")
	app.queue_free()
