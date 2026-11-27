extends PanelContainer
## HUD do jogador encarnado (`/players/*`): o NPC controlado, a barra de ações que o motor aceita e a saída.
## O motor decide se a ação cabe: aqui só se pede. Sair não congela o personagem: ele desaparece do mundo.
const WorldTime := preload("res://Contract/world_time.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const PlayerBody := preload("res://Scenes/player_body.gd")
const CombatView := preload("res://Scenes/combat_view.gd")
const BodyBusy := preload("res://Scenes/body_busy.gd")
const GroundGrid := preload("res://Scenes/ground_grid.gd")

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
signal target_changed(kind: String, id: int, cell: Vector2) ## #233: o alvo do E sob a mira (cell = INF: nenhum); o app realça
signal refused() ## #232: o motor recusou uma ação (o app toca só o som discreto de recusa)
signal struck_hit(target: int) ## #232: o motor aceitou o golpe
signal struck_result(target: int, body: Dictionary) ## #234: o que o motor devolveu ao golpe (outcome, damage, lethal, reaction)
signal defended(kind: String) ## #234: o motor aceitou a defesa ("Dodge" ou "Block")
signal guard_changed(kind: String) ## #234: a guarda do corpo mudou ("" = baixou)
signal incoming(outcome: String, damage: int, by: int) ## #234: golpe recebido pelo corpo (Hit, Dodged, Blocked, Missed)
signal placed(building_id: int, floor: int) ## #227: onde o motor diz que o corpo está: prédio (-1 = fora) e andar; o app entra e sai da casa em foco
signal sheet_read(body: Dictionary) ## #232: ficha do corpo lida do motor (números subindo pelo que mudou)
signal busy_changed(action: String, seconds: float) ## #241: o motor diz que o corpo ocupou-se de `action` por `seconds` reais ("" = acabou ou parou); o app anima em laço
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
const TASK_WAKE_TEXT := {"golpe": "Um golpe interrompeu o tempo acelerado.", "fogo": "O calor do fogo trouxe o tempo ao normal.",
	"fome": "A fome trouxe o tempo ao normal.", "sede": "A sede trouxe o tempo ao normal.", "chamado": "Alguém o chamou: o tempo voltou ao normal.",
	"outro jogador": "Outro jogador chegou: o tempo voltou ao normal."}
var time_view: Dictionary = {}
var world_rate := 0.0 ## #241: ms de mundo por ms real do ritmo em vigor, como o motor o diz no relógio (0 = ainda não sei)
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
const BODY_BUSY_EVERY := 1.0 ## #243: com uma ação rendendo item, a ficha é lida mais vezes: "+1 Madeira" aparece quando a unidade sai
var _paused_note_clock := 0.0
var full_possession := false ## #222: o app liga com o token de admin (posse plena, AD-197e)
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
		if busy.active() and TASK_WAKE_TEXT.has(reason): # #241: o salto era de uma ação longa, não de sono
			_set_status(TASK_WAKE_TEXT[reason])
		elif WAKE_TEXT.has(reason):
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
		target_changed.emit("", -1, Vector2.INF)
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
var _hint_from: Array = [] ## #243: a última oferta da dica, para escolher de novo o alvo quando a câmera gira
var _hint_look := Vector2.ZERO
const HINT_TURN := 0.996 ## cosseno entre o olhar da dica e o de agora (≈5°): girar mais que isso refaz a escolha do alvo


## A câmera girou o bastante desde a escolha do alvo (só com o olhar ligado).
func _look_turned() -> bool:
	var look: Vector2 = aim_look.call()
	return look.length() > 0.001 and (_hint_look.length() < 0.001 or look.normalized().dot(_hint_look.normalized()) < HINT_TURN)


func set_reach_hint(from: Array) -> void:
	_hint_from = from
	_hint_look = aim_look.call()
	var entries := without_own_door(PlayerFrame.game_entries(menu_entries(from, resource_name_of), from, fine, aim_look.call()), building_id)
	reach_hint.text = String(entries[0].text) if not entries.is_empty() else ""
	_hint_offer = {}
	if not entries.is_empty():
		for offer in from:
			if String(offer.get("kind", "")) == String(entries[0].kind) and int(offer.get("id", -1)) == int(entries[0].id):
				_hint_offer = offer
	reach_hint.visible = rpg and reach_hint.text != "" and frame == null and not popup_open()
	if frame != null:
		frame.set_prompt(reach_hint.text if rpg else "")
	if rpg:
		var target := Vector2.INF
		var kind := ""
		var id := -1
		if not entries.is_empty():
			kind = String(entries[0].kind)
			id = int(entries[0].id)
			for offer in from:
				if String(offer.get("kind", "")) == kind and int(offer.get("id", -1)) == id:
					target = Vector2(float(offer.get("x", 0)), float(offer.get("y", 0)))
					if offer.get("fx") != null and offer.get("fy") != null: # #241: o tronco da árvore (o anel soma meia célula ao centro)
						target = Vector2(float(offer.fx) - 0.5, float(offer.fy) - 0.5)
		target_changed.emit(kind, id, target)
	else:
		target_changed.emit("", -1, Vector2.INF)


## #237: a oferta da dica que o motor diz ter alcance (fonte, mata: `reachCells`, da posição fina à borda da célula) e a folga da
## latência: sai da tela no quadro em que o corpo passa disso, sem esperar a próxima leitura (que pode demorar com o motor carregado).
var _hint_offer: Dictionary = {}
const HINT_SLACK := 0.2 ## células: o motor lê a posição dele, até uma latência atrás da prevista aqui
const REACH_MOVE := 0.25 ## células andadas desde a última leitura para pedir outra
const REACH_HINT_NEAR_EVERY := 0.5 ## com uma dica de alcance na tela a leitura se renova mais depressa


## Da posição fina `at` (células) à borda do quadrado da célula (x, y) da oferta; 0 dentro dela.
static func reach_gap(at: Vector2, x: float, y: float) -> float:
	return Vector2(maxf(maxf(x - at.x, at.x - x - 1.0), 0.0), maxf(maxf(y - at.y, at.y - y - 1.0), 0.0)).length()


## A dica de alcance mostrada já não vale para onde o corpo está agora (só ofertas com `reachCells`).
func hint_out_of_reach() -> bool:
	if reach_hint.text == "" or fine == Vector2.INF or not _hint_offer.has("reachCells"):
		return false
	return reach_gap(fine, float(_hint_offer.get("x", 0)), float(_hint_offer.get("y", 0))) > float(_hint_offer.reachCells) + HINT_SLACK


## Pede a oferta para a dica só depois de o corpo andar (nunca por quadro parado), no máximo uma vez por segundo (meio segundo com
## uma dica de alcance na tela). A leitura que falha não deixa a dica velha: sem resposta do motor, a tela não afirma nada.
func _refresh_reach_hint(delta: float) -> void:
	reach_clock += delta
	if rpg and not _hint_from.is_empty() and not _busy and _look_turned():
		set_reach_hint(_hint_from) # #243: a câmera girou: o alvo do E é o que está sob a mira agora, sem nova leitura do motor
	if rpg and hint_out_of_reach():
		set_reach_hint([]) # #237: saiu do alcance: a dica some agora, e a próxima leitura já pode vir
		_reach_at = Vector2.INF
		reach_clock = REACH_HINT_EVERY
	var every := REACH_HINT_NEAR_EVERY if _hint_offer.has("reachCells") else REACH_HINT_EVERY
	if not rpg or _busy or _reach_pending or reach_clock < every or fine == Vector2.INF or fine.distance_to(_reach_at) < REACH_MOVE:
		return
	reach_clock = 0.0
	_reach_at = fine
	_reach_pending = true
	var response: Dictionary = await api.player_reach(player_id)
	_reach_pending = false
	if not rpg:
		return
	if response.ok and response.body is Dictionary:
		set_reach_hint(response.body.get("offers", []))
	else:
		set_reach_hint([])
		_reach_at = Vector2.INF


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
	# #222: quem tem o token de admin (o jogo local) possui em modo pleno (a pessoa não recusa ordens); sem ele, a disputa de vontade
	var response: Dictionary
	if full_possession and has_session:
		response = await api.player_possess(id)
	else:
		response = await api.incarnate(id, player_id)
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
	_offer_guide.call_deferred(false)
	stale_session = false
	_set_actions_enabled(true)
	npc_id = id
	npc_name = name
	_reset_combat()
	is_active = true
	ground = null # #248: o chão é o do corpo novo
	body.ground = null
	_inputs.clear()
	current_action = ""
	visible = true
	_title.text = "Você é %s" % name
	_set_status("")
	_sync_buttons()
	refresh_labels()
	building_id = -1


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
var aim_look: Callable = func() -> Vector2: return Vector2.ZERO ## #243: para onde a câmera olha no chão (x, z): a ação principal do E é a do alvo à frente; sem câmera ligada, sem mira (a ordem de sempre)
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


## #240: há popup central aberto (o registro único do app, via `input_blocked`).
func popup_open() -> bool:
	return input_blocked.is_valid() and input_blocked.call()


## #240: um campo de texto tem o foco (a conversa): as teclas são do texto, não do corpo.
func typing() -> bool:
	if not is_inside_tree():
		return false
	var owner := get_viewport().gui_get_focus_owner()
	return owner is LineEdit or owner is TextEdit


func _held_keys() -> Vector2i:
	var keys := Vector2i.ZERO
	for code in STEP_KEYS:
		if Input.is_key_pressed(code):
			keys += STEP_KEYS[code]
	return keys.clamp(Vector2i(-1, -1), Vector2i(1, 1))


func _process(delta: float) -> void:
	e_held(delta)
	_cooldown = maxf(0.0, _cooldown - delta)
	if not is_active:
		return
	var keys := Vector2i.ZERO if typing() else _held_keys()
	_refresh_busy(keys != Vector2i.ZERO, delta)
	glide(walk_direction(keys), delta) # #240: digitando num popup, WASD não anda o corpo
	_refresh_reach_hint(delta)
	_refresh_body(delta)
	_refresh_goals(delta)
	_refresh_goal_where(delta)
	_hold_guard()
	_paused_note_clock = maxf(0.0, _paused_note_clock - delta)


## #238: o guia da primeira posse neste mundo: abre sozinho uma vez (e lembra); `force` (F1 → "Rever o guia") abre sempre.
var settings: Variant = null ## GameSettings (nulo nos testes e nas medidas: sem guia sozinho)
var world_key := "" ## o mundo em que o jogador está (nome); o guia é por mundo


func _offer_guide(force: bool) -> void:
	var key := "guide:" + world_key
	if api == null or not api.has_method("player_goals") or frame == null or not is_active:
		return
	if not force and (settings == null or settings.has_seen_guide(key)):
		return
	var response: Dictionary = await api.player_goals(player_id, true)
	if not is_active or not response.ok or not response.body is Dictionary or not response.body.get("start") is Dictionary:
		return
	if not force and settings != null:
		settings.mark_guide_seen(key)
	frame.guide.open(response.body.start, npc_name, fine, heading.call(), resource_name_of)


func show_guide() -> void:
	await _offer_guide(true)


const GOAL_WHERE_EVERY := 0.5 ## s entre as releituras do lado em que fica o passo da meta (a câmera gira, o corpo anda)
var _where_clock := 0.0


func _refresh_goal_where(delta: float) -> void:
	_where_clock += delta
	if frame == null or _where_clock < GOAL_WHERE_EVERY:
		return
	_where_clock = 0.0
	frame.set_goal_where(fine, heading.call())


## #235: a meta mais urgente do motor na linha do HUD (a cada GOALS_EVERY s) e, com o painel J aberto, a lista e o diário.
const GOALS_EVERY := 5.0
var _goals_clock := GOALS_EVERY
var _goals_pending := false


func _refresh_goals(delta: float) -> void:
	_goals_clock += delta
	if not rpg or frame == null or _goals_pending or _goals_clock < GOALS_EVERY:
		return
	_goals_clock = 0.0
	await load_goals()


## Pede metas e (com o painel aberto) o diário ao motor; resposta ruim não mexe na tela.
func load_goals() -> void:
	if api == null or not api.has_method("player_goals"):
		return
	_goals_pending = true
	var goals: Dictionary = await api.player_goals(player_id)
	if rpg and goals.ok and goals.body is Dictionary:
		frame.set_goals(goals.body)
	if rpg and frame.goals_open():
		var diary: Dictionary = await api.player_diary(player_id)
		if rpg and diary.ok and diary.body is Dictionary:
			frame.set_diary(diary.body)
	_goals_pending = false


## J: abre ou fecha "Metas e diário", lido do motor na hora ao abrir.
func toggle_goals() -> void:
	if frame == null:
		return
	frame.toggle_goals()
	if frame.goals_open():
		_goals_clock = GOALS_EVERY


## #222: as barras do corpo (saúde, fome, sede, sono) pela ficha do motor, a cada BODY_EVERY s, só no modo jogador.
func _refresh_body(delta: float) -> void:
	_body_clock += delta
	if not rpg or frame == null or _body_pending or _body_clock < (BODY_BUSY_EVERY if busy.units > 0 else BODY_EVERY):
		return
	_body_clock = 0.0
	_body_pending = true
	if api.has_method("invalidate_npc"):
		api.invalidate_npc(npc_id) # #222: o cartão do jogador lê a ficha do motor na hora (dinheiro, fome, sede)
	var response: Dictionary = await api.npc_detail(npc_id)
	_body_pending = false
	if rpg and response.ok and response.body is Dictionary:
		var body: Dictionary = response.body
		var action := ""
		if labels != null and body.get("currentAction") != null:
			action = labels.label_for(int(body.currentAction))
		frame.set_body(npc_name, action, body)
		sheet_read.emit(body)
		frame.set_purse(body.get("money"), body.get("carrying"), resource_name_of)
		frame.set_inventory(body.get("money"), body.get("carrying"), resource_name_of)


## AD-197k: o passo solto. Teclas apertadas viram um rumo contínuo pelo olhar da câmera (milésimos, sem arredondar a 8 direções).
## #248: o motor anda o corpo a cada tick pelo rumo mantido; o cliente só renova o rumo (WALK_SEND é o "ainda estou aqui") e o manda NA HORA
## que muda. Cada comando tem número; a pose do motor devolve o último aplicado e o cliente reaplica o que o motor ainda não viu.
const WALK_SEND := 0.1 ## s entre rumos renovados com a tecla segurada (o motor para se ficar ~1 s sem notícia)
const TURN_SEND := 0.03 ## rad: mudança de rumo que vale mandar já
const GROUND_RADIUS := 8 ## células de cada lado da janela de chão pedida ao motor
const GROUND_EDGE := 3 ## pede outra janela quando o corpo chega a esta distância da borda
const GROUND_MIN_GAP := 0.4 ## s entre pedidos de janela
const MAX_OWD_MS := 400.0 ## teto da meia ida e volta usada na reaplicação
var _blocked := false
var _last_server := Vector2.INF
var _last_server_at := 0
var body := PlayerBody.new() ## #229: o controlador cinemático local (aceleração, colisão, reconciliação com o motor)
var neighborhood: Callable = Callable() ## #229: o app liga: (centro: Vector2) -> [obstáculos [[Rect2, topo]], pessoas [Vector2]], só ao redor do corpo
const NEIGHBORHOOD_EVERY := 0.25 ## s entre leituras da vizinhança (e ao mudar de célula)
var _near_clock := 0.0
var _near_cell := Vector2i(1 << 30, 1 << 30)
var _still_responses := 0
var fine := Vector2.INF ## posição fina do corpo (células, absoluta): a do motor, mais a previsão desde a última resposta
var speed_cells := 0.0 ## células por segundo real (m/s do motor / metros por célula, na escala do relógio)
var _walk_dir := Vector2.ZERO
var _walk_clock := 0.0
var _walk_busy := false
var ground = null ## #248: a máscara de passos que o motor deu (GroundGrid); nula até a primeira resposta
var _ground_busy := false
var _ground_clock := 0.0
var _cell_meters := 1.5
var _walk_seq := 0 ## #248: número do último comando de andar mandado
var _inputs: Array = [] ## #248: os últimos comandos de andar: {seq, dir, at (ms)}
## #248: medida do passo: quadros, puxões (um quadro que desloca o corpo mais de 0,3 m além do passo), o que a correção absorveu e saltos
var walk_stats := {"frames": 0, "pulls": 0, "pull_m": 0.0, "replies": 0, "snaps": 0, "worst_m": 0.0, "back_m": 0.0, "back_events": 0, "gap_mean_m": 0.0, "gap_worst_m": 0.0}
var _back_run := 0.0


func walk_direction(keys: Vector2i) -> Vector2:
	if keys == Vector2i.ZERO:
		return Vector2.ZERO
	var forward: Vector2 = heading.call()
	var right := Vector2(-forward.y, forward.x)
	return (forward * float(-keys.y) + right * float(keys.x)).normalized()


const MOVING_SPEED := 0.05 ## células/s: acima disto o corpo "anda" para o cartão


## Anda a tela já (previsão) e renova o rumo ao motor: na hora se mudou, a cada WALK_SEND se segura, uma vez ao parar.
func glide(direction: Vector2, delta: float) -> void:
	if busy.active():
		direction = Vector2.ZERO # #241: ocupado, o corpo não anda (o motor recusaria); não manda passo
	var run_now := Input.is_key_pressed(KEY_SHIFT) and running
	if direction != _walk_dir:
		_blocked = false # #222: outro rumo: volta a prever
	if fine != Vector2.INF:
		_ensure_ground(delta)
	if fine != Vector2.INF and (speed_cells > 0.0 or body.settling()):
		if body.pos != fine:
			body.place(fine) # a posição fina foi posta de fora (ex.: o app): o corpo parte dela
		_refresh_neighborhood(delta)
		var before := body.pos
		var on_ground: bool = body.ground != null and body.ground.is_valid()
		body.advance(Vector2.ZERO if (_blocked and not on_ground) else direction, speed_cells, delta)
		_note_frame(before, body.pos, delta, direction)
		if body.pos != fine:
			fine = body.pos
			glided.emit(fine)
	if frame != null: # #228: o cartão diz como o corpo se move agora (previsão local); parado, volta à ação do motor
		frame.set_motion(("Correndo" if run_now else "Andando") if body.velocity.length() > MOVING_SPEED else "")
	_walk_clock += delta
	var run := direction != Vector2.ZERO and Input.is_key_pressed(KEY_SHIFT)
	var changed := _turned(direction) or run != _walk_run
	if (changed or (direction != Vector2.ZERO and _walk_clock >= WALK_SEND)) and not _walk_busy:
		_walk_clock = 0.0
		_walk_dir = direction
		_walk_run = run
		send_walk(direction, run)


## #248: o relógio (ms) das respostas e dos comandos; os testes põem um relógio simulado.
var clock: Callable = Callable()


func _now() -> int:
	return int(clock.call()) if clock.is_valid() else Time.get_ticks_msec()


## #248: o rumo mudou o bastante para valer mandar já (parar e arrancar sempre valem)?
func _turned(direction: Vector2) -> bool:
	if (direction == Vector2.ZERO) != (_walk_dir == Vector2.ZERO):
		return true
	return direction != Vector2.ZERO and absf(direction.angle_to(_walk_dir)) > TURN_SEND


## #248: mede o quadro: puxão = deslocamento do corpo na tela maior que o passo previsto mais 0,3 m (correção que se vê, ou salto).
func _note_frame(before: Vector2, after: Vector2, delta: float, direction: Vector2) -> void:
	var walking := direction != Vector2.ZERO
	walk_stats.frames += 1
	var cell_m := maxf(float(_cell_meters), 0.01)
	var allowed := (speed_cells * delta * 1.1 if walking else 0.0) + 0.002
	var excess_m := maxf(after.distance_to(before) - allowed, 0.0) * cell_m
	walk_stats.worst_m = maxf(float(walk_stats.worst_m), excess_m)
	if excess_m > 0.3:
		walk_stats.pulls += 1
		walk_stats.pull_m += excess_m
	var along := (after - before).dot(direction) if walking else 0.0 # recuo: o corpo anda contra o rumo que o jogador segura
	if along < -0.0005:
		_back_run += -along
		walk_stats.back_m += -along * cell_m
	elif _back_run > 0.0 and along > 0.0005:
		if _back_run * cell_m > 0.1:
			walk_stats.back_events += 1
		_back_run = 0.0


## #248: pede a janela de chão ao motor quando falta, o corpo chega perto da borda ou o andar/prédio mudou.
func _ensure_ground(delta: float) -> void:
	_ground_clock += delta
	if _ground_busy or not is_active or _ground_clock < GROUND_MIN_GAP:
		return
	var cell := Vector2i(floori(fine.x), floori(fine.y))
	if ground != null and ground.is_valid() and ground.covers(cell, GROUND_EDGE) and ground.floor_level == floor_level and ground.building_id == building_id:
		return
	_ground_clock = 0.0
	_fetch_ground()


func _fetch_ground() -> void:
	_ground_busy = true
	var response: Dictionary = await api.player_ground(player_id, GROUND_RADIUS)
	_ground_busy = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		var grid = GroundGrid.from_body(response.body)
		if grid.is_valid():
			ground = grid
			body.ground = grid


## #229: a vizinhança do corpo (paredes e gente por perto) só é relida a cada NEIGHBORHOOD_EVERY s ou ao mudar de célula.
func _refresh_neighborhood(delta: float) -> void:
	_near_clock += delta
	var cell := Vector2i(floori(body.pos.x), floori(body.pos.y))
	if not neighborhood.is_valid() or (cell == _near_cell and _near_clock < NEIGHBORHOOD_EVERY):
		return
	_near_cell = cell
	_near_clock = 0.0
	var near: Array = neighborhood.call(body.pos)
	body.set_neighborhood(near[0], near[1])


var _walk_run := false
var floor_level := 0 ## #227: o andar em que o motor diz que o corpo está
var building_id := -1 ## #227: o prédio em que o motor diz que o corpo está (-1 fora ou ainda sem pose)
var running := false ## #222: o motor disse que o corpo está correndo (Shift); cansado, ele anda


## #227: pergunta ao motor a pose agora (parado): é ela que diz se o corpo está num prédio (o app pede quando o desenho sugere que sim).
func sync_pose() -> void:
	if is_active and not _walk_busy:
		send_walk(_walk_dir, _walk_run) # #248: pergunta a pose sem mudar o rumo que o corpo segue


func send_walk(direction: Vector2, run: bool = false) -> void:
	_walk_busy = true
	var started := Time.get_ticks_usec()
	_walk_seq += 1
	var seq := _walk_seq
	_inputs.append({"seq": seq, "dir": direction, "at": _now()})
	while _inputs.size() > 32:
		_inputs.pop_front()
	var response: Dictionary = await api.player_walk(player_id, roundi(direction.x * 1000.0), roundi(direction.y * 1000.0), run, seq)
	_note_latency(started)
	_walk_busy = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		apply_pose(response.body, true)
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
		warning.emit(BodyBusy.human_error(error))


## #222: Espaço pula; o motor decide se o corpo consegue (cansaço, ferimento, idade) e por quanto tempo fica no ar.
func jump() -> void:
	if not is_active or _jumping:
		return
	if busy.active():
		interrupt() # #241: ocupado, Espaço para a ação (o motor decide o que ficou feito)
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
var _interrupting := false

## #241: a ação que ocupa o corpo, como o motor diz (`busy` da pose). A barra, o gesto em laço e o bloqueio do WASD saem daqui.
var busy := BodyBusy.new()
var _busy_note_clock := 0.0


func _time_scale() -> float:
	return float(time_view.get("timeScale", 1.0)) if time_view.get("timeScale") != null else 1.0


## O ritmo do relógio do motor (`hours_per_second` do status): quanto do mundo passa por segundo real agora. O mundo acelera para a
## ação longa de quem joga sozinho e volta ao normal ao acabar: a barra segue o ritmo e o rótulo diz "tempo acelerado".
func set_world_rate(hours_per_second: float) -> void:
	world_rate = hours_per_second * 3600.0
	if busy.active() and world_rate > 0.0:
		busy.retime(float(Time.get_ticks_msec()), world_rate, _time_scale())
		if frame != null:
			frame.set_busy(busy.text(float(Time.get_ticks_msec())), busy.fraction(float(Time.get_ticks_msec())))


func _note_busy(pose: Dictionary) -> void:
	var now := float(Time.get_ticks_msec())
	var before := busy.action
	if busy.note_pose(pose, now, world_rate if world_rate > 0.0 else _time_scale()):
		busy_changed.emit(busy.action, busy.seconds_left(now))
		if before != "" and busy.action == "":
			_busy_ended()
	if frame != null:
		frame.set_busy(busy.text(now) if busy.active() else "", busy.fraction(now))


func _busy_ended() -> void:
	_body_clock = BODY_EVERY # o que ficou nas mãos aparece no cartão


## Barra e fim da tarefa; WASD durante ela diz por que não anda (frase humana, no máximo a cada 2,5 s).
func _refresh_busy(wants_to_walk: bool, delta: float) -> void:
	_busy_note_clock = maxf(0.0, _busy_note_clock - delta)
	if not busy.active():
		return
	var now := float(Time.get_ticks_msec())
	if busy.expired(now):
		busy.clear()
		busy_changed.emit("", 0.0)
		if frame != null:
			frame.set_busy("", 0.0)
		_busy_ended()
		sync_pose() # confirma com o motor que o corpo voltou a andar
		return
	if frame != null:
		frame.set_busy(busy.text(now), busy.fraction(now))
	if wants_to_walk and _busy_note_clock <= 0.0:
		_busy_note_clock = 2.5
		warning.emit(busy.blocked_note())


## #241: para a ação em andamento (Espaço); o motor decide o que ficou feito (a parte proporcional) e devolve a pose.
func interrupt() -> void:
	if not is_active or _interrupting or not busy.active():
		return
	_interrupting = true
	var stopped := BodyBusy.stopped(busy.action)
	var response: Dictionary = await api.player_interrupt(player_id)
	_interrupting = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		apply_pose(response.body)
		busy.clear()
		busy_changed.emit("", 0.0)
		warning.emit(stopped)
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
		warning.emit(BodyBusy.human_error(error))
		if error.find("não há ação") >= 0:
			busy.clear()
			busy_changed.emit("", 0.0)

var combat := CombatView.new() ## #234: recarga, guarda, alcance e último golpe recebido, como o motor disse
var _guard_shown := "" ## a guarda que o app já mostra no corpo (para avisar só quando muda)
var _guard_busy := false
var _block_held := false ## botão direito ou F segurados: renova a guarda quando o motor deixa
var _tap_key := -1 ## duplo toque de direção esquiva: a última direção tocada e quando
var _tap_at := -1_000_000
const DOUBLE_TAP_MS := 260


## A posição fina do motor manda: a previsão volta para ela (o motor sabe da parede, da porta e de quem está no caminho).
func apply_pose(pose: Dictionary, from_walk: bool = false) -> void:
	note_combat(pose)
	_note_busy(pose)
	building_id = int(pose.buildingId) if pose.get("buildingId") != null else -1
	floor_level = int(pose.get("floor", 0))
	placed.emit(building_id, int(pose.get("floor", 0)))
	if frame != null:
		frame.set_ground(pose.get("ground")) # #238: o chão sob o corpo e o que atrasa o passo, como o motor diz
	if pose.has("fx"):
		var server := Vector2(float(pose.fx), float(pose.fy))
		var scale := float(time_view.get("timeScale", 1.0)) if time_view.get("timeScale") != null else 1.0
		_cell_meters = float(pose.get("cellMeters", 1.5))
		speed_cells = float(pose.get("speedMps", 0.0)) / maxf(_cell_meters, 0.01) * scale
		var on_ground: bool = body.ground != null and body.ground.is_valid()
		var now := _now()
		if body.pos != Vector2.INF and from_walk:
			var gap_m := body.pos.distance_to(server) * _cell_meters # medida: o quanto o corpo na tela estava longe da pose crua do motor
			walk_stats.gap_worst_m = maxf(float(walk_stats.gap_worst_m), gap_m)
			walk_stats.gap_mean_m += (gap_m - float(walk_stats.gap_mean_m)) / float(walk_stats.replies + 1)
		walk_stats.replies += 1
		if on_ground:
			# #248: o motor anda o corpo sozinho pelo rumo mantido; a pose dele (mais o que ele ainda não viu, reaplicado com as mesmas regras de
			# chão) diz onde ele está agora, e o corpo converge aí. Resposta de outro comando (não de andar) só vale se o corpo está parado.
			if from_walk:
				var before_pos := body.pos
				body.reconcile_to(_engine_now(server, int(pose.get("seq", 0)), now))
				if body.pos != before_pos:
					walk_stats.snaps += 1
			elif body.pos == Vector2.INF or (_walk_dir == Vector2.ZERO and body.velocity == Vector2.ZERO):
				body.reconcile_to(server)
		else:
			# o motor não deixou andar (parede, gente, canto): para de prever nesse rumo em vez de entrar e voltar.
			# #229: só vale com duas respostas paradas seguidas (a aceleração suave não é recusa)
			if _last_server != Vector2.INF and _walk_dir != Vector2.ZERO and now > _last_server_at:
				var expected := speed_cells * float(now - _last_server_at) / 1000.0
				var still := expected > 0.05 and server.distance_to(_last_server) < expected * 0.25
				_still_responses = _still_responses + 1 if still else 0
				if _still_responses >= 2 and not _blocked:
					_blocked = true
					body.stop()
			body.reconcile(server)
		_last_server = server
		_last_server_at = now
		fine = body.pos
		settled.emit(fine)


## #248: onde o motor está AGORA: a pose dele (do instante em que aplicou o comando `acked`, a meia ida e volta atrás) andada pelo rumo desse
## comando até o próximo que ele ainda não viu, e assim por diante até agora. Sem o comando na lista, a pose crua.
func _engine_now(server: Vector2, acked: int, now: int) -> Vector2:
	var entry: Dictionary = {}
	for input in _inputs:
		if int(input.seq) == acked:
			entry = input
	if entry.is_empty():
		return server
	var owd := clampf(float(now - int(entry.at)) * 0.5, 0.0, MAX_OWD_MS)
	var t := float(now) - owd
	var heading: Vector2 = entry.dir
	var segments: Array = []
	for later in _inputs:
		if int(later.seq) > acked:
			var applies := maxf(t, float(later.at) + owd)
			segments.append([heading, speed_cells, (applies - t) / 1000.0])
			t = applies
			heading = later.dir
	segments.append([heading, speed_cells, maxf(float(now) - t, 0.0) / 1000.0])
	return body.replay(server, segments)


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
	if combat_input(event):
		get_viewport().set_input_as_handled()
		return
	if event is InputEventKey and STEP_KEYS.has(event.keycode):
		get_viewport().set_input_as_handled() # AD-197k: o passo solto anda pelo `_process` (tecla segurada), não por toque
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_SPACE and rpg and not popup_open():
		get_viewport().set_input_as_handled()
		jump()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode in [KEY_TAB, KEY_I] and rpg:
		get_viewport().set_input_as_handled() # #222: sem painel de ordens no modo jogador (só comandos); #233: Tab e I abrem a mochila
		toggle_inventory()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_J and rpg:
		get_viewport().set_input_as_handled()
		toggle_goals() # #235: metas e diário
	elif event is InputEventKey and event.keycode == KEY_E and not event.echo and (not event.pressed or not (input_blocked.is_valid() and input_blocked.call())):
		get_viewport().set_input_as_handled()
		if not event.pressed:
			e_released()
		elif rpg and frame != null and not (frame.menu_open() or (menu != null and menu.visible)):
			e_pressed()
		else:
			if (menu != null and menu.visible) or (frame != null and frame.menu_open()):
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
	"stairs": "Escada", "water": "Fonte", "field": "Lavoura", "leaving": "Espólio", "nature": "Árvore"}
