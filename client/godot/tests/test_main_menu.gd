extends "res://tests/test_base.gd"
## Menu de videogame (AD-174): título, Continuar só com mundo, Novo mundo em passos até entrar no mundo, teclado.

const MainMenu := preload("res://Scenes/main_menu.gd")
const Pages := preload("res://Scenes/menu/wizard_pages.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const Kit := preload("res://Scenes/menu/menu_kit.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const JsonRaw := preload("res://Contract/json_raw.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")

const DEFINITION := '{"periodId":"medieval","periodDefinition":{"Width":2,"Height":2,"Seed": 1,"InitialPopulation":40,"Nested":{"Seed":9},"Name":"a \\"Seed\\": 5"},"version":1}'
const SETTINGS_PATH := "user://test-menu-settings.cfg"
const CARD := {"name": "Vila do vale", "era": "Aldeias agrícolas", "description": "Poucas famílias num vale.", "startingTechnology": "Ferramentas de mão",
	"initialPopulation": 40, "settlementCount": 1, "professionCount": 1, "professions": ["Fazendeiro"], "inventionEnabled": true}


## Mundo como `GET /worlds` devolve desde o AD-175 (aberto agora, ano, período e origem vêm do servidor).
static func world_item(id: String, world_name: String, current := true, origin := "player", year := 3) -> Dictionary:
	return {"worldId": id, "name": world_name, "seed": 7, "currentDate": 30, "population": 20, "createdAt": 0,
		"lastSavedAtUtc": "2026-10-15T20:00:00Z", "lastPlayedAtUtc": "2026-10-15T20:00:00Z", "periodId": "medieval",
		"periodName": "Vila do vale", "year": year, "isCurrent": current, "origin": origin}


## Andamento da criação em segundo plano: uma etapa no meio, depois pronto com o resultado.
static func job_route(failed := false, status := 200, error := "") -> Callable:
	var calls := [0]
	return func(_path: String) -> Dictionary:
		calls[0] += 1
		if calls[0] == 1:
			return {"status": 200, "body": {"jobId": "j", "stage": "families", "stageLabel": "Chamando as famílias", "stageIndex": 2, "stageCount": 6,
				"done": false, "failed": false, "status": 202, "error": "", "result": null, "running": false}}
		return {"status": 200, "body": {"jobId": "j", "stage": "ready", "stageLabel": "Pronto!", "stageIndex": 5, "stageCount": 6, "done": true,
			"failed": failed, "status": status, "error": error, "result": null if failed else {"npcCount": 3, "name": "Vale"}, "running": false}}


func _transport(online: bool = true, worlds: Array = [world_item("a", "Aldeia")], period := "medieval") -> RefCounted:
	var transport := FakeTransport.new()
	transport.on("POST /worlds/create/async", {"status": 202, "body": {"jobId": "j", "statusUrl": "/worlds/create/jobs/j", "stages": []}})
	transport.on("GET /worlds/create/jobs/", job_route())
	if online:
		transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 5 * 3600000, "tickDeltaMs": 3600000, "year": 3}})
		transport.on("GET /worlds", {"status": 200, "body": worlds})
		transport.on("GET /world/info", {"status": 200, "body": {"name": "Aldeia", "periodId": period, "technologyAxes": []}})
	transport.on("GET /periods/medieval/catalog", {"status": 200, "body": {"professionIds": [1], "professionNames": {"1": "Fazendeiro"}}})
	transport.on("GET /periods/medieval", {"status": 200, "body": {}, "text": DEFINITION})
	transport.on("GET /periods", {"status": 200, "body": [{"periodId": "medieval", "source": "Vila medieval", "width": 2, "height": 2, "card": CARD}]})
	transport.on("POST /worlds/preview", {"status": 200, "body": {"width": 2, "height": 1,
		"cells": [{"x": 0, "y": 0, "terrain": 0, "hasWater": false, "altitude": 0}, {"x": 1, "y": 0, "terrain": 1, "hasWater": true, "altitude": 0}],
		"settlements": [{"name": "vila", "x": 0, "y": 0}]}})
	transport.on("POST /worlds/create", {"status": 200, "body": {"npcCount": 3}})
	transport.on("GET /npcs?", {"status": 200, "body": {"total": 0, "items": []}})
	transport.on("GET /authoring/extraordinary/catalog", {"status": 200, "body": []})
	return transport


