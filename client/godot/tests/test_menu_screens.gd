extends "res://tests/test_base.gd"
## Telas do menu e do jogo da AD-174: opções, sem servidor, carregamento, pausa, primeiros passos, ajuda e API local.

const T := preload("res://tests/test_inspector.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const OptionsScreen := preload("res://Scenes/menu/options_screen.gd")
const OfflineScreen := preload("res://Scenes/menu/offline_screen.gd")
const LoadingScreen := preload("res://Scenes/menu/loading_screen.gd")
const LocalServer := preload("res://Contract/local_server.gd")
const Shortcuts := preload("res://Scenes/shortcuts.gd")
const Kit := preload("res://Scenes/menu/menu_kit.gd")

const PATH := "user://test-screens-settings.cfg"


func _fresh_settings():
	DirAccess.remove_absolute(ProjectSettings.globalize_path(PATH))
	return GameSettings.load_from(PATH)


func _app_with(settings, fake) -> Node:
	var app = T.App.new()
	app.settings = settings
	tree.root.add_child(app)
	await app.start(fake, false)
	return app


func test_options_apply_and_are_remembered() -> void:
	var settings = _fresh_settings()
	var screen := OptionsScreen.new()
	screen.settings = settings
	tree.root.add_child(screen)
	screen.volume_slider.value = 40
	eq(GameSettings.load_from(PATH).volume, 0.4, "volume guardado")
	check(absf(db_to_linear(AudioServer.get_bus_volume_db(0)) - 0.4) < 0.01, "volume aplicado no som")
	screen.scale_pick.select(2)
	screen.scale_pick.item_selected.emit(2)
	eq(GameSettings.load_from(PATH).ui_scale, GameSettings.UI_SCALES[2], "tamanho da interface guardado")
	check(is_equal_approx(tree.root.content_scale_factor, GameSettings.UI_SCALES[2]), "e aplicado na janela")
	screen.vsync_check.button_pressed = false
	eq(GameSettings.load_from(PATH).vsync, false, "vsync guardado")
	eq(screen.language_pick.get_item_text(0), "Português (Brasil)", "só português por ora")
	var closed := [false]
	screen.closed.connect(func(): closed[0] = true)
	var esc := InputEventAction.new()
	esc.action = "ui_cancel"
	esc.pressed = true
	tree.root.push_input(esc)
	eq(closed[0], true, "Esc fecha as opções")
	screen.free()
	settings.ui_scale = 1.0
	settings.volume = 0.8
	settings.apply(tree)
	DirAccess.remove_absolute(ProjectSettings.globalize_path(PATH))


func test_offline_screen_explains_and_shows_progress_while_starting() -> void:
	var screen := OfflineScreen.new()
	tree.root.add_child(screen)
	screen.set_can_start(true)
	eq(screen.start_button.visible, true, "com o binário, oferece ligar")
	check(screen.start_button.has_focus(), "e o foco já está nele")
	screen.show_starting(12)
	eq(screen.progress.visible, true, "barra enquanto liga")
	eq(int(screen.progress.value), 12, "segundos de espera")
	eq(screen.start_button.visible, false, "sem botão de ligar de novo")
	screen.show_failed()
	check("não ligou" in screen.headline.text and "Jogar.command" in screen.explanation.text, "falha diz o que fazer")
	screen.free()


func test_loading_screen_walks_real_stages() -> void:
	var screen := LoadingScreen.new()
	tree.root.add_child(screen)
	screen.set_stage(1)
	eq(screen.stage_label.text, LoadingScreen.STAGES[1], "frase da etapa")
	eq(int(screen.bar.value), 2, "barra avança com a etapa")
	screen.set_message("O mundo não está respondendo.")
	eq(screen.stage_label.text, "O mundo não está respondendo.", "mensagem fora das etapas")
	screen.free()


func test_launcher_state_is_read_and_only_started_process_is_stopped() -> void:
	var dir := ProjectSettings.globalize_path("user://test-launcher")
	DirAccess.make_dir_recursive_absolute(dir)
	var file := FileAccess.open(dir.path_join("api.state"), FileAccess.WRITE)
	file.store_string("API_PID=123\nPORT=5216\nTOKEN=abc\nOWNER_PID=9\n")
	file.close()
	eq(LocalServer.launcher_state(dir), {"port": 5216, "token": "abc"}, "porta e token do lançador")
	DirAccess.remove_absolute(dir.path_join("api.state"))
	eq(LocalServer.launcher_state(dir), {}, "sem arquivo, nada")
	DirAccess.remove_absolute(dir)
	eq(LocalServer.can_start(), false, "em teste (sem janela) nunca liga processo")
	check(LocalServer.dll_path().ends_with("src/LivingWorld.Api/bin/Release/net10.0/LivingWorld.Api.dll"), "caminho fixo do binário do projeto")
	var server := LocalServer.new()
	eq(server.start(), false, "não liga sem poder")
	server.stop() # sem processo próprio: não encerra nada
	eq(server.pid, -1, "nenhum processo")


func test_pause_menu_pauses_time_for_the_player_and_resumes_on_continue() -> void:
	var fake = T.transport()
	fake.on("POST /simulation/pause", {"status": 200, "body": null})
	fake.on("POST /simulation/resume", {"status": 200, "body": null})
	var app = await T.make_app(tree, fake)
	app.clock_panel.set_control_enabled(true)
	Shortcuts.escape(app)
	await tree.process_frame
	eq(app.pause_menu.is_open(), true, "Esc sem nada aberto abre a pausa")
	eq(fake.count_calls("POST /simulation/pause"), 1, "pausa o tempo")
	check("Ano 0" in app.pause_menu.world_line.text, "diz em que ano o mundo está")
	check(app.pause_menu.continue_button.has_focus(), "foco em Continuar")
	Shortcuts.escape(app)
	await tree.process_frame
	eq(app.pause_menu.is_open(), false, "Esc de novo fecha")
	eq(fake.count_calls("POST /simulation/resume"), 1, "e devolve o tempo")
	var menu := [false]
	app.menu_requested.connect(func(): menu[0] = true)
	var quit := [false]
	app.quit_requested.connect(func(): quit[0] = true)
	await app.pause_menu.open()
	Kit.find_button(app.pause_menu, "Voltar ao menu").pressed.emit()
	await tree.process_frame
	await tree.process_frame
	eq(menu[0], true, "Voltar ao menu")
	Kit.find_button(app.pause_menu, "Sair do jogo").pressed.emit()
	eq(quit[0], true, "Sair do jogo")
	app.queue_free()


func test_pause_menu_without_time_control_does_not_touch_the_clock() -> void:
	var fake = T.transport()
	var app = await T.make_app(tree, fake)
	await app.pause_menu.open()
	eq(fake.count_calls("POST /simulation"), 0, "sem token não manda nada")
	app.pause_menu.close()
	app.queue_free()


func test_first_steps_tips_show_once_and_help_reopens_them() -> void:
	var settings = _fresh_settings()
	var app = await _app_with(settings, T.transport())
	check(app.first_steps.active(), "primeira vez: dicas abertas")
	check("Dica 1 de 6" in app.first_steps.counter.text, app.first_steps.counter.text)
	await tree.process_frame
	await tree.process_frame
	var bubble: Rect2 = app.first_steps.bubble.get_rect()
	var screen := Rect2(Vector2.ZERO, app.get_viewport().get_visible_rect().size)
	check(screen.encloses(bubble), "dica dentro da tela: %s em %s" % [bubble, screen])
	for _i in 6:
		app.first_steps.next_tip()
	eq(app.first_steps.active(), false, "seis dicas e acabou")
	eq(GameSettings.load_from(PATH).tips_done, true, "lembradas para a próxima vez")
	app.queue_free()
	var again = await _app_with(GameSettings.load_from(PATH), T.transport())
	eq(again.first_steps.active(), false, "segunda vez: sem dicas")
	again.help_button.pressed.emit()
	eq(again.first_steps.help.visible, true, "? abre a ajuda")
	check("Próximo acontecimento" in Kit.all_text(again.first_steps.help), "ajuda lista os atalhos")
	Shortcuts.escape(again)
	eq(again.first_steps.help.visible, false, "Esc fecha a ajuda")
	eq(again.pause_menu.is_open(), false, "sem abrir a pausa junto")
	Kit.find_button(again.first_steps.help, "Ver as dicas de novo").pressed.emit()
	check(again.first_steps.active(), "dá para rever as dicas")
	Shortcuts.escape(again)
	eq(again.first_steps.active(), false, "Esc pula as dicas")
	again.queue_free()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(PATH))


func test_world_button_appears_inside_a_city() -> void:
	var app = await T.make_app(tree, T.transport())
	await tree.process_frame
	eq(app.world_button.visible, false, "no mundo, sem botão")
	await app.enter_scope("City", "city-a")
	await tree.process_frame
	eq(app.world_button.visible, true, "na cidade, botão para voltar ao mapa")
	app.queue_free()


class StubServer extends RefCounted:
	var stopped := 0
	func stop() -> void:
		stopped += 1


func test_quitting_stops_the_api_this_client_started() -> void:
	var boot = load("res://Scenes/boot.gd").new()
	boot.quit_fn = func(): pass
	var stub := StubServer.new()
	boot.local_server = stub
	await boot.quit_gracefully()
	eq(stub.stopped, 1, "ao sair, desliga a API que o próprio cliente ligou (LocalServer.stop só mata o pid dele)")
	boot.free()
