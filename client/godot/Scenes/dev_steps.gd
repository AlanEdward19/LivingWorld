extends RefCounted
## Roteiro de desenvolvimento para capturas e demonstrações automáticas:
##   godot --path client/godot -- --steps "wait:2;shot:/tmp/a.png;city;wait:2;shot:/tmp/b.png;enter:0;quit"
## Passos: wait:s · shot:caminho · city (entra na 1ª cidade) · world · enter:ID · exit · floor:N · zoom:fator ·
##         yaw:graus · list · select:ID · brain:ID · whatif:fome · talk:ID · say:texto · timesay:texto (mede quanto a UI espera) · bio:ID · chronicle · reports · player:ID · act:Nome · demo:janela (mostra uma janela com dados de exemplo) · size:LxA · treasury · trades:ID · tech:CULTURA · admin:motivo (pausa com o console) · adminsearch:texto · follow · quit. Não é usado em produção (sem --steps nada roda).


static func parse(args: PackedStringArray) -> Array:
	for i in args.size():
		if args[i] == "--steps" and i + 1 < args.size():
			return args[i + 1].split(";", false)
	return []


static func run(app: Node, steps: Array) -> void:
	for step in steps:
		var name: String = step.get_slice(":", 0)
		var arg: String = step.get_slice(":", 1) if ":" in step else ""
		match name:
			"wait":
				await app.get_tree().create_timer(float(arg)).timeout
			"shot":
				await RenderingServer.frame_post_draw
				app.get_viewport().get_texture().get_image().save_png(arg)
				print("shot ", arg)
			"city":
				var ids: Array = app.store.cities.keys()
				if ids.size() > 0:
					await app.enter_scope("City", ids[0])
			"world":
				await app.enter_scope("World", "")
			"enter":
				await app.enter_building(int(arg))
			"exit":
				await app.exit_building()
			"floor":
				app.stage.set_floor(int(arg))
			"zoom":
				app.rig.zoom_by(float(arg))
			"yaw":
				app.rig.orbit(deg_to_rad(float(arg)))
			"list":
				app.list_panel.visible = true
				await app.list_panel.load_page()
			"select":
				await app.select_npc(int(arg))
			"brain":
				await app.select_npc(int(arg))
				await app.open_brain(int(arg))
			"whatif":
				app.brain_panel.set_sandbox_mode(true)
				app.brain_panel.need_sliders["hunger"].value = float(arg)
				await app.brain_panel.simulate()
			"bio":
				await app.select_npc(int(arg))
				await app.open_biography(int(arg))
			"chronicle":
				var cities: Array = app.store.cities.keys()
				if cities.size() > 0:
					await app.enter_scope("City", cities[0])
					await app.open_chronicle(String(cities[0]))
			"reports":
				await app.narrative.show_reports()
			"player":
				await app.select_npc(int(arg))
				await app.incarnate(int(arg))
			"act":
				await app.player_hud.act(arg)
			"admin":
				app.admin_window.open()
				app.admin_window.reason_input.text = arg
				await app.admin_window.pause()
			"adminsearch":
				app.admin_window.open()
				app.admin_window.tabs.current_tab = 1
				app.admin_window.search_input.text = arg
				await app.admin_window.search()
			"treasury":
				var cities: Array = app.store.cities.keys()
				if cities.size() > 0:
					await app.enter_scope("City", cities[0])
					await app.economy.show_treasury(String(cities[0]), "Cidade")
			"trades":
				await app.open_trades(int(arg))
			"tech":
				await app.technology.show_culture(int(arg))
			"size":
				var wh: PackedStringArray = arg.split("x")
				DisplayServer.window_set_size(Vector2i(int(wh[0]), int(wh[1])))
				await app.get_tree().create_timer(0.5).timeout
			"demo":
				_demo(app, arg)
			"talk":
				await app.select_npc(int(arg))
				await app.talk_to(int(arg))
			"timesay":
				var started := Time.get_ticks_msec()
				app.conversation.send(arg)
				while app.conversation.waiting and Time.get_ticks_msec() - started < 30000:
					await app.get_tree().process_frame
				print("timesay: esperou ", Time.get_ticks_msec() - started, " ms; avisos=", app.toasts.history, " linhas=", app.conversation.lines.size())
			"say":
				await app.conversation.send(arg)
			"follow":
				app.follow_selected()
			"log":
				var moving := 0
				var actions := {}
				for id in app.store.npcs:
					var a: int = app.store.npcs[id].action
					actions[a] = actions.get(a, 0) + 1
				print("log: scope=", app.store.kind, " cursor=", app.stream.cursor, " state=", app.stream.state, " npcs=", app.store.npcs.size(), " actions=", actions)
			"walls":
				var view = app.stage.interior_view
				var heights := {}
				for wall in view._walls:
					heights[snappedf(wall.mesh.size.y, 0.01)] = heights.get(snappedf(wall.mesh.size.y, 0.01), 0) + 1
				print("walls: n=", view._walls.size(), " heights=", heights, " dir=", view._view_dir, " center=", view._center)
			"click", "dblclick", "hover":
				var xy: PackedStringArray = arg.split(",")
				var position := Vector2(float(xy[0]), float(xy[1]))
				var move := InputEventMouseMotion.new()
				move.position = position
				move.global_position = position
				Input.parse_input_event(move)
				await app.get_tree().process_frame
				if name != "hover":
					var press := InputEventMouseButton.new()
					press.position = position
					press.global_position = position
					press.button_index = MOUSE_BUTTON_LEFT
					press.pressed = true
					press.double_click = name == "dblclick"
					Input.parse_input_event(press)
					await app.get_tree().process_frame
				await app.get_tree().create_timer(0.3).timeout
				print("hover: ", app.hover_text.replace("\n", " | "))
			"stress":
				# Carga sintética: N NPCs andando pelo mapa para medir quadros por segundo.
				var count := int(arg)
				var rng := RandomNumberGenerator.new()
				rng.seed = 7
				for i in count:
					var cell := Vector2i(rng.randi_range(0, 29), rng.randi_range(0, 29))
					app.store.npcs[100000 + i] = {"id": 100000 + i, "cell": cell, "action": rng.randi_range(0, 7), "city": ""}
				app.world_view._rebuild_npcs()
				var frames := 0
				var elapsed := 0.0
				while elapsed < 4.0:
					await app.get_tree().process_frame
					elapsed += app.get_process_delta_time()
					frames += 1
					if frames % 30 == 0:
						for i in count:
							var id := 100000 + i
							var from_cell: Vector2i = app.store.npcs[id].cell
							var to_cell := Vector2i(clampi(from_cell.x + rng.randi_range(-1, 1), 0, 29), clampi(from_cell.y + rng.randi_range(-1, 1), 0, 29))
							app.store.npcs[id].cell = to_cell
							app.store.npc_changed.emit(id, from_cell, to_cell, app.store.npcs[id].action)
				print("stress: npcs=", count, " fps=", snappedf(frames / elapsed, 0.1), " (monitor ", Performance.get_monitor(Performance.TIME_FPS), ")")
			"quit":
				await app.release_observation() # como fechar a janela: retira a fonte e encerra a conversa
				app.get_tree().quit()



