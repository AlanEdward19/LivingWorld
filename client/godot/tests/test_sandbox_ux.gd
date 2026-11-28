extends "res://tests/test_base.gd"
## #260 (UX do sandbox a nível The Sims): velocidade do tempo no HUD (pausa, 1×, 2×, 3×), corte de paredes, luz de objeto à noite, barras com número,
## ícone e tendência, aviso de fim de ação, guia pelo estado do motor, ofertas sem "Vender" dentro de casa. O cliente só mostra o que o motor diz.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const PlayerGuide := preload("res://Scenes/player_guide.gd")
const NeedIcon := preload("res://Scenes/need_icon.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")
const TitleScreen := preload("res://Scenes/menu/title_screen.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const App := preload("res://Scenes/app.gd")


func _time_view(speed: int, can: bool = true) -> Dictionary:
	return {"rhythm": "RealTime", "tickDeltaMs": 50 * maxi(speed, 1), "timeScale": 1.0, "playersPresent": 1, "canSkip": can, "skipping": false, "sleeping": false,
		"speed": speed, "maxSpeed": 3}


func _hud() -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("GET /players/p-1/time", {"status": 200, "body": _time_view(1)})
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	return [hud, transport]


func _free(made: Array) -> void:
	made[0].frame.queue_free()
	made[0].queue_free()


func _key(code: int, shift: bool = false) -> InputEventKey:
	var key := InputEventKey.new()
	key.keycode = code
	key.pressed = true
	key.shift_pressed = shift
	return key


# --- velocidade do tempo -------------------------------------------------------------------------------------------------------------------

func test_the_speed_buttons_show_only_when_the_engine_allows_and_mark_the_current_speed() -> void:
	var frame := PlayerFrame.new()
	frame.set_speed(_time_view(1, false))
	eq(frame.speed_box.visible, false, "com outro jogador (ou sem permissão) os botões somem")
	frame.set_speed(_time_view(2))
	eq(frame.speed_box.visible, true, "sozinho no mundo, os botões aparecem")
	eq(frame.speed_buttons.size(), 4, "pausa, 1×, 2× e 3×")
	eq(frame.speed_buttons[2].button_pressed, true, "o 2× está marcado")
	eq(frame.speed_buttons[1].button_pressed, false, "e o 1× não")
	var chosen: Array = []
	frame.speed_chosen.connect(func(speed: int): chosen.append(speed))
	frame.speed_buttons[3].pressed.emit()
	eq(chosen, [3], "o botão 3× pede velocidade 3")
	frame.speed_buttons[0].pressed.emit()
	eq(chosen, [3, 0], "o primeiro é a pausa")
	frame.set_speed({"canSkip": true, "speed": 1, "maxSpeed": 2})
	eq(frame.speed_buttons[3].visible, false, "o cenário que só deixa até 2× esconde o 3×")
	frame.free()


func test_the_number_keys_ask_the_engine_for_the_speed_and_the_answer_updates_the_hud() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	var transport = made[1]
	transport.on("POST /players/p-1/speed", {"status": 200, "body": _time_view(3)})
	hud._unhandled_input(_key(KEY_3))
	for i in 6:
		await tree.process_frame
	eq(transport.count_calls("POST /players/p-1/speed"), 0, "sem os botões visíveis (o motor não deixa), a tecla não pede nada")
	hud.apply_time(_time_view(1))
	hud._unhandled_input(_key(KEY_3))
	for i in 6:
		await tree.process_frame
	eq(transport.count_calls("POST /players/p-1/speed"), 1, "a tecla 3 pede a velocidade 3 ao motor")
	eq(int(transport.bodies["POST /players/p-1/speed"].back().get("speed", -1)), 3, "o corpo do pedido leva a velocidade")
	eq(hud.speed_now(), 3, "o HUD adota o que o motor respondeu")
	eq(hud._time_scale(), 3.0, "a passada e as barras de ação contam o mundo 3× mais rápido")
	eq(hud.frame.speed_buttons[3].button_pressed, true, "o botão 3× fica marcado")
	_free(made)


func test_a_refused_speed_keeps_the_hud_and_says_why() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	hud.apply_time(_time_view(1))
	made[1].on("POST /players/p-1/speed", {"status": 409, "body": {"error": "conflict: só quem joga sozinho muda a velocidade do tempo"}})
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.set_speed(2)
	eq(hud.speed_now(), 1, "recusado: a velocidade não muda")
	check(warnings.size() == 1 and "sozinho" in String(warnings[0]), "o aviso diz o motivo (%s)" % [warnings])
	_free(made)


func test_while_paused_the_body_does_not_walk_and_the_hud_says_how_to_continue() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	hud.apply_time(_time_view(0))
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	hud.glide(Vector2(0, -1), 0.1)
	hud.glide(Vector2(0, -1), 0.1)
	eq(warnings.size(), 1, "um aviso só, sem repetir a cada quadro")
	check("aperte 1" in String(warnings[0]), "diz como voltar (%s)" % [warnings])
	eq(hud._walk_dir, Vector2.ZERO, "nenhum rumo vai ao motor")
	_free(made)


func test_the_screen_clock_follows_the_players_pause_and_speed() -> void:
	eq(App.clock_paused({"isPaused": false, "playerSpeed": 0}), true, "a pausa do jogador para o relógio")
	eq(App.clock_paused({"isPaused": false, "playerSpeed": 2}), false, "2× corre")
	eq(App.hours_per_second({"playerSpeed": 0, "tickDeltaMs": 50, "loopTicksPerSecond": 20.0}), 0.0, "pausado, nenhuma hora por segundo")
	check(App.hours_per_second({"playerSpeed": 3, "tickDeltaMs": 150, "loopTicksPerSecond": 20.0}) > App.hours_per_second({"playerSpeed": 1, "tickDeltaMs": 50, "loopTicksPerSecond": 20.0}) * 2.9, "3× anda 3 vezes mais")
	eq(App.speed_suffix({"isPaused": false, "playerSpeed": 3}), "  ·  3×", "o relógio diz a velocidade")
	eq(App.speed_suffix({"isPaused": false, "playerSpeed": 1}), "", "1× não diz nada")
	check("pausado" in App.speed_suffix({"isPaused": false, "playerSpeed": 0}), "pausado diz pausado")


# --- paredes cortadas ----------------------------------------------------------------------------------------------------------------------

func _room(mode: int) -> Array:
	var cells: Array = []
	var walls: Array = []
	for x in range(1, 4):
		cells.append({"cell": {"x": x, "y": 1}, "material": 3})
		walls.append({"a": {"x": x, "y": 0}, "b": {"x": x, "y": 1}, "material": 1})
		walls.append({"a": {"x": x, "y": 1}, "b": {"x": x, "y": 2}, "material": 1})
	walls.append({"a": {"x": 0, "y": 1}, "b": {"x": 1, "y": 1}, "material": 1})
	walls.append({"a": {"x": 3, "y": 1}, "b": {"x": 4, "y": 1}, "material": 1})
	var view := InteriorView.new()
	tree.root.add_child(view)
	view.immersive = true
	view.wall_mode = mode
	view.show_interior({"building": 1, "floors": 1, "layout": [{"level": 0, "cells": cells, "walls": walls, "doors": []}], "occupants": []}, Vector2i(0, 0), ActionLabels.new())
	var actor := NpcActor.new()
	actor.setup(1, Vector2i(3, 1), -1, ActionLabels.new())
	tree.root.add_child(actor)
	actor.position = Vector3(3.5, 0, 1.5)
	view.guide_body(1, actor, 0)
	view.set_view_direction(Vector3.ZERO)
	view.update_near(Vector3(-6.0, 3.0, 1.5)) # a câmera alta, do lado oeste
	return [view, actor]


func _heights(view: Node) -> Array:
	var low := 0
	var tall := 0
	for wall in view._walls:
		if wall.get("lintel", false):
			continue
		var height: float = (wall.mesh as BoxMesh).size.y
		if is_equal_approx(height, InteriorView.cutaway_height()):
			low += 1
		elif is_equal_approx(height, InteriorView.wall_height()):
			tall += 1
	return [low, tall]


func test_the_cutaway_lowers_the_walls_on_the_cameras_side_and_keeps_the_others() -> void:
	var made := _room(InteriorView.WALLS_CUT)
	var view = made[0]
	var counts := _heights(view)
	check(counts[0] >= 1, "as paredes do lado da câmera ficam baixas (%s)" % [counts])
	check(counts[1] >= 1, "as do fundo seguem inteiras (%s)" % [counts])
	for wall in view._walls:
		if not wall.get("lintel", false) and is_equal_approx((wall.mesh as BoxMesh).size.y, InteriorView.cutaway_height()):
			eq((wall.node as MeshInstance3D).transparency, 0.0, "a baixa é opaca: lê-se a planta")
	view.release_guided()
	view.queue_free()
	made[1].queue_free()


func test_v_cycles_the_wall_mode_up_down_and_cut() -> void:
	var made := _room(InteriorView.WALLS_CUT)
	var view = made[0]
	eq(view.cycle_wall_mode(), InteriorView.WALLS_UP, "depois do corte, paredes inteiras")
	view.update_near(Vector3(-6.0, 3.0, 1.5))
	eq(_heights(view)[0], 0, "inteiras: nenhuma baixa")
	eq(view.cycle_wall_mode(), InteriorView.WALLS_DOWN, "depois, todas baixas")
	view.update_near(Vector3(-6.0, 3.0, 1.5))
	eq(_heights(view)[1], 0, "baixas: nenhuma inteira")
	eq(view.cycle_wall_mode(), InteriorView.WALLS_CUT, "e volta ao corte")
	view.update_near(Vector3(-6.0, 3.0, 1.5))
	check(_heights(view)[0] >= 1, "de volta ao corte, as do lado da câmera baixam de novo")
	view.release_guided()
	view.queue_free()
	made[1].queue_free()


func test_in_first_person_the_walls_stay_whole() -> void:
	var made := _room(InteriorView.WALLS_CUT)
	var view = made[0]
	view.update_near(Vector3(3.5, 1.2, 1.5)) # a câmera na altura dos olhos, no mesmo lugar do corpo
	eq(_heights(view)[0], 0, "o olho dentro do cômodo não corta parede nenhuma")
	view.release_guided()
	view.queue_free()
	made[1].queue_free()


func test_entering_turns_the_look_away_from_a_wall_toward_the_open_space() -> void:
	var made := _room(InteriorView.WALLS_CUT)
	var view = made[0]
	var from := Vector2(3.5, 1.5) # no fim do corredor 3×1: a parede leste está a meia célula
	var turned: Vector2 = view.open_direction(from, Vector2(0, -1)) # olhando para a parede norte, colada
	check(turned != Vector2.ZERO and turned.x < -0.5, "de nariz na parede, vira para onde há espaço (o corredor, a oeste): %s" % turned)
	eq(view.open_direction(from, Vector2(-1, 0)), Vector2.ZERO, "já olhando para o espaço livre, não mexe")
	check(view.free_distance(from, Vector2(-1, 0)) > view.free_distance(from, Vector2(0, -1)) * 3.0, "o espaço livre é bem maior que a parede colada")
	view.release_guided()
	view.queue_free()
	made[1].queue_free()


func test_yaw_toward_the_room_turns_the_camera_to_face_the_direction() -> void:
	for direction in [Vector2(0, 1), Vector2(1, 0), Vector2(-1, -1).normalized()]:
		var yaw := App.yaw_toward(direction)
		var forward := Vector3.FORWARD.rotated(Vector3.UP, yaw)
		check(Vector2(forward.x, forward.z).distance_to(direction) < 0.001, "o olhar aponta para %s" % [direction])


# --- luz de objeto à noite -----------------------------------------------------------------------------------------------------------------

func test_an_object_with_a_light_in_the_catalog_gives_a_lamp_that_turns_on_at_night() -> void:
	var factory := ObjectFactory.new([{"id": "lamp", "shape": [{"shape": "box", "size": [0.3, 1.0, 0.3], "offset": [0, 0.5, 0], "color": "4a3f35"}],
		"light": {"color": "ffd9a0", "rangeM": 7.0, "energy": 1.6, "heightM": 1.3}}, {"id": "bed", "shape": [{"shape": "box", "size": [1, 0.4, 1], "offset": [0, 0.2, 0], "color": "d8cfb8"}]}])
	eq(factory.light_of("bed"), {}, "a cama não dá luz")
	eq(float(factory.light_of("lamp").energy), 1.6, "o abajur dá a luz que o catálogo diz")
	var view := InteriorView.new()
	tree.root.add_child(view)
	var holder := Node3D.new()
	view.add_child(holder)
	view._add_lamp(holder, factory.light_of("lamp"))
	eq(view.lamp_count(), 1, "uma luz")
	var lamp: OmniLight3D = holder.get_node("Lamp")
	eq(lamp.visible, false, "apagada até escurecer")
	view.set_lamp_level(1.0)
	eq(lamp.visible, true, "acesa")
	check(is_equal_approx(lamp.light_energy, 1.6), "com a intensidade do catálogo")
	check(lamp.omni_range > 3.0, "alcance de uns 7 m em unidades da grade (%s)" % lamp.omni_range)
	view.set_lamp_level(0.0)
	eq(lamp.visible, false, "de dia, apaga")
	view.queue_free()


func test_the_lamps_light_only_when_it_is_dark_and_the_house_is_occupied() -> void:
	eq(App.lamp_level(0.0, true), 1.0, "noite com gente em casa: acesas")
	eq(App.lamp_level(0.0, false), 0.0, "noite e casa vazia: apagadas")
	eq(App.lamp_level(1.0, true), 0.0, "dia: apagadas")
	var dusk := App.lamp_level(0.4, true)
	check(dusk > 0.0 and dusk < 1.0, "no entardecer acendem aos poucos (%s)" % dusk)


# --- barras com número, ícone e tendência -----------------------------------------------------------------------------------------------------

func test_each_need_bar_shows_percent_icon_and_trend_from_the_engines_rates() -> void:
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	frame.set_body("Ana", "Parada", {"health": 100, "hunger": 15, "thirst": 70, "sleep": 50, "social": 80,
		"needRates": {"hunger": -2.0, "thirst": 9.0, "sleep": -0.1, "hygiene": -2.5},
		"catalogNeeds": [{"id": "hygiene", "name": "Higiene", "value": 32, "urgent": true, "color": "5fc7c0", "icon": "soap"}]})
	eq(frame._rows.hunger.pct.text, "15%", "o número da fome")
	eq(frame._rows.hunger.trend.trend, -1, "a fome cai devagar")
	eq(frame._rows.thirst.trend.trend, 2, "a sede sobe rápido (bebendo)")
	eq(frame._rows.sleep.trend.trend, 0, "o sono está estável")
	eq(frame._rows.hunger.icon.kind, "food", "ícone da fome")
	eq(frame._rows.hygiene.icon.kind, "soap", "o ícone da higiene vem do catálogo")
	eq(frame._rows.hygiene.pct.text, "32%", "o número da higiene")
	eq(frame._rows.hygiene.trend.trend, -1, "a higiene cai")
	eq(frame._pulse_id, "hygiene", "a mais urgente pulsa")
	frame._process(0.1)
	var first: Color = frame._rows.hygiene.bar.modulate
	frame._process(0.2)
	check(frame._rows.hygiene.bar.modulate != first, "a barra urgente pulsa (muda de cor entre quadros)")
	frame.set_body("Ana", "Parada", {"health": 100, "hunger": 90, "thirst": 70, "sleep": 50, "social": 80,
		"catalogNeeds": [{"id": "hygiene", "name": "Higiene", "value": 80, "urgent": false, "color": "5fc7c0", "icon": "soap"}]})
	eq(frame._pulse_id, "", "nada urgente, nada pulsa")
	eq(frame._rows.hunger.trend.visible, false, "sem taxa do motor, sem seta")
	frame.queue_free()


func test_the_trend_levels_come_from_points_per_hour() -> void:
	eq(NeedIcon.level_for(0.2), 0, "quase parado")
	eq(NeedIcon.level_for(-2.0), -1, "cai")
	eq(NeedIcon.level_for(-12.0), -2, "cai rápido")
	eq(NeedIcon.level_for(3.0), 1, "sobe")
	eq(NeedIcon.level_for(40.0), 2, "sobe rápido")


# --- aviso de fim de ação, guia e ofertas -----------------------------------------------------------------------------------------------------

func test_the_end_of_a_use_is_announced_with_what_it_gave() -> void:
	var sheet := {"hunger": 40, "catalogNeeds": [{"id": "hygiene", "name": "Higiene", "value": 92}]}
	eq(PlayerHud.finish_text("Tomar banho", "hygiene", sheet), "Terminou: Tomar banho · Higiene 92%", "o uso do catálogo e a necessidade que subiu")
	eq(PlayerHud.finish_text("Comer", "hunger", sheet), "Terminou: Comer · Fome 40%", "uma das de sempre")
	eq(PlayerHud.finish_text("Assistir TV", "", sheet), "Terminou: Assistir TV", "sem necessidade, só o fim")


func test_a_use_that_ends_by_itself_leaves_a_note_but_a_stopped_one_does_not() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	hud._use_needs["Tomar banho"] = "hygiene"
	hud._note_busy({"busy": {"action": "use", "use": "shower", "label": "Tomar banho", "phase": "do", "totalMs": 900000, "remainingMs": 500000}})
	hud._note_busy({})
	eq(hud._ended_use.get("label"), "Tomar banho", "acabou sozinho: o aviso espera a ficha")
	eq(hud._ended_use.get("need"), "hygiene", "com a necessidade que o uso sobe")
	hud._ended_use = {}
	hud._note_busy({"busy": {"action": "use", "use": "shower", "label": "Tomar banho", "phase": "do", "totalMs": 900000, "remainingMs": 500000}})
	hud._stopping = true
	hud._note_busy({})
	hud._stopping = false
	eq(hud._ended_use.is_empty(), true, "parar de propósito não é 'terminou'")
	_free(made)


func test_the_guide_does_not_tell_someone_already_home_to_enter() -> void:
	var start := {"child": false, "homeless": false, "home": {"x": 13, "y": 17, "distance": 0, "alone": false, "inside": true,
		"members": [{"id": 1, "name": "Rafael", "relation": "spouse"}], "pantry": []}, "pressing": "none", "money": 50}
	var steps: Array = PlayerGuide.steps_for(start, "Marina", Vector2(13.5, 17.5), Vector2(0, -1))
	var who := String(steps[0].text)
	check("para entrar" not in who, "não manda entrar quem está dentro (%s)" % who)
	check("Você está em casa" in who and "Rafael" in who, "diz que está em casa e com quem (%s)" % who)
	start.home.inside = false
	check("para entrar" in String(PlayerGuide.steps_for(start, "Marina", Vector2(13.5, 17.5), Vector2(0, -1))[0].text), "de fora, ainda manda entrar")


func test_the_house_sale_is_not_in_the_wheel_inside_and_the_pantry_takes_the_catalog_name() -> void:
	var offers := [{"kind": "stock", "id": 1, "label": "Geladeira", "verbs": ["deposit"], "resources": [], "x": 13, "y": 17},
		{"kind": "house", "id": 2, "label": "", "verbs": ["sell"], "resources": [], "x": 12, "y": 14}]
	var entries: Array = PlayerHud.menu_entries(offers)
	eq(String(entries[0].text), "Geladeira: Guardar o que leva", "a despensa tem o nome do móvel do catálogo")
	eq(PlayerHud.without_sale(entries, 0).size(), 1, "dentro de casa a roda não mostra vender")
	eq(String(PlayerHud.without_sale(entries, 0)[0].kind), "stock", "o que sobra é a geladeira")
	eq(PlayerHud.without_sale(entries, -1).size(), 2, "de fora, a casa mirada oferece a venda")
	eq(String(entries[1].text), "Sua casa: Vender", "e ela é a última")


func test_the_skill_and_title_texts_use_game_language() -> void:
	var GoalsPanel := preload("res://Scenes/goals_panel.gd")
	var text := GoalsPanel.skill_text({"name": "Morador", "level": 7, "maxLevel": 10, "toNext": 3.4, "practicing": false})
	check("7/10" not in text and "nível 7 de 10" in text and "faltam 4 pontos" in text, "sem '7/10' nem decimal (%s)" % text)
	eq(TitleScreen.continue_text("Mundo sem nome", "ano 0 · 20 pessoas"), "Continuar\nano 0 · 20 pessoas", "um mundo padrão não vira 'seu mundo'")
	eq(TitleScreen.continue_text("Vila Azul", "ano 3"), "Continuar: Vila Azul\nano 3", "o nome de verdade aparece")


func test_the_name_over_a_person_has_fixed_size_and_hides_while_the_wheel_is_open() -> void:
	var actor := NpcActor.new()
	actor.setup(2, Vector2i(3, 1), -1, ActionLabels.new())
	tree.root.add_child(actor)
	actor.set_display_name("Rafael")
	actor.set_hovered(true)
	var label: Label3D = actor._get_label()
	eq(label.fixed_size, true, "o nome não incha com a câmera perto")
	eq(label.visible, true, "aparece ao mirar")
	NpcActor.labels_muted = true
	actor._refresh_label()
	eq(label.visible, false, "com a roda aberta some")
	NpcActor.labels_muted = false
	actor.queue_free()
