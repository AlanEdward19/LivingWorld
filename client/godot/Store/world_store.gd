extends RefCounted
## Estado local do cliente: espelho do que o servidor projeta, reconstruível por snapshot + replay.
## Nenhuma regra de simulação aqui; só aplicar snapshot/deltas e avisar a cena.

signal reloaded(kind: String)
signal npc_changed(id: int, from_cell: Vector2i, to_cell: Vector2i, action: int)
signal npc_removed(id: int)
signal cities_changed()
signal interior_changed()
signal buildings_changed(ids: Array) ## prédios novos ou movidos por delta: o renderizador redesenha só esses
signal building_removed(id: int)
signal processes_changed() ## processos em andamento (descanso, comida, recurso, lavoura, carga, obra) mudaram
signal indicators_changed() ## indicadores da cidade em foco vieram no delta
signal events_arrived(events: Array) ## eventos notáveis do delta, com `location` quando o servidor sabe onde
signal npc_observed(id: int) ## sinais visíveis do NPC (corpo, roupa, carga, emoção, alvo) mudaram
signal building_states_changed(ids: Array) ## função, dono, moradores, estoque
signal crops_changed()
signal fauna_changed() ## animais (cidade) ou bandos (mundo)
signal items_changed() ## espólio no chão e objetos inventados
signal climate_changed()
signal works_changed(ids: Array) ## AD-193: obras em andamento (fração, turma, quem está no lote, parada) mudaram
signal roads_changed() ## ruas mudaram (retrato, delta `roadUpserts`/`roadRemoved` ou releitura da camada): só o chão redesenha

const FrameCost := preload("res://Contract/frame_cost.gd")
const NO_ACTION := -1
const Parse := preload("res://Store/observable_parse.gd")
const Ground := preload("res://Store/city_ground.gd")
const Roads := preload("res://Store/road_book.gd")
const ScopeDelta := preload("res://Store/scope_delta.gd")
const Works := preload("res://Store/work_book.gd")

var kind := ""
var map_size := Vector2i.ZERO
var rivers: Dictionary = {} ## Vector2i -> true
var roads: Dictionary = {} ## Vector2i -> rua do motor (Ground.road: cidade, superfície, uso, pisoteio, obra, extra)
var roads_live := false ## o escopo manda `livingState.roads` e delta de rua (AD-181): a releitura de 30 s não roda
var finished_works: Dictionary = {} ## cidade -> células onde a última obra de rua acabou (lugar do `StreetWorkDone`)
var terrain: Dictionary = {} ## Vector2i -> int (id de terreno), vem do mundo e persiste entre escopos
var names: Dictionary = {} ## npc id -> nome (vem do escopo: `observable.name`)
var cities: Dictionary = {} ## id (String) -> {id,name,location,population,bounds:Rect2i,density}
var npcs: Dictionary = {} ## id (int) -> {id,cell,action,city,look,relocation,obs,target,intent,intent_target,started}
var buildings: Dictionary = {} ## id (int) -> {id,type,cell,orientation,floors,footprint:Array,value:int,lodging:{units,occupied,rentPerMonth,nightlyRate}|null}
var city: Dictionary = {} ## CitySnapshot do escopo cidade: {id,name,location,bounds,indicators}
var interior: Dictionary = {} ## {building,city,floors,layout:Array[{level,cells}],occupants:Array}
var processes: Dictionary = {} ## id (int) -> {id,kind,target,progress,descriptor,quality,remaining,cell:Vector2i,has_cell}
var pending_footprints: Dictionary = {} ## id de prédio vindo por delta sem planta -> true (servidor antigo: canteiro)
var building_states: Dictionary = {} ## id do prédio -> {function, type_name, owner_id, residents, workers, stock, ...}
var crops: Dictionary = {} ## id -> lavoura (Parse.crop)
var animals: Dictionary = {} ## id -> animal (escopo Cidade)
var fauna_groups: Dictionary = {} ## "espécie:região" -> bando (escopo Mundo)
var leavings: Dictionary = {} ## id -> espólio no chão
var objects: Dictionary = {} ## id -> peça inventada
var works: Dictionary = {} ## AD-193: id da obra -> obra em andamento (Works.parse), só no escopo Cidade
var climate: Dictionary = {} ## estação e clima do escopo (Parse.climate); vazio = não modelado
var last_tick := -1 ## tick do último snapshot ou delta aplicado (AD-172: processos rest/food avançam sozinhos)
var climate_regions: Array = [] ## camada Climate do mundo (Parse.climate_regions): [{region, cell, temperature, drought, weather}]


