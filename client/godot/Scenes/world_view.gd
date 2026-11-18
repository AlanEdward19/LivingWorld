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
const GroundRenderer := preload("res://Scenes/render/ground_renderer.gd")
const RouteLine := preload("res://Scenes/render/route_line.gd")
const BuildingLabels := preload("res://Scenes/render/building_labels.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
const WeatherRenderer := preload("res://Scenes/render/weather_renderer.gd")
const WeatherLook := preload("res://Scenes/render/weather_look.gd")
const Grid := preload("res://Scenes/grid.gd")

signal weather_changed(weather: Dictionary) ## tempo sob a câmera mudou (o app atualiza o sol e o indicador)

const TERRAIN_COLORS := TerrainRenderer.TERRAIN_COLORS
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
var ground: GroundRenderer ## ruas e lotes que o motor expõe (AD-177)
var building_labels: BuildingLabels ## função/nome dos prédios, só de perto
var route_line: RouteLine ## rota restante de quem está selecionado ou sendo seguido (AD-181)
var followed_npc := -1 ## o app informa: pessoa seguida (F) — a rota dela também aparece
var registry ## EntityRegistry (nomes dos tipos de prédio para os rótulos)
var zoom_size := 20.0 ## o app informa: rótulos só aparecem perto
var _ground_dirty := false
var weather: WeatherRenderer ## chuva, neve e granizo do tempo do motor (AD-180)
var snow: GPUParticles3D:
	get:
		return weather.snow
var _ground_signature := ""
var _weather_poll := 0.0
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
	ground = GroundRenderer.new()
	ground.name = "GroundRenderer"
	add_child(ground)
	building_labels = BuildingLabels.new()
	building_labels.name = "BuildingLabels"
	add_child(building_labels)
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
	route_line = RouteLine.new()
	route_line.name = "RouteLineRenderer"
	add_child(route_line)
	weather = WeatherRenderer.new()
	weather.name = "Weather"
	add_child(weather)


func bind(p_store, p_labels) -> void:
	store = p_store
	labels = p_labels
	npcs.store = store
	npcs.labels = labels
	processes.store = store
	crops.store = store
	fauna.store = store
	items.store = store
	ground.store = store
	store.roads_changed.connect(_mark_ground)
	store.building_removed.connect(_on_building_gone)
	npcs.building_point = building_center
	route_line.target_of = npcs.goal_point
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
	ground.rebuild()
	_ground_dirty = false
	_relabel()


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
	_relabel()


## Chão (rua ou lote) mudou: redesenha uma vez no próximo quadro, por mais deltas que cheguem juntos.
func _mark_ground() -> void:
	_ground_dirty = true


func _on_building_gone(_id: int) -> void:
	_mark_ground()
	_relabel()


func _relabel() -> void:
	building_labels.rebuild(store, registry, buildings.nodes)


## Estação e tempo: paleta do chão e das copas, chão das ruas e o que cai do céu, tudo como o motor diz.
func _apply_climate() -> void:
	terrain.apply_season(store.climate)
	var signature := TerrainRenderer.season_signature(store.climate)
	if signature != _ground_signature: # rua, caminho e quintal só redesenham quando o chão do tempo muda (não a cada hora)
		_ground_signature = signature
		_mark_ground()
	_set_weather(store.weather_at(Vector2i(int(view_center.x), int(view_center.z))))


func _set_weather(here: Dictionary) -> void:
	weather.apply(here)
	weather_changed.emit(here)


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
	_mark_ground()
	_relabel()


func _on_npc_removed(id: int) -> void:
	npcs.on_removed(id)


## Usado pelos testes e pela vista de prédio: mesmo desenho do renderizador.
func _make_building(info: Dictionary) -> Node3D:
	return buildings.make_building(info)


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
	if zoom_size <= NEAR_ZOOM:
		return "near" # bem perto, também no mapa-múndi (quem está no campo, fora das cidades)
	return "world" if scope_kind == "World" else "city"


func set_lod(level: String) -> void:
	lod = level
	npcs.detail_zoom = level == "near"


func _process(delta: float) -> void:
	npcs.advance(delta, view_center, view_radius, far_zoom, bubble_zoom)
	route_line.step(delta, [npcs.selected, followed_npc], npcs.nodes)
	if _ground_dirty:
		_ground_dirty = false
		ground.rebuild(true) # rua por delta: nasce, muda e some em transição
	ground.step(delta)
	fauna.step(delta) # animais deslizam entre os deltas (AD-188)
	building_labels.set_zoom(zoom_size, lod != "world")
	events.advance(delta)
	_weather_poll -= delta
	if _weather_poll <= 0.0 and store != null and store.kind == "World": # no mapa-múndi, o tempo é o da região sob a câmera
		_weather_poll = 0.5
		var here: Dictionary = store.weather_at(Vector2i(int(view_center.x), int(view_center.z)))
		if here != weather.weather:
			_set_weather(here)
	weather.follow(view_center, view_radius, WeatherLook.lod_ratio(lod, far_zoom, bubble_zoom))
