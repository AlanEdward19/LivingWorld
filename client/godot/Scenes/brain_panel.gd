extends PanelContainer
## Painel "Cérebro": as decisões recentes de um NPC (`cognitionTrace` de GET /npcs/{id}) em linha do tempo e,
## ao escolher uma, o fluxo Estímulo → Ponderação → Decisão. Só mostra o que o servidor gravou: nada é
## recalculado aqui. O rastro existe só para NPC em cidade/interior observados (ver ScopeObserver) ou vigiado.

signal closed()
signal warning(text: String)

const WAKE := {0: "motivo desconhecido", 1: "necessidade urgente", 2: "ação concluída", 3: "evento", 4: "agendado"}
const EMPTY_NO_TRACE := "Sem rastro: este NPC não está numa cidade/interior observado agora. Aproxime a câmera da cidade dele ou marque-o como vigiado."
const EMPTY_GONE := "Este NPC morreu ou foi arquivado: não há mais decisões a mostrar."
const EMPTY_ERROR := "Não foi possível carregar o cérebro deste NPC."
const BAR_FLOOR := 100.0 ## escala mínima das barras; cresce se o servidor mandar valor maior
const GOOD := Color("7ccf88")
const BAD := Color("e07a74")
const BLOCK := Color("f0b64a")

var api
var labels
var date_of: Callable = func(tick: int) -> String: return "tick %d" % tick
var npc_id := -1
var npc_name := ""
var is_open := false
var watching := false
var watched: Dictionary = {} ## ids que ESTA sessão do cliente vigiou (o servidor não informa o estado)
var entries: Array = [] ## [{tick, trace}], mais recente primeiro
var selected := -1
var empty_text := "" ## != "" quando não há fluxo para mostrar
var timeline: ItemList
var pressure_nodes: Array = []
var opportunity_nodes: Array = []
var alternative_nodes: Array = []
var winner_node: Control
var watch_button: Button
var _title: Label
var _empty: Label
var _flow_root: Control
var _columns: HBoxContainer
var _lines: FlowLines
var _last_tick := -1
var _follow_latest := true


class FlowLines extends Control:
	var links: Array = [] ## [[from, to, weight]]

	func _init() -> void:
		mouse_filter = Control.MOUSE_FILTER_IGNORE

	func _draw() -> void:
		var origin := global_position
		for link in links:
			if not is_instance_valid(link[0]) or not is_instance_valid(link[1]):
				continue
			var from_rect: Rect2 = (link[0] as Control).get_global_rect()
			var to_rect: Rect2 = (link[1] as Control).get_global_rect()
			var start := Vector2(from_rect.end.x, from_rect.position.y + from_rect.size.y / 2.0) - origin
			var end := Vector2(to_rect.position.x, to_rect.position.y + to_rect.size.y / 2.0) - origin
			var color := Color(0.75, 0.85, 1.0, float(link[2]))
			draw_line(start, end, color, 2.0, true)
			var dir := (end - start).normalized()
			var side := Vector2(-dir.y, dir.x) * 4.0
			draw_colored_polygon(PackedVector2Array([end, end - dir * 9.0 + side, end - dir * 9.0 - side]), color)


func _init() -> void:
	visible = false
	custom_minimum_size = Vector2(560, 0)
	var box := VBoxContainer.new()
	add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	_title = Label.new()
	_title.add_theme_font_size_override("font_size", 20)
	_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_title)
	watch_button = Button.new()
	watch_button.pressed.connect(func(): toggle_watch())
	header.add_child(watch_button)
	var close_button := Button.new()
	close_button.text = "Fechar (B)"
	close_button.pressed.connect(func(): close())
	header.add_child(close_button)
	var body := HBoxContainer.new()
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	box.add_child(body)
	timeline = ItemList.new()
	timeline.custom_minimum_size = Vector2(340, 0)
	timeline.item_selected.connect(func(index: int): select_entry(index))
	body.add_child(timeline)
	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	body.add_child(right)
	_empty = Label.new()
	_empty.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_empty.custom_minimum_size.x = 300
	_empty.modulate = Color(1, 1, 1, 0.85)
	right.add_child(_empty)
	_flow_root = MarginContainer.new()
	_flow_root.size_flags_vertical = Control.SIZE_EXPAND_FILL
	right.add_child(_flow_root)
	_columns = HBoxContainer.new()
	_columns.add_theme_constant_override("separation", 26)
	_columns.sort_children.connect(func(): _redraw_lines())
	_flow_root.add_child(_columns)
	_lines = FlowLines.new()
	_flow_root.add_child(_lines)
	_refresh_labels()


