extends RefCounted
## Peças geradas por código para o corpo do NPC (ADR-0047 etapa 3, sem pacote de assets): chapéu, ferramenta na mão, barriga
## de gravidez, saco/jarro da carga, recipiente levado, rosto (olhos, boca, sobrancelha, lágrima) e esteira de quem está de
## cama. Malhas e materiais são compartilhados entre todos os NPCs (chave por forma e cor); nenhuma peça projeta sombra.

static var _meshes: Dictionary = {}
static var _materials: Dictionary = {}


static func material(color: Color, unshaded: bool = false) -> StandardMaterial3D:
	var key := "%s|%s" % [color.to_html(), unshaded]
	if not _materials.has(key):
		var m := StandardMaterial3D.new()
		m.albedo_color = color
		m.roughness = 0.85
		if unshaded:
			m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		_materials[key] = m
	return _materials[key]


static func _box(size: Vector3) -> Mesh:
	var key := "box%s" % size
	if not _meshes.has(key):
		var m := BoxMesh.new()
		m.size = size
		_meshes[key] = m
	return _meshes[key]


static func _cylinder(top: float, bottom: float, height: float, segments: int = 10) -> Mesh:
	var key := "cyl%s|%s|%s|%d" % [top, bottom, height, segments]
	if not _meshes.has(key):
		var m := CylinderMesh.new()
		m.top_radius = top
		m.bottom_radius = bottom
		m.height = height
		m.radial_segments = segments
		m.rings = 1
		_meshes[key] = m
	return _meshes[key]


static func _sphere(radius: float, height: float = -1.0) -> Mesh:
	var key := "sph%s|%s" % [radius, height]
	if not _meshes.has(key):
		var m := SphereMesh.new()
		m.radius = radius
		m.height = radius * 2.0 if height < 0.0 else height
		m.radial_segments = 10
		m.rings = 5
		_meshes[key] = m
	return _meshes[key]


static func _capsule(radius: float, height: float) -> Mesh:
	var key := "cap%s|%s" % [radius, height]
	if not _meshes.has(key):
		var m := CapsuleMesh.new()
		m.radius = radius
		m.height = height
		m.radial_segments = 10
		m.rings = 3
		_meshes[key] = m
	return _meshes[key]


static func part(parent: Node3D, mesh: Mesh, color: Color, at: Vector3, rotation: Vector3 = Vector3.ZERO) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = material(color)
	node.position = at
	node.rotation = rotation
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(node)
	return node


## Malha da forma da peça de um design (vocabulário de `/designs`: box, cylinder, sphere, capsule), no tamanho dado.
static func shape_mesh(shape: String, size: float) -> Mesh:
	match shape:
		"cylinder":
			return _cylinder(size * 0.45, size * 0.5, size)
		"sphere":
			return _sphere(size * 0.5)
		"capsule":
			return _capsule(size * 0.3, size)
	return _box(Vector3.ONE * size)


## Malha única feita de várias (mesma cor): um nó só por peça do corpo em 500 NPCs, em vez de um por parte.
static func _merged(key: String, parts: Array) -> Mesh:
	if not _meshes.has(key):
		var tool := SurfaceTool.new()
		tool.begin(Mesh.PRIMITIVE_TRIANGLES)
		for entry in parts:
			tool.append_from(entry[0], 0, entry[1])
		_meshes[key] = tool.commit()
	return _meshes[key]


static func _at(position: Vector3) -> Transform3D:
	return Transform3D(Basis(), position)


