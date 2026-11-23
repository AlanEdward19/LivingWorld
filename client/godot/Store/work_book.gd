extends RefCounted
## Obras em andamento do escopo Cidade (AD-193): `livingState.works` no retrato e `workUpserts`/`workRemoved` no delta. Só guarda o
## que o motor diz (fração, turma, quem está no lote, o que falta, por que parou); nenhuma regra de obra aqui.
const WorldTime := preload("res://Contract/world_time.gd")

const HALT_TEXT := {
	"tempo": "chuva forte, neve ou chão congelado no lote: hoje ninguém trabalha nem conta hora",
	"parada": "sem nenhuma hora de trabalho há dias: falta gente na obra",
	"material": "a cidade não tem no estoque o material do próximo passo da obra",
}
const PAYER_TEXT := {"casa": "a família paga a turma", "fundador": "o fundador paga a turma com o que guardou", "cidade": "o tesouro da cidade paga a turma"}
const STAGES := 10 ## a obra desenhada muda de forma a cada décimo (o motor manda a fração a cada hora trabalhada)


## WorkSiteVisual do servidor no formato do cliente; ausente/nulo = vazio.
static func parse(raw: Dictionary) -> Dictionary:
	var materials: Array = []
	for entry in raw.get("materials", []) if raw.get("materials") is Array else []:
		if entry is Dictionary:
			materials.append({"resource": int(entry.get("resourceId", -1)), "name": String(entry.get("name", "")) if entry.get("name") != null else "",
				"needed": int(entry.get("needed", 0)), "placed": int(entry.get("placed", 0)),
				"on_site": int(entry.get("onSite", 0)) if entry.get("onSite") != null else -1}) # AD-194: -1 = o motor não conta o lote
	var location = raw.get("location")
	return {
		"id": int(raw.get("id", -1)), "kind": String(raw.get("kind", "")), "city": String(raw.get("cityId", "")),
		"cell": Vector2i(int(location.x), int(location.y)) if location is Dictionary else Vector2i.ZERO,
		"progress": clampf(float(raw.get("progress", 0.0)), 0.0, 1.0),
		"building_id": int(raw.buildingId) if raw.get("buildingId") != null else -1,
		"type_id": int(raw.buildingTypeId) if raw.get("buildingTypeId") != null else -1,
		"type_name": String(raw.typeName) if raw.get("typeName") != null else "",
		"stalled": bool(raw.get("stalled", false)), "halt": String(raw.halt) if raw.get("halt") != null else "",
		"payer": String(raw.get("payer", "")),
		"owner_id": int(raw.ownerId) if raw.get("ownerId") != null else -1,
		"household_id": int(raw.householdId) if raw.get("householdId") != null else -1,
		"crew": _ids(raw.get("crew")), "helpers": _ids(raw.get("helpers")), "on_site": _ids(raw.get("onSite")),
		"hours_total": int(raw.get("hoursTotal", 0)), "hours_left": int(raw.get("hoursLeft", 0)),
		"hours_crew": int(raw.get("hoursByCrew", 0)), "hours_helpers": int(raw.get("hoursByHelpers", 0)),
		"haul_left": int(raw.get("haulHoursLeft", 0)), "paid": int(raw.get("paid", 0)), "spent": int(raw.get("spent", 0)),
		"offer_pm": int(raw.get("offerPm", 1000)), "started": WorldTime.hours_of(raw, "startedAtMs"), "last_worked": WorldTime.hours_of(raw, "lastWorkedAtMs"),
		"materials": materials,
	}


static func _ids(list: Variant) -> Array:
	var out: Array = []
	for value in list if list is Array else []:
		out.append(int(value.value) if value is Dictionary and value.has("value") else int(value))
	return out


## Retrato: troca todas as obras do escopo.
static func load_snapshot(store, state: Dictionary) -> void:
	store.works.clear()
	for raw in state.get("works", []) if state.get("works") is Array else []:
		if raw is Dictionary:
			var work := parse(raw)
			store.works[work.id] = work
			# o retrato da cidade (`payload.buildings`) não traz `work` no prédio: o canteiro é o que a obra diz
			var site = store.buildings.get(int(work.building_id))
			if site != null and site.get("work", {}).is_empty():
				site.work = {"id": work.id, "kind": work.kind, "progress": work.progress, "stalled": work.stalled, "building_id": work.building_id,
					"halt": work.halt, "materials": [], "haul_left": 0}


## Delta: só a obra que mudou vem; a que acabou ou foi abandonada sai. Avisa com os ids tocados (o renderizador redesenha só esses).
static func apply_delta(store, delta: Dictionary) -> void:
	var touched: Array = []
	for raw in delta.get("workUpserts", []) if delta.get("workUpserts") is Array else []:
		if raw is Dictionary:
			var work := parse(raw)
			store.works[work.id] = work
			touched.append(work.id)
	for removed in delta.get("workRemoved", []) if delta.get("workRemoved") is Array else []:
		var id := int(removed.value) if removed is Dictionary and removed.has("value") else int(removed)
		if store.works.erase(id):
			touched.append(id)
	if not touched.is_empty():
		store.works_changed.emit(touched)


## A obra do canteiro (prédio com `work`), {} se o prédio está pronto ou a obra não está no escopo.
static func of_building(store, building_id: int) -> Dictionary:
	var building = store.buildings.get(building_id)
	if building != null and building.get("work", {}).is_empty():
		return {} # o motor diz que o prédio está pronto (a obra que ainda está na lista sai no mesmo delta)
	for work in store.works.values():
		if int(work.building_id) == building_id:
			return work
	return store.works.get(int(building.work.id), building.work) if building != null else {}


## Décimo da obra que o desenho mostra (0..STAGES).
static func stage(work: Dictionary) -> int:
	return clampi(int(floor(float(work.get("progress", 0.0)) * STAGES)), 0, STAGES)


## Material no lote e ainda não posto, fração 0..1 do total. AD-194: casa e oficina contam o lote (`onSite`); a obra da fila conta o
## posto e o carregamento (o material carregado e não posto, quando o carregamento acabou e o estoque tem o passo).
static func pile_fraction(work: Dictionary) -> float:
	var needed := 0
	var on_site := 0
	var placed := 0
	var counted := false
	for entry in work.get("materials", []):
		needed += int(entry.needed)
		placed += int(entry.placed)
		if int(entry.get("on_site", -1)) >= 0:
			counted = true
			on_site += int(entry.on_site)
	if needed <= 0:
		return 0.0
	if counted:
		return clampf(float(on_site) / float(needed), 0.0, 1.0)
	if int(work.get("haul_left", 0)) > 0 or String(work.get("halt", "")) == "material":
		return 0.0
	return clampf(float(needed - placed) / float(needed), 0.0, 1.0)


## O que a obra levanta, em palavras: casa, oficina ou o tipo de prédio da fila da cidade.
static func what(work: Dictionary) -> String:
	match String(work.get("kind", "")):
		"casa":
			return "casa"
		"oficina":
			return "oficina"
	return String(work.type_name) if String(work.get("type_name", "")) != "" else "prédio da cidade"
