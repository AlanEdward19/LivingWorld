extends Node3D
## Interior de um prédio: um nó por andar, empilhados; o andar visível corta teto e andares acima.
## A planta vem pronta do servidor (layout por andar); o cliente nunca inventa parede nem escada.

const Grid := preload("res://Scenes/grid.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")

const FLOOR_HEIGHT := 1.0
const WALL_HEIGHT := 0.9
const WALL_STONE := 0
const WALL_WOOD := 1
const DOOR := 2
const STAIR := 4
const CUTAWAY_HEIGHT := 0.22 ## paredes voltadas para a câmera ficam baixas para ver dentro

var origin := Vector2i.ZERO ## célula do mundo onde começa a planta do prédio
var floor_count := 0
var current_floor := 0
var floor_nodes: Dictionary = {} ## nível -> Node3D
var roof: Node3D
var occupant_nodes: Dictionary = {} ## npc_id -> NpcActor
var _occupants: Array = []
var _labels
var _walls: Array = [] ## [{node, mesh, cell}]
var _center := Vector3.ZERO
var _view_dir := Vector3.ZERO


## `info` = WorldStore.interior: {building, floors, layout:[{level, cells:[{cell, material}]}], occupants}
func show_interior(info: Dictionary, p_origin: Vector2i, labels) -> void:
	_clear()
	origin = p_origin
	_labels = labels
	floor_count = int(info.floors)
	for floor_layout in info.layout:
		var level := int(floor_layout.level)
		var node := Node3D.new()
		node.name = "Floor_%d" % level
		node.position.y = level * FLOOR_HEIGHT
		for entry in floor_layout.cells:
			_add_cell(node, entry, level)
		add_child(node)
		floor_nodes[level] = node
	roof = Node3D.new()
	roof.name = "Roof"
	roof.position.y = floor_count * FLOOR_HEIGHT
	var seen := {}
	_compute_center(info)
	for entry in info.layout[0].cells if info.layout.size() > 0 else []:
		var cell: Vector2i = origin + Vector2i(int(entry.cell.x), int(entry.cell.y))
		if seen.has(cell):
			continue
		seen[cell] = true
		var slab := MeshInstance3D.new()
		var mesh := BoxMesh.new()
		mesh.size = Vector3(Grid.CELL, 0.1, Grid.CELL)
		slab.mesh = mesh
		slab.material_override = _material(Color("8a4b3c"))
		slab.position = Grid.to_world(cell)
		roof.add_child(slab)
	add_child(roof)
	set_occupants(info.occupants)
	set_floor(floor_count - 1 if current_floor >= floor_count else current_floor)


func _add_cell(parent: Node3D, entry: Dictionary, level: int) -> void:
	var cell: Vector2i = origin + Vector2i(int(entry.cell.x), int(entry.cell.y))
	var material: int = int(entry.material)
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	match material:
		WALL_STONE, WALL_WOOD:
			mesh.size = Vector3(Grid.CELL, WALL_HEIGHT, Grid.CELL)
			node.material_override = _material(Color("9a948a") if material == WALL_STONE else Color("a27b52"))
			node.position = Grid.to_world(cell, WALL_HEIGHT / 2.0)
			_walls.append({"node": node, "mesh": mesh, "cell": cell})
		STAIR:
			mesh.size = Vector3(Grid.CELL * 0.9, 0.5, Grid.CELL * 0.9)
			node.material_override = _material(Color("c9b28a"))
			node.position = Grid.to_world(cell, 0.25)
		DOOR:
			mesh.size = Vector3(Grid.CELL, 0.5, Grid.CELL)
			node.material_override = _material(Color("3b2a20"))
			node.position = Grid.to_world(cell, 0.25)
		_:
			mesh.size = Vector3(Grid.CELL, 0.08, Grid.CELL)
			node.material_override = _material(Color("c9b896") if (cell.x + cell.y + level) % 2 == 0 else Color("bfae8c"))
			node.position = Grid.to_world(cell, 0.0)
	node.mesh = mesh
	parent.add_child(node)


