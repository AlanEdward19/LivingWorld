extends PanelContainer
## HUD do jogador encarnado (`/players/*`): o NPC controlado, a barra de ações que o motor aceita e a saída.
## O motor decide se a ação cabe: aqui só se pede. Sair não congela o personagem: ele desaparece do mundo.
const WorldTime := preload("res://Contract/world_time.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")

signal released()
signal warning(text: String)
## AD-197d: previsão e correção do passo (o cliente anda já e corrige pela posição do motor), a conversa aberta pelo motor.
signal predicted(delta: Vector2i)
signal confirmed(x: int, y: int)
signal rejected()
signal conversation_opened(npc_id: int, name: String, session_id: int)
signal struck(target: int) ## AD-197j: o golpe saiu (o app anima o braço)
## AD-197k: passo solto: o corpo anda já na tela pelo rumo e pela velocidade do motor e corrige pela posição fina que ele responde.
signal glided(position: Vector2) ## posição fina prevista (células, absoluta)
signal settled(position: Vector2) ## posição fina do motor
signal menu_toggled(open: bool) ## o menu de interações abriu ou fechou (o app solta e prende o cursor)
signal acted(verb: String) ## #224: o motor aceitou o verbo (o app toca o gesto no corpo)
signal jumped(air_s: float) ## #222: o motor deixou o corpo pular por `air_s` segundos reais (o app anima o salto)

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
## AD-197p: no modo jogador (3ª/1ª pessoa) o topo é compacto: nome, ritmo, menu e sair. As ordens (ações, poderes, dormir e
## acelerar) ficam num menu que solta o cursor (Tab ou o botão); a mira mostra só o nome curto do que o E alcançaria, da oferta do motor.
var rpg := false
var orders: VBoxContainer
var orders_button: Button
var reach_hint: Label
var _orders_open := false
var _reach_pending := false
var reach_clock := 0.0 ## segundos desde a última oferta pedida para a dica da mira
var _reach_at := Vector2.INF ## onde o corpo estava na última oferta da dica
const REACH_HINT_EVERY := 1.0 ## no máximo uma oferta por segundo, e só depois de o corpo andar
var frame: Control ## #222: a moldura de jogo (PlayerFrame) do modo jogador; o app liga
var header: HFlowContainer
var _body_clock := 0.0
var _body_pending := false
const BODY_EVERY := 2.0 ## s entre leituras da ficha do corpo para as barras (só no modo jogador)
var _paused_note_clock := 0.0
var session_enabled := false ## AD-197a: pede sessão (token) e abre o canal persistente antes de encarnar; o app liga
var has_session := false


func _init() -> void:
	visible = false
	var box := VBoxContainer.new()
	add_child(box)
	header = HFlowContainer.new() # AD-197p: nome, ritmo, menu e sair; o resto fica nas ordens
	box.add_child(header)
	_title = Label.new()
	_title.add_theme_font_size_override("font_size", 18)
	_title.custom_minimum_size.x = 150
	header.add_child(_title)
	rhythm_label = Label.new()
	rhythm_label.custom_minimum_size.x = 120
	header.add_child(rhythm_label)
	orders_button = Button.new()
	orders_button.text = "Ordens (Tab)"
	orders_button.visible = false
	orders_button.pressed.connect(func(): toggle_orders())
	header.add_child(orders_button)
	orders = VBoxContainer.new()
	box.add_child(orders)
	var row := HFlowContainer.new() # faixa sob a barra do topo; quebra de linha se faltar largura
	orders.add_child(row)
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
	header.add_child(logout_button)
	_powers_row = HFlowContainer.new()
	orders.add_child(_powers_row)
	_build_time_row(orders)
	reach_hint = Label.new()
	reach_hint.visible = false
	reach_hint.mouse_filter = Control.MOUSE_FILTER_IGNORE
	reach_hint.add_theme_font_size_override("font_size", 14)
	reach_hint.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	_status = Label.new()
	_status.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_status.custom_minimum_size.x = 560
	_status.modulate = Color(1, 1, 1, 0.8)
	_status.visible = false
	box.add_child(_status)
	refresh_labels()


func _build_time_row(box: VBoxContainer) -> void:
	var row := HFlowContainer.new()
	box.add_child(row)
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


