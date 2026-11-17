extends Node3D
## Prédios da cidade (paredes, telhado, janelas, porta, poste, chaminé). Um nó por prédio; delta de prédio novo ou
## movido redesenha só aquele (sem recarregar a cidade). Lê a WorldStore; nunca fala com o servidor.

const Grid := preload("res://Scenes/grid.gd")
const Scenery := preload("res://Scenes/scenery.gd")
const Dress := preload("res://Scenes/render/building_dress.gd")

const MATERIAL_WALL_STONE := 0
const MATERIAL_WALL_WOOD := 1
const MATERIAL_DOOR := 2
const FLOOR_HEIGHT := 1.0
const PLACEHOLDER_FOOTPRINT := [Vector2i(0, 0), Vector2i(1, 0), Vector2i(0, 1), Vector2i(1, 1)]

var nodes: Dictionary = {} ## id -> Node3D
var dressed: Dictionary = {} ## id -> assinatura do estado desenhado (estilo, placa, fechado): só redesenha se mudar


func rebuild(store) -> void:
	for node in nodes.values():
		_dispose(node)
	nodes.clear()
	if store.kind != "City":
		return
	for id in store.buildings:
		upsert(store.buildings[id], store.building_states.get(id, {}))


## Desenha (ou redesenha) um prédio. Sem planta (veio por delta e o snapshot ainda não chegou), usa um canteiro
## de obra 2x2 de madeira no lugar certo até a planta chegar.
static func signature(state: Dictionary) -> String:
	return "%s|%s|%s" % [Dress.style_of(state), Dress.sign_text(state), bool(state.get("closed", false))]


## Estado novo do prédio (função, placa, fechado) só redesenha quando a aparência muda (dono e estoque não mudam a forma).
func restyle(info: Dictionary, state: Dictionary) -> void:
	if String(dressed.get(int(info.id), "")) != signature(state):
		upsert(info, state)


func upsert(info: Dictionary, state: Dictionary = {}) -> Node3D:
	remove(int(info.id))
	dressed[int(info.id)] = signature(state)
	var drawn := info
	if info.footprint.is_empty():
		drawn = info.duplicate()
		drawn.footprint = []
		for cell in PLACEHOLDER_FOOTPRINT:
			drawn.footprint.append({"cell": cell, "material": MATERIAL_DOOR if cell == Vector2i.ZERO else MATERIAL_WALL_WOOD})
	var node := make_building(drawn, state)
	add_child(node)
	nodes[int(info.id)] = node
	return node


func remove(id: int) -> void:
	dressed.erase(id)
	if nodes.has(id):
		_dispose(nodes[id])
		nodes.erase(id)


func _dispose(node: Node) -> void:
	if node.is_inside_tree():
		node.queue_free()
	else:
		node.free()


const WALL_COLORS := {
	0: Color("a9a399"), # pedra
	1: Color("b88f62"), # madeira
}
const ROOF_COLORS: Array[Color] = [Color("a14f3d"), Color("6b5b73"), Color("7d6a45"), Color("8c4a32")]
const DIRS: Array[Vector2i] = [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]

var window_material: StandardMaterial3D = _make_window_material()
var lamp_material: StandardMaterial3D = _make_lamp_material()


func _make_lamp_material() -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = Color("fff1c4")
	material.emission_enabled = true
	material.emission = Color("ffc860")
	material.emission_energy_multiplier = 0.0
	return material


func _make_window_material() -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = Color("2b3a4a")
	material.emission_enabled = true
	material.emission = Color("ffd27a")
	material.emission_energy_multiplier = 0.0
	material.roughness = 0.2
	return material



