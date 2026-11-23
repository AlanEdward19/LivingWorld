extends "res://tests/test_base.gd"

const App := preload("res://Scenes/app.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")


func _app() -> Node:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 5 * 3600000, "tickDeltaMs": 3600000, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var detail := Fx.npc_inspect([])
	detail.body["culture"] = {"id": 1}
	detail.body["employer"] = {"value": 4}
	transport.on("GET /npcs/7", detail)
	transport.on("GET /narratives/biographies/7", {"status": 200, "body": {"npcId": 7, "prose": "x", "claims": []}})
	transport.on("GET /narratives/reports", {"status": 200, "body": []})
	transport.on("GET /cities/city-a/treasury", {"status": 200, "body": {"cityId": "city-a", "balance": 0, "fromDay": 0, "toDay": 0, "months": []}})
	transport.on("GET /workplaces/4/trades", {"status": 200, "body": {"workplaceId": 4, "fromDay": 0, "toDay": 0, "days": []}})
	transport.on("GET /cultures/1/technologies", {"status": 200, "body": []})
	var app := App.new()
	app.admin_enabled = true
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	return app


func _open_count(app: Node) -> int:
	var n := 0
	for window in app._info_windows():
		if window.is_open:
			n += 1
	return n


## AD-169 (pedido do dono, ADR-0047): as janelas deixaram de fechar umas às outras. O teste antigo verificava que
## cada entrada abria a sua janela; continua verificando isso, e agora também que as anteriores seguem abertas.
func test_opening_an_info_window_keeps_the_others_open() -> void:
	var app := await _app()
	await app.select_npc(7)

	await app.open_brain(7)
	eq(_open_count(app), 1)
	await app.open_biography(7)
	eq([app.brain_panel.is_open, app.narrative.is_open], [true, true], "narrativa abre e o cérebro continua")
	await app.open_trades(4)
	eq([app.narrative.is_open, app.economy.is_open], [true, true], "economia abre e a narrativa continua")
	app.city_panel.show_city({"id": "city-a", "name": "Alfa", "indicators": {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}})
	await app.open_treasury("city-a")
	eq(app.economy.mode, "treasury", "tesouro substitui as vendas na MESMA janela de economia")
	eq(_open_count(app), 3)
	await app._show_technologies(1)
	eq([app.economy.is_open, app.technology.is_open], [true, true])
	app._toggle_admin()
	eq(app.admin_window.is_open, true, "o console abre junto")
	app._toggle_reports()
	await tree.process_frame
	eq([app.narrative.is_open, app.narrative.mode], [true, "reports"])
	eq(_open_count(app), 5, "as cinco convivem")
	eq(app.router.top(), app.narrative, "a última aberta fica na frente")
	app.queue_free()


func test_open_windows_tile_without_overlapping_and_esc_closes_the_front_one() -> void:
	var app := await _app()
	await app.open_brain(7)
	await app.open_biography(7)
	var rects: Array = app.router.open_windows().map(func(w): return Rect2(w.position, w.size))
	for i in rects.size():
		for j in range(i + 1, rects.size()):
			check(not rects[i].intersects(rects[j]), "encaixe sem sobrepor: %s x %s" % [rects[i], rects[j]])
	await app._show_technologies(1)
	await app.open_trades(4)
	app._toggle_admin()
	var corners := {}
	for window in app.router.open_windows():
		corners[window.position] = true
	eq(corners.size(), 5, "com as cinco abertas nenhuma fica escondida atrás de outra (cada título num lugar)")
	app.admin_window.close()
	app.economy.close()
	var esc := InputEventKey.new()
	esc.keycode = KEY_ESCAPE
	esc.pressed = true
	app._unhandled_input(esc)
	eq([app.technology.is_open, app.narrative.is_open, app.brain_panel.is_open], [false, true, true], "Esc fecha só a da frente")
	app.router.drag(app.narrative, Vector2(40, 30))
	check(app.router.floating.has(app.narrative), "arrastar solta a janela do encaixe")
	eq(app.brain_panel.size.x, app.router.area().size.x, "a que ficou no encaixe ocupa a largura da área")
	app.queue_free()


func test_conversation_and_the_player_hud_are_not_closed_by_info_windows() -> void:
	var app := await _app()
	app.conversation.is_open = true
	app.conversation.visible = true
	await app.open_brain(7)
	eq(app.conversation.is_open, true, "a conversa fica numa faixa própria, embaixo")
	app.conversation.is_open = false
	app.queue_free()


func test_windows_keep_a_minimum_height_and_stay_inside_the_central_area() -> void:
	var app := await _app()
	await app.open_brain(7)
	var area: Rect2 = app.router.area()
	for window in app._info_windows():
		check(window.custom_minimum_size.y >= 220.0, "altura mínima")
	eq(app.brain_panel.position.x, 310.0, "margem fixa à esquerda: o canvas tem sempre 1600 unidades ou mais de largura")
	check(area.encloses(Rect2(app.brain_panel.position, app.brain_panel.size)), "dentro da área central")
	app.queue_free()


## Reescrito no AD-169: antes cada par verificava "só uma aberta"; agora verifica que a segunda entrada abre a sua
## janela sem fechar a primeira (salvo quando é a mesma janela, como tesouro e vendas) e que o HUD do jogador fica.
func test_every_entry_opens_its_window_without_closing_the_other_and_the_player_hud_stays() -> void:
	var app := await _app()
	app.city_panel.show_city({"id": "city-a", "name": "Alfa", "indicators": {"population": 1, "wealth": 1, "health": 1, "inequality": 0.1, "economy": 1, "housing": 1}})
	await app.select_npc(7)
	var entries: Array = [
		["brain", func(): await app.open_brain(7)],
		["biografia", func(): await app.open_biography(7)],
		["cronica", func(): await app.open_chronicle("city-a")],
		["crencas", func(): await app.open_beliefs("city-a")],
		["tesouro", func(): await app.open_treasury("city-a")],
		["vendas", func(): await app.open_trades(4)],
		["tecnologias", func(): await app._show_technologies(1)],
		["console", func(): app._toggle_admin()],
		["relatos", func(): app._toggle_reports()],
	]
	app.player_hud.visible = true
	for first in entries:
		for second in entries:
			if first[0] == second[0]:
				continue
			for window in app._info_windows():
				if window.is_open:
					window.close()
			await first[1].call()
			await tree.process_frame
			var first_window = app.router.top()
			await second[1].call()
			await tree.process_frame
			var second_window = app.router.top()
			check(second_window != null and second_window.is_open, "%s abre a sua janela" % second[0])
			if first_window != second_window:
				eq(first_window.is_open, true, "%s depois de %s: a primeira continua aberta" % [second[0], first[0]])
	eq(app.player_hud.visible, true, "o HUD do jogador não é uma janela de informação")
	app.queue_free()
