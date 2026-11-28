extends RefCounted
## #266: modelos procedurais de móveis, gerados por código (sem assets externos). O que cada móvel é vem do catálogo do cenário: `model.kind` escolhe o gerador genérico
## abaixo, `model.sizeM` [largura, profundidade, altura] dá as medidas em METROS, `model.style` as cores por material (madeira, tecido, metal, cerâmica...) e `model.align`
## onde ele fica na pegada (`back`: encostado no fundo, junto à parede; `center`). O modelo é desenhado centrado na pegada que o motor deu (`footprint`) e com o fundo
## para -z, então cabe nela e fica de costas para a parede no giro que o motor mandou. Malhas e materiais são compartilhados por forma e cor (cache): nada é criado por instância.

const Grid := preload("res://Scenes/grid.gd")
const FurnitureFootprint := preload("res://Scenes/furniture_footprint.gd")

const WALL_HALF := 0.11 ## unidades: meia espessura da parede (wall_panels.gd THICKNESS 0.22): a face de dentro da parede
const WALL_GAP_M := 0.012 ## folga em metros entre o fundo do móvel e a parede
const DETAIL_RANGE := 11.0 ## unidades: as peças pequenas (livros, botões, almofadas) somem de longe; o corpo fica (LOD barato)
const KINDS := ["bed", "sofa", "armchair", "table", "chair", "fridge", "stove", "sink", "shower", "toilet", "tv", "lamp", "floorlamp", "plant", "bookshelf", "rug", "painting"]

static var _meshes: Dictionary = {}
static var _materials: Dictionary = {}
static var meshes_built := 0 ## quantas malhas distintas existem (o teste confere o compartilhamento)


## O descritor traz um modelo que sabemos desenhar.
static func supports(descriptor: Dictionary) -> bool:
	var model: Variant = descriptor.get("model")
	return model is Dictionary and String(model.get("kind", "")) in KINDS and model.get("sizeM") is Array and model.sizeM.size() >= 3


## O nó do móvel: origem no chão, no centro da pegada; o fundo para -z. Em unidades da grade (uma célula = 1).
static func build(descriptor: Dictionary, variation: int = 0) -> Node3D:
	var model: Dictionary = descriptor.model
	var size := Vector3(float(model.sizeM[0]), float(model.sizeM[2]), float(model.sizeM[1])) # x largura, y altura, z profundidade (m)
	var footprint := FurnitureFootprint.size_of(descriptor)
	var cell_m := Grid.cell_meters
	var root := Node3D.new()
	root.name = String(descriptor.get("id", "furniture"))
	var body := Node3D.new()
	body.name = "Body"
	body.scale = Vector3.ONE / cell_m # os geradores trabalham em metros
	if String(model.get("align", "back")) == "back":
		body.position.z = (-float(footprint.y) / 2.0 + WALL_HALF) + (WALL_GAP_M + size.z / 2.0) / cell_m
	root.add_child(body)
	var style: Dictionary = model.get("style", {}) if model.get("style") is Dictionary else {}
	match String(model.kind):
		"bed": _bed(body, size, style, variation)
		"sofa", "armchair": _sofa(body, size, style, variation)
		"table": _table(body, size, style, variation)
		"chair": _chair(body, size, style, variation)
		"fridge": _fridge(body, size, style, variation)
		"stove": _stove(body, size, style, variation)
		"sink": _sink(body, size, style, variation)
		"shower": _shower(body, size, style, variation)
		"toilet": _toilet(body, size, style, variation)
		"tv": _tv(body, size, style, variation)
		"lamp": _lamp(body, size, style, variation)
		"floorlamp": _floorlamp(body, size, style, variation)
		"plant": _plant(body, size, style, variation)
		"bookshelf": _bookshelf(body, size, style, variation)
		"rug": _rug(body, size, style, variation)
		"painting": _painting(body, size, style, variation)
	return root


## A caixa (em unidades, no espaço do nó) que as partes ocupam: serve ao enquadramento da miniatura e aos testes.
static func node_bounds(node: Node3D) -> AABB:
	var out := AABB()
	var first := true
	for found in node.find_children("*", "MeshInstance3D", true, false):
		var instance := found as MeshInstance3D
		if instance.mesh == null:
			continue
		var xf := Transform3D.IDENTITY
		var cur: Node = instance
		while cur != null and cur != node and cur is Node3D:
			xf = (cur as Node3D).transform * xf
			cur = cur.get_parent()
		var box: AABB = xf * instance.mesh.get_aabb()
		out = box if first else out.merge(box)
		first = false
	return out


# ---------- partes ----------

static func _part(parent: Node3D, mesh: Mesh, material: Material, xf: Transform3D, detail: bool) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = material
	node.transform = xf
	if detail:
		node.visibility_range_end = DETAIL_RANGE
		node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	parent.add_child(node)
	return node


