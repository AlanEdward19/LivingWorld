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


## Identidade do mundo e catálogo do período em uma chamada: {period_id, axes, catalog} (catálogo vazio se não há período).
func world_identity() -> Dictionary:
	var out := {"period_id": "", "axes": [], "catalog": {}}
	var info: Dictionary = await world_info()
	if not info.ok:
		return out
	out.period_id = String(info.body.get("periodId", ""))
	out.axes = info.body.get("technologyAxes", [])
	if out.period_id == "":
		return out
	var catalog: Dictionary = await period_catalog(out.period_id)
	if catalog.ok and catalog.body is Dictionary:
		out.catalog = catalog.body
		if registry != null:
			registry.load_catalog(catalog.body)
	return out
