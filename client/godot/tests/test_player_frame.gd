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
