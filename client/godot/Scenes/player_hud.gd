extends PanelContainer
## HUD do jogador encarnado (`/players/*`): o NPC controlado, a barra de ações que o motor aceita e a saída.
## O motor decide se a ação cabe: aqui só se pede. Sair não congela o personagem: ele desaparece do mundo.

signal released()
signal warning(text: String)

const CHOICES := [["Eat", 0], ["Sleep", 1], ["Work", 2], ["Socialize", 3], ["Idle", 5], ["Buy", 6]] ## PlayerCommands.Choosable
const PAUSED_NOTE := "Mundo pausado: suas ações só andam quando o tempo voltar."

var api
var labels
var player_id := ""
var npc_id := -1
var npc_name := ""
var is_active := false
var current_action := ""
var paused := false
var action_buttons: Dictionary = {} ## "Eat" -> Button
var auto_button: Button
var logout_button: Button
var status_text := ""
var _title: Label
var _status: Label
var _pause: Label
var _busy := false


func _init() -> void:
	visible = false
	var box := VBoxContainer.new()
	add_child(box)
	var row := HFlowContainer.new() # faixa sob a barra do topo; quebra de linha se faltar largura
	box.add_child(row)
	_title = Label.new()
	_title.add_theme_font_size_override("font_size", 18)
	_title.custom_minimum_size.x = 150
	row.add_child(_title)
	_pause = Label.new()
	_pause.text = PAUSED_NOTE
	_pause.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_pause.custom_minimum_size.x = 560
	_pause.add_theme_color_override("font_color", Color("ffcf4a"))
	_pause.visible = false
	box.add_child(_pause)
	for choice in CHOICES:
		var button := Button.new()
		button.toggle_mode = true
		button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		var name: String = choice[0]
		button.pressed.connect(func(): act(name))
		row.add_child(button)
		action_buttons[name] = button
	auto_button = Button.new()
	auto_button.text = "Deixar o motor decidir"
	auto_button.pressed.connect(func(): act(""))
	row.add_child(auto_button)
	logout_button = Button.new()
	logout_button.text = "Sair do personagem"
	logout_button.pressed.connect(func(): logout())
	row.add_child(logout_button)
	_status = Label.new()
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_status.custom_minimum_size.x = 560
	_status.modulate = Color(1, 1, 1, 0.8)
	box.add_child(_status)
	refresh_labels()


## Rótulos dos botões vêm do catálogo do servidor (o cliente não escreve texto de ação).
func refresh_labels() -> void:
	for choice in CHOICES:
		var name: String = choice[0]
		var label: String = labels.label_for(int(choice[1])) if labels != null else ""
		action_buttons[name].text = label if label != "" else name


## Encarna o NPC; devolve true se o servidor aceitou.
func incarnate(id: int, name: String) -> bool:
	var response: Dictionary = await api.incarnate(id, player_id)
	if response.ok:
		npc_id = id
		npc_name = name
		is_active = true
		current_action = ""
		visible = true
		_title.text = "Você é %s" % name
		_set_status("")
		_sync_buttons()
		refresh_labels()
		return true
	warning.emit(_incarnate_error(response))
	return false


func act(action: String) -> void:
	if not is_active or _busy:
		return
	_busy = true
	var response: Dictionary = await api.player_action(player_id, action)
	_busy = false
	if not is_active:
		return
	if response.ok:
		current_action = action
		_sync_buttons()
		_set_status("Pedido: %s." % (action_text(action) if action != "" else "o motor decide"))
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		warning.emit("Esta ação não é permitida (erro %d)." % response.status)
		_sync_buttons()


func logout() -> void:
	if not is_active or _busy:
		return
	_busy = true
	var response: Dictionary = await api.player_logout(player_id)
	_busy = false
	if response.ok and response.body is Dictionary:
		_lost("%s desapareceu do mundo no tick %d. Não ficou congelado: o mundo seguiu." % [npc_name, int(response.body.get("disappearedAtTick", 0))])
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		warning.emit("Não foi possível sair (erro %d)." % response.status)


func set_paused(value: bool) -> void:
	paused = value
	_pause.visible = value and is_active


func action_text(action: String) -> String:
	return action_buttons[action].text if action_buttons.has(action) else action


func _lost(text: String) -> void:
	is_active = false
	current_action = ""
	_pause.visible = false
	_title.text = "Sem personagem"
	_set_status(text)
	released.emit()
	visible = true # a mensagem de saída fica à vista até o próximo NPC


func _sync_buttons() -> void:
	for name in action_buttons:
		action_buttons[name].set_pressed_no_signal(name == current_action)
	_pause.visible = paused and is_active


func _set_status(text: String) -> void:
	status_text = text
	_status.text = text


func _incarnate_error(response: Dictionary) -> String:
	var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
	match response.status:
		404:
			return "Este NPC morreu ou não existe: não dá para encarná-lo."
		409:
			return "Você já controla outro NPC." if "outro NPC" in error else "Este NPC já é controlado por outro jogador."
	return "Não foi possível encarnar (erro %d)." % response.status
