extends Node3D
## Interior de um prédio: um nó por andar, empilhados; o andar visível corta teto e andares acima.
## A planta vem pronta do servidor (layout por andar); o cliente nunca inventa parede nem escada.

const Grid := preload("res://Scenes/grid.gd")
const WallPanels := preload("res://Scenes/wall_panels.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const NpcFigure := preload("res://Scenes/npc_figure.gd")
const WorldStore := preload("res://Store/world_store.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")

## #222: a parede tem a altura do andar (o motor diz os metros); antes 0,9 unidade = 1,35 m, mais baixa que uma pessoa
static func wall_height() -> float: return Grid.floor_height() * 0.99 ## #227: quase o andar inteiro: a câmera do jogador (dentro da caixa do andar) fica sob o forro, sem fresta de céu
static func cutaway_height() -> float: return Grid.units(0.45) ## as voltadas para a câmera ficam na altura do joelho
const WALL_HEIGHT := 0.9 ## (legado: só para mundos sem escala; use wall_height())
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
var ceiling: Node3D ## #227: forro do andar visível: só aparece com a câmera do jogador dentro da casa e abaixo dele (sem céu por cima das paredes)
var figure_context: Dictionary = {} ## #247: o mesmo dado que veste o boneco na rua (store, catálogo, modelo); o app injeta
var catalog = AssetCatalog.new() ## mobília por tipo de prédio; o app injeta o catálogo carregado
var furniture: Node3D
var _use_slots: Dictionary = {} ## #237: id do catálogo -> [{base: Vector3, yaw: float, level: int}] cada peça colocada, com o andar e o giro que o motor deu (alvos de uso)
var _use_points: Dictionary = {} ## id do catálogo -> pontos de uso do descritor
var occupant_nodes: Dictionary = {} ## npc_id -> NpcActor
var object_points: Array = [] ## [{id, level, position}] cada peça de mobília, para clique e dica (ADR-0047)
var _occupants: Array = []
var _labels
var _walls: Array = [] ## [{node, mesh, cell}]
var _center := Vector3.ZERO
var _span := 0.0
var _plans: Dictionary = {} ## nível -> {célula local: material} (a planta do motor, para saber se uma célula é parede)
var _panels: Dictionary = {} ## nível -> [Rect2] painéis finos de parede (células absolutas) para a colisão do corpo (#226)
var _view_dir := Vector3.ZERO
var _tint: Dictionary = {} ## célula local -> cor do piso do cômodo (#226: só apresentação: o cômodo e o tipo são do motor)
var _bounds := Rect2() ## planta do térreo em células absolutas (para a caixa da câmera do jogador)
var _immersive_applied := false
var immersive := false ## #227: o jogador está na casa: nada de corte de maquete, as paredes ficam na altura inteira


## `info` = WorldStore.interior: {building, floors, layout:[{level, cells:[{cell, material}]}], occupants}
## #237: a casa deixa de existir no desenho (pisos, paredes, forro, teto e ocupantes); `show_interior` a monta de novo.
func close() -> void:
	_clear()
	visible = false


func show_interior(info: Dictionary, p_origin: Vector2i, labels, building_type: int = -1, lodging_units: int = 0) -> void:
	_clear()
	visible = true
	origin = p_origin
	_labels = labels
	floor_count = int(info.floors)
	for floor_layout in info.layout:
		var level := int(floor_layout.level)
		var node := Node3D.new()
		node.name = "Floor_%d" % level
		node.position.y = level * Grid.floor_height()
		var plan := {}
		_tint = _tints_of(info, level)
		for entry in floor_layout.cells:
			plan[Vector2i(int(entry.cell.x), int(entry.cell.y))] = int(entry.material)
			_add_cell(node, entry, level)
		_add_panels(node, plan, level, _walls_of(floor_layout, info, level), _doors_of(floor_layout, info, level))
		add_child(node)
		floor_nodes[level] = node
	roof = Node3D.new()
	roof.name = "Roof"
	ceiling = Node3D.new()
	ceiling.name = "Ceiling"
	ceiling.visible = false
	roof.position.y = floor_count * Grid.floor_height()
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
		var lid := MeshInstance3D.new()
		var lid_mesh := BoxMesh.new()
		lid_mesh.size = Vector3(Grid.CELL, 0.1, Grid.CELL)
		lid.mesh = lid_mesh
		lid.material_override = _material(Color("c8bba2"))
		lid.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF # o forro não escurece a casa: o sol segue o do mundo
		lid.position = Grid.to_world(cell)
		ceiling.add_child(lid)
	add_child(roof)
	add_child(ceiling)
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
	if objects is Dictionary and factory != null and _any_planned(objects):
		_place_planned(info, objects, factory) # #237: o motor diz onde cada peça fica; o cliente só desenha
		return
	var remaining := lodging_units
	for floor_layout in info.layout:
		var level := int(floor_layout.level)
		var floor_node: Node3D = floor_nodes.get(level)
		if floor_node == null or (level != 0 and remaining <= 0):
			continue
		var free_cells: Array[Vector2i] = []
		var plan_cells := {}
		for entry in floor_layout.cells:
			plan_cells[Vector2i(int(entry.cell.x), int(entry.cell.y))] = int(entry.material)
		for entry in floor_layout.cells:
			var local := Vector2i(int(entry.cell.x), int(entry.cell.y))
			if int(entry.material) not in [WALL_STONE, WALL_WOOD, DOOR, STAIR] and not _in_front_of_door(plan_cells, local):
				free_cells.append(local)
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


## #237: a célula logo à frente da porta (por dentro) nunca recebe peça (servidor antigo, sem lugar dado pelo motor).
func _in_front_of_door(plan_cells: Dictionary, cell: Vector2i) -> bool:
	for side in [Vector2i(0, -1), Vector2i(-1, 0), Vector2i(1, 0), Vector2i(0, 1)]:
		if plan_cells.get(cell + side, -1) == DOOR:
			return true
	return false


## #237: o servidor deu `cell` a alguma peça (mobília posta pelo motor).
func _any_planned(objects: Dictionary) -> bool:
	for placed in objects.get("furniture", []):
		if placed is Dictionary and placed.get("cell") != null:
			return true
	return false


## #237: desenha cada peça no andar, célula e giro que o motor deu; peça sem `cell` não coube (o motor não a pôs). O uso (cama etc.) vale em qualquer andar.
func _place_planned(info: Dictionary, objects: Dictionary, factory) -> void:
	var containers := {}
	for placed in objects.furniture:
		if placed.get("cell") == null or not factory.has(String(placed.id)):
			continue
		var level := int(placed.get("floor", 0))
		var floor_node: Node3D = floor_nodes.get(level)
		if floor_node == null:
			continue
		if not containers.has(level):
			var container := Node3D.new()
			container.name = "Furniture"
			floor_node.add_child(container)
			containers[level] = container
			if level == 0:
				furniture = container
		var cell: Vector2i = origin + Vector2i(int(placed.cell.x), int(placed.cell.y))
		var yaw := deg_to_rad(float(placed.get("facing", 0)))
		_add_object(factory, String(placed.id), int(placed.variation), cell, containers[level], yaw)
		_register_slot(String(placed.id), cell, yaw, level)


func _add_object(factory, piece_id: String, variation: int, cell: Vector2i, parent: Node3D, yaw: float = 0.0) -> void:
	var node: Node3D = factory.build(piece_id, variation)
	node.position += Grid.to_world(cell)
	node.rotation.y = yaw
	parent.add_child(node)
	_register_point(piece_id, cell, parent)


func _register_point(piece_id: String, cell: Vector2i, parent: Node3D) -> void:
	var level := int(parent.get_parent().position.y / Grid.floor_height()) if parent.get_parent() is Node3D else 0
	object_points.append({"id": piece_id, "level": level, "position": Grid.to_world(cell, level * Grid.floor_height() + 0.35)})


## Peças do andar visível (para a seleção): [{id, level, position}].
func visible_objects() -> Array:
	return object_points.filter(func(point): return int(point.level) == current_floor)


## Guarda a base (chão) da peça do catálogo para o NPC que a usa ser posto no ponto de uso.
func _register_base(piece_id: String, cell: Vector2i) -> void:
	_register_slot(piece_id, cell, 0.0, 0)


## #237: lugar de uso de uma peça: base no chão do andar `level` e giro `yaw` (rad) em torno de Y.
func _register_slot(piece_id: String, cell: Vector2i, yaw: float, level: int) -> void:
	if not _use_slots.has(piece_id):
		_use_slots[piece_id] = []
	_use_slots[piece_id].append({"base": Grid.to_world(cell, level * Grid.floor_height()), "yaw": yaw, "level": level})


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
	_register_point(piece_id, cell, parent)


func _add_cell(parent: Node3D, entry: Dictionary, level: int) -> void:
	var cell: Vector2i = origin + Vector2i(int(entry.cell.x), int(entry.cell.y))
	var material: int = int(entry.material)
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	match material:
		WALL_STONE, WALL_WOOD: # #226: a parede é painel fino (ver _add_panels); a célula dela é piso útil, como o resto do cômodo
			mesh.size = Vector3(Grid.CELL, 0.08, Grid.CELL)
			node.material_override = _material(Color("c9b896") if (cell.x + cell.y + level) % 2 == 0 else Color("bfae8c"))
			node.position = Grid.to_world(cell, 0.0)
		STAIR: # #222: degraus que sobem até o andar de cima (antes um bloco chato)
			mesh.size = Vector3(Grid.CELL, 0.06, Grid.CELL)
			node.material_override = _material(Color("c9b896"))
			node.position = Grid.to_world(cell, 0.0)
			var steps := 6
			for k in steps:
				var step := MeshInstance3D.new()
				var box := BoxMesh.new()
				var rise := Grid.floor_height() / float(steps)
				box.size = Vector3(Grid.CELL * 0.85, rise * (k + 1), Grid.CELL / float(steps))
				step.mesh = box
				step.material_override = _material(Color("b08a5a"))
				step.position = Grid.to_world(cell, rise * (k + 1) / 2.0) + Vector3(0, 0, -Grid.CELL / 2.0 + Grid.CELL / float(steps) * (k + 0.5))
				parent.add_child(step)
		DOOR: # #226: a célula da porta é piso inteiro (a abertura e a verga ficam na aresta, ver _add_panels)
			mesh.size = Vector3(Grid.CELL, 0.06, Grid.CELL)
			node.material_override = _material(Color("8a6a43"))
			node.position = Grid.to_world(cell, 0.0)
		_:
			mesh.size = Vector3(Grid.CELL, 0.08, Grid.CELL)
			node.material_override = _material(_floor_color(Vector2i(int(entry.cell.x), int(entry.cell.y)), (cell.x + cell.y + level) % 2 == 0))
			node.position = Grid.to_world(cell, 0.0)
	node.mesh = mesh
	parent.add_child(node)


## #226: o piso de cada cômodo ganha um tom pelo tipo que o motor deu (o mesmo tipo, o mesmo tom); sem cômodos, o piso de sempre.
func _tints_of(info: Dictionary, level: int) -> Dictionary:
	var tints := {}
	var rooms: Array = _rooms_of(info, level).get("rooms", [])
	if rooms.size() < 2:
		return tints
	for room in rooms:
		var tone := Color.from_hsv(float(String(room.get("kind", "")).hash() % 360) / 360.0, 0.22, 0.80)
		for cell in room.cells:
			tints[Vector2i(int(cell.x), int(cell.y))] = tone
	return tints


func _floor_color(local: Vector2i, even: bool) -> Color:
	var base: Color = _tint.get(local, Color("c9b896") if even else Color("bfae8c"))
	return base if even or not _tint.has(local) else base.darkened(0.05)


## #226: as paredes de fora do andar (`walls` da planta) mais as de dentro (cômodos, `rooms[level].walls`): o motor diz em que aresta fica cada uma.
func _walls_of(floor_layout: Dictionary, info: Dictionary, level: int) -> Array:
	var walls: Array = []
	walls.append_array(floor_layout.get("walls", []) if floor_layout.get("walls") != null else [])
	walls.append_array(_rooms_of(info, level).get("walls", []))
	return walls


## As aberturas de porta do andar: a da rua (térreo) e as portas entre cômodos.
func _doors_of(floor_layout: Dictionary, info: Dictionary, level: int) -> Array:
	var doors: Array = []
	doors.append_array(floor_layout.get("doors", []) if floor_layout.get("doors") != null else [])
	doors.append_array(_rooms_of(info, level).get("doors", []))
	return doors


## Os cômodos do andar (`rooms` do motor, na ordem dos andares); vazio em mundo sem cômodos.
func _rooms_of(info: Dictionary, level: int) -> Dictionary:
	var rooms = info.get("rooms")
	if rooms is Array and level < rooms.size() and rooms[level] is Dictionary:
		return rooms[level]
	return {}


## #226: painéis finos a partir das arestas de parede do motor: os de fora rentes à borda por dentro, os de dentro no meio da aresta; a
## porta é só o vão (verga em cima), sem painel.
func _add_panels(parent: Node3D, plan: Dictionary, level: int, walls: Array, doors: Array) -> void:
	_plans[level] = plan
	var rects: Array = []
	for opening in WallPanels.openings(doors, plan):
		var span: Rect2 = opening.rect
		var door_h := Grid.units(Grid.door_meters)
		var lintel := MeshInstance3D.new()
		var top := BoxMesh.new()
		top.size = Vector3(span.size.x * Grid.CELL, maxf(0.05, wall_height() - door_h), span.size.y * Grid.CELL)
		lintel.mesh = top
		lintel.material_override = _material(Color("6b4a2b"))
		var middle := Vector2(origin) + span.get_center()
		lintel.position = Vector3(middle.x * Grid.CELL, door_h + top.size.y / 2.0, middle.y * Grid.CELL)
		parent.add_child(lintel)
		var lintel_cell: Vector2i = origin + Vector2i(opening.cell)
		_walls.append({"node": lintel, "mesh": top, "cell": lintel_cell, "lintel": true})
	for panel in WallPanels.panels(walls, plan):
		var rect: Rect2 = panel.rect
		var absolute := Rect2(Vector2(origin) + rect.position, rect.size)
		rects.append(absolute)
		var node := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(rect.size.x * Grid.CELL, wall_height(), rect.size.y * Grid.CELL)
		node.mesh = box
		node.material_override = _material(Color("9a948a") if int(panel.material) == WALL_STONE else Color("a27b52"))
		var center := absolute.get_center()
		node.position = Vector3(center.x * Grid.CELL, wall_height() / 2.0, center.y * Grid.CELL)
		parent.add_child(node)
		var cell: Vector2i = origin + Vector2i(panel.cell)
		_walls.append({"node": node, "mesh": box, "cell": cell, "edge": node.position - Grid.to_world(cell, wall_height() / 2.0)})
	_panels[level] = rects


## #227: a célula absoluta `cell` é parede na planta do motor? (o corpo que o motor põe numa parede não sai dela: o motor veda)
func is_wall_cell(cell: Vector2i, level: int = 0) -> bool:
	var plan: Dictionary = _plans.get(level, {})
	var local := cell - origin
	return plan.has(local) and WallPanels.is_wall(int(plan[local]))


## #237: a célula absoluta `cell` é da planta do motor naquele andar (parede ou piso)? Fora dela é rua, não a casa.
func covers(cell: Vector2i, level: int = 0) -> bool:
	return _plans.get(level, {}).has(cell - origin)


## #226/#229: o que o corpo do jogador toca no andar visível ao redor de `center` (células): [[Rect2, altura]].
func obstacles_near(center: Vector2, reach: float, level: int = -1) -> Array:
	var out: Array = []
	var area := Rect2(center - Vector2(reach, reach), Vector2(reach, reach) * 2.0)
	for rect: Rect2 in _panels.get(current_floor if level < 0 else level, []):
		if area.intersects(rect):
			out.append([rect, wall_height()])
	return out


## #227: a caixa do andar visível no mundo (a câmera do jogador não sai dela) e as caixas dos painéis finos desse andar.
func floor_box() -> AABB:
	var base := current_floor * Grid.floor_height()
	return AABB(Vector3(_bounds.position.x * Grid.CELL, base, _bounds.position.y * Grid.CELL),
		Vector3(_bounds.size.x * Grid.CELL, Grid.floor_height(), _bounds.size.y * Grid.CELL))


func wall_boxes() -> Array:
	var out: Array = []
	var base := current_floor * Grid.floor_height()
	for rect: Rect2 in _panels.get(current_floor, []):
		out.append(AABB(Vector3(rect.position.x * Grid.CELL, base, rect.position.y * Grid.CELL), Vector3(rect.size.x * Grid.CELL, wall_height(), rect.size.y * Grid.CELL)))
	return out


## Quem está no andar visível (posição fina em células) fora `except_id`: o corpo empurra de leve.
func people_near(center: Vector2, reach: float, except_id: int = -1) -> Array:
	var out: Array = []
	for id in occupant_nodes:
		var actor: Node3D = occupant_nodes[id]
		if id == except_id or not actor.visible:
			continue
		var at := Vector2(actor.position.x, actor.position.z) / Grid.CELL
		if at.distance_to(center) <= reach:
			out.append(at)
	return out


func _material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.9
	return material


func _clear() -> void:
	object_points.clear()
	_use_slots.clear()
	_use_points.clear()
	_walls.clear()
	_panels.clear()
	_plans.clear()
	guided_id = -1
	_view_dir = Vector3.ZERO
	_immersive_applied = false
	_last_fade_at = Vector3(INF, INF, INF)
	for node in floor_nodes.values():
		node.free()
	floor_nodes.clear()
	if roof != null:
		roof.free()
		roof = null
	if ceiling != null:
		ceiling.free()
		ceiling = null
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
	if ceiling != null:
		ceiling.position.y = current_floor * Grid.floor_height() + wall_height() + 0.05 # assenta sobre as paredes do andar
	_apply_occupant_visibility()


## #227: o forro aparece só com o jogador dentro e a câmera abaixo dele; de cima (maquete ou câmera alta) some e a casa se vê aberta.
func update_ceiling(camera_y: float) -> void:
	if ceiling != null:
		ceiling.visible = immersive and camera_y < ceiling.position.y


const FADE_WALL_NEAR := 0.25 ## unidades: parede mais perto que isto da câmera some; some por inteiro e volta opaca até FADE_WALL_FAR
const FADE_WALL_FAR := 0.7
const FADE_LID_NEAR := 0.9 ## forro em volta da câmera: o círculo de baixo dela some, o resto fica
const FADE_LID_FAR := 1.8
var _last_fade_at := Vector3(INF, INF, INF)


## #227: com a câmera do jogador dentro da casa, paredes e forro perto dela ficam translúcidos (nada opaco tampa a tela); fora, tudo opaco.
func update_near(camera_pos: Vector3) -> void:
	if not immersive:
		if _last_fade_at.x != INF:
			_last_fade_at = Vector3(INF, INF, INF)
			_set_fade(_walls, 1.0)
			if ceiling != null:
				for lid in ceiling.get_children():
					(lid as GeometryInstance3D).transparency = 0.0
		return
	var at := to_local(camera_pos)
	if _last_fade_at.distance_to(at) < 0.01:
		return
	_last_fade_at = at
	for wall in _walls:
		if wall.get("lintel", false):
			continue
		var node: MeshInstance3D = wall.node
		var half: Vector3 = (wall.mesh as BoxMesh).size * 0.5
		var closest := Vector3(clampf(at.x, node.position.x - half.x, node.position.x + half.x), clampf(at.y, node.position.y - half.y, node.position.y + half.y),
			clampf(at.z, node.position.z - half.z, node.position.z + half.z))
		node.transparency = 1.0 - smoothstep(FADE_WALL_NEAR, FADE_WALL_FAR, closest.distance_to(at))
	if ceiling != null:
		for lid in ceiling.get_children():
			var node := lid as MeshInstance3D
			var flat := Vector2(node.position.x - at.x, node.position.z - at.z).length()
			node.transparency = 1.0 - smoothstep(FADE_LID_NEAR, FADE_LID_FAR, flat)


func _set_fade(walls: Array, opacity: float) -> void:
	for wall in walls:
		(wall.node as GeometryInstance3D).transparency = 1.0 - opacity


## #227: o corpo do jogador anda pela planta do prédio no mundo (o motor não o lista como ocupante): a casa em foco o desenha como
## ocupante guiado, com a aparência e a posição do boneco do mundo, no andar `level`. O motor continua dizendo onde ele está.
var guided_id := -1


func guide_body(id: int, from_actor: Node3D, level: int) -> Node3D:
	var actor = occupant_nodes.get(id)
	if actor == null:
		actor = NpcFigure.create(self, id, from_actor.cell, -1, _labels, from_actor.look, figure_context)
		occupant_nodes[id] = actor
		actor.position = from_actor.position
		if actor.spec.is_empty(): # sem o dado do motor para vestir: ao menos a escala e o ofício que o boneco do mundo já mostra
			actor.scale = from_actor.scale
	guided_id = id
	actor.driven = true
	actor.set_self(true)
	place_guided(level)
	return actor


func release_guided() -> void:
	var actor = occupant_nodes.get(guided_id)
	if actor != null and not _occupants.any(func(o): return (int(o.npc.value) if o.npc is Dictionary else int(o.npc)) == guided_id):
		actor.free()
		occupant_nodes.erase(guided_id)
	guided_id = -1


func place_guided(level: int) -> void:
	var actor = occupant_nodes.get(guided_id)
	if actor == null:
		return
	actor.position.y = level * Grid.floor_height()
	actor.set_meta("floor", level)
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
			actor = NpcFigure.create(self, id, cell, action, _labels, WorldStore.look_of(occupant.get("appearance")), figure_context)
			occupant_nodes[id] = actor
		elif actor.spec.is_empty(): # o dado do motor chegou depois do boneco
			NpcFigure.dress(actor, id, figure_context)
		if actor.action != action or actor.use_pose != String(use.pose):
			actor.set_action(action, _labels, String(use.pose))
		actor.enable_bed(not use.active) # a cama do catálogo substitui a do NPC
		if actor.driven: # #226: o corpo do jogador anda pela posição fina (colisão nos painéis); a célula do motor só corrige o andar
			actor.position.y = level * Grid.floor_height()
		else:
			actor.position = use.position if use.active else Grid.to_world(cell, level * Grid.floor_height())
			actor.rotation.y = use.facing if use.active else actor.rotation.y
		actor.set_meta("floor", level)
	for id in occupant_nodes.keys():
		if not wanted.has(id) and id != guided_id:
			occupant_nodes[id].free()
			occupant_nodes.erase(id)
	_apply_occupant_visibility()


## Posição na fila de cada objeto em uso (por id do NPC), por andar: o 1º usa a 1ª peça do andar, o 2º a 2ª, voltando ao começo se faltar peça.
func _use_ranks(occupants: Array) -> Dictionary:
	var by_object := {}
	for occupant in occupants:
		var object_id = occupant.get("usingObject")
		if object_id != null:
			var npc_id: int = int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc)
			var key := "%s@%d" % [String(object_id), _floor_of(occupant)]
			by_object[key] = by_object.get(key, []) + [npc_id]
	var ranks := {}
	for key in by_object:
		var ids: Array = by_object[key]
		ids.sort()
		for i in ids.size():
			ranks[ids[i]] = i
	return ranks


