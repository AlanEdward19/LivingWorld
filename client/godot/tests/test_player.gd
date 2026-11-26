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
	transport.on("POST /players/p-1/logout", {"status": 200, "body": {"npcId": 7, "playerId": "p-1", "disappearedAtMs": 321 * 3600000}})
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
	eq(hud.stale_session, true, "o 409 de 'já controla outro NPC' abre a oferta de sair; os demais erros não")
	hud.free()

	var closed := FakeTransport.new()
	closed.on("POST /players/7/incarnate", {"status": 404, "body": {"error": "not_found: NPC ausente ou morto"}})
	var other := _hud(closed)
	await other.incarnate(7, "Ana")
	eq(other.visible, false, "404 não abre o HUD")
	other.free()


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
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 5 * 3600000, "tickDeltaMs": 3600000, "year": 0}})
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


## AD-197p: encarnado (3ª/1ª pessoa) some o que é do espectador e o topo fica compacto; ao sair, tudo volta.
func test_incarnating_hides_the_spectator_ui_and_leaving_restores_it() -> void:
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
	app.timeline.visible = true
	var before := [app.side_panel.visible, app.minimap.visible, app.timeline.visible, app.help_label.text]

	await app.incarnate(7)
	await tree.process_frame
	var playing := [app.player_mode, app.side_panel.visible, app.minimap.visible, app.timeline.visible,
		app.help_label.text.begins_with("WASD"), app.player_hud.orders.visible, app.player_hud.orders_button.visible,
		app.player_hud.logout_button.is_visible_in_tree(), node.is_self]
	app._on_events([{"eventId": 1, "kind": 0, "atMs": 3600000, "label": "Um casamento"}])
	var toasts_while_playing: Array = app.toasts.visible_texts()

	await app.player_hud.logout()
	await tree.process_frame
	var after := [app.side_panel.visible, app.minimap.visible, app.timeline.visible, app.help_label.text]

	eq(playing, [true, false, false, false, true, false, true, false, true]) # #222: sair do personagem fica no menu do Esc
	eq(toasts_while_playing, [], "acontecimento não vira aviso sobre a cena do jogador")
	eq([after, app.player_mode, node.is_self], [before, false, false], "o espectador volta intacto")
	app.queue_free()


func test_tab_opens_the_orders_and_releases_the_cursor_and_the_reach_hint_comes_from_the_offer() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")
	var toggles: Array = []
	hud.menu_toggled.connect(func(open: bool): toggles.append(open))
	hud.set_rpg(true)
	var closed: bool = hud.orders.visible

	hud.toggle_orders()
	var opened: bool = hud.orders.visible
	hud.toggle_orders()
	hud.set_reach_hint([{"kind": "stall", "id": 3, "verbs": ["buy"], "resources": [{"resourceId": 1, "quantity": 2, "price": 4}]}])
	var hint: String = hud.reach_hint.text
	hud.set_reach_hint([])

	eq([closed, opened, toggles, hint, hud.reach_hint.visible], [false, true, [true, false], "Banca: Comprar recurso 1 (4)", false])
	hud.set_rpg(false)
	eq(hud.orders.visible, true, "fora do modo jogador as ordens ficam à vista, como antes")
	hud.free()


func test_the_own_body_shows_no_name_action_or_ring() -> void:
	var node := NpcActor.new()
	node.setup(7, Vector2i(1, 1), 0, ActionLabels.new())
	node.set_display_name("Ana")
	node.set_selected(true)
	var shown := node.label_visible()
	node.set_self(true)
	eq([shown, node.label_visible()], [true, false])
	node.set_self(false)
	eq(node.label_visible(), true)
	node.free()


func test_app_forwards_the_admin_pause_to_the_hud() -> void:
	var transport := _app_transport()
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	await app.player_hud.incarnate(7, "Ana")

	app._on_clock_status({"atMs": 6 * 3600000, "ticksPerSecond": 1.0, "isPaused": true, "year": 0})
	eq(app.player_hud._pause.visible, true, "mundo pausado avisa o jogador")
	app._on_clock_status({"atMs": 6 * 3600000, "ticksPerSecond": 1.0, "isPaused": false, "year": 0})
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
	transport.on("POST /players/p-1/logout", {"status": 200, "body": {"npcId": 7, "playerId": "p-1", "disappearedAtMs": 9 * 3600000}})
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