static func scope_name(value: Variant) -> String:
	if value is String:
		return value
	return ["World", "City", "Interior"][clampi(int(value), 0, 2)]


static func cell_of(value: Variant) -> Vector2i:
	return Vector2i(int(value.x), int(value.y)) if value is Dictionary else Vector2i.ZERO


static func id_of(value: Variant) -> Variant:
	if value is Dictionary and value.has("value"):
		return value.value if value.value is String else int(value.value)
	return value


static func rect_of(value: Dictionary) -> Rect2i:
	return Rect2i(int(value.x), int(value.y), int(value.width), int(value.height))


## Tempo do dia (AD-180) num ponto do mapa: no mapa-múndi, o da região de centro mais perto (camada `Climate`); senão, o do
## escopo. {} = o motor não mandou tempo.
func weather_at(cell: Vector2i) -> Dictionary:
	if kind == "World":
		var best: Dictionary = {}
		var best_d := INF
		for region in climate_regions:
			var d := Vector2(cell).distance_squared_to(Vector2(region.cell))
			if not region.weather.is_empty() and d < best_d:
				best_d = d
				best = region.weather
		if not best.is_empty():
			return best
	return climate.get("weather", {})


func apply_snapshot(envelope: Dictionary) -> void:
	kind = scope_name(envelope.scope.kind)
	var cursor = envelope.get("cursor")
	last_tick = int(cursor.get("tick", -1)) if cursor is Dictionary else -1
	var payload = envelope.get("payload")
	if payload == null:
		npcs.clear()
		buildings.clear()
		processes.clear()
		_clear_observable()
		reloaded.emit(kind)
		return
	match kind:
		"World":
			_load_world(payload)
		"City":
			_load_city(payload)
		"Interior":
			_load_interior(payload)
	reloaded.emit(kind)


func _load_world(payload: Dictionary) -> void:
	map_size = Vector2i(int(payload.width), int(payload.height))
	var layer = payload.get("layers", {}).get("Terrain")
	if layer != null and layer.get("isModeled", false):
		terrain.clear()
		for entry in layer.payload:
			terrain[cell_of(entry.key)] = int(entry.value.id)
	var river_layer = payload.get("layers", {}).get("Rivers")
	rivers.clear()
	if river_layer != null and river_layer.get("isModeled", false):
		for entry in river_layer.payload:
			rivers[cell_of(entry)] = true
	_load_roads(payload.get("livingState"), payload.get("layers", {}).get("Roads"))
	var climate_layer = payload.get("layers", {}).get("Climate")
	climate_regions = Parse.climate_regions(climate_layer.payload.get("regions", [])) if climate_layer is Dictionary \
		and climate_layer.get("isModeled", false) and climate_layer.get("payload") is Dictionary else []
	var state: Dictionary = payload.get("livingState", {}) if payload.get("livingState") is Dictionary else {}
	var densities: Dictionary = {}
	for living_city in _list(state, "cities"):
		densities[String(id_of(living_city.id))] = Parse.density(living_city.get("density"))
	cities.clear()
	for marker in payload.cities:
		var id: String = id_of(marker.id)
		cities[id] = {
			"id": id, "name": marker.name, "location": cell_of(marker.location),
			"population": int(marker.population), "bounds": rect_of(marker.bounds), "density": densities.get(id, {}),
		}
	npcs.clear()
	buildings.clear()
	var living: Dictionary = {}
	for visual in _list(state, "npcs"):
		living[id_of(visual.id)] = visual
	for marker in payload.externalNpcs:
		var npc_id: int = id_of(marker.id)
		var visual: Dictionary = living.get(npc_id, {})
		npcs[npc_id] = _npc(npc_id, cell_of(marker.location), visual.get("currentAction"), "", look_of(marker.get("appearance")),
			visual.get("relocationDestination"))
		_observe(npcs[npc_id], visual)
	_load_processes(state)
	_load_observable(state)
	cities_changed.emit()


