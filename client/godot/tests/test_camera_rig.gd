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
	for i in 40: # #231: depois de bater o braço espera ARM_HOLD_S antes de voltar
		rig._process(0.016)
	var after_hold := rig.arm
	for i in 120:
		rig._process(0.016)
	eq([snappedf(blocked, 0.01), after_one_frame < rig.third_distance(), after_one_frame == blocked, after_hold > blocked, rig.arm], [1.75, true, true, true, rig.third_distance()])
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
	check(rig.lift > 0.3, "sobe um pouco para olhar o corpo (%s)" % rig.lift)
	check(rig.arm >= rig.min_arm() and cam.origin.distance_to(body_center) < rig.third_distance(), "chega perto do corpo")
	check(forward.dot(to_body) > cos(deg_to_rad(35.0)), "o corpo está no enquadramento (%s)" % forward.dot(to_body))
	for id in crossing:
		check(seen.has(id), "o que ainda cobre o corpo fica translúcido (prédio %s)" % id)
	rig.free()


func test_third_person_rises_over_a_low_wall_and_passes_by_the_shoulder_of_a_pillar() -> void:
	var low := [[1, AABB(Vector3(-3, 0, 2), Vector3(6, 2.2, 1))]] # a câmera, subindo, passa por cima
	var made_low := _third_rig(low, 0.0)
	check(made_low[0].lift > 0.4 and made_low[0].arm >= made_low[0].third_distance() - 0.01, "sobe e mantém o braço cheio (%s %s)" % [made_low[0].lift, made_low[0].arm])
	made_low[0].free()
	var pillar := [[2, AABB(Vector3(-0.2, 0, 1), Vector3(0.4, 9, 0.5))]] # coluna alta atrás: nem subindo passa; passa a ombro
	var made_pillar := _third_rig(pillar, 0.0)
	check(made_pillar[0].shoulder != 0.0 and made_pillar[0].arm >= made_pillar[0].third_distance() - 0.01, "passa a ombro (%s %s)" % [made_pillar[0].shoulder, made_pillar[0].arm])
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
	check(_camera_world(rig).origin.y > rig.await_lift(), "sem corpo a câmera espera no alto (%s), não dentro da casa" % _camera_world(rig).origin)
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
	check(is_equal_approx(rig.camera.near, rig.near_third()), "e a 3ª pessoa volta ao near curto")
	rig.free()
	var wall := [[1, AABB(Vector3(0, 0, 0), Vector3(4, 3.9, 4))]]
	var close := Vector3(-0.03, 1.07, 2.0) # fora, a 3 cm da parede
	var pushed: Vector3 = CameraRig.push_out(close, wall)
	check(not wall[0][1].has_point(pushed) and (0.0 - pushed.x) >= CameraRig.eye_clear() - 0.001, "recua até a folga (%s)" % pushed)
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
	check(cam.x <= -CameraRig.eye_clear() + 0.001, "o olho está a pelo menos a folga da parede (%s)" % cam.x)
	rig.free()


# --- AD-197s2: 3ª pessoa de RPG (atrás e acima do ombro, horizonte à vista), cômodo, anel ----------------------------------

## Quanto abaixo do centro da tela (graus) o centro do corpo aparece, e quanto a câmera olha para baixo (graus).
func _body_below_center_and_look_down(rig) -> Array:
	var cam := _camera_world(rig)
	var body_center: Vector3 = rig.target + Vector3(0, rig.eye_height() * 0.55, 0)
	var forward: Vector3 = -cam.basis.z
	var to_body := (body_center - cam.origin).normalized()
	var below := rad_to_deg(forward.angle_to(to_body))
	return [below, rad_to_deg(asin(clampf(-forward.y, -1.0, 1.0)))]


## Rua aberta, mouse no padrão: a inclinação é baixa (olha o horizonte) e o corpo fica no terço de baixo da tela (fov 70: terço = 13°).
func test_open_street_default_view_is_a_low_third_person_with_the_body_in_the_lower_third() -> void:
	var made := _body_rig([], Vector3(5, 0, 5), 0.8)
	var rig = made[0]
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	var seen := _body_below_center_and_look_down(rig)
	check(seen[1] <= 25.0 and seen[1] >= 5.0, "olha para baixo entre 5 e 25 graus (%s)" % seen[1])
	check(seen[0] >= 12.0, "o corpo está no terço de baixo da tela: %s graus abaixo do centro" % seen[0])
	eq([rig.lift, rig.shoulder], [0.0, 0.0], "rua aberta não sobe nem desloca")
	rig.free()


