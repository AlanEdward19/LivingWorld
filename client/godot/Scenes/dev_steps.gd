extends RefCounted
## Roteiro de desenvolvimento para capturas e demonstrações automáticas:
##   godot --path client/godot -- --steps "wait:2;shot:/tmp/a.png;city;wait:2;shot:/tmp/b.png;enter:0;quit"
## Passos: wait:s · shot:caminho · city (entra na 1ª cidade) · world · enter:ID · exit · floor:N · zoom:fator ·
##         yaw:graus · list · select:ID · brain:ID · whatif:fome · talk:ID · gametalk:ID (conversa do modo jogador, #237) · pick:N (fala pronta N) · say:texto · timesay:texto (mede quanto a UI espera) · bio:ID · chronicle · reports · player:ID · act:Nome · creator · creatorstep:N (etapa do criador, #222) · creatorrandom · creatorset:campo=valor,... · creatorcreate (AD-197t) · creatorpick:N (AD-197q) · materialize (aperta Ver em detalhe) · demo:janela (mostra uma janela com dados de exemplo) · size:LxA · treasury · trades:ID · tech:CULTURA · admin:motivo (pausa com o console) · adminsearch:texto · follow · near:zoom · selectfirst · building · next · watch · benchbig:N · third/first (câmera do jogador, AD-197d) · cam:first|third (só o modo, AD-197o) · playstreet (encarna quem está na rua) · unplayer (sai do personagem; medida pareada) · stacks (conta pessoas sobrepostas, AD-197r) · xray · plan · street · lot · ground (AD-177) · roads · walker · heat (AD-181) · pause (menu de pausa) · tips:N (dica N) · help · quit. Não é usado em produção (sem --steps nada roda).


const Registry := preload("res://Store/entity_registry.gd")
const Grid := preload("res://Scenes/grid.gd")
const FrameCost := preload("res://Contract/frame_cost.gd")


static func parse(args: PackedStringArray) -> Array:
	for i in args.size():
		if args[i] == "--steps" and i + 1 < args.size():
			return args[i + 1].split(";", false)
	return []


