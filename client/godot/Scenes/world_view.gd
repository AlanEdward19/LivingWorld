extends Node3D
## Mundo e cidade em 3D isométrico: coordena um renderizador por tipo de entidade (terreno, cidade, prédio, NPC,
## processo, lavoura, fauna, espólio/objeto, marcador de evento). Lê a WorldStore e reage aos sinais dela; nunca fala
## com o servidor. LOD em três níveis (ADR-0047 etapa 3): MUNDO (densidade das cidades, bandos e eventos), CIDADE
## (corpos, prédios, lavoura, animais, objetos) e PERTO (ferramenta, objeto em uso e rosto dos NPCs perto do centro).

const TerrainRenderer := preload("res://Scenes/render/terrain_renderer.gd")
const CityRenderer := preload("res://Scenes/render/city_renderer.gd")
const BuildingRenderer := preload("res://Scenes/render/building_renderer.gd")
const NpcRenderer := preload("res://Scenes/render/npc_renderer.gd")
const ProcessRenderer := preload("res://Scenes/render/process_renderer.gd")
const EventMarkers := preload("res://Scenes/render/event_markers.gd")
const CropRenderer := preload("res://Scenes/render/crop_renderer.gd")
const FaunaRenderer := preload("res://Scenes/render/fauna_renderer.gd")
const ItemRenderer := preload("res://Scenes/render/item_renderer.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
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
var crops: CropRenderer
var fauna: FaunaRenderer
var items: ItemRenderer
var snow: GPUParticles3D
var lod := "city" ## "world" | "city" | "near" (o app calcula pelo zoom e pelo escopo)
const NEAR_ZOOM := 7.5 ## abaixo deste tamanho de zoom, os NPCs perto do centro mostram ferramenta, objeto e rosto

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
	crops = CropRenderer.new()
	crops.name = "CropRenderer"
	add_child(crops)
	fauna = FaunaRenderer.new()
	fauna.name = "FaunaRenderer"
	add_child(fauna)
	items = ItemRenderer.new()
	items.name = "ItemRenderer"
	add_child(items)
	snow = _make_snow()
	add_child(snow)


func bind(p_store, p_labels) -> void:
	store = p_store
	labels = p_labels
	npcs.store = store
	npcs.labels = labels
	processes.store = store
	crops.store = store
	fauna.store = store
	items.store = store
	npcs.building_point = building_center
	store.reloaded.connect(_on_reloaded)
	store.npc_observed.connect(npcs.apply_observable)
	store.building_states_changed.connect(_on_building_states)
	store.crops_changed.connect(_rebuild_crops)
	store.fauna_changed.connect(_rebuild_fauna)
	store.items_changed.connect(_rebuild_items)
	store.climate_changed.connect(_apply_climate)
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
	_rebuild_crops()
	_rebuild_processes()
	_rebuild_fauna()
	_rebuild_items()
	_apply_climate()
	events.clear()


func _rebuild_cities() -> void:
	cities.labels = labels
	cities.rebuild(store)


func _scope_bounds() -> Rect2i:
	return store.city.get("bounds", Rect2i()) if store.kind == "City" else Rect2i()


func _rebuild_crops() -> void:
	crops.bounds_filter = _scope_bounds()
	crops.rebuild()
	_rebuild_processes() # trabalho na lavoura desenhada vira barra


func _rebuild_fauna() -> void:
	fauna.bounds_filter = _scope_bounds()
	fauna.rebuild()


func _rebuild_items() -> void:
	items.bounds_filter = _scope_bounds()
	items.rebuild()


## Estado novo de prédio: só redesenha os que mudaram de aparência (função, placa, fechado).
func _on_building_states(ids: Array) -> void:
	if store.kind != "City":
		return
	for id in ids:
		if store.buildings.has(id):
			buildings.restyle(store.buildings[id], store.building_states.get(id, {}))


## Estação e clima: paleta do chão e das copas, e neve caindo quando o clima diz.
func _apply_climate() -> void:
	terrain.apply_season(store.climate)
	snow.emitting = Season.snowing(store.climate)
	snow.visible = snow.emitting


func _make_snow() -> GPUParticles3D:
	var particles := GPUParticles3D.new()
	particles.name = "Snow"
	particles.amount = 400
	particles.lifetime = 5.0
	particles.emitting = false
	particles.visible = false
	particles.visibility_aabb = AABB(Vector3(-30, -10, -30), Vector3(60, 30, 60))
	var process := ParticleProcessMaterial.new()
	process.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	process.emission_box_extents = Vector3(22, 0.5, 22)
	process.direction = Vector3(0.1, -1, 0)
	process.spread = 8.0
	process.initial_velocity_min = 1.6
	process.initial_velocity_max = 2.4
	process.gravity = Vector3(0, -0.4, 0)
	particles.process_material = process
	var flake := SphereMesh.new()
	flake.radius = 0.04
	flake.height = 0.08
	flake.radial_segments = 4
	flake.rings = 2
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(1, 1, 1, 0.9)
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	flake.material = material
	particles.draw_pass_1 = flake
	particles.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return particles


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
			buildings.upsert(store.buildings[id], store.building_states.get(id, {}))


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


## Nível de LOD pelo escopo e pelo zoom: mundo, cidade ou perto.
static func lod_for(scope_kind: String, zoom_size: float) -> String:
	if scope_kind == "World":
		return "world"
	return "near" if zoom_size <= NEAR_ZOOM else "city"


func set_lod(level: String) -> void:
	lod = level
	npcs.detail_zoom = level == "near"


func _process(delta: float) -> void:
	npcs.advance(delta, view_center, view_radius, far_zoom, bubble_zoom)
	events.advance(delta)
	if snow.emitting:
		snow.position = view_center + Vector3(0, 12, 0)