func make_building(info: Dictionary, state: Dictionary = {}) -> Node3D:
	var root := Node3D.new()
	root.name = "Building_%d" % info.id
	root.set_meta("building_id", info.id)
	var floors: int = info.floors
	var height: float = floors * FLOOR_HEIGHT
	var cells := {}
	for entry in info.footprint:
		cells[info.cell + entry.cell] = entry.material
	var roof_color: Color = ROOF_COLORS[info.type % ROOF_COLORS.size()] if info.type >= 0 else ROOF_COLORS[0]
	var trim := _flat_material(Color("5d4a3a"))
	var door := _flat_material(Color("3b2a20"))
	var step := _flat_material(Color("8d8579"))
	var wall_materials := {0: _flat_material(WALL_COLORS[0]), 1: _flat_material(WALL_COLORS[1])}
	var min_cell := Vector2i(1 << 20, 1 << 20)
	var max_cell := Vector2i(-(1 << 20), -(1 << 20))
	var door_at := Vector3.INF
	var door_out := Vector3(0, 0, 1)
	for cell in cells:
		min_cell = Vector2i(mini(min_cell.x, cell.x), mini(min_cell.y, cell.y))
		max_cell = Vector2i(maxi(max_cell.x, cell.x), maxi(max_cell.y, cell.y))
		var material: int = cells[cell]
		match material:
			MATERIAL_WALL_STONE, MATERIAL_WALL_WOOD:
				_box(root, Vector3(1.0, height, 1.0), Grid.to_world(cell, height / 2.0), wall_materials[material])
				_box(root, Vector3(1.04, 0.12, 1.04), Grid.to_world(cell, 0.06), trim) # rodapé
				for level in range(1, floors):
					_box(root, Vector3(1.03, 0.07, 1.03), Grid.to_world(cell, level * FLOOR_HEIGHT), trim) # friso entre andares
				for dir in DIRS:
					if cells.has(cell + dir):
						continue
					for level in floors:
						if Scenery.cell_hash(cell, level * 4 + DIRS.find(dir) + 11) < 0.5:
							var outward := Vector3(dir.x, 0, dir.y) * 0.505
							var window := _box(root, Vector3(0.36 if dir.x == 0 else 0.04, 0.36, 0.36 if dir.y == 0 else 0.04),
								Grid.to_world(cell, level * FLOOR_HEIGHT + 0.58) + outward, window_material)
							window.name = "Window"
			MATERIAL_DOOR:
				_box(root, Vector3(1.0, height, 1.0), Grid.to_world(cell, height / 2.0), wall_materials[0])
				for dir in DIRS:
					if not cells.has(cell + dir):
						var outward := Vector3(dir.x, 0, dir.y)
						if door_at == Vector3.INF:
							door_at = Grid.to_world(cell) + outward * 0.5
							door_out = outward
						var size := Vector3(0.56 if dir.x == 0 else 0.1, 0.78, 0.56 if dir.y == 0 else 0.1)
						_box(root, size, Grid.to_world(cell, 0.39) + outward * 0.5, door).name = "Door"
						_box(root, Vector3(0.9 if dir.x == 0 else 0.3, 0.06, 0.9 if dir.y == 0 else 0.3),
							Grid.to_world(cell, 0.03) + outward * 0.78, step)
						_lamp_post(root, Grid.to_world(cell) + outward * 1.15 + Vector3(outward.z, 0, outward.x) * 0.5)
			_:
				pass
	var span := max_cell - min_cell + Vector2i.ONE
	var rectangular: bool = cells.size() == span.x * span.y
	var roof := _flat_material(roof_color)
	if rectangular:
		var prism := PrismMesh.new()
		var along_x := span.x >= span.y
		prism.size = Vector3((span.x if along_x else span.y) + 0.4, 0.75, (span.y if along_x else span.x) + 0.4)
		var ridge := MeshInstance3D.new()
		ridge.mesh = prism
		ridge.material_override = roof
		ridge.name = "Roof"
		ridge.position = Vector3(min_cell.x + span.x / 2.0, height + 0.375, min_cell.y + span.y / 2.0)
		if not along_x:
			ridge.rotation.y = PI / 2.0
		root.add_child(ridge)
	else:
		for cell in cells:
			var pyramid := CylinderMesh.new()
			pyramid.top_radius = 0.0
			pyramid.bottom_radius = 0.78
			pyramid.height = 0.62
			pyramid.radial_segments = 4
			var node := MeshInstance3D.new()
			node.mesh = pyramid
			node.material_override = roof
			node.rotation.y = PI / 4.0
			node.position = Grid.to_world(cell, height + 0.31)
			node.name = "Roof"
			root.add_child(node)
	if info.type == -1:
		var chimney_cell: Vector2i = min_cell
		_box(root, Vector3(0.22, 0.7, 0.22), Grid.to_world(chimney_cell, height + 0.55) + Vector3(0.1, 0, 0.1), _flat_material(Color("7a6a5c")))
		root.add_child(_smoke(Grid.to_world(chimney_cell, height + 0.95) + Vector3(0.1, 0, 0.1)))
	if not state.is_empty():
		var center := Vector3(min_cell.x + span.x / 2.0, height, min_cell.y + span.y / 2.0)
		if door_at == Vector3.INF:
			door_at = Vector3(center.x, 0, max_cell.y + 1.0)
		var dress_state := state
		if info.type == -1 and Dress.style_of(state) == "dwelling":
			dress_state = state.duplicate()
			dress_state.function = "" # a casa padrão já tem chaminé com fumaça
		Dress.dress(root, dress_state, Vector3(min_cell.x + 0.5, height, min_cell.y + 0.5), door_at, door_out, height)
	var body := StaticBody3D.new()
	body.set_meta("building_id", info.id)
	var shape := CollisionShape3D.new()
	var collider := BoxShape3D.new()
	collider.size = Vector3(span.x, height + 0.6, span.y)
	shape.shape = collider
	shape.position = Vector3(min_cell.x + span.x / 2.0, (height + 0.6) / 2.0, min_cell.y + span.y / 2.0)
	body.add_child(shape)
	root.add_child(body)
	return root