static func _xf(pos: Vector3, rot_deg: Vector3, scl: Vector3 = Vector3.ONE) -> Transform3D:
	var basis := Basis.from_euler(Vector3(deg_to_rad(rot_deg.x), deg_to_rad(rot_deg.y), deg_to_rad(rot_deg.z))).scaled(scl)
	return Transform3D(basis, pos)


static func _box(parent: Node3D, size: Vector3, pos: Vector3, material: Material, bevel: float = 0.012, rot_deg: Vector3 = Vector3.ZERO, detail: bool = false) -> MeshInstance3D:
	return _part(parent, _box_mesh(size, bevel), material, _xf(pos, rot_deg), detail)


static func _cyl(parent: Node3D, r_top: float, r_bottom: float, height: float, pos: Vector3, material: Material, rot_deg: Vector3 = Vector3.ZERO, scl: Vector3 = Vector3.ONE, detail: bool = false) -> MeshInstance3D:
	var key := "c|%.3f|%.3f|%.3f" % [r_top, r_bottom, height]
	if not _meshes.has(key):
		var mesh := CylinderMesh.new()
		mesh.top_radius = r_top
		mesh.bottom_radius = r_bottom
		mesh.height = height
		mesh.radial_segments = 20
		mesh.rings = 1
		_meshes[key] = mesh
		meshes_built += 1
	return _part(parent, _meshes[key], material, _xf(pos, rot_deg, scl), detail)


static func _ball(parent: Node3D, radius: float, pos: Vector3, material: Material, scl: Vector3 = Vector3.ONE, rot_deg: Vector3 = Vector3.ZERO, detail: bool = false) -> MeshInstance3D:
	var key := "s|%.3f" % radius
	if not _meshes.has(key):
		var mesh := SphereMesh.new()
		mesh.radius = radius
		mesh.height = radius * 2.0
		mesh.radial_segments = 14
		mesh.rings = 8
		_meshes[key] = mesh
		meshes_built += 1
	return _part(parent, _meshes[key], material, _xf(pos, rot_deg, scl), detail)


static func _box_mesh(size: Vector3, bevel: float) -> Mesh:
	var key := "b|%.3f|%.3f|%.3f|%.3f" % [size.x, size.y, size.z, bevel]
	if not _meshes.has(key):
		_meshes[key] = chamfer_mesh(size, bevel)
		meshes_built += 1
	return _meshes[key]


## Caixa de quinas chanfradas (6 faces, 12 chanfros, 8 cantos), sombreamento chapado; centrada na origem.
static func chamfer_mesh(size: Vector3, chamfer: float) -> ArrayMesh:
	var h := size / 2.0
	var c := minf(chamfer, minf(h.x, minf(h.y, h.z)) * 0.9)
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	st.set_smooth_group(-1)
	var corners := [Vector2(-1, -1), Vector2(1, -1), Vector2(1, 1), Vector2(-1, 1)]
	for axis in 3:
		var a1 := (axis + 1) % 3
		var a2 := (axis + 2) % 3
		for s in [-1.0, 1.0]:
			var quad: Array = []
			for uv in corners:
				var p := Vector3.ZERO
				p[axis] = s * h[axis]
				p[a1] = uv.x * (h[a1] - c)
				p[a2] = uv.y * (h[a2] - c)
				quad.append(p)
			_quad(st, quad)
	if c > 0.0005:
		for a in 3:
			for b in range(a + 1, 3):
				var t := 3 - a - b
				for sa in [-1.0, 1.0]:
					for sb in [-1.0, 1.0]:
						var quad: Array = []
						for end in [-1.0, 1.0]:
							var p := Vector3.ZERO
							p[a] = sa * h[a]
							p[b] = sb * (h[b] - c)
							p[t] = end * (h[t] - c)
							quad.append(p)
						for end in [1.0, -1.0]:
							var q := Vector3.ZERO
							q[a] = sa * (h[a] - c)
							q[b] = sb * h[b]
							q[t] = end * (h[t] - c)
							quad.append(q)
						_quad(st, quad)
		for sx in [-1.0, 1.0]:
			for sy in [-1.0, 1.0]:
				for sz in [-1.0, 1.0]:
					_tri(st, Vector3(sx * h.x, sy * (h.y - c), sz * (h.z - c)), Vector3(sx * (h.x - c), sy * h.y, sz * (h.z - c)), Vector3(sx * (h.x - c), sy * (h.y - c), sz * h.z))
	st.generate_normals()
	return st.commit()


static func _quad(st: SurfaceTool, quad: Array) -> void:
	_tri(st, quad[0], quad[1], quad[2])
	_tri(st, quad[0], quad[2], quad[3])


## Um triângulo com a frente para fora do centro (o Godot vira a frente no sentido horário).
static func _tri(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3) -> void:
	var normal := (b - a).cross(c - a)
	if normal.dot((a + b + c) / 3.0) > 0.0:
		var swap := b
		b = c
		c = swap
	st.add_vertex(a)
	st.add_vertex(b)
	st.add_vertex(c)