static func _print_warning(text: String) -> void:
	print("aviso: ", text)


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
			"city": # `city:<id>` entra nessa cidade (AD-193: a fumaça escolhe a que tem obra); sem id, a primeira
				var ids: Array = app.store.cities.keys()
				if arg != "":
					await app.enter_scope("City", arg)
				elif ids.size() > 0:
					await app.enter_scope("City", ids[0])
			"world":
				await app.enter_scope("World", "")
			"enter":
				await app.enter_building(int(arg))
			"exit":
				await app.exit_building()
			"placed": # #237: `placed:-1` faz de conta que o motor disse "saiu do prédio" (`placed:<id>` entra), como a pose faz
				await app._on_player_placed(int(arg), 0)
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
			"unplayer": # AD-197r: sai do personagem (o servidor o libera; encarnar o mesmo corpo na rodada seguinte não dá 409)
				await app.player_hud.logout()
				print("unplayer: ativo=", app.player_hud.is_active)
			"act":
				await app.player_hud.act(arg)
			"creator": # AD-197q: abre o criador de personagem (cidades e primeira amostra do motor)
				await app.open_creator()
			"creatorset": # AD-197t: `creatorset:name=Ana,age=29,role=child,family=0,money=90,height=1.65,look.skinTone=30,outfit=3` preenche o criador
				await _creator_set(app, arg)
			"creatorstep": # #222: `creatorstep:N` vai à etapa N do criador (1 Origem … 5 Revisar), com a prévia do motor
				await app.creator.designer.go_to(int(arg) - 1)
			"creatorrandom": # #222: sorteia a aparência no cliente (o motor valida)
				app.creator.designer._randomize_look(app.creator.designer._all_look_keys())
				await app.creator.designer.refresh()
			"creatorcreate": # AD-197t: cria a pessoa montada e entra
				await app.creator.designer.refresh()
				await app.creator.designer.create()
			"creatorpick": # AD-197q: `creatorpick:N` entra como o N-ésimo candidato que a vontade deixa (0 = o primeiro)
				if app.creator.mode != "sample":
					await app.creator.set_mode("sample")
				var open_cards: Array = app.creator.cards.filter(func(c): return not c.button.disabled)
				if int(arg) < open_cards.size():
					var card: Dictionary = open_cards[int(arg)]
					var who: String = ""
					for c in app.creator.sample.get("candidates", []):
						if int(c.npcId) == int(card.npc_id):
							who = String(c.name)
					await app.creator.choose(int(card.npc_id), who)
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
			"materialize":
				await app.npc_panel.materialize()
			"talk":
				await app.select_npc(int(arg))
				await app.talk_to(int(arg))
			"timesay":
				var started := Time.get_ticks_msec()
				app.conversation.send(arg)
				while app.conversation.waiting and Time.get_ticks_msec() - started < 30000:
					await app.get_tree().process_frame
				print("timesay: esperou ", Time.get_ticks_msec() - started, " ms; avisos=", app.toasts.history, " linhas=", app.conversation.lines.size())
			"gametalk": # #237: a conversa do modo jogador (caixa de jogo com escolhas) com o NPC `arg`, sem depender de quem está ao lado
				app._on_player_menu(true)
				app.conversation.game_style = app.player_mode
				await app.conversation.start(int(arg), app.registry.person_name(int(arg)))
			"toasts":
				print("toasts: ", app.toasts.history)
			"pick":
				await app.conversation.pick(int(arg) - 1)
			"say":
				await app.conversation.send(arg)
			"follow":
				app.follow_selected()
			"pause":
				await app.pause_menu.open()
			"tips":
				app.first_steps.start(true)
				for _i in int(arg):
					app.first_steps.next_tip()
			"help":
				app.first_steps.open_help()
				if arg == "actions": # #238: a página "O que dá para fazer" da ajuda do jogador
					app.first_steps.toggle_actions()
			"guide": # #238: o guia da primeira posse (`guide:N` vai ao passo N, de 1; `guide:close` fecha)
				if arg == "close":
					app.player_hud.frame.guide.close()
					continue
				await app.player_hud.show_guide()
				var guide = app.player_hud.frame.guide
				for _i in maxi(int(arg) - 1, 0):
					guide.go(1)
				print("guide: ", guide.steps.size(), " passos; passo ", guide.index + 1, ": ", guide.steps[guide.index].text if guide.visible else "(fechado)")
			"xray": # AD-177: raio-x, planta, ficha da rua mais usada e do lote com quintal, contagem do chão
				app.reading.toggle_xray()
			"focuswork": # AD-193: câmera e ficha na primeira obra do escopo (captura da fumaça)
				for work_id in app.store.works:
					app.focus_point(app.point_of({"kind": "work", "id": int(work_id)}))
					await app.inspect({"kind": "work", "id": int(work_id)})
					break
			"plan":
				app.reading.toggle_plan()
			"street":
				var best := Vector2i(-1, -1)
				for cell in app.world_view.ground.drawn_roads:
					if best == Vector2i(-1, -1) or int(app.store.roads[cell].users) > int(app.store.roads[best].users):
						best = cell
				if best != Vector2i(-1, -1):
					await app.inspect(Registry.ref(Registry.STREET, "%d,%d" % [best.x, best.y]))
					app.rig.focus(Grid.to_world(best))
			"lot":
				for id in app.store.buildings:
					if not app.store.buildings[id].get("lot", {}).get("croft", []).is_empty():
						await app.inspect(Registry.ref(Registry.BUILDING, int(id)))
						app.rig.focus(app.world_view.building_center(int(id)))
						break
			"ground":
				var crofts: int = app.store.buildings.values().filter(func(b): return not b.get("lot", {}).get("croft", []).is_empty()).size()
				print("ground: scope=%s roads=%d drawn=%d buildings=%d with_croft=%d road_pieces=%d lot_pieces=%d syncs=%d" % [app.store.kind,
					app.store.roads.size(), app.world_view.ground.drawn_roads.size(), app.store.buildings.size(), crofts,
					app.world_view.ground.road_pieces, app.world_view.ground.lot_pieces, app.road_sync.refreshes])
			"roads": # AD-181: rua por delta, obras e quem anda pela rota agora
				var surfaces := {}
				for cell in app.store.roads:
					surfaces[app.store.roads[cell].surface] = surfaces.get(app.store.roads[cell].surface, 0) + 1
				var walking: int = app.world_view.npc_nodes.values().filter(func(a): return a.walk != null).size()
				print("roads: scope=%s live=%s cells=%d drawn=%d reserved=%d surfaces=%s works=%d walking=%d routes=%d syncs=%d" % [app.store.kind,
					app.store.roads_live, app.store.roads.size(), app.world_view.ground.drawn_roads.size(), app.world_view.ground.reserved.size(),
					surfaces, preload("res://Store/road_book.gd").work_cells(app.store.roads).size(), walking,
					app.store.npcs.values().filter(func(n): return not n.get("route", []).is_empty()).size(), app.road_sync.refreshes])
			"walker": # segue alguém andando pela rota (rota no chão)
				for id in app.world_view.npc_nodes:
					if app.world_view.npc_nodes[id].walk != null and not app.world_view.npc_nodes[id].walk.done:
						await app.select_npc(int(id))
						app._highlight(int(id))
						app.follow_selected()
						break
			"heat":
				app.reading.toggle_heat()
			"log":
				var moving := 0
				var actions := {}
				for id in app.store.npcs:
					var a: int = app.store.npcs[id].action
					actions[a] = actions.get(a, 0) + 1
				print("log: scope=", app.store.kind, " cursor=", app.stream.cursor, " state=", app.stream.state, " npcs=", app.store.npcs.size(), " actions=", actions)
				# AD-193: obras que o cliente recebeu e quantas desenhou como canteiro (e quem está no lote com a obra como alvo)
				var sites := 0
				for node in app.world_view.buildings.nodes.values():
					if node.has_meta("work_id"):
						sites += 1
				var on_site := 0
				for id in app.store.npcs:
					if String(app.store.npcs[id].get("target", {}).get("kind", "")) == "work":
						on_site += 1
				var works_text: Array = []
				for work in app.store.works.values():
					var mat := ""
					for entry in work.materials: # AD-194: material no lote / posto / total
						mat += ":mat=%d/%d/%d" % [int(entry.get("on_site", -1)), int(entry.placed), int(entry.needed)]
					works_text.append("%d:%s:%d%%:%s:turma=%d+%d:lote=%d%s" % [int(work.id), String(work.kind), int(round(float(work.progress) * 100.0)),
						String(work.halt) if String(work.halt) != "" else "anda", work.crew.size(), work.helpers.size(), work.on_site.size(), mat])
				var arrivals := 0
				for event in app.registry.events.values():
					if String(event.get("label", "")).begins_with("Chegou ao lote"):
						arrivals += 1
				var carrying := 0 # AD-195: quem leva material nas mãos (observável `carried`)
				for id in app.store.npcs:
					for load in app.store.npcs[id].get("obs", {}).get("carried", []):
						if int(load.get("resource_id", load.get("resourceId", -1))) == 3:
							carrying += 1
				print("log-works: carregando=", carrying, " cargas_no_registro=", arrivals, " works=", app.store.works.size(), " sites=", sites, " npcs_on_site=", on_site, " ", " ".join(works_text))
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
			"stress", "stress_look":
				if app.banner != null:
					app.banner.text = "MEDIDA DE DESEMPENHO: pessoas de teste. Isto não é o mundo do jogo."
					app.banner.visible = true
				# Carga sintética: N NPCs andando pelo mapa para medir quadros por segundo. `stress_look` dá a cada um
				# genes de aparência (como um mundo com o bloco Appearance); `stress` mantém o sorteio local de sempre.
				var count := int(arg)
				var rng := RandomNumberGenerator.new()
				rng.seed = 7
				for i in count:
					var cell := Vector2i(rng.randi_range(0, 29), rng.randi_range(0, 29))
					var look := {}
					if name == "stress_look":
						look = {"skin_tone": rng.randi_range(0, 100), "hair_tone": rng.randi_range(0, 100),
							"hair_curl": rng.randi_range(0, 100), "build": rng.randi_range(0, 100)}
					app.store.npcs[100000 + i] = {"id": 100000 + i, "cell": cell, "action": rng.randi_range(0, 7), "city": "", "look": look}
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
			"bench", "benchbig":
				# quem olha a janela durante a medida precisa saber que isto não é o mundo: é carga sintética local
				if app.banner != null:
					app.banner.text = "MEDIDA DE DESEMPENHO: pessoas e prédios de teste, colocados em grade. Isto não é o mundo do jogo."
					app.banner.visible = true
				await _bench(app, int(arg) if arg != "" else 500, name == "benchbig")
			"playstreet": # só captura (AD-197o): encarna um morador que está na rua (o mesmo do passo "near")
				var id := _street_npc(app)
				if id >= 0:
					await app.select_npc(id)
					await app.incarnate(id)
			"playopen": # só captura (AD-197s2): encarna o morador mais longe de qualquer prédio (rua aberta de verdade)
				var open_id := _open_npc(app)
				if open_id >= 0:
					await app.select_npc(open_id)
					await app.incarnate(open_id)
			"playwall": # só captura (AD-197s4): encarna um morador da rua e o põe a 1 m da quina de um prédio, com a câmera virada para a quina atrás
				var wall_id := _street_npc(app)
				if wall_id >= 0:
					await app.select_npc(wall_id)
					await app.incarnate(wall_id)
					var wall_body: Node3D = app.world_view.npc_nodes.get(wall_id)
					if wall_body != null:
						var spot := _corner_spot(app, wall_body.position)
						if spot.size() == 2:
							wall_body.position = spot[0]
							app.rig.yaw = spot[1]
							app.rig.follow(wall_body)
			"playhome": # só captura (AD-197s): encarna um morador e o põe no meio do prédio mais próximo (só o desenho; o motor não muda)
				var home_id := _street_npc(app)
				if home_id >= 0:
					await app.select_npc(home_id)
					await app.incarnate(home_id)
					var body: Node3D = app.world_view.npc_nodes.get(home_id)
					if body != null:
						var inside := _nearest_box_center(app, body.position, arg == "big")
						if inside != Vector3.INF:
							body.position = inside
							app.rig.follow(body)
			"crowd": # só captura (AD-197r): câmera perto do ponto com mais gente a menos de 0,6 unidade umas das outras
				var best_id := -1
				var best_count := 0
				for id in app.world_view.npc_nodes:
					var at: Vector3 = app.world_view.npc_nodes[id].position
					var count := 0
					for other in app.world_view.npc_nodes.values():
						if Vector2(at.x, at.z).distance_to(Vector2(other.position.x, other.position.z)) < 0.6:
							count += 1
					if count > best_count:
						best_count = count
						best_id = id
				if best_id >= 0:
					print("crowd: ", best_count, " pessoas perto de ", app.world_view.npc_nodes[best_id].position)
					app.rig.focus(app.world_view.npc_nodes[best_id].position)
					app.rig.set_zoom(float(arg) if arg != "" else 4.0)
			"camdump": # só diagnóstico (AD-197s): onde estão corpo e câmera, e em que caixas de prédio cada um está
				print(camera_report(app))
			"stacks": # só medida (AD-197r): quantas pessoas ficam a menos de 0,25 unidade de outra e o maior bolo (com onde e o que fazem)
				print(stacks_report(app.world_view.npc_nodes))
			"cam": # só captura (AD-197o): troca o modo da câmera (`cam:first|third|iso`) sem trocar quem ela segue
				app.rig.set_mode({"first": app.rig.FIRST, "third": app.rig.THIRD}.get(arg, app.rig.ISO))
			"third", "first": # só medida (AD-197d): câmera do modo jogador (3ª/1ª pessoa) colada num morador, sem encarnar
				var ids: Array = app.world_view.npc_nodes.keys()
				ids.sort()
				if not ids.is_empty():
					app.rig.follow(app.world_view.npc_nodes[ids[0]])
				app.rig.set_mode(app.rig.THIRD if name == "third" else app.rig.FIRST)
			"unsub": # só medida (AD-188): para de receber deltas (separa o custo do cliente do da máquina dividida com o servidor)
				app.stream.close()
			"rain": # só medida/captura (AD-188): tempo sintético local (`rain:heavy|moderate|light`, `rain:snow`); o motor não muda
				var snow := arg == "snow"
				var fake := {"precipitation": "snow" if snow else "rain", "intensity": "heavy" if snow else (arg if arg != "" else "heavy"),
					"storm": not snow, "wind": 9.0, "ground": "snow" if snow else "mud", "snow_cm": 12.0 if snow else 0.0, "now_c": -3.0 if snow else 9.0,
					"feels_c": -7.0 if snow else 6.0, "sunrise": 7.0, "sunset": 17.0}
				app.store.climate = app.store.climate.duplicate()
				app.store.climate.weather = fake
				for cell in app.store.roads:
					app.store.roads[cell].ground = fake.ground if int(app.store.roads[cell].surface) < 2 else ("snow" if snow else "wet")
					app.store.roads[cell].snow_cm = fake.snow_cm
				app.store.climate_changed.emit()
				app.world_view._mark_ground()
			"near": # câmera bem perto de um morador na rua (LOD "perto": ferramenta, objeto e rosto)
				var id := _street_npc(app)
				if id >= 0:
					app.rig.focus(app.world_view.npc_nodes[id].position)
					app.rig.set_zoom(float(arg) if arg != "" else 5.0)
			"selectfirst": # ficha de um morador na rua (o mesmo do passo "near")
				var id := _street_npc(app)
				if id >= 0:
					await app.select_npc(id)
			"building": # ficha do primeiro prédio
				var ids: Array = app.store.buildings.keys()
				ids.sort()
				if not ids.is_empty():
					await app.inspect({"kind": "building", "id": int(ids[0])})
			"next":
				app.next_event()
			"watch":
				app.toggle_watch()
			"hold": # #222: `hold:W=2` segura a tecla (W, A, S, D, SHIFT, SPACE, E) por N s, como o teclado de verdade
				var key_name := arg.get_slice("=", 0)
				var code := OS.find_keycode_from_string(key_name)
				var press := InputEventKey.new()
				press.keycode = code
				press.physical_keycode = code
				press.pressed = true
				Input.parse_input_event(press)
				await app.get_tree().create_timer(float(arg.get_slice("=", 1)) if "=" in arg else 0.1).timeout
				var release: InputEventKey = press.duplicate()
				release.pressed = false
				Input.parse_input_event(release)
			"walkshot": # #238: `walkshot:W=2,caminho.png` anda 2 s e fotografa o cartão com o corpo em movimento (e imprime o rótulo do chão)
				var spec := arg.get_slice(",", 0)
				var walk_key := OS.find_keycode_from_string(spec.get_slice("=", 0))
				var walk_press := InputEventKey.new()
				walk_press.keycode = walk_key
				walk_press.physical_keycode = walk_key
				walk_press.pressed = true
				Input.parse_input_event(walk_press)
				await app.get_tree().create_timer(float(spec.get_slice("=", 1)) * 0.6).timeout
				print("ground: ", app.player_frame.action_label.text, " | ", app.player_frame._ground)
				await RenderingServer.frame_post_draw
				app.get_viewport().get_texture().get_image().save_png(arg.get_slice(",", 1))
				await app.get_tree().create_timer(float(spec.get_slice("=", 1)) * 0.4).timeout
				var walk_release: InputEventKey = walk_press.duplicate()
				walk_release.pressed = false
				Input.parse_input_event(walk_release)
			"walkraw": # #237: um passo cru ao motor (`walkraw:-707,-707`) e a resposta inteira
				var raw: Dictionary = await app.api.player_walk(app.player_hud.player_id, int(arg.get_slice(",", 0)), int(arg.get_slice(",", 1)), false)
				print("walkraw: status=", raw.get("status"), " body=", raw.get("body"))
			"resume": # #237: retoma o mundo (admin) para medir com o tempo correndo, como o dono joga
				var resumed: Dictionary = await app.api.admin_resume("dev")
				print("resume: ok=", resumed.get("ok"), " status=", resumed.get("status"))
			"walkwatch": # #237: `walkwatch:W=4` anda 4 s e, a cada 0,25 s, imprime o corpo, a dica na tela e o escopo (prova de oferta velha / troca de escopo)
				var wspec := arg.get_slice(",", 0)
				if not app.player_hud.warning.is_connected(_print_warning):
					app.player_hud.warning.connect(_print_warning)
				var wkey := OS.find_keycode_from_string(wspec.get_slice("=", 0))
				var wpress := InputEventKey.new()
				wpress.keycode = wkey
				wpress.physical_keycode = wkey
				wpress.pressed = true
				Input.parse_input_event(wpress)
				var left := float(wspec.get_slice("=", 1))
				while left > 0.0:
					await app.get_tree().create_timer(0.25).timeout
					left -= 0.25
					var hud2 = app.player_hud
					var shown: String = app.player_frame.prompt.text if app.player_frame != null and app.player_frame.prompt.visible else ""
					var live_water := ""
					var live: Dictionary = await app.api.player_reach(hud2.player_id) # #237: o que o motor oferece neste instante (verdade)
					if live.ok and live.body is Dictionary:
						for offer in live.body.get("offers", []):
							if String(offer.get("kind", "")) == "water":
								var gap := Vector2(maxf(maxf(float(offer.x) - hud2.fine.x, hud2.fine.x - float(offer.x) - 1.0), 0.0), maxf(maxf(float(offer.y) - hud2.fine.y, hud2.fine.y - float(offer.y) - 1.0), 0.0)).length()
								live_water = "agua@%d,%d folga=%.2f" % [offer.x, offer.y, gap]
					var node = app.world_view.npc_nodes.get(hud2.npc_id)
					print("walkwatch: t=", snappedf(float(wspec.get_slice("=", 1)) - left, 0.01), " escopo=", app.store.kind, " fine=", hud2.fine, " motor_fine=", hud2._last_server, " vel=", snappedf(hud2.speed_cells, 0.001), " corpo=", node.position if node != null and is_instance_valid(node) else "sem corpo",
						" alvo_cam=", app.rig.target, " cam=", app.rig.camera.global_position, " zoom=", snappedf(app.rig.zoom_size, 0.01), " segue=", app.rig.following != null and is_instance_valid(app.rig.following), " apoio=", app.body_scope.anchor != null and is_instance_valid(app.body_scope.anchor), " ator=", app.player_actor() != null, " prompt='", shown, "' motor_agua=", live_water)
					if arg.contains(",") and int(roundf(left * 4.0)) % 2 == 0: # um quadro a cada 0,5 s: `walkwatch:W=8,/caminho/prefixo`
						await RenderingServer.frame_post_draw
						app.get_viewport().get_texture().get_image().save_png("%s-%04d.png" % [arg.get_slice(",", 1), int(roundf((float(wspec.get_slice("=", 1)) - left) * 100.0))])
				var wrelease: InputEventKey = wpress.duplicate()
				wrelease.pressed = false
				Input.parse_input_event(wrelease)
			"gesture": # #224: `gesture:reach` toca um gesto no corpo do jogador (captura das animações)
				var own = app.world_view.npc_nodes.get(app.player_hud.npc_id)
				if own != null:
					own.play_gesture(arg, 1.6)
			"pitch": # #224: `pitch:-40` inclina o olhar do jogador (graus)
				app.rig.pitch = deg_to_rad(float(arg))
				app.rig._apply()
			"nochannel": # #222: depuração: comandos do jogador pela rota HTTP em vez do canal
				app.api.close_player_channel()
			"money": # #222: o que a ficha do motor diz agora (dinheiro), lida na hora
				var detail: Dictionary = await app.api.npc_detail(app.player_hud.npc_id)
				print("money: ok=", detail.ok, " status=", detail.status, " money=", detail.body.get("money") if detail.body is Dictionary else null, " erros=", detail.get("errors", []))
			"sizes": # #222: tamanhos da ficha (depuração de layout)
				var ins = app.inspector
				print("sizes: inspector ", ins.size, " min ", ins.get_combined_minimum_size(), " side ", app.side_panel.size)
				for child in ins.get_child(0).get_children():
					print("sizes:   ", child.get_class(), " ", child.size, " min ", child.get_combined_minimum_size(), " vis ", child.visible)
			"reach": # #237: a dica do E que o cliente mostra agora × o que o motor oferece agora (prova da água de longe)
				var hud = app.player_hud
				var live: Dictionary = await app.api.player_reach(hud.player_id)
				var kinds: Array = []
				if live.ok and live.body is Dictionary:
					for offer in live.body.get("offers", []):
						kinds.append("%s:%s@%s,%s" % [offer.get("kind"), ",".join(offer.get("verbs", [])), offer.get("x"), offer.get("y")])
				print("reach: fine=", hud.fine, " dica='", hud.reach_hint.text, "' prompt='", app.player_frame.prompt.text if app.player_frame != null else "", "' vis=", app.player_frame.prompt.visible if app.player_frame != null else false, " motor=", kinds)
			"pos": # #222: onde o corpo do jogador está (motor e desenho)
				var own = app.world_view.npc_nodes.get(app.player_hud.npc_id)
				print("pos: escopo=", app.store.kind, " segue=", app.rig.following != null and is_instance_valid(app.rig.following), " apoio=", app.body_scope.anchor != null and is_instance_valid(app.body_scope.anchor), " ator=", app.player_actor() != null, " ativo=", app.player_hud.is_active, " fine=", app.player_hud.fine, " speed=", app.player_hud.speed_cells,
					" corpo=", own.position if own != null else "sem corpo", " modo=", app.rig.mode, " braço=", app.rig.arm, " subida=", app.rig.lift,
					" ombro=", app.rig.shoulder, " dentro=", app.rig.opened_ids().keys(), " cartão=", app.player_frame.purse_label.text if app.player_frame != null else "", " leitura=", app.player_hud._body_pending, "/", snappedf(app.player_hud._body_clock, 0.1), " rpg=", app.player_hud.rpg, " npc=", app.player_hud.npc_id, " menu1=", app.player_frame.entries[0].text if app.player_frame != null and not app.player_frame.entries.is_empty() else "")
				var actor = app.player_actor()
				print("pos: ator=", actor.position if actor != null else "sem ator", " alvo=", app.rig.target, " camera=", app.rig.camera.global_position, " yaw=", snappedf(app.rig.view_yaw, 0.01), " dentro_da_casa=", app.stage.active, " predio_do_motor=", app.player_hud.building_id, " visivel=", actor.visible if actor != null else "-", " tela=", app.rig.camera.unproject_position(actor.position + Vector3(0, 0.7, 0)) if actor != null else "-", " atras=", app.rig.camera.is_position_behind(actor.position) if actor != null else "-", " rot=", app.rig.camera.global_rotation_degrees, " cams=", app.get_viewport().get_camera_3d() == app.rig.camera, " ocupantes=", app.stage.interior_view.occupant_nodes.keys(), " npcs_store=", app.store.interior.get("occupants", []).size(), " ator_pai=", actor.get_parent().name if actor != null else "-")
				if own != null:
					var eye: Vector3 = app.rig.target + Vector3(0, app.rig.eye_height() + app.rig.shoulder_height(), 0)
					for entry in app.camera_blockers():
						if (entry[1] as AABB).has_point(eye) or (entry[2] as AABB).has_point(eye):
							print("pos: olho na caixa ", entry[0], " ", entry[1], " paredes=", (entry[3] as Array).size())
			"wv": # #227: estado do mundo desenhado (prédios visíveis, modo de processo, nós de transparência)
				var shown := 0
				var faded := 0
				for bid in app.world_view.buildings.nodes:
					var bnode: Node3D = app.world_view.buildings.nodes[bid]
					if bnode.is_visible_in_tree():
						shown += 1
					if app.world_view.buildings.faded.has(bid):
						faded += 1
				print("wv: prédios=", app.world_view.buildings.nodes.size(), " visíveis=", shown, " translúcidos=", faded, " mundo_visivel=", app.world_view.visible, " processo=", app.world_view.process_mode, " opened=", app.rig.opened_ids().keys(), " escopo=", app.store.kind, " casa=", app.stage.active, " cena=", app.store.cities.size())
			"quit":
				await app.release_observation() # como fechar a janela: retira a fonte e encerra a conversa
				app.get_tree().quit()