## Janelas com dados de exemplo (só para capturas de tela): `demo:biografia|cronica|crencas|relatos|tecnologias|tesouro|vendas`.
static func _demo(app: Node, which: String) -> void:
	app._exclusive(null)
	var ok := func(body: Variant) -> Dictionary: return {"ok": true, "status": 200, "body": body}
	var claims := [{"text": "Birth (evento 3): nasceu na vila", "eventIds": [3]}, {"text": "Marriage (evento 9): casou com Ana", "eventIds": [9, 11]},
		{"text": "Death (evento 14): perdeu o irmão numa praga", "eventIds": [14]}]
	match which:
		"biografia":
			app.narrative.mode = "biography"; app.narrative.subject_name = "Beto"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative._title.text = "Biografia de Beto"
			app.narrative._render(ok.call({"npcId": 7, "prose": "Beto cresceu na vila, casou cedo e viu a praga levar a família.", "claims": claims}))
		"cronica":
			app.narrative.mode = "chronicle"; app.narrative.subject_name = "Alfa"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative.period_option.visible = true; app.narrative._title.text = "Crônica de Alfa"
			app.narrative._render(ok.call({"id": 1, "prose": "O ano foi marcado pela praga e pela recuperação da colheita.", "claims": claims}))
		"crencas":
			app.narrative.mode = "beliefs"; app.narrative.subject_name = "Alfa"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative._title.text = "O que Alfa acredita"
			app.narrative._render(ok.call([{"reportId": 1, "distortedMagnitude": 0.83, "distortedTick": 300, "seed": "A praga foi castigo dos vizinhos do norte", "distanceFromFact": 0.62},
				{"reportId": 2, "distortedMagnitude": 0.41, "distortedTick": 900, "seed": "O rei morreu de fome", "distanceFromFact": 0.15}]))
		"relatos":
			app.narrative.mode = "reports"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative._title.text = "Relatos que circulam"
			app.narrative._render(ok.call([{"reportId": 1, "originFactId": 5, "medium": "Song", "confidence": 0.82}, {"reportId": 2, "originFactId": 8, "medium": "Book", "confidence": 0.25}]))
		"tecnologias":
			app.technology.is_open = true; app.technology.visible = true
			app.technology._render(ok.call([{"category": "mobilidade", "vector": [2, 4, 6], "key": "2.4.6", "discoveredAtTick": 500, "isVariation": false, "variationOfKey": null, "adoptedByCities": 1},
				{"category": "mobilidade", "vector": [3, 4, 6], "key": "3.4.6", "discoveredAtTick": 800, "isVariation": true, "variationOfKey": "2.4.6", "adoptedByCities": 3}]))
		"tesouro":
			app.economy.mode = "treasury"; app.economy.is_open = true; app.economy.visible = true; app.economy._title.text = "Tesouro de Alfa"
			app.economy._render(ok.call({"cityId": "x", "balance": 1902, "fromDay": 0, "toDay": 359, "months": [{"month": 0, "revenue": 46, "spending": 0}, {"month": 1, "revenue": 52, "spending": 30}, {"month": 2, "revenue": 40, "spending": 80}]}))
		"vendas":
			app.economy.mode = "trades"; app.economy.is_open = true; app.economy.visible = true; app.economy._title.text = "Vendas de local 0"
			app.economy._render(ok.call({"workplaceId": 0, "fromDay": 0, "toDay": 29, "days": [{"day": 3, "resourceId": 1, "quantity": 30, "value": 30, "purchases": 2}, {"day": 4, "resourceId": 2, "quantity": 20, "value": 20, "purchases": 1}]}))
