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
var buildings: Dictionary = {} ## id (int) -> {id,type,cell,orientation,floors,footprint:Array}
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
		npcs[npc_id] = _npc(npc_id, cell_of(marker.location), actions.get(npc_id), "")
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
		npcs[id] = _npc(id, cell_of(marker.location), marker.get("currentAction"), city.id)
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


func _npc(id: int, cell: Vector2i, action: Variant, city_id: String) -> Dictionary:
	return {"id": id, "cell": cell, "action": NO_ACTION if action == null else int(action), "city": city_id}


func _building(id: int, marker: Dictionary) -> Dictionary:
	var footprint: Array = []
	for entry in marker.get("groundFootprint", []) if marker.get("groundFootprint") != null else []:
		footprint.append({"cell": cell_of(entry.cell), "material": int(entry.material)})
	return {
		"id": id, "type": int(marker.buildingTypeId), "cell": cell_of(marker.location),
		"orientation": int(marker.get("orientation", 0)), "floors": int(marker.get("floors", 1)),
		"footprint": footprint,
	}


func apply_delta(envelope: Dictionary) -> void:
	var delta: Dictionary = envelope.payload
	if delta.has("upserts"): # delta de interior
		_apply_interior_delta(delta)
		return
	for visual in delta.get("npcUpserts", []):
		var id: int = id_of(visual.id)
		var cell := cell_of(visual.location)
		var action: int = NO_ACTION if visual.get("currentAction") == null else int(visual.currentAction)
		var previous = npcs.get(id)
		var from_cell: Vector2i = previous.cell if previous != null else cell
		npcs[id] = _npc(id, cell, action, previous.city if previous != null else "")
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
		if known != null:
			buildings[id].footprint = known.footprint
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


## Delta de interior: `upserts` (entrou ou mudou) e `removed` (saiu), por id de NPC.
func _apply_interior_delta(delta: Dictionary) -> void:
	var occupants: Array = interior.get("occupants", [])
	var by_npc := {}
	for occupant in occupants:
		by_npc[int(id_of(occupant.npc))] = occupant
	for removed in delta.get("removed", []):
		by_npc.erase(int(id_of(removed)))
	for upsert in delta.get("upserts", []):
		by_npc[int(id_of(upsert.npc))] = upsert
	interior["occupants"] = by_npc.values()
	interior_changed.emit()
