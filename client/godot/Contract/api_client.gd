extends RefCounted
## Rotas HTTP não-streaming da API (relógio, lista de NPCs, ficha, catálogo, resumos). Toda resposta
## com forma congelada em tests/contracts/shape.*.json é validada aqui; cenas e painéis só recebem
## `{ok, status, body}` e nunca montam rota nem leem código HTTP.

const ShapeValidator := preload("res://Contract/shape_validator.gd")
const JsonRaw := preload("res://Contract/json_raw.gd")

signal contract_error(route: String, errors: Array)

var transport
var validate_shapes := true


func _init(p_transport) -> void:
	transport = p_transport


func _fetch(path: String, shape_name: String = "") -> Dictionary:
	var response: Dictionary = await transport.request("GET", path)
	return _checked(path, response, shape_name)


func _checked(route: String, response: Dictionary, shape_name: String) -> Dictionary:
	var result := {"ok": response.status == 200, "status": response.status, "body": response.body, "errors": []}
	if result.ok and validate_shapes and shape_name != "":
		var shape = ShapeValidator.load_shape(shape_name)
		if shape != null:
			var errors := ShapeValidator.validate(response.body, shape)
			if not errors.is_empty():
				result.ok = false
				result.errors = errors
				contract_error.emit(route, errors)
	return result


func clock_status() -> Dictionary:
	return await _fetch("/simulation/status", "simulation-status")


func clock_command(action: String) -> Dictionary:
	return await transport.request("POST", "/simulation/" + action)


func set_speed(ticks_per_second: float) -> Dictionary:
	return await transport.request("POST", "/simulation/speed", {"ticksPerSecond": ticks_per_second})


func world_calendar() -> Dictionary:
	return await _fetch("/world/calendar", "world-calendar")


func action_catalog() -> Dictionary:
	return await _fetch("/visual/catalog/actions", "action-catalog")


func npc_page(query: String, skip: int, take: int, city_id: String = "") -> Dictionary:
	var path := "/npcs?skip=%d&take=%d" % [skip, take]
	if query != "":
		path += "&q=" + query.uri_encode()
	if city_id != "":
		path += "&cityId=" + city_id.uri_encode()
	return await _fetch(path, "npc-directory")


func npc_detail(id: int) -> Dictionary:
	return await _fetch("/npcs/%d" % id, "npc-inspect")


func interior_snapshot(building_id: int) -> Dictionary:
	return await _fetch("/visual/subscribe?scope=Interior&refId=%d&mode=Spectator" % building_id, "interior-subscribe")


func city_snapshot(city_id: String) -> Dictionary:
	return await _fetch("/visual/subscribe?scope=City&refId=%s&mode=Spectator" % city_id.uri_encode(), "city-subscribe")


# --- mundos e períodos (menu principal) ---------------------------------------

func periods() -> Dictionary:
	return await _fetch("/periods")


func period_catalog(period_id: String) -> Dictionary:
	return await _fetch("/periods/%s/catalog" % period_id.uri_encode())


## Cenário do período como TEXTO cru (ver JsonRaw), com a seed trocada; "" se o período não carregou.
func period_scenario(period_id: String, seed_value: int) -> String:
	var response: Dictionary = await transport.request("GET", "/periods/%s" % period_id.uri_encode())
	if response.status != 200:
		return ""
	var scenario: String = JsonRaw.member(String(response.get("text", "")), "periodDefinition")
	return JsonRaw.replace_member(scenario, "Seed", str(seed_value)) if scenario != "" else ""


func world_preview(scenario_json: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/worlds/preview", {"scenarioJson": scenario_json})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


func world_create(scenario_json: String, world_name: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/worlds/create", {"scenarioJson": scenario_json, "name": world_name})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


# --- retoques do mundo recém-criado (autoria) ---------------------------------

func power_catalog() -> Dictionary:
	return await _fetch("/authoring/extraordinary/catalog")


func grant_power(npc_id: int, power_id: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/authoring/npcs/%d/extraordinary/grant" % npc_id, {"powerId": power_id})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


func rewrite_personality(npc_id: int, values: Dictionary) -> Dictionary:
	var response: Dictionary = await transport.request("PUT", "/authoring/npcs/%d/personality" % npc_id, values)
	return {"ok": response.status == 200, "status": response.status, "body": response.body}
