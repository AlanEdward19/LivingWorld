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
const CROP := "crop"
const ANIMAL := "animal"
const FAUNA := "fauna" ## bando agregado do mapa-múndi (id = "espécie:região")
const LEAVING := "leaving"
const ITEM := "item" ## peça inventada (objeto da projeção, id numérico); OBJECT é a mobília do interior (id do catálogo)
const KINDS := [PERSON, BUILDING, CITY, PROCESS, OBJECT, WORKPLACE, HOUSEHOLD, EVENT, CROP, ANIMAL, FAUNA, LEAVING, ITEM]
## Tipo local -> tipo da rota `GET /entities/{kind}/{id}` (AD-170); ausente = sem rota (evento, processo, mobília, bando).
const SERVER_KINDS := {PERSON: "npc", BUILDING: "building", CITY: "city", CROP: "crop", ANIMAL: "animal", LEAVING: "leaving",
	ITEM: "object", WORKPLACE: "workplace", HOUSEHOLD: "household"}
const LOCAL_KINDS := {"npc": PERSON, "pessoa": PERSON, "building": BUILDING, "predio": BUILDING, "city": CITY, "cidade": CITY,
	"crop": CROP, "lavoura": CROP, "animal": ANIMAL, "object": ITEM, "objeto": ITEM, "leaving": LEAVING, "espolio": LEAVING,
	"household": HOUSEHOLD, "casa": HOUSEHOLD, "workplace": WORKPLACE, "oficina": WORKPLACE}
const STAGE_NAMES := {"unsown": "sem semear", "planted": "plantado", "growing": "crescendo", "mature": "maduro",
	"harvested": "colhido", "fallow": "pousio"}
const SPECIES_NAMES := {"rabbit": "Coelho", "wolf": "Lobo", "deer": "Cervo", "fox": "Raposa", "sheep": "Ovelha", "cow": "Vaca",
	"pig": "Porco", "chicken": "Galinha", "boar": "Javali", "bear": "Urso", "horse": "Cavalo", "dog": "Cão", "goat": "Cabra"}

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
var events: Dictionary = {} ## id do evento -> Parse.event ({id, label, kind, tick, cell, has_cell, involved, record_only})
var event_order: Array = [] ## ids na ordem de chegada (linha do tempo), no máximo MAX_EVENTS
var entity_names: Dictionary = {} ## "kind:id" -> nome que o servidor deu (links e fichas de /entities)
const MAX_EVENTS := 200
var household_buildings: Dictionary = {} ## household id -> prédio da casa (ficha da pessoa, AD-170)


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
			return String(entity_names.get(key(entity), "Local de trabalho nº %d" % int(id)))
		HOUSEHOLD:
			return String(entity_names.get(key(entity), "Casa da família nº %d" % int(id)))
		EVENT:
			return event_title(events.get(int(id), {}))
		CROP:
			var crop = store.crops.get(int(id)) if store != null else null
			if crop == null:
				return String(entity_names.get(key(entity), "Lavoura nº %d" % int(id)))
			return "Lavoura de %s" % (String(crop.resource_name) if String(crop.resource_name) != "" else "recurso %d" % int(crop.resource_id))
		ANIMAL:
			var animal = store.animals.get(int(id)) if store != null else null
			return species_name(String(animal.species)) if animal != null else String(entity_names.get(key(entity), "Animal"))
		FAUNA:
			var group = store.fauna_groups.get(String(id)) if store != null else null
			return "Bando de %s (%d)" % [species_name(String(group.species)), int(group.count)] if group != null else "Bando"
		LEAVING:
			var leaving = store.leavings.get(int(id)) if store != null else null
			return "Espólio de %s" % person_name(int(leaving.dead_id)) if leaving != null and int(leaving.dead_id) >= 0 else "Espólio"
		ITEM:
			return String(entity_names.get(key(entity), "Objeto nº %d" % int(id)))
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
	for child in detail.get("childIds", []) if detail.get("childIds") is Array else []: # AD-170: todos os filhos
		children.get_or_add(id, {})[int(child.value) if child is Dictionary else int(child)] = true
	var employment = detail.get("employment")
	if employment is Dictionary and employment.get("id") is Dictionary and employment.get("name") is String:
		entity_names[key(ref(WORKPLACE, int(employment.id.value)))] = String(employment.name)
	var home = detail.get("householdBuildingId")
	if home is Dictionary and household is Dictionary:
		household_buildings[int(household.value)] = int(home.value)


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
static func species_name(species: String) -> String:
	return String(SPECIES_NAMES.get(species.to_lower(), species.capitalize() if species != "" else "Animal"))