const VERB_TEXT := {"enter": "Entrar", "exit": "Sair", "take": "Pegar", "deposit": "Guardar o que leva", "buy": "Comprar",
	"sell": "Vender", "sleep": "Dormir", "talk": "Conversar", "strike": "Bater", "abandon": "Abandonar", "up": "Subir",
	"down": "Descer", "drink": "Beber", "fill": "Encher o cântaro", "work": "Trabalhar", "collect": "Recolher", "gather": "Cortar lenha", "steal": "Furtar", "return": "Devolver", "eat": "Comer",
	"socialize": "Conversar", "idle": "Descansar"}


## #233: E no jogo: toque faz a ação principal (a primeira linha do motor); segurar (HOLD_S) abre a roda com todas as ofertas.
const HOLD_S := 0.28
var input_blocked: Callable = Callable() ## o app liga: com a conversa aberta o E não pede nada
var _e_down := false
var _e_hold := 0.0
var _e_ready := false
var _e_wheel := false
var _e_quick := false
var _e_entries: Array = []


func e_pressed() -> void:
	if not is_active or _e_down:
		return
	_e_down = true
	_e_hold = 0.0
	_e_ready = false
	_e_wheel = false
	_e_quick = false
	_e_entries = await _reach_entries()
	_e_ready = true
	_e_step()


