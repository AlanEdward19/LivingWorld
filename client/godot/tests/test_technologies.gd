extends "res://tests/test_base.gd"

const TechnologyWindow := preload("res://Scenes/technology_window.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")

const TECHS := [
	{"category": "mobilidade", "vector": [2, 4, 6], "key": "2.4.6", "discoveredAtTick": 50, "isVariation": false, "variationOfKey": null, "adoptedByCities": 1},
	{"category": "mobilidade", "vector": [3, 4, 6], "key": "3.4.6", "discoveredAtTick": 80, "isVariation": true, "variationOfKey": "2.4.6", "adoptedByCities": 3}]


func _window(transport, validate: bool = false) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	var window := TechnologyWindow.new()
	window.api = api
	window.date_of = func(tick: int) -> String: return "dia %d" % tick
	return window


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /cultures/1/technologies", {"status": 200, "body": TECHS})
	return transport


func test_technologies_show_key_axes_as_bars_adoption_and_variation() -> void:
	var transport := _transport()
	var window := _window(transport)

	await window.show_culture(1)

	eq(transport.calls, ["GET /cultures/1/technologies"])
	eq(window.rows.size(), 2)
	eq(window.rows[0].title, "Tecnologia 2.4.6", "sem nome: a chave identifica (ADR-0013)")
	eq(window.rows[1].title, "Tecnologia 3.4.6 (variação de 2.4.6)")
	eq(window.rows[0].detail, "mobilidade · descoberta em dia 50 · adotada por 1 cidade")
	eq(window.rows[1].detail, "mobilidade · descoberta em dia 80 · adotada por 3 cidades")
	eq(window.rows[0].bars.size(), 3, "eixos ritmo, carga e alcance")
	eq(window.rows[0].axes, ["Ritmo", "Carga", "Alcance"], "nomes dos eixos, na ordem do vetor")
	eq([window.rows[0].bars[0].value, window.rows[0].bars[1].value, window.rows[0].bars[2].value], [2.0, 4.0, 6.0], "barras = eixos do vetor")
	eq(window.rows[0].bars[0].max_value, 6.0, "escala = maior eixo da janela")
	window.free()


func test_empty_404_and_error_states_and_contract_check() -> void:
	var transport := _transport()
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	transport.on("GET /cultures/1/technologies", {"status": 200, "body": []})
	await window.show_culture(1)
	eq(window.empty_text, "Esta cultura ainda não descobriu nenhuma tecnologia.")
	transport.on("GET /cultures/1/technologies", {"status": 404, "body": null})
	await window.show_culture(1)
	eq(window.empty_text, "Esta cultura não existe mais.")
	eq(warnings.size(), 0)
	transport.on("GET /cultures/1/technologies", {"status": 500, "body": null})
	await window.show_culture(1)
	check("erro 500" in window.empty_text, window.empty_text)
	eq(warnings.size(), 1)

	var strict := FakeTransport.new()
	strict.on("GET /cultures/1/technologies", {"status": 200, "body": [{"key": "1"}]})
	var checked := _window(strict, true)
	checked.warning.connect(func(text: String): warnings.append(text))
	await checked.show_culture(1)
	eq(warnings.size(), 2, "corpo fora do contrato vira aviso")
	eq(checked.rows.size(), 0)
	window.free()
	checked.free()


func test_closed_window_is_silent_read_only_and_drops_late_replies() -> void:
	var transport := _transport()
	var window := _window(transport)
	await window.show_culture(1)
	window.close()
	eq(window.visible, false)
	eq(transport.calls.filter(func(c: String) -> bool: return not c.begins_with("GET /cultures/")), [], "só leitura")
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
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var with_culture := Fx.npc_inspect([])
	with_culture.body["culture"] = {"id": 1}
	transport.on("GET /npcs/7", with_culture)
	var other := Fx.npc_inspect([])
	other.body["culture"] = {"id": 2}
	transport.on("GET /npcs/8", other)
	transport.on("GET /cultures/2/technologies", {"status": 200, "body": []})
	return transport


