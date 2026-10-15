extends "res://tests/test_base.gd"

const NarrativeWindow := preload("res://Scenes/narrative_window.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")

const BIO := {"npcId": 7, "prose": "Ana nasceu e casou.", "claims": [
	{"text": "Birth (evento 3): nasceu", "eventIds": [3]}, {"text": "Marriage (evento 9): casou", "eventIds": [9, 11]}]}
const CHRONICLE := {"id": 1, "prose": "Um ano de colheitas.", "claims": [{"text": "Death (evento 5): perda", "eventIds": [5]}]}
const REPORTS := [
	{"reportId": 1, "originFactId": 5, "medium": "Song", "confidence": 0.82},
	{"reportId": 2, "originFactId": 8, "medium": "Book", "confidence": 0.25},
	{"reportId": 3, "originFactId": 9, "medium": "Misterio", "confidence": 1.7}]


func _api(transport, validate: bool = false) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	return api


func _window(transport, validate: bool = false) -> Object:
	var window := NarrativeWindow.new()
	window.api = _api(transport, validate)
	window.calendar_of = func() -> Dictionary: return {"hoursPerDay": 10, "daysPerMonth": 5, "monthsPerYear": 2}
	window.tick_now = func() -> int: return 1234
	return window


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /narratives/biographies/7", {"status": 200, "body": BIO})
	transport.on("GET /narratives/chronicles", {"status": 200, "body": CHRONICLE})
	transport.on("GET /narratives/reports", {"status": 200, "body": REPORTS})
	return transport


func test_biography_calls_the_route_and_shows_prose_claims_and_event_ids() -> void:
	var transport := _transport()
	var window := _window(transport)

	await window.show_biography(7, "Ana")

	eq(transport.calls, ["GET /narratives/biographies/7"])
	eq(window.prose, "Ana nasceu e casou.")
	eq(window.claims.size(), 2)
	eq(window._title.text, "Biografia de Ana")
	var texts: Array = window._list.get_children().map(func(c): return c.text if c is Label else "")
	check("[eventos: 9, 11]" in texts[2] and "casou" in texts[2], "afirmação com os eventos que a sustentam: " + str(texts))
	eq(window.period_option.visible, false, "biografia não tem seletor de período")
	window.free()


func test_biography_empty_404_and_5xx_states() -> void:
	var transport := _transport()
	transport.on("GET /narratives/biographies/7", {"status": 200, "body": {"npcId": 7, "prose": "", "claims": []}})
	transport.on("GET /narratives/biographies/8", {"status": 404, "body": null})
	transport.on("GET /narratives/biographies/9", {"status": 500, "body": null})
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	await window.show_biography(7, "Ana")
	check("Nada foi registrado ainda sobre Ana" in window.empty_text, "sem afirmações: " + window.empty_text)
	await window.show_biography(8, "Beto")
	check("Sem biografia" in window.empty_text, "404 tem mensagem própria")
	eq(warnings.size(), 0, "404 não é erro de servidor")
	await window.show_biography(9, "Caio")
	check("erro 500" in window.empty_text, window.empty_text)
	eq(warnings.size(), 1, "5xx avisa")
	eq(window.is_open, true, "a janela segue utilizável")
	window.free()


func test_period_ticks_follow_the_server_calendar() -> void:
	var window := _window(_transport())

	eq(window.period_ticks("month"), [1184, 1234], "mês = 10 h x 5 dias")
	eq(window.period_ticks("year"), [1134, 1234], "ano = mês x 2")
	eq(window.period_ticks("decade"), [234, 1234], "década = 10 anos")
	window.tick_now = func() -> int: return 30
	eq(window.period_ticks("year"), [0, 30], "nunca antes do tick 0")
	window.free()


func test_chronicle_sends_location_period_and_top_k_and_reloads_on_period_change() -> void:
	var transport := _transport()
	var window := _window(transport)

	await window.show_chronicle("city-a", "Alfa")

	var first: String = transport.calls[0]
	check(first.begins_with("GET /narratives/chronicles?location=city-a&periodStart=1134&periodEnd=1234&topK=5"), first)
	eq(window.period_option.visible, true)
	eq(window.prose, "Um ano de colheitas.")
	eq(window._title.text, "Crônica de Alfa")

	window.period_option.select(0)
	window.period_option.item_selected.emit(0)
	await window.get_tree().process_frame if window.is_inside_tree() else null
	check(transport.calls.back().contains("periodStart=1184&periodEnd=1234"), "último mês recarrega: " + transport.calls.back())
	await window.set_period("decade")
	check(transport.calls.back().contains("periodStart=234&periodEnd=1234"), transport.calls.back())
	window.free()


func test_chronicle_empty_period_message() -> void:
	var transport := _transport()
	transport.on("GET /narratives/chronicles", {"status": 200, "body": {"id": 1, "prose": "", "claims": []}})
	var window := _window(transport)

	await window.show_chronicle("city-a", "Alfa")

	check("Nada de notável" in window.empty_text, window.empty_text)
	window.free()


