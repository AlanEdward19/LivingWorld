extends Control
## Tela de título: nome do jogo sobre o mundo girando ao fundo e poucos botões grandes. "Continuar" só aparece
## quando há um mundo para voltar, já com nome, ano e quantas pessoas vivem nele.

signal continue_pressed()
signal new_world_pressed()
signal my_worlds_pressed()
signal options_pressed()
signal quit_pressed()

const Kit := preload("res://Scenes/menu/menu_kit.gd")

var continue_button: Button
var new_world_button: Button
var my_worlds_button: Button
var options_button: Button
var quit_button: Button
var status_label: Label
var world_info: Dictionary = {} ## {name, year, population}; vazio = sem mundo para continuar


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var box := Kit.screen(self, 560, false)
	var logo := Kit.title("LIVING WORLD", 64, box, Color("fff4d6"))
	logo.autowrap_mode = TextServer.AUTOWRAP_OFF
	logo.add_theme_constant_override("outline_size", 10)
	logo.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.55))
	Kit.title("Um mundo que continua vivo, mesmo quando você só observa", 18, box, Kit.SOFT)
	Kit.spacer(box, 28)
	continue_button = Kit.big_button("Continuar", box, func(): continue_pressed.emit(), 72)
	continue_button.visible = false
	new_world_button = Kit.big_button("Novo mundo", box, func(): new_world_pressed.emit())
	my_worlds_button = Kit.big_button("Meus mundos", box, func(): my_worlds_pressed.emit())
	options_button = Kit.big_button("Opções", box, func(): options_pressed.emit())
	quit_button = Kit.big_button("Sair", box, func(): quit_pressed.emit())
	Kit.spacer(box, 12)
	status_label = Kit.text("Procurando o mundo…", box, 15)
	status_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER


## `info` vazio esconde o Continuar; com mundo, o botão diz qual é.
func set_continue(info: Dictionary) -> void:
	world_info = info
	continue_button.visible = not info.is_empty()
	if not info.is_empty():
		var details: PackedStringArray = []
		if info.has("year"):
			details.append("ano %d" % int(info.year))
		if info.has("population"):
			details.append("%d %s" % [int(info.population), "pessoa" if int(info.population) == 1 else "pessoas"])
		continue_button.text = "Continuar: %s\n%s" % [String(info.get("name", "seu mundo")), " · ".join(details)]
	focus_default()


func set_status(text: String, ok: bool) -> void:
	status_label.text = text
	status_label.add_theme_color_override("font_color", Kit.OK if ok else Kit.SOFT)


func focus_default() -> void:
	if not is_inside_tree():
		return
	Kit.chain_focus([continue_button, new_world_button, my_worlds_button, options_button, quit_button])
	(continue_button if continue_button.visible else new_world_button).grab_focus()