func test_a_409_for_a_leftover_session_offers_to_leave_and_then_allows_incarnating_again() -> void:
	var transport := _transport()
	transport.on("POST /players/7/incarnate", {"status": 409, "body": {"error": "conflict: jogador já controla outro NPC"}})
	var hud := _hud(transport)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))

	var ok: bool = await hud.incarnate(7, "Ana")

	eq(ok, false)
	eq(hud.stale_session, true, "o servidor ainda liga este jogador a um NPC")
	eq(hud.logout_button.text, "Sair do personagem anterior", "o botão diz de qual personagem se sai")
	eq(hud.visible, true, "o HUD abre só para sair")
	eq(hud.action_buttons["Eat"].disabled, true, "sem ações enquanto não sair")
	check("ainda controla outro NPC" in hud.status_text, hud.status_text)
	eq(hud.logout_button.disabled, false)

	await hud.logout()
	eq(transport.count_calls("POST /players/p-1/logout"), 1, "sair da sessão anterior")
	eq(hud.stale_session, false)
	eq(hud.visible, false)
	eq(hud.logout_button.text, "Sair do personagem", "e volta ao rótulo normal ao liberar")
	check("liberado" in warnings.back(), warnings.back())

	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	eq(await hud.incarnate(7, "Ana"), true, "agora encarna")
	eq(hud.action_buttons["Eat"].disabled, false, "ações liberadas de novo")
	hud.free()


func test_leaving_a_leftover_session_failure_keeps_the_offer() -> void:
	var transport := _transport()
	transport.on("POST /players/7/incarnate", {"status": 409, "body": {"error": "conflict: jogador já controla outro NPC"}})
	transport.on("POST /players/p-1/logout", {"status": 500, "body": null})
	var hud := _hud(transport)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.incarnate(7, "Ana")

	await hud.logout()

	eq(hud.stale_session, true, "falhou: a oferta continua")
	check("erro 500" in warnings.back(), warnings.back())
	hud.free()


func test_a_double_click_on_incarnate_sends_a_single_request() -> void:
	var transport := SlowIncarnate.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	var hud := _hud(transport)

	hud.incarnate(7, "Ana") # fica esperando
	var second: bool = await hud.incarnate(7, "Ana")
	eq(second, false, "o segundo clique é ignorado")
	eq(transport.arrived, 1, "um único POST")
	transport.release.emit()
	await Engine.get_main_loop().process_frame
	eq(hud.is_active, true)
	var third: bool = await hud.incarnate(7, "Ana")
	eq(third, false, "já encarnado: nada de novo POST")
	eq(transport.arrived, 1)
	hud.free()


func test_the_powers_the_npc_carries_become_buttons_that_post_use_power_with_the_power_id() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")
	hud.set_powers(["teleport-power"])

	eq(hud.power_buttons.keys(), ["teleport-power"], "um botão por poder que o servidor diz que ele porta")
	hud.power_buttons["teleport-power"].pressed.emit()
	await Engine.get_main_loop().process_frame
	eq(transport.bodies["POST /players/p-1/action"].back(), {"action": "UsePower", "powerId": "teleport-power"}, "UsePower leva o poder")
	eq(hud.power_buttons["teleport-power"].button_pressed, true, "a escolha fica marcada")
	await hud.act("Work")
	eq(transport.bodies["POST /players/p-1/action"].back(), {"action": "Work"}, "as outras ações não levam powerId")
	eq(hud.power_buttons["teleport-power"].button_pressed, false)
	await hud.logout()
	eq(hud.power_buttons.size(), 0, "sem personagem, sem poderes")
	hud.free()


func test_with_a_session_the_player_comes_from_the_token_and_commands_go_through_the_persistent_channel() -> void:
	var transport := _transport()
	transport.on("POST /sessions", {"status": 200, "body": {"sessionToken": "tok-1", "playerId": "p-srv"}})
	transport.channel_replier = func(command: Dictionary) -> Dictionary:
		var body := {"npcId": 7, "playerId": "p-srv"} if command.command == "incarnate" else {"playerId": "p-srv", "action": command.get("action")}
		return {"seq": command.seq, "ok": true, "status": 200, "body": body}
	var hud := _hud(transport)
	hud.session_enabled = true

	var ok: bool = await hud.incarnate(7, "Ana") # a sessão nasce aqui e o canal persistente abre com o token
	eq(ok, true)
	eq(hud.player_id, "p-srv", "o jogador vem da sessão do servidor, não do id local")
	eq(transport.channel_headers["WS /players/ws"], {"Authorization": "Bearer tok-1"}, "o canal abre com o token")

	await hud.act("Work")
	var channel = transport.channels.back()
	eq(channel.sent.size(), 2, "conectado, os comandos vão pelo canal persistente")
	eq([channel.sent[0].command, channel.sent[1].command], ["incarnate", "action"])
	eq(channel.sent[1].action, "Work")
	eq([int(channel.sent[0].seq), int(channel.sent[1].seq)], [1, 2], "comandos numerados")
	eq(hud.current_action, "Work", "a resposta pelo canal vale como a da rota")
	eq(transport.count_calls("POST /players"), 0, "sem requisição HTTP enquanto o canal está aberto")

	channel.drop() # o canal caiu: a rota HTTP segue valendo, com o token
	await hud.act("Eat")
	eq(transport.headers_of["POST /players/p-srv/action"], [{"Authorization": "Bearer tok-1"}], "a rota leva o token")
	hud.free()


