extends "res://tests/test_base.gd"
## #230: corpo procedural articulado: LOD do rig, IK de dois ossos, mistura andar→correr→parar, pé no degrau, mão no objeto.

const NpcActor := preload("res://Scenes/npc_actor.gd")
const BodyPose := preload("res://Scenes/body_pose.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const CharacterPreview := preload("res://Scenes/character_preview.gd")


func _walker(detail: bool = true) -> Node3D:
	var actor := NpcActor.new()
	actor.setup(5, Vector2i(0, 0), 5, ActionLabels.new())
	actor.set_detail(detail)
	actor.move_to(Vector2i(60, 0), 120.0) # anda devagar e por muito tempo: o teste só quer o passo em curso
	return actor


func _count(node: Node) -> int:
	var total := 1
	for child in node.get_children():
		total += _count(child)
	return total


func _ankle(actor: Node3D, i: int) -> Vector3:
	return (actor._legs[i].transform * actor._rig._knees[i].transform * actor._rig._ankles[i].transform).origin


func _hand(actor: Node3D, i: int) -> Vector3:
	var chain: Transform3D = actor._body.transform * actor._arms[i].transform * actor._rig._elbows[i].transform * actor._rig._hands[i].transform
	return chain * Vector3(0, -0.03, 0)


func test_rig_exists_only_near_and_costs_no_nodes_far() -> void:
	var far := NpcActor.new()
	far.setup(1, Vector2i(0, 0), 5, ActionLabels.new())
	var baseline := _count(far)
	far.set_far(true)
	far.advance(0.1)
	check(far._rig == null, "quem nunca esteve perto não tem rig")
	eq(_count(far), baseline, "longe: nenhum nó a mais que o corpo base")
	var near := NpcActor.new()
	near.setup(2, Vector2i(0, 0), 5, ActionLabels.new())
	near.set_detail(true)
	check(near._rig != null and near._rig.active, "perto liga o rig")
	check(_count(near) > baseline + 10, "o rig traz as articulações e as camadas de roupa")
	near.set_far(true)
	check(near._rig == null, "voltar a ficar longe libera o rig")
	far.free()
	near.free()


func test_leaving_detail_returns_to_the_exact_base_body() -> void:
	var actor := NpcActor.new()
	actor.setup(3, Vector2i(0, 0), 5, ActionLabels.new())
	actor.set_detail(true)
	var with_rig := _count(actor)
	actor.set_detail(false)
	check(not actor._rig.active, "saiu de perto: rig desligado")
	actor.set_bubble(true)
	check(actor._rig == null, "bolha libera o rig")
	actor.set_bubble(false)
	check(_count(actor) < with_rig - 10, "e os nós do rig somem")
	actor.free()


func test_the_players_body_and_the_creator_preview_are_always_articulated() -> void:
	var body := NpcActor.new()
	body.setup(8, Vector2i(0, 0), 5, ActionLabels.new())
	body.driven = true
	check(body._rig != null and body._rig.active, "o corpo do jogador é articulado mesmo sem detalhe")
	body.free()
	var preview := CharacterPreview.new()
	preview.show_character({"look": {"skinTone": 40, "hairTone": 50, "hairCurl": 30, "build": 50, "eyeTone": 20, "faceShape": 40, "outfit": 2, "heightM": 1.7}})
	check(preview.actor._rig != null and preview.actor._rig.active, "a prévia do criador mostra o mesmo corpo articulado")
	check(preview.actor.near_detail, "e com rosto")
	preview.free()


func test_two_bone_ik_reaches_the_target_and_stretches_when_out_of_reach() -> void:
	for target in [Vector3(0.1, -0.25, 0.15), Vector3(-0.05, -0.3, -0.1), Vector3(0.2, 0.05, 0.2), Vector3(0, -0.2, 0.25)]:
		var sol := BodyPose.solve_limb(target, 0.19, 0.15, Vector3(0, 0, 1))
		var end := BodyPose.limb_end(sol, 0.19, 0.15)
		check(end.distance_to(target) < 0.003, "os ossos chegam ao alvo %s (veio %s)" % [target, end])
	var stretched := BodyPose.solve_limb(Vector3(0, -1.0, 0), 0.19, 0.15, Vector3(0, 0, 1))
	check(float(stretched.flex) < 0.1, "fora do alcance estica sem dobrar")
	check(BodyPose.limb_end(stretched, 0.19, 0.15).normalized().distance_to(Vector3.DOWN) < 0.01, "e aponta para o alvo")
	var knee := BodyPose.solve_limb(Vector3(0, -0.25, 0.1), 0.19, 0.15, Vector3(0, 0, 1))
	check(float(knee.flex) > 0.2, "o joelho dobra quando o alvo está perto")
	var upper: Vector3 = Basis(knee.basis) * Vector3(0, -0.19, 0)
	check(upper.z > 0.0, "e para a frente (lado do pólo)")


func test_foot_path_is_continuous_and_planted_in_stance() -> void:
	var last := BodyPose.foot_path(0.0, 0.3, 0.05, 0.6)
	for step in range(1, 101):
		var now := BodyPose.foot_path(step / 100.0, 0.3, 0.05, 0.6)
		check(now.distance_to(last) < 0.03, "o pé não dá salto no ciclo (passo %d)" % step)
		last = now
	eq(BodyPose.foot_path(0.3, 0.3, 0.05, 0.6).y, 0.0, "no apoio o pé está no chão")
	check(BodyPose.foot_path(0.8, 0.3, 0.05, 0.6).y > 0.04, "no balanço o pé sobe")


func test_walk_to_run_to_stop_blends_without_snapping() -> void:
	var actor := _walker()
	for i in 20:
		actor.advance(0.05)
	check(actor._rig.gait > 0.9, "andando: peso da passada cheio")
	check(actor._rig.run < 0.05, "e sem corrida")
	var worst := 0.0
	var last := Vector3(actor._legs[0].rotation.x, actor._legs[1].rotation.x, actor._arms[0].rotation.x)
	actor.running = true
	for i in 30: # anda -> corre: peso de corrida sobe aos poucos
		actor.advance(0.02)
		var now := Vector3(actor._legs[0].rotation.x, actor._legs[1].rotation.x, actor._arms[0].rotation.x)
		worst = maxf(worst, (now - last).length())
		last = now
	check(actor._rig.run > 0.5, "correndo: peso de corrida sobe")
	check(worst < 0.6, "andar→correr sem estalo (maior salto por quadro %.2f rad)" % worst)
	var phase_before: float = actor._rig.phase
	actor.running = false
	actor._t = 1.0
	actor.walk = null # parou
	actor.advance(0.03)
	check(actor._rig.gait > 0.05 and actor._rig.gait < 0.95, "parar: o peso da passada decai, não corta (%.2f)" % actor._rig.gait)
	check(actor._rig.phase != phase_before, "a fase segue contínua ao frear")
	for i in 40:
		actor.advance(0.05)
	eq(actor._rig.gait, 0.0, "parado: passada zerada")
	check(absf(actor._legs[0].rotation.x) < 0.04, "pernas de pé")
	actor.free()


func test_feet_stay_on_the_ground_over_a_step() -> void:
	var actor := _walker()
	actor.ground_probe = func(_world: Vector3) -> float: return actor.position.y + 0.15 # chão 0,15 acima (degrau)
	for i in 30:
		actor.advance(0.05)
	var lowest := minf(_ankle(actor, 0).y, _ankle(actor, 1).y)
	check(absf(lowest - (BodyPose.ANKLE_H + 0.15)) < 0.02, "o pé de apoio pisa no degrau (tornozelo y=%.3f)" % lowest)
	var flat := _walker()
	for i in 30:
		flat.advance(0.05)
	var flat_low := minf(_ankle(flat, 0).y, _ankle(flat, 1).y)
	check(absf(flat_low - BodyPose.ANKLE_H) < 0.02, "no plano pisa no chão (y=%.3f)" % flat_low)
	actor.free()
	flat.free()


func test_hand_reaches_the_mouth_on_drink_and_the_object_on_give() -> void:
	var actor := NpcActor.new()
	actor.setup(6, Vector2i(0, 0), 5, ActionLabels.new())
	actor.set_detail(true)
	actor.play_gesture("drink", 1.0)
	actor.animate(0.5)
	var mouth := Vector3(0.035, 0.93, 0.115)
	check(_hand(actor, 1).distance_to(mouth) < 0.06, "a mão chega à boca ao beber (%s)" % _hand(actor, 1))
	var give := NpcActor.new()
	give.setup(6, Vector2i(0, 0), 5, ActionLabels.new())
	give.set_detail(true)
	var target := give.position + Vector3(0.2, 0.8, 0.4)
	give.play_gesture("give", 1.0, target)
	give.animate(0.5)
	check(_hand(give, 1).distance_to(Vector3(0.2, 0.8, 0.4)) < 0.07, "a mão vai ao ponto que o motor indicou (%s)" % _hand(give, 1))
	actor.free()
	give.free()


func test_a_gesture_over_the_walk_does_not_stop_the_legs() -> void:
	var actor := _walker()
	for i in 20:
		actor.advance(0.05)
	var before: float = actor._arms[1].rotation.x
	var phase_before: float = actor._rig.phase
	actor.play_gesture("reach", 1.0)
	actor.animate(0.016)
	check(absf(actor._arms[1].rotation.x - before) < 0.4, "o gesto entra misturado, sem estalo no braço")
	actor.animate(0.4)
	check(actor.gesture_playing() == "reach", "o gesto toca")
	check(actor._rig.gait > 0.9, "as pernas seguem a caminhada por baixo")
	check(actor._rig.phase > phase_before, "e a fase da passada avança")
	actor.free()


func test_every_gesture_and_jump_play_with_the_rig_on() -> void:
	var actor := NpcActor.new()
	actor.setup(9, Vector2i(0, 0), 5, ActionLabels.new())
	actor.set_detail(true)
	for kind in NpcActor.GESTURES:
		actor.play_gesture(kind, 1.0)
		for i in 5:
			actor.animate(0.2)
			check(actor._body.position.y < 0.5 and actor._body.position.y > -0.5, "gesto %s mantém o corpo no lugar" % kind)
	actor.animate(2.0)
	actor.play_jump(0.5)
	actor.animate(0.25)
	check(actor._body.position.y > 0.1, "o pulo sobe o corpo")
	check(actor._legs[0].position.y > 0.36, "e o quadril")
	actor.free()


func test_sleeping_body_still_lies_down_with_the_rig_on() -> void:
	var actor := NpcActor.new()
	actor.setup(3, Vector2i(1, 1), 1, ActionLabels.new())
	actor.set_detail(true)
	actor.advance(0.1)
	check(absf(actor._legs[0].rotation.x + PI / 2.0) < 0.01, "dormindo, a perna deita")
	actor.free()


func test_proportions_follow_the_age_stage() -> void:
	check(BodyPose.head_factor("baby") > BodyPose.head_factor("child"), "bebê tem a cabeça maior")
	eq(BodyPose.head_factor("adult"), 1.0, "adulto neutro")
	check(BodyPose.stride_factor("elder") < BodyPose.stride_factor("adult"), "idoso passa curto")
	var child := NpcActor.new()
	child.setup(4, Vector2i(0, 0), 5, ActionLabels.new())
	child.spec = {"stage": "child", "female": false}
	child.set_detail(true)
	check(child._head.scale.y > 1.2, "a criança perto tem a cabeça maior")
	child.set_detail(false)
	check(absf(child._head.scale.y - 1.0) < 0.01, "e a cabeça volta ao normal ao sair do detalhe")
	child.free()


## #256: a passada sai da velocidade REAL em m/s e do tamanho do corpo: a 1,1 m/s a cadência é de caminhada calma (~1,8 passos/s, não
## 2,5 de quem trota) e o pé de apoio recua na velocidade do corpo (sem patinar).
func test_the_cadence_follows_the_real_speed_and_stays_calm_at_the_sandbox_walk() -> void:
	var cell := 1.5
	var scale := 1.13
	var walk := BodyPose.gait_params(1.1 / cell, scale, "adult", 0.0, cell)
	check(float(walk.steps) > 1.6 and float(walk.steps) < 2.2, "1,1 m/s: %.2f passos/s (calma)" % float(walk.steps))
	var slow := BodyPose.gait_params(0.4 / cell, scale, "adult", 0.0, cell)
	check(float(slow.steps) >= 1.0 and float(slow.steps) < float(walk.steps), "mais devagar, menos passos (%.2f)" % float(slow.steps))
	var run := BodyPose.gait_params(3.0 / cell, scale, "adult", 1.0, cell)
	check(float(run.steps) > 2.6 and float(run.steps) < 3.8, "3 m/s corre a %.2f passos/s" % float(run.steps))
	var child := BodyPose.gait_params(1.1 / cell, 0.7, "child", 0.0, cell)
	check(float(child.stride) < float(walk.stride), "criança passada curta")
	for params in [walk, slow, run, child]: # sem deslize: o pé recua `stride` em `duty` do ciclo, na velocidade do corpo
		var speed_local: float = float(params.mps) / cell / (scale if params != child else 0.7)
		var foot_speed: float = float(params.stride) / (float(params.duty) / float(params.cycles))
		check(absf(foot_speed - speed_local) < 0.001, "pé de apoio na velocidade do corpo (%.3f x %.3f)" % [foot_speed, speed_local])


## #256: medido no corpo desenhado andando a 1,1 m/s: passos por segundo e o pé de apoio parado no chão (a distância que ele anda no
## mundo no apoio é ~0 em relação ao que o corpo percorre).
func test_a_body_walking_at_one_point_one_meters_per_second_steps_calmly_and_the_stance_foot_does_not_skate() -> void:
	var actor := _walker()
	actor.scale = Vector3.ONE * 1.13
	actor.walk = null
	var step := 1.1 / 1.5 # unidades de mundo por segundo
	var phase0 := 0.0
	var frames := 0
	var stance_drift := 0.0
	var stance_samples := 0
	var last_foot := Vector3.INF
	for i in 140:
		actor.position += actor.transform.basis.z.normalized() * step * 0.02 # anda para onde o corpo olha
		actor.animate(0.02)
		if i == 60:
			phase0 = actor._rig.phase
			frames = 0
		if i > 60:
			frames += 1
			var u := fposmod(actor._rig.phase / TAU, 1.0)
			if u < actor._rig._duty: # perna 0 em apoio: o tornozelo no mundo não pode correr com o corpo
				var foot: Vector3 = actor.transform * _ankle(actor, 0)
				if last_foot != Vector3.INF:
					stance_drift += foot.distance_to(last_foot)
					stance_samples += 1
				last_foot = foot
			else:
				last_foot = Vector3.INF
	var seconds: float = frames * 0.02
	var cycles: float = (actor._rig.phase - phase0) / TAU / seconds
	check(cycles * 2.0 > 1.5 and cycles * 2.0 < 2.1, "andando a 1,1 m/s: %.2f passos/s" % (cycles * 2.0))
	var body_travel := step * 0.02 * stance_samples
	check(stance_samples > 10 and stance_drift < body_travel * 0.35, "o pé de apoio não patina: andou %.3f no mundo para %.3f do corpo" % [stance_drift, body_travel])
	actor.free()


## #256: a perna mede o que mede: com o pé à frente o quadril desce o que o alcance pede (senão o IK esticava e o pé escorregava), e com o pé
## sob o corpo não desce.
func test_the_hip_drops_just_enough_for_the_planted_foot_to_reach_the_ground() -> void:
	check(BodyPose.drop_for_reach(0.0, 0.0) < 0.01, "pé sob o quadril: quase não precisa descer")
	var open_legs := BodyPose.drop_for_reach(0.2, 0.0)
	check(open_legs > 0.04 and open_legs < 0.09, "pé 0,20 à frente: o quadril desce %.3f" % open_legs)
	var leg := BodyPose.THIGH + BodyPose.SHIN
	var reach := sqrt(pow(BodyPose.HIP_Y - open_legs - BodyPose.ANKLE_H, 2.0) + 0.2 * 0.2)
	check(reach <= leg, "e com isso o pé está ao alcance da perna (%.3f <= %.3f)" % [reach, leg])
	check(BodyPose.drop_for_reach(0.2, 0.05) < open_legs, "no balanço o pé sobe e pede menos")