func _menu(transport: RefCounted, has_played := false) -> Control:
	DirAccess.remove_absolute(ProjectSettings.globalize_path(SETTINGS_PATH))
	var menu := MainMenu.new()
	menu.api = ApiClient.new(transport)
	menu.api.validate_shapes = false
	menu.settings = GameSettings.load_from(SETTINGS_PATH)
	menu.settings.has_played = has_played
	tree.root.add_child(menu)
	return menu


func _done(menu: Control) -> void:
	menu.free()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(SETTINGS_PATH))


func _press(action: String) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = true
	tree.root.push_input(event)
	var release := InputEventAction.new()
	release.action = action
	tree.root.push_input(release)
	await tree.process_frame


func test_json_raw_replaces_only_top_level_member() -> void:
	var scenario := JsonRaw.member(DEFINITION, "periodDefinition")
	check(scenario.begins_with('{"Width":2'), "extrai o objeto inteiro")
	var changed := JsonRaw.replace_member(scenario, "Seed", "42")
	check('"Seed":42' in changed.replace(" ", ""), "seed do topo trocada")
	check('"Nested":{"Seed":9}' in changed, "seed aninhada intacta")
	check('a \\"Seed\\": 5' in changed, "texto dentro de string intacto")
	check(not ("42.0" in changed), "inteiro continua inteiro")
	eq(JsonRaw.replace_member(scenario, "Ausente", "1"), scenario, "membro ausente não muda nada")


func test_title_shows_continue_with_world_and_offline_screen_without_server() -> void:
	var online := _menu(_transport(true))
	await online.refresh_server()
	eq(online.title.continue_button.visible, true, "online com mundo mostra Continuar")
	check("Aldeia" in online.title.continue_button.text and "ano 3" in online.title.continue_button.text and "20 pessoas" in online.title.continue_button.text,
		"Continuar diz nome, ano e população: " + online.title.continue_button.text)
	eq(online.current, online.title, "fica no título")
	_done(online)
	var offline := _menu(_transport(false))
	await offline.refresh_server()
	eq(offline.title.continue_button.visible, false, "offline esconde Continuar")
	eq(offline.current, offline.offline, "tela amigável em vez de 'Conectando…' eterno")
	var text := Kit.all_text(offline.offline)
	check("ainda não está ligado" in text, "diz que o mundo está desligado")
	check("Jogar.command" in text and "play.sh" in text, "diz como ligar: " + text)
	eq(offline.offline.start_button.visible, false, "sem o binário e em teste, não oferece ligar daqui")
	_done(offline)


func test_continue_is_hidden_without_a_world_to_return_to() -> void:
	var empty := _menu(_transport(true, []))
	await empty.refresh_server()
	eq(empty.title.continue_button.visible, false, "nenhum mundo salvo: sem Continuar")
	check(empty.title.new_world_button.has_focus(), "o foco começa em Novo mundo")
	_done(empty)
	var bootstrap := _menu(_transport(true, [world_item("b", "Mundo", true, "automatic", 0)], ""))
	await bootstrap.refresh_server()
	eq(bootstrap.title.continue_button.visible, false, "o mundo que a API cria sozinha não conta antes de jogar")
	_done(bootstrap)
	var played := _menu(_transport(true, [world_item("b", "Mundo", true, "automatic", 0)], ""), true)
	await played.refresh_server()
	eq(played.title.continue_button.visible, true, "quem já jogou continua o mesmo mundo")
	check(played.title.continue_button.has_focus(), "o foco começa em Continuar")
	_done(played)


func test_continue_emits_world_ready_and_remembers_playing() -> void:
	var menu := _menu(_transport())
	await menu.refresh_server()
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	menu.title.continue_button.pressed.emit()
	eq(fired[0], true, "Continuar entra no mundo")
	eq(GameSettings.load_from(SETTINGS_PATH).has_played, true, "fica lembrado que já jogou")
	_done(menu)


