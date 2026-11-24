extends PanelContainer
## HUD do jogador encarnado (`/players/*`): o NPC controlado, a barra de ações que o motor aceita e a saída.
## O motor decide se a ação cabe: aqui só se pede. Sair não congela o personagem: ele desaparece do mundo.
const WorldTime := preload("res://Contract/world_time.gd")

signal released()
signal warning(text: String)
## AD-197d: previsão e correção do passo (o cliente anda já e corrige pela posição do motor), a conversa aberta pelo motor.
signal predicted(delta: Vector2i)
signal confirmed(x: int, y: int)
signal rejected()
signal conversation_opened(npc_id: int, name: String, session_id: int)

const CHOICES := [["Eat", 0], ["Sleep", 1], ["Work", 2], ["Socialize", 3], ["Idle", 5], ["Buy", 6]] ## PlayerCommands.Choosable
const PAUSED_NOTE := "Mundo pausado: suas ações só andam quando o tempo voltar."

var api
var labels
var player_id := ""
var npc_id := -1
var npc_name := ""
var is_active := false
var stale_session := false ## o servidor ainda liga este playerId a um NPC (cliente reiniciado): só dá para sair
var current_action := ""
var paused := false
var action_buttons: Dictionary = {} ## "Eat" -> Button
var power_buttons: Dictionary = {} ## powerId -> Button (AD-196: os poderes que o NPC porta, segundo o servidor)
var _powers_row: HFlowContainer
var auto_button: Button
var logout_button: Button
var status_text := ""
var _title: Label
var _status: Label
var _pause: Label
var _busy := false
## AD-197c: o tempo como o motor o diz (`GET /players/{id}/time`) e os controles de dormir e acelerar, que só aparecem quando o motor
## diz que valem (quem joga sozinho, mundo andando). O que acorda (golpe, fogo, fome, sede, chamado) vem do motor.
const RHYTHM_TEXT := {"Fast": "Tempo rápido", "Accelerated": "Tempo acelerado", "Manual": "Tempo fixado pelo admin"}
const WAKE_TEXT := {"golpe": "Você acordou com um golpe.", "fogo": "Você acordou com o calor do fogo.", "fome": "A fome o acordou.",
	"sede": "A sede o acordou.", "chamado": "Alguém o chamou.", "hora": "Chegou a hora marcada.", "descansado": "Você acordou descansado.",
	"outro jogador": "Outro jogador chegou: o tempo voltou ao normal."}
var time_view: Dictionary = {}
var hours_per_day := 24
var rhythm_label: Label
var sleep_hour: SpinBox
var sleep_until_button: Button
var sleep_fast_button: Button
var accelerate_button: Button
var wake_button: Button
var _last_wake := ""
var session_enabled := false ## AD-197a: pede sessão (token) e abre o canal persistente antes de encarnar; o app liga
var has_session := false


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
	_powers_row = HFlowContainer.new()
	box.add_child(_powers_row)
	_build_time_row(box)
	_status = Label.new()
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_status.custom_minimum_size.x = 560
	_status.modulate = Color(1, 1, 1, 0.8)
	box.add_child(_status)
	refresh_labels()


func _build_time_row(box: VBoxContainer) -> void:
	var row := HFlowContainer.new()
	box.add_child(row)
	rhythm_label = Label.new()
	rhythm_label.custom_minimum_size.x = 150
	row.add_child(rhythm_label)
	sleep_hour = SpinBox.new()
	sleep_hour.min_value = 0
	sleep_hour.max_value = hours_per_day - 1
	sleep_hour.value = 6
	sleep_hour.tooltip_text = "Hora do dia para acordar"
	row.add_child(sleep_hour)
	sleep_until_button = _time_button(row, "Dormir até a hora", func(): sleep(int(sleep_hour.value)))
	sleep_fast_button = _time_button(row, "Dormir acelerando", func(): sleep(-1))
	accelerate_button = _time_button(row, "Acelerar o tempo", func(): accelerate())
	wake_button = _time_button(row, "Acordar", func(): wake())
	apply_time({})


