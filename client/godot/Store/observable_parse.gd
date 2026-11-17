extends RefCounted
## Leitura da projeção do observável (AD-170, ADR-0047 etapa 2) para o formato local do cliente. Só converte o que o servidor
## manda (snapshot e delta); nenhuma regra de simulação, nenhum valor inventado: campo ausente vira vazio/zero.


static func cell_of(value: Variant) -> Vector2i:
	return Vector2i(int(value.x), int(value.y)) if value is Dictionary else Vector2i.ZERO


static func _int_or(value: Variant, fallback: int = -1) -> int:
	return fallback if value == null else int(value)


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
		carried.append({"resource_id": int(load.get("resourceId", -1)), "quantity": int(load.get("quantity", 0)),
			"name": String(load.get("resourceName", "")) if load.get("resourceName") != null else ""})
	var pregnancy = raw.get("pregnancy")
	var illness = raw.get("illness")
	var held = raw.get("held")
	return {
		"name": String(raw.get("name", "")), "sex": String(raw.get("sex", "")), "age": int(raw.get("ageYears", 0)),
		"stage": String(raw.get("lifeStage", "")), "profession_id": _int_or(raw.get("professionId")),
		"profession_name": String(raw.get("professionName", "")) if raw.get("professionName") != null else "",
		"height_cm": int(raw.get("heightCm", 0)), "growth": float(raw.get("growthFraction", 1.0)),
		"pregnancy": float(pregnancy.get("fraction", 0.0)) if pregnancy is Dictionary else 0.0,
		"babies": int(pregnancy.get("babies", 0)) if pregnancy is Dictionary else 0,
		"ill": illness is Dictionary, "symptomatic": illness is Dictionary and bool(illness.get("symptomatic", false)),
		"bedridden": illness is Dictionary and bool(illness.get("bedridden", false)),
		"disease_id": int(illness.get("diseaseId", -1)) if illness is Dictionary else -1,
		"emotions": emotions,
		"dominant": String(raw.get("dominantEmotion", "")) if raw.get("dominantEmotion") != null else "",
		"mourning": bool(raw.get("mourning", false)), "carried": carried,
		"held": {"item_id": int(held.get("itemId", 0)), "design_id": int(held.get("designId", 0))} if held is Dictionary else {},
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
		actions.append({"action": _int_or(entry.get("action")), "count": int(entry.get("count", 0))})
	return {"inside": int(raw.get("inside", 0)), "pooled": int(raw.get("pooled", 0)), "actions": actions,
		"temperature": float(raw.get("temperatureC", 0.0))}


static func building_state(raw: Dictionary) -> Dictionary:
	var stock: Array = []
	for line in raw.get("stock", []) if raw.get("stock") is Array else []:
		stock.append({"resource_id": int(line.get("resourceId", -1)), "quantity": int(line.get("quantity", 0)),
			"name": String(line.get("resourceName", "")) if line.get("resourceName") != null else ""})
	return {
		"building_id": int(raw.buildingId), "type": int(raw.get("buildingTypeId", -1)),
		"type_name": String(raw.get("typeName", "")) if raw.get("typeName") != null else "",
		"function": String(raw.get("function", "structure")), "workplace_id": _int_or(raw.get("workplaceId")),
		"location_type_id": _int_or(raw.get("locationTypeId")),
		"location_type_name": String(raw.get("locationTypeName", "")) if raw.get("locationTypeName") != null else "",
		"owner_id": _int_or(raw.get("ownerId")), "households": _ids(raw.get("householdIds")),
		"residents": _ids(raw.get("residentIds")), "workers": _ids(raw.get("workerIds")), "stock": stock,
		"pantry": int(raw.get("pantryUnits", 0)), "closed": bool(raw.get("closed", false)),
	}


## Lavoura: estágio visual (unsown, planted, growing, mature, harvested, fallow), fração, cultura, área e colheita esperada.
static func crop(raw: Dictionary) -> Dictionary:
	return {
		"id": int(raw.id), "cell": cell_of(raw.get("plot")), "resource_id": int(raw.get("resourceId", -1)),
		"resource_name": String(raw.get("resourceName", "")) if raw.get("resourceName") != null else "",
		"status": String(raw.get("status", "")), "stage": String(raw.get("stage", "growing")),
		"fraction": clampf(float(raw.get("fraction", 0.0)), 0.0, 1.0), "thirsty": bool(raw.get("thirsty", false)),
		"sown": float(raw.get("sownHectares", 0.0)), "arable": float(raw.get("arableHectares", 0.0)),
		"standing": int(raw.get("standing", 0)), "farm_id": _int_or(raw.get("farmId")), "owner_id": _int_or(raw.get("ownerId")),
		"expected": int(raw.get("expectedHarvest", 0)),
	}


static func animal(raw: Dictionary) -> Dictionary:
	return {"id": int(raw.id), "species": String(raw.get("species", "")), "cell": cell_of(raw.get("position")),
		"energy": int(raw.get("energy", 0)), "disease": bool(raw.get("carriesDisease", false)),
		"predator_of": String(raw.get("predatorOf", "")) if raw.get("predatorOf") != null else ""}


static func fauna_key(raw: Dictionary) -> String:
	return "%s:%d" % [String(raw.get("species", "")), int(raw.get("regionId", -1))]


static func fauna_group(raw: Dictionary) -> Dictionary:
	return {"key": fauna_key(raw), "species": String(raw.get("species", "")), "region": int(raw.get("regionId", -1)),
		"count": int(raw.get("count", 0)), "cell": cell_of(raw.get("center"))}


static func leaving(raw: Dictionary) -> Dictionary:
	return {"id": int(raw.id), "cell": cell_of(raw.get("location")), "dead_id": _int_or(raw.get("deadId")),
		"household_id": _int_or(raw.get("householdId")), "heir_household_id": _int_or(raw.get("heirHouseholdId")),
		"tick": int(raw.get("tick", 0)), "units": int(raw.get("units", 0)), "item_id": _int_or(raw.get("itemId")),
		"design_id": _int_or(raw.get("designId")), "fetcher_id": _int_or(raw.get("fetcherId")),
		"carrier_id": _int_or(raw.get("carrierId"))}


## Peça inventada: casa dona, quem leva, onde está, à venda; forma e material da peça raiz (o resto em /designs/{id}).
static func object(raw: Dictionary) -> Dictionary:
	var location = raw.get("location")
	return {"id": int(raw.id), "design_id": int(raw.get("designId", 0)), "household_id": _int_or(raw.get("householdId")),
		"holder_id": _int_or(raw.get("holderId")), "cell": cell_of(location), "has_cell": location is Dictionary,
		"for_sale": bool(raw.get("forSale", false)), "market_id": _int_or(raw.get("marketId")),
		"shape": String(raw.get("rootShape", "box")) if raw.get("rootShape") != null else "box",
		"material": _int_or(raw.get("rootMaterial"), 0), "quality": float(raw.get("qualityFraction", 0.0))}


static func climate(raw: Variant) -> Dictionary:
	if not raw is Dictionary:
		return {}
	return {"month": int(raw.get("month", 0)), "season": int(raw.get("seasonIndex", 0)),
		"season_key": String(raw.get("seasonKey", "")), "temperature": float(raw.get("temperatureC", 0.0)),
		"region": int(raw.get("regionId", -1)), "crop_factor": int(raw.get("cropYearFactorMilli", 1000)),
		"drought": bool(raw.get("drought", false))}


## Evento do delta: id do servidor, rótulo, tipo, hora, lugar e envolvidos (crime: autor, vítima, caso, desfecho; `record_only`
## = só registro, o rótulo narrado nunca é mostrado).
static func event(raw: Dictionary, fallback_id: int) -> Dictionary:
	var location = raw.get("location")
	var involved: Dictionary = {}
	var raw_involved = raw.get("involved")
	if raw_involved is Dictionary:
		for key in ["subjectId", "otherId", "authorId", "victimId", "householdId", "buildingId", "workplaceId", "caseId",
				"leavingId", "designId", "amount"]:
			if raw_involved.get(key) != null:
				involved[key] = int(raw_involved[key])
		if raw_involved.get("cityId") != null:
			involved.cityId = String(raw_involved.cityId)
		if raw_involved.get("outcome") != null:
			involved.outcome = String(raw_involved.outcome)
		if raw_involved.get("relatedIds") is Array:
			involved.relatedIds = _ids(raw_involved.relatedIds)
	return {"id": int(raw.eventId) if raw.get("eventId") != null else fallback_id, "label": String(raw.get("label", "")),
		"kind": int(raw.get("kind", 0)), "tick": int(raw.get("tick", 0)), "cell": cell_of(location),
		"has_cell": location is Dictionary, "involved": involved,
		"record_only": raw_involved is Dictionary and bool(raw_involved.get("recordOnly", false))}
