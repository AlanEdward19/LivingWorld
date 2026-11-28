extends Control
## Tela de título: nome do jogo sobre o mundo girando ao fundo e poucos botões grandes. "Continuar" só aparece
## quando há um mundo para voltar, já com nome, ano, quantas pessoas vivem nele e o lugar de onde veio (dados de
## `GET /worlds`, AD-175).

signal continue_pressed()
signal new_world_pressed()
signal my_worlds_pressed()
signal options_pressed()
signal quick_start_pressed(period_id: String) ## #252: período que o servidor oferece num botão (cria e abre o mundo direto)
signal quit_pressed()

const Kit := preload("res://Scenes/menu/menu_kit.gd")

var continue_button: Button
var new_world_button: Button
var my_worlds_button: Button
var options_button: Button
var quit_button: Button
var quick_buttons: Array[Button] = [] ## um por período `card.quickStart` (ex.: "Sandbox: casal")
var _box: VBoxContainer
var status_label: Label
var world_info: Dictionary = {} ## {name, year, population, period}; vazio = sem mundo para continuar


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var box := Kit.screen(self, 560, false)
	_box = box
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


## #252: os períodos de início rápido que o servidor declara (`card.quickStart`): um botão cada, logo abaixo de "Novo mundo".
## `periods` é a lista de `GET /periods`; sem nenhum, nenhum botão. Nada aqui sabe o nome de um período.
func set_quick_starts(periods: Array) -> void:
	for button in quick_buttons:
		button.queue_free()
	quick_buttons.clear()
	var at := new_world_button.get_index() + 1
	for item in periods:
		var card: Dictionary = item.get("card", {}) if item is Dictionary else {}
		if not bool(card.get("quickStart", false)):
			continue
		var id := String(item.periodId)
		var button := Kit.big_button(String(card.get("name", id)), _box, func(): quick_start_pressed.emit(id))
		button.name = "quick_" + id
		_box.move_child(button, at)
		at += 1
		quick_buttons.append(button)
	focus_default()


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
		if String(info.get("period", "")) != "":
			details.append(String(info.period))
		var world_name := String(info.get("name", ""))
		continue_button.text = continue_text(world_name, " · ".join(details))
	focus_default()


## #260: "Continuar: Nome\nano 3 · 2 pessoas"; um mundo sem nome de verdade ("Mundo sem nome") não é anunciado como se fosse do jogador.
static func continue_text(world_name: String, details: String) -> String:
	if world_name.strip_edges() == "" or world_name.strip_edges().to_lower() == "mundo sem nome":
		return "Continuar\n%s" % details if details != "" else "Continuar"
	return "Continuar: %s\n%s" % [world_name, details]


func set_status(text: String, ok: bool) -> void:
	status_label.text = text
	status_label.add_theme_color_override("font_color", Kit.OK if ok else Kit.SOFT)


func focus_default() -> void:
	if not is_inside_tree():
		return
	var chain: Array = [continue_button, new_world_button]
	chain.append_array(quick_buttons)
	chain.append_array([my_worlds_button, options_button, quit_button])
	Kit.chain_focus(chain)
	(continue_button if continue_button.visible else new_world_button).grab_focus()