# ---------- materiais ----------

static func _color(style: Dictionary, role: String, fallback: String, variation: int = 0) -> Color:
	var color := Color(String(style.get(role, fallback)))
	if variation > 0:
		color = color.lightened(0.05 * variation)
	return color


static func _mat(kind: String, color: Color, glow: float = 0.0) -> Material:
	var key := "%s|%s|%.2f" % [kind, color.to_html(), glow]
	if _materials.has(key):
		return _materials[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = color
	match kind:
		"wood":
			m.roughness = 0.62
		"fabric":
			m.roughness = 0.96
		"metal":
			m.metallic = 0.3 # sem reflexo de ambiente o metal cheio vira preto: a cor do catálogo aparece, com brilho
			m.roughness = 0.38
		"ceramic":
			m.roughness = 0.16
		"glass":
			m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
			m.albedo_color = Color(color.r, color.g, color.b, 0.32)
			m.roughness = 0.05
		"screen":
			m.roughness = 0.07
			m.metallic = 0.4
		"leaf":
			m.roughness = 0.72
		_:
			m.roughness = 0.8
	if glow > 0.0:
		m.emission_enabled = true
		m.emission = color
		m.emission_energy_multiplier = glow
	_materials[key] = m
	return m


# ---------- geradores (medidas em metros; x largura, y altura, z profundidade; fundo em -z) ----------

static func _bed(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "8a6a43"))
	var dark := _mat("wood", _color(st, "wood", "8a6a43").darkened(0.18))
	var sheet := _mat("fabric", _color(st, "fabric", "efe8d8", v))
	var duvet := _mat("fabric", _color(st, "accent", "6f8fb5", v))
	var fold := _mat("fabric", _color(st, "accent", "6f8fb5", v).lightened(0.22))
	var w := s.x
	var d := s.z
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_box(b, Vector3(0.07, 0.12, 0.07), Vector3(sx * (w / 2.0 - 0.05), 0.06, sz * (d / 2.0 - 0.05)), dark, 0.01)
	_box(b, Vector3(w, 0.20, d - 0.02), Vector3(0, 0.22, 0), wood, 0.022)
	_box(b, Vector3(w - 0.06, 0.20, d - 0.14), Vector3(0, 0.42, 0.015), sheet, 0.05)
	var from_z := -d / 2.0 + 0.66
	var to_z := d / 2.0 - 0.05
	_box(b, Vector3(w - 0.03, 0.09, to_z - from_z), Vector3(0, 0.555, (from_z + to_z) / 2.0), duvet, 0.035)
	_box(b, Vector3(w - 0.05, 0.11, 0.24), Vector3(0, 0.565, from_z + 0.12), fold, 0.04)
	for sx in [-1.0, 1.0]:
		_box(b, Vector3(0.50, 0.13, 0.32), Vector3(sx * 0.285, 0.585, -d / 2.0 + 0.30), sheet, 0.055, Vector3(-6, sx * 3, 0), true)
	_box(b, Vector3(w, s.y - 0.12, 0.07), Vector3(0, 0.12 + (s.y - 0.12) / 2.0, -d / 2.0 + 0.035), wood, 0.022)
	_box(b, Vector3(w - 0.22, 0.42, 0.02), Vector3(0, 0.78, -d / 2.0 + 0.082), dark, 0.01, Vector3.ZERO, true)
	_box(b, Vector3(w, 0.38, 0.06), Vector3(0, 0.31, d / 2.0 - 0.03), wood, 0.02)
	_box(b, Vector3(w - 0.07, 0.035, 0.30), Vector3(0, 0.575, d / 2.0 - 0.25), fold, 0.015, Vector3.ZERO, true) # a manta dobrada aos pés


static func _sofa(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "3a2c22"))
	var base := _mat("fabric", _color(st, "fabric", "4d5f78", v))
	var cushion := _mat("fabric", _color(st, "accent", "5b6f8a", v))
	var pillow := _mat("fabric", _color(st, "accent", "5b6f8a", v).lightened(0.28))
	var w := s.x
	var d := s.z
	var arm := 0.2
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_box(b, Vector3(0.06, 0.10, 0.06), Vector3(sx * (w / 2.0 - 0.1), 0.05, sz * (d / 2.0 - 0.1)), wood, 0.008)
	_box(b, Vector3(w, 0.20, d), Vector3(0, 0.20, 0), base, 0.035)
	for sx in [-1.0, 1.0]:
		_box(b, Vector3(arm, 0.34, d), Vector3(sx * (w / 2.0 - arm / 2.0), 0.47, 0), base, 0.06)
	_box(b, Vector3(w - 2.0 * arm + 0.02, 0.50, 0.22), Vector3(0, 0.55, -d / 2.0 + 0.11), base, 0.06)
	var span := w - 2.0 * arm
	var count := maxi(1, roundi(span / 0.8))
	var seat := span / count
	for i in count:
		var x := -span / 2.0 + seat * (i + 0.5)
		_box(b, Vector3(seat - 0.025, 0.15, d - 0.32), Vector3(x, 0.375, 0.07), cushion, 0.055, Vector3.ZERO, true)
		_box(b, Vector3(seat - 0.05, 0.34, 0.15), Vector3(x, 0.60, -d / 2.0 + 0.30), cushion, 0.06, Vector3(-12, 0, 0), true)
	if count >= 2:
		for sx in [-1.0, 1.0]:
			_box(b, Vector3(0.30, 0.30, 0.10), Vector3(sx * (span / 2.0 - 0.20), 0.60, -d / 2.0 + 0.40), pillow, 0.05, Vector3(-18, sx * 22, sx * 6), true)


