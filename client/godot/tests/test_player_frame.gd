extends "res://tests/test_base.gd"
## #222: jogabilidade simples no modo jogador — moldura de jogo (barras, dica do E, menu numerado), correr (Shift), pular (Espaço)
## e o passo recusado que não fica calado. O motor decide tudo; aqui só se confere o que o cliente pede e mostra.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const CROWD := [
	{"kind": "stall", "id": 3, "label": "", "verbs": ["buy"], "x": 5, "y": 5,
		"resources": [{"resourceId": 1, "quantity": 9, "price": 4}]},
	{"kind": "person", "id": 9, "label": "Bia", "verbs": ["talk", "strike"], "x": 9, "y": 9, "resources": []},
	{"kind": "person", "id": 10, "label": "Caio", "verbs": ["talk", "strike"], "x": 5, "y": 6, "resources": []},
	{"kind": "person", "id": 11, "label": "Duda", "verbs": ["talk", "strike"], "x": 6, "y": 5, "resources": []},
	{"kind": "person", "id": 12, "label": "Eva", "verbs": ["talk", "strike"], "x": 7, "y": 7, "resources": []},
	{"kind": "counter", "id": 4, "label": "", "verbs": ["work"], "x": 5, "y": 4, "resources": []},
]


func _hud(replies: Dictionary = {}) -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	for route in replies:
		transport.on(route, replies[route])
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	return [hud, transport]


func _free(made: Array) -> void:
	made[0].frame.queue_free()
	made[0].queue_free()


func test_the_e_menu_in_game_drops_strike_keeps_things_first_and_only_the_nearest_people() -> void:
	var all := PlayerHud.menu_entries(CROWD)
	var entries := PlayerFrame.game_entries(all, CROWD, Vector2(5.5, 5.5))
	var texts: Array = entries.map(func(e): return String(e.text))
	eq(texts, ["Banca: Comprar recurso 1 (4)", "Balcão: Trabalhar", "Caio: Conversar", "Duda: Conversar", "Eva: Conversar"],
		"sem Bater (é o clique), coisas antes, só as 3 pessoas mais perto")
	check(entries.size() <= PlayerFrame.MENU_MAX, "nunca passa do teto de linhas")


func test_number_keys_choose_from_the_numbered_menu_and_esc_closes_it() -> void:
	var made: Array = await _hud({"GET /players/p-1/reach": {"status": 200, "body": {"offers": CROWD}},
		"POST /players/p-1/interact": {"status": 200, "body": {"kind": "stall", "id": 3, "verb": "buy", "quantity": 1, "value": 4}}})
	var hud = made[0]
	var transport = made[1]
	hud.fine = Vector2(5.5, 5.5)
	await hud.open_interactions()
	check(hud.frame.menu_open(), "E abre o menu da moldura no modo jogador")
	var buttons: Array = hud.frame.menu_box.get_children().filter(func(c): return c is Button)
	check(String(buttons[0].text).begins_with("1"), "linhas numeradas (%s)" % buttons[0].text)
	var key := InputEventKey.new()
	key.keycode = KEY_1
	key.pressed = true
	hud._unhandled_input(key)
	for i in 4:
		await tree.process_frame
	check(transport.calls.has("POST /players/p-1/interact"), "a tecla 1 pede a primeira linha ao motor")
	eq(String(transport.bodies["POST /players/p-1/interact"].back().verb), "buy", "a da banca")
	await hud.open_interactions()
	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	hud._unhandled_input(esc)
	check(not hud.frame.menu_open(), "Esc fecha o menu")
	_free(made)


func test_space_jumps_when_the_engine_allows_and_says_why_when_not() -> void:
	var made: Array = await _hud({"POST /players/p-1/jump": {"status": 200, "body": {"playerId": "p-1", "npcId": 7, "airMs": 500}}})
	var hud = made[0]
	var air: Array = []
	hud.jumped.connect(func(s: float): air.append(s))
	await hud.jump()
	eq(air, [0.5], "o salto dura o tempo no ar que o motor disse")
	made[1].on("POST /players/p-1/jump", {"status": 409, "body": {"error": "conflict: recusa: cansada demais para pular"}})
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	await hud.jump()
	eq(warnings, ["cansada demais para pular"], "a recusa do motor aparece")
	eq(air.size(), 1, "e o corpo não pula")
	_free(made)


func test_walking_into_a_paused_world_says_how_to_resume_instead_of_silence() -> void:
	var made: Array = await _hud({"POST /players/p-1/walk": {"status": 409, "body": {"error": "conflict: mundo pausado pelo admin"}}})
	var hud = made[0]
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	await hud.send_walk(Vector2(0, -1))
	eq(warnings.size(), 1, "um aviso")
	check("pausado" in String(warnings[0]), "diz que o mundo está pausado (%s)" % warnings)
	await hud.send_walk(Vector2(0, -1))
	eq(warnings.size(), 1, "sem repetir a cada passo")
	_free(made)


func test_shift_asks_the_engine_to_run_and_the_reply_says_if_it_did() -> void:
	var made: Array = await _hud({"POST /players/p-1/walk": {"status": 200, "body": {"playerId": "p-1", "npcId": 7, "x": 5, "y": 5,
		"fx": 5.5, "fy": 5.5, "speedMps": 3.1, "cellMeters": 1.5, "running": true}}})
	var hud = made[0]
	await hud.send_walk(Vector2(0, -1), true)
	eq(bool(made[1].bodies["POST /players/p-1/walk"].back().get("run", false)), true, "o passo pede para correr")
	eq(hud.running, true, "o motor disse que correu")
	_free(made)


