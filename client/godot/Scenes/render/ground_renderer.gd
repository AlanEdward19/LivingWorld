extends Node3D
## Chão da cidade que o motor expõe (AD-177): ruas da camada `Roads` (largura e acabamento pela superfície, largo e rua
## principal distinguíveis, mais larga com mais gente) e o lote de cada prédio (quintal cercado com chão próprio, caminho
## da porta até a rua). Nada inventado: sem `Roads` não há rua; sem `lot.croft` não há quintal; o resto é o terreno do
## mapa. Duas MultiMesh (ruas, lotes) com uma caixa unitária e UM material compartilhado; cada peça é escala + cor.

const Grid := preload("res://Scenes/grid.gd")
const Scenery := preload("res://Scenes/scenery.gd")
const Style := preload("res://Scenes/render/street_style.gd")
const Ground := preload("res://Store/city_ground.gd")
const Registry := preload("res://Store/entity_registry.gd")

const DIRS: Array[Vector2i] = [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]
const ROAD_Y := 0.014
const PATH_Y := 0.018
const CROFT_Y := 0.008
const FENCE_COLOR := Color("6b5236")

var store
var roads_instance: MultiMeshInstance3D
var lots_instance: MultiMeshInstance3D
var lot_cells: Dictionary = {} ## célula -> {kind: croft|path, building}
var drawn_roads: Dictionary = {} ## célula -> rua desenhada agora (o filtro do escopo já aplicado)
var road_pieces := 0 ## testável: quantas peças a malha de ruas tem
var lot_pieces := 0
var _box := BoxMesh.new()
var _material := StandardMaterial3D.new()
var _pieces: Array = [] ## [Transform3D, Color] em construção


func _init() -> void:
	_box.size = Vector3.ONE
	_material.vertex_color_use_as_albedo = true
	_material.roughness = 0.95
	roads_instance = _instance("Roads")
	lots_instance = _instance("Lots")


func _instance(label: String) -> MultiMeshInstance3D:
	var node := MultiMeshInstance3D.new()
	node.name = label
	node.material_override = _material
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.use_colors = true
	multi.mesh = _box
	node.multimesh = multi
	add_child(node)
	return node


## Redesenha ruas e lotes (os dois se ligam: o caminho da porta encosta na rua). Barato: só escala e cor por peça.
func rebuild() -> void:
	_select_roads()
	rebuild_lots()
	_draw_roads()


## Ruas do escopo: na cidade, as dela (pelo id ou dentro dos limites); no mapa-múndi, todas.
func _select_roads() -> void:
	drawn_roads = {}
	if store != null and store.kind in ["World", "City"]:
		var bounds: Rect2i = store.city.get("bounds", Rect2i()) if store.kind == "City" else Rect2i()
		var city_id := String(store.city.get("id", "")) if store.kind == "City" else ""
		for cell in store.roads:
			var road: Dictionary = store.roads[cell]
			if city_id == "" or String(road.city) == city_id or bounds.has_point(cell):
				drawn_roads[cell] = road


func _draw_roads() -> void:
	_pieces = []
	for cell in drawn_roads:
		_road_cell(cell, Style.style_of(drawn_roads[cell]))
	road_pieces = _flush(roads_instance)


func _road_cell(cell: Vector2i, style: Dictionary) -> void:
	var w := float(style.width)
	var jitter := float(style.jitter)
	var shade := 0.93 + Scenery.cell_hash(cell, 5) * 0.12
	var color: Color = Color(style.color) * shade
	color.a = 1.0
	var center := Grid.to_world(cell, ROAD_Y)
	center += Vector3(Scenery.cell_hash(cell, 1) - 0.5, 0, Scenery.cell_hash(cell, 2) - 0.5) * 2.0 * jitter
	var turn := (Scenery.cell_hash(cell, 3) - 0.5) * jitter * 4.0
	_piece(center, Vector3(w, 0.02, w), color, turn)
	if w < 1.0:
		for dir in DIRS:
			var next := cell + dir
			if not drawn_roads.has(next) and not (lot_cells.get(next, {}).get("kind", "") == "path"):
				continue
			var other := w if not drawn_roads.has(next) else minf(w, float(Style.style_of(drawn_roads[next]).width))
			_arm(center, cell, dir, other, color, ROAD_Y)
	match String(style.detail):
		"pebbles":
			for i in 4:
				var at := center + Vector3(Scenery.cell_hash(cell, 10 + i) - 0.5, 0.012, Scenery.cell_hash(cell, 20 + i) - 0.5) * w * 0.8
				_piece(at, Vector3(0.07, 0.03, 0.06), Color("a8a093") * (0.9 + Scenery.cell_hash(cell, 30 + i) * 0.2))
		"slabs":
			var half := w / 2.0
			for i in 4:
				var at := center + Vector3((float(i % 2) - 0.5) * half, 0.006, (float(i / 2) - 0.5) * half)
				_piece(at, Vector3(half - 0.04, 0.025, half - 0.04), Color("8e8c88") * (0.88 + Scenery.cell_hash(cell, 40 + i) * 0.2))
	if bool(style.main) and not bool(style.square):
		_piece(center + Vector3(0, 0.008, 0), Vector3(0.08, 0.022, 0.08), Color("5e5348")) # marco da rua principal


