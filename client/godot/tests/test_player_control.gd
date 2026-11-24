extends "res://tests/test_base.gd"
## AD-197d: câmera do modo jogador (3ª/1ª pessoa, mouse olha), WASD relativo ao olhar enviado como intenção pelo canal, e o menu
## de interações vindo só da oferta do motor.

const CameraRig := preload("res://Scenes/camera_rig.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const OFFERS := [
	{"kind": "stall", "id": 3, "label": "", "verbs": ["buy"], "x": 4, "y": 5,
		"resources": [{"resourceId": 1, "quantity": 9, "price": 4}, {"resourceId": 2, "quantity": 0, "price": 2}]},
	{"kind": "person", "id": 9, "label": "Bia", "verbs": ["talk", "strike"], "x": 4, "y": 4, "resources": []},
	{"kind": "door", "id": 12, "label": "", "verbs": ["enter"], "x": 5, "y": 4, "resources": []},
]


func _hud_with_channel() -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /sessions", {"status": 200, "body": {"sessionToken": "tok", "playerId": "p-1"}})
	transport.channel_replier = func(command: Dictionary) -> Dictionary:
		var body: Variant = {"npcId": 7, "playerId": "p-1"}
		match String(command.command):
			"move": body = {"playerId": "p-1", "npcId": 7, "x": 5 + int(command.dx), "y": 5 + int(command.dy)}
			"reach": body = {"offers": OFFERS}
			"interact": body = {"kind": command.kind, "id": command.id, "verb": command.verb, "quantity": 1, "value": 4,
				"sessionId": 77 if command.verb == "talk" else null, "pose": {"playerId": "p-1", "npcId": 7, "x": 5, "y": 5}}
		return {"seq": command.seq, "ok": true, "status": 200, "body": body}
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.api.validate_shapes = true
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.session_enabled = true
	tree.root.add_child(hud)
	return [hud, transport]


func test_the_player_camera_is_third_person_by_default_c_switches_to_first_and_leaving_goes_back_to_iso() -> void:
	var rig := CameraRig.new()
	eq(rig.camera.projection, Camera3D.PROJECTION_ORTHOGONAL, "espectador: isométrica")
	rig.set_mode(rig.THIRD)
	eq(rig.camera.projection, Camera3D.PROJECTION_PERSPECTIVE, "encarnado: perspectiva")
	check(rig.camera.position.z > 1.0, "3ª pessoa: câmera atrás do corpo")
	var key := InputEventKey.new()
	key.keycode = KEY_C
	key.pressed = true
	rig._unhandled_input(key)
	eq(rig.mode, rig.FIRST, "C troca para a 1ª pessoa")
	eq(rig.camera.position.z, 0.0, "1ª pessoa: nos olhos")
	rig._unhandled_input(key)
	eq(rig.mode, rig.THIRD, "C volta para a 3ª")
	rig.set_mode(rig.ISO)
	eq(rig.camera.projection, Camera3D.PROJECTION_ORTHOGONAL, "sair do personagem volta à isométrica")
	rig.toggle_person()
	eq(rig.mode, rig.ISO, "na isométrica C não vale")
	rig.free()


func test_the_mouse_looks_around_with_a_limited_pitch_and_the_heading_follows_the_yaw() -> void:
	var rig := CameraRig.new()
	rig.set_mode(rig.THIRD)
	rig.yaw = 0.0
	rig.look(Vector2(0, -100000))
	check(is_equal_approx(rig.pitch, rig.MAX_PITCH), "não vira de cabeça para baixo")
	rig.look(Vector2(0, 100000))
	check(is_equal_approx(rig.pitch, rig.MIN_PITCH), "nem para baixo além do limite")
	rig.yaw = 0.0
	check(rig.heading().distance_to(Vector2(0, -1)) < 0.001, "giro 0 olha para -z (a linha de cima da grade)")
	rig.look(Vector2(-PI / 2.0 / rig.LOOK_SPEED, 0))
	check(rig.heading().distance_to(Vector2(-1, 0)) < 0.001, "girou 90° para a esquerda (%s)" % rig.heading())
	rig.free()


