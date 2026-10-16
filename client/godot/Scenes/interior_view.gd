extends Node3D
## Interior de um prédio: um nó por andar, empilhados; o andar visível corta teto e andares acima.
## A planta vem pronta do servidor (layout por andar); o cliente nunca inventa parede nem escada.

const Grid := preload("res://Scenes/grid.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const WorldStore := preload("res://Store/world_store.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")

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
var catalog = AssetCatalog.new() ## mobília por tipo de prédio; o app injeta o catálogo carregado
var furniture: Node3D
var _object_bases: Dictionary = {} ## id do catálogo -> [Vector3] base (chão) de cada peça, na ordem de colocação (alvos de uso)
var _use_points: Dictionary = {} ## id do catálogo -> pontos de uso do descritor
var occupant_nodes: Dictionary = {} ## npc_id -> NpcActor
var _occupants: Array = []
var _labels
var _walls: Array = [] ## [{node, mesh, cell}]
var _center := Vector3.ZERO
var _view_dir := Vector3.ZERO


## `info` = WorldStore.interior: {building, floors, layout:[{level, cells:[{cell, material}]}], occupants}
func show_interior(info: Dictionary, p_origin: Vector2i, labels, building_type: int = -1, lodging_units: int = 0) -> void:
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
	_place_furniture(info, building_type, lodging_units)
	set_occupants(info.occupants)
	set_floor(floor_count - 1 if current_floor >= floor_count else current_floor)


## Leitos usam o piso livre de todos os andares; outras peças do catálogo ficam no térreo.
## Nunca ocupa parede, porta ou escada. Cada peça pertence ao andar para respeitar o corte de visão.
func _place_furniture(info: Dictionary, building_type: int, lodging_units: int = 0) -> void:
	var objects = info.get("objects")
	var factory = ObjectFactory.new(objects.catalog) if objects is Dictionary else null
	if factory != null:
		for descriptor in objects.catalog:
			_use_points[String(descriptor.id)] = descriptor.get("usePoints", [])
	var remaining := lodging_units
	for floor_layout in info.layout:
		var level := int(floor_layout.level)
		var floor_node: Node3D = floor_nodes.get(level)
		if floor_node == null or (level != 0 and remaining <= 0):
			continue
		var free_cells: Array[Vector2i] = []
		for entry in floor_layout.cells:
			if int(entry.material) not in [WALL_STONE, WALL_WOOD, DOOR, STAIR]:
				free_cells.append(Vector2i(int(entry.cell.x), int(entry.cell.y)))
		free_cells.sort_custom(func(a: Vector2i, b: Vector2i) -> bool: return a.y < b.y or (a.y == b.y and a.x < b.x))
		if free_cells.is_empty():
			continue
		var floor_furniture := Node3D.new()
		floor_furniture.name = "Furniture"
		floor_node.add_child(floor_furniture)
		if level == 0:
			furniture = floor_furniture
		var seed_value := origin.x * 31 + origin.y
		var stride := maxi(free_cells.size() / 5, 1)
		var used := {}
		var index := 0
		var pieces: Array = []
		var beds := mini(remaining, free_cells.size())
		remaining -= beds
		for i in beds:
			pieces.append(["bed", 0])
		if level == 0:
			if factory != null:
				for placed in objects.furniture:
					pieces.append([String(placed.id), int(placed.variation)])
			else:
				for piece_id in catalog.furniture_for(building_type):
					pieces.append([String(piece_id), 0])
		for piece in pieces:
			if used.size() >= free_cells.size():
				break
			var slot := (seed_value + index * stride) % free_cells.size()
			while used.has(slot) and used.size() < free_cells.size():
				slot = (slot + 1) % free_cells.size()
			used[slot] = true
			if factory != null and factory.has(piece[0]):
				_add_object(factory, piece[0], piece[1], origin + free_cells[slot], floor_furniture)
				if level == 0: # só o térreo recebe uso (o servidor não diz o andar da peça)
					_register_base(piece[0], origin + free_cells[slot])
			else:
				_add_piece(piece[0], origin + free_cells[slot], floor_furniture)
			index += 1


func _add_object(factory, piece_id: String, variation: int, cell: Vector2i, parent: Node3D) -> void:
	var node: Node3D = factory.build(piece_id, variation)
	node.position += Grid.to_world(cell)
	parent.add_child(node)


## Guarda a base (chão) da peça do catálogo para o NPC que a usa ser posto no ponto de uso.
func _register_base(piece_id: String, cell: Vector2i) -> void:
	if not _object_bases.has(piece_id):
		_object_bases[piece_id] = []
	_object_bases[piece_id].append(Grid.to_world(cell))


func _add_piece(piece_id: String, cell: Vector2i, parent: Node3D) -> void:
	var spec: Dictionary = catalog.piece(piece_id)
	var size: Array = spec.size
	var node := MeshInstance3D.new()
	node.name = piece_id
	var mesh := BoxMesh.new()
	mesh.size = Vector3(size[0], size[1], size[2])
	node.mesh = mesh
	node.material_override = _material(Color(String(spec.color)))
	node.position = Grid.to_world(cell, size[1] / 2.0)
	parent.add_child(node)


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
	_object_bases.clear()
	_use_points.clear()
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
	furniture = null # filho do térreo, já liberado com ele


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
	var rank := _use_ranks(occupants)
	for occupant in occupants:
		var id: int = int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc)
		wanted[id] = occupant
		var cell: Vector2i = origin + Vector2i(int(occupant.localCell.x), int(occupant.localCell.y))
		var level: int = int(occupant.floor.value) if occupant.floor is Dictionary else int(occupant.floor)
		var action: int = -1 if occupant.get("action") == null else int(occupant.action)
		var use := _use_of(occupant, id, action, level, rank)
		var actor = occupant_nodes.get(id)
		if actor == null:
			actor = NpcActor.new()
			add_child(actor)
			actor.setup(id, cell, action, _labels, WorldStore.look_of(occupant.get("appearance")))
			occupant_nodes[id] = actor
		if actor.action != action or actor.use_pose != String(use.pose):
			actor.set_action(action, _labels, String(use.pose))
		actor.enable_bed(not use.active) # a cama do catálogo substitui a do NPC
		actor.position = use.position if use.active else Grid.to_world(cell, level * FLOOR_HEIGHT)
		actor.rotation.y = use.facing if use.active else actor.rotation.y
		actor.set_meta("floor", level)
	for id in occupant_nodes.keys():
		if not wanted.has(id):
			occupant_nodes[id].free()
			occupant_nodes.erase(id)
	_apply_occupant_visibility()


