extends Node
## Raiz do jogo: abre o menu principal; ao ter um mundo, entra na simulação (App); o menu de pausa (Esc) ou F10 volta.
## `--steps` (capturas e demos de desenvolvimento) pula o menu e vai direto ao mundo; `--steps "menu;…"` fotografa o menu.
## Se o próprio cliente ligou a API (LocalServer), desliga ao sair; nunca mexe em processo que não criou.

const App := preload("res://Scenes/app.gd")
const MainMenu := preload("res://Scenes/main_menu.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const LocalServer := preload("res://Contract/local_server.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")
const MenuSteps := preload("res://Scenes/menu/menu_steps.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const UiSounds := preload("res://Scenes/menu/ui_sounds.gd")

signal _quit_ready()

const QUIT_GRACE := 2.0 ## no máximo isto à espera do servidor ao fechar a janela

var menu
var app
var quit_fn: Callable
var quit_grace := QUIT_GRACE
var local_server = LocalServer.new()
var settings
var _transport
var _released := false
var _quitting := false


func _ready() -> void:
	get_tree().auto_accept_quit = false # fechar a janela passa por quit_gracefully
	if not quit_fn.is_valid():
		quit_fn = get_tree().quit
	_transport = HttpTransport.new()
	add_child(_transport)
	add_child(UiSounds.new()) # sons de interface do menu e da pausa (gerados por código; volume das Opções)
	settings = GameSettings.load_from()
	var steps := DevSteps.parse(OS.get_cmdline_user_args())
	if MenuSteps.is_menu(steps):
		_show_menu()
		MenuSteps.run(self, steps)
		return
	if not steps.is_empty():
		_play() # medidas e capturas do jogo: sem preferências locais (vsync, escala) para não mudar o número
		return
	settings.apply(get_tree())
	_show_menu()


func _show_menu() -> void:
	if app != null:
		await app.release_observation()
		app.queue_free()
		app = null
	menu = MainMenu.new()
	menu.api = ApiClient.new(_transport)
	menu.local_server = local_server
	menu.settings = settings
	menu.world_ready.connect(_play)
	menu.quit_requested.connect(quit_gracefully)
	add_child(menu)
	menu.refresh_server()


func _play() -> void:
	var quick: bool = menu != null and menu.is_quick_start() # #256: o início rápido já entra no corpo de uma pessoa da casa
	if menu != null:
		menu.queue_free()
		menu = null
	app = App.new()
	app.settings = settings
	app.quick_start = quick
	app.menu_requested.connect(_show_menu)
	app.quit_requested.connect(quit_gracefully)
	add_child(app)


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		quit_gracefully()


## Fechar a janela do jogo retira a fonte de observação e encerra a conversa aberta, como voltar ao menu; depois
## desliga a API se foi este cliente que a ligou.
func quit_gracefully() -> void:
	if _quitting:
		return
	_quitting = true
	if app != null:
		_released = false
		_release_app()
		if not _released:
			get_tree().create_timer(quit_grace).timeout.connect(func(): _quit_ready.emit())
			await _quit_ready
	local_server.stop()
	quit_fn.call()


func _release_app() -> void:
	await app.release_observation()
	_released = true
	_quit_ready.emit()