## Parede colada atrás do corpo: o enquadramento é curto mas continua raso (nada de olhar a prumo para a cabeça) e fica fora da parede.
func test_wall_right_behind_keeps_the_view_shallow_not_overhead() -> void:
	var boxes := [[3, AABB(Vector3(-5, 0, 0.5), Vector3(10, 5, 3))]] # parede a meio passo, atrás (a câmera fica em +z)
	var made := _body_rig(boxes, Vector3(0, 0, 0), 0.0)
	var rig = made[0]
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	var seen := _body_below_center_and_look_down(rig)
	check(seen[1] <= 55.0, "a vista curta não olha a prumo (%s graus; antes ~80)" % seen[1])
	check(not _camera_inside_any_shell(rig, boxes), "e fora da parede")
	rig.free()


## Casa de `size` células com paredes em anel (miolo livre size-2): [[id, caixa, casca, paredes]].
func _walled(size: int) -> Array:
	var walls: Array = []
	for x in size:
		for z in size:
			if x == 0 or z == 0 or x == size - 1 or z == size - 1:
				walls.append(AABB(Vector3(x, 0, z), Vector3(1, 2.0, 1)))
	var raw := AABB(Vector3.ZERO, Vector3(size, 2.9, size))
	return [[7, raw, AABB(Vector3(-0.2, 0, -0.2), Vector3(size + 0.4, 3.0, size + 0.4)), walls]]


## Cômodo largo (miolo 7x7): a câmera fica DENTRO dele, abaixo do teto, fora das paredes, olhar raso; o prédio abre.
func test_inside_a_roomy_house_the_camera_stays_in_the_room() -> void:
	var boxes := _walled(9)
	var made := _body_rig(boxes, Vector3(4.5, 0, 4.5), 0.0)
	var rig = made[0]
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	var cam := _camera_world(rig).origin
	check((boxes[0][1] as AABB).has_point(cam), "a câmera está dentro do prédio (%s)" % cam)
	var in_wall := false
	for wall in boxes[0][3]:
		in_wall = in_wall or (wall as AABB).has_point(cam)
	check(not in_wall, "e fora das paredes")
	check(cam.y < 2.0, "abaixo do teto do andar (%s)" % cam.y)
	check(rig.arm >= rig.room_arm() - 0.001, "com a distância de cômodo (%s)" % rig.arm)
	check(made[3].has(7) and made[2].has(7), "o prédio abre (paredes translúcidas, telhado some)")
	var seen := _body_below_center_and_look_down(rig)
	check(seen[1] <= 25.0, "olhar raso dentro do cômodo (%s)" % seen[1])
	rig.free()


## Casa pequena (miolo de 1 célula): não cabe câmera no cômodo; #222: corte de RPG, a câmera fica fora da casca, atrás e acima da
## parede do andar, olhando o corpo de cima (o telhado abre), na distância cheia em vez de a prumo sobre o telhado.
func test_inside_a_tiny_house_the_camera_goes_above_the_roof() -> void:
	var boxes := _walled(3)
	var made := _body_rig(boxes, Vector3(1.5, 0, 1.5), 0.0)
	var rig = made[0]
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	var cam := _camera_world(rig).origin
	check(not _camera_inside_any_shell(rig, boxes), "fora da casca (%s)" % cam)
	check(cam.y > Grid.floor_height(), "acima da parede do andar (%s)" % cam.y)
	check(-_camera_world(rig).basis.z.y < 0.0, "olhando o corpo de cima")
	rig.free()


## #228: corpo dentro de um prédio pequeno com paredes (um local de trabalho de 4x4): nunca a vista aérea; câmera na altura de gente.
func test_inside_a_small_walled_building_the_view_is_never_aerial() -> void:
	var boxes := _walled(4)
	for at in [Vector3(1.5, 0, 1.5), Vector3(1.1, 0, 2.5), Vector3(2.5, 0, 1.9), Vector3(1.0, 0, 1.0)]:
		for yaw in [0.0, PI / 2.0, PI, -PI / 2.0, 0.8]:
			var made := _body_rig(boxes, at, yaw)
			var rig = made[0]
			rig.pitch = deg_to_rad(-15.0)
			rig.set_mode(rig.THIRD)
			rig.follow(made[1])
			var forward: Vector3 = -_camera_world(rig).basis.z
			var label := "corpo %s giro %s" % [at, snappedf(yaw, 0.01)]
			check(forward.y > -sin(deg_to_rad(50.0)), "olha de lado, não de cima (%s: %s)" % [label, forward.y])
			check(rig.lift < Grid.floor_height(), "sobe menos que um andar (%s: %s)" % [label, rig.lift])
			check(made[3].has(7), "o prédio abre (%s)" % label)
			rig.free()


