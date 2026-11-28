extends Control
## #222: a moldura de jogo do modo jogador (3ª/1ª pessoa), no padrão de RPG: o cartão do corpo no canto de baixo (nome, o que faz,
## saúde, fome, sede e sono, como o motor diz), a hora no topo, a dica do que o E alcança sob a mira, a faixa de teclas no rodapé e
## o menu do E (numerado, 1–9 escolhe, Esc fecha). Só desenha: o que vale e o que acontece é do motor (via player_hud).

signal chosen(entry: Dictionary)
signal speed_chosen(speed: int) ## #260: o jogador pediu pausa (0), 1×, 2× ou 3× (botão ou tecla 0–3); o motor valida

const BARS := [["health", "Saúde", Color("5fb36b")], ["hunger", "Fome", Color("e0a43a")], ["thirst", "Sede", Color("4aa3df")],
	["sleep", "Sono", Color("9b7fd1")]]
const HINTS := "WASD andar   Shift correr   Espaço pular   E interagir (segure: roda)   Clique bater\nI mochila   J metas   B comprar   C câmera   Q ombro   V paredes   0–5 tempo   + − zoom   F1 ajuda   Esc menu" ## #227: duas linhas (cabe em 1280)
const GoalsPanel := preload("res://Scenes/goals_panel.gd")
const NeedIcon := preload("res://Scenes/need_icon.gd")
const DEFAULT_SPEEDS := [1, 2, 3] ## #260: sem lista do motor (cenário antigo): 1×, 2×, 3× como no The Sims; #261: a lista é do cenário (`speeds`), com 0 = pausa
const BAR_WIDTH := 112.0 ## #260: a barra encolhe para caber o ícone, o número e a seta de tendência na mesma linha
const PULSE_HZ := 1.4 ## a necessidade mais urgente pulsa (ciclos por segundo)
const PlayerGuide := preload("res://Scenes/player_guide.gd")
const RelationPanel := preload("res://Scenes/relation_panel.gd")
const COMBAT_HINTS := "   Botão direito/F bloquear   Ctrl esquivar" ## #234: combate (Clique bate já está nas dicas)
const COOLDOWN_MAX_MS := 2000.0 ## a barra de recarga enche em até isto
const WHEEL_MIN_RADIUS := 96.0 ## px: a roda nunca fecha mais que isto (o centro, a mira, fica limpo)
const WHEEL_AIM_CLEAR := 30.0 ## px de folga em volta da mira (centro da tela) sem botão
const WHEEL_GAP := 8.0 ## px livres entre dois botões
const WHEEL_BUTTON := Vector2(232, 34)
const MENU_MAX := 8 ## #255: linhas por página da roda do E; passando disso a última linha é "Mais ▸" e a roda pagina (nada fica de fora)
const PEOPLE_MAX := 3 ## pessoas no menu do E (numa multidão, só as mais próximas)

var card: PanelContainer
var name_label: Label
var action_label: Label
var mood_row: HBoxContainer ## #262: o rosto e o nome da emoção dominante ("Feliz · Tomando banho"), do motor
var mood_icon: Control
var mood_label: Label
var moments_box: VBoxContainer ## #262: os momentos com causa e tempo restante ("Bem descansado +2 · 3 h")
var _mood: Dictionary = {}
var _moments_key := ""
var switch_label: Label ## #252: "G — passar a ser Rafael" (só com outra pessoa na casa)
var purse_label: Label ## #222: dinheiro e o que leva, da ficha do motor
var clock_label: Label
var speed_box: HBoxContainer ## #260: pausa/1×/2×/3× no canto de cima (só quando o motor deixa: sozinho no mundo)
var speed_buttons: Array[Button] = []
var speed_values: Array = [] ## #261: o valor que cada botão pede ao motor (0 = pausa, depois a lista de velocidades do cenário); as teclas 0–9 pegam daqui
var speed_now := 1
var prompt: Label
var hints: Label
var bars: Dictionary = {} ## "health" -> ProgressBar
var menu_panel: Control ## a roda de ações: um Control no centro da tela; as fatias são Buttons em círculo
var menu_box: Control
var inventory: PanelContainer ## #233: a mochila (tecla I ou Tab)
var inventory_box: VBoxContainer
var goal_label: Label ## #235: a meta mais urgente, numa linha discreta sob o relógio
var goals: PanelContainer ## #235: "Metas e diário" (tecla J)
var relation: PanelContainer ## #259: a relação com a pessoa sob a mira (afeição, confiança, estado, humor, último fato)
var catalog_box: VBoxContainer ## #255
var catalog_bars: Dictionary = {} ## id da necessidade do catálogo -> {bar, name}
var _rows: Dictionary = {} ## #260: id (as de sempre e as do catálogo) -> {row, bar, name, pct, icon, trend}
var _pulse_id := "" ## a necessidade mais urgente agora: a barra pulsa
var _pulse_clock := 0.0
var _builtin_urgent := ""
var need_hint: Label ## "Você está com a bexiga cheia → Vaso" (o alerta é do cenário; o objeto, do catálogo)
var queue_label: Label ## #255: a fila de ações ("1 Tomar banho · 2 Assistir TV"), Shift+1–4 tira
var _queue_size := 0
var entries: Array = []
var reason_label: Label ## #259: o motivo de a linha esmaecida não valer, sob a roda
var _all_entries: Array = [] ## a lista inteira da roda; `entries` é a página mostrada
var _page := 0
var busy_panel: PanelContainer ## #252: a troca de corpo disponível ("" = ninguém para quem trocar): uma linha discreta sob o nome.
func set_switch(name: String) -> void:
	switch_label.visible = name != ""
	switch_label.text = "G — passar a ser %s" % name