## AD-197p: liga ou desliga o modo jogador (o app chama na troca de câmera e ao sair). Ligado, as ordens fecham e o topo fica compacto;
## desligado, tudo como antes (o HUD do espectador que encarna pela isométrica).
func set_rpg(on: bool) -> void:
	rpg = on and is_active
	orders_button.visible = rpg and not PLAYER_COMMANDS_ONLY
	refresh_labels()
	header.visible = not rpg # #222: no modo jogador o topo some; nome e barras ficam na moldura, sair fica no menu (Esc)
	if frame != null:
		frame.visible = rpg
		if not rpg:
			frame.close_menu()
		else:
			_body_clock = BODY_EVERY
	if not rpg:
		reach_hint.visible = false
		reach_hint.text = ""
	if _orders_open and not rpg:
		_orders_open = false
		menu_toggled.emit(false)
	orders.visible = not rpg or _orders_open
	_show_in_rpg()


## #222: no modo jogador o painel só aparece com as ações abertas (Tab), ou com algo a dizer.
func _show_in_rpg() -> void:
	if rpg:
		visible = _orders_open or _status.visible or _pause.visible
	elif is_active or stale_session:
		visible = true


## Tab ou o botão: abre ou fecha as ordens (abertas, o cursor fica solto para clicar). #222: no modo jogador não há painel de
## ordens: o corpo só recebe comandos (teclas, mouse e o menu do E, onde comer, dormir e comprar vêm da oferta do motor).
func toggle_orders() -> void:
	if not rpg or PLAYER_COMMANDS_ONLY:
		return
	_orders_open = not _orders_open
	orders.visible = _orders_open
	_show_in_rpg()
	menu_toggled.emit(_orders_open)


func orders_open() -> bool:
	return _orders_open


## A dica da mira: o nome curto da primeira coisa que o E alcançaria, tal como o motor a ofereceu (nada inventado aqui).
func set_reach_hint(from: Array) -> void:
	var entries := PlayerFrame.game_entries(menu_entries(from, resource_name_of), from, fine)
	reach_hint.text = String(entries[0].text) if not entries.is_empty() else ""
	reach_hint.visible = rpg and reach_hint.text != "" and frame == null
	if frame != null:
		frame.set_prompt(reach_hint.text if rpg else "")


## Pede a oferta para a dica só depois de o corpo andar (nunca por quadro parado), no máximo uma vez por segundo.
func _refresh_reach_hint(delta: float) -> void:
	reach_clock += delta
	if not rpg or _busy or _reach_pending or reach_clock < REACH_HINT_EVERY or fine == Vector2.INF or fine.distance_to(_reach_at) < 0.5:
		return
	reach_clock = 0.0
	_reach_at = fine
	_reach_pending = true
	var response: Dictionary = await api.player_reach(player_id)
	_reach_pending = false
	if rpg and response.ok and response.body is Dictionary:
		set_reach_hint(response.body.get("offers", []))


## Rótulos dos botões vêm do catálogo do servidor (o cliente não escreve texto de ação). #222: no modo jogador o botão é uma ordem
## ("Comer"), não o estado ("Comendo").
const PLAYER_COMMANDS_ONLY := true
const ORDER_TEXT := {"Eat": "Comer", "Sleep": "Dormir", "Work": "Trabalhar", "Socialize": "Conversar", "Idle": "Descansar", "Buy": "Comprar"}


func refresh_labels() -> void:
	for choice in CHOICES:
		var name: String = choice[0]
		var label: String = labels.label_for(int(choice[1])) if labels != null else ""
		action_buttons[name].text = ORDER_TEXT[name] if rpg else (label if label != "" else name)


## Encarna o NPC; devolve true se o servidor aceitou.
func incarnate(id: int, name: String) -> bool:
	if _busy or is_active:
		return false # outro pedido em andamento ou já encarnado: um clique a mais não vira segundo POST
	_busy = true
	await _ensure_session()
	var response: Dictionary = await api.incarnate(id, player_id)
	_busy = false
	if response.ok:
		_became(id, name)
		return true
	var message := _incarnate_error(response)
	if response.status == 409 and "outro NPC" in message:
		_offer_release()
	warning.emit(message)
	return false


func _became(id: int, name: String) -> void:
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


## AD-197t: a prévia validada pelo motor (a sessão vem antes, como ao encarnar).
func character_preview(spec: Dictionary) -> Dictionary:
	await _ensure_session()
	return await api.player_character_preview(player_id, spec)


## AD-197t: cria a pessoa montada e a encarna. Devolve o corpo da resposta (`npcId`, `character`) ou, na recusa, `{error}`.
func create_character(spec: Dictionary) -> Dictionary:
	if _busy or is_active:
		return {"error": "Você já controla outro NPC." if is_active else ""}
	_busy = true
	await _ensure_session()
	var response: Dictionary = await api.player_character(player_id, spec)
	_busy = false
	if response.ok:
		_became(int(response.body.npcId), String(response.body.character.name))
		return response.body
	var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
	return {"error": error if error != "" else "Não foi possível criar a pessoa (erro %d)." % response.status}


