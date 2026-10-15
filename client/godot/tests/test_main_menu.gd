extends "res://tests/test_base.gd"

const MainMenu := preload("res://Scenes/main_menu.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const JsonRaw := preload("res://Contract/json_raw.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")

const DEFINITION := '{"periodId":"medieval","periodDefinition":{"Width":2,"Height":2,"Seed": 1,"Nested":{"Seed":9},"Name":"a \\"Seed\\": 5"},"version":1}'


func _transport(online: bool = true) -> RefCounted:
	var transport := FakeTransport.new()
	if online:
		transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 3}})
	transport.on("GET /periods/medieval/catalog", {"status": 200, "body": {"professionIds": [1], "professionNames": {"1": "Fazendeiro"}}})
	transport.on("GET /periods/medieval", {"status": 200, "body": {}, "text": DEFINITION})
	transport.on("GET /periods", {"status": 200, "body": [{"periodId": "medieval", "width": 2, "height": 2}]})
	transport.on("POST /worlds/preview", {"status": 200, "body": {"width": 2, "height": 1,
		"cells": [{"x": 0, "y": 0, "terrain": 0, "hasWater": false, "altitude": 0}, {"x": 1, "y": 0, "terrain": 1, "hasWater": true, "altitude": 0}],
		"settlements": [{"name": "vila", "x": 0, "y": 0}]}})
	transport.on("POST /worlds/create", {"status": 200, "body": {"npcCount": 3}})
	transport.on("GET /npcs?", {"status": 200, "body": {"total": 0, "items": []}})
	transport.on("GET /authoring/extraordinary/catalog", {"status": 200, "body": []})
	return transport


func _menu(transport: RefCounted) -> Control:
	var menu := MainMenu.new()
	menu.api = ApiClient.new(transport)
	tree.root.add_child(menu)
	return menu


func test_json_raw_replaces_only_top_level_member() -> void:
	var scenario := JsonRaw.member(DEFINITION, "periodDefinition")
	check(scenario.begins_with('{"Width":2'), "extrai o objeto inteiro")
	var changed := JsonRaw.replace_member(scenario, "Seed", "42")
	check('"Seed":42' in changed.replace(" ", ""), "seed do topo trocada")
	check('"Nested":{"Seed":9}' in changed, "seed aninhada intacta")
	check('a \\"Seed\\": 5' in changed, "texto dentro de string intacto")
	check(not ("42.0" in changed), "inteiro continua inteiro")
	eq(JsonRaw.replace_member(scenario, "Ausente", "1"), scenario, "membro ausente não muda nada")


func test_title_enables_continue_only_with_server() -> void:
	var online := _menu(_transport(true))
	await online.refresh_server()
	eq(online.continue_button.disabled, false, "online habilita Continuar")
	check("ano 3" in online.status_label.text, "mostra o estado do servidor")
	online.free()
	var offline := _menu(_transport(false))
	await offline.refresh_server()
	eq(offline.continue_button.disabled, true, "offline desabilita Continuar")
	check("play.sh" in offline.status_label.text, "diz como subir o servidor")
	offline.free()


func test_continue_emits_world_ready() -> void:
	var menu := _menu(_transport())
	await menu.refresh_server()
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	menu.continue_button.pressed.emit()
	eq(fired[0], true, "Continuar entra no mundo")
	menu.free()


func test_new_world_lists_periods_previews_and_creates() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	await menu.show_new_world()
	eq(menu.period_ids, ["medieval"] as Array[String], "lista os períodos do servidor")
	eq(menu.selected_period, "medieval", "seleciona o primeiro")
	check("Fazendeiro" in menu.period_info.text, "mostra profissões do catálogo")
	check(menu.preview_rect.texture != null, "prévia do mapa desenhada")
	eq(menu.create_button.disabled, false, "nome e período habilitam Criar")
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	menu.api.validate_shapes = false
	await menu._on_create()
	check("POST /worlds/create" in transport.calls, "chamou /worlds/create")
	check(menu.touchup != null, "mundo criado abre os retoques")
	menu.touchup.finished.emit()
	eq(fired[0], true, "terminar os retoques entra no jogo")
	menu.free()


func test_create_failure_shows_message_and_stays() -> void:
	var transport := _transport()
	transport.on("POST /worlds/create", {"status": 400, "body": "cenário inválido"})
	var menu := _menu(transport)
	await menu.show_new_world()
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	await menu._on_create()
	eq(fired[0], false, "falha não entra no jogo")
	check("cenário inválido" in menu.message_label.text, "mostra o erro do servidor")
	menu.free()


func test_preview_image_paints_water_and_settlements() -> void:
	var image: Image = MainMenu.preview_image({"width": 2, "height": 1,
		"cells": [{"x": 0, "y": 0, "terrain": 0, "hasWater": false, "altitude": 0}, {"x": 1, "y": 0, "terrain": 1, "hasWater": true, "altitude": 0}],
		"settlements": [{"x": 0, "y": 0}]})
	eq(image.get_pixel(0, 0).to_html(false), MainMenu.SETTLEMENT_COLOR.to_html(false), "assentamento amarelo")
	eq(image.get_pixel(1, 0).to_html(false), MainMenu.WATER_COLOR.to_html(false), "água azul")


func test_continuing_the_current_world_forgets_the_period_looked_at_in_new_world() -> void:
	var menu := _menu(_transport())
	var emitted: Array = []
	menu.world_ready.connect(func(): emitted.append(true))
	menu.selected_period = "medieval" # o menu seleciona o 1º período ao abrir "Novo mundo"

	menu._on_continue()

	eq(menu.selected_period, "", "mundo continuado não carrega o período olhado na tela de novo mundo")
	eq(emitted, [true])
	menu.queue_free()