## A distância de cômodo vem dos metros do motor: outra escala de célula, outro valor em unidades.
func test_room_distance_follows_the_engine_scale() -> void:
	var before := Grid.cell_meters
	Grid.cell_meters = 1.5
	var a := CameraRig.room_arm()
	Grid.cell_meters = 0.5
	var b := CameraRig.room_arm()
	Grid.cell_meters = before
	check(absf(a - 1.0 / 1.5) < 0.001 and absf(b - 2.0) < 0.001, "1 m em unidades da grade (%s, %s)" % [a, b]) # #222


# --- AD-197s3: escala em metros, histerese cômodo/telhado, ombro antes de subir ------------------------------------------

## Os números da câmera são metros do motor: trocar a célula de 1,5 m para 0,5 m muda tudo em unidades, na mesma proporção.
func test_camera_distances_follow_the_engine_cell_size() -> void:
	var before := Grid.cell_meters
	Grid.cell_meters = 1.5
	var base := [CameraRig.third_distance(), CameraRig.min_arm(), CameraRig.arm_margin(), CameraRig.near_third()]
	eq([snappedf(base[0], 0.001), snappedf(base[1], 0.001), snappedf(base[2], 0.001), snappedf(base[3], 0.001)], [3.0, 0.5, 0.25, 0.05], "na escala de hoje as mesmas unidades de antes")
	Grid.cell_meters = 0.5
	eq([snappedf(CameraRig.third_distance(), 0.001), snappedf(CameraRig.min_arm(), 0.001), snappedf(CameraRig.arm_margin(), 0.001), snappedf(CameraRig.near_third(), 0.001)],
		[9.0, 1.5, 0.75, 0.15], "com célula de 0,5 m: o triplo")
	var made := _body_rig([], Vector3(5, 0, 5), 0.8)
	var rig = made[0]
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	check(is_equal_approx(rig.arm, 9.0) and is_equal_approx(rig.camera.near, 0.15), "o rig usa os metros do motor (braço %s, near %s)" % [rig.arm, rig.camera.near])
	rig.free()
	Grid.cell_meters = before


## Limite entre ficar no cômodo e subir ao telhado: perto dele a escolha não pisca (histerese de 15%).
func test_room_versus_roof_does_not_flicker_at_the_limit() -> void:
	var boxes := _walled(9)
	var need := CameraRig.room_arm()
	var stay := true
	var flips := 0
	var was_room := false
	var z_values: Array = [5.0, 5.5, 6.0, 6.2]
	for k in 10:
		z_values.append(8.0 - (need + CameraRig.arm_margin()) - 0.017) # logo abaixo do limite
		z_values.append(8.0 - (need + CameraRig.arm_margin()) + 0.017) # logo acima
	for z in z_values:
		var plan := CameraRig.plan_third(Vector3(4.5, 1.4, z), 0.0, 0.0, CameraRig.third_distance(), boxes, 1.0, was_room)
		var now := bool(plan.get("room", false))
		if now != was_room:
			flips += 1
		was_room = now
	check(flips <= 1, "uma entrada no cômodo e nenhuma troca por causa do tremor (%s trocas)" % flips)
	check(was_room, "e termina no cômodo")
	# de fora, no mesmo ponto limite, não entra (precisa de 15% a mais)
	var outside := CameraRig.plan_third(Vector3(4.5, 1.4, 8.0 - (need + CameraRig.arm_margin()) - 0.017), 0.0, 0.0, CameraRig.third_distance(), boxes, 1.0, false)
	check(not outside.has("room"), "quem está em cima não desce com folga mínima")


