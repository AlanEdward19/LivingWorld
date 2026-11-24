extends RefCounted
## Chão da cidade que o motor expõe (AD-173, lido pelo cliente na AD-177): a camada `Roads` (uma célula de rua por
## entrada: cidade, superfície, quantas pessoas usam, principal, largo) e o lote de cada prédio (`lot`: quintal, caminho
## da porta, célula de rua da frente). Só lê: nada aqui inventa rua ou quintal. Campo desconhecido não quebra: vai para
## `extra` (quem desenha decide em `street_style.gd`). AD-181: pisoteio (`wear`), faixa de uso (`useBand`), reserva do
## traçado (`planned`), condição do calçamento (`condition`, 0–1000) e obra (`work`); `lot.access` (como se chega à porta).
## AD-180: chão da célula pelo tempo (`ground`, `snowCm`, `wetPm`). AD-185: geometria e estado do acesso do lote.

const KNOWN_ROAD_KEYS := ["city", "cell", "surface", "users", "main", "square", "wear", "useBand", "planned", "condition", "work",
	"ground", "snowCm", "wetPm"]
const KNOWN_WORK_KEYS := ["surface", "progressPct", "repair", "payer"]


static func _cell(value: Variant) -> Variant:
	return Vector2i(int(value.x), int(value.y)) if value is Dictionary and value.get("x") != null and value.get("y") != null else null


## Uma entrada da camada `Roads`; null se não dá para saber a célula.
static func road(entry: Variant) -> Variant:
	if not entry is Dictionary:
		return null
	var cell = _cell(entry.get("cell"))
	if cell == null:
		cell = _cell(entry) # forma antiga (só x/y)
	if cell == null:
		return null
	var city = entry.get("city")
	var extra := {}
	for key in entry:
		if not String(key) in KNOWN_ROAD_KEYS and String(key) not in ["x", "y"]:
			extra[String(key)] = entry[key]
	return {
		"cell": cell,
		"city": String(city.get("value", "")) if city is Dictionary else (String(city) if city != null else ""),
		"surface": int(entry.surface) if entry.get("surface") is float or entry.get("surface") is int else -1,
		"users": maxi(int(entry.get("users")) if entry.get("users") != null else 0, 0),
		"main": bool(entry.get("main")) if entry.get("main") != null else false,
		"square": bool(entry.get("square")) if entry.get("square") != null else false,
		"wear": maxi(_int(entry.get("wear"), 0), 0),
		"use_band": clampi(_int(entry.get("useBand"), -1), -1, 4), ## -1 = servidor antigo (sem faixa: vale `users`)
		"planned": bool(entry.get("planned")) if entry.get("planned") != null else false,
		"condition": _int(entry.get("condition"), -1), ## -1 = sem calçamento (trilha, terra) ou não veio
		"work": work(entry.get("work")),
		"ground": String(entry.get("ground")) if entry.get("ground") != null else "", ## AD-180: "" = sem tempo do motor
		"snow_cm": float(entry.get("snowCm")) if entry.get("snowCm") != null else 0.0,
		"wet_pm": _int(entry.get("wetPm"), 0),
		"extra": extra,
	}


static func _int(value: Variant, fallback: int) -> int:
	return int(value) if value is float or value is int else fallback


## Obra de calçamento da célula: {surface, progress (0–100), repair, payer, extra}; {} sem obra. `extra` guarda o que o
## contrato ainda vai trazer (ex.: quem trabalha nela).
static func work(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	var extra := {}
	for key in raw:
		if not String(key) in KNOWN_WORK_KEYS:
			extra[String(key)] = raw[key]
	return {"surface": _int(raw.get("surface"), -1), "progress": clampi(_int(raw.get("progressPct"), 0), 0, 100),
		"repair": bool(raw.get("repair")) if raw.get("repair") != null else false,
		"payer": String(raw.get("payer")) if raw.get("payer") != null else "", "extra": extra}


## Camada `Roads` inteira -> {célula: rua}; vazia se não modelada ou fora do formato. A primeira entrada da célula vence.
static func roads(layer: Variant) -> Dictionary:
	if not layer is Dictionary or not bool(layer.get("isModeled", false)) or not layer.get("payload") is Array:
		return {}
	return road_list(layer.payload)


## Lista de ruas (`livingState.roads` do escopo ou `payload` da camada) -> {célula: rua}.
static func road_list(list: Variant) -> Dictionary:
	var out := {}
	if not list is Array:
		return out
	for entry in list:
		var parsed = road(entry)
		if parsed != null and not out.has(parsed.cell):
			out[parsed.cell] = parsed
	return out


## `lot` do prédio: {croft: [células], front_path: [células], street: célula|null}; vazio se o motor não mandou lote.
static func lot(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	return {"croft": _cells(raw.get("croft")), "front_path": _cells(raw.get("frontPath")), "street": _cell(raw.get("street")),
		"access": String(raw.get("access")) if raw.get("access") != null else "", ## "" = servidor sem a rua de verdade
		# AD-185: geometria do acesso (soleira primeiro), o vizinho por onde se chega e onde desemboca (reserved/planned/trodden)
		"access_cells": _cells(raw.get("accessCells")), "access_via": _int(raw.get("accessViaBuildingId"), -1),
		"access_state": String(raw.get("accessState")) if raw.get("accessState") != null else ""}


static func _cells(list: Variant) -> Array:
	var out: Array = []
	if list is Array:
		for entry in list:
			var cell = _cell(entry)
			if cell != null:
				out.append(cell)
	return out


## Quem é dono do chão em cada célula do escopo: {célula: {kind: "croft"|"path", building: id}} (a rua fica em `roads`).
static func lot_cells(buildings: Dictionary) -> Dictionary:
	var out := {}
	for id in buildings:
		var parcel: Dictionary = buildings[id].get("lot", {})
		for cell in parcel.get("croft", []):
			out[cell] = {"kind": "croft", "building": int(id)}
		for cell in parcel.get("front_path", []):
			if not out.has(cell):
				out[cell] = {"kind": "path", "building": int(id)}
	return out