func _load_city(payload: Dictionary) -> void:
	var bounds := rect_of(payload.bounds)
	city = {
		"id": id_of(payload.id), "name": payload.name, "location": cell_of(payload.location),
		"bounds": bounds, "indicators": payload.indicators,
	}
	npcs.clear()
	# Os deltas do servidor descrevem o estado "vivo" do escopo (NPC fora dos limites ou dentro de um prédio
	# sai da cidade); o snapshot usa a mesma fonte para o cliente e o servidor concordarem sobre quem é visível.
	var living = payload.get("livingState")
	var visible: Array = living.npcs if living != null and living.get("npcs") != null else payload.residents
	for marker in visible:
		var id: int = id_of(marker.id)
		npcs[id] = _npc(id, cell_of(marker.location), marker.get("currentAction"), city.id, look_of(marker.get("appearance")),
			marker.get("relocationDestination"))
		_observe(npcs[id], marker)
	buildings.clear()
	pending_footprints.clear()
	for marker in payload.buildings:
		var id: int = id_of(marker.id)
		buildings[id] = _building(id, marker)
	_load_processes(living if living != null else {})
	_load_observable(living if living is Dictionary else {})
	if living is Dictionary and living.get("roads") is Array: # servidor antigo: ficam as ruas do retrato do mundo
		_load_roads(living)


func _load_processes(living: Variant) -> void:
	processes.clear()
	if living is Dictionary:
		for visual in living.get("processes", []) if living.get("processes") != null else []:
			var process := Parse.process(visual)
			processes[process.id] = process


func _clear_observable() -> void:
	for bag in [building_states, crops, animals, fauna_groups, leavings, objects, works]:
		bag.clear()
	climate = {}


## Prédio, lavoura, fauna, espólio, objetos e clima do snapshot (`livingState`); ausente = vazio.
func _load_observable(state: Dictionary) -> void:
	_clear_observable()
	for raw in _list(state, "buildingStates"):
		var parsed := Parse.building_state(raw)
		building_states[parsed.building_id] = parsed
	for raw in _list(state, "crops"):
		crops[int(raw.id)] = Parse.crop(raw)
	for raw in _list(state, "animals"):
		animals[int(raw.id)] = Parse.animal(raw)
	for raw in _list(state, "faunaGroups"):
		var group := Parse.fauna_group(raw)
		fauna_groups[group.key] = group
	for raw in _list(state, "leavings"):
		leavings[int(raw.id)] = Parse.leaving(raw)
	for raw in _list(state, "objects"):
		objects[int(raw.id)] = Parse.object(raw)
	climate = Parse.climate(state.get("climate"))
	Works.load_snapshot(self, state)


func _load_roads(state: Variant, layer: Variant = null) -> void:
	var loaded := Roads.from_snapshot(state, layer)
	roads = loaded.roads
	roads_live = loaded.live
	finished_works = {}


## Camada `Roads` (releitura periódica, servidor sem delta de rua): troca as ruas e avisa só se mudaram.
func refresh_roads(layer: Variant, notify := true) -> bool:
	var parsed := Ground.roads(layer)
	if parsed == roads:
		return false
	roads = parsed
	if notify:
		roads_changed.emit()
	return true


static func _list(source: Variant, key: String) -> Array:
	return source.get(key) if source is Dictionary and source.get(key) is Array else []