func _time_button(row: HFlowContainer, text: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.pressed.connect(action)
	row.add_child(button)
	return button


## O tempo visto pelo motor: o ritmo, e os controles só quando valem agora (dormir e acelerar: só quem joga sozinho; acordar: só
## com o pulo em curso). O motivo de ter acordado vira aviso uma vez.
func apply_time(view: Dictionary) -> void:
	time_view = view
	var rhythm := String(view.get("rhythm", ""))
	if rhythm == "RealTime":
		rhythm_label.text = "Tempo real 1:%s" % _scale_text(float(view.get("timeScale", 1.0)))
	else:
		rhythm_label.text = RHYTHM_TEXT.get(rhythm, "")
	var skipping := bool(view.get("skipping", false))
	var can_skip := bool(view.get("canSkip", false)) and not skipping and is_active
	sleep_hour.max_value = hours_per_day - 1
	sleep_hour.visible = can_skip
	sleep_until_button.visible = can_skip
	sleep_fast_button.visible = can_skip
	accelerate_button.visible = can_skip
	wake_button.visible = skipping and is_active
	wake_button.text = "Acordar" if bool(view.get("sleeping", false)) else "Parar de acelerar"
	var reason := String(view.get("lastWakeReason", "")) if view.get("lastWakeReason") != null else ""
	if reason != _last_wake:
		_last_wake = reason
		if WAKE_TEXT.has(reason):
			_set_status(WAKE_TEXT[reason])


func _scale_text(scale: float) -> String:
	return str(int(scale)) if is_equal_approx(scale, roundf(scale)) else "%.2f" % scale


## Pede ao motor o tempo do jogador (o app chama junto com o relógio); inativo, não pede nada.
func refresh_time() -> void:
	if not is_active or _busy:
		return
	var response: Dictionary = await api.player_time(player_id)
	if response.ok and response.body is Dictionary and is_active:
		apply_time(response.body)


## Dormir até a hora do dia `hour`, ou, com −1, acelerando até descansar. O motor recusa (409) com outro jogador no mundo.
func sleep(hour: int) -> void:
	await _time_command(func(): return await api.player_sleep(player_id, hour), "Dormindo.")


func accelerate() -> void:
	await _time_command(func(): return await api.player_accelerate(player_id), "Tempo acelerado: você fica parado vendo o mundo.")


func wake() -> void:
	await _time_command(func(): return await api.player_wake(player_id), "")


func _time_command(request_fn: Callable, note: String) -> void:
	if not is_active or _busy:
		return
	_busy = true
	var response: Dictionary = await request_fn.call()
	_busy = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		apply_time(response.body)
		if note != "":
			_set_status(note)
	elif response.status == 409:
		warning.emit("Só quem joga sozinho pode pular o tempo agora.")
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		warning.emit("Não foi possível mudar o tempo (erro %d)." % response.status)


## Rótulos dos botões vêm do catálogo do servidor (o cliente não escreve texto de ação).
func refresh_labels() -> void:
	for choice in CHOICES:
		var name: String = choice[0]
		var label: String = labels.label_for(int(choice[1])) if labels != null else ""
		action_buttons[name].text = label if label != "" else name


## Encarna o NPC; devolve true se o servidor aceitou.
func incarnate(id: int, name: String) -> bool:
	if _busy or is_active:
		return false # outro pedido em andamento ou já encarnado: um clique a mais não vira segundo POST
	_busy = true
	await _ensure_session()
	var response: Dictionary = await api.incarnate(id, player_id)
	_busy = false
	if response.ok:
		stale_session = false
		_set_actions_enabled(true)
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
	var message := _incarnate_error(response)
	if response.status == 409 and "outro NPC" in message:
		_offer_release()
	warning.emit(message)
	return false


## ADR-0048: a identidade vem da sessão do servidor (token), não do id local; depois de conectado os comandos vão pelo canal
## persistente. Servidor sem `/sessions` (antigo): segue no modo local com o id de `user://player.cfg`.
func _ensure_session() -> void:
	if not session_enabled or has_session:
		return
	var session: Dictionary = await api.create_session()
	if not session.ok:
		return
	has_session = true
	player_id = String(session.body.get("playerId", player_id))
	api.open_player_channel()


## Os poderes do NPC encarnado (`powerIds` da ficha do servidor): um botão por poder. O motor decide se a invocação cabe
## agora; sem oportunidade o personagem fica parado, como qualquer NPC.
func set_powers(power_ids: Array) -> void:
	for id in power_buttons:
		power_buttons[id].queue_free()
	power_buttons.clear()
	for raw in power_ids:
		var id := String(raw)
		var button := Button.new()
		button.toggle_mode = true
		button.text = "Usar poder: %s" % id
		button.pressed.connect(func(): act("UsePower", id))
		_powers_row.add_child(button)
		power_buttons[id] = button


func act(action: String, power_id: String = "") -> void:
	if not is_active or _busy:
		return
	_busy = true
	var response: Dictionary = await api.player_action(player_id, action, power_id)
	_busy = false
	if not is_active:
		return
	if response.ok:
		current_action = "UsePower:" + power_id if action == "UsePower" else action
		_sync_buttons()
		_set_status("Pedido: %s." % (action_text(current_action) if action != "" else "o motor decide"))
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	elif response.status == 400:
		warning.emit("Esta ação não é permitida agora (o servidor recusou).")
		_sync_buttons()
	else:
		warning.emit("Não foi possível pedir a ação (erro %d)." % response.status)
		_sync_buttons()


## AD-197d: WASD/setas andam com o personagem relativo à câmera (W para onde ela olha), uma célula por passo pelo comando `move`
## (intenção, não posição); segurar a tecla anda no passo do corpo. O cliente prevê o passo e corrige pela posição que o motor
## responde; a recusa (parede, porta, alguém no caminho) volta o corpo. A latência de cada comando fica medida.
const STEP_KEYS := {KEY_W: Vector2i(0, -1), KEY_UP: Vector2i(0, -1), KEY_S: Vector2i(0, 1), KEY_DOWN: Vector2i(0, 1),
	KEY_A: Vector2i(-1, 0), KEY_LEFT: Vector2i(-1, 0), KEY_D: Vector2i(1, 0), KEY_RIGHT: Vector2i(1, 0)}
const STEP_INTERVAL := 0.35 ## s entre passos com a tecla segurada (andar; o motor ainda não limita a velocidade, ver issue)
var heading: Callable = func() -> Vector2: return Vector2(0, -1) ## para onde a câmera olha no chão (x, z); o app liga o rig
var _stepping := false
var _cooldown := 0.0
var latencies: Array[float] = [] ## ms do pedido à resposta do motor, os últimos 64
var offers: Array = [] ## a última oferta do motor (E)
var menu: VBoxContainer ## as interações que o motor ofereceu; nada além delas


## Teclas apertadas (W para frente, D para a direita) viram uma direção de grade pelo olhar da câmera, nas 8 direções.
func intent_for(keys: Vector2i) -> Vector2i:
	if keys == Vector2i.ZERO:
		return Vector2i.ZERO
	var forward: Vector2 = heading.call()
	var right := Vector2(-forward.y, forward.x)
	var world := forward * float(-keys.y) + right * float(keys.x)
	var octant := roundi(world.angle() / (PI / 4.0))
	var snapped := Vector2.RIGHT.rotated(octant * PI / 4.0)
	return Vector2i(roundi(snapped.x), roundi(snapped.y))


func _held_keys() -> Vector2i:
	var keys := Vector2i.ZERO
	for code in STEP_KEYS:
		if Input.is_key_pressed(code):
			keys += STEP_KEYS[code]
	return keys.clamp(Vector2i(-1, -1), Vector2i(1, 1))


func _process(delta: float) -> void:
	_cooldown = maxf(0.0, _cooldown - delta)
	if not is_active or _stepping or _cooldown > 0.0:
		return
	var direction := intent_for(_held_keys())
	if direction != Vector2i.ZERO:
		step(direction.x, direction.y)


func _unhandled_input(event: InputEvent) -> void:
	if not is_active:
		return
	if event is InputEventKey and event.pressed and STEP_KEYS.has(event.keycode):
		get_viewport().set_input_as_handled()
		if not event.echo:
			var direction := intent_for(STEP_KEYS[event.keycode])
			step(direction.x, direction.y)
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_E:
		get_viewport().set_input_as_handled()
		if menu != null and menu.visible:
			close_menu()
		else:
			open_interactions()


## Um passo; um pedido por vez (tecla segurada não enfileira passos que o motor ainda não respondeu).
func step(dx: int, dy: int) -> void:
	if not is_active or _stepping or (dx == 0 and dy == 0):
		return
	_stepping = true
	_cooldown = STEP_INTERVAL
	predicted.emit(Vector2i(dx, dy))
	var started := Time.get_ticks_usec()
	var response: Dictionary = await api.player_move(player_id, dx, dy)
	_note_latency(started)
	_stepping = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		confirmed.emit(int(response.body.get("x", 0)), int(response.body.get("y", 0)))
		return
	rejected.emit()
	if response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	elif response.status == 409:
		warning.emit("Mundo pausado: o passo só anda quando o tempo voltar.")
	elif response.status != 400:
		warning.emit("Não foi possível andar (erro %d)." % response.status)


func _note_latency(started: int) -> void:
	latencies.append((Time.get_ticks_usec() - started) / 1000.0)
	if latencies.size() > 64:
		latencies.pop_front()


## Mediana da latência dos últimos comandos (ms), 0 sem medida.
func latency_median_ms() -> float:
	if latencies.is_empty():
		return 0.0
	var sorted := latencies.duplicate()
	sorted.sort()
	return sorted[sorted.size() / 2]


const KIND_TEXT := {"door": "Porta", "stock": "Despensa", "stall": "Banca", "bed": "Cama", "person": "", "house": "Sua casa"}
const VERB_TEXT := {"enter": "Entrar", "exit": "Sair", "take": "Pegar", "deposit": "Guardar o que leva", "buy": "Comprar",
	"sell": "Vender", "sleep": "Dormir", "talk": "Conversar", "strike": "Bater", "abandon": "Abandonar"}


## E: pede ao motor o que o corpo alcança e mostra só isso (uma linha por coisa e verbo; por recurso, na despensa e na banca).
func open_interactions() -> void:
	if not is_active or _busy:
		return
	_busy = true
	var started := Time.get_ticks_usec()
	var response: Dictionary = await api.player_reach(player_id)
	_note_latency(started)
	_busy = false
	if not is_active:
		return
	if not response.ok or not (response.body is Dictionary):
		if response.status == 404:
			_lost("Você não controla mais %s." % npc_name)
		return
	offers = response.body.get("offers", [])
	show_menu(menu_entries(offers))


## As linhas do menu, todas tiradas da oferta: {text, kind, id, verb, resource}.
static func menu_entries(from: Array) -> Array:
	var entries: Array = []
	for offer in from:
		var kind := String(offer.get("kind", ""))
		var title := String(offer.get("label", "")) if kind == "person" else String(KIND_TEXT.get(kind, kind))
		for verb in offer.get("verbs", []):
			var resources: Array = offer.get("resources", [])
			if String(verb) in ["buy", "take"] and not resources.is_empty():
				for item in resources:
					if int(item.get("quantity", 0)) <= 0:
						continue
					var price := int(item.get("price", 0))
					entries.append({"text": "%s: %s recurso %d%s" % [title, VERB_TEXT[verb], int(item.resourceId),
						" (%d)" % price if price > 0 else ""], "kind": kind, "id": int(offer.id), "verb": String(verb),
						"resource": int(item.resourceId)})
			else:
				entries.append({"text": "%s: %s" % [title, VERB_TEXT.get(String(verb), String(verb))], "kind": kind,
					"id": int(offer.id), "verb": String(verb), "resource": -1})
	return entries


func show_menu(entries: Array) -> void:
	if menu == null:
		menu = VBoxContainer.new()
		add_child(menu)
	for child in menu.get_children():
		child.queue_free()
	if entries.is_empty():
		var none := Label.new()
		none.text = "Nada ao alcance."
		menu.add_child(none)
	for entry in entries:
		var button := Button.new()
		button.text = entry.text
		button.pressed.connect(func(): interact(entry))
		menu.add_child(button)
	menu.visible = true


func close_menu() -> void:
	if menu != null:
		menu.visible = false


## Pede a interação ao motor; a recusa da pessoa possuída (409 com o motivo) vira aviso, e a conversa aceita abre o chat.
func interact(entry: Dictionary) -> void:
	if not is_active or _busy:
		return
	close_menu()
	_busy = true
	var started := Time.get_ticks_usec()
	var response: Dictionary = await api.player_interact(player_id, entry.kind, entry.id, entry.verb, entry.get("resource", -1))
	_note_latency(started)
	_busy = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		var pose = response.body.get("pose")
		if pose is Dictionary:
			confirmed.emit(int(pose.get("x", 0)), int(pose.get("y", 0)))
		if response.body.get("sessionId") != null:
			conversation_opened.emit(int(entry.id), _offer_label(entry), int(response.body.sessionId))
		_set_status(String(entry.get("text", "")))
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
		warning.emit(error.trim_prefix("conflict: recusa: ").trim_prefix("conflict: ") if error != "" else "O motor recusou (erro %d)." % response.status)


func _offer_label(entry: Dictionary) -> String:
	for offer in offers:
		if String(offer.get("kind", "")) == entry.kind and int(offer.get("id", -1)) == int(entry.id):
			return String(offer.get("label", ""))
	return ""


## Clique: golpe no NPC sob a mira; o motor decide alcance e vontade.
func strike(target: int) -> void:
	if not is_active or _busy or target < 0 or target == npc_id:
		return
	_busy = true
	var started := Time.get_ticks_usec()
	var response: Dictionary = await api.player_strike(player_id, target)
	_note_latency(started)
	_busy = false
	if not is_active or response.ok:
		return
	var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
	warning.emit(error.trim_prefix("conflict: recusa: ") if error != "" else "O golpe não saiu (erro %d)." % response.status)


func logout() -> void:
	if (not is_active and not stale_session) or _busy:
		return
	_busy = true
	var response: Dictionary = await api.player_logout(player_id)
	_busy = false
	if stale_session and not is_active:
		if response.ok or response.status == 404:
			stale_session = false
			logout_button.text = "Sair do personagem"
			_set_actions_enabled(true)
			visible = false
			warning.emit("Personagem anterior liberado. Pode encarnar de novo.")
		else:
			warning.emit("Não foi possível sair do personagem anterior (erro %d)." % response.status)
		return
	if response.ok and response.body is Dictionary:
		_lost("%s desapareceu do mundo no tick %d. Não ficou congelado: o mundo seguiu." % [npc_name, WorldTime.hours_of(response.body, "disappearedAtMs")])
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		warning.emit("Não foi possível sair (erro %d)." % response.status)


## O servidor lembra deste `playerId` num NPC (o cliente reiniciou sem sair): oferece só "Sair do personagem anterior".
func _offer_release() -> void:
	stale_session = true
	visible = true
	_title.text = "Sessão anterior ativa"
	logout_button.text = "Sair do personagem anterior"
	_set_actions_enabled(false)
	_set_status("Este jogador ainda controla outro NPC no servidor. Saia dele para encarnar de novo.")


func _set_actions_enabled(enabled: bool) -> void:
	for name in action_buttons:
		action_buttons[name].disabled = not enabled
	for id in power_buttons:
		power_buttons[id].disabled = not enabled
	auto_button.disabled = not enabled


func set_paused(value: bool) -> void:
	paused = value
	_pause.visible = value and is_active


func action_text(action: String) -> String:
	if action.begins_with("UsePower:") and power_buttons.has(action.substr(9)):
		return power_buttons[action.substr(9)].text
	return action_buttons[action].text if action_buttons.has(action) else action


func _lost(text: String) -> void:
	is_active = false
	apply_time({})
	current_action = ""
	set_powers([])
	_pause.visible = false
	_title.text = "Sem personagem"
	_set_status(text)
	released.emit()
	visible = true # a mensagem de saída fica à vista até o próximo NPC


func _sync_buttons() -> void:
	for name in action_buttons:
		action_buttons[name].set_pressed_no_signal(name == current_action)
	for id in power_buttons:
		power_buttons[id].set_pressed_no_signal("UsePower:" + id == current_action)
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