func e_released() -> void:
	if not _e_down:
		return
	_e_down = false
	if _e_wheel:
		return # a roda aberta fica até escolher (1–9, clique) ou Esc
	_e_quick = true
	_e_step()


func e_held(delta: float) -> void:
	if _e_down:
		_e_hold += delta
		_e_step()


func _e_step() -> void:
	if not is_active:
		return
	if _e_down and _e_ready and _e_hold >= HOLD_S and not _e_wheel:
		_e_wheel = true
		frame.show_menu(_e_entries)
		menu_toggled.emit(true)
	elif _e_quick and _e_ready:
		_e_quick = false
		if _e_entries.is_empty():
			warning.emit("Nada ao alcance.")
		else:
			interact(_e_entries[0])


## O que o corpo alcança agora, no formato do jogo (coisas primeiro, só as pessoas mais perto); guarda `offers`.
func _reach_entries() -> Array:
	if _busy:
		return []
	_busy = true
	var started := Time.get_ticks_usec()
	var response: Dictionary = await api.player_reach(player_id)
	_note_latency(started)
	_busy = false
	if not is_active or not response.ok or not (response.body is Dictionary):
		if response.status == 404 and is_active:
			_lost("Você não controla mais %s." % npc_name)
		return []
	offers = response.body.get("offers", [])
	return without_own_door(PlayerFrame.game_entries(menu_entries(offers, resource_name_of), offers, fine, aim_look.call()), building_id)


