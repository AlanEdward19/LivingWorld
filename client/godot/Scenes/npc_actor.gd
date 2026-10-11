extends Node3D
## Um NPC em 3D: corpo procedural (placeholder estilizado) com animações por pose.
## Movimento e pose seguem o que o servidor projeta; nada aqui decide comportamento.

const Grid := preload("res://Scenes/grid.gd")
const Anim := preload("res://Scenes/animation_catalog.gd")

var npc_id := 0
var cell := Vector2i.ZERO
var action := -1
var pose := Anim.NEUTRAL_POSE
var label_text := ""

var _from := Vector3.ZERO
var _to := Vector3.ZERO
var _t := 1.0
var _duration := 1.0
var _clock := 0.0
var _body: MeshInstance3D
var _head: MeshInstance3D
var _arm: MeshInstance3D
var _label: Label3D
var _material: StandardMaterial3D
var _selected := false


func setup(id: int, p_cell: Vector2i, p_action: int, labels) -> void:
	npc_id = id
	cell = p_cell
	position = Grid.to_world(cell)
	_from = position
	_to = position
	_build()
	set_action(p_action, labels)


func _build() -> void:
	var hue := fmod(float(npc_id) * 0.61803398875, 1.0)
	_material = StandardMaterial3D.new()
	_material.albedo_color = Color.from_hsv(hue, 0.55, 0.85)
	_material.emission_enabled = true
	_material.emission = Color.BLACK
	_body = MeshInstance3D.new()
	var capsule := CapsuleMesh.new()
	capsule.radius = 0.2
	capsule.height = 0.7
	_body.mesh = capsule
	_body.material_override = _material
	_body.position.y = 0.35
	add_child(_body)
	_head = MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.15
	sphere.height = 0.3
	_head.mesh = sphere
	var skin := StandardMaterial3D.new()
	skin.albedo_color = Color(0.93, 0.78, 0.65)
	_head.material_override = skin
	_head.position.y = 0.85
	add_child(_head)
	_arm = MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3(0.08, 0.3, 0.08)
	_arm.mesh = box
	_arm.material_override = _material
	_arm.position = Vector3(0.25, 0.5, 0)
	add_child(_arm)
	_label = Label3D.new()
	_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	_label.position.y = 1.25
	_label.pixel_size = 0.008
	_label.font_size = 28
	_label.no_depth_test = true
	_label.visible = false
	add_child(_label)
	var body := StaticBody3D.new()
	body.set_meta("npc_id", npc_id)
	var shape := CollisionShape3D.new()
	var cylinder := CylinderShape3D.new()
	cylinder.radius = 0.35
	cylinder.height = 1.2
	shape.shape = cylinder
	shape.position.y = 0.6
	body.add_child(shape)
	add_child(body)


func set_action(new_action: int, labels) -> void:
	action = new_action
	pose = Anim.pose_for(new_action)
	label_text = labels.label_for(new_action) if labels != null else ""
	_label.text = label_text


func move_to(new_cell: Vector2i, duration: float) -> void:
	cell = new_cell
	_from = position
	_to = Grid.to_world(new_cell)
	_duration = maxf(duration, 0.001)
	_t = 0.0


func is_moving() -> bool:
	return _t < 1.0


func set_selected(selected: bool) -> void:
	_selected = selected
	_label.visible = selected


func advance(dt: float) -> void:
	_clock += dt
	if _t < 1.0:
		_t = minf(1.0, _t + dt / _duration)
		position = _from.lerp(_to, _t)
		var heading := _to - _from
		if heading.length() > 0.01:
			rotation.y = lerp_angle(rotation.y, atan2(heading.x, heading.z), 0.25)
	_animate()


func _process(delta: float) -> void:
	advance(delta)


func _animate() -> void:
	var phase := _clock * 6.0 + float(npc_id)
	_body.rotation = Vector3.ZERO
	_body.position = Vector3(0, 0.35, 0)
	_head.position = Vector3(0, 0.85, 0)
	_arm.position = Vector3(0.25, 0.5, 0)
	_arm.rotation = Vector3.ZERO
	_material.emission = Color.BLACK
	var effective := "walk" if (is_moving() and pose != "sleep") else pose
	match effective:
		"walk":
			_body.position.y = 0.35 + absf(sin(phase * 1.4)) * 0.05
			_body.rotation.x = 0.15
			_arm.rotation.x = sin(phase * 1.4) * 0.8
		"sleep":
			_body.rotation.x = PI / 2
			_body.position = Vector3(0, 0.2, 0)
			_head.position = Vector3(0, 0.2, 0.5)
			_arm.position = Vector3(0.25, 0.2, 0)
			_arm.rotation.x = PI / 2
		"work":
			_arm.rotation.x = -absf(sin(phase * 1.2)) * 1.6
			_arm.position.y = 0.6
		"eat":
			_head.position.y = 0.85 - maxf(0.0, sin(phase * 0.8)) * 0.08
			_arm.rotation.x = -1.2 + sin(phase * 0.8) * 0.3
			_arm.position.y = 0.6
		"socialize":
			_head.rotation.y = sin(phase * 0.5) * 0.5
			_body.position.y = 0.35 + absf(sin(phase * 0.9)) * 0.03
			_arm.rotation.z = sin(phase) * 0.5
		"buy":
			_arm.rotation.x = -1.0
			_arm.position.y = 0.6
		"power":
			_material.emission = Color(0.4, 0.7, 1.0) * (0.5 + 0.5 * sin(phase * 0.7))
			_body.position.y = 0.45 + sin(phase * 0.7) * 0.08
		_:
			_body.scale = Vector3(1, 1.0 + sin(phase * 0.5) * 0.015, 1)
	if _selected:
		_material.emission = Color(1.0, 0.85, 0.2) * 0.6
