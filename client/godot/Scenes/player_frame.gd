extends Control
## #222: a moldura de jogo do modo jogador (3ª/1ª pessoa), no padrão de RPG: o cartão do corpo no canto de baixo (nome, o que faz,
## saúde, fome, sede e sono, como o motor diz), a hora no topo, a dica do que o E alcança sob a mira, a faixa de teclas no rodapé e
## o menu do E (numerado, 1–9 escolhe, Esc fecha). Só desenha: o que vale e o que acontece é do motor (via player_hud).

signal chosen(entry: Dictionary)

const BARS := [["health", "Saúde", Color("5fb36b")], ["hunger", "Fome", Color("e0a43a")], ["thirst", "Sede", Color("4aa3df")],
	["sleep", "Sono", Color("9b7fd1")]]
const HINTS := "WASD andar   Shift correr   Espaço pular   E interagir   Clique bater   C câmera   + − zoom   F1 ajuda   Esc menu"
const MENU_MAX := 8 ## linhas no menu do E (o resto fica de fora: quem está mais perto primeiro)
const PEOPLE_MAX := 3 ## pessoas no menu do E (numa multidão, só as mais próximas)

var card: PanelContainer
var name_label: Label
var action_label: Label
var purse_label: Label ## #222: dinheiro e o que leva, da ficha do motor
var clock_label: Label
var prompt: Label
var hints: Label
var bars: Dictionary = {} ## "health" -> ProgressBar
var menu_panel: PanelContainer
var menu_box: VBoxContainer
var entries: Array = []


func _init() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	card = PanelContainer.new()
	card.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.72), 10))
	card.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	card.offset_left = 16
	card.offset_bottom = -44
	card.offset_top = -44
	card.grow_vertical = Control.GROW_DIRECTION_BEGIN
	card.custom_minimum_size.x = 260
	card.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(card)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 4)
	card.add_child(box)
	name_label = _label(20, Color.WHITE)
	box.add_child(name_label)
	action_label = _label(14, Color(1, 1, 1, 0.7))
	box.add_child(action_label)
	purse_label = _label(15, Color("ffe9a6"))
	purse_label.visible = false
	box.add_child(purse_label)
	for spec in BARS:
		var row := HBoxContainer.new()
		var text := _label(13, Color(1, 1, 1, 0.8))
		text.text = spec[1]
		text.custom_minimum_size.x = 52
		row.add_child(text)
		var bar := ProgressBar.new()
		bar.show_percentage = false
		bar.max_value = 100.0
		bar.custom_minimum_size = Vector2(190, 10)
		bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		bar.add_theme_stylebox_override("background", _style(Color(1, 1, 1, 0.12), 4))
		bar.add_theme_stylebox_override("fill", _style(spec[2], 4))
		bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_child(bar)
		box.add_child(row)
		bars[spec[0]] = bar
	clock_label = _label(17, Color("ffe9a6"))
	clock_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	clock_label.offset_top = 14
	clock_label.grow_horizontal = Control.GROW_DIRECTION_BOTH
	clock_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	clock_label.add_theme_constant_override("outline_size", 6)
	clock_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	add_child(clock_label)
	prompt = _label(17, Color.WHITE)
	prompt.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	prompt.offset_top = 34
	prompt.grow_horizontal = Control.GROW_DIRECTION_BOTH
	prompt.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	prompt.add_theme_constant_override("outline_size", 6)
	prompt.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
	prompt.visible = false
	add_child(prompt)
	hints = _label(13, Color(1, 1, 1, 0.75))
	hints.text = HINTS
	hints.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	hints.offset_top = -30
	hints.offset_bottom = -10
	hints.grow_horizontal = Control.GROW_DIRECTION_BOTH
	hints.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hints.add_theme_constant_override("outline_size", 5)
	hints.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
	add_child(hints)
	menu_panel = PanelContainer.new()
	menu_panel.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.85), 10))
	menu_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	menu_panel.offset_left = 60
	menu_panel.offset_top = -40
	menu_panel.custom_minimum_size.x = 300
	menu_panel.visible = false
	add_child(menu_panel)
	menu_box = VBoxContainer.new()
	menu_box.add_theme_constant_override("separation", 2)
	menu_panel.add_child(menu_box)


