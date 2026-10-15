extends "res://tests/test_base.gd"

const AdminWindow := preload("res://Scenes/admin_window.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")

const PAUSED := {"isPaused": true, "actor": "alan", "reason": "manutenção", "maxTicks": 70, "pausedAtTick": 12}
const RUNNING := {"isPaused": false, "actor": null, "reason": null, "maxTicks": null, "pausedAtTick": null}
const HITS := [
	{"id": 4, "name": "Ana", "city": "Alfa", "professionId": 2, "alive": true},
	{"id": 9, "name": "Ana Maria", "city": "", "professionId": 3, "alive": false}]
const AUDIT := [
	{"branchId": 1, "sequence": 1, "tick": 12, "actor": "alan", "action": "world.pause", "target": "world", "detail": "reason=x;maxTicks=70"},
	{"branchId": 1, "sequence": 2, "tick": 30, "actor": "alan", "action": "world.resume", "target": "world", "detail": ""}]


func _api(transport, validate: bool = false) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	return api


func _window(transport, validate: bool = false) -> Object:
	var window := AdminWindow.new()
	window.api = _api(transport, validate)
	window.calendar_of = func() -> Dictionary: return {"hoursPerDay": 10}
	window.date_of = func(tick: int) -> String: return "dia %d" % tick
	window.actor = "alan"
	window.open()
	return window


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("POST /admin/world/pause", {"status": 200, "body": PAUSED})
	transport.on("POST /admin/world/resume", {"status": 200, "body": RUNNING})
	transport.on("POST /admin/world/speed", {"status": 200, "body": RUNNING})
	transport.on("GET /admin/search", {"status": 200, "body": HITS})
	transport.on("GET /admin/audit", {"status": 200, "body": AUDIT})
	return transport


func test_pause_sends_reason_ceiling_and_the_operator_header() -> void:
	var transport := _transport()
	var window := _window(transport)
	window.reason_input.text = "  manutenção  "
	window.max_spin.value = 70

	await window.pause()

	eq(transport.bodies.get("POST /admin/world/pause", []), [{"reason": "manutenção", "maxTicks": 70}])
	eq(transport.headers_of.get("POST /admin/world/pause", []), [{"X-Admin-Actor": "alan"}], "o operador vai no cabeçalho")
	check("Pausado por alan desde dia 12: manutenção" in window.state_text, window.state_text)
	eq(window.pause_info.reason, "manutenção")
	window.free()


func test_pause_is_validated_on_the_client_before_any_request() -> void:
	var transport := _transport()
	var window := _window(transport)
	eq(window.max_spin.value, 70.0, "padrão = 7 dias do calendário")
	eq(window.max_spin.max_value, 300.0, "teto = 30 dias do calendário")

	await window.pause()
	eq(window.state_text, "Informe o motivo da pausa.")
	window.reason_input.text = "x"
	window.max_spin.max_value = 100000
	window.max_spin.value = 301
	await window.pause()
	check("entre 1 e 300" in window.state_text, window.state_text)
	window.max_spin.min_value = 0
	window.max_spin.value = 0
	await window.pause()
	check("entre 1 e 300" in window.state_text, window.state_text)

	eq(transport.calls, [], "nada foi ao servidor")
	window.free()


func test_server_refusals_are_shown_and_warn() -> void:
	var transport := _transport()
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))
	window.reason_input.text = "x"

	transport.on("POST /admin/world/pause", {"status": 400, "body": {"error": "maxTicks: entre 1 e 300"}})
	await window.pause()
	check("O servidor recusou: maxTicks: entre 1 e 300" in window.state_text, window.state_text)
	transport.on("POST /admin/world/pause", {"status": 403, "body": null})
	await window.pause()
	check("Acesso negado" in window.state_text, window.state_text)
	transport.on("POST /admin/world/pause", {"status": 500, "body": null})
	await window.pause()
	check("erro 500" in window.state_text, window.state_text)

	eq(warnings.size(), 3, "toda falha avisa")
	eq(window.pause_info, {}, "falha não lembra pausa")
	window.free()


func test_resume_clears_the_remembered_pause_and_speed_posts_the_value() -> void:
	var transport := _transport()
	var window := _window(transport)
	window.reason_input.text = "x"
	await window.pause()

	await window.resume()
	eq(window.pause_info, {})
	eq(window.state_text, "Mundo retomado.")
	eq(transport.headers_of.get("POST /admin/world/resume", []), [{"X-Admin-Actor": "alan"}])

	await window.set_speed(4.0)
	eq(transport.bodies.get("POST /admin/world/speed", []), [{"ticksPerSecond": 4.0}])
	eq(transport.headers_of.get("POST /admin/world/speed", []), [{"X-Admin-Actor": "alan"}])
	await window.set_speed(0.0)
	eq(transport.count_calls("POST /admin/world/speed"), 1, "velocidade não positiva nem sai")
	window.free()


