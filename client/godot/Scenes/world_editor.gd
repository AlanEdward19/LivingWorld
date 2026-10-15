extends RefCounted
## Edição do cenário de um novo mundo, sempre em TEXTO (ver JsonRaw): só os membros do topo que o usuário
## personaliza são trocados; o resto do período segue byte a byte. Nada aqui decide simulação: o servidor
## valida o cenário final em /worlds/preview e /worlds/create e devolve o erro.

const JsonRaw := preload("res://Contract/json_raw.gd")

const MIN_SIDE := 8
const MAX_SIDE := 64
const MAX_POPULATION := 1000


## Valores atuais do rascunho para preencher os controles (leitura apenas; nada é reescrito a partir daqui).
static func read(text: String) -> Dictionary:
	var settlements: Array = []
	var parsed = _parse(text, "Settlements")
	for item in parsed if parsed is Array else []:
		settlements.append({"name": String(item.get("Name", "")), "x": int(item.get("X", 0)), "y": int(item.get("Y", 0))})
	return {
		"width": int(JsonRaw.member(text, "Width")), "height": int(JsonRaw.member(text, "Height")),
		"population": int(JsonRaw.member(text, "InitialPopulation")),
		"conception": float(JsonRaw.member(text, "AnnualConceptionChance")),
		"longevity": int(JsonRaw.member(text, "MaxLongevityYears")),
		"settlements": settlements,
	}


## Membro do topo lido como valor Godot (só leitura); null se ausente.
static func _parse(text: String, key: String) -> Variant:
	var raw := JsonRaw.member(text, key)
	return null if raw == "" else JSON.parse_string(raw)


static func with_seed(text: String, seed_value: int) -> String:
	return JsonRaw.replace_member(text, "Seed", str(seed_value))


## Mapa novo tamanho: assentamentos que ficariam fora voltam para dentro (o servidor recusaria o cenário).
static func with_size(text: String, width: int, height: int) -> String:
	width = clampi(width, MIN_SIDE, MAX_SIDE)
	height = clampi(height, MIN_SIDE, MAX_SIDE)
	var out := JsonRaw.replace_member(text, "Width", str(width))
	out = JsonRaw.replace_member(out, "Height", str(height))
	var settlements: Array = read(out).settlements
	for s in settlements:
		s.x = clampi(s.x, 0, width - 1)
		s.y = clampi(s.y, 0, height - 1)
	return with_settlements(out, settlements)


static func with_population(text: String, count: int) -> String:
	return JsonRaw.replace_member(text, "InitialPopulation", str(clampi(count, 1, MAX_POPULATION)))


static func with_conception(text: String, chance: float) -> String:
	return JsonRaw.replace_member(text, "AnnualConceptionChance", "%.3f" % clampf(chance, 0.0, 1.0))


static func with_longevity(text: String, years: int) -> String:
	return JsonRaw.replace_member(text, "MaxLongevityYears", str(clampi(years, 30, 150)))


## Troca a lista de assentamentos; o primeiro é também a aldeia inicial (VillageX/VillageY).
static func with_settlements(text: String, settlements: Array) -> String:
	var items: Array[String] = []
	for s in settlements:
		items.append('{"Name":%s,"X":%d,"Y":%d}' % [JSON.stringify(String(s.name)), int(s.x), int(s.y)])
	var out := JsonRaw.replace_member(text, "Settlements", "[" + ",".join(items) + "]")
	if not settlements.is_empty():
		out = JsonRaw.replace_member(out, "VillageX", str(int(settlements[0].x)))
		out = JsonRaw.replace_member(out, "VillageY", str(int(settlements[0].y)))
	return out


# --- famílias autoradas (ADR-0024) ------------------------------------------------

const TRAITS: Array[String] = [
	"Extroversion", "Agreeableness", "Conscientiousness", "EmotionalStability", "Openness",
	"Ambition", "Loyalty", "Altruism", "Impulsivity", "RiskAversion",
]