func _floor_of(occupant: Dictionary) -> int:
	return int(occupant.floor.value) if occupant.floor is Dictionary else int(occupant.floor)


## {active, position, facing, pose}: onde o NPC fica e que pose faz usando o objeto que o servidor apontou; inativo
## (sem objeto, sem peça colocada neste andar ou sem ponto de uso) = posição e pose de sempre. #237: o ponto de uso gira com a peça
## (o motor a vira para a parede) e o corpo deitado fica centrado nele, sobre a cama.
func _use_of(occupant: Dictionary, npc_id: int, action: int, level: int, rank: Dictionary) -> Dictionary:
	var none := {"active": false, "position": Vector3.ZERO, "facing": 0.0, "pose": ""}
	var object_id = occupant.get("usingObject")
	if object_id == null or not _use_slots.has(String(object_id)):
		return none
	var point := _use_point(String(object_id), action)
	if point.is_empty():
		return none
	var slots: Array = _use_slots[String(object_id)].filter(func(slot): return int(slot.level) == level)
	if slots.is_empty():
		return none
	var slot: Dictionary = slots[int(rank.get(npc_id, 0)) % slots.size()]
	var offset: Array = point.offset
	var base: Vector3 = slot.base + Basis(Vector3.UP, float(slot.yaw)) * Vector3(float(offset[0]), float(offset[1]), float(offset[2]))
	return {"active": true, "position": base, "facing": float(slot.yaw) + deg_to_rad(float(point.get("facing", 0.0))), "pose": point.get("pose", "")}


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