## Braço do centro da célula até a borda na direção `dir` (rua ou caminho contínuo de uma célula à outra).
func _arm(center: Vector3, cell: Vector2i, dir: Vector2i, width: float, color: Color, y: float) -> void:
	var edge := Grid.to_world(cell, y) + Vector3(dir.x, 0, dir.y) * 0.5
	var mid := (center + edge) / 2.0
	var length := maxf(absf(edge.x - center.x) if dir.x != 0 else absf(edge.z - center.z), 0.05) + width * 0.25
	var size := Vector3(length, 0.02, width) if dir.x != 0 else Vector3(width, 0.02, length)
	_piece(Vector3(mid.x, y, mid.z), size, color)


## Lotes do escopo cidade: quintal (chão + cerca em volta, com a porteira para o caminho) e caminho da porta à rua.
func rebuild_lots() -> void:
	lot_cells = {}
	_pieces = []
	if store != null and store.kind == "City":
		lot_cells = Ground.lot_cells(store.buildings)
		for id in store.buildings:
			_lot(store.buildings[id])
	lot_pieces = _flush(lots_instance)


func _lot(building: Dictionary) -> void:
	var parcel: Dictionary = building.get("lot", {})
	if parcel.is_empty():
		return
	var footprint := {}
	for entry in building.footprint:
		footprint[building.cell + entry.cell] = true
	var croft := {}
	for cell in parcel.croft:
		croft[cell] = true
	var path := {}
	for cell in parcel.front_path:
		path[cell] = true
	for cell in croft:
		_piece(Grid.to_world(cell, CROFT_Y), Vector3(0.98, 0.016, 0.98), Style.CROFT_COLOR * (0.92 + Scenery.cell_hash(cell, 7) * 0.14))
		for dir in DIRS:
			var next: Vector2i = cell + dir
			if not croft.has(next) and not footprint.has(next) and not path.has(next):
				_fence(cell, dir)
	var street = parcel.street
	for cell in path:
		var center := Grid.to_world(cell, PATH_Y)
		_piece(center, Vector3(0.34, 0.02, 0.34), Style.PATH_COLOR)
		for dir in DIRS:
			var next: Vector2i = cell + dir
			if path.has(next) or footprint.has(next) or (street != null and next == street) or drawn_roads.has(next):
				_arm(center, cell, dir, 0.34, Style.PATH_COLOR, PATH_Y)


## Cerca de varas no lado `dir` da célula: dois postes e duas travessas.
func _fence(cell: Vector2i, dir: Vector2i) -> void:
	var edge := Grid.to_world(cell) + Vector3(dir.x, 0, dir.y) * 0.47
	var along := Vector3(absf(dir.y), 0, absf(dir.x))
	for rail in [0.16, 0.32]:
		_piece(edge + Vector3(0, rail, 0), along * 0.94 + Vector3(0.035, 0.035, 0.035) * (Vector3.ONE - along), FENCE_COLOR)
	for side in [-0.45, 0.45]:
		_piece(edge + along * side + Vector3(0, 0.2, 0), Vector3(0.06, 0.4, 0.06), FENCE_COLOR * 0.85)


func _piece(at: Vector3, size: Vector3, color: Color, turn := 0.0) -> void:
	var basis := Basis(Vector3.UP, turn) * Basis.from_scale(size) if turn != 0.0 else Basis.from_scale(size)
	_pieces.append([Transform3D(basis, at), color])


func _flush(node: MultiMeshInstance3D) -> int:
	var multi := node.multimesh
	multi.instance_count = _pieces.size()
	for i in _pieces.size():
		multi.set_instance_transform(i, _pieces[i][0])
		multi.set_instance_color(i, _pieces[i][1])
	var count := _pieces.size()
	_pieces = []
	node.visible = count > 0
	return count


## O que o chão diz nesta célula: rua (ficha da rua) ou lote (ficha do prédio dono); {} = terreno comum.
func entity_at(cell: Vector2i) -> Dictionary:
	if drawn_roads.has(cell):
		return Registry.ref(Registry.STREET, "%d,%d" % [cell.x, cell.y])
	if lot_cells.has(cell):
		return Registry.ref(Registry.BUILDING, int(lot_cells[cell].building))
	return {}
