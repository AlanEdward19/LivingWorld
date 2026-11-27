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


const WorldReader := preload("res://Contract/world_reader.gd")


## #222: o cartão lê a ficha do motor na hora, mesmo com o leitor em cache (em tempo real a hora do mundo leva 1 h para virar).
func test_the_card_reads_the_sheet_fresh_through_the_cached_reader() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("GET /npcs/7", {"status": 200, "body": {"health": 90, "hunger": 50, "thirst": 50, "sleep": 50, "money": 50}})
	var hud := PlayerHud.new()
	hud.api = WorldReader.new(transport)
	hud.api.validate_shapes = false
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	await hud._refresh_body(PlayerHud.BODY_EVERY)
	eq(hud.frame.purse_label.text, "Dinheiro 50")
	transport.on("GET /npcs/7", {"status": 200, "body": {"health": 90, "hunger": 50, "thirst": 50, "sleep": 50, "money": 45}})
	await hud._refresh_body(PlayerHud.BODY_EVERY)
	eq(hud.frame.purse_label.text, "Dinheiro 45", "a compra aparece sem esperar a hora do mundo virar")
	hud.frame.queue_free()
	hud.queue_free()


## #222: com o token de admin (jogo local) a posse é plena: o cliente pede /possess (a pessoa não recusa ordens); sem ele, /incarnate.
func test_the_admin_possesses_fully_and_others_incarnate() -> void:
	for full in [true, false]:
		var transport := FakeTransport.new()
		transport.on("POST /sessions", {"status": 200, "body": {"sessionToken": "tok", "playerId": "p-1"}})
		transport.on("POST /players/7/possess", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
		transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
		transport.channel_replier = func(command: Dictionary) -> Dictionary:
			return {"seq": command.seq, "ok": true, "status": 200, "body": {"npcId": 7, "playerId": "p-1"}}
		var hud := PlayerHud.new()
		hud.api = ApiClient.new(transport)
		hud.labels = ActionLabels.new()
		hud.player_id = "p-1"
		hud.session_enabled = true
		hud.full_possession = full
		tree.root.add_child(hud)
		check(await hud.incarnate(7, "Ana"), "encarnou")
		eq(transport.calls.has("POST /players/7/possess"), full, "posse plena só com o admin (%s)" % full)
		if not full:
			eq(String(transport.channels.back().sent.back().command), "incarnate", "sem admin, a posse comum pelo canal")
		hud.api.close_player_channel()
		hud.queue_free()


## #233: a dica da mira diz o verbo (com ícone) e o alvo; a roda põe as fatias em círculo, a primeira no alto.
func test_the_prompt_says_verb_and_target_and_the_wheel_is_a_circle() -> void:
	eq(PlayerFrame.prompt_text("Caio: Conversar"), "Conversar · Caio")
	eq(PlayerFrame.prompt_text("Banca: Comprar comida (4)"), "Comprar comida (4) · Banca")
	eq(PlayerFrame.prompt_text("Mesa"), "Mesa", "sem alvo e verbo, a linha do motor como veio")
	var first := PlayerFrame.wheel_offset(0, 4)
	check(absf(first.x) < 0.01 and first.y < 0.0, "a primeira fatia fica no alto")
	check(PlayerFrame.wheel_offset(1, 4).x > 0.0, "a segunda, à direita (horário)")
	var seen := {}
	for i in 6:
		seen[PlayerFrame.wheel_offset(i, 6)] = true
	eq(seen.size(), 6, "nenhuma fatia sobre a outra")


## #228: de 1 a 8 entradas os botões da roda não se cobrem, nem o título nem a mira (o centro fica limpo).
func test_the_wheel_buttons_never_overlap_for_one_to_eight_entries() -> void:
	for n in range(1, 9):
		var rects := PlayerFrame.wheel_rects(n)
		for i in rects.size():
			check(not rects[i].has_point(Vector2.ZERO) and not rects[i].grow(PlayerFrame.WHEEL_AIM_CLEAR).has_point(Vector2.ZERO), "centro livre (%d entradas, fatia %d)" % [n, i])
			for j in range(i + 1, rects.size()):
				check(not rects[i].intersects(rects[j]), "fatias %d e %d sem cobrir (%d entradas)" % [i, j, n])
		var frame := PlayerFrame.new()
		var list: Array = []
		for i in n:
			list.append({"text": "Alvo %d: Trabalhar Lavoura" % i, "verb": "work", "kind": "thing", "id": i})
		frame.show_menu(list)
		var title: Label = frame.menu_box.get_child(0)
		var title_rect := Rect2(title.position, title.size)
		for rect in rects:
			check(not title_rect.intersects(rect), "o título não cobre botão (%d entradas)" % n)
		check(not title_rect.has_point(Vector2.ZERO), "o título fica fora da mira (%d entradas)" % n)
		frame.free()


## #228: o cartão diz "Andando"/"Correndo" enquanto o corpo se move pela previsão, e volta à ação do motor ao parar.
func test_the_card_says_walking_while_the_body_moves_and_the_engine_action_when_still() -> void:
	var frame := PlayerFrame.new()
	frame.set_body("Ana", "Parado", {})
	eq(frame.action_label.text, "Parado")
	frame.set_motion("Andando")
	eq(frame.action_label.text, "Andando")
	frame.set_body("Ana", "Parado", {}) # a ficha do motor chega andando: o cartão segue dizendo o movimento
	eq(frame.action_label.text, "Andando")
	frame.set_motion("Correndo")
	eq(frame.action_label.text, "Correndo")
	frame.set_motion("")
	eq(frame.action_label.text, "Parado", "parou: volta à ação do motor")
	frame.free()


## #228: nenhum emoji sem glifo na fonte da roda e do prompt (aparecia um quadrado).
func test_the_prompt_and_the_wheel_use_no_emoji() -> void:
	for line in ["Caio: Conversar", "Banca: Comprar comida (5)", "Campo: Trabalhar Lavoura", "Porta: Entrar"]:
		for ch in PlayerFrame.prompt_text(line):
			check(ch.unicode_at(0) < 0x2000, "só letras e pontuação comum (%s)" % line)


## #233: toque em E faz a ação principal (a primeira linha do motor), sem abrir roda; segurar abre a roda.
func test_a_tap_on_e_does_the_primary_action_and_a_hold_opens_the_wheel() -> void:
	var made: Array = await _hud({"GET /players/p-1/reach": {"status": 200, "body": {"offers": CROWD}},
		"POST /players/p-1/interact": {"status": 200, "body": {"kind": "stall", "id": 3, "verb": "buy", "quantity": 1, "value": 4}}})
	var hud = made[0]
	var transport = made[1]
	hud.fine = Vector2(5.5, 5.5)
	await hud.e_pressed()
	hud.e_released()
	for i in 4:
		await tree.process_frame
	check(not hud.frame.menu_open(), "toque não abre a roda")
	eq(String(transport.bodies["POST /players/p-1/interact"].back().verb), "buy", "a principal: a banca, primeira linha")
	await hud.e_pressed()
	hud.e_held(PlayerHud.HOLD_S + 0.05)
	check(hud.frame.menu_open(), "segurar E abre a roda")
	check(hud.frame.entries.size() >= 2, "com as ofertas do motor")
	hud.e_released()
	check(hud.frame.menu_open(), "soltar não fecha a roda aberta")
	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	hud._unhandled_input(esc)
	check(not hud.frame.menu_open(), "Esc fecha")
	_free(made)


func test_the_aim_target_is_announced_and_cleared_when_nothing_is_in_reach() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	var seen: Array = []
	hud.target_changed.connect(func(kind: String, id: int, cell: Vector2): seen.append([kind, id, cell]))
	hud.fine = Vector2(5.5, 5.5)
	hud.set_reach_hint(CROWD)
	eq(seen.back(), ["stall", 3, Vector2(5, 5)], "o alvo é a primeira linha, com a célula do motor")
	hud.set_reach_hint([])
	eq(seen.back()[2], Vector2.INF, "sem oferta, o realce some")
	_free(made)


func test_the_backpack_opens_with_i_or_tab_and_shows_what_the_engine_says() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("GET /npcs/7", {"status": 200, "body": {"health": 90, "hunger": 50, "thirst": 50, "sleep": 50,
		"money": 120, "carrying": {"resourceId": 2, "quantity": 3}}})
	var hud := PlayerHud.new()
	hud.api = WorldReader.new(transport)
	hud.api.validate_shapes = false
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	var made := [hud]
	for code in [KEY_I, KEY_TAB]:
		var key := InputEventKey.new()
		key.keycode = code
		key.pressed = true
		hud._unhandled_input(key)
		check(hud.frame.inventory_open(), "abre com a tecla %d" % code)
		for i in 6: # abrir a mochila pede a ficha na hora (o `_process` a lê)
			await tree.process_frame
		var text := ""
		for child in hud.frame.inventory_box.get_children():
			text += (String(child.text) + "\n") if child is Label else ""
		check("Dinheiro: 120" in text and "3 recurso 2" in text, "dinheiro e mãos da ficha (%s)" % text)
		hud._unhandled_input(key)
		check(not hud.frame.inventory_open(), "a mesma tecla fecha")
	_free(made)


## #227: a dica de teclas tem duas linhas, cada uma cabe em 1280 px, e nenhuma tecla some.
func test_the_key_hints_fit_in_1280_in_two_lines() -> void:
	var text: String = PlayerFrame.hint_text()
	var lines := text.split("\n")
	eq(lines.size(), 2, "duas linhas")
	var font := ThemeDB.fallback_font
	for line in lines:
		check(font.get_string_size(line, HORIZONTAL_ALIGNMENT_LEFT, -1, 13).x < 1280.0 - 40.0, "a linha cabe em 1280 (%s)" % line)
	for key in ["WASD", "Shift", "Espaço", "E interagir", "segure: roda", "Clique", "Botão direito/F", "Ctrl", "I mochila", "J metas", "C câmera", "Q ombro", "+ −", "F1", "Esc"]:
		check(key in text, "a dica lista " + key)