func _material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.9
	return material


func _clear() -> void:
	_walls.clear()
	_view_dir = Vector3.ZERO
	for node in floor_nodes.values():
		node.free()
	floor_nodes.clear()
	if roof != null:
		roof.free()
		roof = null
	for actor in occupant_nodes.values():
		actor.free()
	occupant_nodes.clear()


## Andar visível = `level`: andares acima e o teto somem; só os ocupantes desse andar aparecem.
func set_floor(level: int) -> void:
	current_floor = clampi(level, 0, maxi(floor_count - 1, 0))
	for key in floor_nodes:
		floor_nodes[key].visible = key <= current_floor
	if roof != null:
		roof.visible = false
	_apply_occupant_visibility()


func set_occupants(occupants: Array) -> void:
	_occupants = occupants
	var wanted := {}
	for occupant in occupants:
		var id: int = int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc)
		wanted[id] = occupant
		var cell: Vector2i = origin + Vector2i(int(occupant.localCell.x), int(occupant.localCell.y))
		var level: int = int(occupant.floor.value) if occupant.floor is Dictionary else int(occupant.floor)
		var action: int = -1 if occupant.get("action") == null else int(occupant.action)
		var actor = occupant_nodes.get(id)
		if actor == null:
			actor = NpcActor.new()
			add_child(actor)
			actor.setup(id, cell, action, _labels)
			actor.enable_bed(true)
			occupant_nodes[id] = actor
		elif actor.action != action:
			actor.set_action(action, _labels)
		actor.position = Grid.to_world(cell, level * FLOOR_HEIGHT)
		actor.set_meta("floor", level)
	for id in occupant_nodes.keys():
		if not wanted.has(id):
			occupant_nodes[id].free()
			occupant_nodes.erase(id)
	_apply_occupant_visibility()


func _apply_occupant_visibility() -> void:
	for actor in occupant_nodes.values():
		actor.visible = int(actor.get_meta("floor", 0)) == current_floor


func visible_occupant_ids() -> Array:
	var ids: Array = []
	for id in occupant_nodes:
		if occupant_nodes[id].visible:
			ids.append(id)
	ids.sort()
	return ids


func _compute_center(info: Dictionary) -> void:
	var lo := Vector2(1e9, 1e9)
	var hi := Vector2(-1e9, -1e9)
	for entry in info.layout[0].cells if info.layout.size() > 0 else []:
		lo = Vector2(minf(lo.x, entry.cell.x), minf(lo.y, entry.cell.y))
		hi = Vector2(maxf(hi.x, entry.cell.x), maxf(hi.y, entry.cell.y))
	_center = Vector3(origin.x + (lo.x + hi.x) / 2.0 + 0.5, 0, origin.y + (lo.y + hi.y) / 2.0 + 0.5)


## Corte de maquete: paredes entre a câmera e o centro ficam baixas; as do fundo seguem altas.
## `direction` = direção horizontal do centro para a câmera.
func set_view_direction(direction: Vector3) -> void:
	var flat := Vector3(direction.x, 0, direction.z)
	if flat.length() < 0.001:
		return
	flat = flat.normalized()
	if flat.is_equal_approx(_view_dir):
		return
	_view_dir = flat
	for wall in _walls:
		var cell: Vector2i = wall.cell
		var offset := Grid.to_world(cell) - _center
		var height := CUTAWAY_HEIGHT if offset.dot(flat) > 0.4 else WALL_HEIGHT
		var box: BoxMesh = wall.mesh
		box.size = Vector3(box.size.x, height, box.size.z)
		var wall_node: Node3D = wall.node
		wall_node.position = Vector3(wall_node.position.x, height / 2.0, wall_node.position.z)


func apply_names(names: Dictionary) -> void:
	for id in occupant_nodes:
		if names.has(id):
			occupant_nodes[id].set_display_name(names[id])


func _process(delta: float) -> void:
	for actor in occupant_nodes.values():
		actor.advance_motion(delta)
		if actor.visible:
			actor.animate(delta)