## #233: a mochila (I ou Tab): dinheiro e o que leva, da ficha do motor, lida na hora ao abrir.
func toggle_inventory() -> void:
	if frame == null:
		return
	frame.toggle_inventory()
	if frame.inventory_open():
		_body_clock = BODY_EVERY


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
		frame.show_menu(PlayerFrame.game_entries(menu_entries(offers, resource_name_of), offers, fine, aim_look.call()))
		menu_toggled.emit(true)
		return
	show_menu(menu_entries(offers, resource_name_of))


## As linhas do menu, todas tiradas da oferta: {text, kind, id, verb, resource}; o nome do recurso vem do catálogo do servidor.
## #227: dentro de um prédio a porta dele não oferece "Entrar" (já está dentro).
static func without_own_door(entries: Array, inside: int) -> Array:
	return entries if inside < 0 else entries.filter(func(e): return not (String(e.kind) == "door" and String(e.verb) == "enter" and int(e.id) == inside))


static func menu_entries(from: Array, name_of: Callable = Callable()) -> Array:
	var entries: Array = []
	for offer in from:
		var kind := String(offer.get("kind", ""))
		var title := String(offer.get("label", "")) if kind in ["person", "item", "object"] else String(KIND_TEXT.get(kind, kind)) # o nome vem do motor
		title = String(OBJECT_TEXT.get(title, title)) # #222: os móveis do catálogo padrão têm id em inglês ("counter")
		for verb in offer.get("verbs", []):
			var resources: Array = offer.get("resources", [])
			if String(verb) in ["buy", "take", "steal", "gather"] and not resources.is_empty():
				for item in resources:
					if int(item.get("quantity", 0)) <= 0:
						continue
					var price := int(item.get("price", 0))
					var resource_text: String = name_of.call(int(item.resourceId)) if name_of.is_valid() else "recurso %d" % int(item.resourceId)
					var line := "%s: %s" % [title, VERB_TEXT[verb]] if String(verb) == "gather" else "%s: %s %s%s" % [title, VERB_TEXT[verb], resource_text,
						" (%d)" % price if price > 0 else ""] # #241: "Cortar lenha · Árvore" (a madeira é implícita)
					entries.append({"text": line, "kind": kind, "id": int(offer.id), "verb": String(verb), "resource": int(item.resourceId)})
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
		_body_clock = BODY_EVERY # #222: o dinheiro e a carga mudam na hora no cartão
		var pose = response.body.get("pose")
		if pose is Dictionary:
			_pace(pose)
			_note_busy(pose)
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
		refused.emit()
		warning.emit(BodyBusy.human_error(error) if error != "" else "O motor recusou (erro %d)." % response.status)


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


