extends "res://tests/test_base.gd"

const ConversationWindow := preload("res://Scenes/conversation_window.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")


func _api(transport) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = false
	return api


func _window(transport) -> Object:
	var window := ConversationWindow.new()
	window.api = _api(transport)
	return window


class SlowTransport extends "res://tests/fake_transport.gd":
	signal release()

	func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
		if path.begins_with("/conversations/send"):
			await release
		return await super.request(method, path, body, extra_headers)


func _accepting(slow: bool = false) -> Object:
	var transport := SlowTransport.new() if slow else FakeTransport.new()
	transport.on("POST /conversations/start", {"status": 200, "body": {"decision": "Accepted", "sessionId": 9}})
	transport.on("POST /conversations/send", {"status": 200, "body": {"dialogue": "Bom dia!", "emotion": "happy", "intent": "greet", "proposedActions": ["Eat"]}})
	transport.on("POST /conversations/end", {"status": 200, "body": {"result": "Ok"}})
	return transport


func test_start_posts_the_npc_and_only_accepted_opens_the_window() -> void:
	var transport := _accepting()
	var window := _window(transport)
	await window.start(7, "Ana")
	eq(transport.bodies["POST /conversations/start"], [{"npcId": 7}], "pede a conversa com o NPC")
	eq(window.is_open, true)
	eq(window.session_id, 9)
	window.free()

	var expected := {"RejectedBusy": "ocupado", "RejectedHostile": "não quer falar", "RejectedUnavailable": "não está disponível"}
	for decision in expected:
		var refused := FakeTransport.new()
		refused.on("POST /conversations/start", {"status": 200, "body": {"decision": decision, "sessionId": null}})
		var other := _window(refused)
		var warnings: Array = []
		other.warning.connect(func(text: String): warnings.append(text))
		await other.start(7, "Ana")
		eq(other.is_open, false, decision + ": a janela não abre")
		check(warnings.size() == 1 and expected[decision] in warnings[0], "%s: motivo em português: %s" % [decision, str(warnings)])
		other.free()


func test_send_shows_dialogue_and_emotion_and_ignores_proposed_actions() -> void:
	var transport := _accepting()
	var window := _window(transport)
	await window.start(7, "Ana")

	await window.send("Olá")

	eq(transport.bodies["POST /conversations/send"], [{"sessionId": 9, "message": "Olá"}])
	eq(window.lines.size(), 2, "fala do jogador e resposta")
	eq(window.lines[1], {"who": "Ana", "text": "Bom dia!", "emotion": "happy"})
	eq(window.status_text, "", "acabou o 'pensando…'")
	var writes: Array = transport.calls.filter(func(c: String) -> bool: return c.begins_with("POST") and not c.begins_with("POST /conversations/"))
	eq(writes, [], "nenhuma rota de escrita além das três de conversa")
	window.free()


func test_thinking_state_then_timeout_drops_the_late_reply() -> void:
	var transport := _accepting(true)
	var window := _window(transport)
	tree.root.add_child(window)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))
	await window.start(7, "Ana")
	window.send("Olá") # sem await: ainda esperando
	eq(window.waiting, true, "mostra 'pensando…' enquanto aguarda")
	check(not window._deadline.is_stopped() and window._deadline.time_left > 7.0, "o prazo de 8 s foi armado ao enviar")
	check("pensando" in window.status_text)
	window._deadline.timeout.emit()
	eq(window.waiting, false, "a espera acaba no prazo + margem")
	eq(warnings.size(), 1, "avisa que demorou")
	transport.release.emit()
	await tree.process_frame
	eq(window.lines.size(), 1, "a resposta tardia não entra no histórico")
	window.free()


func test_404_and_409_end_the_window_with_a_warning() -> void:
	for case in [[404, null, "não existe mais"], [409, "npc-dead", "morreu"], [409, "session-ended", "encerrada"]]:
		var transport := _accepting()
		transport.on("POST /conversations/send", {"status": case[0], "body": case[1]})
		var window := _window(transport)
		var warnings: Array = []
		window.warning.connect(func(text: String): warnings.append(text))
		await window.start(7, "Ana")
		await window.send("Olá")
		eq(window.is_open, false, "%d fecha a janela" % case[0])
		check(warnings.size() == 1 and case[2] in warnings[0], "%d avisa: %s" % [case[0], str(warnings)])
		window.free()


func test_closing_posts_end_with_the_session() -> void:
	var transport := _accepting()
	var window := _window(transport)
	await window.start(7, "Ana")
	await window.end_conversation()
	eq(transport.bodies["POST /conversations/end"], [{"sessionId": 9}])
	eq(window.is_open, false)
	await window.end_conversation()
	eq(transport.count_calls("POST /conversations/end"), 1, "fechar de novo não reenvia")
	window.free()


func test_shapes_are_frozen_and_checked() -> void:
	var start_shape = ApiClient.ShapeValidator.load_shape("conversation-start")
	var send_shape = ApiClient.ShapeValidator.load_shape("conversation-send")
	eq(ApiClient.ShapeValidator.validate({"decision": "RejectedBusy", "sessionId": null}, start_shape), [])
	check(not ApiClient.ShapeValidator.validate({"sessionId": 1}, start_shape).is_empty(), "sem decision reprova")
	check(not ApiClient.ShapeValidator.validate({"dialogue": "x", "emotion": "happy", "intent": 3, "proposedActions": []}, send_shape).is_empty(), "intent não-string reprova")


