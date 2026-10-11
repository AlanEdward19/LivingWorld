extends RefCounted
## Roteiro de desenvolvimento para capturas e demonstrações automáticas:
##   godot --path client/godot -- --steps "wait:2;shot:/tmp/a.png;city;wait:2;shot:/tmp/b.png;enter:0;quit"
## Passos: wait:s · shot:caminho · city (entra na 1ª cidade) · world · enter:ID · exit · floor:N · zoom:fator ·
##         yaw:graus · list · select:ID · follow · quit. Não é usado em produção (sem --steps nada roda).


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
			"quit":
				app.get_tree().quit()