## Prédio colado atrás do corpo, na rua: passa a ombro (dobrando a esquina) antes de subir; sem subir, o olhar fica raso.
func test_building_right_behind_is_passed_by_the_shoulder_before_rising() -> void:
	var boxes := [[3, AABB(Vector3(-5, 0, 0.5), Vector3(5.4, 4, 3))]] # o prédio acaba em x = 0,4: à direita (+x) há rua livre
	var plan := CameraRig.plan_third(Vector3(0, 1.4, 0), 0.0, 0.0, CameraRig.third_distance(), boxes, 1.0)
	eq(plan.lift, 0.0, "não sobe")
	check(plan.shoulder > 0.0, "passa a ombro para o lado livre (%s)" % plan.shoulder)
	check(plan.arm >= CameraRig.third_distance() * CameraRig.COMFORT, "com caminho cheio (%s)" % plan.arm)
	var mirrored := [[3, AABB(Vector3(-0.4, 0, 0.5), Vector3(5.4, 4, 3))]] # espelhado: a rua livre fica à esquerda
	var other := CameraRig.plan_third(Vector3(0, 1.4, 0), 0.0, 0.0, CameraRig.third_distance(), mirrored, 1.0)
	check(other.shoulder < 0.0 and other.lift == 0.0, "e do lado oposto quando é o outro que está livre (%s)" % other.shoulder)


## AD-197s4: a ombro a câmera gira para mirar o corpo: o corpo fica a poucos graus do eixo de visão, nos dois lados.
func test_shoulder_framing_turns_to_aim_at_the_body() -> void:
	for side in [1.0, -1.0]:
		var boxes: Array = [[3, AABB(Vector3(-5, 0, 0.5) if side > 0.0 else Vector3(-0.4, 0, 0.5), Vector3(5.4, 4, 3))]]
		var made := _body_rig(boxes, Vector3(0, 0, 0), 0.0)
		var rig = made[0]
		rig.pitch = 0.0
		rig.set_mode(rig.THIRD)
		rig.follow(made[1])
		check(rig.shoulder * side > 0.0 and rig.lift == 0.0, "passou a ombro sem subir (lado %s: %s)" % [side, rig.shoulder])
		var cam := _camera_world(rig)
		var body_center: Vector3 = rig.target + Vector3(0, rig.eye_height() * 0.55, 0)
		var forward: Vector3 = -cam.basis.z
		var flat_forward := Vector2(forward.x, forward.z).normalized()
		var flat_to_body := Vector2(body_center.x - cam.origin.x, body_center.z - cam.origin.z).normalized()
		var yaw_off := rad_to_deg(flat_forward.angle_to(flat_to_body))
		check(absf(yaw_off) < 3.0, "o corpo está no eixo da tela (%s graus de lado)" % yaw_off)
		rig.free()


## #222: encostado na parede (olho sob o beiral do vizinho), olhando para longe dele, a câmera fica no braço cheio, não colada no corpo.
func test_body_against_a_wall_under_the_eave_keeps_the_full_arm() -> void:
	var box := AABB(Vector3(0, 0, 0), Vector3(4, 3, 4))
	var boxes := [[5, box, box.grow(0.3)]]
	var eye := Vector3(-0.1, 1.4, 2.0) # fora da caixa, dentro da casca (beiral)
	var plan := CameraRig.plan_third(eye, -PI / 2.0, 0.0, CameraRig.third_distance(), boxes, 1.0) # olhando para +x: as costas para longe da parede
	check(plan.arm >= CameraRig.third_distance() * CameraRig.COMFORT, "braço cheio (%s)" % plan.arm)


# --- #231: câmera de jogo (molas, colisão suave, ombro, transições) --------------------------------------------------------

## A mola crítica é exata: o mesmo alvo dá o mesmo resultado com 30, 60, 125 ou 20 FPS.
func test_spring_gives_the_same_result_at_any_frame_step() -> void:
	var results: Array = []
	for step in [1.0 / 125.0, 1.0 / 60.0, 1.0 / 30.0, 1.0 / 20.0]:
		var x := 0.0
		var v := 0.0
		for i in roundi(0.5 / step):
			var r: Array = CameraRig.spring(x, v, 1.0, 22.0, step)
			x = r[0]
			v = r[1]
		results.append(x)
	for r in results:
		check(absf(r - results[0]) < 0.0005, "mesmo resultado em qualquer passo (%s)" % [results])
	check(results[0] > 0.9 and results[0] < 1.0, "amortecida: perto do alvo sem passar dele (%s)" % results[0])


