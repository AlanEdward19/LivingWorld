extends RefCounted
## Canteiro de obra (AD-193): o prédio que o motor ainda não ergueu, desenhado pelo que a obra tem. Alicerce na planta do lote,
## paredes que sobem com a fração feita (só as de fora; a porta fica aberta), andaime enquanto a obra não acaba, a armação do
## telhado no último quinto e, na obra da fila da cidade, o material já carregado ao lote e ainda não posto. Nada é inventado:
## sem fração, sem material contado ou sem planta, não aparece.

const Grid := preload("res://Scenes/grid.gd")
const Works := preload("res://Store/work_book.gd")

const MATERIAL_DOOR := 2
const DIRS: Array[Vector2i] = [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]
const WALL := 0.16 ## espessura da parede em obra
const ROOF_FROM := 0.8 ## a armação do telhado aparece no último quinto da obra

static var _materials: Dictionary = {}


static func material(color: Color) -> StandardMaterial3D:
	var key := color.to_html()
	if not _materials.has(key):
		var made := StandardMaterial3D.new()
		made.albedo_color = color
		made.roughness = 0.95
		_materials[key] = made
	return _materials[key]


static func make(info: Dictionary, work: Dictionary, floor_height: float) -> Node3D:
	var root := Node3D.new()
	root.name = "Site_%d" % int(info.id)
	root.set_meta("building_id", info.id)
	root.set_meta("work_id", int(work.get("id", -1)))
	root.set_meta("windows", [])
	root.set_meta("lit", false)
	var cells := {}
	for entry in info.footprint:
		cells[info.cell + entry.cell] = int(entry.material)
	var min_cell := Vector2i(1 << 20, 1 << 20)
	var max_cell := Vector2i(-(1 << 20), -(1 << 20))
	for cell in cells:
		min_cell = Vector2i(mini(min_cell.x, cell.x), mini(min_cell.y, cell.y))
		max_cell = Vector2i(maxi(max_cell.x, cell.x), maxi(max_cell.y, cell.y))
	var span := max_cell - min_cell + Vector2i.ONE
	var full: float = maxi(1, int(info.get("floors", 1))) * floor_height
	var done: float = float(Works.stage(work)) / float(Works.STAGES)
	var wall_h: float = full * done
	var raw_wall := material(Color("c2a57c"))
	var stone := material(Color("8d8579"))
	var timber := material(Color("7a5a3a"))
	var door_at := Vector3.INF
	var door_out := Vector3(0, 0, 1)
	for cell in cells:
		_box(root, Vector3(1.0, 0.08, 1.0), Grid.to_world(cell, 0.04), stone).name = "Foundation"
		for dir in DIRS:
			if cells.has(cell + dir):
				continue
			var outward := Vector3(dir.x, 0, dir.y)
			if int(cells[cell]) == MATERIAL_DOOR:
				if door_at == Vector3.INF:
					door_at = Grid.to_world(cell) + outward * 0.5
					door_out = outward
				continue # o vão da porta fica aberto
			if wall_h > 0.0:
				var size := Vector3(1.0 if dir.x == 0 else WALL, wall_h, 1.0 if dir.y == 0 else WALL)
				_box(root, size, Grid.to_world(cell, 0.08 + wall_h / 2.0) + outward * (0.5 - WALL / 2.0), raw_wall).name = "Wall"
	var top: float = 0.08 + wall_h
	if done < 1.0: # andaime nos cantos e tábua na altura da parede (a obra ainda anda)
		var x0 := float(min_cell.x) - 0.08
		var x1 := float(max_cell.x + 1) + 0.08
		var z0 := float(min_cell.y) - 0.08
		var z1 := float(max_cell.y + 1) + 0.08
		var pole_h := top + 0.55
		for corner in [Vector3(x0, 0, z0), Vector3(x1, 0, z0), Vector3(x0, 0, z1), Vector3(x1, 0, z1)]:
			_box(root, Vector3(0.06, pole_h, 0.06), corner + Vector3(0, pole_h / 2.0, 0), timber).name = "Scaffold"
		for z in [z0, z1]:
			_box(root, Vector3(x1 - x0, 0.05, 0.16), Vector3((x0 + x1) / 2.0, top + 0.05, z), timber).name = "Plank"
	if done >= ROOF_FROM: # armação do telhado: cumeeira e caibros, sem telha
		var along_x := span.x >= span.y
		var length := float(span.x if along_x else span.y)
		var width := float(span.y if along_x else span.x)
		var rise := 0.5 * (done - ROOF_FROM) / (1.0 - ROOF_FROM) + 0.15
		var center := Vector3(min_cell.x + span.x / 2.0, top, min_cell.y + span.y / 2.0)
		var ridge := _box(root, Vector3(length if along_x else 0.08, 0.08, 0.08 if along_x else length), center + Vector3(0, rise, 0), timber)
		ridge.name = "Ridge"
		var slope := atan2(rise, width / 2.0)
		for i in int(length) + 1:
			var t := -length / 2.0 + float(i)
			for side in [-1.0, 1.0]:
				var rafter := MeshInstance3D.new()
				var mesh := BoxMesh.new()
				mesh.size = Vector3(0.06, 0.06, sqrt(rise * rise + width * width / 4.0))
				rafter.mesh = mesh
				rafter.material_override = timber
				rafter.name = "Rafter"
				var offset := Vector3(t, rise / 2.0, side * width / 4.0) if along_x else Vector3(side * width / 4.0, rise / 2.0, t)
				rafter.position = center + offset
				if along_x:
					rafter.rotation.x = side * slope
				else:
					rafter.rotation = Vector3(0, PI / 2.0, side * -slope)
				root.add_child(rafter)
	var pile := Works.pile_fraction(work)
	if pile > 0.0: # material da fila já no lote e ainda não posto: a pilha encolhe à medida que vira parede
		if door_at == Vector3.INF:
			door_at = Vector3(min_cell.x + span.x / 2.0, 0, max_cell.y + 1.0)
		var side := Vector3(door_out.z, 0, door_out.x)
		var at := door_at + door_out * 0.55 + side * 0.6
		var logs := maxi(1, int(round(pile * 6.0)))
		for i in logs:
			var piece := _box(root, Vector3(0.62, 0.11, 0.11) if absf(door_out.x) < 0.5 else Vector3(0.11, 0.11, 0.62),
				at + Vector3(0, 0.06 + 0.11 * float(i / 3), 0) + side * (0.12 * float(i % 3) - 0.12), timber)
			piece.name = "Material"
	var body := StaticBody3D.new()
	body.set_meta("building_id", info.id)
	var shape := CollisionShape3D.new()
	var collider := BoxShape3D.new()
	var height := maxf(top, 0.3) + 0.6
	collider.size = Vector3(span.x, height, span.y)
	shape.shape = collider
	shape.position = Vector3(min_cell.x + span.x / 2.0, height / 2.0, min_cell.y + span.y / 2.0)
	body.add_child(shape)
	root.add_child(body)
	root.set_meta("box", AABB(Vector3(min_cell.x, 0, min_cell.y), Vector3(span.x, height, span.y)))
	return root


static func _box(parent: Node3D, size: Vector3, at: Vector3, mat: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	node.mesh = mesh
	node.material_override = mat
	node.position = at
	parent.add_child(node)
	return node