static func _style(color: Color, radius: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(radius)
	style.content_margin_left = 12
	style.content_margin_right = 12
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	return style


static func _label(size: int, color: Color) -> Label:
	var label := Label.new()
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


## O corpo como o motor o diz (ficha `/npcs/{id}`): nome, o que faz e as quatro barras.
func set_body(name: String, action: String, needs: Dictionary) -> void:
	name_label.text = name
	action_label.text = action
	action_label.visible = action != ""
	for key in bars:
		if needs.has(key) and needs[key] != null:
			var value := float(needs[key])
			bars[key].value = value
			bars[key].modulate = Color.WHITE if value >= 25.0 else Color(1.0, 0.55, 0.55)


## #222: "Dinheiro 120 · Leva 3 Comida" (o que o motor diz; sem campo, nada).
func set_purse(money: Variant, carrying: Variant, name_of: Callable) -> void:
	var parts: Array[String] = []
	if money != null:
		parts.append("Dinheiro %d" % int(money))
	if carrying is Dictionary and int(carrying.get("quantity", 0)) > 0:
		parts.append("Leva %d %s" % [int(carrying.quantity), String(name_of.call(int(carrying.resourceId))) if name_of.is_valid() else ""])
	purse_label.text = " · ".join(parts)
	purse_label.visible = not parts.is_empty()


func set_clock(text: String) -> void:
	clock_label.text = text


## A dica sob a mira: "E  Comprar comida (5)", ou nada.
func set_prompt(text: String) -> void:
	prompt.text = ("E   " + text) if text != "" else ""
	prompt.visible = text != "" and not menu_panel.visible


## As linhas do menu do E no modo de jogo: sem "Bater" (é o clique), as coisas primeiro e só as pessoas mais perto de `at`.
static func game_entries(all: Array, offers: Array, at: Vector2) -> Array:
	var near := {}
	for offer in offers:
		if String(offer.get("kind", "")) == "person":
			near[int(offer.id)] = at.distance_squared_to(Vector2(float(offer.get("x", 0)), float(offer.get("y", 0)))) if at != Vector2.INF else 0.0
	var people: Array = near.keys()
	people.sort_custom(func(a, b): return near[a] < near[b] or (near[a] == near[b] and a < b))
	people = people.slice(0, PEOPLE_MAX)
	var things: Array = []
	var persons: Array = []
	for entry in all:
		if String(entry.verb) == "strike":
			continue
		if String(entry.kind) == "person":
			if int(entry.id) in people:
				persons.append(entry)
		else:
			things.append(entry)
	persons.sort_custom(func(a, b): return people.find(int(a.id)) < people.find(int(b.id)))
	return (things + persons).slice(0, MENU_MAX)


func show_menu(list: Array) -> void:
	entries = list
	for child in menu_box.get_children():
		child.queue_free()
	var title := _label(15, Color("ffe9a6"))
	title.text = "O que fazer" if not list.is_empty() else "Nada ao alcance"
	menu_box.add_child(title)
	for i in list.size():
		var entry: Dictionary = list[i]
		var button := Button.new()
		button.text = "%d   %s" % [i + 1, entry.text]
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.focus_mode = Control.FOCUS_NONE
		button.pressed.connect(func(): chosen.emit(entry))
		menu_box.add_child(button)
	var foot := _label(12, Color(1, 1, 1, 0.55))
	foot.text = "1–%d escolhe · E ou Esc fecha" % list.size() if not list.is_empty() else "E ou Esc fecha"
	menu_box.add_child(foot)
	menu_panel.visible = true
	prompt.visible = false


func close_menu() -> void:
	menu_panel.visible = false
	prompt.visible = prompt.text != ""


func menu_open() -> bool:
	return menu_panel.visible


## Tecla 1–9 com o menu aberto: a linha dessa posição (ou {} se não há).
func entry_for_key(keycode: int) -> Dictionary:
	var index := keycode - KEY_1
	return entries[index] if index >= 0 and index < entries.size() else {}
