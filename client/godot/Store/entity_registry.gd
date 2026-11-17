extends RefCounted
## Registro local de entidades observáveis (ADR-0047, etapa 1): toda coisa clicável tem uma referência {kind, id} e um
## nome legível. Só junta o que já chegou (store, catálogo do período, fichas lidas); nunca pergunta ao servidor.
## Parentesco e moradia que o contrato não lista (filhos, moradores) são aprendidos das fichas já abertas: "conhecidos".

const PERSON := "person"
const BUILDING := "building"
const CITY := "city"
const PROCESS := "process"
const OBJECT := "object"
const WORKPLACE := "workplace"
const HOUSEHOLD := "household"
const EVENT := "event"
const KINDS := [PERSON, BUILDING, CITY, PROCESS, OBJECT, WORKPLACE, HOUSEHOLD, EVENT]

const PROCESS_NAMES := {
	"rest": "Descanso", "food": "Refeição", "cook-food": "Cozinhar", "collect-water": "Buscar água",
	"deliver-water": "Entregar água", "carry-water": "Carregar água", "plant-crop": "Plantio", "water-crop": "Rega",
	"harvest-crop": "Colheita", "construction": "Obra", "process": "Trabalho",
}
const KIND_NAMES := {"rest": "Descanso", "food": "Refeição", "cook": "Cozinha", "water": "Água", "crop": "Lavoura",
	"construction": "Obra", "extraordinary-construct": "Construto extraordinário", "process": "Trabalho"}

var store
var professions: Dictionary = {} ## id -> nome (catálogo do período)
var skills: Dictionary = {}
var resources: Dictionary = {}
var building_types: Dictionary = {} ## id -> nome (descritores BuildingType do período)
var location_types: Dictionary = {}
var cultures: Dictionary = {}
var details: Dictionary = {} ## npc id -> última ficha lida (GET /npcs/{id})
var children: Dictionary = {} ## npc id -> {filho: true} aprendido das fichas
var households: Dictionary = {} ## household id -> {npc: true}
var workers: Dictionary = {} ## workplace id -> {npc: true}
var objects: Dictionary = {} ## "id do catálogo" -> descritor do objeto do interior aberto
var events: Dictionary = {} ## id local -> {label, kind, tick, cell}


static func ref(kind: String, id: Variant) -> Dictionary:
	return {"kind": kind, "id": id}


static func key(entity: Dictionary) -> String:
	return "%s:%s" % [entity.get("kind", ""), str(entity.get("id", ""))]


func _init(p_store = null) -> void:
	store = p_store


## Nomes do catálogo do período (`GET /periods/{id}/catalog`); campos ausentes ficam vazios e caem no número.
func load_catalog(body: Dictionary) -> void:
	professions = {}
	for profession_id in body.get("professionIds", []):
		professions[int(profession_id)] = String(body.get("professionNames", {}).get(str(int(profession_id)), "Profissão %d" % int(profession_id)))
	skills = {}
	for skill_id in body.get("skillIds", []):
		skills[int(skill_id)] = String(body.get("skillNames", {}).get(str(int(skill_id)), "Habilidade %d" % int(skill_id)))
	var descriptors: Dictionary = body.get("descriptors", {}) if body.get("descriptors") is Dictionary else {}
	resources = _names(descriptors.get("resource", []))
	building_types = _names(descriptors.get("buildingType", []))
	location_types = _names(descriptors.get("locationType", []))
	cultures = _names(descriptors.get("culture", []))


static func _names(list: Variant) -> Dictionary:
	var out := {}
	for descriptor in list if list is Array else []:
		out[int(descriptor.id)] = String(descriptor.get("name", ""))
	return out


func person_name(id: int) -> String:
	if store != null and store.names.has(id):
		return String(store.names[id])
	var detail = details.get(id)
	if detail is Dictionary and String(detail.get("name", "")) != "":
		return String(detail.name)
	return "Pessoa nº %d" % id


func profession_name(id: int) -> String:
	return String(professions.get(id, "ofício nº %d" % id)) if id >= 0 else "sem ofício"


func building_type_name(type_id: int) -> String:
	if building_types.has(type_id) and String(building_types[type_id]) != "":
		return String(building_types[type_id])
	return "Casa" if type_id == -1 else "Prédio tipo %d" % type_id


func process_name(process: Dictionary) -> String:
	var by_descriptor := String(PROCESS_NAMES.get(String(process.get("descriptor", "")), ""))
	return by_descriptor if by_descriptor != "" else String(KIND_NAMES.get(String(process.get("kind", "")), String(process.get("kind", "processo"))))


## Título curto de qualquer referência (botão de link, histórico, dica).
func title_of(entity: Dictionary) -> String:
	var id = entity.get("id")
	match String(entity.get("kind", "")):
		PERSON:
			return person_name(int(id))
		BUILDING:
			var building = store.buildings.get(int(id)) if store != null else null
			return "%s nº %d" % [building_type_name(int(building.type)) if building != null else "Edifício", int(id)]
		CITY:
			if store != null and String(store.city.get("id", "")) == String(id) and String(store.city.get("name", "")) != "":
				return String(store.city.name)
			var city = store.cities.get(String(id)) if store != null else null
			return String(city.name) if city != null and String(city.name) != "" else "Cidade"
		PROCESS:
			var process = store.processes.get(int(id)) if store != null else null
			return process_name(process) if process != null else "Processo encerrado"
		OBJECT:
			var descriptor: Dictionary = objects.get(String(id), {})
			return String(descriptor.get("name", String(id).capitalize()))
		WORKPLACE:
			return "Local de trabalho nº %d" % int(id)
		HOUSEHOLD:
			return "Casa da família nº %d" % int(id)
		EVENT:
			return String(events.get(id, {}).get("label", "Evento"))
	return str(id)


## Guarda a ficha e aprende os vínculos que ela revela (pais → filhos, casa → moradores, emprego → trabalhadores).
func learn_person(id: int, detail: Dictionary) -> void:
	details[id] = detail
	for parent_key in ["motherId", "fatherId"]:
		var parent = detail.get(parent_key)
		if parent is Dictionary:
			children.get_or_add(int(parent.value), {})[id] = true
	var household = detail.get("household")
	if household is Dictionary:
		households.get_or_add(int(household.value), {})[id] = true
	var employer = detail.get("employer")
	if employer is Dictionary:
		workers.get_or_add(int(employer.value), {})[id] = true


static func sorted_ids(bag: Dictionary) -> Array:
	var ids: Array = bag.keys()
	ids.sort()
	return ids


func known_children(id: int) -> Array:
	return sorted_ids(children.get(id, {}))


func known_household(household_id: int) -> Array:
	return sorted_ids(households.get(household_id, {}))


func known_workers(workplace_id: int) -> Array:
	return sorted_ids(workers.get(workplace_id, {}))


## Profissão e idade conhecidas (só de quem já teve a ficha lida): o corpo do NPC usa no lugar do modelo base.
func model_of(id: int) -> Dictionary:
	var detail = details.get(id)
	if not detail is Dictionary:
		return {}
	var profession = detail.get("profession")
	return {"profession": int(profession.id) if profession is Dictionary else -1, "age": float(detail.get("ageYears", 0))}
