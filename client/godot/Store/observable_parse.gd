extends RefCounted
## Leitura da projeção do observável (AD-170, ADR-0047 etapa 2) para o formato local do cliente. Só converte o que o servidor
## manda (snapshot e delta); nenhuma regra de simulação, nenhum valor inventado: campo ausente vira vazio/zero.
const WorldTime := preload("res://Contract/world_time.gd")


static func cell_of(value: Variant) -> Vector2i:
	return Vector2i(int(value.x), int(value.y)) if value is Dictionary else Vector2i.ZERO


## Delta sem nulos (AD-171 item 16): chave ausente e `null` explícito valem o mesmo (o padrão).
static func _i(source: Variant, key: String, fallback: int) -> int:
	var value = source.get(key) if source is Dictionary else null
	return fallback if value == null else int(value)


static func _f(source: Variant, key: String, fallback: float) -> float:
	var value = source.get(key) if source is Dictionary else null
	return fallback if value == null else float(value)


static func _b(source: Variant, key: String, fallback: bool) -> bool:
	var value = source.get(key) if source is Dictionary else null
	return fallback if value == null else bool(value)


static func _s(source: Variant, key: String, fallback: String = "") -> String:
	var value = source.get(key) if source is Dictionary else null
	return fallback if value == null else String(value)


static func _int_or(value: Variant, fallback: int = -1) -> int:
	return fallback if value == null else int(value)


## `npc.route` (AD-176): células da rota, de onde está até a chegada; ponto sem x/y é pulado. Menos de 2 = não anda.
static func route(list: Variant) -> Array:
	var out: Array = []
	for value in list if list is Array else []:
		if value is Dictionary and value.get("x") != null and value.get("y") != null:
			out.append(Vector2i(int(value.x), int(value.y)))
	return out if out.size() >= 2 else []


## `npc.routeOver` (AD-185): índices de `route` que passam por cima (escada, laje, telhado); fora da rota é descartado.
static func route_over(list: Variant, size: int) -> Array:
	var out: Array = []
	for value in list if list is Array else []:
		if value != null and int(value) >= 0 and int(value) < size:
			out.append(int(value))
	return out


static func _ids(list: Variant) -> Array:
	var out: Array = []
	for value in list if list is Array else []:
		out.append(int(value))
	return out