## AD-197q: amostra do agregado da cidade para o criador de personagem (a sessão vem antes, como ao encarnar).
func candidates(city_id: String, filter: Dictionary) -> Dictionary:
	await _ensure_session()
	return await api.player_candidates(player_id, city_id, filter)


## AD-197q: entra como o candidato escolhido: o motor tira a pessoa do agregado e a encarna. Devolve "" se entrou, senão o motivo.
func enter_as_new(city_id: String, id: int, sample: int, name: String) -> String:
	if _busy or is_active:
		return "Você já controla outro NPC." if is_active else ""
	_busy = true
	await _ensure_session()
	var response: Dictionary = await api.player_create(player_id, city_id, id, sample)
	_busy = false
	if response.ok:
		_became(id, name)
		return ""
	var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
	return error if error != "" else "Não foi possível entrar (erro %d)." % response.status


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
var resource_name_of: Callable = func(id: int) -> String: return "recurso %d" % id ## AD-197j: o app liga o catálogo de recursos
var step_seconds := STEP_INTERVAL ## AD-197j: quanto o último passo durou no corpo (o motor diz `readyInMs`), em segundos reais
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
	if not is_active:
		return
	glide(walk_direction(_held_keys()), delta)
	_refresh_reach_hint(delta)
	_refresh_body(delta)
	_paused_note_clock = maxf(0.0, _paused_note_clock - delta)


## #222: as barras do corpo (saúde, fome, sede, sono) pela ficha do motor, a cada BODY_EVERY s, só no modo jogador.
func _refresh_body(delta: float) -> void:
	_body_clock += delta
	if not rpg or frame == null or _body_pending or _body_clock < BODY_EVERY:
		return
	_body_clock = 0.0
	_body_pending = true
	var response: Dictionary = await api.npc_detail(npc_id)
	_body_pending = false
	if rpg and response.ok and response.body is Dictionary:
		var body: Dictionary = response.body
		var action := ""
		if labels != null and body.get("currentAction") != null:
			action = labels.label_for(int(body.currentAction))
		frame.set_body(npc_name, action, body)


## AD-197k: o passo solto. Teclas apertadas viram um rumo contínuo pelo olhar da câmera (milésimos, sem arredondar a 8 direções).
const WALK_SEND := 0.1 ## s entre rumos enviados com a tecla segurada (o motor anda o corpo pelo tempo de mundo entre eles)
var fine := Vector2.INF ## posição fina do corpo (células, absoluta): a do motor, mais a previsão desde a última resposta
var speed_cells := 0.0 ## células por segundo real (m/s do motor / metros por célula, na escala do relógio)
var _walk_dir := Vector2.ZERO
var _walk_clock := 0.0
var _walk_busy := false


func walk_direction(keys: Vector2i) -> Vector2:
	if keys == Vector2i.ZERO:
		return Vector2.ZERO
	var forward: Vector2 = heading.call()
	var right := Vector2(-forward.y, forward.x)
	return (forward * float(-keys.y) + right * float(keys.x)).normalized()


## Anda a tela já (previsão) e manda o rumo ao motor a cada WALK_SEND, e uma vez ao parar.
func glide(direction: Vector2, delta: float) -> void:
	if fine != Vector2.INF and direction != Vector2.ZERO and speed_cells > 0.0:
		fine += direction * speed_cells * delta
		glided.emit(fine)
	_walk_clock += delta
	var run := direction != Vector2.ZERO and Input.is_key_pressed(KEY_SHIFT)
	var changed := direction != _walk_dir or run != _walk_run
	if (changed or (direction != Vector2.ZERO and _walk_clock >= WALK_SEND)) and not _walk_busy:
		_walk_clock = 0.0
		_walk_dir = direction
		_walk_run = run
		send_walk(direction, run)


var _walk_run := false
var running := false ## #222: o motor disse que o corpo está correndo (Shift); cansado, ele anda


func send_walk(direction: Vector2, run: bool = false) -> void:
	_walk_busy = true
	var started := Time.get_ticks_usec()
	var response: Dictionary = await api.player_walk(player_id, roundi(direction.x * 1000.0), roundi(direction.y * 1000.0), run)
	_note_latency(started)
	_walk_busy = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		apply_pose(response.body)
		running = bool(response.body.get("running", false))
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	elif direction != Vector2.ZERO:
		_refused_step(response)


