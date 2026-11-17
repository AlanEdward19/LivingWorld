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

const NO_ACTION := -1
const Parse := preload("res://Store/observable_parse.gd")

var kind := ""
var map_size := Vector2i.ZERO
var rivers: Dictionary = {} ## Vector2i -> true
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
var climate: Dictionary = {} ## estação e clima do escopo (Parse.climate); vazio = não modelado
var last_tick := -1 ## tick do último snapshot ou delta aplicado (AD-172: processos rest/food avançam sozinhos)
var climate_regions: Array = [] ## camada Climate do mundo: [{regionId, center, temperatureC, drought}]


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
	var climate_layer = payload.get("layers", {}).get("Climate")
	climate_regions = climate_layer.payload.get("regions", []) if climate_layer is Dictionary and climate_layer.get("isModeled", false) \
		and climate_layer.get("payload") is Dictionary else []
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


func _load_processes(living: Variant) -> void:
	processes.clear()
	if living is Dictionary:
		for visual in living.get("processes", []) if living.get("processes") != null else []:
			var process := _process(visual)
			processes[process.id] = process


func _clear_observable() -> void:
	for bag in [building_states, crops, animals, fauna_groups, leavings, objects]:
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
		"intent_target": "", "started": -1}


## ProcessVisual do servidor: `kind` (rest, food, cook, water, crop, construction, extraordinary-construct, process),
## `targetId` (o NPC que age; a lavoura; o tipo de prédio da obra), progresso 0..1 e célula quando o servidor sabe onde.
static func _process(visual: Dictionary) -> Dictionary:
	var location = visual.get("location")
	return {"id": int(visual.id), "kind": String(visual.kind), "target": int(visual.get("targetId", -1)),
		"progress": clampf(float(visual.get("progress", 0.0)), 0.0, 1.0), "descriptor": String(visual.get("descriptorKey", "")),
		"quality": visual.get("quality"), "remaining": visual.get("remainingHours"),
		"cell": cell_of(location), "has_cell": location is Dictionary}


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
		"id": id, "type": int(marker.buildingTypeId), "cell": cell_of(marker.location),
		"orientation": int(marker.get("orientation", 0)), "floors": int(marker.get("floors", 1)),
		"footprint": footprint, "value": int(marker.get("value", 0)), "lodging": marker.get("lodging"),
	}


