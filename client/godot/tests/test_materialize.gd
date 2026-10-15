extends "res://tests/test_base.gd"

const Panels := preload("res://Scenes/panels.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")


func _panel(transport, validate: bool = false) -> Object:
	var panel := Panels.NpcDetailPanel.new()
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	panel.api = api
	return panel


func _pooled() -> Dictionary:
	var detail := Fx.npc_inspect([], 2)
	detail.body["name"] = "NPC 9"
	return detail


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/9", _pooled())
	var full := Fx.npc_inspect([], 0)
	full.body["name"] = "Ana de Verdade"
	transport.on("POST /npcs/9/materialize", full)
	return transport


func test_an_anonymous_pool_member_offers_to_be_detailed_and_get_never_materializes() -> void:
	var transport := _transport()
	var panel := _panel(transport)

	await panel.show_npc(9)

	eq(panel.materialize_button.visible, true, "lod 2 oferece 'Ver em detalhe'")
	eq(panel.pooled_note.visible, true)
	check("anônimo" in panel.pooled_note.text, panel.pooled_note.text)
	eq(transport.calls, ["GET /npcs/9"], "só o GET, que nunca materializa")
	panel.free()


func test_materialized_and_archived_npcs_do_not_show_the_button() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/1", Fx.npc_inspect([], 0))
	transport.on("GET /npcs/2", Fx.npc_inspect([], 1))
	var panel := _panel(transport)

	await panel.show_npc(1)
	eq(panel.materialize_button.visible, false, "detalhado: sem botão")
	await panel.show_npc(2)
	eq(panel.materialize_button.visible, false, "arquivado: sem botão")
	panel.free()


func test_pressing_the_button_posts_materialize_and_shows_the_full_sheet() -> void:
	var transport := _transport()
	var panel := _panel(transport)
	await panel.show_npc(9)

	panel.materialize_button.pressed.emit()
	await Engine.get_main_loop().process_frame

	eq(transport.calls, ["GET /npcs/9", "POST /npcs/9/materialize"], "comando explícito, uma vez")
	eq(panel._title.text, "Ana de Verdade", "a ficha completa substitui a anônima")
	eq(panel.lod, 0)
	eq(panel.materialize_button.visible, false)
	eq(panel.pooled_note.visible, false)
	await panel.materialize()
	eq(transport.count_calls("POST /npcs/9/materialize"), 1, "já detalhado: não repete")
	panel.free()


func test_materialize_errors_warn_and_keep_the_anonymous_sheet() -> void:
	var transport := _transport()
	var panel := _panel(transport)
	var warnings: Array = []
	panel.warning.connect(func(text: String): warnings.append(text))
	await panel.show_npc(9)

	transport.on("POST /npcs/9/materialize", {"status": 404, "body": null})
	await panel.materialize()
	transport.on("POST /npcs/9/materialize", {"status": 500, "body": null})
	await panel.materialize()

	eq(warnings.size(), 2, "cada falha avisa")
	check("não existe mais" in warnings[0], warnings[0])
	check("erro 500" in warnings[1], warnings[1])
	eq(panel.materialize_button.visible, true, "segue podendo tentar de novo")
	eq(panel._title.text, "Morador anônimo #9")
	panel.free()


func test_the_materialize_reply_is_checked_against_the_frozen_npc_shape() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/9", _pooled())
	transport.on("POST /npcs/9/materialize", {"status": 200, "body": {"name": "só o nome"}})
	var panel := _panel(transport)
	var warnings: Array = []
	panel.warning.connect(func(text: String): warnings.append(text))
	panel.api.validate_shapes = false
	await panel.show_npc(9) # o GET do teste não tem a ficha inteira: sem validar aqui
	panel.api.validate_shapes = true

	await panel.materialize()

	eq(warnings.size(), 1, "resposta fora do contrato vira aviso")
	eq(panel._title.text, "Morador anônimo #9", "e não troca a ficha")
	panel.free()


