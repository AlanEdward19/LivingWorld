extends "res://tests/test_base.gd"

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerProfile := preload("res://Contract/player_profile.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const App := preload("res://Scenes/app.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Fx := preload("res://tests/fixtures.gd")

const CATALOG := [{"actionType": 0, "label": "Comer"}, {"actionType": 1, "label": "Dormir"}, {"actionType": 2, "label": "Trabalhar"},
	{"actionType": 3, "label": "Socializar"}, {"actionType": 5, "label": "Ficar parado"}, {"actionType": 6, "label": "Comprar"}]


func _api(transport, validate: bool = false) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = validate
	return api


func _hud(transport, validate: bool = false) -> Object:
	var hud := PlayerHud.new()
	hud.api = _api(transport, validate)
	hud.labels = ActionLabels.new()
	hud.labels.load_from(CATALOG)
	hud.player_id = "p-1"
	return hud


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("POST /players/p-1/action", {"status": 200, "body": {"playerId": "p-1", "action": "Eat"}})
	transport.on("POST /players/p-1/logout", {"status": 200, "body": {"npcId": 7, "playerId": "p-1", "disappearedAtTick": 321}})
	return transport


func test_profile_is_created_once_and_reused() -> void:
	var path := "user://test_player_profile.cfg"
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))

	var first := PlayerProfile.load_or_create(path)
	var second := PlayerProfile.load_or_create(path)

	check(first.begins_with("p-") and first.length() == 18, "id gerado: " + first)
	eq(second, first, "o mesmo id nas próximas vezes")
	check(FileAccess.file_exists(path), "gravado em disco")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	check(PlayerProfile.load_or_create(path) != first, "sem arquivo, novo id")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))


func test_action_bar_has_exactly_the_choosable_actions_with_server_labels() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana") # o catálogo do servidor chega depois da montagem: os rótulos se atualizam ao encarnar

	eq(hud.action_buttons.keys(), ["Eat", "Sleep", "Work", "Socialize", "Idle", "Buy"], "exatamente PlayerCommands.Choosable")
	var expected := {"Eat": "Comer", "Sleep": "Dormir", "Work": "Trabalhar", "Socialize": "Socializar", "Idle": "Ficar parado", "Buy": "Comprar"}
	for name in expected:
		eq(hud.action_buttons[name].text, expected[name], "%s usa o id certo do catálogo do servidor" % name)
	hud.free()


func test_incarnate_posts_the_player_id_and_shows_the_controlled_npc() -> void:
	var transport := _transport()
	var hud := _hud(transport)

	var ok: bool = await hud.incarnate(7, "Ana")

	eq(ok, true)
	eq(transport.bodies["POST /players/7/incarnate"], [{"playerId": "p-1"}])
	eq(hud.is_active, true)
	eq(hud._title.text, "Você é Ana")
	eq(hud.visible, true)
	hud.free()


func test_incarnate_errors_have_their_own_messages_and_leave_the_hud_closed() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))

	transport.on("POST /players/7/incarnate", {"status": 409, "body": {"error": "conflict: NPC já tem controlador"}})
	await hud.incarnate(7, "Ana")
	transport.on("POST /players/7/incarnate", {"status": 409, "body": {"error": "conflict: jogador já controla outro NPC"}})
	await hud.incarnate(7, "Ana")
	transport.on("POST /players/7/incarnate", {"status": 404, "body": {"error": "not_found: NPC ausente ou morto"}})
	await hud.incarnate(7, "Ana")
	transport.on("POST /players/7/incarnate", {"status": 500, "body": null})
	await hud.incarnate(7, "Ana")

	check("já é controlado" in warnings[0], warnings[0])
	check("já controla outro NPC" in warnings[1], warnings[1])
	check("morreu ou não existe" in warnings[2], warnings[2])
	check("erro 500" in warnings[3], warnings[3])
	eq(hud.is_active, false)
	eq(hud.visible, false)
	hud.free()