func test_the_frame_shows_the_body_as_the_engine_says_and_the_paused_incarnate_reason() -> void:
	var frame := PlayerFrame.new()
	frame.set_body("Ana", "Trabalhando", {"health": 90, "hunger": 15, "thirst": 70, "sleep": 50})
	eq(frame.name_label.text, "Ana")
	eq(frame.bars["hunger"].value, 15.0, "a fome da ficha")
	frame.set_prompt("Banca: Comprar comida (4)")
	check(frame.prompt.text.begins_with("E"), "a dica diz a tecla (%s)" % frame.prompt.text)
	frame.free()
	var hud := PlayerHud.new()
	var paused := hud._incarnate_error({"status": 409, "body": {"error": "conflict: mundo pausado pelo admin"}})
	check("pausado" in paused, "posse recusada por pausa não diz que é de outro jogador (%s)" % paused)
	var taken := hud._incarnate_error({"status": 409, "body": {"error": "conflict: NPC já tem controlador"}})
	eq(taken, "Este NPC já é controlado por outro jogador.")
	hud.free()


const App := preload("res://Scenes/app.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")


## #224: todo verbo que o motor oferece ao jogador tem gesto (menos os que já são pose ou golpe), e o gesto conhecido toca.
func test_every_player_verb_has_a_gesture_and_it_plays() -> void:
	for verb in PlayerHud.VERB_TEXT:
		if verb in ["strike"]:
			continue # o golpe tem animação própria (play_strike)
		check(App.GESTURE_BY_VERB.has(verb), "verbo sem gesto: %s" % verb)
	var actor := NpcActor.new()
	actor.setup(7, Vector2i(1, 1), 5, ActionLabels.new())
	for kind in NpcActor.GESTURES:
		actor.play_gesture(kind, 1.0)
		actor.animate(0.3)
		eq(actor.gesture_playing(), kind, "o gesto %s toca" % kind)
	actor.animate(2.0)
	eq(actor.gesture_playing(), "", "e termina")
	actor.play_gesture("inventado")
	eq(actor.gesture_playing(), "", "gesto desconhecido não toca")
	actor.free()


## #222: o relógio estima o tempo pelo ritmo do motor: em tempo real (Δ 100 ms, 10 ticks/s) passa 1 s de mundo por segundo, não 1 hora.
func test_the_clock_estimate_follows_the_engine_rhythm() -> void:
	var real := App.hours_per_second({"ticksPerSecond": 1.0, "tickDeltaMs": 100, "loopTicksPerSecond": 10.0})
	check(absf(real - 1.0 / 3600.0) < 1e-9, "tempo real: 1 s por segundo (%s h/s)" % real)
	eq(App.hours_per_second({"ticksPerSecond": 4.0, "tickDeltaMs": 3600000, "loopTicksPerSecond": 4.0}), 4.0, "rápido: horas por tick × ticks")
	eq(App.hours_per_second({"ticksPerSecond": 2.0}), 2.0, "servidor antigo: ticksPerSecond")


## #222: interação aceita no modo jogador vira um aviso curto com o que o motor disse (quanto e por quanto).
func test_an_accepted_interaction_says_what_happened() -> void:
	eq(PlayerHud.done_text({"text": "Banca: Comprar Comida (4)"}, {"quantity": 1, "value": 4}), "✓ Banca: Comprar Comida · 1 un. por 4")
	eq(PlayerHud.done_text({"text": "Porta: Entrar"}, {"quantity": null, "value": null}), "✓ Porta: Entrar")


const CameraRig := preload("res://Scenes/camera_rig.gd")


## #222: zoom sem mouse: + e − na 3ª pessoa aproximam e afastam a câmera, dentro dos limites; na isométrica mudam o zoom.
func test_plus_and_minus_zoom_without_a_mouse() -> void:
	var rig := CameraRig.new()
	rig.set_mode(rig.THIRD)
	var key := InputEventKey.new()
	key.pressed = true
	key.keycode = KEY_EQUAL
	rig._unhandled_input(key)
	check(rig.third_scale < 1.0, "+ aproxima (%s)" % rig.third_scale)
	key.keycode = KEY_MINUS
	for i in 20:
		rig._unhandled_input(key)
	eq(rig.third_scale, CameraRig.THIRD_SCALE_MAX, "− afasta até o limite")
	rig.set_mode(rig.ISO)
	var before: float = rig._zoom_goal
	key.keycode = KEY_KP_ADD
	rig._unhandled_input(key)
	var after: float = rig._zoom_goal
	check(after < before, "na isométrica + aproxima (%s → %s)" % [before, after])
	rig.free()


## #222: o cartão mostra o dinheiro e o que a pessoa leva, como a ficha do motor diz (o nome do recurso vem do catálogo).
func test_the_card_shows_money_and_what_is_carried() -> void:
	var frame := PlayerFrame.new()
	frame.set_purse(120, {"resourceId": 1, "quantity": 3}, func(id: int) -> String: return "Comida" if id == 1 else "?")
	eq(frame.purse_label.text, "Dinheiro 120 · Leva 3 Comida")
	frame.set_purse(45, null, Callable())
	eq(frame.purse_label.text, "Dinheiro 45", "sem carga, só o dinheiro")
	frame.set_purse(null, null, Callable())
	eq(frame.purse_label.visible, false, "servidor sem o campo: nada")
	frame.free()