static func _table(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "8a6a43"))
	var dark := _mat("wood", _color(st, "wood", "8a6a43").darkened(0.16))
	var w := s.x
	var d := s.z
	var h := s.y
	_box(b, Vector3(w, 0.05, d), Vector3(0, h - 0.025, 0), wood, 0.014)
	_box(b, Vector3(w - 0.14, 0.07, d - 0.14), Vector3(0, h - 0.085, 0), dark, 0.01)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_box(b, Vector3(0.06, h - 0.05, 0.06), Vector3(sx * (w / 2.0 - 0.07), (h - 0.05) / 2.0, sz * (d / 2.0 - 0.07)), dark, 0.01)


static func _chair(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "7a5a38"))
	var cushion := _mat("fabric", _color(st, "fabric", "b8895a", v))
	var w := s.x
	var d := s.z
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_box(b, Vector3(0.036, 0.42, 0.036), Vector3(sx * (w / 2.0 - 0.03), 0.21, sz * (d / 2.0 - 0.03)), wood, 0.006)
		_box(b, Vector3(0.036, 0.46, 0.036), Vector3(sx * (w / 2.0 - 0.03), 0.67, -d / 2.0 + 0.03), wood, 0.006, Vector3(-5, 0, 0))
	_box(b, Vector3(w, 0.04, d), Vector3(0, 0.44, 0), wood, 0.01)
	_box(b, Vector3(w - 0.04, 0.045, d - 0.04), Vector3(0, 0.485, 0.0), cushion, 0.018, Vector3.ZERO, true)
	_box(b, Vector3(w - 0.05, 0.10, 0.03), Vector3(0, 0.80, -d / 2.0 + 0.012), wood, 0.008, Vector3(-5, 0, 0))
	_box(b, Vector3(w - 0.05, 0.09, 0.03), Vector3(0, 0.64, -d / 2.0 + 0.026), wood, 0.008, Vector3(-5, 0, 0), true)