## Lê `observable`, alvo, intenção e início da ação de um NpcVisual; `observable` omitido = não mudou (o NPC que só andou).
## Devolve true se os sinais visíveis ou o alvo mudaram.
func _observe(npc: Dictionary, visual: Dictionary, previous: Variant = null) -> bool:
	var before: Dictionary = previous.get("obs", {}) if previous is Dictionary else {}
	var before_target: Dictionary = previous.get("target", {}) if previous is Dictionary else {}
	npc.obs = Parse.observable(visual.get("observable")) if visual.get("observable") is Dictionary else before
	if previous is Dictionary and visual.get("actionStartedAtTick") == null:
		# AD-172: sem `actionStartedAtTick` o grupo (início, alvo, intenção) é o de antes
		npc.target = previous.get("target", {})
		npc.intent = int(previous.get("intent", -1))
		npc.intent_target = String(previous.get("intent_target", ""))
		npc.started = int(previous.get("started", -1))
	else: # grupo inteiro novo: ausência de alvo/intenção = nulo
		npc.target = Parse.target(visual.get("actionTarget"))
		npc.intent = int(visual.intent) if visual.get("intent") != null else -1
		npc.intent_target = String(visual.intentTarget) if visual.get("intentTarget") != null else ""
		npc.started = int(visual.actionStartedAtTick) if visual.get("actionStartedAtTick") != null else -1
	npc.route = Parse.route(visual.get("route")) # AD-176: rota de quem viaja (ausente = não viaja)
	npc.arrival = int(visual.routeArrivalTick) if visual.get("routeArrivalTick") != null and not npc.route.is_empty() else -1
	# AD-185: saída explícita da viagem e os índices da rota que passam por cima (escada, laje, telhado)
	npc.departed = int(visual.routeDepartedTick) if visual.get("routeDepartedTick") != null and not npc.route.is_empty() else -1
	npc.route_over = Parse.route_over(visual.get("routeOver"), npc.route.size())
	if npc.obs.has("name") and String(npc.obs.name) != "":
		names[int(npc.id)] = String(npc.obs.name)
	return npc.obs != before or npc.target != before_target


func _load_interior(payload: Dictionary) -> void:
	var layout: Dictionary = payload.layout
	interior = {
		"building": id_of(payload.id), "city": id_of(payload.city), "floors": int(layout.floors),
		"layout": layout.floorLayouts, "occupants": payload.occupants,
		"objects": payload.get("objects"), ## catálogo + mobília do servidor (ADR-0034); null = cai no assets.json
	}


func _npc(id: int, cell: Vector2i, action: Variant, city_id: String, look: Dictionary = {}, relocation: Variant = null) -> Dictionary:
	return {"id": id, "cell": cell, "action": NO_ACTION if action == null else int(action), "city": city_id, "look": look,
		"relocation": cell_of(relocation) if relocation is Dictionary else null, "obs": {}, "target": {}, "intent": -1,
		"intent_target": "", "started": -1, "route": [], "arrival": -1, "departed": -1, "route_over": []}


## Genes de aparência do motor ({skinTone,...}) no formato do cliente; vazio quando o mundo não os tem.
static func look_of(appearance: Variant) -> Dictionary:
	if not appearance is Dictionary:
		return {}
	return {"skin_tone": int(appearance.skinTone), "hair_tone": int(appearance.hairTone),
		"hair_curl": int(appearance.hairCurl), "build": int(appearance.build)}


func _building(id: int, marker: Dictionary) -> Dictionary:
	var footprint: Array = []
	for entry in marker.get("groundFootprint", []) if marker.get("groundFootprint") != null else []:
		footprint.append({"cell": cell_of(entry.cell), "material": int(entry.material)})
	return {
		"id": id, "type": Parse._i(marker, "buildingTypeId", -1), "cell": cell_of(marker.get("location")),
		"orientation": Parse._i(marker, "orientation", 0), "floors": Parse._i(marker, "floors", 1),
		"footprint": footprint, "value": Parse._i(marker, "value", 0), "lodging": marker.get("lodging"),
		"lot": Ground.lot(marker.get("lot")), ## AD-173: quintal, caminho da porta e rua da frente; {} = sem lote
		"work": _work_view(marker.get("work")), ## AD-184/193: canteiro (obra em curso no lote); {} = prédio pronto
	}


