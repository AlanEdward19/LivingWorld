extends RefCounted
## Estado local do cliente: espelho do que o servidor projeta, reconstruível por snapshot + replay.
## Nenhuma regra de simulação aqui; só aplicar snapshot/deltas e avisar a cena.

signal reloaded(kind: String)
signal npc_changed(id: int, from_cell: Vector2i, to_cell: Vector2i, action: int)
signal npc_removed(id: int)
signal cities_changed()
signal interior_changed()

const NO_ACTION := -1

var kind := ""
var map_size := Vector2i.ZERO
var rivers: Dictionary = {} ## Vector2i -> true
var terrain: Dictionary = {} ## Vector2i -> int (id de terreno), vem do mundo e persiste entre escopos
var names: Dictionary = {} ## npc id -> nome (vem de GET /npcs)
var cities: Dictionary = {} ## id (String) -> {id,name,location,population,bounds:Rect2i}
var npcs: Dictionary = {} ## id (int) -> {id,cell,action,city}
var buildings: Dictionary = {} ## id (int) -> {id,type,cell,orientation,floors,footprint:Array,value:int,lodging:{units,occupied,rentPerMonth,nightlyRate}|null}
var city: Dictionary = {} ## CitySnapshot do escopo cidade: {id,name,location,bounds,indicators}
var interior: Dictionary = {} ## {building,city,floors,layout:Array[{level,cells}],occupants:Array}


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
	var payload = envelope.get("payload")
	if payload == null:
		npcs.clear()
		buildings.clear()
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
	cities.clear()
	for marker in payload.cities:
		var id: String = id_of(marker.id)
		cities[id] = {
			"id": id, "name": marker.name, "location": cell_of(marker.location),
			"population": int(marker.population), "bounds": rect_of(marker.bounds),
		}
	npcs.clear()
	buildings.clear()
	var actions := {}
	for visual in payload.get("livingState", {}).get("npcs", []):
		actions[id_of(visual.id)] = visual.get("currentAction")
	for marker in payload.externalNpcs:
		var npc_id: int = id_of(marker.id)
		npcs[npc_id] = _npc(npc_id, cell_of(marker.location), actions.get(npc_id), "", look_of(marker.get("appearance")))
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
		npcs[id] = _npc(id, cell_of(marker.location), marker.get("currentAction"), city.id, look_of(marker.get("appearance")))
	buildings.clear()
	for marker in payload.buildings:
		var id: int = id_of(marker.id)
		buildings[id] = _building(id, marker)


func _load_interior(payload: Dictionary) -> void:
	var layout: Dictionary = payload.layout
	interior = {
		"building": id_of(payload.id), "city": id_of(payload.city), "floors": int(layout.floors),
		"layout": layout.floorLayouts, "occupants": payload.occupants,
	}


func _npc(id: int, cell: Vector2i, action: Variant, city_id: String, look: Dictionary = {}) -> Dictionary:
	return {"id": id, "cell": cell, "action": NO_ACTION if action == null else int(action), "city": city_id, "look": look}


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
	for visual in delta.get("npcUpserts", []):
		var id: int = id_of(visual.id)
		var cell := cell_of(visual.location)
		var action: int = NO_ACTION if visual.get("currentAction") == null else int(visual.currentAction)
		var previous = npcs.get(id)
		var from_cell: Vector2i = previous.cell if previous != null else cell
		npcs[id] = _npc(id, cell, action, previous.city if previous != null else "", previous.look if previous != null else {})
		npc_changed.emit(id, from_cell, cell, action)
	for removed in delta.get("npcRemoved", []):
		var id: int = id_of(removed)
		if npcs.erase(id):
			npc_removed.emit(id)
	for upsert in delta.get("buildingUpserts", []):
		var id: int = id_of(upsert.id)
		var known = buildings.get(id)
		buildings[id] = _building(id, {
			"buildingTypeId": upsert.buildingTypeId, "location": upsert.location, "orientation": upsert.get("orientation", 0),
			"floors": known.floors if known != null else 1, "groundFootprint": null,
		})
		if known != null: # o delta só traz posição: o resto do que o snapshot trouxe continua valendo
			buildings[id].footprint = known.footprint
			buildings[id].value = known.value
			buildings[id].lodging = known.lodging
	for removed in delta.get("buildingRemoved", []):
		buildings.erase(id_of(removed))
	if delta.get("cityUpserts", []).size() > 0 or delta.get("cityRemoved", []).size() > 0:
		for upsert in delta.cityUpserts:
			var id: String = id_of(upsert.id)
			cities[id] = {
				"id": id, "name": upsert.name, "location": cell_of(upsert.location),
				"population": int(upsert.population), "bounds": rect_of(upsert.bounds),
			}
		for removed in delta.cityRemoved:
			cities.erase(id_of(removed))
		cities_changed.emit()


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