## Clique: golpe no NPC sob a mira; o motor decide alcance, recarga e vontade. O braço sobe já só se o corpo está pronto
## (recarga lida do motor); o que acertou, o dano e a reação do alvo vêm na resposta.
func strike(target: int) -> void:
	if not is_active or _busy or target < 0 or target == npc_id:
		return
	_busy = true
	var started := Time.get_ticks_usec()
	var swung := combat.strike_ready(Time.get_ticks_msec())
	if swung:
		struck.emit(target) # o braço sobe já (previsão); o motor diz se acertou
	var response: Dictionary = await api.player_strike(player_id, target)
	_note_latency(started)
	_busy = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		var body: Dictionary = response.body
		if not swung:
			struck.emit(target) # sem previsão: o braço sobe quando o motor aceita
		combat.note_strike(int(body.get("readyInMs", 0)), Time.get_ticks_msec())
		if body.get("pose") is Dictionary:
			note_combat(body.pose)
		struck_hit.emit(target)
		struck_result.emit(target, body)
		_body_clock = BODY_EVERY
		return
	if response.ok:
		struck_hit.emit(target)
		_body_clock = BODY_EVERY
		return
	refused.emit()
	warning.emit(CombatView.refusal_text(response.status, response.body, "O golpe não saiu"))