# --- abrir, fechar, atualizar -------------------------------------------------

func open(id: int) -> void:
	npc_id = id
	watching = watched.has(id)
	is_open = true
	visible = true
	entries = []
	_last_tick = -1
	_follow_latest = true
	await refresh(true)


func close() -> void:
	if not is_open:
		return
	is_open = false
	visible = false
	closed.emit()


## Chamado a cada mudança de hora do mundo; fechado, não faz nada (nenhuma requisição).
func on_world_tick(tick: int) -> void:
	if not is_open or tick == _last_tick:
		return
	_last_tick = tick
	await refresh()


func refresh(force: bool = false) -> void:
	var id := npc_id
	var response: Dictionary = await api.npc_detail(id)
	if id != npc_id or not is_open:
		return # trocou de NPC ou fechou durante a espera
	if response.status == 404:
		_show_empty(EMPTY_GONE)
		return
	if not response.ok or not (response.body is Dictionary):
		_show_empty(EMPTY_ERROR)
		return
	var npc: Dictionary = response.body
	npc_name = String(npc.get("name", ""))
	_refresh_labels()
	if int(npc.get("lod", 0)) == 1:
		_show_empty(EMPTY_GONE)
		return
	var trace: Array = npc.get("cognitionTrace", [])
	var latest := -1 if trace.is_empty() else int(trace.back().tick)
	var current := -1 if entries.is_empty() else int(entries[0].tick)
	if not force and latest == current and trace.size() == entries.size():
		return # nada novo: não redesenha
	var kept_tick := -1 if selected < 0 or selected >= entries.size() else int(entries[selected].tick)
	entries = trace.duplicate()
	entries.reverse()
	_rebuild_timeline()
	if entries.is_empty():
		_show_empty(EMPTY_NO_TRACE)
		return
	var index := 0
	if not _follow_latest:
		for i in entries.size():
			if int(entries[i].tick) == kept_tick:
				index = i
	select_entry(index, false)


func toggle_watch() -> void:
	if npc_id < 0:
		return
	var response: Dictionary = await api.watch_npc(npc_id, not watching)
	if response.ok:
		watching = not watching
		if watching:
			watched[npc_id] = true
		else:
			watched.erase(npc_id)
		_refresh_labels()
	else:
		warning.emit("Não foi possível %s este NPC (erro %d)." % ["vigiar" if not watching else "parar de vigiar", response.status])


# --- desenho ---------------------------------------------------------------

func _refresh_labels() -> void:
	_title.text = "Cérebro de %s" % npc_name if npc_name != "" else "Cérebro"
	watch_button.text = "Parar de vigiar" if watching else "Vigiar"


func _show_empty(text: String) -> void:
	empty_text = text
	_empty.text = text
	_empty.visible = true
	_clear_flow()
	if text != EMPTY_NO_TRACE:
		entries = []
		_rebuild_timeline()


func _rebuild_timeline() -> void:
	timeline.clear()
	for entry in entries:
		var trace: Dictionary = entry.trace
		var action: String = labels.label_for(int(trace.winner)) if labels != null else str(trace.winner)
		timeline.add_item("%s · %s · %s" % [date_of.call(int(entry.tick)), action, WAKE.get(int(trace.wakeReason), WAKE[0])])


func select_entry(index: int, user_choice: bool = true) -> void:
	if index < 0 or index >= entries.size():
		return
	if user_choice:
		_follow_latest = index == 0
	selected = index
	if timeline.item_count > index:
		timeline.select(index)
	empty_text = ""
	_empty.visible = false
	_draw_flow(entries[index].trace)


func _clear_flow() -> void:
	for child in _columns.get_children():
		_columns.remove_child(child)
		child.queue_free()
	pressure_nodes = []
	opportunity_nodes = []
	alternative_nodes = []
	winner_node = null
	_lines.links = []
	_redraw_lines()


