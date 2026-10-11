extends Node3D
## Mundo e cidade em 3D isométrico: terreno, regiões de cidade, prédios e NPCs animados.
## Lê a WorldStore; reage aos sinais dela. Nunca fala com o servidor.

const Grid := preload("res://Scenes/grid.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")

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
		multi.set_instance_transform(index, Transform3D(Basis(), Grid.to_world(cell, -0.15)))
		var shade := 0.94 + float((cell.x * 7 + cell.y * 13) % 5) * 0.03
		var color: Color = TERRAIN_COLORS[(id - 1) % TERRAIN_COLORS.size()] * shade
		if store.rivers.has(cell):
			color = Color("4a86b8") * shade
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


func _make_building(info: Dictionary) -> Node3D:
	var root := Node3D.new()
	root.name = "Building_%d" % info.id
	root.set_meta("building_id", info.id)
	var height: float = info.floors * FLOOR_HEIGHT
	var stone := _flat_material(Color("9a948a"))
	var wood := _flat_material(Color("a27b52"))
	var roof := _flat_material(Color("8a4b3c"))
	var door := _flat_material(Color("3b2a20"))
	var min_cell := Vector2i(1 << 20, 1 << 20)
	var max_cell := Vector2i(-(1 << 20), -(1 << 20))
	for entry in info.footprint:
		var cell: Vector2i = info.cell + entry.cell
		min_cell = Vector2i(mini(min_cell.x, cell.x), mini(min_cell.y, cell.y))
		max_cell = Vector2i(maxi(max_cell.x, cell.x), maxi(max_cell.y, cell.y))
		var box := MeshInstance3D.new()
		var mesh := BoxMesh.new()
		match entry.material:
			MATERIAL_WALL_STONE, MATERIAL_WALL_WOOD:
				mesh.size = Vector3(Grid.CELL, height, Grid.CELL)
				box.material_override = stone if entry.material == MATERIAL_WALL_STONE else wood
				box.position = Grid.to_world(cell, height / 2.0)
			MATERIAL_DOOR:
				mesh.size = Vector3(Grid.CELL, height * 0.7, Grid.CELL)
				box.material_override = door
				box.position = Grid.to_world(cell, height * 0.35)
			_:
				mesh.size = Vector3(Grid.CELL, 0.12, Grid.CELL)
				box.material_override = roof
				box.position = Grid.to_world(cell, height)
		box.mesh = mesh
		root.add_child(box)
	var body := StaticBody3D.new()
	body.set_meta("building_id", info.id)
	var shape := CollisionShape3D.new()
	var collider := BoxShape3D.new()
	var span := max_cell - min_cell + Vector2i.ONE
	collider.size = Vector3(span.x, height, span.y)
	shape.shape = collider
	shape.position = Vector3(min_cell.x + span.x / 2.0, height / 2.0, min_cell.y + span.y / 2.0)
	body.add_child(shape)
	root.add_child(body)
	return root


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
