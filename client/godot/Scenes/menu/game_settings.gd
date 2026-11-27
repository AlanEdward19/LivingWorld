extends RefCounted
## Preferências locais do jogador (só desta máquina, nada vai ao servidor): tamanho da interface, volume, tela cheia,
## vsync, idioma, dicas já vistas e se já entrou em algum mundo. Guardadas em `user://settings.cfg`.

const DEFAULT_PATH := "user://settings.cfg"
const UI_SCALES: Array[float] = [0.85, 1.0, 1.15, 1.3, 1.5]

var path := DEFAULT_PATH ## os testes apontam para um arquivo próprio
var ui_scale := 1.0
var volume := 0.8 ## 0..1
var sfx_volume := 0.8 ## 0..1: efeitos do jogo (passos, golpes, portas), no barramento SFX (#232)
var shake := true ## tremor leve da câmera no golpe e no pouso forte (#232)
var fullscreen := false
var vsync := true
var language := "pt_BR" ## só português por enquanto
var tips_done := false ## as dicas dos primeiros passos já foram vistas (ou puladas)
var has_played := false ## já entrou em algum mundo: o Continuar faz sentido
var guide_seen: PackedStringArray = [] ## #238: mundos em que o guia da primeira posse já foi visto (ou dispensado)


static func load_from(p_path: String = DEFAULT_PATH):
	var settings = load("res://Scenes/menu/game_settings.gd").new()
	settings.path = p_path
	var file := ConfigFile.new()
	if file.load(p_path) != OK:
		return settings
	settings.ui_scale = clampf(float(file.get_value("view", "ui_scale", 1.0)), 0.75, 1.6)
	settings.volume = clampf(float(file.get_value("sound", "volume", 0.8)), 0.0, 1.0)
	settings.sfx_volume = clampf(float(file.get_value("sound", "sfx_volume", 0.8)), 0.0, 1.0)
	settings.shake = bool(file.get_value("view", "shake", true))
	settings.fullscreen = bool(file.get_value("view", "fullscreen", false))
	settings.vsync = bool(file.get_value("view", "vsync", true))
	settings.language = String(file.get_value("general", "language", "pt_BR"))
	settings.tips_done = bool(file.get_value("progress", "tips_done", false))
	settings.has_played = bool(file.get_value("progress", "has_played", false))
	settings.guide_seen = PackedStringArray(file.get_value("progress", "guide_seen", PackedStringArray()))
	return settings


func save() -> void:
	var file := ConfigFile.new()
	file.set_value("view", "ui_scale", ui_scale)
	file.set_value("view", "fullscreen", fullscreen)
	file.set_value("view", "vsync", vsync)
	file.set_value("sound", "volume", volume)
	file.set_value("sound", "sfx_volume", sfx_volume)
	file.set_value("view", "shake", shake)
	file.set_value("general", "language", language)
	file.set_value("progress", "tips_done", tips_done)
	file.set_value("progress", "has_played", has_played)
	file.set_value("progress", "guide_seen", guide_seen)
	file.save(path)


## #238: o guia da primeira posse deste mundo já foi visto? (a chave é do mundo; reabrir fica pela ajuda F1)
func has_seen_guide(key: String) -> bool:
	return guide_seen.has(key)


func mark_guide_seen(key: String) -> void:
	if not guide_seen.has(key):
		guide_seen.append(key)
		save()


## Aplica na janela e no som. Sem janela de verdade (testes headless) só o volume e a escala valem.
func apply(tree: SceneTree) -> void:
	if tree != null and tree.root != null:
		tree.root.content_scale_factor = ui_scale
	var bus := AudioServer.get_bus_index("Master")
	AudioServer.set_bus_volume_db(bus, linear_to_db(maxf(volume, 0.0001)))
	AudioServer.set_bus_mute(bus, volume <= 0.001)
	apply_sfx()
	if DisplayServer.get_name() == "headless":
		return
	DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN if fullscreen else DisplayServer.WINDOW_MODE_WINDOWED)
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_ENABLED if vsync else DisplayServer.VSYNC_DISABLED)


## #232: volume dos efeitos no barramento SFX (criado pelo Feedback; sem ele, nada a fazer).
func apply_sfx() -> void:
	var sfx := AudioServer.get_bus_index("SFX")
	if sfx >= 0:
		AudioServer.set_bus_volume_db(sfx, linear_to_db(maxf(sfx_volume, 0.0001)))
		AudioServer.set_bus_mute(sfx, sfx_volume <= 0.001)