func test_full_new_world_journey_creates_and_enters_the_world() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	await menu.refresh_server()
	menu.title.new_world_button.pressed.emit()
	await tree.process_frame
	var wizard = menu.wizard
	while wizard.selected_period == "" or wizard.draft == "":
		await tree.process_frame
	eq(menu.current, wizard, "Novo mundo abre o assistente")
	eq(wizard.period_ids, ["medieval"] as Array[String], "lista os períodos do servidor")
	eq(wizard.selected_period, "medieval", "seleciona o primeiro")
	check("Fazendeiro" in wizard.period_info.text, "mostra ofícios do catálogo")
	eq(wizard.period_cards.get_child_count(), 1, "um cartão por período")
	eq(wizard.step, 0, "começa na época")
	await wizard.next()
	eq(wizard.step, 1, "nome e semente")
	check(wizard.world_name != "" and wizard.seed_edit.text.is_valid_int(), "nome e semente já sorteados")
	await wizard.next()
	eq(wizard.step, 2, "tamanho")
	wizard.choose_size(1)
	check("150 pessoas" in wizard.population_label.text and "Vila" in wizard.population_label.text, "rótulo humano e número: " + wizard.population_label.text)
	await wizard.next()
	eq(wizard.step, 3, "mapa")
	check(wizard.preview_rect.texture != null, "prévia do mapa desenhada")
	check("POST /worlds/preview" in transport.calls, "a prévia veio de /worlds/preview")
	eq(menu.backdrop.map_size, Vector2i(2, 1), "o fundo 3D mostra o mapa real")
	await wizard.next()
	eq(wizard.step, 4, "resumo")
	check(wizard.world_name in wizard.summary_label.text and "150" in wizard.summary_label.text, "resumo com as escolhas")
	eq(wizard.next_button.text.begins_with("Criar mundo"), true, "último botão cria")
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	var stages: Array[String] = []
	wizard.creation_progress.connect(func(_i: int, _n: int, text: String): stages.append(text))
	await wizard.next()
	check("POST /worlds/create/async" in transport.calls, "chamou a criação em segundo plano")
	check("GET /worlds/create/jobs/j" in transport.calls, "acompanhou o andamento no endereço que o servidor deu")
	eq(stages, ["Chamando as famílias", "Pronto!"] as Array[String], "o carregamento mostrou as etapas do servidor")
	eq(fired[0], true, "criado, entra no mundo")
	eq(menu.current, menu.loading, "tela de carregamento enquanto entra")
	check(not ("POST /simulation/resume" in transport.calls), "sem token, não mexe no tempo")
	check(not ("POST /worlds/create/async?startRunning=true" in transport.calls), "sem token, não pede o mundo rodando")
	_done(menu)


func test_touchup_is_an_optional_detour_after_creating() -> void:
	var menu := _menu(_transport())
	await menu.show_new_world()
	menu.wizard.touchup_after = true
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	await menu.wizard.create()
	check(menu.touchup != null, "com a opção marcada, abre os retoques")
	eq(fired[0], false, "ainda não entrou")
	menu.touchup.finished.emit()
	eq(fired[0], true, "terminar os retoques entra no jogo")
	_done(menu)


func test_create_failure_shows_human_message_and_stays() -> void:
	var transport := _transport()
	transport.on("POST /worlds/create/async", {"status": 404, "body": null}) # API de antes do AD-175: criação de uma vez
	transport.on("POST /worlds/create", {"status": 400, "body": "cenário inválido"})
	var menu := _menu(transport)
	await menu.show_new_world()
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	await menu.wizard.go_to(menu.wizard.SUMMARY_STEP)
	await menu.wizard.next()
	eq(fired[0], false, "falha não entra no jogo")
	check("cenário inválido" in menu.wizard.message_label.text, "mostra o motivo: " + menu.wizard.message_label.text)
	eq(menu.current, menu.wizard, "volta ao assistente")
	transport.on("POST /worlds/create", {"status": 409, "body": "Já existe um mundo salvo com esta seed."})
	await menu.wizard.next()
	check("semente" in menu.wizard.message_label.text and not ("seed" in menu.wizard.message_label.text), "conflito em frase humana")
	_done(menu)


func test_name_and_seed_errors_appear_next_to_the_field() -> void:
	var menu := _menu(_transport())
	await menu.show_new_world()
	var wizard = menu.wizard
	await wizard.go_to(1)
	wizard.name_edit.text = ""
	wizard.world_name = ""
	await wizard.next()
	eq(wizard.step, 1, "sem nome não avança")
	check("nome" in wizard.name_error.text, "erro ao lado do nome: " + wizard.name_error.text)
	wizard.world_name = "Vale"
	wizard.seed_edit.text = "abc"
	await wizard.next()
	eq(wizard.step, 1, "semente inválida não avança")
	check("número" in wizard.seed_error.text, "erro ao lado da semente: " + wizard.seed_error.text)
	wizard.seed_edit.text = "42"
	await wizard.next()
	eq(wizard.step, 2, "corrigido, avança")
	eq(wizard.seed_value, 42, "semente aplicada")
	_done(menu)