## `BuildingVisual.work` (id, tipo, fração, parada, turma, ajudantes, oferta): o prédio ainda não existe, é obra.
static func _work_view(raw: Variant) -> Dictionary:
	if not raw is Dictionary or raw.get("id") == null:
		return {}
	return {"id": int(raw.id), "kind": String(raw.get("kind", "")), "progress": clampf(float(raw.get("progress", 0.0)), 0.0, 1.0),
		"stalled": bool(raw.get("stalled", false)), "building_id": -1, "halt": "", "materials": [], "haul_left": 0}


func apply_delta(envelope: Dictionary) -> void:
	var delta: Dictionary = envelope.payload
	if delta.has("entered"): # delta de interior
		_apply_interior_delta(delta)
		return
	var upserted := {}
	# AD-172: id em `npcRemoved` e em `npcUpserts` no mesmo delta = recomeço; o que se sabia é descartado e o upsert vale inteiro
	var reset := {}
	for removed in delta.get("npcRemoved", []):
		reset[id_of(removed)] = true
	var started := Time.get_ticks_usec()
	for visual in delta.get("npcUpserts", []):
		var id: int = id_of(visual.id)
		var previous_known = null if reset.has(id) else npcs.get(id)
		# `location` sempre vem; se faltar (ou vier nula), fica a célula conhecida
		var cell: Vector2i = cell_of(visual.location) if visual.get("location") is Dictionary else (previous_known.cell if previous_known != null else Vector2i.ZERO)
		var action: int = NO_ACTION if visual.get("currentAction") == null else int(visual.currentAction)
		var previous = previous_known
		var from_cell: Vector2i = previous.cell if previous != null else cell
		var city_id: String = String(id_of(visual.city)) if visual.get("city") is Dictionary else (previous.city if previous != null else "")
		# AD-172 (delta enxuto): `city: null` = mesma cidade; `appearance` ausente = a anterior
		var look: Dictionary = look_of(visual.appearance) if visual.get("appearance") is Dictionary else (previous.look if previous != null else {})
		npcs[id] = _npc(id, cell, action, city_id, look, visual.get("relocationDestination"))
		var observed := _observe(npcs[id], visual, previous)
		upserted[id] = true
		npc_changed.emit(id, from_cell, cell, action)
		if observed:
			npc_observed.emit(id)
	FrameCost.add("npc_upserts", started)
	# `moved` é a projeção de compatibilidade (só posição): vale para quem não veio inteiro em npcUpserts
	for entry in delta.get("moved", []):
		var id: int = id_of(entry.npcId)
		var known = npcs.get(id)
		if upserted.has(id) or known == null:
			continue
		var from_cell: Vector2i = known.cell
		if not entry.get("location") is Dictionary:
			continue
		known.cell = cell_of(entry.location)
		if known.cell != from_cell:
			npc_changed.emit(id, from_cell, known.cell, int(known.action))
	for removed in delta.get("npcRemoved", []):
		var id: int = id_of(removed)
		if upserted.has(id):
			continue
		if npcs.erase(id):
			npc_removed.emit(id)
	var changed_buildings: Array = []
	var work_only: Array = []
	for upsert in delta.get("buildingUpserts", []):
		var id: int = id_of(upsert.id)
		var known = buildings.get(id)
		if kind == "City" and upsert.get("cityId") is Dictionary and String(id_of(upsert.cityId)) != String(city.get("id", "")):
			continue
		var fresh := _building(id, upsert) # AD-170: o delta traz a planta completa (andares, planta, valor, hospedagem)
		if known != null and _same_but_work(known, fresh):
			known.work = fresh.work # AD-193: só a obra andou (fração, turma): a obra redesenha pelo `works_changed`
			work_only.append(int(fresh.work.id))
			continue
		buildings[id] = fresh
		if buildings[id].footprint.is_empty() and known != null: # servidor antigo, só posição: vale o que o snapshot trouxe
			buildings[id].footprint = known.footprint
			buildings[id].floors = known.floors
			buildings[id].value = known.value
			buildings[id].lodging = known.lodging
			buildings[id].lot = known.get("lot", {})
		if buildings[id].footprint.is_empty():
			pending_footprints[id] = true # sem planta: canteiro até o próximo retrato (nenhuma leitura extra)
		else:
			pending_footprints.erase(id)
		changed_buildings.append(id)
	if not changed_buildings.is_empty():
		buildings_changed.emit(changed_buildings)
	if not work_only.is_empty():
		works_changed.emit(work_only)
	for removed in delta.get("buildingRemoved", []):
		var id: int = id_of(removed)
		if buildings.erase(id):
			pending_footprints.erase(id)
			building_removed.emit(id)
	var processes_touched := false
	var sent := {}
	for visual in delta.get("processUpserts", []):
		var process := Parse.process(visual)
		processes[process.id] = process
		sent[process.id] = true
		processes_touched = true
	for removed in delta.get("processRemoved", []):
		sent[int(id_of(removed))] = true
		processes_touched = processes.erase(int(id_of(removed))) or processes_touched
	var tick_now := int(delta.get("tick", -1))
	if last_tick >= 0 and tick_now > last_tick:
		processes_touched = Parse.advance_processes(processes, tick_now - last_tick, sent) or processes_touched
	if tick_now >= 0:
		last_tick = tick_now
	if processes_touched:
		processes_changed.emit()
	started = Time.get_ticks_usec()
	ScopeDelta.apply_observable(self, delta)
	FrameCost.add("observable", started)
	started = Time.get_ticks_usec()
	ScopeDelta.apply_roads(self, delta)
	FrameCost.add("roads", started)
	Works.apply_delta(self, delta)
	if kind == "City" and not delta.get("indicators", []).is_empty() and city.has("indicators"):
		for update in delta.indicators:
			if update.get("key") != null and update.get("value") != null:
				city.indicators[String(update.key)] = float(update.value)
		indicators_changed.emit()
	if not delta.get("events", []).is_empty():
		events_arrived.emit(delta.events)
	if not _list(delta, "cityUpserts").is_empty() or not _list(delta, "cityRemoved").is_empty():
		for upsert in _list(delta, "cityUpserts"):
			var id: String = id_of(upsert.id)
			var known_density: Dictionary = cities[id].get("density", {}) if cities.has(id) else {}
			cities[id] = {
				"id": id, "name": Parse._s(upsert, "name"), "location": cell_of(upsert.get("location")),
				"population": Parse._i(upsert, "population", 0), "bounds": rect_of(upsert.bounds),
				"density": Parse.density(upsert.get("density")) if upsert.get("density") is Dictionary else known_density,
			}
		for removed in _list(delta, "cityRemoved"):
			cities.erase(id_of(removed))
		cities_changed.emit()


## O prédio que chegou é o mesmo de antes tirando a obra (o canteiro reenviado só porque a obra andou).
static func _same_but_work(known: Dictionary, fresh: Dictionary) -> bool:
	if known.get("work", {}).is_empty() or fresh.work.is_empty():
		return false
	for key in fresh:
		if key != "work" and known.get(key) != fresh[key]:
			return false
	return true


## Delta de interior do servidor: `entered`, `moved` (célula OU ação mudou), `changedFloor` e `exited`.
## As três primeiras trazem o ocupante completo; `exited` traz só o id.
func _apply_interior_delta(delta: Dictionary) -> void:
	var occupants: Array = interior.get("occupants", [])
	var by_npc := {}
	for occupant in occupants:
		by_npc[int(id_of(occupant.npc))] = occupant
	for exited in delta.get("exited", []):
		by_npc.erase(int(id_of(exited)))
	for key in ["entered", "moved", "changedFloor"]:
		for occupant in delta.get(key, []):
			by_npc[int(id_of(occupant.npc))] = occupant
	interior["occupants"] = by_npc.values()
	interior_changed.emit()
