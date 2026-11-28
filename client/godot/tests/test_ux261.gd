extends "res://tests/test_base.gd"
## #261 (UX do sandbox, 3º passo): a lista de velocidades vem do cenário (botões e teclas 0–5), a roda da pessoa não repete "Conversar", o "Bater papo" oferece o
## texto (T), em velocidade alta o corpo não anda por WASD, e a troca de corpo (G) enquadra o interior de cima. O cliente só mostra o que o motor diz.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const SocialMenu := preload("res://Scenes/social_menu.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const App := preload("res://Scenes/app.gd")

const SPEEDS := [1, 3, 10, 60, 300]


func _time_view(speed: int) -> Dictionary:
	return {"rhythm": "RealTime", "tickDeltaMs": 50, "timeScale": 1.0, "playersPresent": 1, "canSkip": true, "skipping": false, "sleeping": false,
		"speed": speed, "maxSpeed": 300, "speeds": SPEEDS, "walkMaxSpeed": 10}


func _hud() -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("GET /players/p-1/time", {"status": 200, "body": _time_view(1)})
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


func _key(code: int) -> InputEventKey:
	var key := InputEventKey.new()
	key.keycode = code
	key.pressed = true
	return key


func test_the_buttons_come_from_the_scenario_list_and_mark_the_current_speed() -> void:
	var frame := PlayerFrame.new()
	frame.set_speed(_time_view(10))
	eq(frame.speed_buttons.map(func(b): return String(b.text)), ["II", "1×", "3×", "10×", "60×", "300×"] as Array, "pausa e a lista do cenário")
	eq(frame.speed_buttons[3].button_pressed, true, "o 10× está marcado")
	eq(frame.speed_buttons[1].button_pressed, false, "e o 1× não")
	check("tempo real" in frame.speed_buttons[1].tooltip_text, "o 1× diz que é tempo real")
	var chosen: Array = []
	frame.speed_chosen.connect(func(speed: int): chosen.append(speed))
	frame.speed_buttons[5].pressed.emit()
	frame.speed_buttons[0].pressed.emit()
	eq(chosen, [300, 0] as Array, "o botão pede o valor da velocidade (300×), não a posição")
	eq(frame.speed_for_key(0), 0, "a tecla 0 é a pausa")
	eq(frame.speed_for_key(1), 1, "a 1 é a primeira da lista")
	eq(frame.speed_for_key(5), 300, "a 5 é a quinta")
	eq(frame.speed_for_key(6), -1, "sem a sexta, a tecla não faz nada")
	frame.set_speed({"canSkip": true, "speed": 1, "maxSpeed": 2})
	eq(frame.speed_buttons.size(), 3, "cenário antigo (sem lista): pausa, 1× e 2×")
	frame.free()


func test_the_keys_zero_to_five_ask_the_engine_for_the_listed_speed() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	var transport = made[1]
	hud.apply_time(_time_view(1))
	transport.on("POST /players/p-1/speed", {"status": 200, "body": _time_view(300)})
	hud._unhandled_input(_key(KEY_5))
	for i in 6:
		await tree.process_frame
	eq(int(transport.bodies["POST /players/p-1/speed"].back().get("speed", -1)), 300, "a tecla 5 pede 300×")
	eq(hud.speed_now(), 300, "o HUD adota o que o motor respondeu")
	eq(hud.frame.speed_buttons[5].button_pressed, true, "o botão 300× fica marcado")
	var calls: int = transport.count_calls("POST /players/p-1/speed")
	hud._unhandled_input(_key(KEY_6))
	for i in 6:
		await tree.process_frame
	eq(transport.count_calls("POST /players/p-1/speed"), calls, "a tecla 6 não tem botão: nada é pedido")
	transport.on("POST /players/p-1/speed", {"status": 200, "body": _time_view(0)})
	hud._unhandled_input(_key(KEY_0))
	for i in 6:
		await tree.process_frame
	eq(int(transport.bodies["POST /players/p-1/speed"].back().get("speed", -1)), 0, "a tecla 0 pausa")
	_free(made)