func apply_delta(envelope: Dictionary) -> void:
	var delta: Dictionary = envelope.payload
	if delta.has("entered"): # delta de interior
		_apply_interior_delta(delta)
		return
	var upserted := {}
	for visual in delta.get("npcUpserts", []):
		var id: int = id_of(visual.id)
		var cell := cell_of(visual.location)
		var action: int = NO_ACTION if visual.get("currentAction") == null else int(visual.currentAction)
		var previous = npcs.get(id)
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
	# `moved` é a projeção de compatibilidade (só posição): vale para quem não veio inteiro em npcUpserts
	for entry in delta.get("moved", []):
		var id: int = id_of(entry.npcId)
		var known = npcs.get(id)
		if upserted.has(id) or known == null:
			continue
		var from_cell: Vector2i = known.cell
		known.cell = cell_of(entry.location)
		if known.cell != from_cell:
			npc_changed.emit(id, from_cell, known.cell, int(known.action))
	for removed in delta.get("npcRemoved", []):
		var id: int = id_of(removed)
		if npcs.erase(id):
			npc_removed.emit(id)
	var changed_buildings: Array = []
	for upsert in delta.get("buildingUpserts", []):
		var id: int = id_of(upsert.id)
		var known = buildings.get(id)
		if kind == "City" and upsert.get("cityId") is Dictionary and String(id_of(upsert.cityId)) != String(city.get("id", "")):
			continue
		buildings[id] = _building(id, upsert) # AD-170: o delta traz a planta completa (andares, planta, valor, hospedagem)
		if buildings[id].footprint.is_empty() and known != null: # servidor antigo, só posição: vale o que o snapshot trouxe
			buildings[id].footprint = known.footprint
			buildings[id].floors = known.floors
			buildings[id].value = known.value
			buildings[id].lodging = known.lodging
		if buildings[id].footprint.is_empty():
			pending_footprints[id] = true # sem planta: canteiro até o próximo retrato (nenhuma leitura extra)
		else:
			pending_footprints.erase(id)
		changed_buildings.append(id)
	if not changed_buildings.is_empty():
		buildings_changed.emit(changed_buildings)
	for removed in delta.get("buildingRemoved", []):
		var id: int = id_of(removed)
		if buildings.erase(id):
			pending_footprints.erase(id)
			building_removed.emit(id)
	var processes_touched := false
	var sent := {}
	for visual in delta.get("processUpserts", []):
		var process := _process(visual)
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
	_apply_observable_delta(delta)
	if kind == "City" and not delta.get("indicators", []).is_empty() and city.has("indicators"):
		for update in delta.indicators:
			city.indicators[String(update.key)] = float(update.value)
		indicators_changed.emit()
	if not delta.get("events", []).is_empty():
		events_arrived.emit(delta.events)
	if delta.get("cityUpserts", []).size() > 0 or delta.get("cityRemoved", []).size() > 0:
		for upsert in delta.cityUpserts:
			var id: String = id_of(upsert.id)
			var known_density: Dictionary = cities[id].get("density", {}) if cities.has(id) else {}
			cities[id] = {
				"id": id, "name": upsert.name, "location": cell_of(upsert.location),
				"population": int(upsert.population), "bounds": rect_of(upsert.bounds),
				"density": Parse.density(upsert.get("density")) if upsert.get("density") is Dictionary else known_density,
			}
		for removed in delta.cityRemoved:
			cities.erase(id_of(removed))
		cities_changed.emit()


## Upserts/removidos do observável: prédio (estado), lavoura, animais, bandos, espólio, objetos e clima (nulo = não mudou).
func _apply_observable_delta(delta: Dictionary) -> void:
	var states: Array = []
	for raw in _list(delta, "buildingStateUpserts"):
		var parsed := Parse.building_state(raw)
		building_states[parsed.building_id] = parsed
		states.append(parsed.building_id)
	for removed in _list(delta, "buildingStateRemoved"):
		if building_states.erase(int(id_of(removed))):
			states.append(int(id_of(removed)))
	if not states.is_empty():
		building_states_changed.emit(states)
	if _apply_bag(crops, _list(delta, "cropUpserts"), _list(delta, "cropRemoved"), Parse.crop):
		crops_changed.emit()
	var fauna_touched := _apply_bag(animals, _list(delta, "animalUpserts"), _list(delta, "animalRemoved"), Parse.animal)
	for raw in _list(delta, "faunaGroupUpserts"):
		var group := Parse.fauna_group(raw)
		fauna_groups[group.key] = group
		fauna_touched = true
	for removed in _list(delta, "faunaGroupRemoved"):
		fauna_touched = fauna_groups.erase(String(removed)) or fauna_touched
	if fauna_touched:
		fauna_changed.emit()
	var items_touched := _apply_bag(leavings, _list(delta, "leavingUpserts"), _list(delta, "leavingRemoved"), Parse.leaving)
	items_touched = _apply_bag(objects, _list(delta, "objectUpserts"), _list(delta, "objectRemoved"), Parse.object) or items_touched
	if items_touched:
		items_changed.emit()
	if delta.get("climate") is Dictionary:
		climate = Parse.climate(delta.climate)
		climate_changed.emit()


static func _apply_bag(bag: Dictionary, upserts: Array, removed: Array, parse: Callable) -> bool:
	for raw in upserts:
		bag[int(raw.id)] = parse.call(raw)
	var touched := not upserts.is_empty()
	for id in removed:
		touched = bag.erase(int(id_of(id))) or touched
	return touched


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