## Medida recalibrada (ADR-0047): sem vsync, na cidade aberta (prédios e NPCs reais do servidor), com a ficha do
## primeiro morador aberta e a multidão completada até `total` NPCs andando dentro da cidade. Imprime FPS médio e o
## tempo de quadro p95 de 6 s, depois de 1 s de aquecimento. Uso: `--steps "wait:3;city;wait:3;bench:500;quit"`.
## Morador na rua (fora da planta dos prédios), de preferência trabalhando ou conversando; -1 se não há.
static func stacks_report(nodes: Dictionary, limit: float = 0.25) -> String:
	var cells := {}
	for id in nodes:
		var at: Vector3 = nodes[id].position
		cells.get_or_add(Vector2i(floori(at.x / limit), floori(at.z / limit)), []).append(id)
	var close := {}
	var best: Array = []
	for id in nodes:
		var at: Vector3 = nodes[id].position
		var group: Array = []
		for dx in [-1, 0, 1]:
			for dz in [-1, 0, 1]:
				for other in cells.get(Vector2i(floori(at.x / limit) + dx, floori(at.z / limit) + dz), []):
					if other != id and Vector2(at.x, at.z).distance_to(Vector2(nodes[other].position.x, nodes[other].position.z)) < limit:
						group.append(other)
		if not group.is_empty():
			close[id] = true
			if group.size() + 1 > best.size():
				best = group + [id]
	var detail := ""
	if not best.is_empty():
		var first = nodes[best[0]]
		detail = " maior=%d em %s ações=%s andando=%s" % [best.size(), first.position, best.map(func(i): return nodes[i].action),
			best.map(func(i): return nodes[i].walk != null)]
	return "stacks: npcs=%d sobrepostos=%d%s" % [nodes.size(), close.size(), detail]


