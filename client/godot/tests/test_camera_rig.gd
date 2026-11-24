extends "res://tests/test_base.gd"

const CameraRig := preload("res://Scenes/camera_rig.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Grid := preload("res://Scenes/grid.gd")


func test_navigation_sends_nothing() -> void:
	var transport := FakeTransport.new()
	var rig := CameraRig.new() # o rig nem recebe o transporte: navegar não tem como falar com o servidor
	var requests: Array = []
	rig.scope_requested.connect(func(k, r): requests.append([k, r]))
	var before: Vector3 = rig.target

	rig.pan(Vector2(40, -20))
	rig.orbit(0.5)
	rig.zoom_by(0.8)
	rig.zoom_by(1.2)

	eq(transport.calls, [], "nenhuma chamada ao transporte")
	eq(requests, [], "nenhum pedido de escopo dentro da faixa de mundo")
	check(rig.target != before, "o ponto de vista se moveu")
	check(not is_equal_approx(rig.yaw, deg_to_rad(45.0)), "o giro mudou")
	rig.free()


func test_zoom_threshold_requests_city_scope() -> void:
	var rig := CameraRig.new()
	var requests: Array = []
	rig.scope_requested.connect(func(k, r): requests.append([k, r]))
	rig.city_at = func(_p: Vector3) -> String: return "city-a"

	while rig.zoom_size > rig.city_enter_size + 0.5:
		rig.zoom_by(0.9)
	eq(requests, [], "ainda acima do limiar")
	rig.zoom_by(0.9)
	rig.zoom_by(0.9)

	eq(requests, [["City", "city-a"]], "um único pedido ao cruzar o limiar")
	rig.set_scope("City")
	while rig.zoom_size < rig.city_exit_size - 0.5:
		rig.zoom_by(1.1)
	rig.zoom_by(1.1)
	rig.zoom_by(1.1)
	eq(requests.back(), ["World", ""], "afastar volta ao mundo")
	rig.free()


func test_follow_tracks_until_released() -> void:
	var rig := CameraRig.new()
	var actor := NpcActor.new()
	actor.setup(1, Vector2i(2, 2), 5, ActionLabels.new())
	rig.follow(actor)

	actor.position = Vector3(10.5, 0, 6.5)
	for i in 120:
		rig._process(0.05)
	check(rig.target.distance_to(actor.position) < 0.05, "a câmera chegou ao NPC (%s)" % rig.target)

	rig.release()
	var parked: Vector3 = rig.target
	actor.position = Vector3(20, 0, 20)
	for i in 20:
		rig._process(0.05)
	eq(rig.target, parked, "solto: a câmera não acompanha mais")
	rig.free()
	actor.free()


## AD-197p: braço de mola da 3ª pessoa: a câmera para antes da parede (qualquer tamanho de prédio) e volta devagar quando abre.
func test_third_person_camera_stops_before_a_wall_and_returns_smoothly() -> void:
	var eye := Vector3(0, 1.6, 0)
	var back := Vector3(0, 0, 1)
	var wall := [[5, AABB(Vector3(-3, 0, 2), Vector3(6, 4, 1))]]
	var small := [[6, AABB(Vector3(-0.2, 0, 1), Vector3(0.4, 0.5, 0.4))]] # baixo demais: a linha do olho passa por cima
	var inside := [[7, AABB(Vector3(-2, 0, -2), Vector3(4, 3, 4))]] # o corpo dentro de casa: a casa não empurra a câmera

	eq([snappedf(CameraRig.free_arm(eye, back, 4.5, wall), 0.01), CameraRig.free_arm(eye, back, 4.5, []),
		CameraRig.free_arm(eye, back, 4.5, small), CameraRig.free_arm(eye, back, 4.5, inside)], [1.75, 4.5, 4.5, 4.5])

	var rig := CameraRig.new()
	var body := Node3D.new()
	rig.add_child(body)
	var boxes := wall.duplicate()
	rig.blockers = func() -> Array: return boxes
	rig.set_mode(rig.THIRD)
	rig.pitch = 0.0
	rig.yaw = 0.0
	rig.follow(body)
	rig._process(0.016)
	var blocked := rig.arm
	boxes.clear()
	rig._process(0.016)
	var after_one_frame := rig.arm
	for i in 120:
		rig._process(0.016)
	eq([snappedf(blocked, 0.01), after_one_frame < rig.THIRD_DISTANCE, after_one_frame > blocked, rig.arm], [1.75, true, true, rig.THIRD_DISTANCE])
	rig.free()


func test_what_still_covers_the_body_is_listed_for_translucency() -> void:
	var roof := [[9, AABB(Vector3(-1, 2, 1), Vector3(2, 1, 1))], [10, AABB(Vector3(10, 0, 10), Vector3(1, 1, 1))]]
	eq(CameraRig.covering(Vector3(0, 3.5, 3), Vector3(0, 0.9, 0), roof), {9: true})


## Câmera aplicada no mundo (rig * pivô * câmera): fora da árvore não há global_transform.
func _camera_world(rig) -> Transform3D:
	return rig.transform * rig._pivot.transform * rig.camera.transform


func _third_rig(boxes: Array, yaw: float, scale: float = 1.0) -> Array:
	var rig := CameraRig.new()
	var body := Node3D.new()
	body.scale = Vector3.ONE * scale
	rig.add_child(body)
	var seen := {}
	rig.see_through.connect(func(ids: Dictionary): seen.assign(ids))
	rig.blockers = func() -> Array: return boxes
	rig.set_mode(rig.THIRD)
	rig.pitch = 0.0
	rig.yaw = yaw
	rig.follow(body)
	for i in 240:
		rig._process(0.016)
	return [rig, seen, body]


## AD-197r: parede alta atrás, parede alta na frente e telhado alto: a câmera sobe e chega perto, olha o corpo, não fica dentro de
## nenhuma caixa, e o que ainda cobre a linha até o corpo fica na lista de translucidez. O corpo é sempre visto.
func test_narrow_street_between_tall_houses_keeps_the_body_in_view() -> void:
	var boxes := [
		[1, AABB(Vector3(0.9, 0, -4), Vector3(2, 8, 8))], # parede atrás da câmera (o olhar vai para -x, a câmera fica em +x)
		[2, AABB(Vector3(-2.9, 0, -4), Vector3(2, 8, 8))], # parede em frente
		[3, AABB(Vector3(-3, 7, -4), Vector3(6, 2, 8))], # telhado alto cobrindo a rua
	]
	var made := _third_rig(boxes, PI / 2.0)
	var rig = made[0]
	var seen: Dictionary = made[1]
	var cam := _camera_world(rig)
	var body_center: Vector3 = rig.target + Vector3(0, rig.eye_height() * 0.55, 0)
	var to_body := (body_center - cam.origin).normalized()
	var forward := -cam.basis.z
	var inside := false
	var crossing := {}
	for entry in boxes:
		inside = inside or (entry[1] as AABB).has_point(cam.origin)
		if (entry[1] as AABB).intersects_segment(cam.origin, body_center) != null:
			crossing[entry[0]] = true
	check(not inside, "a câmera não termina dentro de uma parede ou telhado (%s)" % cam.origin)
	check(rig.lift > 0.5, "sobe para olhar o corpo de cima (%s)" % rig.lift)
	check(rig.arm >= rig.MIN_ARM and cam.origin.distance_to(body_center) < rig.THIRD_DISTANCE, "chega perto do corpo")
	check(forward.dot(to_body) > cos(deg_to_rad(35.0)), "o corpo está no enquadramento (%s)" % forward.dot(to_body))
	for id in crossing:
		check(seen.has(id), "o que ainda cobre o corpo fica translúcido (prédio %s)" % id)
	rig.free()


func test_third_person_rises_over_a_low_wall_and_passes_by_the_shoulder_of_a_pillar() -> void:
	var low := [[1, AABB(Vector3(-3, 0, 2), Vector3(6, 2.2, 1))]] # a câmera, subindo, passa por cima
	var made_low := _third_rig(low, 0.0)
	check(made_low[0].lift > 0.4 and made_low[0].arm >= made_low[0].THIRD_DISTANCE - 0.01, "sobe e mantém o braço cheio (%s %s)" % [made_low[0].lift, made_low[0].arm])
	made_low[0].free()
	var pillar := [[2, AABB(Vector3(-0.2, 0, 1), Vector3(0.4, 9, 0.5))]] # coluna alta atrás: nem subindo passa; passa a ombro
	var made_pillar := _third_rig(pillar, 0.0)
	check(made_pillar[0].shoulder != 0.0 and made_pillar[0].arm >= made_pillar[0].THIRD_DISTANCE - 0.01, "passa a ombro (%s %s)" % [made_pillar[0].shoulder, made_pillar[0].arm])
	made_pillar[0].free()
	var open := _third_rig([], 0.0)
	eq([open[0].lift, open[0].shoulder], [0.0, 0.0])
	open[0].free()


## AD-197r: a altura dos olhos vem da altura do corpo na escala do motor, não de constante.
func test_eye_height_comes_from_the_body_height_in_engine_scale() -> void:
	var before := Grid.cell_meters
	Grid.cell_meters = 1.5
	var adult: float = CameraRig.eye_height_for(170.0)
	var child: float = CameraRig.eye_height_for(125.0)
	check(absf(adult - 1.6 / 1.5) < 0.02, "adulto mediano: olhos a ~1,6 m = ~1,07 unidade (%s)" % adult)
	check(child < adult * 0.8 and absf(child - 1.175 / 1.5) < 0.02, "criança de 1,25 m: olhos mais baixos (%s)" % child)
	Grid.cell_meters = 1.0
	check(CameraRig.eye_height_for(170.0) > adult, "a escala do motor manda: célula menor, mais unidades")
	Grid.cell_meters = before

	var made := _third_rig([], 0.0, 0.735) # corpo de criança (125/170)
	var rig = made[0]
	check(absf(rig.eye_height() - child) < 0.01, "o rig lê a altura do corpo que segue (%s)" % rig.eye_height())
	rig.set_mode(rig.FIRST)
	eq(snappedf(rig._pivot.position.y, 0.001), snappedf(child, 0.001))
	check(rig._pivot.position.y < adult, "na 1ª pessoa os olhos da criança estão abaixo dos do adulto")
	rig.set_mode(rig.THIRD)
	check(rig._pivot.position.y > rig.eye_height() and rig._pivot.position.y < rig.eye_height() * 1.5, "a 3ª mira logo acima do ombro, coerente com o corpo")
	rig.free()


# --- AD-197s: a 3ª pessoa nasce com o corpo à vista; a 1ª pessoa não atravessa parede -----------------------------------

## Casa de 5x5 células, andar de 3 unidades (+0,9 de telhado na caixa); a casca soma beiral e cumeeira.
func _house(id: int, at: Vector3, floors: int = 1) -> Array:
	var raw := AABB(at, Vector3(5, 3.0 * floors + 0.9, 5))
	return [id, raw, AABB(raw.position - Vector3(0.2, 0, 0.2), raw.size + Vector3(0.4, 0.2, 0.4))]


func _camera_inside_any_shell(rig, boxes: Array) -> bool:
	var spot: Vector3 = _camera_world(rig).origin
	for entry in boxes:
		if CameraRig.shell_of(entry).has_point(spot):
			return true
	return false


func _body_rig(boxes: Array, body_at: Vector3, yaw: float) -> Array:
	var rig := CameraRig.new()
	var body := Node3D.new()
	rig.add_child(body)
	body.position = body_at
	var seen := {}
	var open := {}
	rig.see_through.connect(func(ids: Dictionary): seen.assign(ids))
	rig.opened.connect(func(ids: Dictionary): open.assign(ids))
	rig.blockers = func() -> Array: return boxes
	rig.pitch = deg_to_rad(-15.0)
	rig.yaw = yaw
	return [rig, body, seen, open]


## Causa 1: com o corpo dentro de casa (olho dentro da caixa) o braço terminava dentro das paredes ou do telhado. Agora a câmera
## sobe para fora da casca, olha o corpo de cima, e o prédio abre (telhado some), em qualquer direção de olhar e em qualquer andar.
func test_body_inside_a_house_is_seen_from_above_the_roof_in_every_direction_and_floor() -> void:
	for floors in [1, 3]:
		for body_cell in [Vector3(2.5, 0.0, 2.5), Vector3(0.5, 0.0, 0.5), Vector3(4.5, 0.0, 2.5)]:
			for floor_up in range(floors):
				var boxes := [_house(7, Vector3.ZERO, floors), _house(8, Vector3(6, 0, 0), floors)]
				var at: Vector3 = body_cell + Vector3(0, floor_up * 3.0, 0)
				for yaw in [0.0, PI / 2.0, PI, -PI / 2.0, 0.7]:
					var made := _body_rig(boxes, at, yaw)
					var rig = made[0]
					rig.set_mode(rig.THIRD)
					rig.follow(made[1])
					var label := "andares %s, corpo %s, giro %s" % [floors, at, snappedf(yaw, 0.01)]
					check(not _camera_inside_any_shell(rig, boxes), "a câmera fica fora de toda casca (%s; %s)" % [label, _camera_world(rig).origin])
					var cam := _camera_world(rig)
					var body_center: Vector3 = rig.target + Vector3(0, rig.eye_height() * 0.55, 0)
					check((-cam.basis.z).dot((body_center - cam.origin).normalized()) > cos(deg_to_rad(20.0)), "o corpo está no centro do enquadramento (%s)" % label)
					check(made[3].has(7), "o prédio do corpo abre (%s)" % label)
					check(made[2].has(7), "e fica na translucidez (%s)" % label)
					rig.free()


## A vertical nunca é passada: o enquadramento de cima olha o corpo sem virar a câmera de cabeça para baixo.
func test_view_from_above_never_passes_the_vertical() -> void:
	var boxes := [_house(7, Vector3.ZERO, 3)]
	for pitch in [-70.0, -15.0, 0.0, 35.0]:
		var made := _body_rig(boxes, Vector3(2.5, 0.0, 2.5), 0.3)
		var rig = made[0]
		rig.pitch = deg_to_rad(pitch)
		rig.set_mode(rig.THIRD)
		rig.follow(made[1])
		var forward: Vector3 = -_camera_world(rig).basis.z
		check(forward.y > -1.0 and forward.y < 0.0 and forward.y >= -sin(deg_to_rad(84.0)) - 0.001, "olha para baixo sem passar da vertical (pitch %s: %s)" % [pitch, forward.y])
		rig.free()


## Causa 2: o corpo da pessoa nova só chega ao desenho depois: o rig ficava num ponto sem corpo (dentro do prédio) e só ajustava
## ao correr o tempo. Agora `follow` já vai ao enquadramento: no quadro em que o corpo chega, nada de câmera dentro de parede.
func test_body_that_arrives_later_gets_the_framing_on_the_very_first_frame() -> void:
	var boxes := [_house(7, Vector3.ZERO)]
	var made := _body_rig(boxes, Vector3(2.5, 0.0, 2.5), 0.0)
	var rig = made[0]
	rig.target = Vector3(2.5, 0.0, 4.5) # o ponto resolvido da ficha: dentro da casa, sem corpo
	rig.set_mode(rig.THIRD)
	rig.follow(null)
	rig._process(0.016) # quadros sem corpo não movem nem quebram nada
	check(_camera_world(rig).origin.y > rig.AWAIT_LIFT, "sem corpo a câmera espera no alto (%s), não dentro da casa" % _camera_world(rig).origin)
	rig.follow(made[1]) # o corpo chegou no delta
	check(not _camera_inside_any_shell(rig, boxes), "no mesmo quadro em que o corpo chega a câmera já está fora das paredes (%s)" % _camera_world(rig).origin)
	check(made[3].has(7) and made[2].has(7), "e o prédio já abriu")
	rig.free()


## Troca de modo com o corpo já seguido (C: 1ª -> 3ª) também vai direto, sem passar pela posição antiga.
func test_switching_to_third_person_inside_a_house_goes_straight_to_the_framing() -> void:
	var boxes := [_house(7, Vector3.ZERO)]
	var made := _body_rig(boxes, Vector3(2.5, 0.0, 2.5), 1.0)
	var rig = made[0]
	rig.set_mode(rig.FIRST)
	rig.follow(made[1])
	rig.set_mode(rig.THIRD)
	check(not _camera_inside_any_shell(rig, boxes), "ao voltar para a 3ª pessoa a câmera sai das paredes de uma vez")
	rig.free()


## Causa 3: o olho do corpo fora de casa mas a câmera, 3 atrás, caía no beiral do telhado vizinho (a caixa não o incluía): a
## casca do prédio (beiral, cumeeira) é o que vale. Corpo na rua, beiral e telhado alto atrás dele.
func test_camera_keeps_out_of_the_eaves_and_the_ridge_of_the_neighbour() -> void:
	var raw := AABB(Vector3(0, 0, 0), Vector3(4, 2.4, 4))
	var shell := AABB(Vector3(-0.2, 0, -0.2), Vector3(4.4, 3.6, 4.4)) # cumeeira de um telhado alto (templo: pitch 1,4), acima da caixa
	var boxes := [[5, raw, shell]]
	var made := _body_rig(boxes, Vector3(-1.5, 0.0, 2.0), PI / 2.0) # a câmera fica em +x (para a casa) e sobe: passaria por cima da caixa, não da cumeeira
	var rig = made[0]
	rig.pitch = 0.0
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	var cam := _camera_world(rig).origin
	check(not shell.has_point(cam), "fora da casca (%s)" % cam)
	rig.free()


func test_roof_lift_and_containing_only_count_the_box_the_eye_is_in() -> void:
	var boxes := [_house(1, Vector3.ZERO), _house(2, Vector3(10, 0, 0))]
	check(CameraRig.roof_lift(Vector3(2.5, 1.4, 2.5), boxes) > 2.0, "dentro: sobe acima da casca")
	eq(CameraRig.roof_lift(Vector3(7.5, 1.4, 2.5), boxes), 0.0, "fora de qualquer caixa: nada")
	eq(CameraRig.containing(Vector3(2.5, 1.0, 2.5), boxes), {1: true})
	eq(CameraRig.containing(Vector3(-0.1, 1.0, 2.5), boxes), {})


## 1ª pessoa: o corte do plano próximo cobre a cabeça, e o olho do lado de fora não encosta na parede.
func test_first_person_eye_never_touches_a_wall() -> void:
	var rig := CameraRig.new()
	rig.set_mode(rig.FIRST)
	check(rig.camera.near >= 0.1 and rig.camera.near <= 0.2, "near da 1ª pessoa cobre a cabeça (%s)" % rig.camera.near)
	rig.set_mode(rig.THIRD)
	check(is_equal_approx(rig.camera.near, rig.NEAR_THIRD), "e a 3ª pessoa volta ao near curto")
	rig.free()
	var wall := [[1, AABB(Vector3(0, 0, 0), Vector3(4, 3.9, 4))]]
	var close := Vector3(-0.03, 1.07, 2.0) # fora, a 3 cm da parede
	var pushed: Vector3 = CameraRig.push_out(close, wall)
	check(not wall[0][1].has_point(pushed) and (0.0 - pushed.x) >= CameraRig.EYE_CLEAR - 0.001, "recua até a folga (%s)" % pushed)
	eq(CameraRig.push_out(Vector3(-1.0, 1.07, 2.0), wall), Vector3(-1.0, 1.07, 2.0), "longe da parede: não mexe")
	eq(CameraRig.push_out(Vector3(2.0, 1.07, 2.0), wall), Vector3(2.0, 1.07, 2.0), "dentro do prédio (o corpo mora lá): não mexe")
	eq(CameraRig.push_out(Vector3(-0.03, 5.0, 2.0), wall), Vector3(-0.03, 5.0, 2.0), "acima do telhado: não mexe")


func test_first_person_rig_applies_the_eye_push_next_to_a_wall() -> void:
	var boxes := [[1, AABB(Vector3(0, 0, 0), Vector3(4, 3.9, 4))]]
	var made := _body_rig(boxes, Vector3(-0.03, 0.0, 2.0), 0.0)
	var rig = made[0]
	rig.set_mode(rig.FIRST)
	rig.follow(made[1])
	var cam := _camera_world(rig).origin
	check(cam.x <= -CameraRig.EYE_CLEAR + 0.001, "o olho está a pelo menos a folga da parede (%s)" % cam.x)
	rig.free()
