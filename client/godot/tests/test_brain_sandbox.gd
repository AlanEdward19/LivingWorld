extends "res://tests/test_base.gd"

const BrainPanel := preload("res://Scenes/brain_panel.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Fx := preload("res://tests/fixtures.gd")


func _api(transport, validate: bool = false) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	return api


func _result(pressures: int = 2, opportunities: int = 1, alternatives: int = 3) -> Dictionary:
	var trace: Dictionary = Fx.trace_entry(0, 2, pressures, opportunities, alternatives).trace
	trace.erase("winner")
	return {"status": 200, "body": {"action": 1, "trace": trace}}


func _panel(transport, validate: bool = false) -> Object:
	var panel := BrainPanel.new()
	panel.api = _api(transport, validate)
	panel.labels = ActionLabels.new()
	panel.labels.load_from([{"actionType": 0, "label": "Comer"}, {"actionType": 1, "label": "Dormir"}, {"actionType": 2, "label": "Trabalhar"}])
	panel.date_of = func(tick: int) -> String: return "dia %d" % tick
	return panel


func _opened(transport, validate: bool = false) -> Object:
	transport.on("GET /npcs/7", Fx.npc_inspect([Fx.trace_entry(10, 0), Fx.trace_entry(20, 1)]))
	var panel := _panel(transport, validate)
	await panel.open(7)
	return panel


func test_sliders_start_at_the_npcs_real_values() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport)

	eq(panel.need_sliders["hunger"].value, 80.0, "fome da ficha")
	eq(panel.need_sliders["social"].value, 50.0)
	eq(panel.trait_sliders.size(), 10, "os 10 traços")
	eq(panel.trait_sliders["ambition"].value, 50.0)
	panel.free()


func test_simulate_posts_only_what_changed_and_draws_the_result_in_the_same_flow() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport)
	transport.on("POST /npcs/7/decision-sandbox", _result(3, 2, 2))
	panel.set_sandbox_mode(true)
	panel.need_sliders["hunger"].value = 5

	await panel.simulate()

	eq(transport.bodies["POST /npcs/7/decision-sandbox"], [{"needs": {"hunger": 5}}], "só a fome mudou")
	eq([panel.pressure_nodes.size(), panel.opportunity_nodes.size(), panel.alternative_nodes.size()], [3, 2, 2], "mesmo componente de fluxo")
	check(panel.winner_node.has_meta("winner"), "vencedora destacada")
	check(not panel.sandbox_trace.is_empty(), "marca que é simulação")
	check(panel._empty.visible and "Simulação" in panel._empty.text, "a tela avisa que é simulação")
	eq(panel.sandbox_trace.winner, 1, "ação vem do campo action")
	panel.free()


func test_traits_are_sent_and_untouched_sliders_send_an_empty_body() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport)
	transport.on("POST /npcs/7/decision-sandbox", _result())

	await panel.simulate()
	panel.trait_sliders["ambition"].value = 90
	await panel.simulate()

	eq(transport.bodies["POST /npcs/7/decision-sandbox"], [{}, {"personality": {"ambition": 90}}])
	panel.free()


func test_errors_warn_without_breaking_the_panel() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport)
	var warnings: Array = []
	panel.warning.connect(func(text: String): warnings.append(text))

	transport.on("POST /npcs/7/decision-sandbox", {"status": 400, "body": {"error": "needs.hunger: fora de [0,100]"}})
	await panel.simulate()
	transport.on("POST /npcs/7/decision-sandbox", {"status": 500, "body": null})
	await panel.simulate()

	eq(warnings.size(), 2)
	check("fora de [0,100]" in warnings[0], warnings[0])
	check("erro 500" in warnings[1], warnings[1])
	eq(panel.timeline.item_count, 2, "o painel segue funcionando")

	transport.on("POST /npcs/7/decision-sandbox", {"status": 404, "body": null})
	await panel.simulate()
	eq(panel.empty_text, BrainPanel.EMPTY_GONE, "NPC sumiu")
	panel.free()


func test_selecting_a_real_decision_leaves_the_simulation_and_toggle_swaps_columns() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport)
	transport.on("POST /npcs/7/decision-sandbox", _result(3, 2, 2))
	panel.set_sandbox_mode(true)
	eq(panel.timeline.visible, false, "sliders no lugar da linha do tempo")
	await panel.simulate()

	panel.set_sandbox_mode(false)
	eq(panel.timeline.visible, true)
	panel.select_entry(1)

	eq(panel.sandbox_trace, {}, "voltou ao rastro real")
	eq(panel.pressure_nodes.size(), 2, "fluxo real da decisão escolhida")
	panel.free()


func test_a_new_hour_does_not_replace_a_simulation_on_screen() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport)
	transport.on("POST /npcs/7/decision-sandbox", _result(3, 2, 2))
	await panel.simulate()
	var shown = panel.winner_node

	transport.on("GET /npcs/7", Fx.npc_inspect([Fx.trace_entry(10, 0), Fx.trace_entry(20, 1), Fx.trace_entry(30, 2)]))
	await panel.on_world_tick(200)

	eq(panel.timeline.item_count, 3, "a linha do tempo atualiza")
	check(panel.winner_node == shown, "o fluxo simulado continua na tela")
	panel.free()


func test_only_the_sandbox_route_is_written_and_closed_panel_cannot_simulate() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport)
	transport.on("POST /npcs/7/decision-sandbox", _result())
	await panel.simulate()
	var writes: Array = transport.calls.filter(func(c: String) -> bool: return c.begins_with("POST") or c.begins_with("DELETE") or c.begins_with("PUT"))
	eq(writes, ["POST /npcs/7/decision-sandbox"], "simular é a única escrita")

	panel.close()
	await panel.simulate()
	eq(transport.count_calls("POST /npcs/7/decision-sandbox"), 1, "fechado não simula")
	panel.free()


func test_the_response_is_checked_against_the_frozen_shape() -> void:
	var transport := FakeTransport.new()
	var panel = await _opened(transport, true)
	var warnings: Array = []
	panel.warning.connect(func(text: String): warnings.append(text))
	transport.on("POST /npcs/7/decision-sandbox", {"status": 200, "body": {"action": 1}})

	await panel.simulate()

	eq(warnings.size(), 1, "resposta fora do contrato vira aviso")
	eq(panel.sandbox_trace, {}, "e não é desenhada")
	panel.free()