const GENES: Array[String] = ["SkinTone", "HairTone", "HairCurl", "Build"]
## Herdabilidade do bloco `Appearance` que o editor acrescenta quando alguém tem aparência própria (a dos presets).
const APPEARANCE_BLOCK := '{"Heritability":0.6}'


## `families`: [{settlement: String, members: [{name, sex: "Female"|"Male", age, profession, personality: Dictionary|null,
## spouse: int, mother: int, father: int, appearance: Dictionary|null, skills: Dictionary id->nível}]}]; -1 = sem vínculo.
## Vazio remove o bloco do efeito (lista vazia). Aparência própria exige o bloco `Appearance` no período: é acrescentado se faltar.
static func with_households(text: String, families: Array) -> String:
	var family_items: Array[String] = []
	var wants_appearance := false
	for family in families:
		var members: Array[String] = []
		for member in family.members:
			var fields: Array[String] = [
				'"Name":%s' % JSON.stringify(String(member.name)), '"Sex":"%s"' % member.sex,
				'"AgeYears":%d' % int(member.age), '"ProfessionId":%d' % int(member.profession),
			]
			if member.get("personality") is Dictionary:
				var traits: Array[String] = []
				for trait_name in TRAITS:
					traits.append('"%s":%d' % [trait_name, int(member.personality.get(trait_name, 50))])
				fields.append('"Personality":{%s}' % ",".join(traits))
			if member.get("appearance") is Dictionary:
				var genes: Array[String] = []
				for gene in GENES:
					genes.append('"%s":%d' % [gene, int(member.appearance.get(gene, 50))])
				fields.append('"Appearance":{%s}' % ",".join(genes))
				wants_appearance = true
			if member.get("skills") is Dictionary and not member.skills.is_empty():
				var levels: Array[String] = []
				var ids: Array = member.skills.keys()
				ids.sort()
				for skill_id in ids:
					levels.append('"%d":%d' % [int(skill_id), int(member.skills[skill_id])])
				fields.append('"Skills":{%s}' % ",".join(levels))
			for link in [["Spouse", "spouse"], ["Mother", "mother"], ["Father", "father"]]:
				if int(member.get(link[1], -1)) >= 0:
					fields.append('"%s":%d' % [link[0], int(member[link[1]])])
			members.append("{" + ",".join(fields) + "}")
		var head := '"Members":[%s]' % ",".join(members)
		if String(family.get("settlement", "")) != "":
			head = '"Settlement":%s,%s' % [JSON.stringify(String(family.settlement)), head]
		family_items.append("{" + head + "}")
	var out := JsonRaw.set_member(text, "AuthoredHouseholds", "[" + ",".join(family_items) + "]")
	if wants_appearance and JsonRaw.member(out, "Appearance") == "":
		out = JsonRaw.set_member(out, "Appearance", APPEARANCE_BLOCK)
	return out


## Corpo de `POST /authoring/households` (ADR-0033): um item de `AuthoredHouseholds` do cenário, com os inteiros como inteiros.
## `family` é o mesmo formato de `with_households`; o assentamento vazio vira "vila inicial" (a cidade mais antiga).
static func household_body(family: Dictionary) -> Dictionary:
	var members: Array = []
	for member in family.members:
		var item := {"Name": String(member.name), "Sex": String(member.sex), "AgeYears": int(member.age), "ProfessionId": int(member.profession)}
		if member.get("personality") is Dictionary:
			var traits := {}
			for trait_name in TRAITS:
				traits[trait_name] = int(member.personality.get(trait_name, 50))
			item["Personality"] = traits
		if member.get("appearance") is Dictionary:
			var genes := {}
			for gene in GENES:
				genes[gene] = int(member.appearance.get(gene, 50))
			item["Appearance"] = genes
		if member.get("skills") is Dictionary and not member.skills.is_empty():
			var levels := {}
			for skill_id in member.skills:
				levels[str(int(skill_id))] = int(member.skills[skill_id])
			item["Skills"] = levels
		for link in [["Spouse", "spouse"], ["Mother", "mother"], ["Father", "father"]]:
			if int(member.get(link[1], -1)) >= 0:
				item[link[0]] = int(member[link[1]])
		members.append(item)
	var body := {"Members": members}
	if String(family.get("settlement", "")) != "":
		body["Settlement"] = String(family.settlement)
	return body


