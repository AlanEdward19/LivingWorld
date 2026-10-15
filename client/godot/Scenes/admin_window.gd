extends PanelContainer
## Console de admin (ADR-0015): pausa com motivo e teto, retomada, velocidade, busca de NPC e auditoria. Só existe quando
## `LW_ADMIN_TOKEN` está definido (o transporte manda `X-Admin-Token`); o operador se identifica em `X-Admin-Actor`.
## Quem decide é o servidor: aqui se valida o óbvio (motivo, 1..teto) e se mostra o 400/403 que ele devolver.

signal closed()
signal warning(text: String)
signal npc_selected(id: int)

const MAX_PAUSE_DAYS := 30 ## espelha SimulationHost.MaxAdminPauseDays; o servidor continua sendo o juiz (400)
const DEFAULT_PAUSE_DAYS := 7
const KINDS := [["name", "nome"], ["city", "cidade"], ["profession", "profissão (número)"]]

var api
var calendar_of: Callable = func() -> Dictionary: return {"hoursPerDay": 24}
var date_of: Callable = func(tick: int) -> String: return "tick %d" % tick
var is_open := false
var actor := "admin"
var pause_info: Dictionary = {} ## última pausa feita POR ESTE cliente: {actor, reason, pausedAtTick}
var state_text := ""
var search_text := ""
var hits: Array = []
var audit_rows: Array = [] ## [String]
var audit_state := ""
var actor_input: LineEdit
var reason_input: LineEdit
var max_spin: SpinBox
var speed_spin: SpinBox
var pause_button: Button
var resume_button: Button
var speed_button: Button
var search_input: LineEdit
var kind_option: OptionButton
var search_button: Button
var result_list: ItemList
var audit_from: SpinBox
var audit_button: Button
var tabs: TabContainer
var _state: Label
var _search_state: Label
var _audit_list: VBoxContainer
var _audit_status: Label


func _init() -> void:
	visible = false
	var box := VBoxContainer.new()
	add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	var title := Label.new()
	title.text = "Console de admin"
	title.add_theme_font_size_override("font_size", 20)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	var who := Label.new()
	who.text = "operador"
	header.add_child(who)
	actor_input = LineEdit.new()
	actor_input.text = actor
	actor_input.custom_minimum_size.x = 120
	actor_input.text_changed.connect(func(text: String): actor = text.strip_edges() if text.strip_edges() != "" else "admin")
	header.add_child(actor_input)
	var close_button := Button.new()
	close_button.text = "Fechar (F8)"
	close_button.pressed.connect(func(): close())
	header.add_child(close_button)
	tabs = TabContainer.new()
	tabs.size_flags_vertical = Control.SIZE_EXPAND_FILL
	box.add_child(tabs)
	_build_world_tab()
	_build_search_tab()
	_build_audit_tab()


func open() -> void:
	is_open = true
	visible = true
	var hours := _hours_per_day()
	max_spin.max_value = hours * MAX_PAUSE_DAYS
	max_spin.value = hours * DEFAULT_PAUSE_DAYS


func close() -> void:
	if not is_open:
		return
	is_open = false
	visible = false
	closed.emit()


func _hours_per_day() -> int:
	return int(calendar_of.call().get("hoursPerDay", 24))


# --- mundo: pausa, retomada, velocidade -----------------------------------------

func _build_world_tab() -> void:
	var tab := VBoxContainer.new()
	tab.name = "Mundo"
	tabs.add_child(tab)
	reason_input = LineEdit.new()
	reason_input.placeholder_text = "motivo da pausa (obrigatório)"
	tab.add_child(reason_input)
	var row := HBoxContainer.new()
	tab.add_child(row)
	var label := Label.new()
	label.text = "Retoma sozinho em (ticks)"
	row.add_child(label)
	max_spin = SpinBox.new()
	max_spin.min_value = 1
	max_spin.max_value = 24 * MAX_PAUSE_DAYS
	max_spin.value = 24 * DEFAULT_PAUSE_DAYS
	row.add_child(max_spin)
	pause_button = Button.new()
	pause_button.text = "Pausar o mundo"
	pause_button.pressed.connect(func(): pause())
	row.add_child(pause_button)
	resume_button = Button.new()
	resume_button.text = "Retomar"
	resume_button.pressed.connect(func(): resume())
	row.add_child(resume_button)
	var speed_row := HBoxContainer.new()
	tab.add_child(speed_row)
	var speed_label := Label.new()
	speed_label.text = "Velocidade (ticks/s)"
	speed_row.add_child(speed_label)
	speed_spin = SpinBox.new()
	speed_spin.min_value = 0.5
	speed_spin.max_value = 1000
	speed_spin.step = 0.5
	speed_spin.value = 1
	speed_row.add_child(speed_spin)
	speed_button = Button.new()
	speed_button.text = "Aplicar"
	speed_button.pressed.connect(func(): set_speed(speed_spin.value))
	speed_row.add_child(speed_button)
	_state = Label.new()
	_state.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_state.custom_minimum_size.x = 520
	tab.add_child(_state)


func pause() -> void:
	var reason := reason_input.text.strip_edges()
	if reason == "":
		_set_state("Informe o motivo da pausa.")
		return
	var ceiling := _hours_per_day() * MAX_PAUSE_DAYS
	var max_ticks := int(max_spin.value)
	if max_ticks < 1 or max_ticks > ceiling:
		_set_state("O teto da pausa deve ficar entre 1 e %d ticks." % ceiling)
		return
	var response: Dictionary = await api.admin_pause(actor, reason, max_ticks)
	if response.ok:
		pause_info = {"actor": String(response.body.get("actor", actor)), "reason": reason, "pausedAtTick": int(response.body.get("pausedAtTick", 0))}
		_set_state("Pausado por %s desde %s: %s (retoma em até %d ticks)." % [pause_info.actor, date_of.call(pause_info.pausedAtTick), reason, max_ticks])
	else:
		_fail(response)


