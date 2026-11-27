extends PanelContainer
## Janela de conversa com um NPC (`/conversations/start|send|end`). O servidor decide se o NPC aceita e o que
## ele responde (com orçamento de 5 s e resposta de reserva determinística); aqui só se mostra o diálogo e a
## emoção. `proposedActions` é ignorado de propósito: o motor valida e aplica, nunca o cliente (llm-boundary).

signal closed()
signal warning(text: String)

const HISTORY_MIN_HEIGHT := 110.0 ## #237: o histórico nunca encolhe a ponto de cortar linhas (cabem as últimas falas)
const REPLY_TIMEOUT := 8.0 ## orçamento do servidor (5 s) + margem
const REFUSALS := {
	"RejectedBusy": "%s está ocupado e não pode conversar agora.",
	"RejectedHostile": "%s não quer falar com você.",
	"RejectedUnavailable": "%s não está disponível para conversar.",
}
## #233: falas prontas do jogador (só o texto que o jogador diria; vão pelo mesmo `send`, e o motor/LLM decide a resposta).
const CHOICES := ["Bom dia! Como vai?", "Tem alguma novidade por aqui?", "Preciso de ajuda.", "Posso ajudar em algo?", "Até logo."]
const EMOTIONS := {
	"neutral": ["neutro", "c7ced9"], "concerned": ["preocupado", "e6c36a"], "happy": ["feliz", "7ccf88"],
	"annoyed": ["irritado", "e0955a"], "curious": ["curioso", "6ab7e6"], "afraid": ["com medo", "b48ee0"],
	"friendly": ["amigável", "8fd9b0"], "angry": ["furioso", "e07a74"], "sad": ["triste", "7f9bd0"],
	"suspicious": ["desconfiado", "c9a07a"],
}

var api
var npc_id := -1
var npc_name := ""
var session_id := -1
var is_open := false
var waiting := false
var lines: Array = [] ## [{who, text, emotion}] em ordem
var status_text := ""
var input: LineEdit
var game_style := false ## #233: modo jogador: a fala do NPC em destaque, escolhas numeradas (1–5) e texto livre com Enter
var choices_row: HFlowContainer
var spoken: Label ## a última fala do NPC, grande
var _title: Label
var _history: VBoxContainer
var _status: Label
var _send_button: Button
var _scroll: ScrollContainer
var _deadline: Timer
var _request := 0


func _init() -> void:
	visible = false
	grow_vertical = Control.GROW_DIRECTION_BEGIN # #237: se o conteúdo pedir mais altura, o painel cresce para cima (nunca para baixo da tela)
	var box := VBoxContainer.new()
	add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	_title = Label.new()
	_title.add_theme_font_size_override("font_size", 18)
	_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_title)
	var end_button := Button.new()
	end_button.text = "Encerrar (Esc)"
	end_button.pressed.connect(func(): end_conversation())
	header.add_child(end_button)
	_scroll = ScrollContainer.new()
	_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	_scroll.custom_minimum_size.y = HISTORY_MIN_HEIGHT
	box.add_child(_scroll)
	_history = VBoxContainer.new()
	_history.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_scroll.add_child(_history)
	spoken = Label.new()
	spoken.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	spoken.add_theme_font_size_override("font_size", 17)
	spoken.visible = false
	box.add_child(spoken)
	_status = Label.new()
	_status.modulate = Color(1, 1, 1, 0.7)
	box.add_child(_status)
	choices_row = HFlowContainer.new()
	choices_row.visible = false
	for i in CHOICES.size():
		var choice := Button.new()
		choice.text = "%d  %s" % [i + 1, CHOICES[i]]
		choice.focus_mode = Control.FOCUS_NONE
		choice.pressed.connect(func(): pick(i))
		choices_row.add_child(choice)
	box.add_child(choices_row)
	var row := HBoxContainer.new()
	box.add_child(row)
	input = LineEdit.new()
	input.placeholder_text = "diga algo…"
	input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	input.text_submitted.connect(func(text: String): send(text))
	input.gui_input.connect(func(event: InputEvent):
		if event is InputEventKey and event.pressed and event.keycode == KEY_ESCAPE:
			input.accept_event()
			end_conversation())
	row.add_child(input)
	_send_button = Button.new()
	_send_button.text = "Enviar"
	_send_button.pressed.connect(func(): send(input.text))
	row.add_child(_send_button)
	_deadline = Timer.new()
	_deadline.one_shot = true
	_deadline.wait_time = REPLY_TIMEOUT
	_deadline.timeout.connect(_on_deadline)
	add_child(_deadline)


## Pede a conversa; só `Accepted` abre a janela, os demais mostram o motivo em português.
func start(id: int, name: String) -> void:
	if is_open:
		await end_conversation()
	var response: Dictionary = await api.conversation_start(id)
	if not response.ok or not (response.body is Dictionary):
		warning.emit("Não foi possível iniciar a conversa com %s (erro %d)." % [name, response.status])
		return
	var decision := String(response.body.get("decision", ""))
	if decision != "Accepted" or response.body.get("sessionId") == null:
		warning.emit(String(REFUSALS.get(decision, "%s não pode conversar agora.")) % name)
		return
	_open(id, name, int(response.body.sessionId))