## #234: lê o `pose.combat` (de qualquer resposta com pose): recarga, guarda, alcance e golpes recebidos novos.
func note_combat(pose: Dictionary) -> void:
	for event in combat.note_pose(pose, Time.get_ticks_msec()):
		incoming.emit(String(event.outcome), int(event.damage), int(event.by))
		if event.outcome == "Hit":
			_body_clock = BODY_EVERY # a saúde mudou: a barra lê já
	_sync_guard()


## A guarda que vale agora no corpo ("" quando a janela do motor acabou); avisa o app só quando muda.
func _sync_guard() -> void:
	var now := "" if not combat.guarding(Time.get_ticks_msec()) else combat.guard
	if now != _guard_shown:
		_guard_shown = now
		guard_changed.emit(now)
	if frame != null:
		frame.set_combat(combat.strike_wait_ms(Time.get_ticks_msec()), now, combat.guard_wait_ms(Time.get_ticks_msec()))


## #234: bloquear (botão direito ou F, segure): o motor decide a janela e a recarga; recusa mostra o motivo dele.
func block() -> void:
	await _defend("block")


## #234: esquivar (Ctrl, Alt ou duplo toque numa direção).
func dodge() -> void:
	await _defend("dodge")


func _defend(kind: String) -> void:
	if not is_active or _guard_busy:
		return
	_guard_busy = true
	var response: Dictionary = await (api.player_block(player_id) if kind == "block" else api.player_dodge(player_id))
	_guard_busy = false
	if not is_active:
		return
	if response.ok and response.body is Dictionary:
		var body: Dictionary = response.body
		note_combat(body.get("pose", body) if body.get("pose", body) is Dictionary else body)
		defended.emit("Block" if kind == "block" else "Dodge")
	elif response.status == 404:
		_lost("Você não controla mais %s." % npc_name)
	else:
		refused.emit()
		warning.emit(CombatView.refusal_text(response.status, response.body, "A defesa não saiu"))