## #241: a ação em andamento (barra discreta acima do cartão do jogador)
var busy_label: Label
var busy_bar: ProgressBar
var cooldown: ProgressBar ## #234
var guard_label: Label ## #234
var blocked := false: ## #240: popup aberto: a dica do E some (volta ao fechar, se ainda houver alvo)
	set(value):
		blocked = value
		prompt.visible = prompt.text != "" and not menu_panel.visible and not blocked
var _engine_action := ""
var _doing := "" ## #246: a ação do corpo (uso, interação) como a barra a diz; vence o "Parado" do motor no cartão
var guide: PanelContainer ## #238: o guia da primeira posse
var _goal_body: Dictionary = {}
var _goal_where := "" ## #238: para que lado fica o passo da meta mais urgente ("a 12 m, à frente e à direita")
var _ground := "" ## #238: o chão sob o corpo e o quanto atrasa, dito pelo motor ("Chão pesado −40%")
var _ground_slow := 0
var _motion := "" ## #228: "Andando"/"Correndo" enquanto o próprio corpo se move pela previsão local; vazio: vale a ação do motor


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
	mood_row = HBoxContainer.new()
	mood_row.add_theme_constant_override("separation", 6)
	mood_row.visible = false
	box.add_child(mood_row)
	mood_icon = NeedIcon.new("calm", Color("9fc9a2"))
	mood_icon.set_zoom(1.4)
	mood_row.add_child(mood_icon)
	mood_label = _label(16, Color("9fc9a2"))
	mood_label.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	mood_row.add_child(mood_label)
	moments_box = VBoxContainer.new()
	moments_box.add_theme_constant_override("separation", 1)
	moments_box.visible = false
	box.add_child(moments_box)
	switch_label = _label(17, Color("9ad0ff")) # #260: a troca de pessoa (G) é uma das coisas centrais do sandbox: maior e mais clara
	switch_label.visible = false
	box.add_child(switch_label)
	purse_label = _label(15, Color("ffe9a6"))
	purse_label.visible = false
	box.add_child(purse_label)
	for spec in BARS:
		var made := _make_bar_row(String(spec[0]), String(spec[1]), spec[2], String(NeedIcon.DEFAULT_ICONS.get(spec[0], "")), box)
		bars[spec[0]] = made.bar
		_rows[spec[0]] = made
	catalog_box = VBoxContainer.new() # #255: as barras das necessidades do catálogo do cenário (higiene, diversão, bexiga...), criadas pelo que o motor diz
	catalog_box.add_theme_constant_override("separation", 4)
	box.add_child(catalog_box)
	need_hint = _label(13, Color("ffd27a"))
	need_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	need_hint.custom_minimum_size.x = 244
	need_hint.visible = false
	box.add_child(need_hint)
	queue_label = _label(13, Color("9ad0ff"))
	queue_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	queue_label.custom_minimum_size.x = 244
	queue_label.visible = false
	box.add_child(queue_label)
	relation = RelationPanel.new()
	add_child(relation)
	busy_panel = PanelContainer.new() # #241: "Cortando lenha — 40%" e a barra, acima do cartão; só enquanto o motor diz que o corpo está ocupado
	busy_panel.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.72), 8))
	busy_panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_LEFT)
	busy_panel.offset_left = 16
	busy_panel.grow_vertical = Control.GROW_DIRECTION_BEGIN
	busy_panel.custom_minimum_size.x = 260
	busy_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	busy_panel.visible = false
	add_child(busy_panel)
	var busy_box := VBoxContainer.new()
	busy_box.add_theme_constant_override("separation", 3)
	busy_panel.add_child(busy_box)
	busy_label = _label(14, Color("ffe9a6"))
	busy_box.add_child(busy_label)
	busy_bar = ProgressBar.new()
	busy_bar.show_percentage = false
	busy_bar.max_value = 1.0
	busy_bar.custom_minimum_size = Vector2(236, 6)
	busy_bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	busy_bar.add_theme_stylebox_override("background", _style(Color(1, 1, 1, 0.12), 3))
	busy_bar.add_theme_stylebox_override("fill", _style(Color("e0a43a"), 3))
	busy_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	busy_box.add_child(busy_bar)
	clock_label = _label(17, Color("ffe9a6"))
	clock_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	clock_label.offset_top = 14
	clock_label.grow_horizontal = Control.GROW_DIRECTION_BOTH
	clock_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	clock_label.add_theme_constant_override("outline_size", 6)
	clock_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	add_child(clock_label)
	speed_box = HBoxContainer.new()
	speed_box.set_anchors_and_offsets_preset(Control.PRESET_TOP_RIGHT)
	speed_box.offset_top = 12
	speed_box.offset_right = -16
	speed_box.offset_left = -190
	speed_box.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	speed_box.add_theme_constant_override("separation", 4)
	speed_box.alignment = BoxContainer.ALIGNMENT_END
	speed_box.visible = false
	add_child(speed_box)
	_build_speed_buttons(DEFAULT_SPEEDS)
	goal_label = _label(14, Color(1, 1, 1, 0.65))
	goal_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	goal_label.offset_top = 40
	goal_label.grow_horizontal = Control.GROW_DIRECTION_BOTH
	goal_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	goal_label.add_theme_constant_override("outline_size", 5)
	goal_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.7))
	add_child(goal_label)
	goals = GoalsPanel.new()
	add_child(goals)
	guide = PlayerGuide.new()
	add_child(guide)
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
	hints.text = hint_text()
	hints.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	hints.offset_top = -50
	hints.offset_bottom = -10
	hints.grow_horizontal = Control.GROW_DIRECTION_BOTH
	hints.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hints.add_theme_constant_override("outline_size", 5)
	hints.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
	add_child(hints)
	cooldown = ProgressBar.new() # #234: recarga discreta sob a mira (só enquanto o corpo se recupera do golpe)
	cooldown.show_percentage = false
	cooldown.max_value = 1.0
	cooldown.custom_minimum_size = Vector2(64, 4)
	cooldown.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	cooldown.offset_left = -32
	cooldown.offset_right = 32
	cooldown.offset_top = 26
	cooldown.offset_bottom = 30
	cooldown.add_theme_stylebox_override("background", _style(Color(1, 1, 1, 0.1), 2))
	cooldown.add_theme_stylebox_override("fill", _style(Color(1, 0.8, 0.45, 0.7), 2))
	cooldown.mouse_filter = Control.MOUSE_FILTER_IGNORE
	cooldown.visible = false
	add_child(cooldown)
	guard_label = _label(13, Color("9ad0ff"))
	guard_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	guard_label.offset_top = 34
	guard_label.grow_horizontal = Control.GROW_DIRECTION_BOTH
	guard_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	guard_label.add_theme_constant_override("outline_size", 5)
	guard_label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
	guard_label.visible = false
	add_child(guard_label)
	menu_panel = Control.new()
	menu_panel.set_anchors_and_offsets_preset(Control.PRESET_CENTER)
	menu_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	menu_panel.visible = false
	add_child(menu_panel)
	menu_box = Control.new()
	menu_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	menu_panel.add_child(menu_box)
	inventory = PanelContainer.new()
	inventory.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.88), 10))
	inventory.set_anchors_and_offsets_preset(Control.PRESET_CENTER_RIGHT)
	inventory.offset_right = -24
	inventory.offset_left = -300
	inventory.grow_horizontal = Control.GROW_DIRECTION_BEGIN
	inventory.grow_vertical = Control.GROW_DIRECTION_BOTH
	inventory.visible = false
	add_child(inventory)
	inventory_box = VBoxContainer.new()
	inventory_box.add_theme_constant_override("separation", 4)
	inventory.add_child(inventory_box)
	set_inventory(null, null, Callable())


