extends "res://tests/test_base.gd"
## #241: ação em andamento visível. O motor diz (`busy` na pose: `action`, `totalMs`, `remainingMs`): o cliente mostra a barra com o nome
## ("Cortando lenha — 40%"), anima o corpo em laço, não manda passo nem mostra erro cru ocupado, e uma tecla (Espaço) interrompe.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const BodyBusy := preload("res://Scenes/body_busy.gd")
const BodyPose := preload("res://Scenes/body_pose.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Scenery := preload("res://Scenes/scenery.gd")
const Toasts := preload("res://Scenes/toasts.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const POSE := {"playerId": "p-1", "npcId": 7, "x": 3, "y": 4, "readyInMs": 600000, "fx": 3.5, "fy": 4.5, "speedMps": 1.4, "cellMeters": 1.5,
	"running": false, "busy": {"action": "gather", "untilMs": 900000, "totalMs": 600000, "remainingMs": 360000}}
const FREE := {"playerId": "p-1", "npcId": 7, "x": 3, "y": 4, "readyInMs": 0, "fx": 3.5, "fy": 4.5, "speedMps": 1.4, "cellMeters": 1.5, "running": false}


func _hud(replies: Dictionary = {}) -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	for route in replies:
		transport.on(route, replies[route])
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


func test_the_bar_says_what_the_body_is_doing_and_how_far_along() -> void:
	var busy := BodyBusy.new()
	check(not busy.active(), "sem busy na pose, livre")
	check(busy.note_pose(POSE, 1000.0, 1.0), "começou: mudou")
	eq(busy.action, "gather", "a ação é a do motor")
	eq(busy.text(1000.0), "Cortando lenha — 40% · faltam 6 min", "faltam 360 s de 600 s: 40% (tempo real, escala 1), e quanto falta em minutos de mundo")
	eq(busy.text(1000.0 + 180000.0), "Cortando lenha — 70% · faltam 3 min", "o tempo real passa e a barra anda")
	check(busy.expired(1000.0 + 360000.0), "vence quando o tempo que o motor disse acaba")
	check(busy.note_pose({"x": 1}, 2000.0, 1.0), "pose sem busy: terminou")
	check(not busy.active(), "e fica livre")
	var fast := BodyBusy.new()
	fast.note_pose(POSE, 0.0, 12.0) # relógio 12×: 360 s de mundo = 30 s reais
	check(absf(fast.seconds_left(0.0) - 30.0) < 0.01, "a escala do relógio converte o tempo de mundo em real")


func test_the_hud_shows_the_bar_above_the_card_and_hides_it_when_free() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	hud.apply_pose(POSE)
	check(hud.frame.busy_panel.visible, "a barra aparece")
	check(hud.frame.busy_label.text.begins_with("Cortando lenha — "), "com o nome da ação: " + hud.frame.busy_label.text)
	check(absf(hud.frame.busy_bar.value - 0.4) < 0.02, "no ponto que o motor disse")
	check(hud.frame.busy_panel.offset_bottom < hud.frame.card.offset_bottom, "acima do cartão do jogador")
	hud.apply_pose(FREE)
	check(not hud.frame.busy_panel.visible, "sem busy na pose, a barra some")
	_free(made)


func test_walking_while_busy_sends_no_step_and_says_why_in_plain_words() -> void:
	var made: Array = await _hud({"POST /players/p-1/walk": {"status": 409, "body": {"error": "conflict: walk: o corpo está ocupado (faltam 241165500 ms)"}}})
	var hud = made[0]
	var transport = made[1]
	hud.apply_pose(POSE)
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	var before: int = transport.count_calls("POST /players/p-1/walk")
	hud.glide(Vector2(1, 0), 0.2)
	hud._refresh_busy(true, 0.2)
	hud.glide(Vector2(1, 0), 0.2)
	await tree.process_frame
	eq(transport.count_calls("POST /players/p-1/walk"), before, "WASD ocupado não manda passo ao servidor")
	eq(warnings, ["Ocupado: cortando lenha (Espaço para parar)"], "e diz o que fazer, sem erro cru nem milissegundos")
	hud._refresh_busy(true, 0.2)
	eq(warnings.size(), 1, "sem repetir a cada quadro")
	_free(made)


func test_server_errors_are_never_shown_raw() -> void:
	eq(BodyBusy.human_error("conflict: walk: o corpo está ocupado (faltam 241165500 ms)"), "O corpo está ocupado.", "prefixo e ms somem")
	eq(BodyBusy.human_error("conflict: recusa: cansada demais para pular"), "Cansada demais para pular.", "recusa vira frase")
	eq(BodyBusy.human_error("move: o corpo ainda está no passo anterior (faltam 90 ms)"), "O corpo ainda está no passo anterior.", "passo")
	eq(BodyBusy.human_error(""), "O motor recusou.", "vazio")
	var made: Array = await _hud({"POST /players/p-1/walk": {"status": 409, "body": {"error": "conflict: walk: o corpo está ocupado (faltam 5 ms)"}}})
	var hud = made[0]
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	await hud.send_walk(Vector2(1, 0))
	eq(warnings, ["O corpo está ocupado."], "mesmo se o servidor recusar, o aviso é humano")
	_free(made)


func test_space_interrupts_the_action_instead_of_jumping_and_the_engine_decides() -> void:
	var made: Array = await _hud({"POST /players/p-1/interrupt": {"status": 200, "body": FREE},
		"POST /players/p-1/jump": {"status": 200, "body": {"playerId": "p-1", "npcId": 7, "airMs": 500}}})
	var hud = made[0]
	var transport = made[1]
	hud.apply_pose(POSE)
	var changes: Array = []
	hud.busy_changed.connect(func(action: String, _s: float): changes.append(action))
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	await hud.jump()
	eq(transport.count_calls("POST /players/p-1/interrupt"), 1, "Espaço ocupado pede ao motor para interromper")
	eq(transport.count_calls("POST /players/p-1/jump"), 0, "e não pula")
	check(not hud.busy.active(), "a pose do motor sem busy libera o corpo")
	check("" in changes, "o app é avisado para soltar o gesto")
	eq(warnings, ["Parou de cortar lenha."], "e o aviso diz o que aconteceu")
	check(not hud.frame.busy_panel.visible, "a barra some")
	_free(made)


func test_the_busy_state_ends_by_itself_and_asks_the_engine_for_the_pose() -> void:
	var made: Array = await _hud({"POST /players/p-1/walk": {"status": 200, "body": FREE}})
	var hud = made[0]
	var transport = made[1]
	hud.apply_pose(POSE)
	hud.busy._end_msec = 0.0 # o tempo que o motor disse acabou
	var changes: Array = []
	hud.busy_changed.connect(func(action: String, _s: float): changes.append(action))
	hud._refresh_busy(false, 0.1)
	check(not hud.busy.active(), "vencida, a ação sai")
	eq(changes, [""], "avisa o app")
	await tree.process_frame
	check(transport.count_calls("POST /players/p-1/walk") >= 1, "e confirma a pose com o motor")
	_free(made)


func test_the_warning_toast_disappears_by_itself() -> void:
	var toasts := Toasts.new()
	tree.root.add_child(toasts)
	toasts.compact = true
	toasts.push("O corpo está ocupado.")
	eq(toasts.visible_texts(), ["O corpo está ocupado."], "aparece")
	toasts._process(Toasts.LIFETIME + 0.1)
	await tree.process_frame
	eq(toasts.visible_texts(), [], "some sozinho depois do tempo")
	toasts.queue_free()


func test_the_body_chops_in_a_loop_while_busy_and_stops_with_the_engine() -> void:
	var actor := NpcActor.new()
	actor.setup(9, Vector2i(0, 0), 5, ActionLabels.new())
	actor.set_detail(true)
	eq(BodyBusy.gesture_for("gather"), "chop", "cortar lenha é o golpe de machado")
	actor.play_loop("chop", 30.0)
	var arms: Array = []
	for i in 24: # 2,4 s: mais de dois golpes (período 1,15 s)
		actor.animate(0.1)
		arms.append(actor._arms[1].rotation.x)
	eq(actor.gesture_playing(), "chop", "o gesto segue tocando muito depois de um gesto curto (laço)")
	var lo: float = arms.min()
	var hi: float = arms.max()
	check(hi - lo > 0.4, "o braço sobe e desce (%s a %s)" % [lo, hi])
	check(BodyPose.chop_swing(0.0) == 0.0 and BodyPose.chop_swing(0.55) >= 0.99 and BodyPose.chop_swing(0.9) == 0.0, "o ciclo levanta e desce")
	check(BodyPose.loop_weight(10.0, 30.0) == 1.0 and BodyPose.loop_weight(0.0, 30.0) == 0.0, "entra e sai suave, firme no meio")
	actor.stop_gesture()
	eq(actor.gesture_playing(), "", "o motor liberou: o gesto para")
	actor.free()


func test_the_gather_offer_reads_cut_wood_at_the_tree_and_aims_the_ring_at_the_trunk() -> void:
	var offers := [{"kind": "nature", "id": 4294967299, "label": "Árvore", "verbs": ["gather"], "x": 40, "y": 0,
		"resources": [{"resourceId": 3, "quantity": 2, "price": 0}], "fx": 40.328, "fy": 0.68}]
	var entries := PlayerHud.menu_entries(offers, func(id: int): return "Madeira")
	eq(entries.size(), 1, "uma linha")
	eq(PlayerFrame.prompt_text(String(entries[0].text)), "Cortar lenha · Árvore", "o rótulo diz árvore, não mata")
	var made: Array = await _hud()
	var hud = made[0]
	var aimed: Array = []
	hud.target_changed.connect(func(kind: String, id: int, cell: Vector2): aimed.append([kind, id, cell]))
	hud.set_reach_hint(offers)
	eq(aimed.size(), 1, "o alvo vai ao app")
	eq(aimed[0][0], "nature", "tipo")
	check(absf(aimed[0][2].x + 0.5 - 40.328) < 0.001 and absf(aimed[0][2].y + 0.5 - 0.68) < 0.001, "o anel mira o tronco (fx, fy), não o centro da célula")
	_free(made)


func test_the_tree_the_client_draws_is_the_one_the_engine_offers() -> void:
	# o motor (PlayerNature.TreeAt) usa este mesmo sorteio; os valores abaixo são os do teste .NET (mesmas células e sais)
	check(absf(Scenery.cell_hash(Vector2i(3, 5)) - 0.9912) < 0.00005, "cell_hash(3,5)")
	check(absf(Scenery.cell_hash(Vector2i(12, 40), 1) - 0.8887) < 0.00005, "cell_hash(12,40,1)")
	check(absf(Scenery.cell_hash(Vector2i(-4, 7), 2) - 0.3875) < 0.00005, "cell_hash(-4,7,2)")
	eq(Scenery.TREE_CHANCE, 0.16, "a mesma chance de árvore por célula")
	eq(Scenery.TREE_JITTER, 0.6, "e o mesmo deslocamento do tronco")


func test_the_bar_says_time_is_accelerated_and_follows_the_pace_of_the_world() -> void:
	var busy := BodyBusy.new()
	var jornada := {"busy": {"action": "gather", "untilMs": 24000000, "totalMs": 24000000, "remainingMs": 24000000}} # ~6 h 40 min de mundo
	busy.note_pose(jornada, 0.0, 1.0) # o relógio ainda em tempo real: 6 h 40 min faltam
	eq(busy.text(0.0), "Cortando lenha — 0% · faltam 6 h 40 min", "sem poder pular (ou antes do salto), diz o que falta em horas e minutos de mundo")
	busy.retime(0.0, 8.0 * 3600.0, 1.0) # o motor acelera a 8 h de mundo por segundo real
	check(busy.accelerated, "ritmo bem acima do real: acelerado")
	eq(busy.text(0.0), "Cortando lenha — 0% · tempo acelerado", "o rótulo diz que o tempo está acelerado")
	check(absf(busy.seconds_left(0.0) - 24000.0 / (8.0 * 3600.0)) < 0.01, "a espera real cai para menos de um segundo por hora de mundo: %f s" % busy.seconds_left(0.0))
	var half := busy.seconds_left(0.0) * 1000.0 / 2.0
	check(absf(busy.fraction(half) - 0.5) < 0.01, "a barra anda pelo mundo: metade do tempo, metade da barra")
	busy.retime(half, 1.0, 1.0) # algo o chamou: volta ao tempo real
	check(not busy.accelerated, "voltou ao tempo normal: some o rótulo")
	check(absf(busy.fraction(half) - 0.5) < 0.01, "a barra não pula ao mudar o ritmo")
	check(busy.text(half).contains("faltam"), "e volta a dizer o que falta")
	eq(BodyBusy.left_text(35.0 * 60000.0), "35 min", "minutos")
	eq(BodyBusy.left_text(4.0 * 3600000.0 + 12.0 * 60000.0), "4 h 12 min", "horas e minutos")


func test_the_hud_goes_accelerated_when_the_engine_clock_runs_fast_and_back_when_it_slows() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	var jornada := POSE.duplicate(true)
	jornada.busy = {"action": "gather", "untilMs": 24000000, "totalMs": 24000000, "remainingMs": 24000000}
	hud.apply_pose(jornada)
	check(not hud.frame.busy_label.text.contains("acelerado"), "em tempo real, sem o rótulo")
	hud.set_world_rate(8.0) # status do relógio: 8 h de mundo por segundo real
	check(hud.frame.busy_label.text.ends_with("· tempo acelerado"), "acelerado: " + hud.frame.busy_label.text)
	hud.set_world_rate(1.0 / 3600.0) # voltou a 1:1
	check(not hud.frame.busy_label.text.contains("acelerado"), "voltou ao ritmo normal: " + hud.frame.busy_label.text)
	_free(made)


func test_the_sky_slides_to_the_engine_clock_while_accelerated_instead_of_jumping() -> void:
	var cycle = preload("res://Scenes/day_cycle.gd").new()
	cycle.sync(10.0, 8.0, false)
	cycle.advance(0.5) # 4 h de mundo no ritmo acelerado
	var before: float = cycle._tick
	cycle.sync(before + 1.0, 8.0, false) # o motor está 1 h adiante da estimativa: não salta
	eq(cycle._tick, before, "a leitura do motor não salta o céu")
	cycle.advance(0.05)
	check(cycle._tick > before + 0.4 and cycle._tick < before + 0.4 + 1.0, "ele corre e a diferença some aos poucos")
	for i in 20:
		cycle.advance(0.05)
	check(absf(cycle._tick - (before + 1.0 + 8.0 * 1.05)) < 0.01, "e em um segundo está onde o motor disse: %f" % cycle._tick)
	cycle.sync(100.0, 8.0, false) # salto grande de verdade (outro mundo): acerta de uma vez
	eq(cycle._tick, 100.0, "erro grande corrige de uma vez")
	cycle.free()