func test_a_reply_after_selecting_another_npc_is_dropped() -> void:
	var transport := SlowTransport.new()
	transport.on("GET /npcs/9", _pooled())
	transport.on("GET /npcs/5", Fx.npc_inspect([], 0))
	var full := Fx.npc_inspect([], 0)
	full.body["name"] = "Ana de Verdade"
	transport.on("POST /npcs/9/materialize", full)
	var panel := _panel(transport)
	await panel.show_npc(9)
	panel.materialize() # fica esperando
	await panel.show_npc(5)

	transport.release.emit()
	await Engine.get_main_loop().process_frame

	eq(panel.npc_id, 5)
	eq(panel._title.text, "Ana", "a resposta tardia do NPC 9 não sobrescreve o NPC 5")
	panel.free()


class SlowTransport extends "res://tests/fake_transport.gd":
	signal release()

	func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
		if path.contains("/materialize"):
			await release
		return await super.request(method, path, body, extra_headers)


func _find_button(node: Node, text: String) -> Button:
	for child in node.get_children():
		if child is Button and child.text == text:
			return child
		var found := _find_button(child, text)
		if found != null:
			return found
	return null


func test_the_app_wires_the_button_and_the_warning_toast() -> void:
	var transport := _transport()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)

	await app.select_npc(9)
	_find_button(app.npc_panel, "Ver em detalhe").pressed.emit()
	await tree.process_frame
	eq(app.npc_panel.lod, 0, "o botão do app materializa")

	transport.on("POST /npcs/9/materialize", {"status": 500, "body": null})
	app.npc_panel.lod = 2
	await app.npc_panel.materialize()
	check(app.toasts.history.any(func(t: String) -> bool: return "erro 500" in t), "o aviso chega ao toast: " + str(app.toasts.history))
	app.queue_free()


func test_an_anonymous_sheet_hides_the_meaningless_blocks_and_names_it_anonymous() -> void:
	var transport := _transport()
	var panel := _panel(transport)
	await panel.show_npc(9)

	eq(panel._title.text, "Morador anônimo #9", "sem nome real: título anônimo")
	eq(panel._subtitle.text, "faz parte da multidão da cidade")
	eq(panel._grid.visible, false, "sem barras de necessidades inventadas")
	eq(panel._details.visible, false)
	for control in panel._sheet_only:
		eq(control.visible, false, "botão de NPC detalhado fica oculto no anônimo: " + control.text)
	eq(panel.trades_button.visible, false)

	await panel.materialize()
	eq(panel._grid.visible, true, "materializado: barras voltam")
	for control in panel._sheet_only:
		eq(control.visible, true, "botões voltam: " + control.text)
	eq(panel._title.text, "Ana de Verdade")
	panel.free()


func test_the_city_sheet_offers_to_meet_an_anonymous_resident_and_opens_its_sheet() -> void:
	var transport := _transport()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	var indicators := {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}

	app.city_panel.show_city({"id": "c", "name": "Alfa", "indicators": indicators, "pendingResidentIds": []})
	eq(app.city_panel.anonymous_button.visible, false, "sem moradores anônimos: sem botão")
	app.city_panel.show_city({"id": "c", "name": "Alfa", "indicators": indicators, "pendingResidentIds": [{"value": 9}, {"value": 10}]})
	eq(app.city_panel.anonymous_button.visible, true)
	check("2 moradores anônimos" in app.city_panel.anonymous_button.text, app.city_panel.anonymous_button.text)

	app.city_panel.anonymous_button.pressed.emit()
	await tree.process_frame
	await tree.process_frame

	check(transport.calls.has("GET /npcs/9"), "abre a ficha do primeiro morador anônimo: " + str(transport.calls))
	eq(app.npc_panel.lod, 2)
	eq(app.npc_panel.materialize_button.visible, true, "e daí é só 'Ver em detalhe'")
	app.queue_free()
