extends Node3D
## Mundo e cidade em 3D isométrico: coordena um renderizador por tipo de entidade (terreno, cidade, prédio, NPC,
## processo, marcador de evento). Lê a WorldStore e reage aos sinais dela; nunca fala com o servidor.

const TerrainRenderer := preload("res://Scenes/render/terrain_renderer.gd")
const CityRenderer := preload("res://Scenes/render/city_renderer.gd")
const BuildingRenderer := preload("res://Scenes/render/building_renderer.gd")
const NpcRenderer := preload("res://Scenes/render/npc_renderer.gd")
const ProcessRenderer := preload("res://Scenes/render/process_renderer.gd")
const EventMarkers := preload("res://Scenes/render/event_markers.gd")
const Grid := preload("res://Scenes/grid.gd")

const TERRAIN_COLORS := TerrainRenderer.TERRAIN_COLORS
const STREET_COLOR := TerrainRenderer.STREET_COLOR
const YARD_COLOR := TerrainRenderer.YARD_COLOR
const STEP_SECONDS := NpcRenderer.STEP_SECONDS

var store
var labels
var terrain: TerrainRenderer
var cities: CityRenderer
var buildings: BuildingRenderer
var npcs: NpcRenderer
var processes: ProcessRenderer
var events: EventMarkers

var view_center := Vector3(15, 0, 15) ## o app informa o ponto de vista
var view_radius := 40.0 ## raio (em células) dentro do qual NPCs animam por inteiro
var bubble_zoom := false ## mapa-múndi muito afastado: NPCs viram bolhas
var far_zoom := false ## câmera longe: NPCs em versão simples

var npc_nodes: Dictionary:
	get:
		return npcs.nodes
var city_nodes: Dictionary:
	get:
		return cities.nodes
var building_nodes: Dictionary:
	get:
		return buildings.nodes
var terrain_root: Node3D:
	get:
		return terrain.terrain_root
var window_material: StandardMaterial3D:
	get:
		return buildings.window_material
var lamp_material: StandardMaterial3D:
	get:
		return buildings.lamp_material


func _init() -> void:
	terrain = TerrainRenderer.new()
	terrain.name = "TerrainRenderer"
	add_child(terrain)
	cities = CityRenderer.new()
	cities.name = "CityRenderer"
	add_child(cities)
	buildings = BuildingRenderer.new()
	buildings.name = "BuildingRenderer"
	add_child(buildings)
	npcs = NpcRenderer.new()
	npcs.name = "NpcRenderer"
	add_child(npcs)
	processes = ProcessRenderer.new()
	processes.name = "ProcessRenderer"
	add_child(processes)
	events = EventMarkers.new()
	events.name = "EventMarkers"
	add_child(events)


func bind(p_store, p_labels) -> void:
	store = p_store
	labels = p_labels
	npcs.store = store
	npcs.labels = labels
	processes.store = store
	store.reloaded.connect(_on_reloaded)
	store.npc_changed.connect(npcs.on_changed)
	store.npc_removed.connect(npcs.on_removed)
	store.cities_changed.connect(_rebuild_cities)
	store.buildings_changed.connect(_on_buildings_changed)
	store.building_removed.connect(buildings.remove)
	store.processes_changed.connect(_rebuild_processes)


func _on_reloaded(kind: String) -> void:
	if kind == "Interior":
		return
	terrain.rebuild(store)
	_rebuild_cities()
	buildings.rebuild(store)
	_rebuild_npcs()
	_rebuild_processes()
	events.clear()


func _rebuild_cities() -> void:
	cities.rebuild(store)


func _rebuild_npcs() -> void:
	npcs.rebuild()


func _rebuild_processes() -> void:
	processes.bounds_filter = store.city.get("bounds", Rect2i()) if store.kind == "City" else Rect2i()
	processes.rebuild()


func _on_buildings_changed(ids: Array) -> void:
	if store.kind != "City":
		return
	for id in ids:
		if store.buildings.has(id):
			buildings.upsert(store.buildings[id])


func _on_npc_removed(id: int) -> void:
	npcs.on_removed(id)


## Usado pelos testes e pela vista de prédio: mesmo desenho do renderizador.
func _make_building(info: Dictionary) -> Node3D:
	return buildings.make_building(info)


func _settlement_cells() -> Dictionary:
	return TerrainRenderer.settlement_cells(store)


func highlight_npc(id: int) -> void:
	npcs.highlight(id)


func hover_npc(id: int) -> void:
	npcs.hover(id)


func apply_names() -> void:
	npcs.apply_names()


func city_at(point: Vector3) -> String:
	var cell := Grid.to_cell(point)
	for id in store.cities:
		if (store.cities[id].bounds as Rect2i).has_point(cell):
			return id
	return ""


## Centro de um prédio desenhado (para câmera, seguir e marcadores); Vector3.INF se não está na cena.
func building_center(id: int) -> Vector3:
	var info = store.buildings.get(id) if store != null else null
	if info == null:
		return Vector3.INF
	var lo := Vector2i(1 << 20, 1 << 20)
	var hi := Vector2i(-(1 << 20), -(1 << 20))
	for entry in info.footprint:
		var cell: Vector2i = info.cell + entry.cell
		lo = Vector2i(mini(lo.x, cell.x), mini(lo.y, cell.y))
		hi = Vector2i(maxi(hi.x, cell.x), maxi(hi.y, cell.y))
	if info.footprint.is_empty():
		return Grid.to_world(info.cell) + Vector3(0.5, 0, 0.5)
	return Vector3((lo.x + hi.x + 1) / 2.0, 0, (lo.y + hi.y + 1) / 2.0)


func _process(delta: float) -> void:
	npcs.advance(delta, view_center, view_radius, far_zoom, bubble_zoom)
	events.advance(delta)