## Chapéu por tipo (palha, boné, pano, elmo, capuz); "none" = nada. Fica na cabeça (filho de `head`); um nó só.
static func hat(head: Node3D, kind: String) -> Node3D:
	if kind == "none" or kind == "":
		return null
	var mesh: Mesh
	var color: Color
	match kind:
		"straw":
			mesh = _merged("hat-straw", [[_cylinder(0.24, 0.25, 0.025, 12), _at(Vector3(0, 0.1, 0))],
				[_cylinder(0.1, 0.13, 0.1, 10), _at(Vector3(0, 0.16, 0))]])
			color = Color("c9a85a")
		"helmet":
			mesh = _merged("hat-helmet", [[_sphere(0.15, 0.16), _at(Vector3(0, 0.08, 0))]])
			color = Color("8a9098")
		"hood":
			mesh = _merged("hat-hood", [[_sphere(0.155, 0.26), _at(Vector3(0, 0.02, -0.02))]])
			color = Color("5a4a6a")
		"cloth":
			mesh = _merged("hat-cloth", [[_cylinder(0.135, 0.14, 0.07, 10), _at(Vector3(0, 0.1, 0))]])
			color = Color("e8e0cc")
		_:
			mesh = _merged("hat-cap", [[_cylinder(0.13, 0.15, 0.09, 10), _at(Vector3(0, 0.12, 0))],
				[_box(Vector3(0.16, 0.015, 0.1)), _at(Vector3(0, 0.08, 0.14))]])
			color = Color("3b3f46")
	var node := part(head, mesh, color, Vector3.ZERO)
	node.name = "Hat"
	return node


## Ferramenta do ofício na mão (filho do braço direito, a mão fica em y = -0.34).
static func tool(arm: Node3D, kind: String) -> Node3D:
	var root := Node3D.new()
	root.name = "Tool"
	arm.add_child(root)
	var wood := Color("6b4a2b")
	var iron := Color("7d838c")
	match kind:
		"hoe":
			part(root, _box(Vector3(0.03, 0.62, 0.03)), wood, Vector3(0, -0.4, 0.06))
			part(root, _box(Vector3(0.16, 0.03, 0.08)), iron, Vector3(0, -0.7, 0.1))
		"axe":
			part(root, _box(Vector3(0.035, 0.42, 0.035)), wood, Vector3(0, -0.4, 0.06))
			part(root, _box(Vector3(0.04, 0.1, 0.13)), iron, Vector3(0, -0.58, 0.12))
		"pick":
			part(root, _box(Vector3(0.035, 0.44, 0.035)), wood, Vector3(0, -0.4, 0.06))
			part(root, _box(Vector3(0.26, 0.035, 0.035)), iron, Vector3(0, -0.6, 0.06))
		"rod":
			part(root, _box(Vector3(0.02, 0.9, 0.02)), wood, Vector3(0, -0.55, 0.2), Vector3(0.5, 0, 0))
		"peel":
			part(root, _box(Vector3(0.03, 0.5, 0.03)), wood, Vector3(0, -0.45, 0.06))
			part(root, _box(Vector3(0.16, 0.02, 0.16)), Color("a07a4a"), Vector3(0, -0.72, 0.06))
		"spear":
			part(root, _box(Vector3(0.025, 1.0, 0.025)), wood, Vector3(0, -0.3, 0.06))
			part(root, _cylinder(0.0, 0.03, 0.1, 6), iron, Vector3(0, 0.25, 0.06))
		"staff":
			part(root, _box(Vector3(0.03, 0.95, 0.03)), wood, Vector3(0, -0.3, 0.06))
		"scale":
			part(root, _box(Vector3(0.22, 0.015, 0.015)), Color("b8963a"), Vector3(0, -0.4, 0.1))
			for side in [-1, 1]:
				part(root, _cylinder(0.05, 0.05, 0.015, 8), Color("b8963a"), Vector3(side * 0.1, -0.46, 0.1))
		"needle":
			part(root, _box(Vector3(0.1, 0.1, 0.1)), Color("c8b8a0"), Vector3(0, -0.4, 0.08))
		_: # martelo
			part(root, _box(Vector3(0.035, 0.34, 0.035)), wood, Vector3(0, -0.36, 0.08))
			part(root, _box(Vector3(0.14, 0.07, 0.07)), iron, Vector3(0, -0.52, 0.08))
	return root


