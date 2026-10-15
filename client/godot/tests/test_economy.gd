extends "res://tests/test_base.gd"

const EconomyWindow := preload("res://Scenes/economy_window.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Panels := preload("res://Scenes/panels.gd")
const Fx := preload("res://tests/fixtures.gd")

const TREASURY := {"cityId": "city-a", "balance": 1234, "fromDay": 0, "toDay": 359, "months": [
	{"month": 0, "revenue": 50, "spending": 25}, {"month": 13, "revenue": 10, "spending": 100}]}
const TRADES := {"workplaceId": 4, "fromDay": 0, "toDay": 29, "days": [
	{"day": 10, "resourceId": 1, "quantity": 5, "value": 15, "purchases": 2}, {"day": 12, "resourceId": 7, "quantity": 4, "value": 20, "purchases": 1}]}


func _window(transport, validate: bool = false) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	var window := EconomyWindow.new()
	window.api = api
	window.calendar_of = func() -> Dictionary: return {"hoursPerDay": 10, "daysPerMonth": 5, "monthsPerYear": 12}
	window.date_of = func(tick: int) -> String: return "tick %d" % tick
	window.resource_name_of = func(id: int) -> String: return {1: "Trigo"}.get(id, "recurso %d" % id)
	return window


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /cities/city-a/treasury", {"status": 200, "body": TREASURY})
	transport.on("GET /workplaces/4/trades", {"status": 200, "body": TRADES})
	return transport


func test_treasury_calls_the_route_and_shows_balance_and_proportional_month_bars() -> void:
	var transport := _transport()
	var window := _window(transport)

	await window.show_treasury("city-a", "Alfa")

	eq(transport.calls, ["GET /cities/city-a/treasury"])
	eq(window._title.text, "Tesouro de Alfa")
	eq(window.balance, 1234)
	eq(window._summary.text, "Saldo: 1234")
	eq(window.month_rows.size(), 2)
	eq(window.month_rows[0].label, "Ano 0 · Mês 1", "mês 0 pelo calendário")
	eq(window.month_rows[1].label, "Ano 1 · Mês 2", "mês 13 = ano 1, mês 2 (12 meses por ano)")
	eq(window.month_rows[0].revenue_bar.value, 50.0)
	eq(window.month_rows[0].revenue_bar.max_value, 100.0, "escala = maior valor da janela")
	eq(window.month_rows[1].spending_bar.value, 100.0)
	check(window.month_rows[0].revenue_bar.modulate != window.month_rows[0].spending_bar.modulate, "entrou e saiu têm cores diferentes")
	window.free()


func test_treasury_empty_404_and_error_states() -> void:
	var transport := _transport()
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	transport.on("GET /cities/city-a/treasury", {"status": 200, "body": {"cityId": "city-a", "balance": 0, "fromDay": 0, "toDay": 0, "months": []}})
	await window.show_treasury("city-a", "Alfa")
	eq(window.empty_text, "Sem movimento no tesouro neste período.")
	eq(window._summary.text, "Saldo: 0", "o saldo aparece mesmo sem movimento")

	transport.on("GET /cities/city-a/treasury", {"status": 404, "body": null})
	await window.show_treasury("city-a", "Alfa")
	eq(window.empty_text, "Esta cidade não existe mais.")
	eq(warnings.size(), 0)

	transport.on("GET /cities/city-a/treasury", {"status": 500, "body": null})
	await window.show_treasury("city-a", "Alfa")
	check("erro 500" in window.empty_text, window.empty_text)
	eq(warnings.size(), 1)
	window.free()


func test_trades_show_day_resource_name_quantity_value_and_purchases() -> void:
	var transport := _transport()
	var window := _window(transport)

	await window.show_trades(4, "local 4")

	eq(transport.calls, ["GET /workplaces/4/trades"])
	eq(window._title.text, "Vendas de local 4")
	eq(window.trade_rows.size(), 2)
	eq(window.trade_rows[0].text, "tick 100 · Trigo · 5 un. por 15 (2 compras)", "dia 10 x 10 h/dia; recurso pelo catálogo")
	eq(window.trade_rows[1].text, "tick 120 · recurso 7 · 4 un. por 20 (1 compra)", "recurso sem nome aparece como número")
	eq(window._summary.text, "Total vendido: 35")
	window.free()


func test_trades_empty_404_and_error_states() -> void:
	var transport := _transport()
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	transport.on("GET /workplaces/4/trades", {"status": 200, "body": {"workplaceId": 4, "fromDay": 0, "toDay": 29, "days": []}})
	await window.show_trades(4, "local 4")
	eq(window.empty_text, "Sem vendas registradas neste período.")
	transport.on("GET /workplaces/4/trades", {"status": 404, "body": null})
	await window.show_trades(4, "local 4")
	eq(window.empty_text, "Esta oficina não existe mais.")
	transport.on("GET /workplaces/4/trades", {"status": 503, "body": null})
	await window.show_trades(4, "local 4")
	check("erro 503" in window.empty_text, window.empty_text)
	eq(warnings.size(), 1)
	window.free()


func test_shapes_are_checked_and_only_get_is_used_and_closed_window_is_silent() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /cities/city-a/treasury", {"status": 200, "body": {"balance": 1}})
	transport.on("GET /workplaces/4/trades", {"status": 200, "body": {"days": [{"day": 1}]}})
	var window := _window(transport, true)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	await window.show_treasury("city-a", "Alfa")
	await window.show_trades(4, "local 4")
	eq(warnings.size(), 2, "corpo fora do contrato vira aviso")
	eq(window.month_rows.size() + window.trade_rows.size(), 0)

	window.close()
	eq(transport.calls.filter(func(c: String) -> bool: return not c.begins_with("GET ")), [], "só leitura")
	window.free()


class SlowTransport extends "res://tests/fake_transport.gd":
	signal release()

	func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
		if path.contains("/treasury"):
			await release
		return await super.request(method, path, body, extra_headers)


func test_a_late_reply_for_another_subject_is_dropped() -> void:
	var transport := SlowTransport.new()
	transport.on("GET /cities/city-a/treasury", {"status": 200, "body": TREASURY})
	transport.on("GET /workplaces/4/trades", {"status": 200, "body": TRADES})
	var window := _window(transport)
	window.show_treasury("city-a", "Alfa")
	await window.show_trades(4, "local 4")

	transport.release.emit()
	await Engine.get_main_loop().process_frame

	eq(window.mode, "trades")
	eq(window.month_rows.size(), 0, "o tesouro tardio não sobrescreve as vendas")
	eq(window.trade_rows.size(), 2)
	window.free()


func test_resource_names_come_from_the_period_catalog_when_the_period_is_known() -> void:
	var transport := _app_transport()
	transport.on("GET /periods/vila-pequena/catalog", {"status": 200, "body": {"descriptors": {"resource": [{"id": 1, "name": "Trigo"}, {"id": 2, "name": "Água"}]}}})
	var app := App.new()
	app.period_id = "vila-pequena"
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)

	eq(app.resource_names, {1: "Trigo", 2: "Água"})
	eq(app.economy.resource_name_of.call(1), "Trigo")
	eq(app.economy.resource_name_of.call(9), "recurso 9")
	app.queue_free()

	var unknown := _app_transport()
	var other := App.new()
	other.player_id = "p-1"
	tree.root.add_child(other)
	await other.start(unknown, false)
	eq(other.resource_names, {}, "sem período conhecido, sem nomes (e sem pedir catálogo)")
	eq(unknown.calls.filter(func(c: String) -> bool: return c.begins_with("GET /periods")), [])
	other.queue_free()


