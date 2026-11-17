extends "res://tests/test_base.gd"

const Menu := preload("res://Scenes/saved_worlds_menu.gd")
const MainMenu := preload("res://Scenes/main_menu.gd")
const Kit := preload("res://Scenes/menu/menu_kit.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")


func _transport() -> RefCounted:
	var transport := FakeTransport.new()
	transport.on("GET /worlds", {"status": 200, "body": [{"worldId": "a", "name": "Aldeia", "seed": 7, "currentDate": 30, "population": 20, "createdAt": 0, "lastSavedAtUtc": "2026-10-15T20:00:00Z"}]})
	transport.on("POST /worlds/a/load", {"status": 200, "body": null})
	transport.on("DELETE /worlds/a", {"status": 204, "body": null})
	return transport


func _menu(transport: RefCounted) -> Control:
	var menu := Menu.new()
	menu.api = ApiClient.new(transport)
	tree.root.add_child(menu)
	return menu


func test_saved_worlds_lists_loads_and_confirms_deletion() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	await menu.refresh_worlds()
	eq(menu.rows.get_child_count(), 1, "lista o mundo do servidor")
	var card := Kit.all_text(menu.rows.get_child(0))
	check("Aldeia" in card and "20 pessoas" in card and "jogado" in card, "nome e metadados visíveis: " + card)
	var fired := [false]
	menu.loaded.connect(func(): fired[0] = true)
	await menu.load_world("a")
	eq(fired[0], true, "carga bem-sucedida entra no mundo")
	menu.request_delete("a", "Aldeia")
	check(menu.confirmation.visible, "exige confirmação")
	check("Aldeia" in menu.confirmation.dialog_text and "Aldeia" in menu.confirmation.ok_button_text, "a confirmação diz o nome do mundo")
	check(menu.confirmation.get_cancel_button().has_focus(), "o foco começa em Manter, não em Apagar")
	check(not ("DELETE /worlds/a" in transport.calls), "não apaga antes da confirmação")
	transport.on("GET /worlds", {"status": 200, "body": []})
	menu.confirmation.confirmed.emit()
	await tree.process_frame
	check("DELETE /worlds/a" in transport.calls, "confirmação apaga")
	eq(menu.rows.get_child_count(), 0, "remove da lista")
	check("Nenhum" in menu.message.text, "estado vazio")
	menu.free()


func test_saved_worlds_errors_keep_the_menu_and_current_world() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	transport.on("GET /worlds", {"status": 0, "body": null})
	await menu.refresh_worlds()
	check("lista" in menu.message.text, "erro de rede visível")
	transport.on("POST /worlds/a/load", {"status": 404, "body": null})
	var fired := [false]
	menu.loaded.connect(func(): fired[0] = true)
	await menu.load_world("a")
	eq(fired[0], false, "falha não entra no jogo")
	transport.on("DELETE /worlds/a", {"status": 409, "body": null})
	menu.pending_delete = "a"
	await menu._delete_confirmed()
	check("aberto agora" in menu.message.text, "explica proteção do mundo atual")
	menu.free()


func test_saved_worlds_is_reachable_from_main_menu() -> void:
	var menu := MainMenu.new()
	menu.api = ApiClient.new(_transport())
	menu.settings = GameSettings.new()
	menu.settings.path = "user://test-saved-worlds-settings.cfg"
	tree.root.add_child(menu)
	eq(menu.title.my_worlds_button.text, "Meus mundos", "botão de mundos existe no título")
	menu.title.my_worlds_button.pressed.emit()
	await tree.process_frame
	check(menu.saved_worlds_screen.visible, "tela de mundos aberta")
	eq(menu.title.visible, false, "título oculto")
	menu.saved_worlds_screen.back.emit()
	eq(menu.title.visible, true, "voltar restaura título")
	menu.free()


func test_current_world_cannot_be_deleted_and_others_show_their_year() -> void:
	var transport := _transport()
	transport.on("GET /worlds", {"status": 200, "body": [
		{"worldId": "a", "name": "Aldeia", "seed": 7, "currentDate": 30, "population": 20, "createdAt": 0, "lastSavedAtUtc": "2026-10-15T20:00:00Z"},
		{"worldId": "b", "name": "Brejo", "seed": 8, "currentDate": 8640 * 2, "population": 1, "createdAt": 0, "lastSavedAtUtc": "2026-10-10T20:00:00Z"}]})
	transport.on("GET /world/info", {"status": 200, "body": {"name": "Aldeia", "periodId": "vila-pequena", "technologyAxes": []}})
	transport.on("GET /world/calendar", {"status": 200, "body": {"hoursPerDay": 24, "daysPerMonth": 30, "monthsPerYear": 12, "hoursPerMonth": 720, "hoursPerYear": 8640}})
	var menu := _menu(transport)
	await menu.refresh_worlds()
	eq(menu.rows.get_child_count(), 2, "dois mundos")
	var first := Kit.all_text(menu.rows.get_child(0))
	var second := Kit.all_text(menu.rows.get_child(1))
	check("Aberto agora" in first and "Vila Pequena" in first, "o primeiro é o aberto, com a época: " + first)
	check("ano 2" in second and "1 pessoa" in second, "ano pelo calendário e singular: " + second)
	eq(Kit.find_button(menu.rows.get_child(0), "Apagar").disabled, true, "o mundo aberto não se apaga")
	eq(Kit.find_button(menu.rows.get_child(1), "Apagar").disabled, false, "os outros sim")
	eq(Menu.when_played("2026-10-15T20:00:00Z", Time.get_unix_time_from_datetime_string("2026-10-18T10:00:00")), "jogado há 2 dias", "quando foi jogado")
	menu.free()


class PendingTransport extends RefCounted:
	signal delivered(response: Dictionary)
	var calls: Array[String] = []
	func request(method: String, path: String, _body: Variant = null, _headers: Dictionary = {}) -> Dictionary:
		calls.append(method + " " + path)
		return await delivered


func test_saved_worlds_exposes_loading_and_ignores_duplicate_actions_until_response() -> void:
	var transport := PendingTransport.new()
	var menu := _menu(transport)
	menu.refresh_worlds()
	check(menu.busy, "requisição pendente bloqueia comandos duplicados")
	check("Carregando" in menu.message.text, "estado de carregamento visível")
	menu.refresh_worlds()
	menu.load_world("a")
	eq(transport.calls.size(), 1, "só uma requisição enquanto ocupado")
	transport.delivered.emit({"status": 200, "body": []})
	await tree.process_frame
	eq(menu.busy, false, "resposta libera comandos")
	check("Nenhum" in menu.message.text, "resposta vazia explicada")
	menu.free()