## Barriga de gravidez (filho do tronco do corpo); a escala acompanha a fração da gestação.
static func belly(body: Node3D, cloth: Color) -> MeshInstance3D:
	var node := part(body, _sphere(0.14), cloth, Vector3(0, 0.56, 0.11))
	node.name = "Belly"
	return node


## Carga pessoal nas costas: saco de pano (cor do recurso) ou jarro (água); um nó só, na escala da quantidade.
static func sack(body: Node3D, spec: Dictionary) -> Node3D:
	var water := bool(spec.get("water", false))
	var mesh: Mesh
	if water:
		mesh = _merged("sack-jar", [[_cylinder(0.06, 0.09, 0.2, 10), _at(Vector3(0, 0.7, -0.2))],
			[_cylinder(0.05, 0.05, 0.02, 10), _at(Vector3(0, 0.81, -0.2))]])
	else:
		mesh = _merged("sack-cloth", [[_sphere(0.13, 0.22), _at(Vector3(0, 0.72, -0.2))],
			[_cylinder(0.03, 0.05, 0.06, 8), _at(Vector3(0, 0.85, -0.2))]])
	var node := part(body, mesh, Color("9a6a43") if water else spec.color, Vector3.ZERO)
	node.name = "Sack"
	node.scale = Vector3.ONE * float(spec.get("size", 1.0))
	return node


## Recipiente que a pessoa leva (peça de um design): forma da peça raiz e cor do material; na mão esquerda.
static func held(arm: Node3D, shape: String, color: Color) -> MeshInstance3D:
	var node := part(arm, shape_mesh(shape, 0.14), color, Vector3(0, -0.4, 0.06))
	node.name = "Held"
	return node


## Rosto simples (só de perto): olhos, boca e, conforme a expressão, sobrancelha franzida ou lágrima.
static func face(head: Node3D, expression: String) -> Node3D:
	var root := Node3D.new()
	root.name = "Face"
	head.add_child(root)
	var dark := Color("2a1d16")
	var eye_size := Vector3(0.03, 0.045 if expression == "fear" else 0.03, 0.01)
	for side in [-1, 1]:
		part(root, _box(eye_size), dark, Vector3(side * 0.045, 0.02, 0.125))
		if expression == "anger":
			part(root, _box(Vector3(0.05, 0.012, 0.01)), dark, Vector3(side * 0.045, 0.06, 0.124), Vector3(0, 0, side * -0.5))
		elif expression in ["grief", "loneliness", "shame"]:
			part(root, _box(Vector3(0.05, 0.012, 0.01)), dark, Vector3(side * 0.045, 0.06, 0.124), Vector3(0, 0, side * 0.4))
	if expression == "grief":
		part(root, _box(Vector3(0.012, 0.04, 0.008)), Color("8fc8ff"), Vector3(0.05, -0.02, 0.127))
	var mouth := part(root, _box(Vector3(0.06, 0.014, 0.01)), Color("7a2e2e"), Vector3(0, -0.05, 0.12))
	match expression:
		"joy":
			mouth.scale = Vector3(1.2, 1.6, 1)
		"fear":
			mouth.scale = Vector3(0.5, 2.6, 1)
		"grief", "loneliness", "shame":
			mouth.rotation.z = 0.0
			mouth.position.y = -0.06
	return root


## Esteira e coberta de quem está de cama (doença que derruba).
static func sickbed(owner: Node3D) -> Node3D:
	var root := Node3D.new()
	root.name = "Sickbed"
	owner.add_child(root)
	part(root, _box(Vector3(0.6, 0.05, 1.1)), Color("cbbf9e"), Vector3(0, 0.025, -0.2))
	part(root, _box(Vector3(0.56, 0.06, 0.6)), Color("8a6a9a"), Vector3(0, 0.33, -0.05))
	return root
