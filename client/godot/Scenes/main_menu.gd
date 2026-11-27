extends Control
## Menu principal estilo videogame: título sobre um mundo girando, Continuar (só com mundo para voltar), Novo mundo
## em passos, Meus mundos, Opções e Sair. Sem servidor, explica o que fazer ou liga o mundo sozinho (LocalServer).
## Cada tela mora no seu arquivo (Scenes/menu/); aqui só a troca entre elas. Só fala com a API pelo ApiClient.

signal world_ready() ## há um mundo no servidor para jogar (continuado, aberto ou recém-criado)
signal quit_requested()

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const UiTheme := preload("res://Scenes/ui_theme.gd")
const Backdrop := preload("res://Scenes/menu/menu_backdrop.gd")
const TitleScreen := preload("res://Scenes/menu/title_screen.gd")
const OfflineScreen := preload("res://Scenes/menu/offline_screen.gd")
const Wizard := preload("res://Scenes/menu/new_world_wizard.gd")
const LoadingScreen := preload("res://Scenes/menu/loading_screen.gd")
const OptionsScreen := preload("res://Scenes/menu/options_screen.gd")
const SavedWorldsMenu := preload("res://Scenes/saved_worlds_menu.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const LocalServer := preload("res://Contract/local_server.gd")
const WorldTouchup := preload("res://Scenes/world_touchup.gd")
const FamilyBuilder := preload("res://Scenes/family_builder.gd")
const WorldEditor := preload("res://Scenes/world_editor.gd")

const START_TIMEOUT := 90 ## segundos esperando o mundo ligado pelo próprio cliente

var api
var local_server ## LocalServer do Boot (vive além do menu); nulo = o cliente nunca liga a API
var settings
var server_online := false
## O token de admin da sessão (LW_ADMIN_TOKEN, posto pelo lançador) deixa pedir o mundo já rodando ao criar ou abrir.
## #237: lido na hora de usar (o token entra no ambiente DEPOIS do menu existir quando o próprio cliente liga a API ou lê o api.state);
## ler uma vez só, ao criar o menu, deixava todo mundo aberto pausado.
var can_run_clock: bool:
	get:
		return bool(_run_override) if _run_override != null else OS.get_environment("LW_ADMIN_TOKEN") != ""
	set(value):
		_run_override = value
var _run_override: Variant = null ## atribuído (testes, quem decide): vale o valor; senão, o ambiente na hora
var backdrop: Node3D
var title: Control
var offline: Control
var wizard: Control
var loading: Control
var options: Control
var saved_worlds_screen: Control
var touchup
var family_builder
var current: Control


func _ready() -> void:
	theme = UiTheme.build()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if settings == null:
		settings = GameSettings.load_from()
	backdrop = Backdrop.new()
	add_child(backdrop)
	var shade := TextureRect.new() # escurece as bordas para o texto ler bem sobre o mundo girando
	var gradient := Gradient.new()
	gradient.set_color(0, Color(0.03, 0.05, 0.1, 0.35))
	gradient.set_color(1, Color(0.03, 0.05, 0.1, 0.8))
	var texture := GradientTexture2D.new()
	texture.gradient = gradient
	texture.fill = GradientTexture2D.FILL_RADIAL
	texture.fill_from = Vector2(0.5, 0.45)
	texture.fill_to = Vector2(1.1, 1.1)
	shade.texture = texture
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	shade.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(shade)
	title = TitleScreen.new()
	add_child(title)
	title.continue_pressed.connect(_on_continue)
	title.new_world_pressed.connect(show_new_world)
	title.my_worlds_pressed.connect(show_saved_worlds)
	title.options_pressed.connect(show_options)
	title.quit_pressed.connect(func(): quit_requested.emit())
	offline = OfflineScreen.new()
	add_child(offline)
	offline.retry_pressed.connect(refresh_server)
	offline.start_pressed.connect(start_local_server)
	offline.quit_pressed.connect(func(): quit_requested.emit())
	wizard = Wizard.new()
	add_child(wizard)
	wizard.cancelled.connect(show_title)
	wizard.creating.connect(func(): _show_loading("Criando " + wizard.world_name.strip_edges(), 1))
	wizard.creation_progress.connect(func(index: int, count: int, text: String): loading.set_progress(index, count, text))
	wizard.create_failed.connect(func(): _switch(wizard))
	wizard.created.connect(_on_created)
	wizard.map_previewed.connect(func(body: Dictionary): backdrop.show_map(body))
	wizard.family_requested.connect(show_family_builder)
	loading = LoadingScreen.new()
	add_child(loading)
	options = OptionsScreen.new()
	options.settings = settings
	add_child(options)
	options.closed.connect(show_title)
	for screen in [title, offline, wizard, loading, options]:
		screen.visible = false
	_switch(title)


func _switch(screen: Control) -> void:
	for other in [title, offline, wizard, loading, options, saved_worlds_screen]:
		if other != null:
			other.visible = other == screen
	current = screen
	Kit.fade_in(screen)
	if screen.has_method("focus_default") and is_inside_tree():
		screen.focus_default()


func _wizard_api() -> void:
	wizard.api = api
	wizard.start_running = can_run_clock


## Chamado depois de `api` ser definido: consulta o servidor; sem ele, procura o do lançador ou liga um.
func refresh_server() -> void:
	var status: Dictionary = await api.clock_status()
	server_online = status.ok
	if server_online:
		title.set_status("Mundo ligado", true)
		title.set_continue(await continue_info(status.body))
		if current == offline:
			_switch(title)
		return
	if await _try_launcher_api():
		return
	title.set_continue({})
	title.set_status("O mundo está desligado", false)
	var can_start: bool = local_server != null and LocalServer.can_start()
	offline.set_can_start(can_start)
	_switch(offline)
	if can_start and not local_server.running():
		await start_local_server()


## O lançador (`Jogar.command`) deixa porta e token em `api.state`: um cliente aberto à parte usa a mesma API.
func _try_launcher_api() -> bool:
	if local_server == null or OS.get_environment("LW_API_URL") != "":
		return false
	var state: Dictionary = LocalServer.launcher_state()
	if state.is_empty():
		return false
	LocalServer.use(int(state.port), String(state.token))
	_point_transport()
	var status: Dictionary = await api.clock_status()
	if not status.ok:
		return false
	server_online = true
	title.set_status("Mundo ligado", true)
	title.set_continue(await continue_info(status.body))
	_switch(title)
	return true


func _point_transport() -> void:
	if "base_url" in api.transport:
		api.transport.base_url = OS.get_environment("LW_API_URL")


## Liga a API compilada do projeto e espera ela responder, mostrando quanto falta.
func start_local_server() -> void:
	if local_server == null or not local_server.start():
		offline.show_failed()
		return
	_point_transport()
	for second in START_TIMEOUT:
		offline.show_starting(second)
		await get_tree().create_timer(1.0).timeout
		if not local_server.running():
			break
		var status: Dictionary = await api.clock_status()
		if status.ok:
			await refresh_server()
			return
	offline.show_failed()


## {name, year, population, period} do mundo aberto no servidor (o da lista com `isCurrent`, AD-175); vazio se não há
## mundo para continuar. O mundo inicial que a API cria sozinha (`origin` "automatic") só conta se o jogador já jogou.
func continue_info(_status_body: Dictionary = {}) -> Dictionary:
	var list: Dictionary = await api.saved_worlds()
	if not list.ok or not (list.body is Array) or list.body.is_empty():
		return {}
	var open: Dictionary = {}
	for world in list.body:
		if bool(world.get("isCurrent", false)):
			open = world
	if open.is_empty():
		return {}
	if String(open.get("origin", "")) == "automatic" and not settings.has_played:
		return {}
	return {"name": SavedWorldsMenu.display_name(open), "year": int(open.get("year", 0)),
		"population": int(open.get("population", 0)), "period": String(open.get("periodName", ""))}


func show_title() -> void:
	if saved_worlds_screen != null:
		saved_worlds_screen.queue_free()
		saved_worlds_screen = null
	backdrop.show_island(7)
	_switch(title)


func show_new_world() -> void:
	_wizard_api()
	_switch(wizard)
	await wizard.open()


func show_options() -> void:
	_switch(options)


func show_saved_worlds() -> void:
	saved_worlds_screen = SavedWorldsMenu.new()
	saved_worlds_screen.api = api
	saved_worlds_screen.start_running = can_run_clock
	saved_worlds_screen.back.connect(show_title)
	saved_worlds_screen.loaded.connect(_enter_world)
	add_child(saved_worlds_screen)
	_switch(saved_worlds_screen)
	await saved_worlds_screen.refresh_worlds()


func _show_loading(heading: String, stage: int) -> void:
	loading.set_heading(heading)
	loading.set_stage(stage)
	_switch(loading)


func _on_continue() -> void:
	_enter_world()


## Entrar no mundo: lembra que o jogador já jogou (Continuar faz sentido daqui em diante) e passa ao jogo.
func _enter_world() -> void:
	_show_loading("Entrando no mundo", 2)
	settings.has_played = true
	settings.save()
	world_ready.emit()


func _on_created(with_touchup: bool) -> void:
	loading.set_stage(2)
	if with_touchup:
		await show_touchup()
	else:
		_enter_world()


## Retoques depois de criado (opcional): personalidade e dons de pessoas do mundo recém-criado.
func show_touchup() -> void:
	for screen in [title, offline, wizard, loading, options]:
		screen.visible = false
	touchup = WorldTouchup.new()
	touchup.api = api
	touchup.finished.connect(func(): touchup.queue_free(); touchup = null; _enter_world())
	add_child(touchup)
	await touchup.load_npcs()


func show_family_builder() -> void:
	wizard.visible = false
	family_builder = FamilyBuilder.new()
	var settlement_names: Array[String] = []
	for settlement in WorldEditor.read(wizard.draft).settlements:
		settlement_names.append(settlement.name)
	add_child(family_builder)
	family_builder.setup(WorldEditor.read_households(wizard.draft), wizard.professions, settlement_names, wizard.skills)
	family_builder.done.connect(func(families: Array):
		wizard.draft = WorldEditor.with_households(wizard.draft, families)
		_close_family_builder())
	family_builder.cancelled.connect(_close_family_builder)


func _close_family_builder() -> void:
	family_builder.queue_free()
	family_builder = null
	_switch(wizard)
	wizard.custom.sync()
	wizard.refresh_preview()