func test_direction_keys_ask_the_engine_for_one_step_through_the_move_command() -> void:
	var transport := _transport()
	transport.on("POST /players/p-1/move", {"status": 200, "body": {"playerId": "p-1", "npcId": 7, "x": 4, "y": 3}})
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")

	await hud.step(1, 0)
	eq(transport.bodies["POST /players/p-1/move"], [{"dx": 1, "dy": 0}], "intenção de passo, não posição")
	var key := InputEventKey.new()
	key.keycode = KEY_W
	key.pressed = true
	eq(hud.STEP_KEYS[KEY_W], Vector2i(0, -1), "W anda para cima")
	transport.on("POST /players/p-1/move", {"status": 409, "body": {"error": "conflict: mundo pausado pelo admin"}})
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.step(0, -1)
	check(warnings.size() == 1 and "pausado" in warnings[0], "pausa avisa")
	eq(hud.is_active, true, "pausa não tira o personagem")
	hud.free()


func test_a_server_without_sessions_keeps_the_local_mode() -> void:
	var transport := _transport()
	transport.on("POST /sessions", {"status": 404, "body": null})
	var hud := _hud(transport)
	hud.session_enabled = true
	var ok: bool = await hud.incarnate(7, "Ana")
	eq(ok, true)
	eq(hud.player_id, "p-1", "servidor antigo: segue com o id local")
	eq(transport.bodies["POST /players/7/incarnate"], [{"playerId": "p-1"}])
	hud.free()


## AD-197c: o ritmo do tempo e os controles de dormir e acelerar aparecem só quando o motor diz que valem (quem joga sozinho).
func test_time_controls_follow_what_the_engine_says() -> void:
	var transport := _transport()
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")

	hud.apply_time({"rhythm": "RealTime", "timeScale": 1, "canSkip": false, "skipping": false, "sleeping": false})
	var hidden: bool = not hud.sleep_until_button.visible and not hud.accelerate_button.visible and not hud.wake_button.visible
	hud.apply_time({"rhythm": "RealTime", "timeScale": 12, "canSkip": true, "skipping": false, "sleeping": false})
	var shown: bool = hud.sleep_until_button.visible and hud.sleep_fast_button.visible and hud.accelerate_button.visible

	eq([hidden, shown, hud.rhythm_label.text], [true, true, "Tempo real 1:12"])
	hud.free()


func test_sleep_posts_the_hour_and_the_engine_reason_for_waking_becomes_a_notice() -> void:
	var transport := _transport()
	transport.on("POST /players/p-1/sleep", {"status": 200, "body": {"rhythm": "Accelerated", "tickDeltaMs": 3600000, "timeScale": 1,
		"playersPresent": 1, "canSkip": false, "skipping": true, "sleeping": true, "untilMs": 30 * 3600000}})
	var hud := _hud(transport, true)
	await hud.incarnate(7, "Ana")

	await hud.sleep(6)
	var asleep: Array = [transport.bodies["POST /players/p-1/sleep"], hud.wake_button.visible, hud.wake_button.text, hud.rhythm_label.text]
	hud.apply_time({"rhythm": "RealTime", "timeScale": 1, "canSkip": true, "skipping": false, "sleeping": false, "lastWakeReason": "golpe"})

	eq(asleep, [[{"untilHour": 6}], true, "Acordar", "Tempo acelerado"])
	eq([hud.wake_button.visible, hud.status_text], [false, "Você acordou com um golpe."])
	hud.free()


func test_a_refused_skip_with_another_player_warns() -> void:
	var transport := _transport()
	transport.on("POST /players/p-1/accelerate", {"status": 409, "body": {"error": "conflict: só quem joga sozinho pula o tempo"}})
	var hud := _hud(transport)
	var warnings: Array = []
	hud.warning.connect(func(text): warnings.append(text))
	await hud.incarnate(7, "Ana")

	await hud.accelerate()

	eq(warnings, ["Só quem joga sozinho pode pular o tempo agora."])
	hud.free()


class SlowIncarnate extends "res://tests/fake_transport.gd":
	signal release()
	var arrived := 0

	func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
		if path.contains("/incarnate"):
			arrived += 1
			# solta sozinho em 0,5 s: um POST a mais (guard removido) vira falha de asserção, não um teste pendurado
			Engine.get_main_loop().create_timer(0.5).timeout.connect(func(): release.emit(), CONNECT_ONE_SHOT)
			await release
		return await super.request(method, path, body, extra_headers)
