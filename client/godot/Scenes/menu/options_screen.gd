extends Control
## Opções: tamanho da interface, volume, tela cheia, vsync e idioma (só português por ora). Cada mudança vale na hora
## e fica guardada nesta máquina (GameSettings). Esc ou "Voltar" fecha. Usada no título e no menu de pausa.

signal closed()

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const SCALE_NAMES: Array[String] = ["Pequena", "Normal", "Grande", "Maior", "Enorme"]

var settings
var scale_pick: OptionButton
var volume_slider: HSlider
var volume_label: Label
var fullscreen_check: CheckButton
var vsync_check: CheckButton
var language_pick: OptionButton
var back_button: Button


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	if settings == null:
		settings = GameSettings.load_from()
	var box := Kit.screen(self, 560)
	Kit.title("Opções", 36, box, Kit.GOLD)
	scale_pick = OptionButton.new()
	for i in GameSettings.UI_SCALES.size():
		scale_pick.add_item("%s (%d%%)" % [SCALE_NAMES[i], int(round(GameSettings.UI_SCALES[i] * 100))], i)
	scale_pick.select(_nearest_scale(settings.ui_scale))
	scale_pick.item_selected.connect(func(i: int): settings.ui_scale = GameSettings.UI_SCALES[i]; _changed())
	_line(box, "Tamanho dos textos e botões", scale_pick)
	var volume_row := HBoxContainer.new()
	volume_slider = HSlider.new()
	volume_slider.min_value = 0
	volume_slider.max_value = 100
	volume_slider.step = 5
	volume_slider.value = round(settings.volume * 100)
	volume_slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	volume_slider.focus_mode = Control.FOCUS_ALL
	volume_slider.value_changed.connect(func(v: float): settings.volume = v / 100.0; _changed())
	volume_row.add_child(volume_slider)
	volume_label = Label.new()
	volume_label.custom_minimum_size.x = 56
	volume_row.add_child(volume_label)
	_line(box, "Volume", volume_row)
	fullscreen_check = _check(settings.fullscreen, func(on: bool): settings.fullscreen = on; _changed())
	_line(box, "Tela cheia", fullscreen_check)
	vsync_check = _check(settings.vsync, func(on: bool): settings.vsync = on; _changed())
	_line(box, "Sincronia vertical", vsync_check)
	language_pick = OptionButton.new()
	language_pick.add_item("Português (Brasil)")
	language_pick.disabled = true
	language_pick.tooltip_text = "Outros idiomas virão depois."
	_line(box, "Idioma", language_pick)
	Kit.spacer(box, 8)
	back_button = Kit.big_button("Voltar", box, close)
	_refresh_labels()


func _line(parent: Control, caption: String, control: Control) -> void:
	var row := Kit.row(parent)
	var label := Label.new()
	label.text = caption
	label.custom_minimum_size.x = 210
	label.add_theme_font_size_override("font_size", 17)
	row.add_child(label)
	control.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(control)


## Interruptor que diz o próprio estado ("Ligada"/"Desligada").
func _check(on: bool, action: Callable) -> CheckButton:
	var check := CheckButton.new()
	check.text = "Ligada" if on else "Desligada"
	check.button_pressed = on
	check.focus_mode = Control.FOCUS_ALL
	check.toggled.connect(action)
	check.toggled.connect(func(now: bool): check.text = "Ligada" if now else "Desligada")
	return check


func _nearest_scale(value: float) -> int:
	var best := 1
	for i in GameSettings.UI_SCALES.size():
		if absf(GameSettings.UI_SCALES[i] - value) < absf(GameSettings.UI_SCALES[best] - value):
			best = i
	return best


func _changed() -> void:
	settings.apply(get_tree())
	settings.save()
	_refresh_labels()


func _refresh_labels() -> void:
	volume_label.text = "%d%%" % int(round(settings.volume * 100))


func focus_default() -> void:
	scale_pick.grab_focus()


func close() -> void:
	closed.emit()


func _unhandled_input(event: InputEvent) -> void:
	if visible and event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		close()