## Centro da planta do térreo no mundo (unidades), para o foco da câmera e o fundo.
func center() -> Vector3:
	return _center * Grid.CELL


## Maior lado da planta do térreo, em células.
func span_cells() -> float:
	return _span


func _compute_center(info: Dictionary) -> void:
	var lo := Vector2(1e9, 1e9)
	var hi := Vector2(-1e9, -1e9)
	for entry in info.layout[0].cells if info.layout.size() > 0 else []:
		lo = Vector2(minf(lo.x, entry.cell.x), minf(lo.y, entry.cell.y))
		hi = Vector2(maxf(hi.x, entry.cell.x), maxf(hi.y, entry.cell.y))
	_span = maxf(hi.x - lo.x, hi.y - lo.y) + 1.0 if hi.x >= lo.x else 0.0
	_bounds = Rect2(Vector2(origin) + lo, hi - lo + Vector2.ONE) if hi.x >= lo.x else Rect2()
	_center = Vector3(origin.x + (lo.x + hi.x) / 2.0 + 0.5, 0, origin.y + (lo.y + hi.y) / 2.0 + 0.5)


## Corte de maquete: paredes entre a câmera e o centro ficam baixas; as do fundo seguem altas.
## `direction` = direção horizontal do centro para a câmera.
func set_view_direction(direction: Vector3) -> void:
	var flat := Vector3(direction.x, 0, direction.z)
	if immersive: # a câmera está dentro da casa: nenhuma parede baixa (aplica uma vez)
		if _immersive_applied:
			return
		_immersive_applied = true
		flat = Vector3.ZERO
	else:
		if flat.length() < 0.001:
			return
		flat = flat.normalized()
		if _immersive_applied:
			_immersive_applied = false
			_view_dir = Vector3.ZERO # voltou ao corte de maquete: refaz
		if flat.is_equal_approx(_view_dir):
			return
	_view_dir = flat
	for wall in _walls:
		var cell: Vector2i = wall.cell
		var offset := Grid.to_world(cell) + Vector3(wall.get("edge", Vector3.ZERO)) * 2.0 - _center
		var near: bool = not immersive and offset.dot(flat) > 0.4
		var wall_node: Node3D = wall.node
		if wall.get("lintel", false): # a verga some no lado cortado (a parede ali está na altura do joelho)
			wall_node.visible = not near
			continue
		var height := cutaway_height() if near else wall_height()
		var box: BoxMesh = wall.mesh
		box.size = Vector3(box.size.x, height, box.size.z)
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