func test_wasd_is_relative_to_where_the_camera_looks_and_goes_as_a_step_intent_through_the_channel() -> void:
	var made: Array = _hud_with_channel()
	var hud = made[0]
	var transport = made[1]
	await hud.incarnate(7, "Ana")
	hud.heading = func() -> Vector2: return Vector2(0, -1)
	eq(hud.intent_for(Vector2i(0, -1)), Vector2i(0, -1), "W anda para onde a câmera olha")
	eq(hud.intent_for(Vector2i(1, -1)), Vector2i(1, -1), "W+D na diagonal")
	hud.heading = func() -> Vector2: return Vector2(1, 0)
	eq(hud.intent_for(Vector2i(0, -1)), Vector2i(1, 0), "olhando para +x, W anda para +x")
	eq(hud.intent_for(Vector2i(-1, 0)), Vector2i(0, -1), "e A para a esquerda do olhar")

	var seen: Array = []
	hud.predicted.connect(func(d: Vector2i): seen.append(["prevê", d]))
	hud.confirmed.connect(func(x: int, y: int): seen.append(["motor", Vector2i(x, y)]))
	var key := InputEventKey.new()
	key.keycode = KEY_W
	key.pressed = true
	hud._unhandled_input(key)
	for i in 4:
		await tree.process_frame
	var channel = transport.channels.back()
	var move: Dictionary = channel.sent.back()
	eq([move.command, int(move.dx), int(move.dy)], ["move", 1, 0], "intenção pelo canal persistente, não posição")
	eq(seen, [["prevê", Vector2i(1, 0)], ["motor", Vector2i(6, 5)]], "prevê já e corrige pela posição do motor")
	check(hud.latency_median_ms() >= 0.0 and hud.latencies.size() >= 1, "latência medida do comando à confirmação")
	eq(transport.count_calls("POST /players"), 0, "nada por HTTP com o canal aberto")
	hud.queue_free()


func test_the_interaction_menu_shows_only_what_the_engine_offers_and_sends_the_chosen_one() -> void:
	var made: Array = _hud_with_channel()
	var hud = made[0]
	var transport = made[1]
	await hud.incarnate(7, "Ana")
	var opened: Array = []
	hud.conversation_opened.connect(func(id: int, name: String, session: int): opened.append([id, name, session]))

	var key := InputEventKey.new()
	key.keycode = KEY_E
	key.pressed = true
	hud._unhandled_input(key)
	for i in 4:
		await tree.process_frame
	var texts: Array = []
	for child in hud.menu.get_children():
		if child is Button:
			texts.append(child.text)
	eq(texts, ["Banca: Comprar recurso 1 (4)", "Bia: Conversar", "Bia: Bater", "Porta: Entrar"],
		"uma linha por oferta e verbo do motor (o recurso sem estoque fica de fora)")
	var entries: Array = PlayerHud.menu_entries(OFFERS)
	await hud.interact(entries[1])
	var channel = transport.channels.back()
	var sent: Dictionary = channel.sent.back()
	eq([sent.command, sent.kind, int(sent.id), sent.verb], ["interact", "person", 9, "talk"], "a escolha vai ao motor como está na oferta")
	eq(opened, [[9, "Bia", 77]], "a conversa aceita pelo motor abre o chat de sempre")
	eq(hud.menu.visible, false, "o menu fecha depois da escolha")
	eq(PlayerHud.menu_entries([]), [], "sem oferta, nenhuma linha")
	hud.queue_free()


func test_a_refused_order_shows_the_engine_reason() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("POST /players/p-1/interact", {"status": 409, "body": {"error": "conflict: recusa: Ana não abandona Rui (peso 97)"}})
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	tree.root.add_child(hud)
	await hud.incarnate(7, "Ana")
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.interact({"kind": "person", "id": 8, "verb": "abandon", "resource": -1, "text": "Rui: Abandonar"})
	eq(warnings, ["Ana não abandona Rui (peso 97)"], "o motivo do motor aparece")
	eq(hud.is_active, true, "a recusa não tira o personagem")
	hud.queue_free()
