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
			"walk": body = {"playerId": "p-1", "npcId": 7, "x": 5, "y": 5, "fx": 5.5, "fy": 5.5, "speedMps": 1.4, "cellMeters": 1.5}
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
	for i in 8: # #231: a troca é animada; terminada, a câmera está nos olhos
		rig._process(0.05)
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

	eq(hud.step_seconds > 0.0, true)
	await hud.step(1, 0) # o passo de uma célula (comando `move`) segue valendo
	var channel = transport.channels.back()
	var move: Dictionary = channel.sent.back()
	eq([move.command, int(move.dx), int(move.dy)], ["move", 1, 0], "intenção pelo canal persistente, não posição")

	# AD-197k: o passo solto: rumo contínuo pelo olhar, previsão já e correção pela posição fina do motor
	hud.heading = func() -> Vector2: return Vector2(0, -1)
	eq(hud.walk_direction(Vector2i(0, -1)), Vector2(0, -1), "W: o rumo do olhar")
	check(hud.walk_direction(Vector2i(1, -1)).distance_to(Vector2(1, -1).normalized()) < 0.001, "W+D: diagonal contínua")
	hud.set_process(false) # o teste faz o papel do quadro (sem teclas de verdade apertadas)
	var fines: Array = []
	hud.settled.connect(func(p: Vector2): fines.append(["motor", p]))
	hud.glided.connect(func(p: Vector2): fines.append(["prevê", p]))
	hud.glide(Vector2(1, 0), 0.016)
	for i in 4:
		await tree.process_frame
	var walk: Dictionary = channel.sent.back()
	eq([walk.command, int(walk.dx), int(walk.dy)], ["walk", 1000, 0], "o rumo vai em milésimos pelo canal")
	eq(fines.back(), ["motor", Vector2(5.5, 5.5)], "a posição fina do motor manda")
	check(is_equal_approx(hud.speed_cells, 1.4 / 1.5), "células por segundo pela velocidade do corpo (%s)" % hud.speed_cells)
	hud.glide(Vector2(1, 0), 0.5)
	check(fines.back()[0] == "prevê" and fines.back()[1].x > 5.5, "anda já na tela, antes da resposta")
	for i in 4:
		await tree.process_frame
	hud.glide(Vector2.ZERO, 0.016)
	for i in 4:
		await tree.process_frame
	eq([int(channel.sent.back().dx), int(channel.sent.back().dy)], [0, 0], "soltou a tecla: o rumo para")
	check(hud.latency_median_ms() >= 0.0 and hud.latencies.size() >= 2, "latência medida do comando à confirmação")
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


# --- AD-197j: no app (câmera, mira, golpe animado, passo no tempo do corpo, nomes de recurso) ---------------------------

const App := preload("res://Scenes/app.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Fx := preload("res://tests/fixtures.gd")


func _app_transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("POST /players/p-1/strike", {"status": 200, "body": {"playerId": "p-1", "targetId": 9}})
	transport.on("POST /players/p-1/move", {"status": 200, "body": {"playerId": "p-1", "npcId": 7, "x": 2, "y": 1, "readyInMs": 1800}})
	transport.on("POST /players/p-1/logout", {"status": 200, "body": {"npcId": 7, "playerId": "p-1", "disappearedAtMs": 3600000}})
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 5 * 3600000, "tickDeltaMs": 3600000, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	transport.on("GET /npcs/7", Fx.npc_inspect([]))
	return transport


func test_in_the_app_the_crosshair_strike_pace_and_resource_names_follow_the_engine() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	app.player_hud.session_enabled = false
	var node := NpcActor.new()
	node.setup(7, Vector2i(1, 1), 0, app.labels)
	app.add_child(node)
	app.world_view.npc_nodes[7] = node

	await app.incarnate(7)
	eq(app.rig.mode, app.rig.THIRD, "encarnado: 3ª pessoa")
	eq(app.crosshair.visible, true, "mira no centro no modo jogador")
	eq(app.aim_point(), app.get_viewport().get_visible_rect().size / 2.0, "o golpe mira o centro da tela")
	app.rig.yaw = 0.0
	eq(app.player_hud.heading.call(), app.rig.heading(), "WASD segue o olhar da câmera do app")

	app.player_hud.struck.emit(9)
	check(node._strike_until > node._clock, "o braço do personagem desce no golpe")
	await app.player_hud.step(1, 0)
	check(is_equal_approx(app.player_hud.step_seconds, 1.8), "o próximo passo espera o tempo do corpo que o motor disse (%s)" % app.player_hud.step_seconds)
	app.registry.resources[1] = "pão"
	var entries: Array = PlayerHud.menu_entries(OFFERS, app.player_hud.resource_name_of)
	eq(entries[0].text, "Banca: Comprar pão (4)", "o nome real do recurso no menu")

	eq(app.rig.cursor_captured, true, "AD-197k: no modo jogador o cursor já entra preso (o mouse olha sem botão)")
	app.player_hud.show_menu([])
	eq(app.rig.cursor_captured, false, "o menu de interações solta o cursor")
	app.player_hud.close_menu()
	eq(app.rig.cursor_captured, true, "fechado, prende de novo")
	var z := InputEventKey.new()
	z.keycode = KEY_Z
	z.pressed = true
	app.rig._unhandled_input(z)
	eq(app.rig.cursor_captured, false, "Z solta para os menus")
	app.rig._unhandled_input(z)
	eq(app.rig.cursor_captured, true, "e Z prende de novo")
	app.player_hud.settled.emit(Vector2(3.25, 1.5))
	check(node.position.distance_to(Vector3(3.25, node.position.y, 1.5)) < 0.001 and node.driven, "o boneco anda pela posição fina")
	await app.player_hud.logout()
	eq([app.rig.mode, app.crosshair.visible, app.rig.cursor_captured], [app.rig.ISO, false, false], "saiu: isométrica, sem mira, cursor solto")
	app.queue_free()


func test_invented_items_show_with_the_engine_label_and_verbs_only() -> void:
	var entries: Array = PlayerHud.menu_entries([
		{"kind": "item", "id": 41, "label": "recipiente", "verbs": ["take"], "x": 1, "y": 1, "resources": []},
		{"kind": "item", "id": 42, "label": "veículo", "verbs": ["return"], "x": 1, "y": 1, "resources": []}])
	eq(entries.map(func(e): return e.text), ["recipiente: Pegar", "veículo: Devolver"], "o nome e o verbo vêm da oferta do motor")
	eq([entries[0].kind, entries[0].id, entries[0].verb], ["item", 41, "take"], "a escolha vai ao motor como está na oferta")
