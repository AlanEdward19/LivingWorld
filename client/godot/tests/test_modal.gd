extends "res://tests/test_base.gd"
## #240: regra de popup. Um registro único de "popup aberto": com um aberto somem a mira, o anel do alvo, a dica do E e os números
## flutuantes; E, clique e pulo não disparam; fechar restaura; o pedido de hold (pausa do mundo, só single player) vai ao abrir
## e é solto ao fechar.

const Modal := preload("res://Scenes/modal.gd")
const PopupHold := preload("res://Scenes/popup_hold.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const Feedback := preload("res://Scenes/feedback.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")


func test_the_registry_follows_visibility_covers_the_hint_and_counts_popups() -> void:
	var modal := Modal.new()
	var a := Control.new()
	var b := PanelContainer.new()
	var hint := Label.new()
	for node in [a, b, hint]:
		tree.root.add_child(node)
	a.visible = false
	b.visible = false
	modal.watch(a)
	modal.watch(b)
	modal.cover(hint)
	var changes: Array = []
	modal.changed.connect(func(on: bool): changes.append(on))
	check(not modal.active and hint.visible, "nada aberto: sem popup")
	a.visible = true
	check(modal.active and not hint.visible, "abriu: ativo e a dica de teclas some")
	b.visible = true
	eq(modal.count(), 2, "dois popups")
	a.visible = false
	check(modal.active and not hint.visible, "ainda há um aberto")
	b.visible = false
	check(not modal.active and hint.visible, "fechou o último: volta")
	eq(changes, [true, false], "só avisa na virada")
	check(a.z_index >= Modal.POPUP_Z, "o popup sobe acima do HUD")
	modal.open("guia")
	check(modal.active and modal.is_open("guia"), "uma chamada abre por chave")
	modal.close("guia")
	check(not modal.active, "e outra fecha")
	for node in [a, b, hint]:
		node.queue_free()


func test_a_popup_freed_while_open_releases_the_registry() -> void:
	var modal := Modal.new()
	var a := Control.new()
	tree.root.add_child(a)
	modal.watch(a)
	a.visible = true
	check(modal.active)
	a.queue_free()
	await tree.process_frame
	check(not modal.active, "janela destruída aberta não prende o mundo")


func test_the_prompt_is_hidden_while_blocked_and_returns_after() -> void:
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	frame.set_prompt("Banca: Comprar")
	check(frame.prompt.visible, "dica visível sem popup")
	frame.blocked = true
	check(not frame.prompt.visible, "popup aberto: sem dica sob a mira")
	frame.set_prompt("Banca: Comprar")
	check(not frame.prompt.visible, "nem uma oferta nova a mostra")
	frame.blocked = false
	check(frame.prompt.visible, "fechou: a dica volta")
	frame.queue_free()


func test_floating_numbers_do_not_appear_while_a_popup_is_open() -> void:
	var fb := Feedback.new()
	tree.root.add_child(fb)
	fb.show_damage(Vector3.ZERO, 5, Color.RED)
	fb._float("+5 comida", Color.WHITE)
	fb.suppressed = true
	for label in fb._dmg_labels:
		check(not label.visible, "número de dano some")
	for label in fb._labels:
		check(not label.visible, "número flutuante some")
	var before := fb.floaters_shown
	fb.show_damage(Vector3.ZERO, 7, Color.RED)
	fb._float("−3 dinheiro", Color.WHITE)
	for label in fb._dmg_labels:
		check(not label.visible, "novo dano não aparece durante o popup")
	check(fb.floaters_shown == before + 1 and not fb._labels[0].visible, "novo número não aparece durante o popup")
	fb.suppressed = false
	fb.show_damage(Vector3.ZERO, 9, Color.RED)
	check(fb._dmg_labels.any(func(l): return l.visible), "fechou: voltam a aparecer")
	fb.queue_free()


func test_with_a_popup_open_e_and_space_do_not_fire() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	var modal := Modal.new()
	hud.input_blocked = func() -> bool: return modal.active
	modal.open("mochila")
	var e := InputEventKey.new()
	e.keycode = KEY_E
	e.pressed = true
	hud._unhandled_input(e)
	check(not hud._e_down, "E não inicia interação com popup aberto")
	var space := InputEventKey.new()
	space.keycode = KEY_SPACE
	space.pressed = true
	var before := transport.count_calls("POST /players/p-1/jump")
	hud._unhandled_input(space)
	eq(transport.count_calls("POST /players/p-1/jump"), before, "Espaço não pula com popup aberto")
	var right := InputEventMouseButton.new()
	right.button_index = MOUSE_BUTTON_RIGHT
	right.pressed = true
	check(not hud.combat_input(right), "o bloqueio de golpe e esquiva também vale")
	modal.close("mochila")
	hud._unhandled_input(e)
	check(hud._e_down, "fechado o popup, E volta a funcionar")
	hud.frame.queue_free()
	hud.queue_free()


func test_hold_is_requested_on_open_and_released_on_close_only_with_a_channel() -> void:
	var transport := FakeTransport.new()
	transport.channel_replier = func(command: Dictionary) -> Dictionary:
		return {"seq": command.seq, "ok": true, "status": 200, "body": {"held": command.on, "leaseMs": 30000}}
	var api := ApiClient.new(transport)
	var modal := Modal.new()
	var hold := PopupHold.new()
	hold.api = api
	hold.bind(modal)
	tree.root.add_child(hold)
	modal.open("conversa") # sem canal (espectador): nada a pedir
	modal.close("conversa")
	eq(hold.sent, [], "sem canal aberto não há pedido")
	api.player_token = "tok"
	api.open_player_channel()
	modal.open("conversa")
	modal.open("mochila")
	await tree.process_frame
	modal.close("conversa")
	modal.close("mochila")
	await tree.process_frame
	var sent: Array = api.player_channel.sent.map(func(c): return [c.command, c.on])
	eq(sent, [["hold", true], ["hold", false]], "um pedido ao abrir o primeiro e um ao soltar o último")
	check(hold._timer.is_stopped(), "renovação parada depois de soltar")
	modal.open("conversa")
	check(not hold._timer.is_stopped(), "aberto: o relógio de renovação roda")
	hold._timer.timeout.emit()
	eq(hold.sent.size(), 4, "a renovação reenvia o pedido ligado (o servidor vence em 30 s sem ela)")
	modal.close("conversa")
	hold.queue_free()


func test_the_app_hides_crosshair_ring_prompt_and_asks_for_the_hold_then_restores() -> void:
	var transport := FakeTransport.new()
	transport.channel_replier = func(command: Dictionary) -> Dictionary:
		return {"seq": command.seq, "ok": true, "status": 200, "body": {"held": command.on}}
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 0, "tickDeltaMs": 3600000, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	app.api.player_token = "tok"
	app.api.open_player_channel()
	app.rig.set_mode(app.rig.THIRD)
	app.crosshair.visible = true
	app.aim_highlight.show_at("stall", 3, Vector3.ZERO)
	app.player_frame.set_prompt("Banca: Comprar")
	app.player_frame.visible = true

	app.player_frame.inventory.visible = true # a mochila (I/Tab)
	check(app.modal.active, "a mochila abre o registro")
	check(not app.crosshair.visible, "mira some")
	check(not app.aim_highlight.visible, "anel do alvo some")
	check(not app.player_frame.prompt.visible, "dica do E some")
	check(not app.player_frame.hints.visible, "dica de teclas some")
	check(app.feedback.suppressed, "números flutuantes suspensos")
	app._on_player_target("stall", 3, Vector2(1, 1))
	check(not app.aim_highlight.visible, "um alvo novo não desenha o anel com popup aberto")
	await tree.process_frame
	eq(app.api.player_channel.sent.map(func(c): return c.on), [true], "pedido de hold enviado ao abrir")

	app.player_frame.inventory.visible = false
	check(not app.modal.active, "fechou")
	check(app.crosshair.visible, "mira volta")
	check(app.player_frame.prompt.visible, "dica volta")
	check(app.player_frame.hints.visible, "dica de teclas volta")
	check(not app.feedback.suppressed, "números voltam")
	await tree.process_frame
	eq(app.api.player_channel.sent.map(func(c): return c.on), [true, false], "hold solto ao fechar")

	app.pause_menu.visible = true # o menu de pausa e a ajuda também são popups
	check(app.modal.active, "menu de pausa abre o registro")
	app.pause_menu.visible = false
	app.first_steps.open_help()
	check(app.modal.active, "ajuda F1 abre o registro")
	app.first_steps.toggle_actions() # #238: a página "O que dá para fazer" vive na ajuda: continua popup
	check(app.modal.active, "a lista de ações é popup")
	app.first_steps.close_help()
	check(not app.modal.active, "ajuda fechada solta")
	app.player_frame.guide.visible = true # #238: o guia da primeira posse
	check(app.modal.active and not app.crosshair.visible and not app.player_frame.prompt.visible, "o guia esconde a mira e a dica do E")
	app.player_frame.guide.visible = false
	check(not app.modal.active and app.crosshair.visible, "fechou o guia: volta")
	app.queue_free()


func test_typing_in_a_popup_text_field_stops_wasd_from_walking_the_body() -> void:
	var hud := PlayerHud.new()
	tree.root.add_child(hud)
	var field := LineEdit.new()
	tree.root.add_child(field)
	check(not hud.typing(), "sem foco em texto o corpo anda")
	field.grab_focus()
	check(hud.typing(), "campo de texto com foco: WASD é do texto")
	field.release_focus()
	check(not hud.typing(), "sem foco, volta a andar")
	field.queue_free()
	hud.queue_free()
