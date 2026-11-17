extends Node3D
## Terreno (MultiMesh com cor por célula), rios, ruas e quintais da cidade em foco, e a vegetação (Scenery).

const Grid := preload("res://Scenes/grid.gd")
const Scenery := preload("res://Scenes/scenery.gd")
const Season := preload("res://Scenes/render/season_palette.gd")

const TERRAIN_COLORS: Array[Color] = [
	Color("6aa84f"), Color("c9b777"), Color("7a8a5a"), Color("8a8f98"), Color("4a7fa8"), Color("a67c52"),
]
const STREET_COLOR := Color("8f8574")
const YARD_COLOR := Color("6e5c45")

var store
var terrain_root: Node3D
var scenery
var _multi: MultiMesh
var _base: Array = [] ## [cor, paved, água] por instância: a estação recolore sem refazer a malha
var _season_key := "-"


func rebuild(p_store) -> void:
	store = p_store
	if terrain_root != null:
		if terrain_root.is_inside_tree():
			terrain_root.queue_free()
		else:
			terrain_root.free()
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
	_base.clear()
	_multi = multi
	_season_key = "-"
	var paved := settlement_cells(store)
	for cell in store.terrain:
		var id: int = store.terrain[cell]
		var bed_drop := -0.13 if store.rivers.has(cell) else 0.0
		multi.set_instance_transform(index, Transform3D(Basis(), Grid.to_world(cell, -0.15 + bed_drop)))
		var shade := 0.94 + float((cell.x * 7 + cell.y * 13) % 5) * 0.03
		var color: Color = TERRAIN_COLORS[(id - 1) % TERRAIN_COLORS.size()] * shade
		if store.rivers.has(cell):
			color = Color("6d7f5a") * shade # leito sob a água
		elif paved.has(cell):
			color = paved[cell] * (0.92 + float((cell.x + cell.y) % 2) * 0.1) # xadrez de pedra: rua e quintal
		_base.append([color, paved.has(cell), store.rivers.has(cell)])
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
	apply_season(store.climate)


static func season_signature(climate: Dictionary) -> String:
	if climate.is_empty():
		return ""
	return "%s|%s|%s" % [Season.season_key(climate), Season.snowing(climate), bool(climate.get("drought", false))]


## Paleta da estação no chão e nas copas: só recolore as instâncias quando a estação, a neve ou a seca mudam.
func apply_season(climate: Dictionary) -> void:
	var signature := season_signature(climate)
	if signature == _season_key or _multi == null:
		return
	_season_key = signature
	for i in _base.size():
		_multi.set_instance_color(i, Season.ground(_base[i][0], climate, _base[i][1], _base[i][2]))
	if scenery != null:
		scenery.apply_season(climate)


## Dentro de um assentamento o chão deixa de ser mato: ruas de pedra e terra batida sob os prédios.
## Só cor de instância do terreno (custo zero); no mapa-múndi devolve vazio e o chão segue natural.
static func settlement_cells(store) -> Dictionary:
	var cells := {}
	if store.kind != "City" or not store.city.has("bounds"):
		return cells
	var bounds: Rect2i = store.city.bounds
	var yard := {}
	for building in store.buildings.values():
		for entry in building.footprint:
			yard[entry.cell] = true
	for x in range(bounds.position.x, bounds.end.x):
		for y in range(bounds.position.y, bounds.end.y):
			var cell := Vector2i(x, y)
			cells[cell] = STREET_COLOR if not yard.has(cell) else YARD_COLOR
	return cells
