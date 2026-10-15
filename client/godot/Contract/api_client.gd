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


## Marca (`on`) ou desmarca o NPC na watchlist: com ela o rastro de cognição é retido por inteiro.
func watch_npc(id: int, on: bool) -> Dictionary:
	var response: Dictionary = await transport.request("POST" if on else "DELETE", "/npcs/%d/watchlist" % id)
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## Publica o que a câmera observa (`scope` = {kind, cityId?, buildingId?}) ou, com `{}`, retira a fonte.
func observation_scope(source_id: String, scope: Dictionary) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/observation/scope", {"sourceId": source_id, "scope": scope if not scope.is_empty() else null})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## "E se…?" do painel Cérebro: o servidor roda a decisão do NPC com estímulos sobrepostos; nada é gravado.
func decision_sandbox(id: int, overrides: Dictionary) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/npcs/%d/decision-sandbox" % id, overrides)
	return _checked("/npcs/%d/decision-sandbox" % id, {"status": response.status, "body": response.body}, "decision-sandbox")


# --- narrativas (crenças da comunidade, nunca a verdade do motor) ------------------------------

func biography(npc_id: int) -> Dictionary:
	return await _fetch("/narratives/biographies/%d" % npc_id, "narrative-biography")


func chronicle(city_id: String, period_start: int, period_end: int, top_k: int) -> Dictionary:
	return await _fetch("/narratives/chronicles?location=%s&periodStart=%d&periodEnd=%d&topK=%d" % [city_id.uri_encode(), period_start, period_end, top_k], "narrative-chronicle")


func reports() -> Dictionary:
	return await _fetch("/narratives/reports", "narrative-reports")


## Crenças vivas da comunidade (nunca `/history/truth`: é rota de motor).
func beliefs(city_id: String) -> Dictionary:
	return await _fetch("/history/beliefs?communityId=%s" % city_id.uri_encode(), "history-beliefs")


# --- conversa com NPC (só estas três rotas escrevem; o cliente não aplica nada ao mundo) -----

func conversation_start(npc_id: int) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/conversations/start", {"npcId": npc_id})
	return _checked("/conversations/start", {"status": response.status, "body": response.body}, "conversation-start")


func conversation_send(session_id: int, message: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/conversations/send", {"sessionId": session_id, "message": message})
	return _checked("/conversations/send", {"status": response.status, "body": response.body}, "conversation-send")


func conversation_end(session_id: int) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/conversations/end", {"sessionId": session_id})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


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