## [posição a 1 m da quina mais próxima, giro para a quina ficar atrás da câmera] ou [].
static func _corner_spot(app: Node, from: Vector3) -> Array:
	var best: Array = []
	var best_distance := INF
	for node in app.world_view.buildings.nodes.values():
		var box: AABB = node.get_meta("box", AABB())
		if box.size == Vector3.ZERO:
			continue
		var center := Vector3(box.get_center().x, 0.0, box.get_center().z)
		for corner: Vector3 in [Vector3(box.position.x, 0, box.position.z), Vector3(box.end.x, 0, box.position.z), Vector3(box.position.x, 0, box.end.z), Vector3(box.end.x, 0, box.end.z)]:
			var outward: Vector3 = (corner - center).normalized()
			if from.distance_to(corner) < best_distance:
				best_distance = from.distance_to(corner)
				best = [corner + outward * Grid.units(1.5), atan2(-outward.x, -outward.z)]
	return best


static func _open_npc(app: Node) -> int:
	var boxes: Array = []
	for node in app.world_view.buildings.nodes.values():
		var box: AABB = node.get_meta("box", AABB())
		if box.size != Vector3.ZERO:
			boxes.append(box.grow(0.0))
	var best := -1
	var best_gap := -1.0
	var ids: Array = app.world_view.npc_nodes.keys()
	ids.sort()
	for id in ids:
		var at: Vector3 = app.world_view.npc_nodes[id].position
		var gap := INF
		for box in boxes:
			var b: AABB = box
			var dx := maxf(maxf(b.position.x - at.x, at.x - b.end.x), 0.0)
			var dz := maxf(maxf(b.position.z - at.z, at.z - b.end.z), 0.0)
			gap = minf(gap, sqrt(dx * dx + dz * dz))
		if gap > best_gap:
			best_gap = gap
			best = int(id)
	return best


