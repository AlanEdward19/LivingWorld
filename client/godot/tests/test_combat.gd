extends "res://tests/test_base.gd"
## #234: o cliente do combate — comandos de esquiva e bloqueio, recarga e guarda lidas do motor, número de dano só com acerto
## confirmado, recusa discreta com o motivo do motor, e a animação que nasce do resultado e da reação projetada.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const CombatView := preload("res://Scenes/combat_view.gd")
const Feedback := preload("res://Scenes/feedback.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ReachRing := preload("res://Scenes/reach_ring.gd")
const Parse := preload("res://Store/observable_parse.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const POSE := {"playerId": "p-1", "npcId": 7, "x": 5, "y": 5, "fx": 5.5, "fy": 5.5, "speedMps": 1.4, "cellMeters": 1.5}
const CALM := {"strikeReadyInMs": 0, "guardMs": 0, "guardReadyInMs": 0, "reachMeters": 2.2}


func _pose(combat: Dictionary) -> Dictionary:
	var pose := POSE.duplicate()
	pose["combat"] = combat
	return pose


func _hud(transport) -> PlayerHud:
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.session_enabled = false
	tree.root.add_child(hud)
	return hud


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	return transport


func test_combat_view_reads_cooldown_guard_reach_and_new_incoming_blows_only_once() -> void:
	var view := CombatView.new()
	var events := view.note_pose(_pose({"strikeReadyInMs": 600, "guard": "Block", "guardMs": 500, "guardReadyInMs": 1200, "reachMeters": 2.2,
		"lastIncoming": {"outcome": "Hit", "damage": 7, "by": 9, "agoMs": 100}}), 10_000)
	eq(events, [{"outcome": "Hit", "damage": 7, "by": 9}], "o golpe recente é novo")
	eq([view.strike_ready(10_000), view.strike_ready(10_600), view.strike_wait_ms(10_200)], [false, true, 400], "recarga do golpe")
	eq([view.guarding(10_400), view.guarding(10_600), view.guard_wait_ms(10_000), view.reach_m], [true, false, 1200, 2.2], "guarda e alcance")
	var again := CALM.duplicate()
	again["lastIncoming"] = {"outcome": "Hit", "damage": 7, "by": 9, "agoMs": 180}
	eq(view.note_pose(_pose(again), 10_080).size(), 0, "o mesmo golpe na leitura seguinte não repete")
	var next := CALM.duplicate()
	next["lastIncoming"] = {"outcome": "Hit", "damage": 7, "by": 9, "agoMs": 50}
	eq(view.note_pose(_pose(next), 12_000).size(), 1, "outro golpe igual, tempos depois, é novo")
	var old := CombatView.new()
	var stale := CALM.duplicate()
	stale["lastIncoming"] = {"outcome": "Hit", "damage": 3, "by": 4, "agoMs": 60_000}
	eq(old.note_pose(_pose(stale), 5000).size(), 0, "golpe antigo visto pela 1ª vez é história")
	eq(old.note_pose(POSE, 5000).size(), 0, "pose sem combat não muda nada")


func test_refusal_text_is_the_engines_own_reason() -> void:
	eq(CombatView.refusal_text(400, {"error": "bad request: strike: Bia está a 3,4 m; alcance 2,2 m"}, "x"), "Bia está a 3,4 m; alcance 2,2 m", "alcance")
	eq(CombatView.refusal_text(409, {"error": "conflict: recusa: ainda se recupera do golpe (faltam 300 ms)"}, "x"), "ainda se recupera do golpe (faltam 300 ms)", "recarga")
	eq(CombatView.refusal_text(500, null, "O golpe não saiu"), "O golpe não saiu (erro 500).", "sem corpo")


func test_api_sends_dodge_and_block_commands_over_http() -> void:
	var transport := FakeTransport.new()
	var dodged := CALM.duplicate()
	dodged["guard"] = "Dodge"
	transport.on("POST /players/p-1/dodge", {"status": 200, "body": _pose(dodged)})
	transport.on("POST /players/p-1/block", {"status": 409, "body": {"error": "conflict: recusa: guarda em recarga"}})
	var api := ApiClient.new(transport)
	var dodge: Dictionary = await api.player_dodge("p-1")
	eq([dodge.ok, dodge.body.combat.guard], [true, "Dodge"], "esquiva aceita traz a pose com combat")
	var block: Dictionary = await api.player_block("p-1")
	eq([block.ok, block.status], [false, 409], "bloqueio em recarga é 409")
	eq(transport.calls, ["POST /players/p-1/dodge", "POST /players/p-1/block"], "rotas HTTP")


func test_commands_go_through_the_persistent_channel_like_walk() -> void:
	var transport := FakeTransport.new()
	transport.channel_replier = func(command: Dictionary) -> Dictionary:
		return {"seq": command.seq, "ok": true, "status": 200, "body": _pose(CALM)}
	var api := ApiClient.new(transport)
	api.player_token = "tok"
	api.open_player_channel()
	check(api.player_channel != null, "canal aberto")
	await api.player_block("p-1")
	await api.player_dodge("p-1")
	var sent: Array = api.player_channel.sent.map(func(c): return c.command)
	eq(sent, ["block", "dodge"], "comandos pelo canal persistente")
	eq(transport.calls, ["WS /players/ws"], "só a abertura do canal, nenhum pedido HTTP")


func test_strike_result_is_announced_and_the_refusal_only_shows_the_engine_reason() -> void:
	var transport := _transport()
	var struck := CALM.duplicate()
	struck["strikeReadyInMs"] = 900
	transport.on("POST /players/p-1/strike", {"status": 200, "body": {"playerId": "p-1", "targetId": 9, "outcome": "Hit", "damage": 12,
		"lethal": false, "reaction": "Flee", "reactionMs": 2000, "readyInMs": 900, "pose": _pose(struck)}})
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")
	var results: Array = []
	var refusals := []
	var warnings: Array = []
	hud.struck_result.connect(func(target: int, body: Dictionary): results.append([target, body.damage]))
	hud.refused.connect(func(): refusals.append(1))
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.strike(9)
	eq(results, [[9, 12]], "o resultado do motor chega com o dano real")
	eq(hud.combat.strike_ready(Time.get_ticks_msec()), false, "a recarga do motor vale logo depois do golpe")
	eq(hud.combat.reach_m, 2.2, "alcance lido da pose do golpe")
	transport.on("POST /players/p-1/strike", {"status": 400, "body": {"error": "bad request: Bia está a 4,1 m; alcance 2,2 m"}})
	await hud.strike(9)
	transport.on("POST /players/p-1/strike", {"status": 409, "body": {"error": "conflict: recusa: ainda se recupera do golpe (faltam 400 ms)"}})
	await hud.strike(9)
	eq(results.size(), 1, "recusa não é resultado de golpe")
	eq(refusals.size(), 2, "recusa só dispara o som de recusa")
	eq(warnings, ["Bia está a 4,1 m; alcance 2,2 m", "ainda se recupera do golpe (faltam 400 ms)"], "o motivo do motor, nada mais")
	hud.queue_free()


## #250: com o canal persistente a resposta chega quadros depois; a defesa tem que esperá-la com `await` em cada chamada (antes um
## `await (a() if x else b())` chamava a corrotina sem await e a engine dava "Trying to call an async function without await").
## O runner do gate trata esse erro de script como falha: este teste o provoca se a regressão voltar.
func test_defending_over_the_persistent_channel_awaits_the_reply_without_the_async_error() -> void:
	var transport := _transport()
	transport.channel_replier = func(command: Dictionary) -> Dictionary:
		var guarded := CALM.duplicate()
		guarded["guard"] = "Block" if command.command == "block" else "Dodge"
		guarded["guardMs"] = 600
		return {"seq": command.seq, "ok": true, "status": 200, "body": _pose(guarded)}
	var hud := _hud(transport)
	hud.api.player_token = "tok"
	hud.api.open_player_channel()
	await hud.incarnate(7, "Ana")
	var defended: Array = []
	hud.defended.connect(func(kind: String): defended.append(kind))
	await hud.dodge()
	await hud.block()
	eq(defended, ["Dodge", "Block"], "as duas defesas esperaram a resposta do canal")
	hud.queue_free()


func test_block_shows_the_guard_and_the_cooldown_in_the_frame() -> void:
	var transport := _transport()
	var guarded := CALM.duplicate()
	guarded["strikeReadyInMs"] = 800
	guarded["guard"] = "Block"
	guarded["guardMs"] = 700
	guarded["guardReadyInMs"] = 1200
	transport.on("POST /players/p-1/block", {"status": 200, "body": _pose(guarded)})
	var hud := _hud(transport)
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	hud.frame = frame
	await hud.incarnate(7, "Ana")
	var guards: Array = []
	var defended: Array = []
	hud.guard_changed.connect(func(kind: String): guards.append(kind))
	hud.defended.connect(func(kind: String): defended.append(kind))
	await hud.block()
	eq(defended, ["Block"], "o motor aceitou o bloqueio")
	eq(guards, ["Block"], "a guarda aparece no corpo")
	eq([frame.guard_label.visible, frame.guard_label.text], [true, "Guarda"], "guarda visível no cartão")
	eq(frame.cooldown.visible, true, "recarga do golpe discreta sob a mira")
	hud.combat.note_pose(_pose(CALM), Time.get_ticks_msec())
	hud._sync_guard()
	eq(guards, ["Block", ""], "acabou a janela: a guarda baixa")
	eq([frame.cooldown.visible, frame.guard_label.visible], [false, false], "nada sobra na tela fora de briga")
	transport.on("POST /players/p-1/block", {"status": 409, "body": {"error": "conflict: recusa: guarda em recarga"}})
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.block()
	eq(warnings, ["guarda em recarga"], "recusa mostra o motivo do motor")
	eq(defended.size(), 1, "recusa não é defesa")
	frame.queue_free()
	hud.queue_free()


func test_keys_block_with_right_button_or_f_and_dodge_with_ctrl_or_double_tap() -> void:
	var transport := _transport()
	transport.on("POST /players/p-1/block", {"status": 200, "body": _pose(CALM)})
	transport.on("POST /players/p-1/dodge", {"status": 200, "body": _pose(CALM)})
	var hud := _hud(transport)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	var right := InputEventMouseButton.new()
	right.button_index = MOUSE_BUTTON_RIGHT
	right.pressed = true
	eq(hud.combat_input(right), true, "botão direito é da guarda")
	eq(hud._block_held, true, "segurando")
	right.pressed = false
	hud.combat_input(right)
	eq(hud._block_held, false, "soltar desfaz o segurar")
	var f := InputEventKey.new()
	f.keycode = KEY_F
	f.pressed = true
	hud.combat_input(f)
	eq(hud._block_held, true, "F segura a guarda")
	f.pressed = false
	hud.combat_input(f)
	await hud.get_tree().process_frame
	check(transport.calls.count("POST /players/p-1/block") >= 1, "o bloqueio foi ao motor (%s)" % str(transport.calls))
	var ctrl := InputEventKey.new()
	ctrl.keycode = KEY_CTRL
	ctrl.pressed = true
	eq(hud.combat_input(ctrl), true, "Ctrl esquiva")
	await hud.get_tree().process_frame
	var after_ctrl: int = transport.calls.count("POST /players/p-1/dodge")
	eq(after_ctrl, 1, "Ctrl mandou a esquiva")
	var w := InputEventKey.new()
	w.keycode = KEY_W
	w.pressed = true
	hud.combat_input(w)
	await hud.get_tree().process_frame
	eq(transport.calls.count("POST /players/p-1/dodge"), after_ctrl, "um toque só anda, não esquiva")
	hud.combat_input(w)
	await hud.get_tree().process_frame
	eq(transport.calls.count("POST /players/p-1/dodge"), after_ctrl + 1, "o duplo toque na mesma direção esquiva")
	hud.queue_free()


func test_damage_numbers_only_for_a_confirmed_hit_with_real_damage() -> void:
	var fb := Feedback.new()
	tree.root.add_child(fb)
	fb.on_struck_result(9, {"outcome": "Hit", "damage": 12, "lethal": false})
	eq(fb.damage_shown, [{"amount": 12, "own": false}], "acerto confirmado mostra o dano do motor")
	fb.on_struck_result(9, {"outcome": "Hit", "damage": 0})
	fb.on_struck_result(9, {"outcome": "Dodged", "damage": 0})
	fb.on_struck_result(9, {"outcome": "Blocked", "damage": 3})
	fb.on_struck_result(9, {"outcome": "Missed"})
	eq(fb.damage_shown.size(), 1, "dano zero, esquiva, bloqueio e erro não mostram número")
	fb.on_struck_hit(9)
	eq(fb.damage_shown.size(), 1, "o golpe aceito sem resposta de dano não inventa número")
	eq([fb.played.has("swish"), fb.played.has("guard"), fb.played.has("hit")], [true, true, true], "esquiva, bloqueio e acerto soam diferente")
	var before := fb.played.size()
	fb.on_refused()
	eq(fb.played.slice(before), ["deny"], "recusa só toca o som neutro")
	eq(fb.damage_shown.size(), 1, "recusa não mostra número")
	fb.on_incoming("Hit", 5, 9)
	eq(fb.damage_shown[fb.damage_shown.size() - 1], {"amount": 5, "own": true}, "golpe recebido mostra o dano no próprio corpo")
	fb.on_incoming("Dodged", 0, 9)
	eq(fb.damage_shown.size(), 2, "esquivar o golpe recebido não mostra número")
	var sizes := fb.pool_sizes()
	for i in 100:
		fb.show_damage(Vector3(i, 0, 0), 1, Color.WHITE)
	eq(fb.pool_sizes(), sizes, "o pool de números de dano não cresce")
	fb.queue_free()


func _actor(id: int = 8) -> NpcActor:
	var node := NpcActor.new()
	node.setup(id, Vector2i(1, 1), 0, ActionLabels.new())
	tree.root.add_child(node)
	return node


func test_npcs_animate_from_the_projected_reaction() -> void:
	var flee := _actor()
	flee.apply_observable({"combat": "flee"})
	eq(flee.running, true, "fugir é correr")
	flee.apply_observable({"combat": ""})
	eq(flee.running, false, "a reação passou: volta a andar")
	var fight := _actor()
	fight.apply_observable({"combat": "retaliate"})
	eq(fight.striking(), true, "revidar é golpear")
	var help := _actor()
	help.apply_observable({"combat": "callHelp"})
	eq([help.gesture_playing(), help.running], ["call", true], "pedir ajuda é o gesto de chamar, correndo")
	var back := _actor()
	back.apply_observable({"combat": "recoil"})
	eq(back.defense_playing(), "recoil", "recuar é o passo atrás")
	var same := _actor()
	same.apply_observable({"combat": "flee"})
	same.running = false
	same.apply_observable({"combat": "flee"})
	eq(same.running, false, "só reage quando a reação muda")
	var own := _actor(7)
	own.set_self(true)
	own.apply_observable({"combat": "flee"})
	eq(own.running, false, "o corpo do jogador não é dirigido pela projeção")
	for node in [flee, fight, help, back, same, own]:
		node.queue_free()


func test_strike_has_windup_impact_and_recovery_and_defenses_look_different() -> void:
	eq([NpcActor.strike_phase(0.1), NpcActor.strike_phase(0.5), NpcActor.strike_phase(0.9)], ["windup", "impact", "recover"], "três tempos")
	check(NpcActor.strike_arm(0.0) == 0.0 and is_equal_approx(NpcActor.strike_arm(NpcActor.STRIKE_WINDUP), -2.6), "antecipação: o braço sobe")
	check(NpcActor.strike_arm(NpcActor.STRIKE_IMPACT) > 0.4, "impacto: o braço desceu")
	check(is_equal_approx(NpcActor.strike_arm(1.0), 0.0), "recuo: volta ao repouso")
	var poses := {}
	for kind in ["hurt", "dodge", "block", "recoil"]:
		var node := _actor()
		node.play_defense(kind)
		node.animate(float(NpcActor.DEFENSE_TIME[kind]) * 0.5)
		poses[kind] = [snappedf(node._body.rotation.x, 0.001), snappedf(node._body.rotation.z, 0.001), snappedf(node._arms[0].rotation.x, 0.001)]
		node.queue_free()
	eq(poses.size(), 4, "quatro reações")
	check(poses.hurt != poses.dodge and poses.dodge != poses.block and poses.block != poses.recoil and poses.hurt != poses.block, "acerto, esquiva e bloqueio são poses distintas (%s)" % str(poses))
	check(poses.dodge[1] > 0.0 and poses.hurt[0] < 0.0 and poses.block[2] < -1.0, "esquiva inclina de lado, ferida cai para trás, bloqueio ergue os braços (%s)" % str(poses))
	var guard := _actor()
	guard.set_guard(true)
	guard._animate(0.016)
	check(guard._arms[0].rotation.x < -1.0 and guard._arms[1].rotation.x < -1.0, "a guarda é visível no corpo enquanto vale")
	guard.set_guard(false)
	guard._animate(0.016)
	check(guard._arms[0].rotation.x > -1.0, "baixou a guarda: os braços voltam")
	guard.queue_free()


func test_projection_parses_the_optional_combat_field_and_the_reach_ring_follows_the_body() -> void:
	eq(Parse.observable({"emotions": {}, "combat": "flee"}).combat, "flee", "campo opcional lido")
	eq(Parse.observable({"emotions": {}}).combat, "", "omitido quando não há briga")
	var body := Node3D.new()
	tree.root.add_child(body)
	var ring := ReachRing.new()
	tree.root.add_child(ring)
	ring.show_around(body, 2.2)
	check(ring.visible and is_equal_approx(ring.radius_units(), 2.2 / 1.5), "o anel tem o alcance do motor em unidades do mundo")
	body.position = Vector3(3, 0, 4)
	ring._process(0.1)
	eq(ring.position.x, 3.0, "o anel acompanha o corpo")
	ring.show_around(body, 0.0)
	eq(ring.visible, false, "sem alcance dito pelo motor, nada se desenha")
	ring.queue_free()
	body.queue_free()


func test_the_hint_line_tells_how_to_block_and_dodge() -> void:
	check("bloquear" in PlayerFrame.COMBAT_HINTS and "esquivar" in PlayerFrame.COMBAT_HINTS, "a dica diz como defender")
	check("R golpe" in PlayerFrame.HINTS and "Clique ir até" in PlayerFrame.HINTS, "a dica do golpe (R) e do clique (ir até) estão na tela") # #267: o clique vai até; o golpe é R