func test_actions_post_the_name_and_the_engine_button_posts_null() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")

	await hud.act("Work")
	eq(transport.bodies["POST /players/p-1/action"], [{"action": "Work"}])
	eq(hud.action_buttons["Work"].button_pressed, true, "a escolha fica marcada")
	await hud.act("Eat")
	eq(hud.action_buttons["Work"].button_pressed, false)
	eq(hud.action_buttons["Eat"].button_pressed, true)

	hud.auto_button.pressed.emit()
	await Engine.get_main_loop().process_frame
	eq(transport.bodies["POST /players/p-1/action"].back(), {"action": null}, "'Deixar o motor decidir' envia action nulo")
	eq(hud.current_action, "")
	hud.free()


func test_a_refused_action_warns_and_losing_control_ends_the_hud() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	var released: Array = []
	hud.released.connect(func(): released.append(true))
	await hud.incarnate(7, "Ana")

	transport.on("POST /players/p-1/action", {"status": 400, "body": {"error": "action: fora do que o jogador pode escolher"}})
	await hud.act("Eat")
	eq(warnings.size(), 1, "400 avisa")
	eq(hud.is_active, true, "e o HUD segue")

	transport.on("POST /players/p-1/action", {"status": 404, "body": {"error": "not_found: jogador não controla este NPC"}})
	await hud.act("Eat")
	eq(hud.is_active, false, "404 = perdeu o controle")
	eq(released, [true])
	check("não controla mais Ana" in hud.status_text, hud.status_text)
	hud.free()


func test_logout_posts_and_shows_that_the_character_disappeared_at_the_tick() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	var released: Array = []
	hud.released.connect(func(): released.append(true))
	await hud.incarnate(7, "Ana")

	await hud.logout()

	eq(transport.count_calls("POST /players/p-1/logout"), 1)
	eq(hud.is_active, false)
	check("desapareceu do mundo no tick 321" in hud.status_text and "Não ficou congelado" in hud.status_text, hud.status_text)
	eq(released, [true])
	await hud.logout()
	eq(transport.count_calls("POST /players/p-1/logout"), 1, "inativo não repete")
	hud.free()


func test_paused_world_note_only_while_controlling() -> void:
	var hud := _hud(_transport())
	hud.set_paused(true)
	eq(hud._pause.visible, false, "sem personagem não há o que avisar")
	await hud.incarnate(7, "Ana")
	hud.set_paused(true)
	eq(hud._pause.visible, true, "pausado: avisa")
	check("pausado" in hud._pause.text.to_lower())
	hud.set_paused(false)
	eq(hud._pause.visible, false)
	hud.free()


func test_inactive_hud_requests_nothing_and_only_players_routes_are_used() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	await hud.act("Eat")
	await hud.logout()
	eq(transport.calls, [], "sem personagem nada sai")

	await hud.incarnate(7, "Ana")
	await hud.act("Eat")
	await hud.logout()
	eq(transport.calls.filter(func(c: String) -> bool: return not c.begins_with("POST /players/")), [], "só /players/*")
	hud.free()


func test_responses_are_checked_against_frozen_shapes() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7}})
	var hud := _hud(transport, true)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))

	var ok: bool = await hud.incarnate(7, "Ana")

	eq(ok, false, "resposta fora do contrato não encarna")
	eq(warnings.size(), 1)
	hud.free()


func _find_button(node: Node, text: String) -> Button:
	for child in node.get_children():
		if child is Button and child.text == text:
			return child
		var found := _find_button(child, text)
		if found != null:
			return found
	return null


func _app_transport() -> Object:
	var transport := _transport()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": CATALOG})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 5, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	transport.on("GET /npcs/7", Fx.npc_inspect([]))
	return transport