func test_app_button_and_t_key_start_a_conversation_and_esc_ends_it() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 0, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	transport.on("GET /npcs/7", Fx.npc_inspect([]))
	transport.on("POST /conversations/start", {"status": 200, "body": {"decision": "Accepted", "sessionId": 9}})
	transport.on("POST /conversations/end", {"status": 200, "body": {"result": "Ok"}})
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	await app.select_npc(7)

	var button: Button = _find_button(app.npc_panel, "Conversar (T)")
	button.pressed.emit()
	await tree.process_frame
	eq(app.conversation.is_open, true, "o botão Conversar abre")

	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	app._unhandled_input(esc)
	await tree.process_frame
	eq(app.conversation.is_open, false, "Esc encerra")
	eq(transport.count_calls("POST /conversations/end"), 1)

	var t := InputEventKey.new()
	t.keycode = KEY_T
	t.pressed = true
	app._unhandled_input(t)
	await tree.process_frame
	eq(app.conversation.is_open, true, "T conversa com o NPC selecionado")
	app.queue_free()


func _find_button(node: Node, text: String) -> Button:
	for child in node.get_children():
		if child is Button and child.text == text:
			return child
		var found := _find_button(child, text)
		if found != null:
			return found
	return null


func test_emotion_is_shown_with_portuguese_name_and_colour() -> void:
	var window := _window(_accepting())
	await window.start(7, "Ana")
	await window.send("Olá")

	var label: Label = window._line_node(window.lines[1])

	check("(feliz)" in label.text and "Bom dia!" in label.text, "nome da emoção em português: " + label.text)
	eq(label.get_theme_color("font_color"), Color("7ccf88"), "cor da emoção")
	check(not (window._line_node(window.lines[0]) as Label).has_theme_color_override("font_color"), "fala do jogador sem cor de emoção")
	window.free()


func test_reply_deadline_is_the_server_budget_plus_margin() -> void:
	var window := _window(_accepting())
	eq(window._deadline.wait_time, 8.0, "5 s do servidor + margem")
	check(window._deadline.one_shot, "prazo único")
	window.free()


func test_a_malformed_start_reply_is_rejected_by_the_frozen_shape() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /conversations/start", {"status": 200, "body": {"sessionId": 3}})
	var window := ConversationWindow.new()
	window.api = ApiClient.new(transport) # validate_shapes ligado
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	await window.start(7, "Ana")

	eq(window.is_open, false, "resposta fora do contrato não abre a janela")
	eq(warnings.size(), 1)
	window.free()


func test_open_window_makes_no_requests_while_frames_pass() -> void:
	var transport := _accepting()
	var window := _window(transport)
	tree.root.add_child(window)
	await window.start(7, "Ana")
	var before: int = transport.calls.size()
	for i in 5:
		await tree.process_frame
	eq(transport.calls.size(), before, "nenhuma requisição por frame")
	window.free()


func test_returning_to_the_menu_ends_the_open_conversation_once() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 0, "year": 0}})
	transport.on("POST /conversations/start", {"status": 200, "body": {"decision": "Accepted", "sessionId": 4}})
	transport.on("POST /conversations/end", {"status": 200, "body": {"result": "Ok"}})
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	await app.talk_to(7)

	await app.release_observation()

	eq(transport.count_calls("POST /conversations/end"), 1, "voltar ao menu encerra a conversa uma vez")
	app.queue_free()


func test_esc_inside_the_text_field_ends_the_conversation_once() -> void:
	var transport := _accepting()
	var window := _window(transport)
	await window.start(7, "Ana")
	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true

	window.input.gui_input.emit(esc)
	await tree.process_frame

	eq(window.is_open, false, "Esc com o foco no campo encerra")
	eq(transport.count_calls("POST /conversations/end"), 1)
	window.free()


func test_a_5xx_on_send_warns_and_leaves_the_window_usable() -> void:
	var transport := _accepting()
	transport.on("POST /conversations/send", {"status": 500, "body": null})
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))
	await window.start(7, "Ana")

	await window.send("Olá")

	eq(warnings.size(), 1, "avisa o erro")
	check("erro 500" in warnings[0], warnings[0])
	eq(window.is_open, true, "a janela segue aberta")
	eq(window.waiting, false, "pode tentar de novo")
	window.free()


func test_api_rejects_start_and_send_bodies_that_break_the_frozen_shape() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /conversations/start", {"status": 200, "body": {"sessionId": 3}})
	transport.on("POST /conversations/send", {"status": 200, "body": {"dialogue": "oi", "intent": "greet", "proposedActions": []}})
	var api := ApiClient.new(transport) # validate_shapes ligado
	var errors: Array = []
	api.contract_error.connect(func(route: String, _e: Array): errors.append(route))

	var started: Dictionary = await api.conversation_start(7)
	var sent: Dictionary = await api.conversation_send(3, "oi")

	eq(started.ok, false, "start sem 'decision' reprova")
	eq(sent.ok, false, "send sem 'emotion' reprova")
	eq(errors, ["/conversations/start", "/conversations/send"])


func test_end_button_ends_the_conversation_once() -> void:
	var transport := _accepting()
	var window := _window(transport)
	await window.start(7, "Ana")

	_find_button(window, "Encerrar (Esc)").pressed.emit()
	await tree.process_frame

	eq(window.is_open, false)
	eq(transport.count_calls("POST /conversations/end"), 1, "o botão envia /end uma vez")
	window.free()


func test_full_cycle_only_calls_the_three_conversation_routes() -> void:
	var transport := _accepting()
	var window := _window(transport)
	await window.start(7, "Ana")
	await window.send("Olá")
	await window.end_conversation()

	eq(transport.calls, ["POST /conversations/start", "POST /conversations/send", "POST /conversations/end"], "início, envio e fim: nada além")
	window.free()
