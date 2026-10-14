extends Node
## Raiz do jogo: abre o menu principal; ao ter um mundo, entra na simulação (App) e F10 volta ao menu.
## `--steps` (capturas e demos de desenvolvimento) pula o menu e vai direto ao mundo.

const App := preload("res://Scenes/app.gd")
const MainMenu := preload("res://Scenes/main_menu.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")

var menu
var app
var _transport


func _ready() -> void:
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
	if menu != null:
		menu.queue_free()
		menu = null
	app = App.new()
	app.menu_requested.connect(_show_menu)
	add_child(app)