static func _nearest_box_center(app: Node, from: Vector3, largest: bool = false) -> Vector3:
	var best := Vector3.INF
	var best_distance := INF
	for node in app.world_view.buildings.nodes.values():
		var box: AABB = node.get_meta("box", AABB())
		if box.size == Vector3.ZERO:
			continue
		var center := Vector3(box.get_center().x, 0.0, box.get_center().z)
		var score := from.distance_to(center) if not largest else -box.size.x * box.size.z
		if score < best_distance:
			best_distance = score
			best = center
	return best


static func camera_report(app: Node) -> String:
	var rig = app.rig
	var cam: Vector3 = rig.camera.global_position if rig.camera.is_inside_tree() else Vector3.INF
	var body: Vector3 = rig.following.position if rig.following != null and is_instance_valid(rig.following) else Vector3.INF
	var around: Array = []
	for entry in app.camera_blockers():
		var box: AABB = entry[1]
		if box.has_point(body + Vector3(0, 0.5, 0)):
			around.append("caixa=%s paredes=%d na_parede=%s" % [box, (entry[3] as Array).size(), (entry[3] as Array).any(func(w): return (w as AABB).has_point(body + Vector3(0, 1.3, 0)))])
		around.append("%s:%s%s%s" % [entry[0], "C" if box.has_point(cam) else "-", "B" if box.has_point(body + Vector3(0, 0.5, 0)) else "-", "F" if app.world_view.buildings.faded.has(entry[0]) else "-"])
	var roofs: Array = []
	for id in rig.seen_ids():
		var node = app.world_view.buildings.nodes.get(id)
		if node != null:
			for child in node.find_children("Roof*", "GeometryInstance3D", true, false):
				roofs.append(snappedf((child as GeometryInstance3D).transparency, 0.01))
	around.append("vistos=%s abertos=%s faded=%s kind=%s stage=%s teto=%s" % [rig.seen_ids().keys(), app.world_view.buildings.opened.keys(), app.world_view.buildings.faded.keys(), app.store.kind, app.stage.active, roofs])
	var own = app.world_view.npc_nodes.get(app.player_hud.npc_id)
	var near_ids: Array = []
	if own != null:
		near_ids = app.world_view.npcs.cell_members.get(Grid.to_cell(own.position), [])
	around.append("jogador=%s ativo=%s escondidos=%s mesma_celula=%s marcado=%s" % [app.player_hud.npc_id, app.player_hud.is_active, app._eye_hidden.keys(), near_ids, app._marked])
	return "camdump: mode=%s following=%s body=%s cam=%s arm=%s lift=%s shoulder=%s caixas=%s" % [rig.mode, rig.following != null, body, cam, snappedf(rig.arm, 0.01), snappedf(rig.lift, 0.01), snappedf(rig.shoulder, 0.01), around]


