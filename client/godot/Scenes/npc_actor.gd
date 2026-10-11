extends Node3D
## Um NPC em 3D: corpo articulado procedural (placeholder estilizado) com animação por pose.
## Movimento e pose seguem o que o servidor projeta; nada aqui decide comportamento.

const Grid := preload("res://Scenes/grid.gd")
const Anim := preload("res://Scenes/animation_catalog.gd")

const SKIN_TONES: Array[Color] = [Color("f1c9a5"), Color("e0ac7e"), Color("c68a5c"), Color("a06a44"), Color("7a4b30")]
const HAIR_COLORS: Array[Color] = [Color("2b1b12"), Color("5a3a22"), Color("a0652a"), Color("d6b36a"), Color("8b8b8b"), Color("b5482a")]

static var _meshes: Dictionary = {}
static var _materials: Dictionary = {}

var npc_id := 0
var far_lod := false
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


## Materiais sem emissão variável são compartilhados entre todos os NPCs (menos memória e trocas de estado).
func _shared_mat(color: Color) -> StandardMaterial3D:
	var key := color.to_html()
	if not _materials.has(key):
		_materials[key] = _mat(color)
	return _materials[key]


func _shared_mesh(key: String, factory: Callable) -> Mesh:
	if not _meshes.has(key):
		_meshes[key] = factory.call()
	return _meshes[key]


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
	var mesh := _shared_mesh("limb%s" % size, func():
		var m := BoxMesh.new()
		m.size = size
		return m)
	_part(pivot, mesh, material, Vector3(0, -size.y / 2.0, 0))
	return pivot


func _build() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = npc_id * 7919 + 17
	var hue := rng.randf()
	_shirt = _mat(Color.from_hsv(hue, 0.55, 0.8))
	var pants := _shared_mat(Color.from_hsv(snappedf(fmod(hue + 0.5, 1.0), 0.1), 0.35, 0.4))
	var skin := _shared_mat(SKIN_TONES[rng.randi() % SKIN_TONES.size()])
	var hair := _shared_mat(HAIR_COLORS[rng.randi() % HAIR_COLORS.size()])
	_shirt.emission_enabled = true
	_shirt.emission = Color.BLACK

	_body = Node3D.new()
	add_child(_body)
	var torso_mesh := _shared_mesh("torso", func():
		var m := CapsuleMesh.new()
		m.radius = 0.17
		m.height = 0.55
		return m)
	_torso = _part(_body, torso_mesh, _shirt, Vector3(0, 0.62, 0))
	_head = Node3D.new()
	_head.position = Vector3(0, 0.98, 0)
	_body.add_child(_head)
	var head_mesh := _shared_mesh("head", func():
		var m := SphereMesh.new()
		m.radius = 0.13
		m.height = 0.26
		m.radial_segments = 12
		m.rings = 6
		return m)
	_part(_head, head_mesh, skin, Vector3.ZERO)
	var hair_mesh := _shared_mesh("hair", func():
		var m := SphereMesh.new()
		m.radius = 0.14
		m.height = 0.2
		m.radial_segments = 12
		m.rings = 6
		return m)
	_part(_head, hair_mesh, hair, Vector3(0, 0.04, -0.02))
	for side in [-1, 1]:
		_arms.append(_limb(_body, Vector3(side * 0.22, 0.82, 0), Vector3(0.08, 0.34, 0.08), _shirt))
		_legs.append(_limb(self, Vector3(side * 0.08, 0.36, 0), Vector3(0.1, 0.36, 0.1), pants))

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


## Partes raras (ferramenta, cama, anel, aura, textos) só existem quando precisam: 500 NPCs com tudo
## pronto custam muitos nós; assim a maioria dos NPCs tem só corpo, cabeça, braços e pernas.
func _get_tool() -> Node3D:
	if _tool == null:
		_tool = Node3D.new()
		_arms[1].add_child(_tool)
		var handle := BoxMesh.new()
		handle.size = Vector3(0.04, 0.34, 0.04)
		_part(_tool, handle, _mat(Color("6b4a2b")), Vector3(0, -0.34, 0.08))
		var head_tool := BoxMesh.new()
		head_tool.size = Vector3(0.14, 0.08, 0.08)
		_part(_tool, head_tool, _mat(Color("7d838c")), Vector3(0, -0.5, 0.08))
	return _tool


func _get_bed() -> Node3D:
	if _bed == null:
		_bed = Node3D.new()
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
	return _bed


func _get_ring() -> MeshInstance3D:
	if _ring == null:
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
		add_child(_ring)
	return _ring