class _StubMenu extends Node:
	var selected_period := "vila-pequena"


func test_boot_hands_the_created_period_to_the_app() -> void:
	var boot = load("res://Scenes/boot.gd").new()
	boot.quit_fn = func(): pass
	tree.root.add_child(boot)
	boot.menu = _StubMenu.new()
	boot.add_child(boot.menu)

	await boot._play()

	eq(boot.app.period_id, "vila-pequena", "o período escolhido no menu chega ao app")
	boot.app.queue_free()
	boot.free()


func _find_button(node: Node, text: String) -> Button:
	for child in node.get_children():
		if child is Button and child.text == text:
			return child
		var found := _find_button(child, text)
		if found != null:
			return found
	return null


func _app_transport() -> Object:
	var transport := _transport()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var employed := Fx.npc_inspect([])
	employed.body["employer"] = {"value": 4}
	transport.on("GET /npcs/7", employed)
	transport.on("GET /npcs/8", Fx.npc_inspect([]))
	return transport


func test_buttons_open_the_windows_and_the_workplace_button_only_shows_with_an_employer() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)

	await app.select_npc(8)
	eq(app.npc_panel.trades_button.visible, false, "sem emprego não há o que mostrar")
	await app.select_npc(7)
	eq(app.npc_panel.trades_button.visible, true)
	_find_button(app.npc_panel, "Vendas do local de trabalho").pressed.emit()
	await tree.process_frame
	eq(app.economy.mode, "trades")
	check(transport.calls.has("GET /workplaces/4/trades"), "pede o local do empregador")

	app.city_panel.show_city({"id": "city-a", "name": "Alfa", "indicators": {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}})
	_find_button(app.city_panel, "Tesouro").pressed.emit()
	await tree.process_frame
	eq(app.economy.mode, "treasury")
	check(transport.calls.has("GET /cities/city-a/treasury"), "pede o tesouro da cidade")

	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	app._unhandled_input(esc)
	eq(app.economy.is_open, false, "Esc fecha")
	app.queue_free()
