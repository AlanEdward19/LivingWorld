extends "res://tests/test_base.gd"

const DayCycle := preload("res://Scenes/day_cycle.gd")
const Minimap := preload("res://Scenes/minimap.gd")
const Toasts := preload("res://Scenes/toasts.gd")
const Store := preload("res://Store/world_store.gd")
const App := preload("res://Scenes/app.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")


func _cycle() -> Array:
	var sun := DirectionalLight3D.new()
	var environment := Environment.new()
	var window := StandardMaterial3D.new()
	var cycle := DayCycle.new()
	cycle.bind(sun, environment, window)
	return [cycle, sun, window, environment]


func test_day_cycle_follows_server_hour() -> void:
	var setup := _cycle()
	var cycle = setup[0]
	var sun: DirectionalLight3D = setup[1]
	var window: StandardMaterial3D = setup[2]

	var environment: Environment = setup[3]
	cycle.sync(12.0, 1.0, true)
	check(cycle.daylight() > 0.95, "meio-dia é claro")
	var noon_energy := sun.light_energy
	cycle.sync(7.0, 1.0, true)
	check(sun.light_energy < noon_energy - 0.2, "o sol da manhã é mais fraco que o do meio-dia: %s vs %s" % [sun.light_energy, noon_energy])
	check(sun.light_color.g < 0.97, "o sol baixo é mais quente")
	cycle.sync(12.0, 1.0, true)
	check(environment.background_color.is_equal_approx(Color("a9c7d8")), "céu de dia")
	var noon_rotation := sun.rotation
	eq(window.emission_energy_multiplier, 0.0, "janelas apagadas de dia")
	eq(cycle.phase_name(), "Dia")

	cycle.sync(23.0, 1.0, true)
	check(cycle.daylight() < 0.05, "meia-noite é escura")
	check(window.emission_energy_multiplier > 1.0, "janelas acesas de noite")
	eq(cycle.phase_name(), "Noite")
	eq(cycle.clock_text(), "23:00")
	check(sun.light_energy < 0.6, "luar fraco à noite: %s" % sun.light_energy)
	check(sun.light_color.b > sun.light_color.r, "luz azulada à noite")
	check(environment.background_color.v < 0.3, "céu escuro à noite")
	check(sun.rotation != noon_rotation, "o sol mudou de posição")

	cycle.sync(6.0, 1.0, true)
	eq(cycle.phase_name(), "Amanhecer")
	cycle.sync(18.0, 1.0, true)
	eq(cycle.phase_name(), "Entardecer")
	cycle.sync(19.25, 1.0, true)
	eq(cycle.phase_name(), "Noite", "a noite começa junto com o luar (19 h)")
	for node in setup:
		if node is Node:
			node.free()


func test_day_cycle_runs_only_while_server_runs() -> void:
	var setup := _cycle()
	var cycle = setup[0]
	cycle.sync(10.0, 4.0, true)
	cycle.advance(1.0)
	eq(cycle.hour, 10.0, "pausado: o céu não anda")

	cycle.sync(10.0, 4.0, false)
	cycle.advance(1.0)
	eq(cycle.hour, 14.0, "rodando a 4 ticks/s: +4 h em 1 s")
	cycle.sync(23.0, 4.0, false)
	cycle.advance(1.0)
	eq(cycle.hour, 3.0, "vira o dia")
	setup[1].free()
	cycle.free()


func test_minimap_maps_cells_and_requests_pan() -> void:
	var store := Store.new()
	store.map_size = Vector2i(30, 30)
	for y in 30:
		for x in 30:
			store.terrain[Vector2i(x, y)] = 1
	var map := Minimap.new()
	map.bind(store)
	map.rebuild()

	var point := Vector3(12.0, 0, 21.0)
	var back: Vector3 = map.to_world(map.to_minimap(point))
	check(back.distance_to(point) < 0.01, "ida e volta célula <-> minimapa: %s" % back)

	var requested: Array = []
	map.pan_requested.connect(func(p): requested.append(p))
	var click := InputEventMouseButton.new()
	click.pressed = true
	click.button_index = MOUSE_BUTTON_LEFT
	click.position = map.to_minimap(Vector3(5, 0, 7))
	map._gui_input(click)
	eq(requested.size(), 1, "clicar pede a navegação")
	check(requested[0].distance_to(Vector3(5, 0, 7)) < 0.01, "para a célula clicada")
	map.free()


func test_toasts_keep_five_and_full_history() -> void:
	var toasts := Toasts.new()
	for i in 7:
		toasts.push("evento %d" % i)

	eq(toasts.visible_texts().size(), 5, "no máximo 5 avisos na tela")
	eq(toasts.visible_texts()[0], "evento 2", "os mais antigos saem")
	eq(toasts.history.size(), 7, "o histórico guarda todos")
	toasts.free()


