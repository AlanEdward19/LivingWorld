extends Control
## #222: a moldura de jogo do modo jogador (3ª/1ª pessoa), no padrão de RPG: o cartão do corpo no canto de baixo (nome, o que faz,
## saúde, fome, sede e sono, como o motor diz), a hora no topo, a dica do que o E alcança sob a mira, a faixa de teclas no rodapé e
## o menu do E (numerado, 1–9 escolhe, Esc fecha). Só desenha: o que vale e o que acontece é do motor (via player_hud).

signal chosen(entry: Dictionary)

const BARS := [["health", "Saúde", Color("5fb36b")], ["hunger", "Fome", Color("e0a43a")], ["thirst", "Sede", Color("4aa3df")],
	["sleep", "Sono", Color("9b7fd1")]]
const HINTS := "WASD andar   Shift correr   Espaço pular   E interagir (segure: roda)   Clique bater\nI mochila   J metas   C câmera   Q ombro   + − zoom   F1 ajuda   Esc menu" ## #227: duas linhas (cabe em 1280)
const GoalsPanel := preload("res://Scenes/goals_panel.gd")
const PlayerGuide := preload("res://Scenes/player_guide.gd")
const COMBAT_HINTS := "   Botão direito/F bloquear   Ctrl esquivar" ## #234: combate (Clique bate já está nas dicas)
const COOLDOWN_MAX_MS := 2000.0 ## a barra de recarga enche em até isto
const WHEEL_MIN_RADIUS := 96.0 ## px: a roda nunca fecha mais que isto (o centro, a mira, fica limpo)
const WHEEL_AIM_CLEAR := 30.0 ## px de folga em volta da mira (centro da tela) sem botão
const WHEEL_GAP := 8.0 ## px livres entre dois botões
const WHEEL_BUTTON := Vector2(232, 34)
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
var menu_panel: Control ## a roda de ações: um Control no centro da tela; as fatias são Buttons em círculo
var menu_box: Control
var inventory: PanelContainer ## #233: a mochila (tecla I ou Tab)
var inventory_box: VBoxContainer
var goal_label: Label ## #235: a meta mais urgente, numa linha discreta sob o relógio
var goals: PanelContainer ## #235: "Metas e diário" (tecla J)
var entries: Array = []
var busy_panel: PanelContainer ## #241: a ação em andamento (barra discreta acima do cartão do jogador)
var busy_label: Label
var busy_bar: ProgressBar
var cooldown: ProgressBar ## #234
var guard_label: Label ## #234
var blocked := false: ## #240: popup aberto: a dica do E some (volta ao fechar, se ainda houver alvo)
	set(value):
		blocked = value
		prompt.visible = prompt.text != "" and not menu_panel.visible and not blocked
var _engine_action := ""
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
	for key in bars:
		if needs.has(key) and needs[key] != null:
			var value := float(needs[key])
			bars[key].value = value
			bars[key].modulate = Color.WHITE if value >= 25.0 else Color(1.0, 0.55, 0.55)


## #241: a ação em andamento ("Cortando lenha — 40%", `fraction` 0..1); texto vazio esconde a barra. Fica logo acima do cartão do corpo.
func set_busy(text: String, fraction: float) -> void:
	busy_panel.visible = text != ""
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
	var text := _motion if _motion != "" else _engine_action
	if _motion != "" and _ground != "":
		text = "%s · %s" % [_motion, _ground]
	action_label.add_theme_color_override("font_color", Color("ffcf8a") if _motion != "" and _ground_slow >= 30 else Color(1, 1, 1, 0.7))
	action_label.text = text
	action_label.visible = text != ""


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
		button.pressed.connect(func(): chosen.emit(entry))
		menu_box.add_child(button)
	menu_panel.visible = true
	prompt.visible = false


func close_menu() -> void:
	menu_panel.visible = false
	prompt.visible = prompt.text != "" and not blocked


func menu_open() -> bool:
	return menu_panel.visible


## Tecla 1–9 com o menu aberto: a linha dessa posição (ou {} se não há).
func entry_for_key(keycode: int) -> Dictionary:
	var index := keycode - KEY_1
	return entries[index] if index >= 0 and index < entries.size() else {}