func test_banner_names_who_paused_and_why_else_just_says_paused() -> void:
	var window := _window(_transport())
	eq(window.pause_banner(false), "", "rodando: sem faixa")
	eq(window.pause_banner(true), "Mundo pausado", "outro operador pausou: o cliente só sabe que está pausado")

	window.reason_input.text = "manutenção"
	await window.pause()
	eq(window.pause_banner(true), "Pausado por alan desde dia 12: manutenção")
	window.note_running()
	eq(window.pause_banner(true), "Mundo pausado")
	window.free()


func test_search_sends_query_kind_and_header_and_lists_hits() -> void:
	var transport := _transport()
	var window := _window(transport)
	window.search_input.text = "Ana Maria"
	window.kind_option.select(1)

	await window.search()

	eq(transport.calls, ["GET /admin/search?q=Ana%20Maria&kind=city"])
	eq(transport.headers_of.get("GET /admin/search?q=Ana%20Maria&kind=city", []), [{"X-Admin-Actor": "alan"}])
	eq(window.result_list.item_count, 2)
	check("#4 · Ana · Alfa · ofício 2" in window.result_list.get_item_text(0), window.result_list.get_item_text(0))
	check("sem cidade" in window.result_list.get_item_text(1) and "morto" in window.result_list.get_item_text(1), window.result_list.get_item_text(1))
	window.free()


func test_search_result_opens_the_npc_sheet_and_states_are_explicit() -> void:
	var transport := _transport()
	var window := _window(transport)
	var selected: Array = []
	window.npc_selected.connect(func(id: int): selected.append(id))
	window.search_input.text = "ana"
	await window.search()

	window.result_list.item_selected.emit(1)
	eq(selected, [9], "o clique abre a ficha do NPC do resultado")

	window.search_input.text = ""
	await window.search()
	eq(window.search_text, "Digite o que buscar.")
	transport.on("GET /admin/search", {"status": 200, "body": []})
	window.search_input.text = "zzz"
	await window.search()
	eq(window.search_text, "Nenhum NPC encontrado.")
	transport.on("GET /admin/search", {"status": 400, "body": {"error": "q: profissão é o número do ofício"}})
	await window.search()
	check("O servidor recusou" in window.search_text, window.search_text)
	transport.on("GET /admin/search", {"status": 403, "body": null})
	await window.search()
	check("Acesso negado" in window.search_text, window.search_text)
	eq(window.result_list.item_count, 0)
	window.free()


func test_audit_table_has_who_when_action_and_target() -> void:
	var transport := _transport()
	var window := _window(transport)
	window.audit_from.value = 5

	await window.load_audit()

	eq(transport.calls, ["GET /admin/audit?fromTick=5"])
	eq(transport.headers_of.get("GET /admin/audit?fromTick=5", []), [{"X-Admin-Actor": "alan"}])
	eq(window.audit_rows, ["dia 12 · alan · world.pause · world · reason=x;maxTicks=70", "dia 30 · alan · world.resume · world"])

	transport.on("GET /admin/audit", {"status": 200, "body": []})
	await window.load_audit()
	eq(window.audit_state, "Nenhuma ação registrada a partir deste tick.")
	eq(window.audit_rows.size(), 0)
	transport.on("GET /admin/audit", {"status": 403, "body": null})
	await window.load_audit()
	check("Acesso negado" in window.audit_state, window.audit_state)
	window.free()


func test_responses_are_checked_against_frozen_shapes() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /admin/world/pause", {"status": 200, "body": {"actor": "alan"}})
	transport.on("GET /admin/search", {"status": 200, "body": [{"id": 1}]})
	transport.on("GET /admin/audit", {"status": 200, "body": [{"tick": 1}]})
	var window := _window(transport, true)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))
	window.reason_input.text = "x"
	window.search_input.text = "a"

	await window.pause()
	await window.search()
	await window.load_audit()

	eq(warnings.size(), 3, "resposta fora do contrato vira aviso nas três abas")
	eq(window.pause_info, {})
	eq(window.hits.size(), 0)
	window.free()


func test_closed_window_asks_nothing() -> void:
	var transport := _transport()
	var window := _window(transport)
	window.close()
	eq(window.visible, false)
	eq(transport.calls, [])
	window.free()


func _find_button(node: Node, text: String) -> Button:
	for child in node.get_children():
		if child is Button and child.text == text:
			return child
		var found := _find_button(child, text)
		if found != null:
			return found
	return null


func _app(enabled: bool, status: Dictionary = {}) -> Array:
	var transport := _transport()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": status if not status.is_empty() else {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	transport.on("GET /npcs/9", Fx.npc_inspect([]))
	var app := App.new()
	app.admin_enabled = enabled
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	return [app, transport]


