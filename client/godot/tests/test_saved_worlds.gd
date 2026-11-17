extends "res://tests/test_base.gd"

const Menu := preload("res://Scenes/saved_worlds_menu.gd")
const MainMenu := preload("res://Scenes/main_menu.gd")
const Kit := preload("res://Scenes/menu/menu_kit.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")


## Item de `GET /worlds` com os campos do AD-175 (o fake segue o contrato gravado: a lista é validada pela forma).
static func item(id: String, world_name: String, current: bool, year := 0, people := 20, origin := "player", period_name := "") -> Dictionary:
	return {"worldId": id, "name": world_name, "seed": 7, "currentDate": 30, "population": people, "createdAt": 0,
		"lastSavedAtUtc": "2026-10-15T20:00:00Z", "lastPlayedAtUtc": "2026-10-15T20:00:00Z", "periodId": "", "periodName": period_name,
		"year": year, "isCurrent": current, "origin": origin}


func _transport() -> RefCounted:
	var transport := FakeTransport.new()
	transport.on("POST /worlds/a/rename", {"status": 200, "body": item("a", "Novo", false)})
	transport.on("GET /worlds", {"status": 200, "body": [item("a", "Aldeia", false)]})
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
		item("b", "Brejo", false, 2, 1, "automatic"),
		item("a", "Aldeia", true, 0, 20, "player", "Vila pequena")]})
	var menu := _menu(transport)
	await menu.refresh_worlds()
	eq(menu.rows.get_child_count(), 2, "dois mundos")
	var first := Kit.all_text(menu.rows.get_child(0))
	var second := Kit.all_text(menu.rows.get_child(1))
	check("Aberto agora" in second and "Vila pequena" in second, "o aberto é o que o servidor marca, com o lugar: " + second)
	check(not ("Aberto agora" in first), "o mais recente da lista não é tomado como aberto: " + first)
	check("ano 2" in first and "1 pessoa" in first and "mundo inicial" in first, "ano do servidor, singular e origem: " + first)
	eq(Kit.find_button(menu.rows.get_child(1), "Apagar").disabled, true, "o mundo aberto não se apaga")
	eq(Kit.find_button(menu.rows.get_child(0), "Apagar").disabled, false, "os outros sim")
	check(transport.count_calls("GET /world/") == 0, "nada é adivinhado pelo mundo aberto ou pelo calendário")
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


func test_rename_sends_the_name_and_shows_the_new_list() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	await menu.refresh_worlds()
	Kit.find_button(menu.rows.get_child(0), "Renomear").pressed.emit()
	check(menu.rename_dialog.visible, "Renomear abre o diálogo")
	eq(menu.rename_edit.text, "Aldeia", "já vem com o nome atual")
	menu.rename_edit.text = "  Vale Novo "
	transport.on("GET /worlds", {"status": 200, "body": [item("a", "Vale Novo", false)]})
	await menu._rename_confirmed()
	eq(transport.bodies["POST /worlds/a/rename"][0], {"name": "Vale Novo"}, "manda o nome sem espaços nas pontas")
	eq(menu.rename_dialog.visible, false, "fecha ao dar certo")
	check("Vale Novo" in Kit.all_text(menu.rows) and "Vale Novo" in menu.message.text, "lista e mensagem com o nome novo")
	menu.free()


func test_rename_error_from_the_server_stays_in_the_dialog() -> void:
	var transport := _transport()
	transport.on("POST /worlds/a/rename", {"status": 409, "body": {"error": "Já existe um mundo chamado “Brejo”."}})
	var menu := _menu(transport)
	await menu.refresh_worlds()
	menu.request_rename("a", "Aldeia")
	menu.rename_edit.text = "Brejo"
	await menu._rename_confirmed()
	check(menu.rename_dialog.visible, "o diálogo fica aberto")
	eq(menu.rename_error.text, "Já existe um mundo chamado “Brejo”.", "mostra a frase do servidor")
	menu.free()


func test_loading_asks_for_a_running_world_only_when_allowed() -> void:
	var transport := _transport()
	transport.on("POST /worlds/a/load?startRunning=true", {"status": 200, "body": null})
	var menu := _menu(transport)
	await menu.load_world("a")
	check("POST /worlds/a/load" in transport.calls, "sem permissão, abre como antes (pausado)")
	menu.start_running = true
	await menu.load_world("a")
	check("POST /worlds/a/load?startRunning=true" in transport.calls, "com o token da sessão, pede rodando")
	menu.free()
