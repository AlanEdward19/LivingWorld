extends PanelContainer
## #235: "Metas e diário" (tecla J no modo jogador). Só mostra o que o motor diz: as metas nascem do estado do corpo (fome, família,
## trabalho, dívida...), cada uma com motivo, urgência e o próximo passo; as habilidades com nível e o que falta; o diário com o que
## aconteceu enquanto o jogador controla e os marcos da vida. Nada aqui é escrito à mão nem inventado pelo cliente.

const URGENT := 70 ## fallback da linha de urgência; o motor manda a dele em `urgencyLine`
const DIARY_SHOWN := 14 ## linhas do diário na tela (o motor manda até 60; o resto fica de fora, as mais novas primeiro)
const PRACTICE_ICON := "▲"

var box: VBoxContainer
var goals: Array = []
var skills: Array = []
var entries: Array = []
var urgent_line := URGENT


func _init() -> void:
	add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.9), 10))
	set_anchors_and_offsets_preset(Control.PRESET_CENTER_LEFT)
	offset_left = 24
	offset_right = 420
	grow_vertical = Control.GROW_DIRECTION_BOTH
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	box = VBoxContainer.new()
	box.add_theme_constant_override("separation", 3)
	add_child(box)
	_rebuild()


static func _style(color: Color, radius: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(radius)
	style.content_margin_left = 12
	style.content_margin_right = 12
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	return style


static func _label(text: String, size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.custom_minimum_size.x = 360
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


## "Pegar comida na despensa" / "Seu posto de trabalho · a 6 células" (o passo do motor; sem passo, nada).
static func step_text(step: Variant) -> String:
	if not step is Dictionary or String(step.get("command", "none")) == "none":
		return ""
	var label := String(step.get("label", ""))
	var distance := int(step.get("distance", 0))
	if bool(step.get("here", false)) or distance <= 0:
		return label
	return "%s · a %d %s" % [label, distance, "célula" if distance == 1 else "células"]


## A meta em uma linha: o motivo e, entre parênteses, o próximo passo.
static func goal_line(goal: Dictionary) -> String:
	var step := step_text(goal.get("step"))
	return "%s%s" % [String(goal.get("reason", "")), " → " + step if step != "" else ""]


## A linha discreta do HUD: a meta mais urgente; sem meta, nada.
static func top_line(body: Variant) -> String:
	if not body is Dictionary or not body.get("goals") is Array or body.goals.is_empty():
		return ""
	return goal_line(body.goals[0])


static func is_urgent(goal: Dictionary, line: int = URGENT) -> bool:
	return int(goal.get("urgency", 0)) > line


## "Ano 2 · mês 3 · dia 4 · 14h" a partir dos campos do diário do motor.
static func when_text(entry: Dictionary) -> String:
	return "Ano %d · mês %d · dia %d · %dh" % [int(entry.get("year", 1)), int(entry.get("month", 1)), int(entry.get("day", 1)), int(entry.get("hour", 0))]


## "Nível 2 · faltam 5 pontos" (ou "no teto"), com ▲ quando ela sobe agora.
static func skill_text(skill: Dictionary) -> String:
	var name := String(skill.get("name", ""))
	var to_next := float(skill.get("toNext", 0.0))
	var tail := "no nível máximo" if to_next <= 0.0 else "faltam %s pontos" % str(snappedf(to_next, 0.1))
	return "%s%s  nível %d/%d · %s" % [(PRACTICE_ICON + " ") if bool(skill.get("practicing", false)) else "", name if name != "" else "Ofício",
		int(skill.get("level", 0)), int(skill.get("maxLevel", 0)), tail]


static func entry_text(entry: Dictionary) -> String:
	return "%s%s" % ["★ " if bool(entry.get("milestone", false)) else "", String(entry.get("text", ""))]


func set_goals(body: Dictionary) -> void:
	goals = body.get("goals", []) if body.get("goals") is Array else []
	skills = body.get("skills", []) if body.get("skills") is Array else []
	urgent_line = int(body.get("urgencyLine", URGENT))
	_rebuild()


func set_diary(body: Dictionary) -> void:
	entries = body.get("entries", []) if body.get("entries") is Array else []
	_rebuild()


func _rebuild() -> void:
	for child in box.get_children():
		box.remove_child(child)
		child.queue_free()
	box.add_child(_label("Metas e diário", 18, Color("ffe9a6")))
	box.add_child(_label("Metas", 14, Color(1, 1, 1, 0.6)))
	if goals.is_empty():
		box.add_child(_label("Nada urgente por enquanto.", 14, Color(1, 1, 1, 0.8)))
	for goal in goals:
		var urgent := is_urgent(goal, urgent_line)
		box.add_child(_label(("! " if urgent else "• ") + goal_line(goal), 14, Color("ff9a8a") if urgent else Color.WHITE))
	if not skills.is_empty():
		box.add_child(_label("Habilidades", 14, Color(1, 1, 1, 0.6)))
		for skill in skills:
			box.add_child(_label(skill_text(skill), 14, Color.WHITE))
			var bar := ProgressBar.new()
			bar.show_percentage = false
			bar.max_value = 1.0
			bar.value = float(skill.get("fraction", 0.0))
			bar.custom_minimum_size = Vector2(240, 6)
			bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
			box.add_child(bar)
	box.add_child(_label("Diário", 14, Color(1, 1, 1, 0.6)))
	if entries.is_empty():
		box.add_child(_label("Ainda sem registros.", 14, Color(1, 1, 1, 0.8)))
	for entry in entries.slice(0, DIARY_SHOWN):
		box.add_child(_label("%s  %s" % [when_text(entry), entry_text(entry)], 13,
			Color("ffe9a6") if bool(entry.get("milestone", false)) else Color(1, 1, 1, 0.85 if bool(entry.get("whileControlled", false)) else 0.55)))
	box.add_child(_label("J fecha", 12, Color(1, 1, 1, 0.55)))


func toggle() -> void:
	visible = not visible


func is_open() -> bool:
	return visible