## AD-197d: a conversa que o motor já abriu (interação "conversar" do jogador, tecla E): só a janela, sem novo pedido.
func attach(id: int, name: String, session: int) -> void:
	if is_open:
		await end_conversation()
	_open(id, name, session)


func _open(id: int, name: String, session: int) -> void:
	npc_id = id
	npc_name = name
	session_id = session
	lines = []
	waiting = false
	is_open = true
	visible = true
	_title.text = "Conversando com %s" % name
	_rebuild()
	_set_status("")
	choices_row.visible = game_style
	spoken.visible = false
	spoken.text = ""
	input.placeholder_text = "Enter para escrever…" if game_style else "diga algo…"
	if is_inside_tree() and not game_style:
		input.grab_focus()


func send(text: String) -> void:
	var message := text.strip_edges()
	if not is_open or waiting or message == "":
		return
	input.text = ""
	if game_style and input.is_inside_tree():
		input.release_focus()
	_add_line("Você", message, "")
	waiting = true
	_request += 1
	var mine := _request
	_set_status("%s está pensando…" % npc_name)
	if _deadline.is_inside_tree():
		_deadline.start()
	var response: Dictionary = await api.conversation_send(session_id, message)
	if mine != _request or not waiting:
		return # a espera estourou ou a conversa acabou: resposta tardia é descartada
	_deadline.stop()
	waiting = false
	_set_status("")
	if response.ok and response.body is Dictionary:
		_add_line(npc_name, String(response.body.get("dialogue", "")), String(response.body.get("emotion", "")))
	elif response.status == 404:
		_finish("A conversa não existe mais.")
	elif response.status == 409:
		_finish("%s morreu." % npc_name if "npc-dead" in str(response.body) else "A conversa foi encerrada.")
	else:
		warning.emit("%s não respondeu (erro %d). Tente de novo." % [npc_name, response.status])


## #233: a escolha `index` (tecla 1–5 ou clique): a fala pronta vira o mesmo comando de conversa; "Até logo." encerra depois.
func pick(index: int) -> void:
	if not is_open or waiting or index < 0 or index >= CHOICES.size():
		return
	var farewell := index == CHOICES.size() - 1
	await send(CHOICES[index])
	if farewell and is_open:
		await end_conversation()


func _unhandled_input(event: InputEvent) -> void:
	if not (is_open and game_style and event is InputEventKey and event.pressed and not event.echo) or input.has_focus():
		return
	if event.keycode >= KEY_1 and event.keycode < KEY_1 + CHOICES.size():
		get_viewport().set_input_as_handled()
		pick(event.keycode - KEY_1)
	elif event.keycode == KEY_ENTER or event.keycode == KEY_KP_ENTER:
		get_viewport().set_input_as_handled()
		input.grab_focus()


func end_conversation() -> void:
	if not is_open:
		return
	var id := session_id
	_close()
	await api.conversation_end(id)


func _on_deadline() -> void:
	if waiting:
		waiting = false
		_request += 1
		_set_status("")
		warning.emit("%s demorou demais para responder. Tente de novo." % npc_name)


func _finish(reason: String) -> void:
	_close()
	warning.emit(reason)


func _close() -> void:
	is_open = false
	waiting = false
	_request += 1
	visible = false
	session_id = -1
	closed.emit()


func _add_line(who: String, text: String, emotion: String) -> void:
	lines.append({"who": who, "text": text, "emotion": emotion})
	if game_style and who != "Você":
		spoken.text = "%s: %s" % [who, text]
		spoken.visible = true
	_history.add_child(_line_node(lines.back()))
	_scroll_down.call_deferred()


func _scroll_down() -> void:
	if not is_inside_tree():
		return
	await get_tree().process_frame # o rótulo novo só ganha altura depois do layout; sem isto a rolagem pára na penúltima linha
	if is_inside_tree():
		_scroll.scroll_vertical = int(_scroll.get_v_scroll_bar().max_value)


## Verdadeiro quando a rolagem está no fim (a última fala inteira à vista).
func at_bottom() -> bool:
	var bar := _scroll.get_v_scroll_bar()
	return bar.max_value - bar.page - _scroll.scroll_vertical <= 1.0


func _rebuild() -> void:
	for child in _history.get_children():
		_history.remove_child(child)
		child.queue_free()


func _line_node(line: Dictionary) -> Control:
	var label := Label.new()
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.custom_minimum_size.x = 380
	var tag := ""
	if line.emotion != "":
		var known = EMOTIONS.get(line.emotion)
		tag = " (%s)" % (known[0] if known != null else line.emotion)
		if known != null:
			label.add_theme_color_override("font_color", Color(known[1]))
	label.text = "%s%s: %s" % [line.who, tag, line.text]
	return label


func _set_status(text: String) -> void:
	status_text = text
	_status.text = text