static func _street_npc(app: Node) -> int:
	var covered := {}
	for building in app.store.buildings.values():
		for entry in building.footprint:
			covered[building.cell + entry.cell] = true
	var ids: Array = app.store.npcs.keys()
	ids.sort()
	var best := -1
	for id in ids:
		if not app.world_view.npc_nodes.has(id) or covered.has(app.store.npcs[id].cell):
			continue
		if best < 0 or int(app.store.npcs[id].action) in [2, 3]:
			best = int(id)
			if int(app.store.npcs[id].action) in [2, 3]:
				break
	return best


## Cidade maior para o bench (`benchbig`): 40 prédios 2x2 a mais em grade dentro dos limites; no cliente com a projeção do
## observável (AD-171) também estado de prédio, 30 lavouras e 20 animais. Só carga sintética local, nada vai ao servidor.
static func _enlarge_city(app: Node, bounds: Rect2i, rng: RandomNumberGenerator) -> void:
	var observable: bool = "crops" in app.store
	for i in 40:
		var id := 900000 + i
		var cell := bounds.position + Vector2i((i % 8) * 3, (i / 8) * 3)
		var footprint: Array = []
		for c in [Vector2i(0, 0), Vector2i(1, 0), Vector2i(0, 1), Vector2i(1, 1)]:
			footprint.append({"cell": c, "material": 2 if c == Vector2i.ZERO else i % 2})
		app.store.buildings[id] = {"id": id, "type": i % 6, "cell": cell, "orientation": 0, "floors": 1 + i % 3,
			"footprint": footprint, "value": 100, "lodging": null}
		if observable:
			app.store.building_states[id] = {"building_id": id, "type": i % 6, "type_name": "", "function": ["dwelling", "workplace", "lodging"][i % 3],
				"workplace_id": id, "location_type_id": i % 5, "location_type_name": ["Fazenda", "Ferraria", "Padaria", "Mercado", "Taverna"][i % 5],
				"owner_id": -1, "households": [], "residents": [], "workers": [], "stock": [], "pantry": 0, "closed": i % 7 == 0}
	if observable:
		var stages := ["unsown", "planted", "growing", "mature", "harvested", "fallow"]
		for i in 30:
			app.store.crops[800000 + i] = {"id": 800000 + i, "cell": bounds.position + Vector2i(rng.randi_range(0, bounds.size.x - 1), rng.randi_range(0, bounds.size.y - 1)),
				"resource_id": i % 4, "resource_name": ["Trigo", "Milho", "Feijão", "Batata"][i % 4], "status": "", "stage": stages[i % 6],
				"fraction": rng.randf(), "thirsty": i % 5 == 0, "sown": 1.0, "arable": 2.0, "standing": 0, "farm_id": -1, "owner_id": -1, "expected": 10}
		for i in 20:
			app.store.animals[700000 + i] = {"id": 700000 + i, "species": ["rabbit", "wolf", "sheep", "chicken"][i % 4],
				"cell": bounds.position + Vector2i(rng.randi_range(0, bounds.size.x - 1), rng.randi_range(0, bounds.size.y - 1)),
				"energy": 50, "disease": false, "predator_of": ""}
		app.store.crops_changed.emit()
		app.store.fauna_changed.emit()
	app.world_view.buildings.rebuild(app.store)


