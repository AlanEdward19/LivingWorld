extends "res://tests/test_base.gd"

const CameraRig := preload("res://Scenes/camera_rig.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")


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
	eq([snappedf(blocked, 0.01), after_one_frame < 4.5, after_one_frame > blocked, rig.arm], [1.75, true, true, 4.5])
	rig.free()


func test_what_still_covers_the_body_is_listed_for_translucency() -> void:
	var roof := [[9, AABB(Vector3(-1, 2, 1), Vector3(2, 1, 1))], [10, AABB(Vector3(10, 0, 10), Vector3(1, 1, 1))]]
	eq(CameraRig.covering(Vector3(0, 3.5, 3), Vector3(0, 0.9, 0), roof), {9: true})