func test_follow_and_aim_are_smoothed_independent_of_fps_without_lagging_the_aim() -> void:
	var seen: Array = []
	for step in [1.0 / 120.0, 1.0 / 30.0]:
		var made := _body_rig([], Vector3(5, 0, 5), 0.0)
		var rig = made[0]
		rig.set_mode(rig.THIRD)
		rig.follow(made[1])
		made[1].position = Vector3(9, 0, 5)
		rig.yaw = 0.8
		for i in roundi(0.1 / step):
			rig._process(step)
		var aim_done: float = rig.view_yaw / 0.8
		check(aim_done > 0.95, "em 0,1 s a mira já cobriu 95%% do giro (%s)" % aim_done)
		check(rig.view_target.x > 5.0 and rig.view_target.x < 9.0, "segue o corpo com inércia, sem teleportar (%s)" % rig.view_target.x)
		seen.append([rig.view_target.x, rig.view_yaw])
		rig.free()
	check(absf(seen[0][0] - seen[1][0]) < 0.01 and absf(seen[0][1] - seen[1][1]) < 0.01, "FPS diferente, mesma câmera (%s)" % [seen])


## Parede que aparece de repente: o braço encurta rápido e nunca fica acima do livre + meia folga (não atravessa); some: espera e volta.
func test_arm_shortens_fast_never_passes_the_wall_and_returns_after_a_hold() -> void:
	var boxes: Array = []
	var made := _body_rig(boxes, Vector3(0, 0, 0), 0.0)
	var rig = made[0]
	rig.pitch = 0.0
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	rig._process(0.016)
	var full: float = rig.arm
	boxes.append([5, AABB(Vector3(-3, 0, 2), Vector3(6, 4, 1))])
	var free: float = CameraRig.free_arm(Vector3(0, rig.eye_height() + rig.shoulder_height(), 0), Vector3(0, 0, 1), full, boxes)
	var worst := 0.0
	var frames_to_close := -1
	for i in 30:
		rig._process(0.016)
		worst = maxf(worst, rig.arm - (free + CameraRig.arm_margin() * 0.5))
		if frames_to_close < 0 and rig.arm < free + 0.1:
			frames_to_close = i
	check(worst <= 0.0001, "o braço nunca passa do livre + meia folga (%s)" % worst)
	check(frames_to_close >= 0 and frames_to_close < 12, "encurta rápido (%s quadros)" % frames_to_close)
	check(rig.arm < full - 0.5, "ficou curto (%s de %s)" % [rig.arm, full])
	boxes.clear()
	var arm_hit: float = rig.arm
	for i in 10:
		rig._process(0.016)
	check(rig.arm == arm_hit, "espera um pouco antes de voltar (sem pulsar)")
	for i in 400:
		rig._process(0.016)
	check(is_equal_approx(rig.arm, full), "volta ao braço cheio (%s)" % rig.arm)
	rig.free()


## C: a 3ª↔1ª é animada, parte da posição que estava na tela e chega ao enquadramento da 1ª pessoa.
func test_toggle_person_blends_from_where_the_camera_was_to_the_eye() -> void:
	var made := _body_rig([], Vector3(5, 0, 5), 0.0)
	var rig = made[0]
	rig.pitch = 0.0
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	for i in 5:
		rig._process(0.016)
	var third_origin: Vector3 = _camera_world(rig).origin
	rig.toggle_person()
	check(rig.mode == rig.FIRST and rig.blending(), "em transição para a 1ª pessoa")
	check(_camera_world(rig).origin.distance_to(third_origin) < 0.3, "o primeiro quadro ainda está perto de onde a câmera estava")
	rig._process(0.1)
	var mid: Vector3 = _camera_world(rig).origin
	var eye: Vector3 = Vector3(5, rig.eye_height(), 5)
	check(mid.distance_to(eye) > 0.3 and mid.distance_to(third_origin) > 0.3, "no meio do caminho (%s)" % mid)
	for i in 30:
		rig._process(0.016)
	check(not rig.blending() and _camera_world(rig).origin.distance_to(eye) < 0.05, "chega ao olho da 1ª pessoa")
	rig.toggle_person()
	for i in 40:
		rig._process(0.016)
	check(rig.mode == rig.THIRD and not rig.blending() and _camera_world(rig).origin.distance_to(eye) > 2.0, "e volta para trás do corpo")
	rig.free()


