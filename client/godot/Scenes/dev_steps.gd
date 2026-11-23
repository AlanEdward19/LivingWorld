extends RefCounted
## Roteiro de desenvolvimento para capturas e demonstrações automáticas:
##   godot --path client/godot -- --steps "wait:2;shot:/tmp/a.png;city;wait:2;shot:/tmp/b.png;enter:0;quit"
## Passos: wait:s · shot:caminho · city (entra na 1ª cidade) · world · enter:ID · exit · floor:N · zoom:fator ·
##         yaw:graus · list · select:ID · brain:ID · whatif:fome · talk:ID · say:texto · timesay:texto (mede quanto a UI espera) · bio:ID · chronicle · reports · player:ID · act:Nome · materialize (aperta Ver em detalhe) · demo:janela (mostra uma janela com dados de exemplo) · size:LxA · treasury · trades:ID · tech:CULTURA · admin:motivo (pausa com o console) · adminsearch:texto · follow · near:zoom · selectfirst · building · next · watch · benchbig:N · xray · plan · street · lot · ground (AD-177) · roads · walker · heat (AD-181) · pause (menu de pausa) · tips:N (dica N) · help · quit. Não é usado em produção (sem --steps nada roda).


const Registry := preload("res://Store/entity_registry.gd")
const Grid := preload("res://Scenes/grid.gd")
const FrameCost := preload("res://Contract/frame_cost.gd")


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
			"xray": # AD-177: raio-x, planta, ficha da rua mais usada e do lote com quintal, contagem do chão
				app.reading.toggle_xray()
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
					works_text.append("%d:%s:%d%%:%s:turma=%d+%d:lote=%d" % [int(work.id), String(work.kind), int(round(float(work.progress) * 100.0)),
						String(work.halt) if String(work.halt) != "" else "anda", work.crew.size(), work.helpers.size(), work.on_site.size()])
				print("log-works: works=", app.store.works.size(), " sites=", sites, " npcs_on_site=", on_site, " ", " ".join(works_text))
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
			"quit":
				await app.release_observation() # como fechar a janela: retira a fonte e encerra a conversa
				app.get_tree().quit()



## Medida recalibrada (ADR-0047): sem vsync, na cidade aberta (prédios e NPCs reais do servidor), com a ficha do
## primeiro morador aberta e a multidão completada até `total` NPCs andando dentro da cidade. Imprime FPS médio e o
## tempo de quadro p95 de 6 s, depois de 1 s de aquecimento. Uso: `--steps "wait:3;city;wait:3;bench:500;quit"`.
## Morador na rua (fora da planta dos prédios), de preferência trabalhando ou conversando; -1 se não há.
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