## #222: o passo recusado não fica calado: mundo pausado diz como voltar; outra recusa diz o motivo do motor (no máximo a cada 3 s).
func _refused_step(response: Dictionary) -> void:
	if _paused_note_clock > 0.0:
		return
	_paused_note_clock = 3.0
	var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
	if response.status == 409 and (paused or "pausa" in error):
		warning.emit("O mundo está pausado: o corpo só anda com o tempo correndo (Esc → Continuar, ou ▶ na barra do topo).")
	elif error != "":
		warning.emit(error.trim_prefix("conflict: recusa: ").trim_prefix("conflict: ").trim_prefix("move: ").trim_prefix("jump: "))


## #222: Espaço pula; o motor decide se o corpo consegue (cansaço, ferimento, idade) e por quanto tempo fica no ar.
func jump() -> void:
	if not is_active or _jumping:
		return
	_jumping = true
	var response: Dictionary = await api.player_jump(player_id)
	_jumping = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		var scale := float(time_view.get("timeScale", 1.0)) if time_view.get("timeScale") != null else 1.0
		jumped.emit(float(response.body.get("airMs", 0)) / 1000.0 / maxf(scale, 0.001))
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		_refused_step(response)


var _jumping := false


## A posição fina do motor manda: a previsão volta para ela (o motor sabe da parede, da porta e de quem está no caminho).
func apply_pose(pose: Dictionary) -> void:
	if pose.has("fx"):
		fine = Vector2(float(pose.fx), float(pose.fy))
		var scale := float(time_view.get("timeScale", 1.0)) if time_view.get("timeScale") != null else 1.0
		speed_cells = float(pose.get("speedMps", 0.0)) / maxf(float(pose.get("cellMeters", 1.5)), 0.01) * scale
		settled.emit(fine)


func _unhandled_input(event: InputEvent) -> void:
	if not is_active:
		return
	if frame != null and frame.menu_open() and event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_ESCAPE or event.keycode == KEY_E:
			get_viewport().set_input_as_handled()
			close_menu()
			return
		var entry: Dictionary = frame.entry_for_key(event.keycode)
		if not entry.is_empty():
			get_viewport().set_input_as_handled()
			interact(entry)
			return
	if event is InputEventKey and STEP_KEYS.has(event.keycode):
		get_viewport().set_input_as_handled() # AD-197k: o passo solto anda pelo `_process` (tecla segurada), não por toque
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_SPACE and rpg:
		get_viewport().set_input_as_handled()
		jump()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_TAB and rpg:
		get_viewport().set_input_as_handled() # #222: sem painel de ordens no modo jogador (só comandos)
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
	_cooldown = step_seconds
	predicted.emit(Vector2i(dx, dy))
	var started := Time.get_ticks_usec()
	var response: Dictionary = await api.player_move(player_id, dx, dy)
	_note_latency(started)
	_stepping = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		_pace(response.body)
		confirmed.emit(int(response.body.get("x", 0)), int(response.body.get("y", 0)))
		return
	rejected.emit()
	if response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	elif response.status == 409:
		warning.emit("Mundo pausado: o passo só anda quando o tempo voltar.")
	elif response.status != 400:
		warning.emit("Não foi possível andar (erro %d)." % response.status)


## AD-197j: o próximo passo só depois do tempo do corpo (`readyInMs` de mundo, na escala do relógio): a tecla segurada anda nesse ritmo.
func _pace(pose: Dictionary) -> void:
	var scale := float(time_view.get("timeScale", 1.0)) if time_view.get("timeScale") != null else 1.0
	var seconds := float(pose.get("readyInMs", 0)) / 1000.0 / maxf(scale, 0.001)
	if seconds > 0.0:
		step_seconds = seconds
		_cooldown = seconds


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


const OBJECT_TEXT := {"table": "Mesa", "bench": "Banco", "crate": "Caixote", "barrel": "Barril", "counter": "Balcão", "shelf": "Prateleira",
	"bed": "Cama", "chest": "Baú", "anvil": "Bigorna"}
const KIND_TEXT := {"counter": "Balcão", "door": "Porta", "stock": "Despensa", "stall": "Banca", "bed": "Cama", "person": "", "house": "Sua casa",
	"stairs": "Escada", "water": "Fonte", "field": "Lavoura", "leaving": "Espólio"}