func _draw_flow(trace: Dictionary) -> void:
	_clear_flow()
	var stimulus := _column("Estímulo — por que agir?")
	var weighing := _column("Ponderação — o que posso fazer?")
	var decision := _column("Decisão")
	var pressures: Array = trace.get("topPressures", [])
	var opportunities: Array = trace.get("knownOpportunities", [])
	var scale := BAR_FLOOR
	for p in pressures:
		scale = maxf(scale, float(p.intensity))
	for o in opportunities:
		scale = maxf(scale, float(o.attractiveness))
	for p in pressures:
		var node := _bar_node(String(p.kind), float(p.intensity), scale, ", ".join(p.get("factors", [])))
		stimulus.add_child(node)
		pressure_nodes.append(node)
	var factors := _factors_node(trace)
	weighing.add_child(factors)
	for o in opportunities:
		var node := _bar_node(String(o.kind), float(o.attractiveness), scale, str(o.get("detail") if o.get("detail") != null else ""))
		weighing.add_child(node)
		opportunity_nodes.append(node)
	winner_node = _winner_node(trace)
	decision.add_child(winner_node)
	var alternatives: Array = trace.get("knownAlternatives", [])
	if not alternatives.is_empty():
		var header := Label.new()
		header.text = "Descartadas"
		header.modulate = Color(1, 1, 1, 0.6)
		decision.add_child(header)
	for alternative in alternatives:
		var label := Label.new()
		label.text = labels.label_for(int(alternative)) if labels != null else str(alternative)
		label.modulate = Color(1, 1, 1, 0.45)
		decision.add_child(label)
		alternative_nodes.append(label)
	for node in pressure_nodes:
		_lines.links.append([node, factors, 0.55])
	_lines.links.append([factors, winner_node, 0.9])
	for node in opportunity_nodes:
		_lines.links.append([node, winner_node, 0.3])
	_redraw_lines()


func _column(title: String) -> VBoxContainer:
	var column := VBoxContainer.new()
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	column.add_theme_constant_override("separation", 6)
	var header := Label.new()
	header.text = title
	header.add_theme_font_size_override("font_size", 16)
	header.add_theme_color_override("font_color", Color("ffe9a6"))
	column.add_child(header)
	_columns.add_child(column)
	return column


func _bar_node(kind: String, value: float, scale: float, detail: String) -> Control:
	var box := VBoxContainer.new()
	var title := Label.new()
	title.text = "%s  %s" % [kind, str(snappedf(value, 0.1))]
	box.add_child(title)
	var bar := ProgressBar.new()
	bar.max_value = scale
	bar.value = value
	bar.show_percentage = false
	bar.custom_minimum_size = Vector2(120, 12)
	box.add_child(bar)
	if detail != "":
		var small := Label.new()
		small.text = detail
		small.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		small.custom_minimum_size.x = 120
		small.modulate = Color(1, 1, 1, 0.7)
		small.add_theme_font_size_override("font_size", 12)
		box.add_child(small)
	return box


func _factors_node(trace: Dictionary) -> Control:
	var panel := PanelContainer.new()
	var box := VBoxContainer.new()
	panel.add_child(box)
	for spec in [["a favor", "topPositiveFactors", GOOD], ["contra", "topNegativeFactors", BAD], ["bloqueios", "blockingFactors", BLOCK]]:
		var items: Array = trace.get(spec[1], [])
		if items.is_empty():
			continue
		var label := Label.new()
		label.text = "%s: %s" % [spec[0], ", ".join(items)]
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.custom_minimum_size.x = 150
		label.add_theme_color_override("font_color", spec[2])
		label.add_theme_font_size_override("font_size", 13)
		box.add_child(label)
	return panel


func _winner_node(trace: Dictionary) -> Control:
	var panel := PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.25, 0.4, 0.2, 0.9)
	style.set_border_width_all(2)
	style.border_color = Color("ffe9a6")
	style.set_corner_radius_all(8)
	style.set_content_margin_all(8)
	panel.add_theme_stylebox_override("panel", style)
	panel.set_meta("winner", true)
	var box := VBoxContainer.new()
	panel.add_child(box)
	var title := Label.new()
	title.text = labels.label_for(int(trace.winner)) if labels != null else str(trace.winner)
	title.add_theme_font_size_override("font_size", 18)
	box.add_child(title)
	var utility := Label.new()
	utility.text = "utilidade %s" % str(snappedf(float(trace.winningUtility), 0.1))
	box.add_child(utility)
	if trace.get("previousIntent") != null:
		var before := Label.new()
		before.text = "antes: %s" % (labels.label_for(int(trace.previousIntent)) if labels != null else str(trace.previousIntent))
		before.modulate = Color(1, 1, 1, 0.7)
		box.add_child(before)
	return panel


func _redraw_lines() -> void:
	if _lines != null:
		_lines.queue_redraw.call_deferred()
