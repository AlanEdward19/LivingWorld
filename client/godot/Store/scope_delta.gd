extends RefCounted
## Partes do delta de escopo que não são NPC nem prédio (tiradas de `world_store.gd`, que estava no limite de tamanho):
## o observável (estado de prédio, lavoura, fauna, espólio, objetos, clima) e as ruas (AD-181: `roadUpserts`/`roadRemoved`).
## Só aplica na store e emite os sinais dela; a regra de leitura é a do delta enxuto (ausente/nulo = nulo, upsert inteiro).

const Parse := preload("res://Store/observable_parse.gd")
const Roads := preload("res://Store/road_book.gd")


## Upserts/removidos do observável: prédio (estado), lavoura, animais, bandos, espólio, objetos e clima (nulo = não mudou).
static func apply_observable(store, delta: Dictionary) -> void:
	var states: Array = []
	for raw in _list(delta, "buildingStateUpserts"):
		var parsed := Parse.building_state(raw)
		store.building_states[parsed.building_id] = parsed
		states.append(parsed.building_id)
	for removed in _list(delta, "buildingStateRemoved"):
		if store.building_states.erase(int(_id(removed))):
			states.append(int(_id(removed)))
	if not states.is_empty():
		store.building_states_changed.emit(states)
	if Parse.apply_bag(store.crops, _list(delta, "cropUpserts"), _list(delta, "cropRemoved"), Parse.crop):
		store.crops_changed.emit()
	var fauna_touched := Parse.apply_bag(store.animals, _list(delta, "animalUpserts"), _list(delta, "animalRemoved"), Parse.animal)
	for raw in _list(delta, "faunaGroupUpserts"):
		var group := Parse.fauna_group(raw)
		store.fauna_groups[group.key] = group
		fauna_touched = true
	for removed in _list(delta, "faunaGroupRemoved"):
		fauna_touched = store.fauna_groups.erase(String(removed)) or fauna_touched
	if fauna_touched:
		store.fauna_changed.emit()
	var items_touched := Parse.apply_bag(store.leavings, _list(delta, "leavingUpserts"), _list(delta, "leavingRemoved"), Parse.leaving)
	items_touched = Parse.apply_bag(store.objects, _list(delta, "objectUpserts"), _list(delta, "objectRemoved"), Parse.object) or items_touched
	if items_touched:
		store.items_changed.emit()
	if delta.get("climate") is Dictionary:
		store.climate = Parse.climate(delta.climate)
		Parse.at_hour(store.climate.get("weather", {}), int(delta.get("tick", -1)))
		store.climate_changed.emit()
	elif store.climate.get("weather") is Dictionary and Parse.at_hour(store.climate.weather, int(delta.get("tick", -1))):
		store.climate_changed.emit() # AD-189: a hora nova da curva do dia (o servidor não reenvia o clima por isso)


## Ruas por delta: troca/apaga as células e guarda onde a última obra acabou (lugar do evento `StreetWorkDone`).
static func apply_roads(store, delta: Dictionary) -> void:
	var result := Roads.apply_delta(store.roads, _list(delta, "roadUpserts"), _list(delta, "roadRemoved"))
	if not result.finished.is_empty():
		store.finished_works = result.finished
	if result.changed:
		store.roads_changed.emit()


static func _list(source: Variant, key: String) -> Array:
	return source.get(key) if source is Dictionary and source.get(key) is Array else []


static func _id(value: Variant) -> Variant:
	return value.value if value is Dictionary and value.has("value") else value
