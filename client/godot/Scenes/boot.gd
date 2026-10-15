extends Node
## Raiz do jogo: abre o menu principal; ao ter um mundo, entra na simulação (App) e F10 volta ao menu.
## `--steps` (capturas e demos de desenvolvimento) pula o menu e vai direto ao mundo.

const App := preload("res://Scenes/app.gd")
const MainMenu := preload("res://Scenes/main_menu.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")

signal _quit_ready()

const QUIT_GRACE := 2.0 ## no máximo isto à espera do servidor ao fechar a janela

var menu
var app
var quit_fn: Callable
var quit_grace := QUIT_GRACE
var _transport
var _released := false
var _quitting := false


func _ready() -> void:
	get_tree().auto_accept_quit = false # fechar a janela passa por quit_gracefully
	if not quit_fn.is_valid():
		quit_fn = get_tree().quit
	_transport = HttpTransport.new()
	add_child(_transport)
	if not DevSteps.parse(OS.get_cmdline_user_args()).is_empty():
		_play()
		return
	_show_menu()


func _show_menu() -> void:
	if app != null:
		await app.release_observation()
		app.queue_free()
		app = null
	menu = MainMenu.new()
	menu.api = ApiClient.new(_transport)
	menu.world_ready.connect(_play)
	add_child(menu)
	menu.refresh_server()


func _play() -> void:
	var created_period: String = menu.selected_period if menu != null else ""
	if menu != null:
		menu.queue_free()
		menu = null
	app = App.new()
	app.period_id = created_period
	app.menu_requested.connect(_show_menu)
	add_child(app)


func _notification(what: int) -> void:
	if what == NOTIFICATION_WM_CLOSE_REQUEST:
		quit_gracefully()


## Fechar a janela do jogo retira a fonte de observação e encerra a conversa aberta, como voltar ao menu.
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
	quit_fn.call()


func _release_app() -> void:
	await app.release_observation()
	_released = true
	_quit_ready.emit()