func test_without_a_token_the_console_does_not_appear_or_open() -> void:
	var made := await _app(false)
	var app = made[0]

	eq(_find_button(app, "Console (F8)"), null, "sem LW_ADMIN_TOKEN não há botão")
	var key := InputEventKey.new()
	key.keycode = KEY_F8
	key.pressed = true
	app._unhandled_input(key)
	eq(app.admin_window.is_open, false, "nem pelo atalho")
	app.queue_free()


func test_with_a_token_f8_and_the_button_toggle_and_esc_closes_and_results_open_the_sheet() -> void:
	var made := await _app(true)
	var app = made[0]
	var transport = made[1]
	var key := InputEventKey.new()
	key.keycode = KEY_F8
	key.pressed = true

	app._unhandled_input(key)
	eq(app.admin_window.is_open, true, "F8 abre")
	app._unhandled_input(key)
	eq(app.admin_window.is_open, false, "F8 fecha")
	_find_button(app, "Console (F8)").pressed.emit()
	eq(app.admin_window.is_open, true, "o botão abre")

	app.admin_window.search_input.text = "ana"
	await app.admin_window.search()
	app.admin_window.result_list.item_selected.emit(1)
	await tree.process_frame
	check(transport.calls.any(func(c: String) -> bool: return c == "GET /npcs/9"), "o resultado abre a ficha: " + str(transport.calls))

	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	app._unhandled_input(esc)
	eq(app.admin_window.is_open, false, "Esc fecha")
	app.queue_free()


func test_everyone_sees_the_paused_banner_and_it_names_this_clients_own_pause() -> void:
	var made := await _app(false)
	var app = made[0]

	app._on_clock_status({"tick": 6, "ticksPerSecond": 1.0, "isPaused": true, "year": 0})
	eq(app.pause_label.text, "Mundo pausado", "qualquer jogador vê a pausa")

	app.admin_window.pause_info = {"actor": "alan", "reason": "manutenção", "pausedAtTick": 6}
	app._on_clock_status({"tick": 6, "ticksPerSecond": 1.0, "isPaused": true, "year": 0})
	check("Pausado por alan desde" in app.pause_label.text and "manutenção" in app.pause_label.text, app.pause_label.text)

	app._on_clock_status({"tick": 7, "ticksPerSecond": 1.0, "isPaused": false, "year": 0})
	eq(app.pause_label.text, "", "voltou a andar: faixa some")
	eq(app.admin_window.pause_info, {}, "e a pausa lembrada também")
	app.queue_free()


func test_the_real_transport_sends_the_operator_header_and_the_token() -> void:
	var transport := preload("res://Contract/http_transport.gd").new()

	var headers: PackedStringArray = transport.build_headers({"X-Admin-Actor": "alan"})

	check("X-Admin-Actor: alan" in headers, "o operador vai no cabeçalho real: " + str(headers))
	check("Content-Type: application/json" in headers, "continua JSON")
	eq(transport.build_headers().has("X-Admin-Actor: alan"), false, "sem extras não inventa cabeçalho")
	transport.free()


func test_an_open_console_makes_no_requests_while_frames_pass() -> void:
	var transport := _transport()
	var window := _window(transport)
	tree.root.add_child(window)
	for i in 5:
		await tree.process_frame
	eq(transport.calls, [], "nenhuma requisição por frame")
	window.free()


func test_the_real_transport_sends_the_admin_token_only_when_configured() -> void:
	var transport := preload("res://Contract/http_transport.gd").new()
	var saved := OS.get_environment("LW_ADMIN_TOKEN")

	OS.set_environment("LW_ADMIN_TOKEN", "tok-abc")
	check("X-Admin-Token: tok-abc" in transport.build_headers({"X-Admin-Actor": "alan"}), "o token configurado vai no cabeçalho")
	OS.unset_environment("LW_ADMIN_TOKEN")
	var without: PackedStringArray = transport.build_headers()
	eq(without.size(), 2, "sem LW_ADMIN_TOKEN não há cabeçalho de admin")
	check(not ("X-Admin-Token: tok-abc" in without), "e o token antigo não vaza")

	if saved != "":
		OS.set_environment("LW_ADMIN_TOKEN", saved)
	transport.free()


class _SpyTransport extends "res://Contract/http_transport.gd":
	var seen: Array = []

	func build_headers(extra_headers: Dictionary = {}) -> PackedStringArray:
		seen.append(extra_headers)
		return super.build_headers(extra_headers)


func test_request_hands_its_extra_headers_to_the_header_builder() -> void:
	var transport := _SpyTransport.new()
	transport.base_url = "http://127.0.0.1:1" # nada escuta aqui: só interessa o que `request` monta
	tree.root.add_child(transport)

	var response: Dictionary = await transport.request("GET", "/x", null, {"X-Admin-Actor": "alan"})

	eq(response.status, 0, "sem servidor, a requisição falha limpa")
	eq(transport.seen, [{"X-Admin-Actor": "alan"}], "request repassa o operador ao montar os cabeçalhos")
	transport.free()
