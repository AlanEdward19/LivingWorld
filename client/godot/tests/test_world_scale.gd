extends "res://tests/test_base.gd"
## ADR-0052 (AD-197o): o motor diz a escala (1,5 m por célula, andar de 3 m, porta de 2 m) e o cliente desenha nela: o andar fica
## mais alto que a pessoa, a porta da altura de gente; sem a escala (servidor antigo), as medidas de antes.

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Grid := preload("res://Scenes/grid.gd")
const SCALE := {"cellMeters": 1.5, "floorMeters": 3.0, "doorMeters": 2.0, "doorWidthMeters": 0.9, "windowSillMeters": 1.0, "windowMeters": 1.0}


func _reset() -> void:
	Grid.use_scale({"cellMeters": 1.5, "floorMeters": 1.5, "doorMeters": 1.17, "doorWidthMeters": 0.84, "windowSillMeters": 0.6, "windowMeters": 0.54})


func test_the_engine_scale_makes_a_floor_taller_than_a_person_and_a_door_of_human_height() -> void:
	var fake = O.transport()
	fake.on("GET /world/info", {"status": 200, "body": {"name": "w", "periodId": "", "technologyAxes": [], "scale": SCALE}})
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	eq(Grid.floor_height(), 2.0, "3 m de andar em células de 1,5 m: duas unidades")
	var building: Node3D = app.world_view.building_nodes[4]
	var box: AABB = building.get_meta("box")
	var ana: Node3D = app.world_view.npc_nodes[7]
	var person := 0.0
	for mesh in ana.find_children("*", "MeshInstance3D", true, false):
		var top: float = (mesh as MeshInstance3D).get_aabb().end.y * mesh.global_transform.basis.get_scale().y + mesh.global_position.y - ana.global_position.y
		person = maxf(person, top)
	check(box.size.y - 0.9 >= 2.0 - 0.001, "o prédio tem ao menos um andar de 3 m (%.2f unidades)" % (box.size.y - 0.9))
	check(person > 0.9 and person < Grid.floor_height(), "a pessoa (%.2f) é mais baixa que um andar (%.2f)" % [person, Grid.floor_height()])
	var door: MeshInstance3D = building.find_child("Door", true, false)
	if door != null:
		check(is_equal_approx((door.mesh as BoxMesh).size.y, 2.0 / 1.5), "a porta tem 2 m")
	app.queue_free()
	_reset()


func test_without_the_engine_scale_the_old_measures_stay() -> void:
	_reset()
	Grid.use_scale({})
	eq(Grid.floor_height(), 1.0, "servidor sem escala: um andar de uma célula, como antes")
