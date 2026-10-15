extends RefCounted
## Roteiro de desenvolvimento para capturas e demonstrações automáticas:
##   godot --path client/godot -- --steps "wait:2;shot:/tmp/a.png;city;wait:2;shot:/tmp/b.png;enter:0;quit"
## Passos: wait:s · shot:caminho · city (entra na 1ª cidade) · world · enter:ID · exit · floor:N · zoom:fator ·
##         yaw:graus · list · select:ID · brain:ID · whatif:fome · talk:ID · say:texto · bio:ID · chronicle · reports · follow · quit. Não é usado em produção (sem --steps nada roda).


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
			"talk":
				await app.select_npc(int(arg))
				await app.talk_to(int(arg))
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
				app.get_tree().quit()