func test_app_button_and_i_key_incarnate_the_selected_npc_and_the_camera_follows() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	var node := NpcActor.new()
	node.setup(7, Vector2i(1, 1), 0, app.labels)
	app.add_child(node)
	app.world_view.npc_nodes[7] = node
	await app.select_npc(7)

	_find_button(app.npc_panel, "Encarnar (I)").pressed.emit()
	await tree.process_frame
	eq(app.player_hud.is_active, true, "o botão encarna")
	eq(app.rig.following, node, "a câmera segue o personagem")

	await app.player_hud.logout()
	eq(app.rig.following, null, "sair solta a câmera")

	var key := InputEventKey.new()
	key.keycode = KEY_I
	key.pressed = true
	app._unhandled_input(key)
	await tree.process_frame
	eq(app.player_hud.is_active, true, "I encarna o NPC selecionado")
	eq(transport.count_calls("POST /players/7/incarnate"), 2)
	app._unhandled_input(key)
	await tree.process_frame
	eq(transport.count_calls("POST /players/7/incarnate"), 2, "já encarnado: I não repete")
	app.queue_free()


func test_app_forwards_the_admin_pause_to_the_hud() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	await app.player_hud.incarnate(7, "Ana")

	app._on_clock_status({"tick": 6, "ticksPerSecond": 1.0, "isPaused": true, "year": 0})
	eq(app.player_hud._pause.visible, true, "mundo pausado avisa o jogador")
	app._on_clock_status({"tick": 6, "ticksPerSecond": 1.0, "isPaused": false, "year": 0})
	eq(app.player_hud._pause.visible, false)
	app.queue_free()


class SlowTransport extends "res://tests/fake_transport.gd":
	signal release()
	var arrived: Dictionary = {} ## caminho -> quantas requisições chegaram (antes de liberar a resposta)

	func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
		if path.contains("/action") or path.contains("/logout"):
			arrived[path] = int(arrived.get(path, 0)) + 1
			await release
		return await super.request(method, path, body, extra_headers)


func test_a_second_click_while_waiting_does_not_send_another_request() -> void:
	var transport := SlowTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("POST /players/p-1/action", {"status": 200, "body": {"playerId": "p-1", "action": "Eat"}})
	transport.on("POST /players/p-1/logout", {"status": 200, "body": {"npcId": 7, "playerId": "p-1", "disappearedAtTick": 9}})
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")

	hud.act("Eat")
	hud.act("Work")
	eq(transport.arrived.get("/players/p-1/action"), 1, "ação em andamento: o segundo clique é ignorado")
	transport.release.emit()
	await Engine.get_main_loop().process_frame

	hud.logout()
	hud.logout()
	eq(transport.arrived.get("/players/p-1/logout"), 1, "idem para sair")
	transport.release.emit()
	await Engine.get_main_loop().process_frame
	hud.free()


func test_action_and_logout_bodies_outside_the_frozen_shapes_are_refused() -> void:
	var transport := _transport()
	var hud := _hud(transport, true)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.incarnate(7, "Ana")

	transport.on("POST /players/p-1/action", {"status": 200, "body": {"action": "Eat"}})
	await hud.act("Eat")
	eq(warnings.size(), 1, "ação fora do contrato avisa")
	eq(hud.current_action, "", "e não vira escolha")

	transport.on("POST /players/p-1/logout", {"status": 200, "body": {"npcId": 7}})
	await hud.logout()
	eq(hud.is_active, true, "logout fora do contrato não encerra")
	eq(warnings.size(), 2)
	hud.free()


func test_a_server_error_on_an_action_is_not_reported_as_forbidden() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.incarnate(7, "Ana")

	transport.on("POST /players/p-1/action", {"status": 500, "body": null})
	await hud.act("Eat")
	transport.on("POST /players/p-1/action", {"status": 400, "body": {"error": "x"}})
	await hud.act("Eat")

	check("erro 500" in warnings[0] and not ("não é permitida" in warnings[0]), warnings[0])
	check("não é permitida" in warnings[1], warnings[1])
	hud.free()


func test_an_active_hud_makes_no_requests_while_frames_pass() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	tree.root.add_child(hud)
	await hud.incarnate(7, "Ana")
	var before: int = transport.calls.size()
	for i in 5:
		await tree.process_frame
	eq(transport.calls.size(), before, "nenhuma requisição por frame")
	hud.free()
