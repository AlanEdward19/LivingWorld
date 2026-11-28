extends PanelContainer
## #263: "Carreira" (tecla O no modo jogador): cargo, salário, horário, próximo turno, desempenho do último turno, o que falta para subir, as contas abertas da casa e as vagas
## que a última busca de emprego achou. Só mostra o que o motor diz (`career` da ficha do corpo): nada aqui é escrito à mão, calculado nem inventado pelo cliente.


var box: VBoxContainer
var career: Dictionary = {}


func _init() -> void:
	add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.92), 10))
	set_anchors_and_offsets_preset(Control.PRESET_CENTER_RIGHT)
	offset_left = -430
	offset_right = -24
	grow_horizontal = Control.GROW_DIRECTION_BEGIN
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
	label.custom_minimum_size.x = 372
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


## Os dias da semana do emprego em texto ("Seg–Sex", "Seg, Qua"): sequência vira faixa. Os nomes dos dias são do cenário (`dayNames`); sem eles, "dia 1".
static func days_text(days: Variant, names: Variant = null) -> String:
	if not days is Array or days.is_empty():
		return ""
	var sorted: Array = days.duplicate()
	sorted.sort()
	var parts: Array[String] = []
	var i := 0
	while i < sorted.size():
		var j := i
		while j + 1 < sorted.size() and int(sorted[j + 1]) == int(sorted[j]) + 1:
			j += 1
		var first := _day(sorted[i], names)
		var last := _day(sorted[j], names)
		parts.append(first if i == j else ("%s–%s" % [first, last] if j > i + 1 else "%s, %s" % [first, last]))
		i = j + 1
	return ", ".join(parts)


static func _day(day: Variant, names: Variant) -> String:
	return String(names[int(day)]) if names is Array and int(day) < names.size() else "dia %d" % (int(day) + 1)


## "09:00 às 17:00" a partir da hora de começo e das horas do turno.
static func hours_text(job: Dictionary) -> String:
	var start := int(job.get("startHour", 0))
	return "%02d:00 às %02d:00" % [start, (start + int(job.get("hours", 0))) % 24]


## As linhas do cartão do corpo (uma ou duas): o cargo e o próximo turno (ou "No trabalho até ..."), e a conta mais urgente. Vazio sem emprego nem conta.
static func card_lines(view: Variant) -> Array[String]:
	var lines: Array[String] = []
	if not view is Dictionary:
		return lines
	var job = view.get("job")
	if job is Dictionary:
		if bool(job.get("away", false)):
			lines.append("%s · no trabalho até %s" % [String(job.title), String(job.get("returnText", ""))])
		elif job.get("nextShiftText") != null:
			lines.append("%s · próximo turno %s" % [String(job.title), String(job.nextShiftText)])
		else:
			lines.append("%s · sem turno marcado" % String(job.title))
	var bills = view.get("bills")
	if bills is Array and not bills.is_empty():
		var bill: Dictionary = bills[0]
		lines.append("%s%s %d · vence %s" % ["⚠ " if bool(bill.get("late", false)) or bool(bill.get("cut", false)) else "", String(bill.name), int(bill.open) + int(bill.get("fee", 0)), String(bill.get("dueText", ""))])
	return lines


## O texto do desempenho: "Desempenho do último turno: 78%" ou, antes do primeiro, que ainda não houve.
static func score_text(job: Dictionary) -> String:
	var score := int(job.get("score", -1))
	return "Desempenho do último turno: %d%%" % score if score >= 0 else "Ainda sem turno cumprido."


## Quanto falta para subir: "Subindo: 2 de 3 bons turnos para Analista (14/h)"; no topo, que não há mais nível.
static func promotion_text(job: Dictionary) -> String:
	if job.get("nextTitle") == null:
		return "No topo da carreira."
	return "Para %s (%d/h): %d de %d turnos com desempenho de %d%% ou mais." % [String(job.nextTitle), int(job.get("nextWagePerHour", 0)), int(job.get("good", 0)), int(job.get("promoteAfter", 1)), int(round(float(job.get("promoteAt", 0))))]


func set_career(view: Variant) -> void:
	career = view if view is Dictionary else {}
	_rebuild()


func toggle() -> void:
	visible = not visible


func is_open() -> bool:
	return visible


func _rebuild() -> void:
	for child in box.get_children():
		box.remove_child(child)
		child.queue_free()
	box.add_child(_label("Carreira", 18, Color("ffe9a6")))
	var job = career.get("job")
	if job is Dictionary:
		box.add_child(_label("%s · %s" % [String(job.title), String(job.careerName)], 15, Color.WHITE))
		box.add_child(_label("Salário: %d por hora · Nível %d de %d" % [int(job.wagePerHour), int(job.levelIndex) + 1, int(job.levels)], 14, Color(1, 1, 1, 0.85)))
		box.add_child(_label("Horário: %s · %s" % [hours_text(job), days_text(job.get("days"), career.get("dayNames"))], 14, Color(1, 1, 1, 0.85)))
		if bool(job.get("away", false)):
			box.add_child(_label("No trabalho até %s" % String(job.get("returnText", "")), 14, Color("9ad0ff")))
		elif job.get("nextShiftText") != null:
			box.add_child(_label("Próximo turno: %s" % String(job.nextShiftText), 14, Color("9ad0ff")))
		box.add_child(_label(score_text(job), 14, Color("a8e6a1") if int(job.get("score", -1)) >= int(round(float(job.get("promoteAt", 100)))) else Color.WHITE))
		box.add_child(_label(promotion_text(job), 13, Color(1, 1, 1, 0.7)))
		if int(job.get("absences", 0)) > 0:
			box.add_child(_label("Faltas seguidas: %d de %d (a última demite)" % [int(job.absences), int(job.get("absencesToFire", 3))], 13, Color("ff9a8a")))
		if job.get("skillName") != null:
			box.add_child(_label("%s: %d" % [String(job.skillName), int(round(float(job.get("skill", 0))))], 13, Color(1, 1, 1, 0.7)))
		box.add_child(_label("Turnos: %d · Ganhos: %d" % [int(job.get("shifts", 0)), int(job.get("earned", 0))], 13, Color(1, 1, 1, 0.7)))
		if bool(job.get("canGo", false)):
			box.add_child(_label("E: Ir ao trabalho", 13, Color("ffd27a")))
	else:
		box.add_child(_label("Sem emprego. Procure vagas no jornal da sala e candidate-se (E).", 14, Color(1, 1, 1, 0.8)))
	var listings = career.get("listings")
	if listings is Array and not listings.is_empty():
		box.add_child(_label("Vagas abertas", 15, Color("ffe9a6")))
		for vacancy in listings:
			box.add_child(_label("%s · %s · %d/h · %02d:00, %dh · chance %d%%" % [String(vacancy.title), String(vacancy.careerName), int(vacancy.wagePerHour), int(vacancy.startHour), int(vacancy.hours), int(round(float(vacancy.chance) * 100.0))], 13, Color.WHITE))
	var bills = career.get("bills")
	if bills is Array and not bills.is_empty():
		box.add_child(_label("Contas da casa (E em casa: Contas)", 15, Color("ffe9a6")))
		for bill in bills:
			var tail := " · cortada" if bool(bill.get("cut", false)) else (" · atrasada" if bool(bill.get("late", false)) else "")
			box.add_child(_label("%s: %d%s · vence %s" % [String(bill.name), int(bill.open) + int(bill.get("fee", 0)), tail, String(bill.get("dueText", ""))], 13, Color("ff9a8a") if tail != "" else Color.WHITE))
	box.add_child(_label("O fecha", 12, Color(1, 1, 1, 0.55)))
