extends "res://tests/test_base.gd"

const Panels := preload("res://Scenes/panels.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")


func _api(transport) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = false
	return api



func _page(names: Array, total: int) -> Dictionary:
	var items: Array = []
	for i in names.size():
		items.append({"id": {"value": i + 1}, "name": names[i], "cityId": {"value": "c"}, "action": null, "location": {"x": 0, "y": 0}})
	return {"status": 200, "body": {"items": items, "total": total}}


func test_npc_list_paginates_and_searches() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs?", _page(["Ana", "Bia"], 120))
	var panel := Panels.NpcListPanel.new()
	panel.api = _api(transport)

	await panel.load_page()
	eq(panel.rows.size(), 2)
	eq(panel.total, 120)
	check(transport.calls[0].contains("take=50") and transport.calls[0].contains("skip=0"), "página de 50")

	await panel.page(1)
	check(transport.calls.back().contains("skip=50"), "próxima página avança 50")
	await panel.search("ana")
	check(transport.calls.back().contains("q=ana") and transport.calls.back().contains("skip=0"), "busca reinicia na página 0")

	transport.on("GET /npcs?", _page([], 0))
	await panel.search("zzz")
	eq(panel.empty_label.visible, true, "mostra 'nenhum NPC' quando vazia")
	eq(panel.empty_label.text, "nenhum NPC")
	check(PanelPageSize.value() <= 200, "take respeita o teto do servidor")
	panel.free()


func test_select_npc_fetches_and_highlights() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", {"status": 200, "body": {
		"name": "Ana", "ageYears": 31, "health": 90, "hunger": 80, "thirst": 70, "sleep": 60, "social": 50}})
	var panel := Panels.NpcDetailPanel.new()
	panel.api = _api(transport)
	var highlighted: Array = []
	panel.highlight.connect(func(id): highlighted.append(id))

	await panel.show_npc(7)

	eq(transport.calls, ["GET /npcs/7"], "busca o detalhe do NPC")
	eq(highlighted, [7], "pede para destacá-lo na cena")
	check("Ana" in panel.text and "31" in panel.text, "mostra os dados recebidos")
	eq(panel.bars["Fome"].value, 80.0, "a barra de fome mostra o valor do servidor")
	eq(panel.bars["Saúde"].value, 90.0)
	eq(panel.bars["Sede"].value, 70.0)
	eq(panel.bars["Sono"].value, 60.0)
	eq(panel.bars["Social"].value, 50.0)
	panel.free()


func test_city_panel_shows_snapshot_fields() -> void:
	var panel := Panels.CityPanel.new()

	panel.show_city({"name": "Alfa", "indicators": {
		"population": 120, "wealth": 5000, "health": 88, "inequality": 0.31, "economy": 4000, "housing": 90}})

	check("Alfa" in panel.text and "120" in panel.text and "5000" in panel.text and "0.31" in panel.text, "nome, população e indicadores")
	eq(panel.visible, true)
	panel.free()


func test_city_panel_shows_dash_for_unmeasured_indicators_and_health_as_mean() -> void:
	var panel := Panels.CityPanel.new()

	panel.show_city({"name": "Alfa", "indicators": {
		"population": 5, "wealth": 10, "health": 64.2, "inequality": 0.0, "economy": 10, "housing": 4,
		"security": null, "education": null, "infrastructure": null}})

	check("Saúde (média) 64.2" in panel.text, "saúde é a média, com uma casa decimal")
	check("Segurança —" in panel.text and "Educação —" in panel.text and "Infraestrutura —" in panel.text, "nulo vira traço")
	check(not ("Segurança 0" in panel.text), "nulo nunca vira zero")
	panel.show_city({"name": "Beta", "indicators": {
		"population": 5, "wealth": 10, "health": 50, "inequality": 0.0, "economy": 10, "housing": 4, "security": 7}})
	check("Segurança 7" in panel.text and "Educação —" in panel.text, "valor medido aparece; ausente é traço")
	panel.free()


func test_failed_list_shows_error_not_empty() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs?", {"status": 500, "body": null})
	var panel := Panels.NpcListPanel.new()
	panel.api = _api(transport)

	await panel.load_page()

	eq(panel.empty_label.visible, true)
	eq(panel.empty_label.text, "erro ao carregar a lista", "falha não se disfarça de lista vazia")
	panel.free()


func test_list_filters_by_city_scope() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /npcs?", _page(["Ana"], 1))
	var panel := Panels.NpcListPanel.new()
	panel.api = _api(transport)
	panel.city_filter = "city-a"

	await panel.load_page()

	check(transport.calls[0].contains("cityId=city-a"), "a lista usa o escopo atual: " + transport.calls[0])
	panel.free()


class PanelPageSize:
	static func value() -> int:
		return Panels.PAGE_SIZE