const VERB_TEXT := {"enter": "Entrar", "exit": "Sair", "take": "Pegar", "deposit": "Guardar o que leva", "buy": "Comprar",
	"sell": "Vender", "sleep": "Dormir", "talk": "Conversar", "strike": "Bater", "abandon": "Abandonar", "up": "Subir",
	"down": "Descer", "drink": "Beber", "fill": "Encher o cântaro", "work": "Trabalhar", "collect": "Recolher", "steal": "Furtar", "return": "Devolver", "eat": "Comer",
	"socialize": "Conversar", "idle": "Descansar"}


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
	if rpg and frame != null:
		frame.show_menu(PlayerFrame.game_entries(menu_entries(offers, resource_name_of), offers, fine))
		menu_toggled.emit(true)
		return
	show_menu(menu_entries(offers, resource_name_of))


## As linhas do menu, todas tiradas da oferta: {text, kind, id, verb, resource}; o nome do recurso vem do catálogo do servidor.
static func menu_entries(from: Array, name_of: Callable = Callable()) -> Array:
	var entries: Array = []
	for offer in from:
		var kind := String(offer.get("kind", ""))
		var title := String(offer.get("label", "")) if kind in ["person", "item", "object"] else String(KIND_TEXT.get(kind, kind)) # o nome vem do motor
		title = String(OBJECT_TEXT.get(title, title)) # #222: os móveis do catálogo padrão têm id em inglês ("counter")
		for verb in offer.get("verbs", []):
			var resources: Array = offer.get("resources", [])
			if String(verb) in ["buy", "take", "steal"] and not resources.is_empty():
				for item in resources:
					if int(item.get("quantity", 0)) <= 0:
						continue
					var price := int(item.get("price", 0))
					var resource_text: String = name_of.call(int(item.resourceId)) if name_of.is_valid() else "recurso %d" % int(item.resourceId)
					entries.append({"text": "%s: %s %s%s" % [title, VERB_TEXT[verb], resource_text,
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
	menu_toggled.emit(true)


func close_menu() -> void:
	if frame != null and frame.menu_open():
		frame.close_menu()
		menu_toggled.emit(false)
	if menu != null and menu.visible:
		menu.visible = false
		menu_toggled.emit(false)


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
		acted.emit(String(entry.verb))
		var pose = response.body.get("pose")
		if pose is Dictionary:
			_pace(pose)
			confirmed.emit(int(pose.get("x", 0)), int(pose.get("y", 0)))
		if response.body.get("sessionId") != null:
			conversation_opened.emit(int(entry.id), _offer_label(entry), int(response.body.sessionId))
		if rpg: # #222: o que aconteceu, num aviso curto (quanto levou e por quanto), sem abrir painel
			warning.emit(done_text(entry, response.body))
		else:
			_set_status(String(entry.get("text", "")))
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
		warning.emit(error.trim_prefix("conflict: recusa: ").trim_prefix("conflict: ") if error != "" else "O motor recusou (erro %d)." % response.status)


## #222: o retorno de uma interação aceita, com o que o motor respondeu (quantidade e valor quando houver).
static func done_text(entry: Dictionary, body: Dictionary) -> String:
	var text := String(entry.get("text", "")).get_slice(" (", 0)
	var quantity := int(body.get("quantity", 0)) if body.get("quantity") != null else 0
	var value := int(body.get("value", 0)) if body.get("value") != null else 0
	if quantity > 0:
		text += " · %d un." % quantity + (" por %d" % value if value > 0 else "")
	return "✓ " + text


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
	struck.emit(target) # o braço desce já (previsão); o motor diz se acertou
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
	_show_in_rpg()


func action_text(action: String) -> String:
	if action.begins_with("UsePower:") and power_buttons.has(action.substr(9)):
		return power_buttons[action.substr(9)].text
	return action_buttons[action].text if action_buttons.has(action) else action


func _lost(text: String) -> void:
	is_active = false
	set_rpg(false)
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
	_status.visible = text != "" # AD-197p: sem linha vazia no HUD compacto
	_show_in_rpg()


func _incarnate_error(response: Dictionary) -> String:
	var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
	match response.status:
		404:
			return "Este NPC morreu ou não existe: não dá para encarná-lo."
		409:
			if "outro NPC" in error:
				return "Você já controla outro NPC."
			if "pausa" in error or paused:
				return "O mundo está pausado: aperte ▶ na barra do topo para o tempo correr e encarne de novo."
			if "controlador" in error:
				return "Este NPC já é controlado por outro jogador."
			return error.trim_prefix("conflict: recusa: ").trim_prefix("conflict: ") if error != "" else "O motor recusou a posse."
	return "Não foi possível encarnar (erro %d)." % response.status
