extends Node3D
## Mundo e cidade em 3D isométrico: terreno, regiões de cidade, prédios e NPCs animados.
## Lê a WorldStore; reage aos sinais dela. Nunca fala com o servidor.

const Grid := preload("res://Scenes/grid.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Scenery := preload("res://Scenes/scenery.gd")

const TERRAIN_COLORS: Array[Color] = [
	Color("6aa84f"), Color("c9b777"), Color("7a8a5a"), Color("8a8f98"), Color("4a7fa8"), Color("a67c52"),
]
const MATERIAL_WALL_STONE := 0
const MATERIAL_WALL_WOOD := 1
const MATERIAL_DOOR := 2
const MATERIAL_FLOOR := 3
const MATERIAL_STAIR := 4
const FLOOR_HEIGHT := 1.0
const STEP_SECONDS := 1.0 ## duração visual de um passo; o servidor decide a posição, o cliente só interpola

var store
var labels

var terrain_root: Node3D
var scenery
var city_nodes: Dictionary = {} ## id -> Node3D
var npc_nodes: Dictionary = {} ## id -> NpcActor
var building_nodes: Dictionary = {} ## id -> Node3D
var _selected_npc := -1


func bind(p_store, p_labels) -> void:
	store = p_store
	labels = p_labels
	store.reloaded.connect(_on_reloaded)
	store.npc_changed.connect(_on_npc_changed)
	store.npc_removed.connect(_on_npc_removed)
	store.cities_changed.connect(_rebuild_cities)


func _on_reloaded(kind: String) -> void:
	if kind == "Interior":
		return
	_rebuild_terrain()
	_rebuild_cities()
	_rebuild_buildings()
	_rebuild_npcs()


func _dispose(node: Node) -> void:
	if node.is_inside_tree():
		node.queue_free()
	else:
		node.free()


func _clear(container: Dictionary) -> void:
	for node in container.values():
		_dispose(node)
	container.clear()


func _rebuild_terrain() -> void:
	if terrain_root != null:
		_dispose(terrain_root)
		terrain_root = null
	if store.terrain.is_empty():
		return
	terrain_root = Node3D.new()
	terrain_root.name = "Terrain"
	add_child(terrain_root)
	var mesh := BoxMesh.new()
	mesh.size = Vector3(Grid.CELL, 0.3, Grid.CELL)
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.use_colors = true
	multi.mesh = mesh
	multi.instance_count = store.terrain.size()
	var index := 0
	for cell in store.terrain:
		var id: int = store.terrain[cell]
		var bed_drop := -0.13 if store.rivers.has(cell) else 0.0
		multi.set_instance_transform(index, Transform3D(Basis(), Grid.to_world(cell, -0.15 + bed_drop)))
		var shade := 0.94 + float((cell.x * 7 + cell.y * 13) % 5) * 0.03
		var color: Color = TERRAIN_COLORS[(id - 1) % TERRAIN_COLORS.size()] * shade
		if store.rivers.has(cell):
			color = Color("6d7f5a") * shade # leito sob a água
		multi.set_instance_color(index, color)
		index += 1
	var instance := MultiMeshInstance3D.new()
	instance.multimesh = multi
	var material := StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.roughness = 0.95
	instance.material_override = material
	terrain_root.add_child(instance)
	var picker := StaticBody3D.new()
	picker.set_meta("ground", true)
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(store.map_size.x, 0.3, store.map_size.y)
	shape.shape = box
	shape.position = Vector3(store.map_size.x / 2.0, -0.15, store.map_size.y / 2.0)
	picker.add_child(shape)
	terrain_root.add_child(picker)
	var rects: Array = []
	for id in store.cities:
		rects.append(store.cities[id].bounds)
	scenery = Scenery.new()
	scenery.name = "Scenery"
	terrain_root.add_child(scenery)
	scenery.build(store.terrain, store.rivers, rects)


func _rebuild_cities() -> void:
	_clear(city_nodes)
	if store.kind != "World":
		return
	for id in store.cities:
		var info: Dictionary = store.cities[id]
		var root := Node3D.new()
		root.name = "City_%s" % str(id).left(8)
		root.set_meta("city_id", id)
		var bounds: Rect2i = info.bounds
		var plane := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(bounds.size.x, 0.06, bounds.size.y)
		plane.mesh = box
		var material := StandardMaterial3D.new()
		material.albedo_color = Color(1.0, 0.8, 0.3, 0.35)
		material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		plane.material_override = material
		plane.position = Vector3(bounds.position.x + bounds.size.x / 2.0, 0.03, bounds.position.y + bounds.size.y / 2.0)
		root.add_child(plane)
		var label := Label3D.new()
		label.text = "%s\n%d hab." % [info.name if info.name != "" else "Cidade", info.population]
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		label.pixel_size = 0.012
		label.font_size = 32
		label.no_depth_test = true
		label.position = Vector3(info.location.x + 0.5, 1.6, info.location.y + 0.5)
		root.add_child(label)
		var body := StaticBody3D.new()
		body.set_meta("city_id", id)
		var shape := CollisionShape3D.new()
		var collider := BoxShape3D.new()
		collider.size = Vector3(bounds.size.x, 1.0, bounds.size.y)
		shape.shape = collider
		shape.position = plane.position + Vector3(0, 0.5, 0)
		body.add_child(shape)
		root.add_child(body)
		root.position = Vector3.ZERO
		add_child(root)
		city_nodes[id] = root


func _rebuild_buildings() -> void:
	_clear(building_nodes)
	if store.kind != "City":
		return
	for id in store.buildings:
		var node := _make_building(store.buildings[id])
		add_child(node)
		building_nodes[id] = node


const WALL_COLORS := {
	0: Color("a9a399"), # pedra
	1: Color("b88f62"), # madeira
}
const ROOF_COLORS: Array[Color] = [Color("a14f3d"), Color("6b5b73"), Color("7d6a45"), Color("8c4a32")]
const DIRS: Array[Vector2i] = [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]

var window_material: StandardMaterial3D = _make_window_material()


func _make_window_material() -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = Color("2b3a4a")
	material.emission_enabled = true
	material.emission = Color("ffd27a")
	material.emission_energy_multiplier = 0.0
	material.roughness = 0.2
	return material


func _make_building(info: Dictionary) -> Node3D:
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
						var size := Vector3(0.56 if dir.x == 0 else 0.1, 0.78, 0.56 if dir.y == 0 else 0.1)
						_box(root, size, Grid.to_world(cell, 0.39) + outward * 0.5, door).name = "Door"
						_box(root, Vector3(0.9 if dir.x == 0 else 0.3, 0.06, 0.9 if dir.y == 0 else 0.3),
							Grid.to_world(cell, 0.03) + outward * 0.78, step)
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


func _rebuild_npcs() -> void:
	_clear(npc_nodes)
	for id in store.npcs:
		var info: Dictionary = store.npcs[id]
		_spawn_npc(info)


func _spawn_npc(info: Dictionary) -> void:
	var actor := NpcActor.new()
	add_child(actor)
	actor.setup(info.id, info.cell, info.action, labels)
	actor.set_selected(info.id == _selected_npc)
	if store.names.has(info.id):
		actor.set_display_name(store.names[info.id])
	npc_nodes[info.id] = actor


func _on_npc_changed(id: int, from_cell: Vector2i, to_cell: Vector2i, action: int) -> void:
	var actor = npc_nodes.get(id)
	if actor == null:
		_spawn_npc(store.npcs[id])
		return
	if action != actor.action:
		actor.set_action(action, labels)
	if to_cell != from_cell:
		actor.move_to(to_cell, STEP_SECONDS)


func _on_npc_removed(id: int) -> void:
	var actor = npc_nodes.get(id)
	if actor != null:
		_dispose(actor)
		npc_nodes.erase(id)


func highlight_npc(id: int) -> void:
	if npc_nodes.has(_selected_npc):
		npc_nodes[_selected_npc].set_selected(false)
	_selected_npc = id
	if npc_nodes.has(id):
		npc_nodes[id].set_selected(true)


func city_at(point: Vector3) -> String:
	var cell := Grid.to_cell(point)
	for id in store.cities:
		if (store.cities[id].bounds as Rect2i).has_point(cell):
			return id
	return ""


func apply_names() -> void:
	for id in npc_nodes:
		if store.names.has(id):
			npc_nodes[id].set_display_name(store.names[id])


func hover_npc(id: int) -> void:
	for key in npc_nodes:
		npc_nodes[key].set_hovered(key == id)
