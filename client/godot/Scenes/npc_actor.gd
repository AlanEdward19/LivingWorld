extends Node3D
## Um NPC em 3D: corpo articulado procedural (placeholder estilizado) com animação por pose.
## Movimento e pose seguem o que o servidor projeta; nada aqui decide comportamento.

const Grid := preload("res://Scenes/grid.gd")
const Anim := preload("res://Scenes/animation_catalog.gd")

const SKIN_TONES: Array[Color] = [Color("f1c9a5"), Color("e0ac7e"), Color("c68a5c"), Color("a06a44"), Color("7a4b30")]
const HAIR_COLORS: Array[Color] = [Color("2b1b12"), Color("5a3a22"), Color("a0652a"), Color("d6b36a"), Color("8b8b8b"), Color("b5482a")]

var npc_id := 0
var cell := Vector2i.ZERO
var action := -1
var pose := Anim.NEUTRAL_POSE
var label_text := ""
var display_name := ""

var _from := Vector3.ZERO
var _to := Vector3.ZERO
var _t := 1.0
var _duration := 1.0
var _clock := 0.0
var _body: Node3D ## tronco + cabeça + braços (gira/deita como um bloco)
var _torso: MeshInstance3D
var _head: Node3D
var _arms: Array[Node3D] = []
var _legs: Array[Node3D] = []
var _tool: Node3D
var _bed: Node3D
var _label: Label3D
var _bubble: Label3D
var _zzz: Label3D
var _ring: MeshInstance3D
var _aura: MeshInstance3D
var _shirt: StandardMaterial3D
var use_bed := false ## o interior liga: cama só dentro de casa
var _selected := false
var _hovered := false


func setup(id: int, p_cell: Vector2i, p_action: int, labels) -> void:
	npc_id = id
	cell = p_cell
	position = Grid.to_world(cell)
	_from = position
	_to = position
	_build()
	set_action(p_action, labels)