static func _fridge(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var shell := _mat("metal", _color(st, "metal", "e4e7ea"))
	var trim := _mat("metal", _color(st, "accent", "b9bec4"))
	var gap := _mat("plastic", _color(st, "accent", "b9bec4").darkened(0.55))
	var w := s.x
	var d := s.z
	var h := s.y
	var freezer := 0.55
	var door_h := h - 0.06 - freezer - 0.02
	_box(b, Vector3(w - 0.04, 0.05, d - 0.06), Vector3(0, 0.025, -0.01), gap, 0.006)
	_box(b, Vector3(w, h - 0.05, d - 0.03), Vector3(0, 0.05 + (h - 0.05) / 2.0, -0.015), shell, 0.022)
	_box(b, Vector3(w - 0.02, door_h, 0.03), Vector3(0, 0.06 + door_h / 2.0, d / 2.0 - 0.015), shell, 0.016)
	_box(b, Vector3(w - 0.02, freezer, 0.03), Vector3(0, h - 0.01 - freezer / 2.0, d / 2.0 - 0.015), shell, 0.016)
	_box(b, Vector3(w - 0.01, 0.014, 0.032), Vector3(0, 0.06 + door_h + 0.01, d / 2.0 - 0.015), gap, 0.002)
	_box(b, Vector3(0.024, 0.34, 0.034), Vector3(w / 2.0 - 0.07, 0.06 + door_h - 0.26, d / 2.0 + 0.025), trim, 0.008, Vector3.ZERO, true)
	_box(b, Vector3(0.024, 0.20, 0.034), Vector3(w / 2.0 - 0.07, h - freezer + 0.16, d / 2.0 + 0.025), trim, 0.008, Vector3.ZERO, true)
	for sy in [0.0, 1.0]:
		_box(b, Vector3(0.05, 0.02, 0.012), Vector3(w / 2.0 - 0.07, 0.06 + door_h - 0.08 - sy * 0.36, d / 2.0 + 0.008), trim, 0.004, Vector3.ZERO, true)


static func _stove(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var shell := _mat("metal", _color(st, "metal", "c9ccd1"))
	var dark := _mat("metal", _color(st, "accent", "2b2e33"))
	var glass := _mat("screen", Color(0.08, 0.09, 0.1))
	var ring := _mat("metal", _color(st, "accent", "2b2e33").lightened(0.30))
	var w := s.x
	var d := s.z
	var h := s.y
	_box(b, Vector3(w - 0.04, 0.06, d - 0.04), Vector3(0, 0.03, 0), dark, 0.006)
	_box(b, Vector3(w, h - 0.06 - 0.03, d), Vector3(0, 0.06 + (h - 0.09) / 2.0, 0), shell, 0.016)
	_box(b, Vector3(w, 0.03, d), Vector3(0, h - 0.015, 0), dark, 0.008)
	_box(b, Vector3(w, 0.06, 0.04), Vector3(0, h + 0.0, -d / 2.0 + 0.02), dark, 0.008)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			var at := Vector3(sx * 0.14, h + 0.004, sz * 0.14 - 0.01)
			_cyl(b, 0.07, 0.07, 0.012, at, ring, Vector3.ZERO, Vector3.ONE, true)
			_cyl(b, 0.035, 0.035, 0.016, at + Vector3(0, 0.004, 0), dark, Vector3.ZERO, Vector3.ONE, true)
	_box(b, Vector3(w - 0.02, 0.08, 0.02), Vector3(0, h - 0.10, d / 2.0 - 0.005), dark, 0.006)
	for i in 4:
		_cyl(b, 0.022, 0.022, 0.026, Vector3(-0.2 + i * 0.133, h - 0.10, d / 2.0 + 0.016), shell, Vector3(90, 0, 0), Vector3.ONE, true)
	_box(b, Vector3(w - 0.06, 0.52, 0.03), Vector3(0, 0.38, d / 2.0 + 0.002), shell, 0.014)
	_box(b, Vector3(w - 0.18, 0.26, 0.012), Vector3(0, 0.38, d / 2.0 + 0.021), glass, 0.004, Vector3.ZERO, true)
	_box(b, Vector3(w - 0.14, 0.024, 0.024), Vector3(0, 0.65, d / 2.0 + 0.045), dark, 0.006, Vector3.ZERO, true)
	for sx in [-1.0, 1.0]:
		_box(b, Vector3(0.02, 0.02, 0.03), Vector3(sx * (w / 2.0 - 0.07), 0.65, d / 2.0 + 0.03), dark, 0.004, Vector3.ZERO, true)


static func _sink(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var ceramic := _mat("ceramic", _color(st, "ceramic", "f1f1ee"))
	var basin := _mat("ceramic", _color(st, "ceramic", "f1f1ee").darkened(0.07))
	var wood := _mat("wood", _color(st, "wood", "b9a58a"))
	var door := _mat("wood", _color(st, "wood", "b9a58a").lightened(0.1))
	var metal := _mat("metal", _color(st, "metal", "c9ccd1"))
	var w := s.x
	var d := s.z
	_box(b, Vector3(w, 0.72, d), Vector3(0, 0.36, 0), wood, 0.016)
	for sx in [-1.0, 1.0]:
		_box(b, Vector3(w / 2.0 - 0.03, 0.60, 0.014), Vector3(sx * (w / 4.0), 0.37, d / 2.0 + 0.004), door, 0.006, Vector3.ZERO, true)
		_cyl(b, 0.012, 0.012, 0.02, Vector3(sx * 0.03, 0.55, d / 2.0 + 0.018), metal, Vector3(90, 0, 0), Vector3.ONE, true)
	_box(b, Vector3(w + 0.02, 0.04, d + 0.02), Vector3(0, 0.74, 0), ceramic, 0.012)
	_box(b, Vector3(0.40, 0.012, 0.28), Vector3(0, 0.766, 0.03), basin, 0.005, Vector3.ZERO, true)
	_box(b, Vector3(w + 0.02, 0.12, 0.02), Vector3(0, 0.82, -d / 2.0 + 0.01), ceramic, 0.006)
	_cyl(b, 0.02, 0.02, 0.07, Vector3(0, 0.805, -d / 2.0 + 0.07), metal, Vector3.ZERO, Vector3.ONE, true)
	_box(b, Vector3(0.02, 0.02, 0.11), Vector3(0, 0.85, -d / 2.0 + 0.12), metal, 0.005, Vector3.ZERO, true)


static func _shower(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var ceramic := _mat("ceramic", _color(st, "ceramic", "f1f1ee"))
	var glass := _mat("glass", _color(st, "glass", "cfe3ee"))
	var metal := _mat("metal", _color(st, "metal", "c9ccd1"))
	var dark := _mat("plastic", Color(0.12, 0.13, 0.14))
	var w := s.x
	var d := s.z
	var top := s.y - 0.06
	_box(b, Vector3(w, 0.08, d), Vector3(0, 0.04, 0), ceramic, 0.016)
	_cyl(b, 0.04, 0.04, 0.006, Vector3(0, 0.083, 0.05), dark, Vector3.ZERO, Vector3.ONE, true)
	_box(b, Vector3(w, top - 0.08, 0.02), Vector3(0, 0.08 + (top - 0.08) / 2.0, d / 2.0 - 0.01), glass, 0.004)
	_box(b, Vector3(0.02, top - 0.08, d), Vector3(w / 2.0 - 0.01, 0.08 + (top - 0.08) / 2.0, 0), glass, 0.004)
	for at in [Vector2(-w / 2.0 + 0.015, d / 2.0 - 0.015), Vector2(w / 2.0 - 0.015, d / 2.0 - 0.015), Vector2(w / 2.0 - 0.015, -d / 2.0 + 0.015)]:
		_box(b, Vector3(0.03, top, 0.03), Vector3(at.x, top / 2.0, at.y), metal, 0.006)
	_box(b, Vector3(w, 0.03, 0.03), Vector3(0, top - 0.015, d / 2.0 - 0.015), metal, 0.006)
	_box(b, Vector3(0.03, 0.03, d), Vector3(w / 2.0 - 0.015, top - 0.015, 0), metal, 0.006)
	_cyl(b, 0.012, 0.012, 0.26, Vector3(0.12, 1.0, d / 2.0 + 0.03), metal, Vector3.ZERO, Vector3.ONE, true)
	_cyl(b, 0.012, 0.012, s.y - 0.30, Vector3(-w / 2.0 + 0.12, 0.08 + (s.y - 0.30) / 2.0, -d / 2.0 + 0.06), metal, Vector3.ZERO, Vector3.ONE, true)
	_box(b, Vector3(0.02, 0.02, 0.24), Vector3(-w / 2.0 + 0.12, s.y - 0.20, -d / 2.0 + 0.17), metal, 0.005, Vector3.ZERO, true)
	_cyl(b, 0.085, 0.085, 0.02, Vector3(-w / 2.0 + 0.12, s.y - 0.215, -d / 2.0 + 0.27), metal, Vector3(8, 0, 0), Vector3.ONE, true)
	_box(b, Vector3(0.10, 0.06, 0.04), Vector3(-w / 2.0 + 0.12, 1.05, -d / 2.0 + 0.05), metal, 0.008, Vector3.ZERO, true)


static func _toilet(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var ceramic := _mat("ceramic", _color(st, "ceramic", "f1f1ee"))
	var seat := _mat("ceramic", _color(st, "ceramic", "f1f1ee").darkened(0.04))
	var water := _mat("glass", Color(0.45, 0.62, 0.78))
	var metal := _mat("metal", Color(0.78, 0.8, 0.82))
	var w := s.x
	var d := s.z
	_box(b, Vector3(w, 0.37, 0.19), Vector3(0, 0.575, -d / 2.0 + 0.095), ceramic, 0.02)
	_box(b, Vector3(w + 0.012, 0.02, 0.20), Vector3(0, 0.77, -d / 2.0 + 0.10), seat, 0.008)
	_cyl(b, 0.028, 0.028, 0.012, Vector3(0.0, 0.786, -d / 2.0 + 0.10), metal, Vector3.ZERO, Vector3.ONE, true)
	_box(b, Vector3(0.20, 0.22, 0.30), Vector3(0, 0.11, -0.03), ceramic, 0.03)
	_cyl(b, 0.185, 0.16, 0.22, Vector3(0, 0.32, 0.055), ceramic, Vector3.ZERO, Vector3(1, 1, 1.4))
	_cyl(b, 0.19, 0.19, 0.03, Vector3(0, 0.44, 0.055), seat, Vector3.ZERO, Vector3(1, 1, 1.4))
	_cyl(b, 0.115, 0.115, 0.006, Vector3(0, 0.458, 0.075), water, Vector3.ZERO, Vector3(1, 1, 1.3), true)


static func _tv(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "3b3d42"))
	var panel := _mat("wood", _color(st, "wood", "3b3d42").lightened(0.1))
	var frame := _mat("plastic", Color(0.06, 0.065, 0.075))
	var screen := _mat("screen", _color(st, "screen", "1a1c20"))
	var w := s.x
	var d := s.z
	_box(b, Vector3(w, 0.44, d), Vector3(0, 0.22, 0), wood, 0.018)
	for sx in [-1.0, 1.0]:
		_box(b, Vector3(w / 2.0 - 0.07, 0.34, 0.012), Vector3(sx * (w / 4.0), 0.22, d / 2.0 + 0.004), panel, 0.006, Vector3.ZERO, true)
		_cyl(b, 0.012, 0.012, 0.02, Vector3(sx * 0.04, 0.30, d / 2.0 + 0.016), frame, Vector3(90, 0, 0), Vector3.ONE, true)
	_box(b, Vector3(0.36, 0.02, 0.18), Vector3(0, 0.45, 0), frame, 0.006)
	_box(b, Vector3(0.07, 0.06, 0.04), Vector3(0, 0.50, 0), frame, 0.006)
	_box(b, Vector3(0.98, 0.56, 0.035), Vector3(0, 0.78, 0.0), frame, 0.012)
	_box(b, Vector3(0.93, 0.51, 0.008), Vector3(0, 0.78, 0.0185), screen, 0.003)
	_box(b, Vector3(0.50, 0.36, 0.05), Vector3(0, 0.78, -0.04), frame, 0.01, Vector3.ZERO, true)


static func _lamp(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "4a3f35"))
	var shade := _mat("fabric", _color(st, "fabric", "f3dca0", v), 0.22)
	var metal := _mat("metal", Color(0.72, 0.68, 0.6))
	var w := s.x
	var d := s.z
	_box(b, Vector3(w - 0.02, 0.03, d - 0.02), Vector3(0, 0.45, 0), wood, 0.01)
	_box(b, Vector3(w - 0.10, 0.10, d - 0.10), Vector3(0, 0.385, 0), wood, 0.01)
	_box(b, Vector3(0.14, 0.024, 0.01), Vector3(0, 0.385, d / 2.0 - 0.05), metal, 0.004, Vector3.ZERO, true)
	for sx in [-1.0, 1.0]:
		for sz in [-1.0, 1.0]:
			_box(b, Vector3(0.035, 0.42, 0.035), Vector3(sx * (w / 2.0 - 0.04), 0.21, sz * (d / 2.0 - 0.04)), wood, 0.006)
	_cyl(b, 0.075, 0.085, 0.025, Vector3(0, 0.4725, 0), metal)
	_cyl(b, 0.012, 0.012, 0.22, Vector3(0, 0.595, 0), metal)
	_cyl(b, 0.09, 0.15, 0.22, Vector3(0, 0.82, 0), shade)
	_ball(b, 0.016, Vector3(0, 0.945, 0), metal, Vector3.ONE, Vector3.ZERO, true)


static func _floorlamp(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var metal := _mat("metal", _color(st, "metal", "2f2f35"))
	var shade := _mat("fabric", _color(st, "fabric", "f6e6b4", v), 0.22)
	_cyl(b, 0.17, 0.18, 0.03, Vector3(0, 0.015, 0), metal)
	_cyl(b, 0.014, 0.014, 1.30, Vector3(0, 0.68, 0), metal)
	_cyl(b, 0.13, 0.19, 0.30, Vector3(0, s.y - 0.18, 0), shade)
	_cyl(b, 0.02, 0.02, 0.05, Vector3(0, s.y - 0.025, 0), metal, Vector3.ZERO, Vector3.ONE, true)


static func _plant(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var pot := _mat("wood", _color(st, "pot", "9a5b3c"))
	var soil := _mat("plastic", Color(0.18, 0.12, 0.08))
	var leaf := _color(st, "leaf", "3f7d3a")
	_cyl(b, 0.17, 0.12, 0.30, Vector3(0, 0.15, 0), pot)
	_cyl(b, 0.185, 0.185, 0.03, Vector3(0, 0.315, 0), pot)
	_cyl(b, 0.165, 0.165, 0.01, Vector3(0, 0.32, 0), soil, Vector3.ZERO, Vector3.ONE, true)
	var tones := [_mat("leaf", leaf), _mat("leaf", leaf.lightened(0.14)), _mat("leaf", leaf.darkened(0.16))]
	for i in 10:
		var yaw := deg_to_rad(i * 36.0 + (i % 3) * 7.0)
		var lean := deg_to_rad(18.0 + (i % 4) * 9.0)
		var length := 0.60 + 0.07 * (i % 3)
		var basis := Basis(Vector3.UP, yaw) * Basis(Vector3(0, 0, 1), -lean)
		var base := Basis(Vector3.UP, yaw) * Vector3(0.04, 0.33, 0)
		_part(b, _leaf_mesh(), tones[i % 3], Transform3D(basis.scaled(Vector3(0.9, length, 0.5)), base + basis * Vector3(0, length * 0.5, 0)), true)
	_ball(b, 0.17, Vector3(0.0, 0.80, 0.0), tones[0], Vector3(1.0, 0.85, 1.0))
	_ball(b, 0.13, Vector3(0.11, 0.66, 0.06), tones[1], Vector3(1.0, 0.8, 1.0))
	_ball(b, 0.12, Vector3(-0.10, 0.70, -0.07), tones[2], Vector3(1.0, 0.8, 1.0))


static func _leaf_mesh() -> Mesh:
	if not _meshes.has("leaf"):
		var mesh := SphereMesh.new()
		mesh.radius = 0.1
		mesh.height = 0.2
		mesh.radial_segments = 8
		mesh.rings = 4
		_meshes["leaf"] = mesh
		meshes_built += 1
	return _meshes["leaf"]


static func _bookshelf(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "6b4a2c"))
	var dark := _mat("wood", _color(st, "wood", "6b4a2c").darkened(0.25))
	var accent := _color(st, "accent", "b5523f")
	var w := s.x
	var d := s.z
	var h := s.y
	for sx in [-1.0, 1.0]:
		_box(b, Vector3(0.03, h, d), Vector3(sx * (w / 2.0 - 0.015), h / 2.0, 0), wood, 0.008)
	_box(b, Vector3(w - 0.06, h - 0.1, 0.012), Vector3(0, 0.08 + (h - 0.1) / 2.0, -d / 2.0 + 0.006), dark, 0.002)
	_box(b, Vector3(w, 0.03, d), Vector3(0, h - 0.015, 0), wood, 0.008)
	_box(b, Vector3(w - 0.06, 0.08, d - 0.02), Vector3(0, 0.04, 0.0), dark, 0.008)
	var inner := w - 0.06
	var floors := [0.08]
	for k in range(1, 5):
		floors.append(0.08 + (h - 0.11) * k / 5.0)
		_box(b, Vector3(inner, 0.025, d - 0.03), Vector3(0, floors[k], 0.0), wood, 0.006)
	floors.append(h - 0.03)
	for shelf in 5:
		var low: float = float(floors[shelf]) + (0.0125 if shelf > 0 else 0.0)
		var room: float = float(floors[shelf + 1]) - 0.0125 - low
		var x := -inner / 2.0 + 0.02
		var n := 0
		while x < inner / 2.0 - 0.05 and (shelf != 4 or x < 0.0):
			var salt := shelf * 31 + n * 17
			var width := 0.032 + 0.012 * (salt % 4)
			var height := room * (0.62 + 0.09 * ((salt / 3) % 4))
			var hue := fposmod(accent.h + 0.11 * ((salt / 5) % 6), 1.0)
			var book := _mat("fabric", Color.from_hsv(hue, 0.42 + 0.1 * (salt % 3), 0.58 + 0.08 * ((salt / 2) % 3)))
			_box(b, Vector3(width, snappedf(height, 0.02), d * 0.66), Vector3(x + width / 2.0, low + snappedf(height, 0.02) / 2.0, -d / 2.0 + 0.03 + d * 0.33), book, 0.004, Vector3.ZERO, true)
			x += width + 0.004
			n += 1


static func _rug(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var base := _mat("fabric", _color(st, "fabric", "a85a4a", v))
	var field := _mat("fabric", _color(st, "accent", "d9b88f", v))
	var band := _mat("fabric", _color(st, "fabric", "a85a4a", v).darkened(0.18))
	_box(b, Vector3(s.x, 0.012, s.z), Vector3(0, 0.006, 0), base, 0.004)
	_box(b, Vector3(s.x - 0.14, 0.014, s.z - 0.14), Vector3(0, 0.007, 0), field, 0.003)
	_box(b, Vector3(s.x - 0.30, 0.016, s.z - 0.30), Vector3(0, 0.008, 0), band, 0.003)
	_box(b, Vector3(s.x - 0.42, 0.018, s.z - 0.42), Vector3(0, 0.009, 0), field, 0.003)


static func _painting(b: Node3D, s: Vector3, st: Dictionary, v: int) -> void:
	var wood := _mat("wood", _color(st, "wood", "5a4630"))
	var accent := _color(st, "accent", "4d7ea8")
	var sky := _mat("fabric", accent.lightened(0.12))
	var hill := _mat("fabric", Color.from_hsv(fposmod(accent.h - 0.22, 1.0), 0.45, 0.5))
	var far := _mat("fabric", accent.darkened(0.22))
	var sun := _mat("fabric", Color.from_hsv(0.12, 0.45, 0.98), 0.4)
	var w := s.x
	var z := -s.z / 2.0
	_box(b, Vector3(w, 0.62, 0.04), Vector3(0, 1.45, z + 0.02), wood, 0.01)
	_box(b, Vector3(w - 0.10, 0.52, 0.014), Vector3(0, 1.45, z + 0.045), sky, 0.002)
	_box(b, Vector3(w - 0.10, 0.17, 0.016), Vector3(0, 1.45 - 0.175, z + 0.046), hill, 0.002)
	_box(b, Vector3(w * 0.5, 0.13, 0.018), Vector3(-0.12, 1.45 - 0.07, z + 0.047), far, 0.002, Vector3(0, 0, 4))
	_cyl(b, 0.05, 0.05, 0.018, Vector3(0.22, 1.60, z + 0.047), sun, Vector3(90, 0, 0), Vector3.ONE, true)
