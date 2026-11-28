extends Node3D
## #254: galeria de retratos (só captura; nada vai para o jogo). Monta NPCs sintéticos com o `observable` que o motor manda e
## fotografa: retrato de perto (homem, mulher, criança, idoso), grupo de longe e movimento (andar, correr, trabalhar, comer).
## Uso (janela de verdade, sem API): godot --path client/godot res://tools/portraits.tscn -- out=<pasta> tag=<antes|depois> era=<medieval|modern>

const NpcActor := preload("res://Scenes/npc_actor.gd")
const Look := preload("res://Scenes/npc_look.gd")

var _out := "/tmp"
var _tag := "x"
var _actors: Array = []


static func obs_of(i: int, sex: String, age: int, prof: String, dominant: String = "") -> Dictionary:
	return {"name": "P%d" % i, "sex": sex, "age": age, "stage": "Child" if age < 13 else ("Elder" if age >= 60 else "Adult"),
		"profession_id": i % 6, "profession_name": prof, "height_cm": 75.0 if age < 3 else (125.0 if age < 13 else (158.0 if sex == "Female" else 172.0)),
		"growth": 1.0, "pregnancy": 0.0, "ill": false, "symptomatic": false, "bedridden": false, "emotions": {}, "dominant": dominant,
		"mourning": false, "carried": [], "held": {}}


func _ready() -> void:
	var era := "medieval"
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("out="):
			_out = arg.substr(4)
		elif arg.begins_with("tag="):
			_tag = arg.substr(4)
		elif arg.begins_with("era="):
			era = arg.substr(4)
	var look_script: Object = Look
	if look_script.has_method("set_era"): # antes do #254 não existe
		look_script.call("set_era", era)
	DirAccess.make_dir_recursive_absolute(_out)
	_stage()
	await _run(era)
	get_tree().quit()


func _stage() -> void:
	var env := WorldEnvironment.new()
	env.environment = Environment.new()
	env.environment.background_mode = Environment.BG_COLOR
	env.environment.background_color = Color("9cc3e6")
	env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.environment.ambient_light_color = Color("c9d6e8")
	env.environment.ambient_light_energy = 0.6
	add_child(env)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, 35, 0)
	sun.shadow_enabled = true
	sun.light_energy = 1.1
	add_child(sun)
	var ground := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(60, 60)
	ground.mesh = plane
	var material := StandardMaterial3D.new()
	material.albedo_color = Color("7a9a5a")
	ground.material_override = material
	add_child(ground)


func _spawn(i: int, at: Vector3, obs: Dictionary, near: bool, yaw: float = 0.0) -> Node3D:
	var actor := NpcActor.new()
	add_child(actor)
	actor.setup(1000 + i, Vector2i(0, 0), 5, null, {})
	actor.position = at
	actor.rotation.y = yaw
	actor.apply_observable(obs)
	actor.set_detail(near)
	actor.set_far(not near)
	actor.animate(0.05)
	_actors.append(actor)
	return actor


func _clear() -> void:
	for actor in _actors:
		actor.queue_free()
	_actors.clear()
	await get_tree().process_frame


func _camera(at: Vector3, look_at: Vector3, fov: float = 30.0) -> Camera3D:
	var cam := get_node_or_null("Cam") as Camera3D
	if cam == null:
		cam = Camera3D.new()
		cam.name = "Cam"
		add_child(cam)
	cam.fov = fov
	cam.position = at
	cam.look_at(look_at)
	cam.current = true
	return cam


func _shot(name: String) -> void:
	for _i in 4:
		await get_tree().process_frame
	await RenderingServer.frame_post_draw
	get_viewport().get_texture().get_image().save_png("%s/%s-%s.png" % [_out, name, _tag])
	print("shot ", name)


func _tick(seconds: float) -> void:
	var frames := int(seconds * 60.0)
	for _i in frames:
		for actor in _actors:
			actor.advance(1.0 / 60.0)
		await get_tree().process_frame


func _run(era: String) -> void:
	var cast := [
		["Lavrador", "Male", 32, ""], ["Padeira", "Female", 28, "joy"], ["Menina", "Female", 7, ""],
		["Menino", "Male", 9, "fear"], ["Ferreiro", "Male", 47, "anger"], ["Anciã", "Female", 74, "grief"], ["Ancião", "Male", 71, ""],
	]
	for i in cast.size():
		var c: Array = cast[i]
		_spawn(i, Vector3(-1.8 + i * 0.6, 0, 0), obs_of(i, c[1], c[2], c[0], c[3]), true, 0.0)
	await _tick(0.3)
	_camera(Vector3(0, 0.62, 3.2), Vector3(0, 0.55, 0), 32.0)
	await _shot("fila-%s" % era)
	_camera(Vector3(-1.1, 0.95, 1.6), Vector3(-1.1, 0.88, 0), 24.0)
	await _shot("rostos-esq-%s" % era)
	_camera(Vector3(0.9, 0.95, 1.6), Vector3(0.9, 0.88, 0), 24.0)
	await _shot("rostos-dir-%s" % era)
	_camera(Vector3(-2.6, 0.7, 1.5), Vector3(-1.8, 0.6, 0), 40.0)
	await _shot("tres-quartos-%s" % era)
	await _clear()
	for i in 36:
		var c: Array = cast[i % cast.size()]
		var at := Vector3(-6 + (i % 9) * 1.4, 0, -3 + (i / 9) * 1.6)
		_spawn(i, at, obs_of(i, c[1], c[2], c[0]), false, float(i) * 0.7)
	await _tick(0.3)
	_camera(Vector3(0, 7.0, 11.0), Vector3(0, 0, -0.5), 35.0)
	await _shot("longe-%s" % era)
	_camera(Vector3(0, 3.2, 5.5), Vector3(0, 0.4, -1.0), 35.0)
	await _shot("meio-%s" % era)
	await _clear()
	var poses := [[4, false], [4, true], [2, false], [0, false], [3, false], [5, false]]
	for i in poses.size():
		var c: Array = cast[i % cast.size()]
		var actor := _spawn(i, Vector3(-1.5 + i * 0.6, 0, 0.0), obs_of(i, c[1], c[2], c[0]), true, 0.0)
		actor.set_action(int(poses[i][0]), null)
		actor.running = bool(poses[i][1])
	for frame in 3:
		for i in 2:
			var a: Node3D = _actors[i]
			a.move_to(Vector2i(roundi(a.position.x), roundi(a.position.z + 3)), 3.0)
		await _tick(0.55)
		_camera(Vector3(-0.7, 0.8, 3.0), Vector3(-0.7, 0.55, 0), 38.0)
		await _shot("movimento%d-%s" % [frame, era])