func test_in_high_speed_the_wasd_does_not_walk_and_says_how_to_come_back() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	hud.apply_time(_time_view(60))
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	hud.glide(Vector2(1, 0), 0.05)
	eq(hud._walk_dir, Vector2.ZERO, "o rumo não segue em 60×")
	check(warnings.size() > 0 and "velocidade alta" in String(warnings[0]), "diz que só anda por ordem e como voltar: %s" % [warnings])
	_free(made)


func test_the_person_wheel_has_one_talk_entry_the_catalog_chat_and_it_offers_the_text_window() -> void:
	var person := {"kind": "person", "id": 9, "label": "Marina", "verbs": ["talk"], "x": 5, "y": 5, "resources": [],
		"social": [{"id": "chat", "name": "Bater papo", "category": "friendly", "categoryName": "Amigável", "available": true, "seconds": 40}]}
	var entries := PlayerHud.menu_entries([person])
	eq(entries.map(func(e): return String(e.text)), ["Marina: Interagir ▸"] as Array, "sem 'Conversar' repetido ao lado do 'Bater papo'")
	var far := {"kind": "person", "id": 9, "label": "Marina", "verbs": [], "x": 9, "y": 9, "resources": [], "social": person.social}
	eq(PlayerHud.menu_entries([far]).size(), 1, "de longe só o menu social (o motor manda sem verbos)")
	var no_social := {"kind": "person", "id": 9, "label": "Marina", "verbs": ["talk"], "x": 5, "y": 5, "resources": []}
	eq(PlayerHud.menu_entries([no_social]).map(func(e): return String(e.text)), ["Marina: Conversar"] as Array, "sem catálogo social o Conversar de sempre fica")


func test_an_accepted_chat_offers_the_text_conversation_with_t() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	var transport = made[1]
	transport.on("POST /players/p-1/interact", {"status": 200, "body": {"kind": "person", "id": 9, "verb": "social:chat", "quantity": 0, "value": 0,
		"outcome": "done", "note": "Ana bateu papo com Marina", "pose": {"x": 5, "y": 5, "npcId": 7, "playerId": "p-1"}}})
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	await hud.interact({"kind": "person", "id": 9, "verb": "social:chat", "resource": -1, "text": "Bater papo · 40 s"})
	check("T para escrever" in String(warnings.back()), "o aviso oferece a conversa por texto: %s" % [warnings.back()])
	transport.on("POST /players/p-1/interact", {"status": 200, "body": {"kind": "person", "id": 9, "verb": "talk", "quantity": 0, "value": 0, "sessionId": 5,
		"pose": {"x": 5, "y": 5, "npcId": 7, "playerId": "p-1"}}})
	var opened: Array = []
	hud.conversation_opened.connect(func(id: int, _name: String, session: int): opened.append([id, session]))
	hud._unhandled_input(_key(KEY_T))
	for i in 8:
		await tree.process_frame
	eq(opened, [[9, 5]] as Array, "T abre a janela de conversa com a mesma pessoa")
	_free(made)


func test_switching_bodies_frames_the_interior_from_above_until_the_new_body_settles() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": [{"actionType": 1, "label": "Dormindo"}]})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 0, "tickDeltaMs": 3600000, "year": 0}})
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	app.rig.set_mode(app.rig.THIRD)
	eq(app.rig.scene_framing, false, "antes da troca, sem enquadramento de cena")
	app._on_player_switched(7, 8)
	eq(app.rig.scene_framing, true, "a troca liga o enquadramento a prumo (nunca atrás de parede)")
	for i in 200:
		app._step_glide(0.016)
	eq(app.rig.scene_framing, false, "assentado o corpo novo, volta ao olhar de antes")
	app.queue_free()