## Corpo sintético pelo estado (cliente AD-171): ofício, idade, gravidez, carga e luto variados, como o escopo manda.
static func _synthetic_observable(i: int) -> Dictionary:
	var ages := [1, 8, 15, 30, 45, 70]
	var age: int = ages[i % ages.size()]
	return {"name": "Sintético %d" % i, "sex": "Female" if i % 2 == 0 else "Male", "age": age,
		"stage": "Child" if age < 13 else ("Elder" if age >= 60 else "Adult"), "profession_id": i % 6,
		"profession_name": ["Lavrador", "Ferreiro", "Padeiro", "Mercador", "Lenhador", "Guarda"][i % 6],
		"height_cm": [75, 125, 160, 170, 168, 162][i % ages.size()], "growth": 1.0, "pregnancy": 0.6 if i % 17 == 0 else 0.0,
		"babies": 1, "ill": i % 13 == 0, "symptomatic": i % 13 == 0, "bedridden": false, "disease_id": -1, "emotions": {},
		"dominant": ["", "joy", "grief", "fear", "anger"][i % 5], "mourning": i % 11 == 0,
		"carried": [{"resource_id": 2, "quantity": 4, "name": "Trigo"}] if i % 4 == 0 else [], "held": {}}


static func _bench(app: Node, total: int, big: bool = false) -> void:
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	var real: Array = app.store.npcs.keys()
	real.sort()
	if not real.is_empty():
		await app.select_npc(int(real[0]))
	var bounds: Rect2i = app.store.city.get("bounds", Rect2i(0, 0, 30, 30)) if app.store.kind == "City" else Rect2i(0, 0, 30, 30)
	var rng := RandomNumberGenerator.new()
	rng.seed = 11
	if big and app.store.kind == "City":
		_enlarge_city(app, bounds, rng)
	var observable: bool = "crops" in app.store
	var synthetic: Array = []
	for i in maxi(total - real.size(), 0):
		var id := 200000 + i
		var cell := Vector2i(bounds.position.x + rng.randi_range(0, bounds.size.x - 1), bounds.position.y + rng.randi_range(0, bounds.size.y - 1))
		app.store.npcs[id] = {"id": id, "cell": cell, "action": rng.randi_range(0, 9), "city": "", "look": {}}
		if observable:
			app.store.npcs[id]["obs"] = _synthetic_observable(i)
		app.store.npc_changed.emit(id, cell, cell, app.store.npcs[id].action)
		if observable:
			app.store.npc_observed.emit(id)
		synthetic.append(id)
	var frame_ms: Array = []
	var clock := 0.0
	var next_step := 0.5
	var costed := false
	var last := Time.get_ticks_usec()
	while clock < 7.0:
		await app.get_tree().process_frame
		var now := Time.get_ticks_usec()
		var dt := (now - last) / 1000.0
		last = now
		clock += dt / 1000.0
		if clock >= 1.0:
			frame_ms.append(dt)
			if not costed:
				costed = true
				FrameCost.reset()
			FrameCost.usec["~process"] = int(FrameCost.usec.get("~process", 0)) + int(Performance.get_monitor(Performance.TIME_PROCESS) * 1000000.0)
			FrameCost.count["~process"] = int(FrameCost.count.get("~process", 0)) + 1
			FrameCost.usec["~draws"] = int(FrameCost.usec.get("~draws", 0)) + int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)) * 1000
			FrameCost.count["~draws"] = int(FrameCost.count.get("~draws", 0)) + 1
		if clock >= next_step:
			next_step += 0.5
			for id in synthetic:
				if not app.store.npcs.has(id):
					continue
				var from_cell: Vector2i = app.store.npcs[id].cell
				var to_cell := Vector2i(clampi(from_cell.x + rng.randi_range(-1, 1), bounds.position.x, bounds.end.x - 1),
					clampi(from_cell.y + rng.randi_range(-1, 1), bounds.position.y, bounds.end.y - 1))
				app.store.npcs[id].cell = to_cell
				app.store.npc_changed.emit(id, from_cell, to_cell, app.store.npcs[id].action)
	var sorted := frame_ms.duplicate()
	sorted.sort()
	var total_ms := 0.0
	for value in frame_ms:
		total_ms += value
	var p95: float = sorted[mini(int(sorted.size() * 0.95), sorted.size() - 1)] if not sorted.is_empty() else 0.0
	print("%s: scope=%s npcs=%d buildings=%d fps_avg=%.1f p95_ms=%.2f frames=%d vsync=%d" % ["benchbig" if big else "bench", app.store.kind, app.store.npcs.size(),
		app.store.buildings.size(), frame_ms.size() / (total_ms / 1000.0) if total_ms > 0 else 0.0, p95, frame_ms.size(),
		DisplayServer.window_get_vsync_mode()])
	print("bench-cost: ", FrameCost.report(), " walkers=", app.world_view.npc_nodes.values().filter(func(a): return a.walk != null).size(),
		" nodes=", Performance.get_monitor(Performance.OBJECT_NODE_COUNT))


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
			app.narrative._render(ok.call([{"reportId": 1, "distortedMagnitude": 0.83, "distortedAtMs": 300 * 3600000, "seed": "A praga foi castigo dos vizinhos do norte", "distanceFromFact": 0.62},
				{"reportId": 2, "distortedMagnitude": 0.41, "distortedAtMs": 900 * 3600000, "seed": "O rei morreu de fome", "distanceFromFact": 0.15}]))
		"relatos":
			app.narrative.mode = "reports"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative._title.text = "Relatos que circulam"
			app.narrative._render(ok.call([{"reportId": 1, "originFactId": 5, "medium": "Song", "confidence": 0.82}, {"reportId": 2, "originFactId": 8, "medium": "Book", "confidence": 0.25}]))
		"tecnologias":
			app.technology.is_open = true; app.technology.visible = true
			app.technology._render(ok.call([{"category": "mobilidade", "vector": [2, 4, 6], "key": "2.4.6", "discoveredAtMs": 500 * 3600000, "isVariation": false, "variationOfKey": null, "adoptedByCities": 1},
				{"category": "mobilidade", "vector": [3, 4, 6], "key": "3.4.6", "discoveredAtMs": 800 * 3600000, "isVariation": true, "variationOfKey": "2.4.6", "adoptedByCities": 3}]))
		"tesouro":
			app.economy.mode = "treasury"; app.economy.is_open = true; app.economy.visible = true; app.economy._title.text = "Tesouro de Alfa"
			app.economy._render(ok.call({"cityId": "x", "balance": 1902, "fromDay": 0, "toDay": 359, "months": [{"month": 0, "revenue": 46, "spending": 0}, {"month": 1, "revenue": 52, "spending": 30}, {"month": 2, "revenue": 40, "spending": 80}]}))
		"vendas":
			app.economy.mode = "trades"; app.economy.is_open = true; app.economy.visible = true; app.economy._title.text = "Vendas de local 0"
			app.economy._render(ok.call({"workplaceId": 0, "fromDay": 0, "toDay": 29, "days": [{"day": 3, "resourceId": 1, "quantity": 30, "value": 30, "purchases": 2}, {"day": 4, "resourceId": 2, "quantity": 20, "value": 20, "purchases": 1}]}))