## `npcs[].observable`: quem é, corpo, gravidez já aparente, doença, emoções, carga e recipiente.
static func observable(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	var emotions: Dictionary = {}
	var raw_emotions = raw.get("emotions")
	for key in ["grief", "fear", "anger", "joy", "shame", "loneliness"]:
		emotions[key] = float(raw_emotions.get(key, 0.0)) if raw_emotions is Dictionary else 0.0
	var carried: Array = []
	for load in raw.get("carried", []) if raw.get("carried") is Array else []:
		carried.append({"resource_id": _i(load, "resourceId", -1), "quantity": _i(load, "quantity", 0),
			"name": _s(load, "resourceName", "") if load.get("resourceName") != null else ""})
	var pregnancy = raw.get("pregnancy")
	var illness = raw.get("illness")
	var held = raw.get("held")
	return {
		"name": _s(raw, "name", ""), "sex": _s(raw, "sex", ""), "age": _i(raw, "ageYears", 0),
		"stage": _s(raw, "lifeStage", ""), "profession_id": _int_or(raw.get("professionId")),
		"profession_name": _s(raw, "professionName", "") if raw.get("professionName") != null else "",
		"height_cm": _i(raw, "heightCm", 0), "growth": _f(raw, "growthFraction", 1.0),
		"pregnancy": _f(pregnancy, "fraction", 0.0) if pregnancy is Dictionary else 0.0,
		"babies": _i(pregnancy, "babies", 0) if pregnancy is Dictionary else 0,
		"ill": illness is Dictionary, "symptomatic": illness is Dictionary and _b(illness, "symptomatic", false),
		"bedridden": illness is Dictionary and _b(illness, "bedridden", false),
		"disease_id": _i(illness, "diseaseId", -1) if illness is Dictionary else -1,
		"emotions": emotions,
		"dominant": _s(raw, "dominantEmotion", "") if raw.get("dominantEmotion") != null else "",
		"mourning": _b(raw, "mourning", false), "carried": carried,
		"held": {"item_id": _i(held, "itemId", 0), "design_id": _i(held, "designId", 0)} if held is Dictionary else {},
	}


## `actionTarget` {kind, id} (só trabalho, sono e conversa com o cônjuge vêm do motor); vazio se não há.
static func target(raw: Variant) -> Dictionary:
	if raw is Dictionary and raw.get("kind") != null and raw.get("id") != null:
		return {"kind": String(raw.kind), "id": String(raw.id)}
	return {}


## `cities[].density` (escopo Mundo): quem está dentro, quantos no pool, ações mais comuns e temperatura.
static func density(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	var actions: Array = []
	for entry in raw.get("actions", []) if raw.get("actions") is Array else []:
		actions.append({"action": _int_or(entry.get("action")), "count": _i(entry, "count", 0)})
	return {"inside": _i(raw, "inside", 0), "pooled": _i(raw, "pooled", 0), "actions": actions,
		"temperature": _f(raw, "temperatureC", 0.0)}


static func building_state(raw: Dictionary) -> Dictionary:
	var stock: Array = []
	for line in raw.get("stock", []) if raw.get("stock") is Array else []:
		stock.append({"resource_id": _i(line, "resourceId", -1), "quantity": _i(line, "quantity", 0),
			"name": _s(line, "resourceName", "") if line.get("resourceName") != null else ""})
	return {
		"building_id": int(raw.buildingId), "type": _i(raw, "buildingTypeId", -1),
		"type_name": _s(raw, "typeName", "") if raw.get("typeName") != null else "",
		"function": _s(raw, "function", "structure"), "workplace_id": _int_or(raw.get("workplaceId")),
		"location_type_id": _int_or(raw.get("locationTypeId")),
		"location_type_name": _s(raw, "locationTypeName", "") if raw.get("locationTypeName") != null else "",
		"owner_id": _int_or(raw.get("ownerId")), "households": _ids(raw.get("householdIds")),
		"residents": _ids(raw.get("residentIds")), "workers": _ids(raw.get("workerIds")), "stock": stock,
		"pantry": _i(raw, "pantryUnits", 0), "closed": _b(raw, "closed", false),
		"lit": _b(raw, "lit", false), # AD-189: gente acordada dentro depois do pôr do sol (o motor diz)
	}


## Lavoura: estágio visual (unsown, planted, growing, mature, harvested, fallow), fração, cultura, área e colheita esperada.
static func crop(raw: Dictionary) -> Dictionary:
	return {
		"id": int(raw.id), "cell": cell_of(raw.get("plot")), "resource_id": _i(raw, "resourceId", -1),
		"resource_name": _s(raw, "resourceName", "") if raw.get("resourceName") != null else "",
		"status": _s(raw, "status", ""), "stage": _s(raw, "stage", "growing"),
		"fraction": clampf(_f(raw, "fraction", 0.0), 0.0, 1.0), "thirsty": _b(raw, "thirsty", false),
		"sown": _f(raw, "sownHectares", 0.0), "arable": _f(raw, "arableHectares", 0.0),
		"standing": _i(raw, "standing", 0), "farm_id": _int_or(raw.get("farmId")), "owner_id": _int_or(raw.get("ownerId")),
		"expected": _i(raw, "expectedHarvest", 0),
	}


static func animal(raw: Dictionary) -> Dictionary:
	return {"id": int(raw.id), "species": _s(raw, "species", ""), "cell": cell_of(raw.get("position")),
		"energy": _i(raw, "energy", 0), "disease": _b(raw, "carriesDisease", false),
		"predator_of": _s(raw, "predatorOf", "") if raw.get("predatorOf") != null else ""}


static func fauna_key(raw: Dictionary) -> String:
	return "%s:%d" % [_s(raw, "species", ""), _i(raw, "regionId", -1)]


static func fauna_group(raw: Dictionary) -> Dictionary:
	return {"key": fauna_key(raw), "species": _s(raw, "species", ""), "region": _i(raw, "regionId", -1),
		"count": _i(raw, "count", 0), "cell": cell_of(raw.get("center"))}


static func leaving(raw: Dictionary) -> Dictionary:
	return {"id": int(raw.id), "cell": cell_of(raw.get("location")), "dead_id": _int_or(raw.get("deadId")),
		"household_id": _int_or(raw.get("householdId")), "heir_household_id": _int_or(raw.get("heirHouseholdId")),
		"tick": WorldTime.hours_of(raw, "second"), "units": _i(raw, "units", 0), "item_id": _int_or(raw.get("itemId")),
		"design_id": _int_or(raw.get("designId")), "fetcher_id": _int_or(raw.get("fetcherId")),
		"carrier_id": _int_or(raw.get("carrierId"))}


## Peça inventada: casa dona, quem leva, onde está, à venda; forma e material da peça raiz (o resto em /designs/{id}).
static func object(raw: Dictionary) -> Dictionary:
	var location = raw.get("location")
	return {"id": int(raw.id), "design_id": _i(raw, "designId", 0), "household_id": _int_or(raw.get("householdId")),
		"holder_id": _int_or(raw.get("holderId")), "cell": cell_of(location), "has_cell": location is Dictionary,
		"for_sale": _b(raw, "forSale", false), "market_id": _int_or(raw.get("marketId")),
		"shape": _s(raw, "rootShape", "box") if raw.get("rootShape") != null else "box",
		"material": _int_or(raw.get("rootMaterial"), 0), "quality": _f(raw, "qualityFraction", 0.0)}


static func climate(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	return {"month": _i(raw, "month", 0), "season": _i(raw, "seasonIndex", 0),
		"season_key": _s(raw, "seasonKey", ""), "temperature": _f(raw, "temperatureC", 0.0),
		"region": _i(raw, "regionId", -1), "crop_factor": _i(raw, "cropYearFactorMilli", 1000),
		"drought": _b(raw, "drought", false), "weather": weather(raw.get("weather"))}


## `climate.weather` (AD-180): o tempo do dia que o motor gerou para a região; {} = tempo desligado ou servidor antigo (o
## cliente então não desenha chuva, neve nem chão molhado: nada inventado).
static func weather(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	return {"precipitation": _s(raw, "precipitation", "none"), "intensity": _s(raw, "intensity", "none"),
		"mm": _f(raw, "precipitationMm", 0.0), "storm": _b(raw, "storm", false), "hail": _b(raw, "hail", false),
		"frost": _b(raw, "frost", false), "drought": _b(raw, "drought", false), "heat_wave": _b(raw, "heatWave", false),
		"cold_wave": _b(raw, "coldWave", false), "wind": _f(raw, "windMs", 0.0), "now_c": _f(raw, "temperatureNowC", 0.0),
		"feels_c": _f(raw, "feelsLikeC", 0.0), "min_c": _f(raw, "minC", 0.0), "max_c": _f(raw, "maxC", 0.0),
		"daylight": _f(raw, "daylightHours", 0.0), "sunrise": _f(raw, "sunriseHour", -1.0), "sunset": _f(raw, "sunsetHour", -1.0),
		"ground": _s(raw, "ground", "dry"), "snow_cm": _f(raw, "snowCm", 0.0), "wet_pm": _i(raw, "groundWetPm", 0),
		"lost_days": _i(raw, "outdoorLostDays", 0),
		# AD-189: de onde o vento vem (graus do norte = −y do mapa; -1 = o motor não disse), a folhagem e a curva do dia por hora
		"wind_from": _f(raw, "windFromDeg", -1.0) if raw.get("windFromDeg") != null else -1.0,
		"foliage": foliage(raw.get("foliage")), "hourly": _floats(raw.get("hourlyC")),
		"hourly_feels": _floats(raw.get("hourlyFeelsLikeC"))}


## AD-189: folhagem da região {fraction 0..1, phase bare|budding|full|falling, turning 0..1}; {} = o motor não mandou.
static func foliage(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	return {"fraction": _f(raw, "fraction", 0.0), "phase": _s(raw, "phase", "bare"), "turning": _f(raw, "turning", 0.0)}


static func _floats(raw: Variant) -> Array:
	var out: Array = []
	for value in raw if raw is Array else []:
		out.append(float(value))
	return out


## AD-189: a hora do tick na curva do dia que o motor mandou (o clima não é reenviado só porque a hora mudou). Devolve true se
## a temperatura ou a sensação de agora mudaram.
static func at_hour(weather: Dictionary, tick: int) -> bool:
	var hourly: Array = weather.get("hourly", [])
	var feels: Array = weather.get("hourly_feels", [])
	if hourly.is_empty() or feels.size() != hourly.size() or tick < 0:
		return false
	var index := tick % hourly.size()
	var now := float(hourly[index])
	var feel := float(feels[index])
	if is_equal_approx(now, float(weather.get("now_c", INF))) and is_equal_approx(feel, float(weather.get("feels_c", INF))):
		return false
	weather.now_c = now
	weather.feels_c = feel
	return true


## Camada `Climate` do mundo: [{region, cell (centro), temperature, drought, weather}] (o tempo de cada região).
static func climate_regions(list: Variant) -> Array:
	var out: Array = []
	for raw in list if list is Array else []:
		if raw is Dictionary:
			out.append({"region": _i(raw, "regionId", -1), "cell": cell_of(raw.get("center")),
				"temperature": _f(raw, "temperatureC", 0.0), "drought": _b(raw, "drought", false), "weather": weather(raw.get("weather"))})
	return out


## Evento do delta: id do servidor, rótulo, tipo, hora, lugar e envolvidos (crime: autor, vítima, caso, desfecho; `record_only`
## = só registro, o rótulo narrado nunca é mostrado).
static func event(raw: Dictionary, fallback_id: int) -> Dictionary:
	var location = raw.get("location")
	var involved: Dictionary = {}
	var raw_involved = raw.get("involved")
	if raw_involved is Dictionary:
		for key in ["subjectId", "otherId", "authorId", "victimId", "householdId", "buildingId", "workplaceId", "caseId",
				"leavingId", "designId", "amount", "workId"]:
			if raw_involved.get(key) != null:
				involved[key] = int(raw_involved[key])
		if raw_involved.get("cityId") != null:
			involved.cityId = String(raw_involved.cityId)
		if raw_involved.get("outcome") != null:
			involved.outcome = String(raw_involved.outcome)
		if raw_involved.get("motive") != null and String(raw_involved.motive) != "":
			involved.motive = String(raw_involved.motive)
		if raw_involved.get("relatedIds") is Array:
			involved.relatedIds = _ids(raw_involved.relatedIds)
	return {"id": int(raw.eventId) if raw.get("eventId") != null else fallback_id, "label": _s(raw, "label", ""),
		"kind": _i(raw, "kind", 0), "tick": WorldTime.hours_of(raw, "second"), "cell": cell_of(location),
		"has_cell": location is Dictionary, "involved": involved,
		"record_only": raw_involved is Dictionary and _b(raw_involved, "recordOnly", false)}


## AD-172: `rest` e `food` com tempo restante não são reenviados quando só andaram no relógio; o cliente faz a mesma conta
## do servidor (`ProcessAdvance`) para todo processo conhecido que não veio no delta. Devolve true se algum mudou.
static func advance_processes(processes: Dictionary, dt: int, skip: Dictionary = {}) -> bool:
	var changed := false
	for id in processes:
		var process: Dictionary = processes[id]
		if skip.has(id) or not String(process.kind) in ["rest", "food"] or process.remaining == null:
			continue
		var remaining := float(process.remaining)
		var progress := float(process.progress)
		if remaining <= 0.0 or progress >= 1.0:
			continue
		var duration := roundf(remaining / (1.0 - progress))
		remaining = maxf(0.0, remaining - dt)
		process.remaining = remaining
		process.progress = clampf(1.0 - remaining / duration, 0.0, 1.0) if duration > 0.0 else 1.0
		changed = true
	return changed


## ProcessVisual do servidor: `kind` (rest, food, cook, water, crop, construction, extraordinary-construct, process),
## `targetId` (o NPC que age; a lavoura; o tipo de prédio da obra), progresso 0..1 e célula quando o servidor sabe onde.
static func process(visual: Dictionary) -> Dictionary:
	var location = visual.get("location")
	return {"id": int(visual.id), "kind": _s(visual, "kind", "process"), "target": _i(visual, "targetId", -1),
		"progress": clampf(_f(visual, "progress", 0.0), 0.0, 1.0), "descriptor": _s(visual, "descriptorKey"),
		"quality": visual.get("quality"), "remaining": visual.get("remainingHours"),
		"cell": Vector2i(int(location.x), int(location.y)) if location is Dictionary else Vector2i.ZERO, "has_cell": location is Dictionary}


static func apply_bag(bag: Dictionary, upserts: Array, removed: Array, parse: Callable) -> bool:
	for raw in upserts:
		bag[int(raw.id)] = parse.call(raw)
	var touched := not upserts.is_empty()
	for id in removed:
		touched = bag.erase(int(id.value) if id is Dictionary and id.has("value") else int(id)) or touched
	return touched