func _app_with(transport) -> Node:
	var app := App.new()
	tree.root.add_child(app)
	await app.start(transport, false)
	return app


func _online_transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": [{"actionType": 2, "label": "Trabalhando"}]})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 30, "year": 0}})
	transport.on("GET /npcs?", {"status": 200, "body": {"items": [{"id": {"value": 7}, "name": "Ana"}], "total": 1}})
	var world := Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [{"id": {"value": 7}, "location": {"x": 1, "y": 1}}],
		"activeEvents": [], "portals": [], "layers": {"Terrain": {"isModeled": false, "payload": null}},
		"livingState": {"npcs": [{"id": {"value": 7}, "location": {"x": 1, "y": 1}, "currentAction": 2}]}}, "World")
	world.scope.kind = 0
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": world})
	return transport


func test_loading_overlay_until_first_snapshot_and_names_load() -> void:
	var transport := _online_transport()
	var app = await _app_with(transport)

	eq(app.loading.visible, false, "o carregamento some depois do primeiro snapshot")
	await app._load_names()
	eq(app.store.names.get(7), "Ana", "nomes vêm de GET /npcs")
	eq(app.world_view.npc_nodes[7].display_name, "Ana", "o NPC na cena recebe o nome")
	app.queue_free()


func test_hover_text_comes_from_store_and_server_labels() -> void:
	var app = await _app_with(_online_transport())
	await app._load_names()
	app.store.buildings[4] = {"id": 4, "type": -1, "cell": Vector2i(0, 0), "orientation": 0, "floors": 2, "footprint": []}
	app.store.cities["c"] = {"id": "c", "name": "Alfa", "location": Vector2i(0, 0), "population": 12, "bounds": Rect2i(0, 0, 2, 2)}

	eq(app.describe_hit({"npc_id": 7}), "Ana\nTrabalhando", "nome + rótulo do servidor")
	check("Edifício 4" in app.describe_hit({"building_id": 4}) and "2 andares" in app.describe_hit({"building_id": 4}))
	check("Alfa" in app.describe_hit({"city_id": "c"}) and "12 hab." in app.describe_hit({"city_id": "c"}))
	eq(app.describe_hit({}), "", "nada sob o cursor, nada de dica")
	app.queue_free()


func test_delta_events_become_toasts_with_server_text() -> void:
	var app = await _app_with(_online_transport())

	app._on_delta(Fx.delta(0, 1, {"events": [{"tick": 5, "kind": 3, "label": "Nasceu uma criança", "location": null}]}))

	eq(app.toasts.visible_texts(), ["Nasceu uma criança"], "o texto do aviso vem do servidor")
	app.queue_free()


func test_actor_name_label_shows_on_hover_and_selection() -> void:
	var actor := NpcActor.new()
	actor.setup(3, Vector2i(0, 0), 2, ActionLabels.new())
	actor.set_display_name("Ana")
	eq(actor.label_visible(), false, "sem hover nem seleção o nome fica escondido")

	actor.set_hovered(true)
	eq(actor.label_visible(), true, "sob o cursor mostra o nome")
	check("Ana" in actor.label_text_shown(), "o texto traz o nome: " + actor.label_text_shown())
	actor.set_hovered(false)
	eq(actor.label_visible(), false)

	actor.set_selected(true)
	eq(actor.label_visible(), true, "selecionado mostra o nome")
	actor.set_selected(false)
	eq(actor.label_visible(), false)
	actor.free()


func test_loading_visible_until_first_snapshot() -> void:
	var transport := FakeTransport.new()
	var attempts: Array = []
	transport.on("GET /visual/subscribe", func(_p):
		attempts.append(1)
		return {"status": 0, "body": null} if attempts.size() < 2 else {"status": 200, "body": _online_transport().routes["GET /visual/subscribe?scope=World"].body})
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "tick": 0, "year": 0}})
	var app := App.new()
	tree.root.add_child(app)
	var seen: Array = []
	await app.start(transport, false, func(_s): seen.append(app.loading.visible))

	eq(seen, [true], "a tela de carregamento aparece enquanto não há snapshot")
	eq(app.loading.visible, false, "e some quando o primeiro chega")
	app.queue_free()