## Q: o ombro troca de lado devagar (passa pelo centro), e perto de parede/subindo a câmera abre mão do ombro.
func test_shoulder_swap_slides_the_camera_to_the_other_side() -> void:
	var made := _body_rig([], Vector3(5, 0, 5), 0.0)
	var rig = made[0]
	rig.pitch = 0.0
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	rig._process(0.016)
	var right: float = rig._ots
	check(right > 0.0, "mira por cima do ombro direito (%s)" % right)
	rig.swap_shoulder()
	rig._process(0.05)
	check(rig._ots < right and rig._ots > 0.0, "a câmera desliza, sem saltar (%s)" % rig._ots)
	for i in 60:
		rig._process(0.016)
	check(absf(rig._ots + right) < 0.01, "chegou ao ombro esquerdo (%s)" % rig._ots)
	rig.free()
	var wall := [[3, AABB(Vector3(-0.6, 0, -2), Vector3(0.5, 4, 8))]] # parede colada ao lado esquerdo do corpo em x=0
	var near := _body_rig(wall, Vector3(0, 0, 0), 0.0)
	var rig2 = near[0]
	rig2.pitch = 0.0
	rig2.ots_side = -1.0
	rig2.set_mode(rig2.THIRD)
	rig2.follow(near[1])
	check(rig2._ots == 0.0, "parede ao lado: a câmera não vai para dentro dela (%s)" % rig2._ots)
	rig2.free()


func test_running_widens_the_fov_a_little_and_walking_does_not() -> void:
	var made := _body_rig([], Vector3(0, 0, 0), 0.0)
	var rig = made[0]
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	for i in 60:
		made[1].position.x += 1.0 / 60.0 * Grid.units(1.4)
		rig._process(1.0 / 60.0)
	check(is_equal_approx(rig.camera.fov, rig.BASE_FOV), "andando: fov normal (%s)" % rig.camera.fov)
	for i in 120:
		made[1].position.x += 1.0 / 60.0 * Grid.units(5.0)
		rig._process(1.0 / 60.0)
	check(rig.camera.fov > rig.BASE_FOV + 3.0 and rig.camera.fov <= rig.BASE_FOV + rig.RUN_FOV_BONUS + 0.01, "correndo: fov maior (%s)" % rig.camera.fov)
	rig.free()


## Chegar em outro lugar (cidade↔mundo): a câmera desce de uma vista recuada e alta, não corta seco.
func test_arriving_somewhere_far_away_reveals_instead_of_cutting() -> void:
	var made := _body_rig([], Vector3(5, 0, 5), 0.0)
	var rig = made[0]
	rig.pitch = 0.0
	rig.set_mode(rig.THIRD)
	rig.follow(made[1])
	for i in 3:
		rig._process(0.016)
	var other := Node3D.new()
	rig.add_child(other)
	other.position = Vector3(500, 0, 500)
	rig.follow(other)
	check(rig.blending(), "chegada animada")
	var start_y: float = _camera_world(rig).origin.y
	for i in 70:
		rig._process(0.016)
	check(not rig.blending() and _camera_world(rig).origin.y < start_y - 3.0, "desceu até o enquadramento (%s -> %s)" % [start_y, _camera_world(rig).origin.y])
	rig.free()


## #227: a casa em foco (painéis finos, sem casca de prédio): a câmera de 3ª pessoa fica dentro, em altura de gente, nunca no alto.
func _interior_entry(size: float) -> Array:
	var walls: Array = []
	var thick := 0.05
	walls.append(AABB(Vector3(0, 0, 0), Vector3(size, 2.0, thick)))
	walls.append(AABB(Vector3(0, 0, size - thick), Vector3(size, 2.0, thick)))
	walls.append(AABB(Vector3(0, 0, 0), Vector3(thick, 2.0, size)))
	walls.append(AABB(Vector3(size - thick, 0, 0), Vector3(thick, 2.0, size)))
	var box := AABB(Vector3.ZERO, Vector3(size, 2.9, size))
	return [-1, box, box, walls, true]


func test_interior_entry_is_only_found_with_the_eye_inside() -> void:
	var entries := [_interior_entry(4.0)]
	check(not CameraRig.interior_entry(Vector3(2, 1.4, 2), entries).is_empty(), "olho dentro")
	check(CameraRig.interior_entry(Vector3(9, 1.4, 2), entries).is_empty(), "olho fora")


