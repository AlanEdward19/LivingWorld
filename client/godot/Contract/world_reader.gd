extends "res://Contract/api_client.gd"
## Camada única de leitura do cliente (ADR-0047): todo painel recebe ESTE objeto como `api`, nunca o transporte.
## Herda as rotas do ApiClient e acrescenta o que é compartilhado: a ficha do NPC fica em cache até a hora do
## mundo mudar (inspetor, cérebro e seguir não pedem a mesma ficha duas vezes) e pedidos iguais em voo viram um só.
## Também aprende vínculos no registro de entidades a cada ficha lida.

class _Pending:
	signal done(response: Dictionary)

var tick := -1 ## hora do mundo do último status do relógio; muda = cache vencido
var registry ## EntityRegistry (opcional)
var _npc_cache: Dictionary = {} ## id -> {tick, response}
var _pending: Dictionary = {} ## caminho -> _Pending


func _init(p_transport) -> void:
	super(p_transport)


func set_tick(value: int) -> void:
	if value != tick:
		tick = value
		_npc_cache.clear()
		_entity_cache.clear()


func invalidate_npc(id: int) -> void:
	_npc_cache.erase(id)


func npc_detail(id: int) -> Dictionary:
	var cached = _npc_cache.get(id)
	if cached != null and int(cached.tick) == tick:
		return cached.response
	var path := "/npcs/%d" % id
	if _pending.has(path):
		return await _pending[path].done
	var pending := _Pending.new()
	_pending[path] = pending
	var response: Dictionary = await super(id)
	_pending.erase(path)
	if response.ok:
		_npc_cache[id] = {"tick": tick, "response": response}
		if registry != null and response.body is Dictionary:
			registry.learn_person(id, response.body)
	pending.done.emit(response)
	return response


func materialize_npc(id: int) -> Dictionary:
	invalidate_npc(id)
	var response: Dictionary = await super(id)
	if response.ok and registry != null and response.body is Dictionary:
		registry.learn_person(id, response.body)
	return response


## Envelope comum da rota de inspeção (as gravações `shape.entity-*.json` congelam um exemplo cada, com campos que variam
## de pessoa para pessoa; o cliente exige o envelope e, para pessoa, a ficha no contrato `npc-inspect`).
const ENTITY_ENVELOPE := {"kind": "string", "id": "string", "name": "string", "state": "null",
	"links": [{"relation": "string", "kind": "string", "id": "string"}], "recentEvents": "[]"}

var _entity_cache: Dictionary = {} ## "kind/id" -> {tick, response}
var _designs: Dictionary = {} ## id do design -> resposta (o id é o hash da estrutura: nunca muda)


## `GET /entities/{kind}/{id}` (AD-170): {kind, id, name, state, links[], recentEvents[]}, em cache até a hora do mundo mudar.
## A ficha de pessoa alimenta também o cache de `/npcs/{id}` (cérebro, seguir e inspetor não pedem de novo) e o registro.
func entity(kind: String, id: Variant) -> Dictionary:
	var key := "%s/%s" % [kind, str(id)]
	var cached = _entity_cache.get(key)
	if cached != null and int(cached.tick) == tick:
		return cached.response
	var path := "/entities/%s/%s" % [kind, str(id).uri_encode()]
	if _pending.has(path):
		return await _pending[path].done
	var pending := _Pending.new()
	_pending[path] = pending
	var response: Dictionary = _entity_checked(path, await transport.request("GET", path), kind)
	_pending.erase(path)
	if response.ok and response.body is Dictionary:
		_entity_cache[key] = {"tick": tick, "response": response}
		if _entity_cache.size() > 200:
			_entity_cache.clear()
		if registry != null:
			registry.learn_entity(response.body)
		var state = response.body.get("state")
		if kind == "npc" and state is Dictionary and state.get("npc") is Dictionary:
			_npc_cache[int(id)] = {"tick": tick, "response": {"ok": true, "status": 200, "body": state.npc, "errors": []}}
			if registry != null:
				registry.learn_person(int(id), state.npc)
	pending.done.emit(response)
	return response


func _entity_checked(path: String, raw: Dictionary, kind: String) -> Dictionary:
	var result := {"ok": raw.status == 200, "status": raw.status, "body": raw.body, "errors": []}
	if not result.ok or not validate_shapes:
		return result
	var errors := ShapeValidator.validate(raw.body, ENTITY_ENVELOPE)
	if kind == "npc" and errors.is_empty():
		var npc_shape = ShapeValidator.load_shape("npc-inspect")
		var state = raw.body.get("state")
		if npc_shape != null and state is Dictionary and state.get("npc") is Dictionary:
			errors = ShapeValidator.validate(state.npc, npc_shape, "$.state.npc")
	if not errors.is_empty():
		result.ok = false
		result.errors = errors
		contract_error.emit(path, errors)
	return result


## `GET /designs/{id}`: partes, materiais e função de uma invenção (imutável: cache para sempre).
func design(id: int) -> Dictionary:
	if _designs.has(id):
		return _designs[id]
	var path := "/designs/%d" % id
	if _pending.has(path):
		return await _pending[path].done
	var pending := _Pending.new()
	_pending[path] = pending
	var response: Dictionary = await _fetch(path)
	_pending.erase(path)
	if response.ok:
		_designs[id] = response
	pending.done.emit(response)
	return response


func cached_design(id: int) -> Variant:
	var response = _designs.get(id)
	return response.body if response != null and response.body is Dictionary else null


## Identidade do mundo e catálogo do período em uma chamada: {period_id, axes, catalog} (catálogo vazio se não há período).
func world_identity() -> Dictionary:
	var out := {"period_id": "", "axes": [], "catalog": {}, "scale": {}}
	var info: Dictionary = await world_info()
	if not info.ok:
		return out
	out.period_id = String(info.body.get("periodId", ""))
	out.axes = info.body.get("technologyAxes", [])
	out.scale = info.body.get("scale", {}) if info.body.get("scale") is Dictionary else {} # ADR-0052
	if out.period_id == "":
		return out
	var catalog: Dictionary = await period_catalog(out.period_id)
	if catalog.ok and catalog.body is Dictionary:
		out.catalog = catalog.body
		if registry != null:
			registry.load_catalog(catalog.body)
	return out