## Aprende os nomes que a rota de inspeção dá (a própria entidade e cada link): casa e trabalho passam a ter nome real.
func learn_entity(body: Dictionary) -> void:
	var own_kind := String(LOCAL_KINDS.get(String(body.get("kind", "")), ""))
	if own_kind != "" and String(body.get("name", "")) != "":
		_learn_name(own_kind, body.get("id"), String(body.name))
	for link in body.get("links", []) if body.get("links") is Array else []:
		var kind := String(LOCAL_KINDS.get(String(link.get("kind", "")), ""))
		if kind != "" and link.get("name") is String and String(link.name) != "":
			_learn_name(kind, link.get("id"), String(link.name))


func _learn_name(kind: String, id: Variant, name: String) -> void:
	entity_names[key(ref(kind, id_for(kind, id)))] = name
	if kind == PERSON and store != null and not store.names.has(int(id_for(kind, id))):
		store.names[int(id_for(kind, id))] = name


## Id no tipo local: cidade é Guid (texto), o resto é número.
static func id_for(kind: String, raw: Variant) -> Variant:
	return String(raw) if kind == CITY else int(str(raw))


## Guarda um evento do delta na linha do tempo (o mais antigo sai quando passa de MAX_EVENTS).
func remember_event(event: Dictionary) -> void:
	var id := int(event.id)
	if not events.has(id):
		event_order.append(id)
	events[id] = event
	while event_order.size() > MAX_EVENTS:
		events.erase(event_order.pop_front())


const CASE_WORDS := {"authorId": "autor(a)", "victimId": "vítima"}


## Título do evento para a tela. Violência sexual, doméstica e contra criança (`record_only`) mostram SÓ o registro
## estruturado, nunca o rótulo narrado (decisão 5 do ADR-0047).
func event_title(event: Dictionary) -> String:
	if event.is_empty():
		return "Evento"
	if bool(event.get("record_only", false)):
		var involved: Dictionary = event.get("involved", {})
		var parts: Array[String] = ["Registro de crime"]
		if involved.has("caseId"):
			parts.append("caso nº %d" % int(involved.caseId))
		if involved.has("outcome"):
			parts.append(String(involved.outcome))
		return " · ".join(parts)
	return String(event.get("label", "Evento")) if String(event.get("label", "")) != "" else "Evento"


## Envolvidos do evento como links: [rótulo, ref]. Para registro só de crime, também autor, vítima e caso (sem descrição).
func event_links(event: Dictionary) -> Array:
	var involved: Dictionary = event.get("involved", {})
	var links: Array = []
	for spec in [["authorId", "Autor(a)"], ["victimId", "Vítima"], ["subjectId", "Pessoa"], ["otherId", "Outra pessoa"]]:
		if involved.has(spec[0]):
			links.append([spec[1], ref(PERSON, int(involved[spec[0]]))])
	for related in involved.get("relatedIds", []):
		links.append(["Envolvido(a)", ref(PERSON, int(related))])
	if involved.has("householdId"):
		links.append(["Casa", ref(HOUSEHOLD, int(involved.householdId))])
	if involved.has("buildingId"):
		links.append(["Prédio", ref(BUILDING, int(involved.buildingId))])
	if involved.has("workplaceId"):
		links.append(["Local de trabalho", ref(WORKPLACE, int(involved.workplaceId))])
	if involved.has("leavingId"):
		links.append(["Espólio", ref(LEAVING, int(involved.leavingId))])
	if involved.has("cityId"):
		links.append(["Cidade", ref(CITY, String(involved.cityId))])
	var seen := {}
	var unique: Array = []
	for link in links:
		var k := "%s|%s" % [link[0], key(link[1])]
		if not seen.has(k):
			seen[k] = true
			unique.append(link)
	return unique


## Pessoas envolvidas num evento (para o diretor seguir), na ordem: autor, vítima, sujeito, outro, relacionados.
func event_people(event: Dictionary) -> Array:
	var out: Array = []
	for link in event_links(event):
		if String(link[1].kind) == PERSON and not out.has(int(link[1].id)):
			out.append(int(link[1].id))
	return out


func model_of(id: int) -> Dictionary:
	var npc = store.npcs.get(id) if store != null else null
	if npc != null and not npc.get("obs", {}).is_empty():
		return {"profession": int(npc.obs.profession_id), "age": float(npc.obs.age)}
	var detail = details.get(id)
	if not detail is Dictionary:
		return {}
	var profession = detail.get("profession")
	return {"profession": int(profession.id) if profession is Dictionary else -1, "age": float(detail.get("ageYears", 0))}