## AD-197t: preenche o criador (`campo=valor` separados por vírgula): name, sex (0 sortear, 1 mulher, 2 homem), age, profession (índice),
## role (newcomer, child, sibling, spouse, relative), family (índice da lista), origin (índice), money, health, height, outfit (índice),
## look.<gene> e trait.<traço>. Espera a prévia do motor ao fim.
static func _creator_set(app: Node, text: String) -> void:
	var designer = app.creator.designer
	for pair in text.split(",", false):
		var key: String = pair.get_slice("=", 0)
		var value: String = pair.get_slice("=", 1)
		match key:
			"name":
				designer.name_edit.text = value
			"sex":
				designer.sex_select.select(int(value))
			"profession":
				designer.profession_select.select(int(value))
			"role":
				for i in designer.ROLES.size():
					if designer.ROLES[i][0] == value:
						designer.role_select.select(i)
				designer._refill_families()
			"family":
				designer.family_select.select(int(value))
				designer._refill_vias()
			"via":
				designer.via_select.select(int(value))
			"origin":
				designer.origin_select.select(int(value))
			"outfit":
				designer.outfit_select.select(int(value))
			"age", "money", "health":
				designer.set_field(key, float(value))
			"height":
				designer.set_field("heightM", float(value))
			_:
				designer.set_field(key, float(value))
	await designer.refresh()
