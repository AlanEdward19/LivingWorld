extends RefCounted
## Chão da cidade que o motor expõe (AD-173, lido pelo cliente na AD-177): a camada `Roads` (uma célula de rua por
## entrada: cidade, superfície, quantas pessoas usam, principal, largo) e o lote de cada prédio (`lot`: quintal, caminho
## da porta, célula de rua da frente). Só lê: nada aqui inventa rua ou quintal. Campo desconhecido não quebra: vai para
## `extra` (a AD-176 vai acrescentar tipo de acesso e obra de rua; quem desenha decide em `street_style.gd`).

const KNOWN_ROAD_KEYS := ["city", "cell", "surface", "users", "main", "square"]


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
		"extra": extra,
	}


## Camada `Roads` inteira -> {célula: rua}; vazia se não modelada ou fora do formato. A primeira entrada da célula vence.
static func roads(layer: Variant) -> Dictionary:
	var out := {}
	if not layer is Dictionary or not bool(layer.get("isModeled", false)) or not layer.get("payload") is Array:
		return out
	for entry in layer.payload:
		var parsed = road(entry)
		if parsed != null and not out.has(parsed.cell):
			out[parsed.cell] = parsed
	return out


## `lot` do prédio: {croft: [células], front_path: [células], street: célula|null}; vazio se o motor não mandou lote.
static func lot(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	return {"croft": _cells(raw.get("croft")), "front_path": _cells(raw.get("frontPath")), "street": _cell(raw.get("street"))}


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