func _get_aura() -> MeshInstance3D:
	if _aura == null:
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
		add_child(_aura)
	return _aura


func _get_label() -> Label3D:
	if _label == null:
		_label = _text(Vector3(0, 1.45, 0), 26, Color.WHITE)
		_label.visible = false
	return _label


func _get_bubble() -> Label3D:
	if _bubble == null:
		_bubble = _text(Vector3(0.1, 1.3, 0), 30, Color("ffffff"))
		_bubble.no_depth_test = false
	return _bubble


func _get_zzz() -> Label3D:
	if _zzz == null:
		_zzz = _text(Vector3(0, 0.9, 0), 34, Color("bfe3ff"))
		_zzz.text = "Z"
		_zzz.no_depth_test = false
	return _zzz


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
	_sync_props()


## Longe da câmera os NPCs são só tronco e cabeça (sem braços, pernas nem sombra): 500 NPCs fluidos.
func set_far(value: bool) -> void:
	if far_lod == value:
		return
	far_lod = value
	for limb in _arms + _legs:
		limb.visible = not value
	_torso.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF if value else GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	if value:
		_reset_pose()


func _sync_props() -> void:
	if pose == "work" or _tool != null:
		_get_tool().visible = pose == "work"
	if (pose == "sleep" and use_bed) or _bed != null:
		_get_bed().visible = pose == "sleep" and use_bed
	if pose == "power" or _aura != null:
		_get_aura().visible = pose == "power"
	if pose == "socialize" or _bubble != null:
		_get_bubble().visible = pose == "socialize"
	if pose == "sleep" or _zzz != null:
		_get_zzz().visible = pose == "sleep"


func enable_bed(enabled: bool) -> void:
	use_bed = enabled
	_sync_props()


func set_display_name(value: String) -> void:
	display_name = value
	_refresh_label()


func _refresh_label() -> void:
	if _label == null and not (_selected or _hovered):
		return
	var label := _get_label()
	label.text = ("%s\n%s" % [display_name, label_text]) if display_name != "" else label_text
	label.visible = _selected or _hovered


func label_visible() -> bool:
	return _label != null and _label.visible


func label_text_shown() -> String:
	return _label.text if _label != null else ""


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
	if _ring != null or selected or _hovered:
		_get_ring().visible = selected or _hovered
	_refresh_label()


func set_hovered(hovered: bool) -> void:
	_hovered = hovered
	if _ring != null or hovered or _selected:
		_get_ring().visible = _selected or hovered
	_refresh_label()


func advance(dt: float) -> void:
	if far_lod:
		advance_cheap(dt)
		return
	_clock += dt
	if _t < 1.0:
		_t = minf(1.0, _t + dt / _duration)
		position = _from.lerp(_to, _t)
		var heading := _to - _from
		if heading.length() > 0.01:
			rotation.y = lerp_angle(rotation.y, atan2(heading.x, heading.z), minf(1.0, dt * 10.0))
	_animate()


## Fora da vista: só segue o movimento, sem montar pose (barato).
func advance_cheap(dt: float) -> void:
	_clock += dt
	if _t < 1.0:
		_t = minf(1.0, _t + dt / _duration)
		position = _from.lerp(_to, _t)


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
			var zzz := _get_zzz()
			zzz.position = Vector3(0.1 + sin(_clock) * 0.05, 0.9 + fmod(_clock * 0.3, 0.6), 0)
			zzz.modulate.a = 1.0 - fmod(_clock * 0.3, 0.6) / 0.6
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
			_get_bubble().text = ".".repeat(dots)
		"buy":
			_arms[1].rotation.x = -1.3
			_arms[0].rotation.x = -1.3
			_head.rotation.x = 0.1
		"power":
			_body.position.y = 0.12 + sin(phase * 0.6) * 0.06
			_arms[0].rotation.z = 0.9
			_arms[1].rotation.z = -0.9
			_shirt.emission = Color(0.4, 0.7, 1.0) * (0.5 + 0.5 * sin(phase * 0.7))
			_get_aura().scale = Vector3.ONE * (1.0 + sin(phase * 0.7) * 0.08)
		_:
			_torso.scale.y = 1.0 + sin(phase * 0.4) * 0.015
			_head.rotation.y = sin(phase * 0.15) * 0.4
			_arms[0].rotation.x = sin(phase * 0.4) * 0.05
			_arms[1].rotation.x = -sin(phase * 0.4) * 0.05
	if _ring != null and (_selected or _hovered):
		_ring.rotation.y = _clock * 1.5