static func _style(color: Color, radius: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(radius)
	style.content_margin_left = 12
	style.content_margin_right = 12
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	return style


## #227: a dica de teclas em duas linhas (a de combate vai ao fim da primeira): cada uma cabe em 1280 px.
static func hint_text() -> String:
	var lines := HINTS.split("\n")
	return lines[0] + COMBAT_HINTS + "\n" + lines[1]


static func _label(size: int, color: Color) -> Label:
	var label := Label.new()
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


## O corpo como o motor o diz (ficha `/npcs/{id}`): nome, o que faz e as quatro barras.
func set_body(name: String, action: String, needs: Dictionary) -> void:
	name_label.text = name
	_engine_action = action
	_show_action()
	var rates: Variant = needs.get("needRates")
	var lowest := ""
	var lowest_value := 25.0
	for key in bars:
		if needs.has(key) and needs[key] != null:
			var value := float(needs[key])
			bars[key].value = value
			bars[key].modulate = Color.WHITE if value >= 25.0 else Color(1.0, 0.55, 0.55)
			_note_row(key, value, rates.get(key) if rates is Dictionary else null)
			if value < lowest_value and key != "health":
				lowest = key
				lowest_value = value
	_builtin_urgent = lowest
	set_catalog_needs(needs.get("catalogNeeds", []), needs.get("needHint"), rates)
	set_mood(needs.get("mood"))


## #255: as barras do catálogo do cenário (`catalogNeeds` da ficha: id, name, value, urgent, color), uma por necessidade na ordem do motor, com a mais
## urgente destacada (nome com seta e cor de alerta), e a dica `needHint` ("Você está com a bexiga cheia → Vaso"). Sem catálogo, nada aparece.
func set_catalog_needs(list: Variant, hint: Variant, rates: Variant = null) -> void:
	var rows: Array = list if list is Array else []
	var worst := most_urgent(rows)
	_pulse_id = worst if worst != "" else _builtin_urgent
	if _pulse_id == "":
		_pulse_clock = 0.0
	for row in rows:
		var id := String(row.id)
		if not catalog_bars.has(id):
			catalog_bars[id] = _make_catalog_row(row)
		var entry: Dictionary = catalog_bars[id]
		var value := float(row.value)
		entry.bar.value = value
		var urgent := bool(row.get("urgent", false))
		entry.bar.modulate = Color(1.0, 0.55, 0.55) if urgent else Color.WHITE
		entry.name.text = ("▶ " if id == worst else "") + String(row.name)
		_note_row(id, value, rates.get(id) if rates is Dictionary else null)
		entry.name.add_theme_color_override("font_color", Color("ffd27a") if id == worst else Color(1, 1, 1, 0.8))
	for id in catalog_bars.keys():
		if rows.filter(func(r): return String(r.id) == id).is_empty():
			catalog_bars[id].row.queue_free()
			catalog_bars.erase(id)
			_rows.erase(id)
	need_hint.text = hint_line(hint)
	need_hint.visible = need_hint.text != ""


## A necessidade do catálogo mais urgente: entre as que o motor marca `urgent`, a de menor nível ("" se nenhuma pede atenção).
static func most_urgent(rows: Array) -> String:
	var best := ""
	var low := 101.0
	for row in rows:
		if bool(row.get("urgent", false)) and float(row.value) < low:
			low = float(row.value)
			best = String(row.id)
	return best


## "Você está com a bexiga cheia → Vaso": o alerta do cenário e, se a casa tem, o objeto que atende; vazio sem dica.
static func hint_line(hint: Variant) -> String:
	if not hint is Dictionary:
		return ""
	var alert := String(hint.get("alert", ""))
	var thing: Variant = hint.get("object")
	return alert if thing == null or String(thing) == "" else "%s → %s" % [alert, String(thing)]


func _make_catalog_row(row: Dictionary) -> Dictionary:
	var tint := Color(String(row.get("color", "9aa5b1")) if row.get("color") != null else "9aa5b1")
	var made := _make_bar_row(String(row.id), String(row.name), tint, String(row.get("icon", "")) if row.get("icon") != null else "", catalog_box)
	_rows[String(row.id)] = made
	return made


## #260: uma linha de necessidade: ícone, nome, barra, número (%) e seta de tendência. `parent` recebe a linha.
func _make_bar_row(id: String, text_name: String, tint: Color, icon: String, parent: Control) -> Dictionary:
	var line := HBoxContainer.new()
	line.add_theme_constant_override("separation", 4)
	var glyph := NeedIcon.new(icon, tint)
	glyph.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	line.add_child(glyph)
	var text := _label(13, Color(1, 1, 1, 0.8))
	text.custom_minimum_size.x = 52
	text.text = text_name
	line.add_child(text)
	var bar := ProgressBar.new()
	bar.show_percentage = false
	bar.max_value = 100.0
	bar.custom_minimum_size = Vector2(BAR_WIDTH, 10)
	bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	bar.add_theme_stylebox_override("background", _style(Color(1, 1, 1, 0.12), 4))
	bar.add_theme_stylebox_override("fill", _style(tint, 4))
	bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	line.add_child(bar)
	var pct := _label(12, Color(1, 1, 1, 0.85))
	pct.custom_minimum_size.x = 34
	pct.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	line.add_child(pct)
	var arrow := NeedIcon.new("", Color.WHITE, true)
	arrow.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	line.add_child(arrow)
	parent.add_child(line)
	return {"row": line, "bar": bar, "name": text, "pct": pct, "icon": glyph, "trend": arrow, "id": id, "tint": tint}


## #260: o número e a seta de uma linha: `value` 0..100 e `per_hour` o ritmo que o motor diz (nulo = sem seta).
func _note_row(id: String, value: float, per_hour: Variant) -> void:
	if not _rows.has(id):
		return
	var row: Dictionary = _rows[id]
	row.pct.text = "%d%%" % roundi(value)
	var level := NeedIcon.level_for(float(per_hour)) if per_hour != null else 0
	row.trend.visible = per_hour != null
	row.trend.set_trend(level)
	row.trend.tooltip_text = NeedIcon.trend_text(level)
	row.row.tooltip_text = "%s %d%% · %s" % [String(row.name.text).trim_prefix("▶ "), roundi(value), NeedIcon.trend_text(level)]


func _process(delta: float) -> void:
	if _pulse_id == "" or not _rows.has(_pulse_id):
		return
	_pulse_clock += delta
	var wave := 0.5 + 0.5 * sin(_pulse_clock * TAU * PULSE_HZ)
	var bar: ProgressBar = _rows[_pulse_id].bar
	bar.modulate = Color(1.0, 0.55 + 0.4 * wave, 0.55 + 0.4 * wave)


## #255: a fila de ações (`queue` da pose: use, label, object); vazia esconde.
func set_queue(queue: Variant) -> void:
	var list: Array = queue if queue is Array else []
	_queue_size = list.size()
	queue_label.text = queue_text(list)
	queue_label.visible = _queue_size > 0


func queue_size() -> int:
	return _queue_size


static func queue_text(list: Array) -> String:
	if list.is_empty():
		return ""
	var parts: PackedStringArray = []
	for i in list.size():
		parts.append("%d %s" % [i + 1, String(list[i].get("label", ""))])
	return "Fila: " + " · ".join(parts) + "\n(Shift+1–%d tira da fila)" % list.size()


## #241: a ação em andamento ("Cortando lenha — 40%", `fraction` 0..1); texto vazio esconde a barra. Fica logo acima do cartão do corpo.
func set_busy(text: String, fraction: float) -> void:
	busy_panel.visible = text != ""
	var doing := text.get_slice(" — ", 0) if text != "" and not text.begins_with("Indo:") else "" # #246: o cartão diz o que faz, não "Parado" com a barra acima
	if doing != _doing:
		_doing = doing
		_show_action()
	if text == "":
		return
	busy_label.text = text
	busy_bar.value = fraction
	busy_panel.offset_bottom = -44.0 - card.size.y - 8.0


## #228: o próprio corpo anda pela previsão local (permitida só para ele): o cartão diz "Andando"/"Correndo"; "" volta à ação do motor.
func set_motion(kind: String) -> void:
	if kind == _motion:
		return
	_motion = kind
	_show_action()


## #234: o combate lido do motor: `strike_wait_ms` (recarga do golpe), `guard` ("Block" ou "Dodge" enquanto vale) e a recarga da guarda.
## A barra só aparece enquanto falta recarga; a guarda mostra uma palavra. Nada aparece fora de briga.
func set_combat(strike_wait_ms: int, guard: String, guard_wait_ms: int) -> void:
	var waiting := strike_wait_ms > 0
	if cooldown.visible != waiting:
		cooldown.visible = waiting
	if waiting:
		cooldown.value = clampf(1.0 - float(strike_wait_ms) / COOLDOWN_MAX_MS, 0.0, 1.0)
	var text := ("Guarda" if guard == "Block" else "Esquiva") if guard != "" else ("Guarda recarregando" if guard_wait_ms > 0 else "")
	if guard_label.text != text:
		guard_label.text = text
	guard_label.visible = text != ""


## #238: o chão sob o corpo (`ground` da pose do motor): "Chão pesado −40%"; vazio fora de rua e mato (dentro de prédio).
func set_ground(drag: Variant) -> void:
	var text := ground_label(drag)
	var slow := int(drag.get("slowPct", 0)) if drag is Dictionary else 0
	if text == _ground and slow == _ground_slow:
		return
	_ground = text
	_ground_slow = slow
	_show_action()


## O rótulo do chão a partir de `ground` do motor: o terreno pelo peso dele (`terrainPct`: 0 firme, até 59 irregular, depois pesado) e,
## se o passo está abaixo do normal, o quanto (`slowPct`) com a causa que o motor aponta (carga, sono, tempo, corpo; "ground" = o chão).
static func ground_label(drag: Variant) -> String:
	if not drag is Dictionary or drag.is_empty():
		return ""
	var pct := int(drag.get("terrainPct", 0))
	var text := "Chão firme" if pct <= 0 else ("Chão irregular" if pct < 60 else "Chão pesado")
	var slow := int(drag.get("slowPct", 0))
	if slow <= 0:
		return text
	var cause := ""
	match String(drag.get("cause", "")):
		"load":
			cause = " (carga)"
		"tired":
			cause = " (cansaço)"
		"weather":
			cause = " (lama ou neve)"
		"body":
			cause = " (corpo)"
	return "%s −%d%%%s" % [text, slow, cause]


func _show_action() -> void:
	var text := _motion if _motion != "" else (_doing if _doing != "" else _engine_action)
	if _motion != "" and _ground != "":
		text = "%s · %s" % [_motion, _ground]
	action_label.add_theme_color_override("font_color", Color("ffcf8a") if _motion != "" and _ground_slow >= 30 else Color(1, 1, 1, 0.7))
	action_label.text = text
	var named := String(_mood.get("emotionName", "")) != "" # #262: com humor do motor, o "Parado" vira "Feliz · Parado" na linha do rosto
	action_label.visible = text != "" and not named
	mood_row.visible = named
	if named:
		mood_label.text = mood_line(_mood, text)


## #262: a emoção dominante do motor (`mood` da ficha): rosto, cor e nome; os momentos com causa e tempo; vazio ou nulo esconde tudo.
func set_mood(mood: Variant) -> void:
	_mood = mood if mood is Dictionary else {}
	var named := String(_mood.get("emotionName", "")) != ""
	if named:
		mood_icon.kind = String(_mood.get("icon", "")) if _mood.get("icon") != null else ""
		var hex := String(_mood.get("color", "")) if _mood.get("color") != null else ""
		mood_icon.color = Color(hex) if hex.is_valid_html_color() else Color("c9d3da")
		mood_icon.queue_redraw()
		mood_label.add_theme_color_override("font_color", mood_icon.color.lightened(0.25))
	_show_action()
	var lines := moment_lines(_mood.get("moments", []))
	var key := "|".join(lines.map(func(l): return String(l.text)))
	if key == _moments_key:
		return
	_moments_key = key
	for child in moments_box.get_children():
		moments_box.remove_child(child)
		child.queue_free()
	for line in lines:
		var label := _label(13, Color("8fdc9a") if bool(line.good) else Color("ff9a8a"))
		label.text = String(line.text)
		label.tooltip_text = String(line.tip)
		moments_box.add_child(label)
	moments_box.visible = not lines.is_empty()


## "Feliz · Parado": a emoção e o que o corpo faz (sem ação, só a emoção).
static func mood_line(mood: Dictionary, action: String) -> String:
	var name := String(mood.get("emotionName", ""))
	return name if action == "" else "%s · %s" % [name, action]


const MOMENTS_SHOWN := 5 ## momentos no cartão (os de maior peso primeiro); o resto vira "+N"


## Cada momento do motor vira uma linha: "Bem descansado +2 · 3 h", "Brigou com Marina ×2 −6 · 5 h", "Cheira mal −2" (sem tempo: dura enquanto a condição valer).
static func moment_lines(list: Variant) -> Array:
	var lines: Array = []
	var moments: Array = list if list is Array else []
	for m in moments.slice(0, MOMENTS_SHOWN):
		var weight := float(m.get("weight", 0))
		var sign_text := ("+%s" if weight >= 0.0 else "−%s") % _num(absf(weight))
		var count := int(m.get("count", 1))
		var left := remaining_text(m.get("remainingMs"))
		var text := "%s%s %s%s" % [String(m.get("name", "")), (" ×%d" % count) if count > 1 else "", sign_text, (" · " + left) if left != "" else ""]
		lines.append({"text": text, "good": weight >= 0.0, "tip": "%s (%s)" % [String(m.get("name", "")), left if left != "" else "enquanto durar"]})
	if moments.size() > MOMENTS_SHOWN:
		lines.append({"text": "+%d momentos" % (moments.size() - MOMENTS_SHOWN), "good": true, "tip": ""})
	return lines


static func _num(value: float) -> String:
	return "%d" % int(value) if is_equal_approx(value, round(value)) else ("%.1f" % value).replace(".", ",")


## O que falta para o momento acabar, em tempo de mundo: "3 h", "45 min", "2 d"; nulo (sem prazo) vira vazio.
static func remaining_text(ms: Variant) -> String:
	if ms == null:
		return ""
	var minutes := ceili(float(ms) / 60000.0)
	if minutes >= 2880:
		return "%d d" % int(round(minutes / 1440.0))
	if minutes >= 120:
		return "%d h" % int(round(minutes / 60.0))
	return "%d min" % maxi(1, minutes)


## #222: "Dinheiro 120 · Leva 3 Comida" (o que o motor diz; sem campo, nada).
func set_purse(money: Variant, carrying: Variant, name_of: Callable) -> void:
	var parts: Array[String] = []
	if money != null:
		parts.append("Dinheiro %d" % int(money))
	if carrying is Dictionary and int(carrying.get("quantity", 0)) > 0:
		parts.append("Leva %d %s" % [int(carrying.quantity), String(name_of.call(int(carrying.resourceId))) if name_of.is_valid() else ""])
	purse_label.text = " · ".join(parts)
	purse_label.visible = not parts.is_empty()


## #259: a relação da oferta `person` sob a mira (ou nada: esconde). `now_ms` é o instante de mundo da última pose, para dizer "há 2 min".
func set_relation(offer: Dictionary, now_ms: int = 0) -> void:
	relation.show_for(offer, now_ms)


func set_clock(text: String) -> void:
	clock_label.text = text


## #260: a velocidade do tempo como o motor diz (`GET /players/{id}/time`: `canSkip`, `speed`, `maxSpeed`). Os botões só aparecem quando o
## jogador pode mudar o tempo (sozinho no mundo); com mais de um jogador somem. `maxSpeed` esconde as velocidades que o cenário não permite.
func set_speed(view: Dictionary) -> void:
	var can := bool(view.get("canSkip", false)) or bool(view.get("canSetSpeed", false))
	speed_box.visible = can
	speed_now = int(view.get("speed", 1)) if view.get("speed") != null else 1
	var list: Array = speeds_of(view)
	if speed_values.size() != list.size() + 1 or speed_values.slice(1) != list:
		_build_speed_buttons(list)
	for i in speed_buttons.size():
		speed_buttons[i].set_pressed_no_signal(int(speed_values[i]) == speed_now)


## #261: a lista de velocidades do motor (`speeds`; sem ela, 1..`maxSpeed` como na #260).
static func speeds_of(view: Dictionary) -> Array:
	if view.get("speeds") is Array and not (view.speeds as Array).is_empty():
		return (view.speeds as Array).map(func(v): return int(v))
	var top := int(view.get("maxSpeed", 3)) if view.get("maxSpeed") != null else 3
	return range(1, maxi(1, top) + 1)


## #261: o texto de um botão de velocidade ("II" a pausa, "10×") e a dica com a tecla (0 pausa, 1–9 pela ordem).
static func speed_text(value: int) -> String:
	return "II" if value == 0 else "%d×" % value


static func speed_tip(index: int, value: int) -> String:
	return "Pausa (0)" if value == 0 else "Velocidade %d× (%d)%s" % [value, index, " · tempo real" if value == 1 else ""]


## #261: a velocidade que a tecla `digit` (0–9) pede, ou -1 se não há botão para ela.
func speed_for_key(digit: int) -> int:
	return int(speed_values[digit]) if digit >= 0 and digit < speed_values.size() else -1


func _build_speed_buttons(list: Array) -> void:
	for child in speed_box.get_children():
		speed_box.remove_child(child)
		child.queue_free()
	speed_buttons.clear()
	speed_values = [0]
	speed_values.append_array(list)
	speed_box.offset_left = -(16 + 44 * speed_values.size())
	for i in speed_values.size():
		var value := int(speed_values[i])
		var button := Button.new()
		button.text = speed_text(value)
		button.tooltip_text = speed_tip(i, value)
		button.toggle_mode = true
		button.focus_mode = Control.FOCUS_NONE
		button.custom_minimum_size = Vector2(40, 30)
		button.add_theme_stylebox_override("normal", _style(Color(0.05, 0.06, 0.09, 0.72), 8))
		button.add_theme_stylebox_override("hover", _style(Color(0.15, 0.17, 0.22, 0.85), 8))
		button.add_theme_stylebox_override("pressed", _style(Color("b0873a"), 8))
		button.pressed.connect(func(): speed_chosen.emit(value))
		speed_box.add_child(button)
		speed_buttons.append(button)


## A dica sob a mira: "E  Comprar comida (5) · Banca", ou nada. `text` é a linha do motor ("Alvo: Verbo ...").
func set_prompt(text: String) -> void:
	prompt.text = ("E   " + prompt_text(text)) if text != "" else ""
	prompt.visible = text != "" and not menu_panel.visible and not blocked


## #233: "Caio: Conversar" -> "Conversar · Caio" (verbo primeiro, o alvo depois).
static func prompt_text(line: String) -> String:
	var cut := line.find(": ")
	if cut < 0:
		return line
	var target := line.substr(0, cut)
	var verb := line.substr(cut + 2)
	return "%s · %s" % [verb, target] # #228: sem emoji: a fonte do jogo não tem o glifo (aparecia um quadrado)


## #228: raio da roda para `n` fatias de ângulo igual: cresce de WHEEL_MIN_RADIUS até nenhum botão (com WHEEL_GAP) tocar outro.
static func wheel_radius(n: int) -> float:
	var radius := WHEEL_MIN_RADIUS
	while radius < 600.0 and _wheel_overlaps(n, radius):
		radius += 6.0
	return radius


static func _wheel_overlaps(n: int, radius: float) -> bool:
	var rects := wheel_rects(n, radius)
	for rect in rects:
		if rect.grow(WHEEL_AIM_CLEAR).has_point(Vector2.ZERO):
			return true # cobre a mira
	for i in rects.size():
		for j in range(i + 1, rects.size()):
			if rects[i].grow(WHEEL_GAP * 0.5).intersects(rects[j].grow(WHEEL_GAP * 0.5)):
				return true
	return false


## #233/#228: o centro da fatia `i` de `n` (px do centro da tela): ângulo igual, a primeira no alto, em sentido horário.
static func wheel_offset(i: int, n: int, radius: float = -1.0) -> Vector2:
	var angle := -PI / 2.0 + TAU * float(i) / float(maxi(n, 1))
	return Vector2(cos(angle), sin(angle)) * (radius if radius > 0.0 else wheel_radius(n))


## #228: os retângulos dos botões da roda (px, a partir do centro da tela).
static func wheel_rects(n: int, radius: float = -1.0) -> Array[Rect2]:
	var r := radius if radius > 0.0 else wheel_radius(n)
	var out: Array[Rect2] = []
	for i in n:
		out.append(Rect2(wheel_offset(i, n, r) - WHEEL_BUTTON / 2.0, WHEEL_BUTTON))
	return out


## #233: a mochila como o motor diz (ficha): dinheiro e o que leva nas mãos; sem dado, "vazia".
func set_inventory(money: Variant, carrying: Variant, name_of: Callable) -> void:
	for child in inventory_box.get_children():
		inventory_box.remove_child(child)
		child.queue_free()
	var title := _label(18, Color("ffe9a6"))
	title.text = "Mochila"
	inventory_box.add_child(title)
	var coins := _label(15, Color.WHITE)
	coins.text = "Dinheiro: %d" % int(money) if money != null else "Dinheiro: —"
	inventory_box.add_child(coins)
	var hands := _label(15, Color.WHITE)
	if carrying is Dictionary and int(carrying.get("quantity", 0)) > 0:
		hands.text = "Nas mãos: %d %s" % [int(carrying.quantity), String(name_of.call(int(carrying.resourceId))) if name_of.is_valid() else ""]
	else:
		hands.text = "Nas mãos: nada"
	inventory_box.add_child(hands)
	var foot := _label(12, Color(1, 1, 1, 0.55))
	foot.text = "I ou Tab fecha"
	inventory_box.add_child(foot)


## #235: as metas do motor: a mais urgente na linha do HUD e a lista toda no painel.
func set_goals(body: Dictionary) -> void:
	_goal_body = body
	_refresh_goal_label()
	goals.set_goals(body)


## #238: a linha da meta aponta o lado e a distância do passo "andar até" (o motor diz o lugar; a direção é a da câmera, a do WASD).
func set_goal_where(fine: Vector2, forward: Vector2) -> void:
	var step: Variant = _goal_body.get("goals", [{}])[0].get("step") if _goal_body.get("goals") is Array and not _goal_body.goals.is_empty() else null
	var next := ""
	if step is Dictionary and String(step.get("command", "")) == "go" and fine != Vector2.INF:
		var offset := Vector2(float(step.get("x", 0)) + 0.5, float(step.get("y", 0)) + 0.5) - fine
		next = PlayerGuide.where({"x": step.get("x", 0), "y": step.get("y", 0), "distance": int(round(offset.length()))}, fine, forward)
	if next != _goal_where:
		_goal_where = next
		_refresh_goal_label()


func _refresh_goal_label() -> void:
	var line := GoalsPanel.top_line(_goal_body)
	goal_label.text = line if _goal_where == "" or line == "" else "%s (%s)" % [line, _goal_where]


func set_diary(body: Dictionary) -> void:
	goals.set_diary(body)


func toggle_goals() -> void:
	goals.toggle()


func goals_open() -> bool:
	return goals.is_open()


func toggle_inventory() -> void:
	inventory.visible = not inventory.visible


func inventory_open() -> bool:
	return inventory.visible


## As linhas do menu do E no modo de jogo: sem "Bater" (é o clique), as coisas primeiro e só as pessoas mais perto de `at`.
static func game_entries(all: Array, offers: Array, at: Vector2, look: Vector2 = Vector2.ZERO) -> Array:
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
	return aimed_first(things + persons, offers, at, look)


const AIM_CONE := 0.6 ## cosseno mínimo entre o olhar e a direção do alvo (≈53°) para valer como "sob a mira"
const AIM_NEAR := 0.35 ## células: mais perto que isto o alvo está sob os pés, sem direção: vale pela ordem de sempre


## #243: a ação principal do E é a do alvo para onde a câmera aponta (a árvore realçada), não a da pessoa mais perto: entre as ofertas à
## frente do olhar (`look`, no chão, x e z), a que está mais bem alinhada vem primeiro (empate: a mais perto); as outras linhas seguem na
## ordem de antes, e a roda (segurar E) ainda lista todos, a pessoa incluída. Sem olhar ou sem nada à frente, a ordem não muda.
static func aimed_first(entries: Array, offers: Array, at: Vector2, look: Vector2) -> Array:
	if entries.size() < 2 or at == Vector2.INF or look.length() < 0.001:
		return entries
	var dir := look.normalized()
	var best_key := ""
	var best_score := -2.0
	var best_dist := INF
	for offer in offers:
		var spot := Vector2(float(offer.get("x", 0)) + 0.5, float(offer.get("y", 0)) + 0.5)
		if offer.get("fx") != null and offer.get("fy") != null:
			spot = Vector2(float(offer.fx), float(offer.fy))
		var to := spot - at
		var dist := to.length()
		if dist < AIM_NEAR:
			continue
		var score := dir.dot(to / dist)
		if score < AIM_CONE:
			continue
		var key := "%s:%d" % [String(offer.get("kind", "")), int(offer.get("id", -1))]
		if not _has_entry(entries, key):
			continue
		if score > best_score + 0.0001 or (absf(score - best_score) <= 0.0001 and dist < best_dist):
			best_key = key
			best_score = score
			best_dist = dist
	if best_key == "":
		return entries
	var front: Array = []
	var rest: Array = []
	for entry in entries:
		if "%s:%d" % [String(entry.kind), int(entry.id)] == best_key:
			front.append(entry)
		else:
			rest.append(entry)
	return front + rest


static func _has_entry(entries: Array, key: String) -> bool:
	for entry in entries:
		if "%s:%d" % [String(entry.kind), int(entry.id)] == key:
			return true
	return false


func show_menu(list: Array) -> void:
	_all_entries = list
	_page = 0
	_render_menu()


## #255: a roda mostra `MENU_MAX` linhas por vez; sobrando linhas, a última é "Mais ▸" (a próxima página, voltando à primeira ao fim).
static func page_of(all: Array, page: int) -> Array:
	if all.size() <= MENU_MAX:
		return all
	var per := MENU_MAX - 1
	var pages := ceili(float(all.size()) / per)
	var at := posmod(page, pages)
	var slice := all.slice(at * per, mini(all.size(), (at + 1) * per))
	slice.append({"text": "Mais ▸ (%d/%d)" % [at + 1, pages], "kind": "more", "id": at + 1, "verb": "more", "resource": -1})
	return slice


func _render_menu() -> void:
	var list := page_of(_all_entries, _page)
	entries = list
	for child in menu_box.get_children():
		menu_box.remove_child(child)
		child.queue_free()
	var title := _label(15, Color("ffe9a6"))
	title.text = ("O que fazer\n1–%d escolhe · Esc fecha" % list.size()) if not list.is_empty() else "Nada ao alcance\nEsc fecha"
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	title.size = Vector2(240, 40)
	title.position = Vector2(-120, -(wheel_radius(list.size()) + WHEEL_BUTTON.y / 2.0 + 52.0)) # #228: acima da roda, longe da mira
	menu_box.add_child(title)
	var radius := wheel_radius(list.size())
	for i in list.size():
		var entry: Dictionary = list[i]
		var button := Button.new()
		button.text = "%d   %s" % [i + 1, prompt_text(String(entry.text))]
		button.focus_mode = Control.FOCUS_NONE
		button.size = WHEEL_BUTTON
		button.position = wheel_offset(i, list.size(), radius) - WHEEL_BUTTON / 2.0
		button.add_theme_stylebox_override("normal", _style(Color(0.05, 0.06, 0.09, 0.85), 16))
		button.pressed.connect(func(): choose(entry))
		if bool(entry.get("dim", false)): # #259: o que não vale agora fica esmaecido; o motivo (do motor) aparece sob a roda
			button.modulate = Color(1, 1, 1, 0.5)
			button.tooltip_text = String(entry.get("reason", ""))
			button.mouse_entered.connect(func(): reason_label.text = String(entry.get("reason", "")))
		else:
			button.mouse_entered.connect(func(): reason_label.text = "")
		menu_box.add_child(button)
	reason_label = _label(14, Color("ffb3a0"))
	reason_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	reason_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	reason_label.size = Vector2(420, 40)
	reason_label.position = Vector2(-210, radius + WHEEL_BUTTON.y / 2.0 + 14.0)
	menu_box.add_child(reason_label)
	menu_panel.visible = true
	prompt.visible = false


## Uma linha escolhida (clique ou tecla): "Mais ▸" só vira a página; o resto sobe para o HUD.
func choose(entry: Dictionary) -> void:
	if String(entry.get("verb", "")) == "more":
		_page = int(entry.id)
		_render_menu()
		return
	chosen.emit(entry)


func close_menu() -> void:
	menu_panel.visible = false
	prompt.visible = prompt.text != "" and not blocked


func menu_open() -> bool:
	return menu_panel.visible


## Tecla 1–9 com o menu aberto: a linha dessa posição (ou {} se não há).
func entry_for_key(keycode: int) -> Dictionary:
	var index := keycode - KEY_1
	return entries[index] if index >= 0 and index < entries.size() else {}