## #234: teclas e botões de combate; devolve se tratou o evento. F e botão direito seguram a guarda; Ctrl/Alt esquivam.
func combat_input(event: InputEvent) -> bool:
	if not is_active or not rpg or (input_blocked.is_valid() and input_blocked.call()):
		return false
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT:
		_block_held = event.pressed
		if event.pressed:
			block()
		return true
	if event is InputEventKey and not event.echo:
		if event.keycode == KEY_F:
			_block_held = event.pressed
			if event.pressed:
				block()
			return true
		if event.pressed and event.keycode in [KEY_CTRL, KEY_ALT]:
			dodge()
			return true
		if event.pressed and STEP_KEYS.has(event.keycode):
			var now := Time.get_ticks_msec()
			if event.keycode == _tap_key and now - _tap_at <= DOUBLE_TAP_MS:
				_tap_key = -1
				dodge() # duplo toque na mesma direção
			else:
				_tap_key = event.keycode
				_tap_at = now
			return false # o passo segue pelo `_process` (o toque não é consumido)
	return false


func _reset_combat() -> void:
	combat.reset()
	_block_held = false
	_tap_key = -1
	if _guard_shown != "":
		_guard_shown = ""
		guard_changed.emit("")
	if frame != null:
		frame.set_combat(0, "", 0)


## Segurando a guarda: quando a janela acaba e o motor deixa, renova (o corpo continua protegido enquanto o jogador segura).
func _hold_guard() -> void:
	_sync_guard()
	if _block_held and not _guard_busy and not combat.guarding(Time.get_ticks_msec()) and combat.guard_ready(Time.get_ticks_msec()):
		block()


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
	_reset_combat()
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