func test_status_drives_sky_and_minimap_drives_camera() -> void:
	var app = await _app_with(_online_transport())

	eq(app.day_cycle.hour, 6.0, "a hora do céu vem do tick do status (30 -> 06:00)")
	check("06:00" in app.time_label.text and "Amanhecer" in app.time_label.text, "a barra mostra a hora: " + app.time_label.text)

	app.minimap.pan_requested.emit(Vector3(12.0, 0, 9.0))
	for i in 120:
		app.rig._process(0.05)
	check(app.rig.target.distance_to(Vector3(12.0, 0, 9.0)) < 0.1, "o minimapa leva a câmera: %s" % app.rig.target)
	app.queue_free()


func test_loading_visible_during_normal_startup() -> void:
	var transport := _online_transport()
	var snapshot: Dictionary = transport.routes["GET /visual/subscribe?scope=World"].body
	var app := App.new()
	var seen: Array = []
	transport.on("GET /visual/subscribe?scope=World", func(_p):
		seen.append(app.loading.visible) # o servidor está de pé, mas o snapshot ainda não chegou à tela
		return {"status": 200, "body": snapshot})
	tree.root.add_child(app)

	await app.start(transport, false)

	eq(seen, [true], "durante o carregamento normal a tela de espera está visível")
	eq(app.loading.visible, false, "some quando o snapshot chega")
	app.queue_free()


func test_ambience_levels_follow_daylight_and_mute() -> void:
	var Ambience := preload("res://Scenes/ambience.gd")
	var noon: Dictionary = Ambience.levels(1.0)
	var midnight: Dictionary = Ambience.levels(0.0)
	check(noon.birds > 0.9 and noon.crickets == 0.0, "de dia: pássaros, sem grilos")
	check(midnight.crickets > 0.9 and midnight.birds == 0.0, "de noite: grilos, sem pássaros")
	check(midnight.wind > noon.wind, "o vento é mais audível à noite")

	var ambience := Ambience.new()
	tree.root.add_child(ambience) # na árvore o player de áudio existe
	var player: AudioStreamPlayer = ambience._player
	var audible_db := player.volume_db
	check(audible_db > -40.0, "som audível por padrão: %s dB" % audible_db)

	ambience.set_muted(true)
	check(player.volume_db <= -70.0, "mudo: volume praticamente zero (%s dB)" % player.volume_db)
	ambience.set_muted(false)
	eq(player.volume_db, audible_db, "desmutar devolve o volume")
	ambience.queue_free()


func test_m_key_toggles_ambience_mute() -> void:
	var app = await _app_with(_online_transport())
	var key := InputEventKey.new()
	key.pressed = true
	key.keycode = KEY_M

	app._unhandled_input(key)
	eq(app.ambience.muted, true, "M silencia")
	check(app.ambience._player.volume_db <= -70.0, "e o volume cai")
	app._unhandled_input(key)
	eq(app.ambience.muted, false, "M de novo religa")
	app.queue_free()


func test_clouds_drift_and_wrap() -> void:
	var SkyLife := preload("res://Scenes/sky_life.gd")
	var sky := SkyLife.new()
	sky.build(Vector2(30, 30))
	eq(sky.clouds.size(), SkyLife.CLOUD_COUNT)
	var before: float = sky.clouds[0].position.x
	for puff in sky.clouds[0].get_children():
		eq((puff as GeometryInstance3D).cast_shadow, GeometryInstance3D.SHADOW_CASTING_SETTING_SHADOWS_ONLY, "só a sombra aparece, a nuvem não tapa a vista")

	sky.advance(2.0)
	check(sky.clouds[0].position.x > before, "a nuvem andou")
	sky.clouds[0].position.x = 30.0 * 1.3 + 1.0
	sky.advance(0.1)
	check(sky.clouds[0].position.x < 0.0, "ao sair do mapa a nuvem volta pelo outro lado")
	sky.free()


func test_door_gets_a_lamp_that_lights_at_night() -> void:
	var WorldView := preload("res://Scenes/world_view.gd")
	var view := WorldView.new()
	var building := {"id": 1, "type": -1, "cell": Vector2i(2, 2), "floors": 1, "orientation": 0, "footprint": [
		{"cell": Vector2i(0, 0), "material": 1}, {"cell": Vector2i(1, 0), "material": 1}, {"cell": Vector2i(2, 0), "material": 1},
		{"cell": Vector2i(0, 1), "material": 1}, {"cell": Vector2i(1, 1), "material": 3}, {"cell": Vector2i(2, 1), "material": 1},
		{"cell": Vector2i(0, 2), "material": 1}, {"cell": Vector2i(1, 2), "material": 2}, {"cell": Vector2i(2, 2), "material": 1}]}
	var node: Node3D = view._make_building(building)
	check(node.get_node_or_null("Lamp") != null, "há um poste de luz na porta")

	var sun := DirectionalLight3D.new()
	var cycle := DayCycle.new()
	cycle.bind(sun, Environment.new(), view.window_material, view.lamp_material)
	cycle.sync(12.0, 1.0, true)
	eq(view.lamp_material.emission_energy_multiplier, 0.0, "poste apagado de dia")
	cycle.sync(18.0, 1.0, true)
	var dusk: float = view.lamp_material.emission_energy_multiplier
	cycle.sync(23.0, 1.0, true)
	var night: float = view.lamp_material.emission_energy_multiplier
	check(night > 2.0, "poste aceso à noite")
	check(dusk > 0.0 and dusk < night, "no entardecer o poste acende aos poucos: %s < %s" % [dusk, night])
	cycle.sync(5.0, 1.0, true)
	check(view.lamp_material.emission_energy_multiplier > 0.0 and view.lamp_material.emission_energy_multiplier < night, "e apaga aos poucos ao amanhecer")
	node.free()
	sun.free()
	cycle.free()
	view.free()