func test_reports_list_with_confidence_bars_and_labels() -> void:
	var transport := _transport()
	var window := _window(transport)

	await window.show_reports()

	eq(transport.calls, ["GET /narratives/reports"])
	eq(window.report_rows.size(), 3)
	check("canção" in window.report_rows[0].text and "fato 5" in window.report_rows[0].text, window.report_rows[0].text)
	eq(window.report_rows[0].bar.value, 82.0, "barra proporcional à confiança")
	eq(window.report_rows[1].bar.value, 25.0)
	check("livro" in window.report_rows[1].text, "meio traduzido")
	check("Misterio" in window.report_rows[2].text, "meio desconhecido aparece como veio")
	eq(window.report_rows[2].bar.value, 100.0, "confiança acima de 1 é limitada")
	window.free()


func test_reports_empty_state() -> void:
	var transport := _transport()
	transport.on("GET /narratives/reports", {"status": 200, "body": []})
	var window := _window(transport)

	await window.show_reports()

	eq(window.empty_text, "Nenhum relato circulando no mundo.")
	eq(window.report_rows.size(), 0)
	window.free()


func test_closed_window_makes_no_requests_and_a_late_reply_is_dropped() -> void:
	var transport := _transport()
	var window := _window(transport)
	tree.root.add_child(window)
	await window.show_biography(7, "Ana")
	var before: int = transport.calls.size()
	for i in 5:
		await tree.process_frame
	eq(transport.calls.size(), before, "nenhuma requisição por frame")

	window.close()
	await window.set_period("month")
	eq(transport.calls.size(), before, "fechada não pede nada")
	eq(window.visible, false)
	window.free()


class SlowTransport extends "res://tests/fake_transport.gd":
	signal release()

	func request(method: String, path: String, body: Variant = null) -> Dictionary:
		if path.begins_with("/narratives/biographies/7"):
			await release
		return await super.request(method, path, body)


func test_a_reply_that_arrives_after_switching_subject_is_ignored() -> void:
	var transport := SlowTransport.new()
	transport.on("GET /narratives/biographies/7", {"status": 200, "body": BIO})
	transport.on("GET /narratives/reports", {"status": 200, "body": REPORTS})
	var window := _window(transport)
	window.show_biography(7, "Ana") # fica esperando
	await window.show_reports()

	transport.release.emit()
	await window.get_tree().process_frame if window.is_inside_tree() else null

	eq(window.mode, "reports")
	eq(window.prose, "", "a biografia tardia não sobrescreve os relatos")
	eq(window.report_rows.size(), 3)
	window.free()


func test_responses_are_checked_against_frozen_shapes() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /narratives/biographies/7", {"status": 200, "body": {"npcId": 7, "claims": []}})
	transport.on("GET /narratives/reports", {"status": 200, "body": [{"reportId": 1}]})
	var window := _window(transport, true)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	await window.show_biography(7, "Ana")
	await window.show_reports()

	eq(warnings.size(), 2, "resposta fora do contrato vira aviso")
	eq(window.report_rows.size(), 0)
	window.free()


func test_only_get_routes_are_used() -> void:
	var transport := _transport()
	var window := _window(transport)
	await window.show_biography(7, "Ana")
	await window.show_chronicle("city-a", "Alfa")
	await window.show_reports()

	eq(transport.calls.filter(func(c: String) -> bool: return not c.begins_with("GET /narratives/")), [], "só leitura")
	window.free()


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
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 77, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	transport.on("GET /npcs/7", Fx.npc_inspect([]))
	return transport


func test_app_buttons_and_r_key_open_the_windows_and_esc_closes() -> void:
	var transport := _app_transport()
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	app.day_cycle.set_calendar({"hoursPerDay": 10, "daysPerMonth": 5, "monthsPerYear": 2})
	await app.select_npc(7)

	_find_button(app.npc_panel, "Biografia").pressed.emit()
	await tree.process_frame
	eq(app.narrative.mode, "biography")
	eq(app.narrative.is_open, true)

	app.city_panel.show_city({"id": "city-a", "name": "Alfa", "indicators": {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}})
	_find_button(app.city_panel, "Crônica").pressed.emit()
	await tree.process_frame
	eq(app.narrative.mode, "chronicle")
	check(transport.calls.any(func(c: String) -> bool: return c.contains("location=city-a&periodStart=0&periodEnd=77")), "usa o tick do relógio e o calendário: " + str(transport.calls))

	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	app._unhandled_input(esc)
	eq(app.narrative.is_open, false, "Esc fecha")

	var r := InputEventKey.new()
	r.keycode = KEY_R
	r.pressed = true
	app._unhandled_input(r)
	await tree.process_frame
	eq(app.narrative.mode, "reports")
	app._unhandled_input(r)
	eq(app.narrative.is_open, false, "R de novo fecha")
	app.queue_free()