func test_surprise_me_fills_everything_and_goes_to_summary() -> void:
	var menu := _menu(_transport())
	await menu.show_new_world()
	var wizard = menu.wizard
	wizard.world_name = ""
	await wizard.surprise()
	eq(wizard.step, wizard.SUMMARY_STEP, "vai ao resumo")
	check(wizard.world_name != "" and wizard.selected_period == "medieval" and wizard.size_choice >= 0, "tudo preenchido")
	check(wizard.world_name in wizard.summary_label.text, "resumo mostra o nome sorteado")
	_done(menu)


func test_keyboard_moves_between_buttons_and_esc_goes_back() -> void:
	var menu := _menu(_transport(true, []))
	await menu.refresh_server()
	check(menu.title.new_world_button.has_focus(), "foco inicial em Novo mundo")
	await _press("ui_down")
	check(menu.title.my_worlds_button.has_focus(), "seta para baixo vai a Meus mundos (foco em %s)" % menu.get_viewport().gui_get_focus_owner())
	await _press("ui_up")
	check(menu.title.new_world_button.has_focus(), "seta para cima volta")
	menu.show_new_world()
	while menu.wizard.draft == "":
		await tree.process_frame
	await menu.wizard.go_to(1)
	await _press("ui_cancel")
	eq(menu.wizard.step, 0, "Esc volta um passo")
	await _press("ui_cancel")
	eq(menu.current, menu.title, "Esc no primeiro passo volta ao título")
	_done(menu)


func test_preview_image_paints_water_and_settlements() -> void:
	var image: Image = Pages.preview_image({"width": 2, "height": 1,
		"cells": [{"x": 0, "y": 0, "terrain": 0, "hasWater": false, "altitude": 0}, {"x": 1, "y": 0, "terrain": 1, "hasWater": true, "altitude": 0}],
		"settlements": [{"x": 0, "y": 0}]})
	eq(image.get_pixel(0, 0).to_html(false), Pages.SETTLEMENT_COLOR.to_html(false), "assentamento amarelo")
	eq(image.get_pixel(1, 0).to_html(false), Pages.WATER_COLOR.to_html(false), "água azul")


func test_creating_a_world_sends_the_chosen_period_id() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	menu.wizard.api = menu.api
	menu.wizard.selected_period = "medieval"
	menu.wizard.draft = DEFINITION
	menu.wizard.world_name = "Vale"
	await menu.wizard.create()
	var bodies: Array = transport.bodies.get("POST /worlds/create/async", [])
	eq(bodies.size(), 1, "uma criação")
	eq(bodies[0].get("periodId"), "medieval", "o período escolhido vai junto, para o servidor guardar")
	eq(bodies[0].get("name"), "Vale", "com o nome escolhido")
	_done(menu)


func test_default_presets_catalog_shows_names_not_numbered_fallback() -> void:
	# Mesmo formato que GET /periods/vila-pequena/catalog devolve com os presets padrão (#46 fatia 1).
	var transport := _transport()
	transport.on("GET /periods/medieval/catalog", {"status": 200, "body": {"professionIds": [1, 2],
		"professionNames": {"1": "Lavrador", "2": "Artesão"}}})
	var menu := _menu(transport)
	await menu.show_new_world()
	check("Lavrador" in menu.wizard.period_info.text and "Artesão" in menu.wizard.period_info.text, "mostra os dois nomes")
	check(not ("Profissão " in menu.wizard.period_info.text), "nenhum 'Profissão N' com os presets padrão")
	_done(menu)


func test_unnamed_profession_still_falls_back_to_numbered_label() -> void:
	# Braço de controle: sem nome no catálogo, o rótulo numerado aparece (prova que o teste acima discrimina).
	var transport := _transport()
	transport.on("GET /periods/medieval/catalog", {"status": 200, "body": {"professionIds": [1, 2], "professionNames": {"1": "Lavrador"}}})
	var menu := _menu(transport)
	await menu.show_new_world()
	check("Profissão 2" in menu.wizard.period_info.text, "id sem nome cai em 'Profissão 2'")
	_done(menu)


