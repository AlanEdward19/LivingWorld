extends RefCounted
## Peças do menu de videogame, todas por código: botões grandes, cartões, títulos, telas cheias, foco e transições.
## Uma só aparência (cores e espaçamentos daqui e do UiTheme); nenhum texto em posição fixa: tudo em contêineres.

const GOLD := Color("ffd27a")
const SOFT := Color(1, 1, 1, 0.72)
const ERROR := Color("ff9b8a")
const OK := Color("9fe0a8")
const PANEL := Color(0.06, 0.08, 0.12, 0.86)
const FADE := 0.18 ## segundos das transições entre telas


## Tela cheia que centraliza uma coluna de largura máxima `width`; devolve a coluna (VBox).
static func screen(parent: Control, width: float, panel: bool = true) -> VBoxContainer:
	var root := Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	parent.add_child(root)
	var margin := MarginContainer.new()
	margin.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 24)
	root.add_child(margin)
	var center := CenterContainer.new()
	margin.add_child(center)
	var box := VBoxContainer.new()
	box.custom_minimum_size.x = width
	box.add_theme_constant_override("separation", 14)
	if panel:
		var frame := PanelContainer.new()
		frame.add_theme_stylebox_override("panel", _panel_style())
		center.add_child(frame)
		frame.add_child(box)
	else:
		center.add_child(box)
	return box


static func _panel_style() -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = PANEL
	style.set_corner_radius_all(16)
	style.set_border_width_all(1)
	style.border_color = Color(1, 1, 1, 0.1)
	for side in [SIDE_LEFT, SIDE_RIGHT, SIDE_TOP, SIDE_BOTTOM]:
		style.set_content_margin(side, 28)
	style.shadow_color = Color(0, 0, 0, 0.45)
	style.shadow_size = 18
	return style


static func title(text: String, size: int, parent: Control, color: Color = Color.WHITE) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	parent.add_child(label)
	return label


static func text(content: String, parent: Control, size: int = 16, color: Color = SOFT) -> Label:
	var label := Label.new()
	label.text = content
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	parent.add_child(label)
	return label


## Botão grande do menu (Enter/A aperta; foco dourado vem do tema).
static func big_button(label: String, parent: Control, action: Callable, height: float = 56) -> Button:
	var button := Button.new()
	button.text = label
	button.custom_minimum_size = Vector2(0, height)
	button.add_theme_font_size_override("font_size", 21)
	button.focus_mode = Control.FOCUS_ALL
	button.pressed.connect(action)
	parent.add_child(button)
	return button


static func button(label: String, parent: Control, action: Callable) -> Button:
	var b := Button.new()
	b.text = label
	b.custom_minimum_size = Vector2(0, 40)
	b.add_theme_font_size_override("font_size", 17)
	b.focus_mode = Control.FOCUS_ALL
	b.pressed.connect(action)
	parent.add_child(b)
	return b


static func row(parent: Control, separation: int = 12) -> HBoxContainer:
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", separation)
	parent.add_child(h)
	return h


static func spacer(parent: Control, height: float) -> void:
	var gap := Control.new()
	gap.custom_minimum_size.y = height
	parent.add_child(gap)


## Cartão escolhível (botão alto com imagem, título e frase). `toggle` marca o escolhido.
static func card(heading: String, line: String, picture: Texture2D, parent: Control, action: Callable) -> Button:
	var b := Button.new()
	b.toggle_mode = true
	b.focus_mode = Control.FOCUS_ALL
	b.custom_minimum_size = Vector2(220, 250)
	b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	b.pressed.connect(action)
	b.tooltip_text = line
	parent.add_child(b)
	var box := VBoxContainer.new()
	box.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT, Control.PRESET_MODE_MINSIZE, 12)
	box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	box.add_theme_constant_override("separation", 8)
	b.add_child(box)
	if picture != null:
		var image := TextureRect.new()
		image.texture = picture
		image.custom_minimum_size = Vector2(0, 110)
		image.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		image.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		image.mouse_filter = Control.MOUSE_FILTER_IGNORE
		box.add_child(image)
	var head := title(heading, 20, box, GOLD)
	head.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var body := text(line, box, 14)
	body.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	body.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return b


## Primeiro controle focável dentro de `node` (para o teclado/controle começar em algum lugar).
static func first_focusable(node: Node) -> Control:
	for child in node.get_children():
		if child is Control and (child as Control).is_visible_in_tree():
			if child is BaseButton and not (child as BaseButton).disabled and (child as Control).focus_mode != Control.FOCUS_NONE:
				return child
			if child is LineEdit or child is Range:
				return child
			var inner := first_focusable(child)
			if inner != null:
				return inner
	return null


## Setas e Tab/controle andam pela lista na ordem dada (e dão a volta), só entre os visíveis.
static func chain_focus(controls: Array) -> void:
	var shown: Array = controls.filter(func(c): return c != null and c.visible)
	for i in shown.size():
		var here: Control = shown[i]
		var after: Control = shown[(i + 1) % shown.size()]
		var before: Control = shown[(i - 1 + shown.size()) % shown.size()]
		here.focus_neighbor_bottom = here.get_path_to(after)
		here.focus_next = here.get_path_to(after)
		here.focus_neighbor_top = here.get_path_to(before)
		here.focus_previous = here.get_path_to(before)


static func fade_in(node: CanvasItem) -> void:
	node.modulate.a = 0.0
	if not node.is_inside_tree():
		node.modulate.a = 1.0
		return
	node.create_tween().tween_property(node, "modulate:a", 1.0, FADE)


## Encontra, em profundidade, o primeiro Button cujo texto contém `needle` (testes e atalhos).
static func find_button(node: Node, needle: String) -> Button:
	for child in node.get_children():
		if child is Button and needle in (child as Button).text:
			return child
		var inner := find_button(child, needle)
		if inner != null:
			return inner
	return null


## Todo texto visível dentro de `node`, numa linha (testes).
static func all_text(node: Node) -> String:
	var parts: PackedStringArray = []
	for child in node.get_children():
		if child is Label:
			parts.append((child as Label).text)
		elif child is Button:
			parts.append((child as Button).text)
		parts.append(all_text(child))
	return " ".join(parts)