func _mat(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.85
	return material


func _part(parent: Node3D, mesh: Mesh, material: Material, at: Vector3) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node


func _limb(parent: Node3D, shoulder: Vector3, size: Vector3, material: Material) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = shoulder
	parent.add_child(pivot)
	var mesh := BoxMesh.new()
	mesh.size = size
	_part(pivot, mesh, material, Vector3(0, -size.y / 2.0, 0))
	return pivot


func _build() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = npc_id * 7919 + 17
	var hue := rng.randf()
	_shirt = _mat(Color.from_hsv(hue, 0.55, 0.8))
	var pants := _mat(Color.from_hsv(fmod(hue + 0.5, 1.0), 0.35, 0.4))
	var skin := _mat(SKIN_TONES[rng.randi() % SKIN_TONES.size()])
	var hair := _mat(HAIR_COLORS[rng.randi() % HAIR_COLORS.size()])
	_shirt.emission_enabled = true
	_shirt.emission = Color.BLACK

	_body = Node3D.new()
	add_child(_body)
	var torso_mesh := CapsuleMesh.new()
	torso_mesh.radius = 0.17
	torso_mesh.height = 0.55
	_torso = _part(_body, torso_mesh, _shirt, Vector3(0, 0.62, 0))
	_head = Node3D.new()
	_head.position = Vector3(0, 0.98, 0)
	_body.add_child(_head)
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.13
	head_mesh.height = 0.26
	_part(_head, head_mesh, skin, Vector3.ZERO)
	var hair_mesh := SphereMesh.new()
	hair_mesh.radius = 0.14
	hair_mesh.height = 0.2
	_part(_head, hair_mesh, hair, Vector3(0, 0.04, -0.02))
	for side in [-1, 1]:
		_arms.append(_limb(_body, Vector3(side * 0.22, 0.82, 0), Vector3(0.08, 0.34, 0.08), _shirt))
		_legs.append(_limb(self, Vector3(side * 0.08, 0.36, 0), Vector3(0.1, 0.36, 0.1), pants))

	_tool = Node3D.new()
	_tool.visible = false
	_arms[1].add_child(_tool)
	var handle := BoxMesh.new()
	handle.size = Vector3(0.04, 0.34, 0.04)
	_part(_tool, handle, _mat(Color("6b4a2b")), Vector3(0, -0.34, 0.08))
	var head_tool := BoxMesh.new()
	head_tool.size = Vector3(0.14, 0.08, 0.08)
	_part(_tool, head_tool, _mat(Color("7d838c")), Vector3(0, -0.5, 0.08))

	_bed = Node3D.new()
	_bed.visible = false
	add_child(_bed)
	var mattress := BoxMesh.new()
	mattress.size = Vector3(0.7, 0.14, 1.15)
	_part(_bed, mattress, _mat(Color("e9e2d0")), Vector3(0, 0.07, 0.1))
	var frame := BoxMesh.new()
	frame.size = Vector3(0.76, 0.06, 1.2)
	_part(_bed, frame, _mat(Color("6b4a2b")), Vector3(0, 0.03, 0.1))
	var pillow := BoxMesh.new()
	pillow.size = Vector3(0.3, 0.07, 0.2)
	_part(_bed, pillow, _mat(Color("ffffff")), Vector3(0, 0.17, 0.5))

	_ring = MeshInstance3D.new()
	var ring := TorusMesh.new()
	ring.inner_radius = 0.36
	ring.outer_radius = 0.46
	_ring.mesh = ring
	var ring_material := _mat(Color("ffd84a"))
	ring_material.emission_enabled = true
	ring_material.emission = Color("ffd84a")
	ring_material.emission_energy_multiplier = 1.4
	_ring.material_override = ring_material
	_ring.position.y = 0.03
	_ring.visible = false
	add_child(_ring)

	_aura = MeshInstance3D.new()
	var aura := SphereMesh.new()
	aura.radius = 0.55
	aura.height = 1.1
	_aura.mesh = aura
	var aura_material := _mat(Color(0.45, 0.7, 1.0, 0.25))
	aura_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	aura_material.emission_enabled = true
	aura_material.emission = Color(0.4, 0.7, 1.0)
	_aura.material_override = aura_material
	_aura.position.y = 0.6
	_aura.visible = false
	add_child(_aura)

	_label = _text(Vector3(0, 1.45, 0), 26, Color.WHITE)
	_label.visible = false
	_bubble = _text(Vector3(0.1, 1.3, 0), 30, Color("ffffff"))
	_bubble.no_depth_test = false
	_bubble.visible = false
	_zzz = _text(Vector3(0, 0.9, 0), 34, Color("bfe3ff"))
	_zzz.no_depth_test = false
	_zzz.text = "Z"
	_zzz.visible = false

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


func _text(at: Vector3, size: int, color: Color) -> Label3D:
	var label := Label3D.new()
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.position = at
	label.pixel_size = 0.007
	label.font_size = size
	label.modulate = color
	label.outline_size = 10
	label.no_depth_test = true
	add_child(label)
	return label


func set_action(new_action: int, labels) -> void:
	action = new_action
	pose = Anim.pose_for(new_action)
	label_text = labels.label_for(new_action) if labels != null else ""
	_refresh_label()
	_tool.visible = pose == "work"
	_bed.visible = pose == "sleep" and use_bed
	_aura.visible = pose == "power"
	_bubble.visible = pose == "socialize"
	_zzz.visible = pose == "sleep"


func enable_bed(enabled: bool) -> void:
	use_bed = enabled
	_bed.visible = pose == "sleep" and use_bed


func set_display_name(value: String) -> void:
	display_name = value
	_refresh_label()


func _refresh_label() -> void:
	_label.text = ("%s\n%s" % [display_name, label_text]) if display_name != "" else label_text
	_label.visible = _selected or _hovered


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
	_ring.visible = selected or _hovered
	_refresh_label()


func set_hovered(hovered: bool) -> void:
	_hovered = hovered
	_ring.visible = _selected or hovered
	_refresh_label()


func advance(dt: float) -> void:
	_clock += dt
	if _t < 1.0:
		_t = minf(1.0, _t + dt / _duration)
		position = _from.lerp(_to, _t)
		var heading := _to - _from
		if heading.length() > 0.01:
			rotation.y = lerp_angle(rotation.y, atan2(heading.x, heading.z), minf(1.0, dt * 10.0))
	_animate()


func _process(delta: float) -> void:
	advance(delta)


func _reset_pose() -> void:
	_body.position = Vector3.ZERO
	_body.rotation = Vector3.ZERO
	_head.rotation = Vector3.ZERO
	for limb in _arms + _legs:
		limb.rotation = Vector3.ZERO
	_torso.scale = Vector3.ONE
	_shirt.emission = Color.BLACK


func _animate() -> void:
	_reset_pose()
	var phase := _clock * 7.0 + float(npc_id)
	var effective := "walk" if (is_moving() and pose != "sleep") else pose
	match effective:
		"walk":
			var swing := sin(phase * 1.3)
			_legs[0].rotation.x = swing * 0.7
			_legs[1].rotation.x = -swing * 0.7
			_arms[0].rotation.x = -swing * 0.6
			_arms[1].rotation.x = swing * 0.6
			_body.position.y = absf(sin(phase * 1.3)) * 0.04
			_body.rotation.x = 0.08
		"sleep":
			_body.rotation.x = -PI / 2.0
			_body.position = Vector3(0, 0.27, -0.28)
			_head.rotation.x = 0.2
			_zzz.position = Vector3(0.1 + sin(_clock) * 0.05, 0.9 + fmod(_clock * 0.3, 0.6), 0)
			_zzz.modulate.a = 1.0 - fmod(_clock * 0.3, 0.6) / 0.6
			_torso.scale.y = 1.0 + sin(_clock * 1.6) * 0.03
		"work":
			_arms[1].rotation.x = -1.2 + sin(phase * 1.1) * 0.9
			_arms[0].rotation.x = -0.5
			_body.rotation.x = 0.15 + sin(phase * 1.1) * 0.05
			_legs[0].rotation.x = 0.1
		"eat":
			_arms[1].rotation.x = -1.8 + sin(phase * 0.8) * 0.5
			_head.rotation.x = 0.12 + maxf(0.0, sin(phase * 0.8)) * 0.15
		"socialize":
			_head.rotation.y = sin(phase * 0.45) * 0.6
			_arms[0].rotation.x = -0.9 + sin(phase * 0.9) * 0.5
			_arms[1].rotation.x = -0.7 + cos(phase * 0.8) * 0.5
			_body.position.y = absf(sin(phase * 0.9)) * 0.02
			var dots := 1 + int(_clock * 2.0) % 3
			_bubble.text = ".".repeat(dots)
		"buy":
			_arms[1].rotation.x = -1.3
			_arms[0].rotation.x = -1.3
			_head.rotation.x = 0.1
		"power":
			_body.position.y = 0.12 + sin(phase * 0.6) * 0.06
			_arms[0].rotation.z = 0.9
			_arms[1].rotation.z = -0.9
			_shirt.emission = Color(0.4, 0.7, 1.0) * (0.5 + 0.5 * sin(phase * 0.7))
			_aura.scale = Vector3.ONE * (1.0 + sin(phase * 0.7) * 0.08)
		_:
			_torso.scale.y = 1.0 + sin(phase * 0.4) * 0.015
			_head.rotation.y = sin(phase * 0.15) * 0.4
			_arms[0].rotation.x = sin(phase * 0.4) * 0.05
			_arms[1].rotation.x = -sin(phase * 0.4) * 0.05
	if _selected or _hovered:
		_ring.rotation.y = _clock * 1.5