## Posição na fila de cada objeto em uso (por id do NPC): o 1º usa a 1ª peça, o 2º a 2ª, voltando ao começo se faltar peça.
func _use_ranks(occupants: Array) -> Dictionary:
	var by_object := {}
	for occupant in occupants:
		var object_id = occupant.get("usingObject")
		if object_id != null:
			var npc_id: int = int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc)
			by_object[String(object_id)] = by_object.get(String(object_id), []) + [npc_id]
	var ranks := {}
	for object_id in by_object:
		var ids: Array = by_object[object_id]
		ids.sort()
		for i in ids.size():
			ranks[ids[i]] = i
	return ranks


## {active, position, facing, pose}: onde o NPC fica e que pose faz usando o objeto que o servidor apontou; inativo
## (sem objeto, sem peça colocada, outro andar ou sem ponto de uso) = posição e pose de sempre.
func _use_of(occupant: Dictionary, npc_id: int, action: int, level: int, rank: Dictionary) -> Dictionary:
	var none := {"active": false, "position": Vector3.ZERO, "facing": 0.0, "pose": ""}
	var object_id = occupant.get("usingObject")
	if object_id == null or level != 0 or not _object_bases.has(String(object_id)):
		return none
	var point := _use_point(String(object_id), action)
	if point.is_empty():
		return none
	var bases: Array = _object_bases[String(object_id)]
	var piece_base: Vector3 = bases[int(rank.get(npc_id, 0)) % bases.size()]
	var offset: Array = point.offset
	var base := piece_base + Vector3(float(offset[0]), float(offset[1]), float(offset[2]))
	return {"active": true, "position": base, "facing": deg_to_rad(float(point.get("facing", 0.0))), "pose": point.get("pose", "")}


func _use_point(object_id: String, action: int) -> Dictionary:
	for point in _use_points.get(object_id, []):
		if point.get("action") != null and int(point.action) == action:
			return point
	return {}


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