func test_the_button_appears_only_when_the_culture_has_technologies() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)

	await app.select_npc(8)
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, false, "cultura sem tecnologias: sem botão")

	await app.select_npc(7)
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, true, "cultura com tecnologias: botão")
	_find_button(app.npc_panel, "Tecnologias da cultura").pressed.emit()
	await tree.process_frame
	eq(app.technology.is_open, true)
	eq(app.technology.culture_id, 1, "abre a cultura do NPC")

	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	app._unhandled_input(esc)
	eq(app.technology.is_open, false, "Esc fecha")

	await app.select_npc(8)
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, false, "trocar para cultura sem tecnologia esconde o botão")
	app.queue_free()


class SlowTransport extends "res://tests/fake_transport.gd":
	signal release()

	func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
		if path.contains("/cultures/1/technologies"):
			await release
		return await super.request(method, path, body, extra_headers)


func test_close_button_and_a_reply_that_arrives_after_closing() -> void:
	var transport := SlowTransport.new()
	transport.on("GET /cultures/1/technologies", {"status": 200, "body": TECHS})
	var window := _window(transport)
	window.show_culture(1) # fica esperando
	_find_button(window, "Fechar (Esc)").pressed.emit()
	eq(window.is_open, false, "o botão Fechar fecha")

	transport.release.emit()
	await Engine.get_main_loop().process_frame

	eq(window.rows.size(), 0, "a resposta que chega depois de fechar não é desenhada")
	window.free()


func test_an_open_window_makes_no_requests_while_frames_pass() -> void:
	var transport := _transport()
	var window := _window(transport)
	tree.root.add_child(window)
	await window.show_culture(1)
	var before: int = transport.calls.size()
	for i in 5:
		await tree.process_frame
	eq(transport.calls.size(), before, "nenhuma requisição por frame")
	window.free()


func test_a_probe_error_hides_the_button_and_a_stale_probe_is_ignored() -> void:
	var transport := _app_transport()
	var slow := SlowTransport.new()
	slow.routes = transport.routes.duplicate()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(slow, false)

	await app.select_npc(7)
	await tree.process_frame
	slow.release.emit()
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, true, "cultura 1 com tecnologias")

	slow.on("GET /cultures/1/technologies", {"status": 500, "body": null})
	slow.release.emit()
	await app.select_npc(7)
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, false, "erro na checagem: botão oculto")

	slow.on("GET /cultures/1/technologies", {"status": 200, "body": TECHS})
	app.select_npc(7) # a checagem da cultura 1 fica pendente (transporte lento)
	await tree.process_frame
	await app.select_npc(8) # o usuário troca para a cultura 2 (sem tecnologias)
	await tree.process_frame
	slow.release.emit() # a resposta antiga da cultura 1 chega agora
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, false, "a resposta velha da cultura 1 não acende o botão da cultura 2")
	app.queue_free()


func test_a_server_error_in_the_probe_hides_the_button_for_real() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	await app.select_npc(7)
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, true, "com tecnologias: botão")

	transport.on("GET /cultures/1/technologies", {"status": 500, "body": null})
	await app.select_npc(7)
	await tree.process_frame
	eq(app.npc_panel.tech_button.visible, false, "5xx na checagem: sem botão")
	app.queue_free()


func test_overlapping_requests_draw_only_the_latest_culture() -> void:
	var transport := SlowTransport.new()
	transport.on("GET /cultures/1/technologies", {"status": 200, "body": TECHS})
	transport.on("GET /cultures/2/technologies", {"status": 200, "body": [TECHS[1]]})
	var window := _window(transport)
	window.show_culture(1) # fica esperando (transporte lento só para a cultura 1)
	await window.show_culture(2)

	transport.release.emit()
	await Engine.get_main_loop().process_frame

	eq(window.culture_id, 2)
	eq(window.rows.size(), 1, "a resposta tardia da cultura 1 não se soma à da 2")
	eq(window.rows[0].title, "Tecnologia 3.4.6 (variação de 2.4.6)")
	window.free()


func test_the_app_formats_technology_dates_with_the_server_calendar() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)

	await app.technology.show_culture(1)

	check("Ano 0 · Mês 1 · Dia 3" in app.technology.rows[0].detail, "tick 50 em data do calendário: " + app.technology.rows[0].detail)
	app.queue_free()