func test_crowd_uses_simple_actors_when_far_and_detail_when_near() -> void:
	var WorldView := preload("res://Scenes/world_view.gd")
	var store := Store.new()
	var labels := ActionLabels.new()
	var view := WorldView.new()
	view.bind(store, labels)
	for i in 200:
		store.npcs[i] = {"id": i, "cell": Vector2i(i % 30, i / 30), "action": 5, "city": ""}
	view._rebuild_npcs()

	view.far_zoom = true
	view._process(0.016)
	var simple := view.npc_nodes.values().filter(func(a): return a.far_lod)
	eq(simple.size(), 200, "multidão com câmera longe: todos em versão simples")
	for actor in simple.slice(0, 5):
		check(not actor._arms[0].visible and not actor._legs[0].visible, "sem braços e pernas")
		for part in actor._shadow_parts:
			eq(part.cast_shadow, GeometryInstance3D.SHADOW_CASTING_SETTING_OFF, "tronco, cabeça e cabelo sem sombra própria")
		eq(actor._shadow_parts.size(), 3, "são três peças que projetam sombra")

	view.far_zoom = false
	view._process(0.016)
	eq(view.npc_nodes.values().filter(func(a): return a.far_lod).size(), 0, "câmera perto: detalhe completo")
	check(view.npc_nodes[0]._arms[0].visible, "os braços voltam")

	# poucos NPCs nunca viram versão simples, mesmo com a câmera longe
	for i in range(20, 200):
		view._on_npc_removed(i)
	view.far_zoom = true
	view._process(0.016)
	eq(view.npc_nodes.values().filter(func(a): return a.far_lod).size(), 0, "multidão pequena mantém o detalhe")
	view.free()


func _crowd_view(count: int) -> Object:
	var WorldView := preload("res://Scenes/world_view.gd")
	var store := Store.new()
	var view := WorldView.new()
	view.bind(store, ActionLabels.new())
	for i in count:
		store.npcs[i] = {"id": i, "cell": Vector2i(i % 30, i / 30), "action": 5, "city": ""}
	view._rebuild_npcs()
	view.view_center = Vector3(15, 0, 15)
	view.view_radius = 100.0
	return view


func test_crowd_threshold_is_exactly_150() -> void:
	for count in [150, 151]:
		var view = _crowd_view(count)
		view.far_zoom = true
		view._process(0.016)
		var simple: int = view.npc_nodes.values().filter(func(a): return a.far_lod).size()
		eq(simple, 0 if count == 150 else 151, "com %d NPCs e câmera longe" % count)
		view.free()


func test_walking_takes_the_same_time_in_a_crowd_and_alone() -> void:
	var alone = _crowd_view(5)
	var crowd = _crowd_view(300)
	for view in [alone, crowd]:
		view.far_zoom = false
		view.npc_nodes[0].move_to(Vector2i(20, 20), 1.0)
		for i in 30:
			view._process(1.0 / 60.0)
	check(is_equal_approx(alone.npc_nodes[0]._t, 0.5), "sozinho: meio passo em meio segundo (%s)" % alone.npc_nodes[0]._t)
	check(is_equal_approx(crowd.npc_nodes[0]._t, alone.npc_nodes[0]._t), "multidão: mesmo ritmo (%s)" % crowd.npc_nodes[0]._t)
	alone.free()
	crowd.free()


