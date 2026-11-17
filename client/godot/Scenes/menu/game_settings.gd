extends RefCounted
## Preferências locais do jogador (só desta máquina, nada vai ao servidor): tamanho da interface, volume, tela cheia,
## vsync, idioma, dicas já vistas e se já entrou em algum mundo. Guardadas em `user://settings.cfg`.

const DEFAULT_PATH := "user://settings.cfg"
const UI_SCALES: Array[float] = [0.85, 1.0, 1.15, 1.3, 1.5]

var path := DEFAULT_PATH ## os testes apontam para um arquivo próprio
var ui_scale := 1.0
var volume := 0.8 ## 0..1
var fullscreen := false
var vsync := true
var language := "pt_BR" ## só português por enquanto
var tips_done := false ## as dicas dos primeiros passos já foram vistas (ou puladas)
var has_played := false ## já entrou em algum mundo: o Continuar faz sentido


static func load_from(p_path: String = DEFAULT_PATH):
	var settings = load("res://Scenes/menu/game_settings.gd").new()
	settings.path = p_path
	var file := ConfigFile.new()
	if file.load(p_path) != OK:
		return settings
	settings.ui_scale = clampf(float(file.get_value("view", "ui_scale", 1.0)), 0.75, 1.6)
	settings.volume = clampf(float(file.get_value("sound", "volume", 0.8)), 0.0, 1.0)
	settings.fullscreen = bool(file.get_value("view", "fullscreen", false))
	settings.vsync = bool(file.get_value("view", "vsync", true))
	settings.language = String(file.get_value("general", "language", "pt_BR"))
	settings.tips_done = bool(file.get_value("progress", "tips_done", false))
	settings.has_played = bool(file.get_value("progress", "has_played", false))
	return settings


func save() -> void:
	var file := ConfigFile.new()
	file.set_value("view", "ui_scale", ui_scale)
	file.set_value("view", "fullscreen", fullscreen)
	file.set_value("view", "vsync", vsync)
	file.set_value("sound", "volume", volume)
	file.set_value("general", "language", language)
	file.set_value("progress", "tips_done", tips_done)
	file.set_value("progress", "has_played", has_played)
	file.save(path)


## Aplica na janela e no som. Sem janela de verdade (testes headless) só o volume e a escala valem.
func apply(tree: SceneTree) -> void:
	if tree != null and tree.root != null:
		tree.root.content_scale_factor = ui_scale
	var bus := AudioServer.get_bus_index("Master")
	AudioServer.set_bus_volume_db(bus, linear_to_db(maxf(volume, 0.0001)))
	AudioServer.set_bus_mute(bus, volume <= 0.001)
	if DisplayServer.get_name() == "headless":
		return
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN if fullscreen else DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED if vsync else DisplayServer.VSYNC_DISABLED)
