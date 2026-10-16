extends RefCounted
## Tema único da interface: painéis escuros translúcidos, cantos arredondados, botões legíveis.


static func _box(color: Color, radius: int = 8, border: Color = Color(1, 1, 1, 0.08)) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(radius)
	style.set_border_width_all(1)
	style.border_color = border
	style.content_margin_left = 10
	style.content_margin_right = 10
	style.content_margin_top = 6
	style.content_margin_bottom = 6
	return style


static func build() -> Theme:
	var theme := Theme.new()
	theme.default_font_size = 15
	theme.set_stylebox("panel", "PanelContainer", _box(Color(0.07, 0.09, 0.13, 0.82), 10))
	theme.set_stylebox("panel", "Panel", _box(Color(0.07, 0.09, 0.13, 0.82), 10))
	theme.set_color("font_color", "Label", Color("eef2f7"))
	theme.set_color("font_outline_color", "Label", Color(0, 0, 0, 0.5))
	theme.set_constant("outline_size", "Label", 2)
	for state in ["normal", "hover", "pressed", "focus", "disabled"]:
		var color := Color(0.18, 0.22, 0.3, 0.95)
		if state == "hover":
			color = Color(0.26, 0.32, 0.44, 1.0)
		elif state == "pressed":
			color = Color(0.33, 0.5, 0.78, 1.0)
		theme.set_stylebox(state, "Button", _box(color, 7))
	theme.set_color("font_color", "Button", Color("f4f7fb"))
	theme.set_stylebox("normal", "LineEdit", _box(Color(0.12, 0.15, 0.2, 0.95), 6))
	theme.set_stylebox("panel", "ItemList", _box(Color(0.1, 0.12, 0.17, 0.6), 6))
	theme.set_stylebox("selected", "ItemList", _box(Color(0.3, 0.5, 0.8, 0.9), 4))
	theme.set_stylebox("selected_focus", "ItemList", _box(Color(0.3, 0.5, 0.8, 0.9), 4))
	theme.set_stylebox("background", "ProgressBar", _box(Color(0.16, 0.19, 0.25, 1.0), 4, Color(0, 0, 0, 0)))
	theme.set_stylebox("fill", "ProgressBar", _box(Color("5fb36b"), 4, Color(0, 0, 0, 0)))
	return theme