func test_scenes_drive_actor_animation_every_frame() -> void:
	var view = _crowd_view(5)
	var actor = view.npc_nodes[0]
	var clock_before: float = actor._clock
	view._process(0.1)
	check(actor._clock > clock_before, "o mundo anima os NPCs à vista a cada quadro")

	var InteriorView := preload("res://Scenes/interior_view.gd")
	var interior := InteriorView.new()
	var cells: Array = []
	for y in 3:
		for x in 3:
			cells.append({"cell": {"x": x, "y": y}, "material": 3})
	interior.show_interior({"floors": 2, "layout": [{"level": 0, "cells": cells}, {"level": 1, "cells": cells}], "occupants": [
		{"npc": {"value": 1}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}, "action": 2},
		{"npc": {"value": 2}, "floor": {"value": 1}, "localCell": {"x": 1, "y": 1}, "action": 2}]}, Vector2i(0, 0), ActionLabels.new())
	interior.set_floor(0)
	var visible_actor = interior.occupant_nodes[1]
	var hidden_actor = interior.occupant_nodes[2]
	interior._process(0.1)
	check(visible_actor._clock > 0.0, "o interior anima quem está no andar visível")
	eq(hidden_actor._clock, 0.0, "e não gasta tempo com quem está em outro andar")
	interior.free()
	view.free()


func test_lamp_brightness_ramps_monotonically_through_dusk_and_dawn() -> void:
	var material := StandardMaterial3D.new()
	var cycle := DayCycle.new()
	var sun := DirectionalLight3D.new()
	cycle.bind(sun, Environment.new(), StandardMaterial3D.new(), material)
	var dusk: Array = []
	for hour in [16.0, 17.0, 18.0, 19.0, 20.0, 23.0]:
		cycle.sync(hour, 1.0, true)
		dusk.append(material.emission_energy_multiplier)
	for i in range(1, dusk.size()):
		check(dusk[i] >= dusk[i - 1], "do fim da tarde à noite o poste só aumenta: %s" % [dusk])
	var dawn: Array = []
	for hour in [3.0, 5.0, 6.0, 7.0, 9.0]:
		cycle.sync(hour, 1.0, true)
		dawn.append(material.emission_energy_multiplier)
	for i in range(1, dawn.size()):
		check(dawn[i] <= dawn[i - 1], "da madrugada ao dia o poste só diminui: %s" % [dawn])
	sun.free()
	cycle.free()


func test_muted_ambience_produces_no_audio() -> void:
	var Ambience := preload("res://Scenes/ambience.gd")
	var ambience := Ambience.new()
	tree.root.add_child(ambience)
	ambience.set_muted(true)
	var before: float = ambience._noise
	ambience._process(0.5)
	eq(ambience._noise, before, "mudo não gera nenhuma amostra")
	ambience.queue_free()


func test_app_wires_lamps_ambience_sky_and_camera_feed() -> void:
	var app = await _app_with(_online_transport())

	eq(app.day_cycle.lamp_material, app.world_view.lamp_material, "o ciclo do dia acende os postes do mundo")
	eq(app.day_cycle.window_material, app.world_view.window_material, "e as janelas")
	app.rig.focus(Vector3(9, 0, 9))
	for i in 100:
		app.rig._process(0.05)
	app._process(0.1)
	check(app.world_view.view_center.distance_to(app.rig.target) < 0.01, "a cena sabe para onde a câmera olha")
	eq(app.world_view.far_zoom, app.rig.zoom_size > 16.0, "e se está longe")
	check(absf(app.ambience.daylight - app.day_cycle.daylight()) < 0.001, "o som segue a luz do dia")
	eq(app.sky_life.map_size, Vector2(app.store.map_size), "as nuvens usam o tamanho real do mapa")
	app.queue_free()


func test_calendar_from_server_drives_hour_and_day() -> void:
	var setup := _cycle()
	var cycle = setup[0]
	var calendar := {"hoursPerDay": 12, "daysPerMonth": 10, "monthsPerYear": 4}

	cycle.sync(18.0, 1.0, true, calendar)
	eq(cycle.clock_text(), "12:00", "tick 18 num dia de 12 h é meio-dia")
	eq(cycle.phase_name(), "Dia")
	cycle.sync(12.0, 1.0, true, calendar)
	eq(cycle.clock_text(), "00:00", "tick 12 é meia-noite")

	var date: Dictionary = cycle.date_parts(12.0 * 10.0 * 4.0 * 2.0 + 12.0 * 10.0 + 12.0 * 3.0)
	eq(date, {"year": 2, "month": 2, "day": 4}, "ano, mês e dia pelo calendário do servidor")
	var default_cycle := DayCycle.new()
	eq(default_cycle.date_parts(24.0 * 30.0 * 12.0 + 24.0 * 31.0), {"year": 1, "month": 2, "day": 2}, "sem calendário usa 24/30/12")
	default_cycle.free()
	for node in setup:
		if node is Node:
			node.free()
