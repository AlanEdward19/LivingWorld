extends RefCounted
## Rotas HTTP não-streaming da API (relógio, lista de NPCs, ficha, catálogo, resumos). Toda resposta
## com forma congelada em tests/contracts/shape.*.json é validada aqui; cenas e painéis só recebem
## `{ok, status, body}` e nunca montam rota nem leem código HTTP.

const ShapeValidator := preload("res://Contract/shape_validator.gd")

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
	return await _fetch("/npcs/%d" % id, "npc-detail")


func interior_snapshot(building_id: int) -> Dictionary:
	return await _fetch("/visual/subscribe?scope=Interior&refId=%d&mode=Spectator" % building_id, "interior-subscribe")


func city_snapshot(city_id: String) -> Dictionary:
	return await _fetch("/visual/subscribe?scope=City&refId=%s&mode=Spectator" % city_id.uri_encode(), "city-subscribe")