func resume() -> void:
	var response: Dictionary = await api.admin_resume(actor)
	if response.ok:
		pause_info = {}
		_set_state("Mundo retomado.")
	else:
		_fail(response)


func set_speed(ticks_per_second: float) -> void:
	if ticks_per_second <= 0.0:
		_set_state("A velocidade deve ser positiva.")
		return
	var response: Dictionary = await api.admin_speed(actor, ticks_per_second)
	if response.ok:
		_set_state("Velocidade: %s ticks/s." % str(ticks_per_second))
	else:
		_fail(response)


## Faixa do topo para TODOS quando o mundo está pausado. Quem pausou e por quê vem de `GET /simulation/status` (ADR-0027), não
## do que este cliente fez: vale também para a pausa de outro operador.
func pause_banner(status: Dictionary) -> String:
	if not bool(status.get("isPaused", false)):
		return ""
	if status.get("pausedBy") == null:
		return "Mundo pausado"
	return "Pausado por %s desde %s: %s" % [String(status.pausedBy), date_of.call(int(status.get("pausedAtTick", 0))), String(status.get("pauseReason", ""))]


## O relógio voltou a andar (retomada de outro operador ou teto vencido): esquece a pausa lembrada.
func note_running() -> void:
	pause_info = {}


func _failure(response: Dictionary) -> String:
	match response.status:
		403:
			return "Acesso negado: confira o token de admin (LW_ADMIN_TOKEN)."
		400:
			return "O servidor recusou: %s" % (String(response.body.get("error", "")) if response.body is Dictionary else "pedido inválido")
	return "Não foi possível concluir (erro %d)." % response.status


func _set_state(text: String) -> void:
	state_text = text
	_state.text = text


## Falha do servidor: mostra na aba e avisa por toast.
func _fail(response: Dictionary) -> void:
	_set_state(_failure(response))
	warning.emit(state_text)


# --- busca -----------------------------------------------------------------------

func _build_search_tab() -> void:
	var tab := VBoxContainer.new()
	tab.name = "Busca"
	tabs.add_child(tab)
	var row := HBoxContainer.new()
	tab.add_child(row)
	search_input = LineEdit.new()
	search_input.placeholder_text = "buscar NPC"
	search_input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	search_input.text_submitted.connect(func(_text: String): search())
	row.add_child(search_input)
	kind_option = OptionButton.new()
	for kind in KINDS:
		kind_option.add_item(kind[1])
	row.add_child(kind_option)
	search_button = Button.new()
	search_button.text = "Buscar"
	search_button.pressed.connect(func(): search())
	row.add_child(search_button)
	_search_state = Label.new()
	_search_state.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	tab.add_child(_search_state)
	result_list = ItemList.new()
	result_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	result_list.custom_minimum_size.y = 120
	result_list.item_selected.connect(func(index: int): npc_selected.emit(int(hits[index].id)))
	tab.add_child(result_list)


func search() -> void:
	var query := search_input.text.strip_edges()
	hits = []
	result_list.clear()
	if query == "":
		_search_say("Digite o que buscar.")
		return
	var kind: String = KINDS[kind_option.selected][0]
	var response: Dictionary = await api.admin_search(actor, query, kind)
	if not response.ok:
		_search_say(_failure(response))
		warning.emit(search_text)
		return
	hits = response.body
	if hits.is_empty():
		_search_say("Nenhum NPC encontrado.")
		return
	_search_say("%d encontrados. Clique para abrir a ficha." % hits.size())
	for hit in hits:
		result_list.add_item("#%d · %s · %s · ofício %d%s" % [int(hit.id), hit.name, hit.city if hit.city != "" else "sem cidade", int(hit.professionId), "" if hit.alive else " · morto"])


func _search_say(text: String) -> void:
	search_text = text
	_search_state.text = text


# --- auditoria -------------------------------------------------------------------

func _build_audit_tab() -> void:
	var tab := VBoxContainer.new()
	tab.name = "Auditoria"
	tabs.add_child(tab)
	var row := HBoxContainer.new()
	tab.add_child(row)
	var label := Label.new()
	label.text = "A partir do tick"
	row.add_child(label)
	audit_from = SpinBox.new()
	audit_from.min_value = 0
	audit_from.max_value = 1000000000
	row.add_child(audit_from)
	audit_button = Button.new()
	audit_button.text = "Carregar"
	audit_button.pressed.connect(func(): load_audit())
	row.add_child(audit_button)
	_audit_status = Label.new()
	tab.add_child(_audit_status)
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.custom_minimum_size.y = 120
	tab.add_child(scroll)
	_audit_list = VBoxContainer.new()
	scroll.add_child(_audit_list)


func load_audit() -> void:
	audit_rows = []
	for child in _audit_list.get_children():
		_audit_list.remove_child(child)
		child.queue_free()
	var response: Dictionary = await api.admin_audit(actor, int(audit_from.value))
	if not response.ok:
		_audit_say(_failure(response))
		warning.emit(audit_state)
		return
	if response.body.is_empty():
		_audit_say("Nenhuma ação registrada a partir deste tick.")
		return
	_audit_say("%d ações." % response.body.size())
	for entry in response.body:
		var detail := String(entry.detail)
		var line := "%s · %s · %s · %s%s" % [date_of.call(int(entry.tick)), entry.actor, entry.action, entry.target, (" · " + detail) if detail != "" else ""]
		audit_rows.append(line)
		var label := Label.new()
		label.text = line
		_audit_list.add_child(label)


func _audit_say(text: String) -> void:
	audit_state = text
	_audit_status.text = text