func test_period_cards_and_hint_come_from_the_server_card() -> void:
	var transport := _transport()
	transport.on("GET /periods", {"status": 200, "body": [
		{"periodId": "medieval", "source": "x", "width": 2, "height": 2, "card": CARD},
		{"periodId": "maior", "source": "y", "width": 4, "height": 4, "card": CARD.merged({"name": "Vila grande", "initialPopulation": 150}, true)}]})
	var menu := _menu(transport)
	await menu.show_new_world()
	var cards := Kit.all_text(menu.wizard.period_cards)
	check("Vila do vale" in cards and "Poucas famílias num vale." in cards and "40 pessoas · 1 vila" in cards, "cartão com nome, frase e números do servidor: " + cards)
	check("Vila grande" in cards and "150 pessoas" in cards, "o segundo período também: " + cards)
	check("mesma época" in menu.wizard.period_hint.text and "Aldeias agrícolas" in menu.wizard.period_hint.text,
		"a época aparece como informação, sem prometer outras: " + menu.wizard.period_hint.text)
	check("Ferramentas de mão" in menu.wizard.period_info.text, "tecnologia de partida do cartão: " + menu.wizard.period_info.text)
	eq(menu.wizard.STEP_NAMES[0], "Começo", "o passo não se chama Época quando não há épocas para escolher")
	_done(menu)


func test_without_a_card_the_period_falls_back_to_its_source_and_map_size() -> void:
	var menu := _menu(_transport())
	menu.api.transport.on("GET /periods", {"status": 200, "body": [{"periodId": "medieval", "source": "Vila medieval", "width": 2, "height": 2}]})
	await menu.show_new_world()
	var cards := Kit.all_text(menu.wizard.period_cards)
	check("Vila medieval" in cards and "2 por 2 terras" in cards, "API antiga: nome do cadastro e tamanho: " + cards)
	eq(menu.wizard.period_hint.text, Pages.PERIOD_HINT, "sem época conhecida, a frase de sempre")
	_done(menu)


func test_create_asks_the_server_for_a_running_world_when_the_session_has_the_token() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	menu.can_run_clock = true
	await menu.show_new_world()
	menu.wizard.poll_seconds = 0.0
	await menu.wizard.create()
	check("POST /worlds/create/async?startRunning=true" in transport.calls, "pede rodando: " + str(transport.calls))
	eq(transport.count_calls("POST /simulation/resume"), 0, "o cliente não despausa por conta própria")
	_done(menu)


func test_can_run_clock_reads_the_token_when_used_not_when_the_menu_is_built() -> void:
	# #237: o token entra no ambiente depois do menu (o cliente liga a API ou lê o api.state): pedir rodando tem de valer
	var saved := OS.get_environment("LW_ADMIN_TOKEN")
	OS.unset_environment("LW_ADMIN_TOKEN")
	var menu := _menu(_transport())
	check(not menu.can_run_clock, "sem token, não pede rodando")
	OS.set_environment("LW_ADMIN_TOKEN", "tok-sessao")
	check(menu.can_run_clock, "o token chegou depois do menu: passa a pedir rodando")
	menu.show_saved_worlds()
	check(menu.saved_worlds_screen.start_running, "Mundos salvos abre o mundo rodando")
	if saved == "":
		OS.unset_environment("LW_ADMIN_TOKEN")
	else:
		OS.set_environment("LW_ADMIN_TOKEN", saved)
	_done(menu)


func test_failed_job_shows_the_server_reason_and_stays_in_the_wizard() -> void:
	var transport := _transport()
	transport.on("GET /worlds/create/jobs/", job_route(true, 409, "Já existe um mundo salvo com esta seed."))
	var menu := _menu(transport)
	await menu.show_new_world()
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)
	await menu.wizard.go_to(menu.wizard.SUMMARY_STEP)
	await menu.wizard.next()
	eq(fired[0], false, "falha não entra no jogo")
	check("semente" in menu.wizard.message_label.text, "conflito vindo do andamento em frase humana: " + menu.wizard.message_label.text)
	eq(menu.current, menu.wizard, "volta ao assistente")
	_done(menu)


func test_continue_uses_the_world_the_server_marks_as_open() -> void:
	var menu := _menu(_transport(true, [world_item("x", "Recente fechado", false), world_item("a", "Aberto", true, "player", 12)]))
	await menu.refresh_server()
	var text: String = menu.title.continue_button.text
	check("Aberto" in text and "ano 12" in text and "Vila do vale" in text, "nome, ano e lugar do mundo aberto: " + text)
	check(not ("Recente fechado" in text), "não adivinha pelo mais recente")
	_done(menu)
