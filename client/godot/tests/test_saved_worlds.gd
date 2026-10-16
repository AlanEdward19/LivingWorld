extends "res://tests/test_base.gd"

const Menu := preload("res://Scenes/saved_worlds_menu.gd")
const MainMenu := preload("res://Scenes/main_menu.gd")
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
	check("Aldeia" in menu.rows.get_child(0).get_child(0).text, "nome e metadados visíveis")
	var fired := [false]
	menu.loaded.connect(func(): fired[0] = true)
	await menu.load_world("a")
	eq(fired[0], true, "carga bem-sucedida entra no mundo")
	menu.request_delete("a", "Aldeia")
	check(menu.confirmation.visible, "exige confirmação")
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
	check("listar" in menu.message.text, "erro de rede visível")
	transport.on("POST /worlds/a/load", {"status": 404, "body": null})
	var fired := [false]
	menu.loaded.connect(func(): fired[0] = true)
	await menu.load_world("a")
	eq(fired[0], false, "falha não entra no jogo")
	transport.on("DELETE /worlds/a", {"status": 409, "body": null})
	menu.pending_delete = "a"
	await menu._delete_confirmed()
	check("atual" in menu.message.text, "explica proteção do mundo atual")
	menu.free()


func test_saved_worlds_is_reachable_from_main_menu() -> void:
	var menu := MainMenu.new()
	menu.api = ApiClient.new(_transport())
	tree.root.add_child(menu)
	var column: VBoxContainer = menu.title_screen.get_child(0).get_child(0)
	var buttons := column.get_children().filter(func(child): return child is Button and child.text == "Carregar mundo")
	eq(buttons.size(), 1, "botão de carga existe no menu")
	if buttons.is_empty():
		menu.free()
		return
	buttons[0].pressed.emit()
	await tree.process_frame
	check(menu.saved_worlds_screen.visible, "tela de carga aberta")
	eq(menu.title_screen.visible, false, "título oculto")
	menu.saved_worlds_screen.back.emit()
	eq(menu.title_screen.visible, true, "voltar restaura título")
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
