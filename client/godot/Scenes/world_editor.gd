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
	var parsed = JSON.parse_string(JsonRaw.member(text, "Settlements"))
	for item in parsed if parsed is Array else []:
		settlements.append({"name": String(item.get("Name", "")), "x": int(item.get("X", 0)), "y": int(item.get("Y", 0))})
	return {
		"width": int(JsonRaw.member(text, "Width")), "height": int(JsonRaw.member(text, "Height")),
		"population": int(JsonRaw.member(text, "InitialPopulation")),
		"conception": float(JsonRaw.member(text, "AnnualConceptionChance")),
		"longevity": int(JsonRaw.member(text, "MaxLongevityYears")),
		"settlements": settlements,
	}


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
