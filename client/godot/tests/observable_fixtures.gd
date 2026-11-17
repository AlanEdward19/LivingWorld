extends RefCounted
## Fixtures da projeção do observável (AD-170) na forma de tests/contracts/shape.city-subscribe.json, world-subscribe.json e
## visual-delta.json (camelCase, ids como {value}), para os testes da etapa 3 do cliente.

const T := preload("res://tests/test_inspector.gd")
const Fx := preload("res://tests/fixtures.gd")


static func observable(name: String, extra: Dictionary = {}) -> Dictionary:
	var body := {"name": name, "sex": "Female", "ageYears": 31, "lifeStage": "Adult", "professionId": 1, "professionName": "Lavrador",
		"heightCm": 165, "growthFraction": 1.0, "emotions": {"grief": 0.0, "fear": 0.0, "anger": 0.0, "joy": 0.0, "shame": 0.0,
		"loneliness": 0.0}, "mourning": false, "carried": []}
	body.merge(extra, true)
	return body


static func crop(id: int, stage: String, x: int = 5, y: int = 5, extra: Dictionary = {}) -> Dictionary:
	var body := {"id": id, "plot": {"x": x, "y": y}, "resourceId": 2, "resourceName": "Trigo", "status": "Growing", "stage": stage,
		"fraction": 0.5, "thirsty": false, "sownHectares": 1.5, "arableHectares": 2.0, "standing": 0, "farmId": 4, "ownerId": 8,
		"expectedHarvest": 120}
	body.merge(extra, true)
	return body


## Cidade com observável completo: dois moradores com corpo, prédio com função e dono, lavoura, animal, espólio, objeto
## e clima.
static func city(climate: Dictionary = {}) -> Dictionary:
	var env := T.city()
	var living: Dictionary = env.payload.livingState
	living.npcs[0]["observable"] = observable("Ana", {"pregnancy": {"fraction": 0.6, "babies": 1}})
	living.npcs[0]["actionStartedAtTick"] = 3
	living.npcs[1]["observable"] = observable("Beto", {"sex": "Male", "professionId": 2, "professionName": "Ferreiro",
		"mourning": true, "dominantEmotion": "grief", "carried": [{"resourceId": 2, "quantity": 6, "resourceName": "Trigo"}]})
	living["buildingStates"] = [{"buildingId": 4, "buildingTypeId": 3, "typeName": "Padaria", "function": "workplace",
		"workplaceId": 4, "locationTypeId": 7, "locationTypeName": "Padaria", "ownerId": 8, "householdIds": [], "residentIds": [],
		"workerIds": [7], "stock": [{"resourceId": 2, "quantity": 30, "resourceName": "Trigo"}], "pantryUnits": 0, "closed": false}]
	living["crops"] = [crop(70, "growing")]
	living["animals"] = [{"id": 90, "species": "rabbit", "position": {"x": 1, "y": 5}, "energy": 60, "carriesDisease": false,
		"predatorOf": null}]
	living["faunaGroups"] = []
	living["leavings"] = [{"id": 30, "location": {"x": 4, "y": 5}, "deadId": 9, "householdId": 3, "heirHouseholdId": null,
		"tick": 8, "units": 4, "itemId": null, "designId": null, "fetcherId": null, "carrierId": null}]
	living["objects"] = [{"id": 501, "designId": 77, "householdId": 3, "holderId": null, "location": {"x": 0, "y": 4},
		"forSale": true, "marketId": null, "rootShape": "cylinder", "rootMaterial": 2, "qualityFraction": 0.8}]
	living["climate"] = climate if not climate.is_empty() else {"month": 5, "seasonIndex": 2, "seasonKey": "summer",
		"temperatureC": 24.0, "regionId": 0, "cropYearFactorMilli": 1000, "drought": false}
	return env


static func world_with_density() -> Dictionary:
	var env := T.world()
	env.payload.livingState["cities"] = [{"id": {"value": "city-a"}, "name": "Alfa", "location": {"x": 2, "y": 2}, "population": 3,
		"bounds": {"x": 0, "y": 0, "width": 6, "height": 6}, "foundedFromCityId": null,
		"density": {"inside": 12, "pooled": 40, "actions": [{"action": 2, "count": 8}, {"action": null, "count": 4}], "temperatureC": 11.5}}]
	env.payload.livingState["faunaGroups"] = [{"species": "wolf", "regionId": 1, "count": 7, "center": {"x": 6, "y": 6}}]
	return env


## Transporte do inspetor com a cidade observável e as rotas de inspeção de pessoa, prédio e design.
static func transport() -> Object:
	var fake = T.transport()
	fake.on("GET /visual/subscribe?scope=City", {"status": 200, "body": city()})
	fake.on("GET /entities/building/4", {"status": 200, "body": {"kind": "building", "id": "4", "name": "Padaria do Beto",
		"state": {}, "links": [{"relation": "owner", "kind": "npc", "id": "8", "name": "Beto"},
		{"relation": "worker", "kind": "npc", "id": "7", "name": "Ana"}],
		"recentEvents": [{"eventId": 3, "kind": 4, "label": "Venda de pão", "tick": 9, "location": {"x": 1, "y": 1},
			"involved": {"subjectId": 8}}]}})
	var ana: Dictionary = T.person("Ana", {"motherId": {"value": 9}, "household": {"value": 3}, "employer": {"value": 4},
		"childIds": [{"value": 11}, {"value": 12}], "householdBuildingId": {"value": 4},
		"employment": {"id": {"value": 4}, "locationTypeId": 7, "buildingId": {"value": 4}, "name": "Padaria do Beto"},
		"currentScope": {"kind": 1}, "id": {"value": 7}, "culture": {"id": 0}, "skills": {"values": {}}, "beliefs": [],
		"memories": [], "powerIds": [], "actionStartedAtTick": 3}).body
	fake.on("GET /entities/npc/7", {"status": 200, "body": {"kind": "npc", "id": "7", "name": "Ana", "state": {"npc": ana},
		"links": [{"relation": "child", "kind": "npc", "id": "11", "name": "Caio"}], "recentEvents": []}})
	fake.on("GET /designs/77", {"status": 200, "body": {"id": 77, "parts": [{"shape": "sphere"}],
		"materials": [{"id": 2, "color": "#3366cc"}], "function": "carry", "works": true, "inventorId": 8}})
	return fake


static func delta(payload: Dictionary) -> Dictionary:
	return Fx.delta(0, 1, payload)