## Corpo de `POST /authoring/workplaces`: um item de `Workplaces` do cenário (estoque e preços vazios).
static func workplace_body(location_type: int, cell: Vector2i, vacancies: int, treasury: int) -> Dictionary:
	return {"LocationTypeId": location_type, "X": cell.x, "Y": cell.y, "MaxVacancies": vacancies, "Treasury": treasury, "Stock": {}, "Prices": {}}


## Famílias já presentes no rascunho, no mesmo formato de `with_households`.
static func read_households(text: String) -> Array:
	var families: Array = []
	var parsed = _parse(text, "AuthoredHouseholds")
	for item in parsed if parsed is Array else []:
		var members: Array = []
		for person in item.get("Members", []):
			var personality = null
			if person.get("Personality") is Dictionary:
				personality = {}
				for trait_name in TRAITS:
					personality[trait_name] = int(person.Personality.get(trait_name, 50))
			var appearance = null
			if person.get("Appearance") is Dictionary:
				appearance = {}
				for gene in GENES:
					appearance[gene] = int(person.Appearance.get(gene, 50))
			var skills := {}
			if person.get("Skills") is Dictionary:
				for skill_id in person.Skills:
					skills[int(skill_id)] = int(person.Skills[skill_id])
			members.append({
				"appearance": appearance, "skills": skills,
				"name": String(person.get("Name", "")), "sex": String(person.get("Sex", "Female")),
				"age": int(person.get("AgeYears", 20)), "profession": int(person.get("ProfessionId", 0)),
				"personality": personality, "spouse": int(person.get("Spouse", -1)),
				"mother": int(person.get("Mother", -1)), "father": int(person.get("Father", -1)),
			})
		families.append({"settlement": String(item.get("Settlement", "")), "members": members})
	return families


# --- prédios (locais de trabalho do catálogo) -------------------------------------

## JSON com inteiros como inteiros (o parser do Godot devolve todo número como float).
static func emit(value: Variant) -> String:
	if value is Dictionary:
		var parts: Array[String] = []
		for key in value:
			parts.append("%s:%s" % [JSON.stringify(String(key)), emit(value[key])])
		return "{" + ",".join(parts) + "}"
	if value is Array:
		var parts: Array[String] = []
		for item in value:
			parts.append(emit(item))
		return "[" + ",".join(parts) + "]"
	if value is float and value == floorf(value) and absf(value) < 9.0e15:
		return str(int(value))
	return JSON.stringify(value)


static func read_workplaces(text: String) -> Array:
	var parsed = _parse(text, "Workplaces")
	return parsed if parsed is Array else []


## Tipos de local que o cenário já usa (LocationTypeIds e os dos locais de trabalho).
static func building_types(text: String) -> Array[int]:
	var ids := {}
	var declared = _parse(text, "LocationTypeIds")
	for id in declared if declared is Array else []:
		ids[int(id)] = true
	for workplace in read_workplaces(text):
		ids[int(workplace.get("LocationTypeId", 0))] = true
	var out: Array[int] = []
	for id in ids:
		out.append(id)
	out.sort()
	return out


## Troca os locais de trabalho; os campos não editados (estoque, preços, tesouro) seguem como vieram.
static func with_workplaces(text: String, workplaces: Array) -> String:
	return JsonRaw.replace_member(text, "Workplaces", emit(workplaces))
