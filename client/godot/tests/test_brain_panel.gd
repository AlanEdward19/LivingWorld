extends "res://tests/test_base.gd"

const BrainPanel := preload("res://Scenes/brain_panel.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ScopeObserver := preload("res://Contract/scope_observer.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Fx := preload("res://tests/fixtures.gd")


func _api(transport) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = false
	return api


func _panel(transport) -> Object:
	var panel := BrainPanel.new()
	panel.api = _api(transport)
	panel.labels = ActionLabels.new()
	panel.labels.load_from([{"actionType": 0, "label": "Comer"}, {"actionType": 1, "label": "Dormir"},
		{"actionType": 2, "label": "Trabalhar"}, {"actionType": 3, "label": "Passear"}, {"actionType": 4, "label": "Conversar"}])
	panel.date_of = func(tick: int) -> String: return "dia %d" % tick
	return panel


func _three() -> Array:
	return [Fx.trace_entry(10, 0), Fx.trace_entry(20, 1), Fx.trace_entry(30, 2, 3, 2, 2)]


func test_timeline_lists_decisions_newest_first_with_action_labels() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", Fx.npc_inspect(_three()))
	var panel := _panel(transport)

	await panel.open(7)

	eq(panel.timeline.item_count, 3, "3 decisões na linha do tempo")
	check("dia 30" in panel.timeline.get_item_text(0) and "Trabalhar" in panel.timeline.get_item_text(0), "a mais recente primeiro: " + panel.timeline.get_item_text(0))
	check("Dormir" in panel.timeline.get_item_text(1) and "dia 20" in panel.timeline.get_item_text(1), "depois a do meio")
	check("Comer" in panel.timeline.get_item_text(2) and "necessidade urgente" in panel.timeline.get_item_text(2), "motivo do wake em português")
	panel.free()


func test_selecting_a_decision_draws_the_fixture_counts_and_highlights_the_winner() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", Fx.npc_inspect(_three()))
	var panel := _panel(transport)
	await panel.open(7)

	eq([panel.pressure_nodes.size(), panel.opportunity_nodes.size(), panel.alternative_nodes.size()], [3, 2, 2], "mais recente: 3/2/2")
	check(panel.winner_node != null and panel.winner_node.has_meta("winner"), "vencedora destacada")
	for node in panel.pressure_nodes + panel.opportunity_nodes + panel.alternative_nodes + [panel.winner_node]:
		check(node.get_parent() != null, "o nó foi de fato posto na árvore do painel")
	for node in panel.alternative_nodes:
		check(node.modulate.a < 0.6, "alternativas esmaecidas")

	panel.select_entry(2)

	eq([panel.pressure_nodes.size(), panel.opportunity_nodes.size(), panel.alternative_nodes.size()], [2, 1, 2], "mais antiga: 2/1/2")
	check(panel.winner_node.has_meta("winner"), "continua destacada")
	panel.free()


func test_empty_trace_shows_message_and_no_flow_nodes() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", Fx.npc_inspect([]))
	var panel := _panel(transport)

	await panel.open(7)

	eq(panel.empty_text, BrainPanel.EMPTY_NO_TRACE)
	check("Aproxime a câmera" in panel.empty_text and "vigiado" in panel.empty_text, "diz o que fazer")
	eq(panel.pressure_nodes.size() + panel.opportunity_nodes.size() + panel.alternative_nodes.size(), 0)
	eq(panel.winner_node, null, "sem fluxo")
	panel.free()


func test_dead_or_archived_npc_has_its_own_message() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", Fx.npc_inspect([], 1))
	transport.on("GET /npcs/8", {"status": 404, "body": null})
	var panel := _panel(transport)

	await panel.open(7)
	eq(panel.empty_text, BrainPanel.EMPTY_GONE, "arquivado")
	await panel.open(8)
	eq(panel.empty_text, BrainPanel.EMPTY_GONE, "inexistente/morto")
	panel.free()


func test_watch_posts_then_deletes_and_a_4xx_warns_without_breaking() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", Fx.npc_inspect(_three()))
	transport.on("POST /npcs/7/watchlist", {"status": 200, "body": null})
	transport.on("DELETE /npcs/7/watchlist", {"status": 200, "body": null})
	var panel := _panel(transport)
	var warnings: Array = []
	panel.warning.connect(func(text: String): warnings.append(text))
	await panel.open(7)

	await panel.toggle_watch()
	eq(transport.count_calls("POST /npcs/7/watchlist"), 1, "Vigiar faz POST")
	eq(panel.watching, true)
	eq(panel.watch_button.text, "Parar de vigiar")
	await panel.toggle_watch()
	eq(transport.count_calls("DELETE /npcs/7/watchlist"), 1, "Parar faz DELETE")
	eq(panel.watching, false)

	transport.on("POST /npcs/7/watchlist", {"status": 400, "body": {"error": "NpcId: NPC ausente ou morto"}})
	await panel.toggle_watch()
	eq(panel.watching, false, "falha não muda o estado")
	eq(warnings.size(), 1, "avisa o usuário")
	eq(panel.timeline.item_count, 3, "o painel segue funcionando")
	panel.free()


func test_closed_panel_makes_no_periodic_requests_and_open_one_refreshes_on_new_hour() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", Fx.npc_inspect(_three()))
	var panel := _panel(transport)
	await panel.open(7)
	var after_open := transport.count_calls("GET /npcs/7")

	await panel.on_world_tick(100)
	await panel.on_world_tick(100)
	eq(transport.count_calls("GET /npcs/7"), after_open + 1, "uma busca por hora nova, não por chamada")
	panel.close()
	await panel.on_world_tick(101)
	await panel.on_world_tick(102)
	eq(transport.count_calls("GET /npcs/7"), after_open + 1, "fechado: nenhuma requisição")
	panel.free()


func test_refresh_redraws_only_when_the_latest_entry_changed() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", Fx.npc_inspect(_three()))
	var panel := _panel(transport)
	await panel.open(7)
	var first_winner = panel.winner_node

	await panel.refresh()
	check(panel.winner_node == first_winner, "mesma última entrada: não redesenha")

	transport.on("GET /npcs/7", Fx.npc_inspect(_three() + [Fx.trace_entry(40, 3)]))
	await panel.refresh()
	eq(panel.timeline.item_count, 4)
	check("dia 40" in panel.timeline.get_item_text(0), "nova decisão no topo")
	check(panel.winner_node != first_winner, "redesenhou")
	panel.free()


func test_scope_observer_publishes_scope_changes_and_heartbeat_and_clears() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /observation/scope", {"status": 200, "body": null})
	var observer := ScopeObserver.new()
	observer.api = _api(transport)

	await observer.set_scope("World")
	await observer.set_scope("World")
	eq(transport.count_calls("POST /observation/scope"), 1, "mesmo escopo não reenvia")
	await observer.set_scope("City", "city-a")
	eq(transport.bodies["POST /observation/scope"].back().scope, {"kind": "City", "cityId": "city-a"}, "corpo da cidade")
	await observer.set_scope("Building", "city-a", "4")
	eq(transport.bodies["POST /observation/scope"].back().scope, {"kind": "Building", "cityId": "city-a", "buildingId": "4"}, "corpo do prédio")
	await observer.heartbeat()
	eq(transport.count_calls("POST /observation/scope"), 4, "mudanças + 1 batimento")
	await observer.clear()
	eq(transport.bodies["POST /observation/scope"].back().scope, null, "limpar manda scope null")
	await observer.heartbeat()
	eq(transport.count_calls("POST /observation/scope"), 5, "limpar envia scope null e para o batimento")
	observer.free()


func test_contract_shape_validates_a_cognition_trace_item() -> void:
	var shape = ApiClient.ShapeValidator.load_shape("npc-inspect")
	var trace_shape = shape["cognitionTrace"]
	check(trace_shape is Array and trace_shape.size() == 1, "o contrato congela a forma de UM item do rastro")

	eq(ApiClient.ShapeValidator.validate([Fx.trace_entry(5, 0)], trace_shape), [], "item válido")
	eq(ApiClient.ShapeValidator.validate([], trace_shape), [], "rastro vazio é válido")

	var broken := Fx.trace_entry(5, 0)
	broken.trace.erase("winner")
	check(not ApiClient.ShapeValidator.validate([broken], trace_shape).is_empty(), "sem 'winner' o contrato reprova")
	var wrong_type := Fx.trace_entry(5, 0)
	wrong_type.trace.topPressures[0].intensity = "alta"
	check(not ApiClient.ShapeValidator.validate([wrong_type], trace_shape).is_empty(), "intensidade não numérica reprova")


func test_contract_does_not_require_optional_family_fields() -> void:
	var shape = ApiClient.ShapeValidator.load_shape("npc-inspect")
	var body := {"spouse": null, "household": null, "currentScope": {"kind": 0, "cityId": null}}
	for key in ["spouse", "household"]:
		eq(ApiClient.ShapeValidator.validate(body[key], shape[key]), [], "NPC sem %s não é reprovado" % key)
	eq(ApiClient.ShapeValidator.validate(body.currentScope.cityId, shape.currentScope.cityId), [], "NPC sem cidade no escopo não é reprovado")


func test_scope_observer_heartbeat_timer_fires_every_ten_seconds() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /observation/scope", {"status": 200, "body": null})
	var observer := ScopeObserver.new()
	observer.api = _api(transport)
	tree.root.add_child(observer)
	await observer.set_scope("World")
	var timer: Timer
	for child in observer.get_children():
		if child is Timer:
			timer = child
	check(timer != null and not timer.is_stopped() and is_equal_approx(timer.wait_time, 10.0), "Timer de 10 s rodando")
	var before := transport.count_calls("POST /observation/scope")
	timer.timeout.emit()
	await tree.process_frame
	eq(transport.count_calls("POST /observation/scope"), before + 1, "o fim do Timer reenvia o escopo")
	observer.free()