func _lamp_post(parent: Node3D, at: Vector3) -> void:
	var post := MeshInstance3D.new()
	var pole := CylinderMesh.new()
	pole.top_radius = 0.025
	pole.bottom_radius = 0.035
	pole.height = 0.85
	post.mesh = pole
	post.material_override = _flat_material(Color("3d3a36"))
	post.position = at + Vector3(0, 0.425, 0)
	post.name = "LampPost"
	parent.add_child(post)
	var bulb := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.1
	sphere.height = 0.2
	bulb.mesh = sphere
	bulb.material_override = lamp_material
	bulb.position = at + Vector3(0, 0.9, 0)
	bulb.name = "Lamp"
	parent.add_child(bulb)


func _box(parent: Node3D, size: Vector3, at: Vector3, material: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	node.mesh = mesh
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node


func _smoke(at: Vector3) -> GPUParticles3D:
	var particles := GPUParticles3D.new()
	particles.name = "Smoke"
	particles.position = at
	particles.amount = 12
	particles.lifetime = 3.5
	particles.local_coords = false
	var process := ParticleProcessMaterial.new()
	process.direction = Vector3(0.3, 1, 0.1)
	process.spread = 12.0
	process.initial_velocity_min = 0.25
	process.initial_velocity_max = 0.45
	process.gravity = Vector3(0.12, 0.05, 0)
	process.scale_min = 0.5
	process.scale_max = 1.1
	var curve := Curve.new()
	curve.add_point(Vector2(0, 0.3))
	curve.add_point(Vector2(1, 1.6))
	var scale_texture := CurveTexture.new()
	scale_texture.curve = curve
	process.scale_curve = scale_texture
	var fade := Gradient.new()
	fade.set_color(0, Color(0.85, 0.85, 0.85, 0.55))
	fade.set_color(1, Color(0.85, 0.85, 0.85, 0.0))
	var ramp := GradientTexture1D.new()
	ramp.gradient = fade
	process.color_ramp = ramp
	particles.process_material = process
	var puff := SphereMesh.new()
	puff.radius = 0.12
	puff.height = 0.24
	puff.radial_segments = 8
	puff.rings = 4
	var puff_material := StandardMaterial3D.new()
	puff_material.vertex_color_use_as_albedo = true
	puff_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	puff_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	puff.material = puff_material
	particles.draw_pass_1 = puff
	return particles


func _flat_material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.9
	return material
