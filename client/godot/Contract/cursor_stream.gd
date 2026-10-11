extends RefCounted
## Assinatura de UM escopo visual (mundo, cidade ou interior): snapshot, deltas em ordem de
## `sequence`, `replay` em lacuna, reconexão com espera crescente. Cenas e UI só falam com esta
## classe; nenhum deles interpreta HTTP/WS. O cliente nunca palpita estado: lacuna => replay.

const ShapeValidator := preload("res://Contract/shape_validator.gd")

signal snapshot_received(envelope: Dictionary)
signal delta_applied(envelope: Dictionary)
signal access_denied()
signal connection_changed(state: String, retry_in: float)
signal contract_error(errors: Array)

const BACKOFF_SECONDS: Array[float] = [1.0, 2.0, 4.0, 8.0]
const SHAPE_BY_SCOPE := {"World": "world-subscribe", "City": "city-subscribe", "Interior": "interior-subscribe"}

var transport
var scope_kind := ""
var ref_id := ""
var cursor: Dictionary = {}
var state := "idle" ## idle | online | offline | denied
## Espera injetável: os testes substituem por um registro sem relógio.
var sleeper: Callable = Callable()
var validate_shapes := true

var _channel
var _generation := 0
var _tree: SceneTree


func _init(p_transport, p_tree: SceneTree) -> void:
	transport = p_transport
	_tree = p_tree
	sleeper = func(seconds: float): await _tree.create_timer(seconds).timeout


func subscribe(kind: String, id: String = "") -> void:
	close()
	scope_kind = kind
	ref_id = id
	cursor = {}
	_generation += 1
	await _connect(_generation)


func close() -> void:
	_generation += 1
	if _channel != null:
		_channel.close()
		_channel = null
	state = "idle"


func _scope_query() -> String:
	return "scope=%s&refId=%s&mode=Spectator" % [scope_kind, ref_id.uri_encode()]


func _set_state(new_state: String, retry_in: float = 0.0) -> void:
	state = new_state
	connection_changed.emit(new_state, retry_in)


## Obtém o snapshot (com espera crescente se o servidor estiver fora) e abre o canal.
func _connect(generation: int) -> void:
	var attempt := 0
	while generation == _generation:
		var response: Dictionary = await transport.request("GET", "/visual/subscribe?" + _scope_query())
		if generation != _generation:
			return
		if response.status == 403:
			state = "denied"
			access_denied.emit()
			return
		if response.status == 200 and response.body is Dictionary:
			if not _accept_snapshot(response.body):
				return
			_open_channel(generation)
			_set_state("online")
			return
		var delay: float = BACKOFF_SECONDS[min(attempt, BACKOFF_SECONDS.size() - 1)]
		attempt += 1
		_set_state("offline", delay)
		await sleeper.call(delay)


func _open_channel(generation: int) -> void:
	_channel = transport.open_channel("/visual/ws?" + _scope_query())
	_channel.message.connect(func(data): _on_message(generation, data))
	_channel.closed.connect(func(): _on_channel_closed(generation))


func _accept_snapshot(envelope: Dictionary) -> bool:
	if validate_shapes and not _shape_ok(envelope):
		return false
	cursor = envelope.cursor
	snapshot_received.emit(envelope)
	return true


func _shape_ok(envelope: Dictionary) -> bool:
	var shape = ShapeValidator.load_shape(SHAPE_BY_SCOPE.get(scope_kind, ""))
	if shape == null:
		return true # contratos só existem no repositório de desenvolvimento
	var errors := ShapeValidator.validate(envelope, shape)
	if errors.is_empty():
		return true
	contract_error.emit(errors)
	return false


func _on_message(generation: int, data: Variant) -> void:
	if generation != _generation or not (data is Dictionary):
		return
	if data.has("toCursor"):
		await _on_delta(generation, data)
	else:
		_accept_snapshot(data) # o servidor reenvia um snapshot ao aceitar o canal: reset autoritativo


func _on_delta(generation: int, envelope: Dictionary) -> void:
	var from_seq: int = int(envelope.fromCursor.sequence)
	var to_seq: int = int(envelope.toCursor.sequence)
	var last: int = int(cursor.get("sequence", 0))
	if to_seq <= last:
		return # duplicata: já aplicada
	if from_seq != last:
		await _replay(generation) # lacuna: o delta recebido é descartado, o replay o traz de volta
		return
	cursor = envelope.toCursor
	delta_applied.emit(envelope)


func _replay(generation: int) -> void:
	var path := "/visual/replay?%s&sinceTick=%d&sinceSequence=%d" % [
		_scope_query(), int(cursor.get("tick", 0)), int(cursor.get("sequence", 0))]
	var response: Dictionary = await transport.request("GET", path)
	if generation != _generation:
		return
	if response.status != 200 or not (response.body is Array):
		await _connect(generation) # replay indisponível: recomeça do snapshot
		return
	for envelope in response.body:
		if int(envelope.fromCursor.sequence) != int(cursor.get("sequence", 0)):
			await _connect(generation) # replay também com lacuna: snapshot novo
			return
		cursor = envelope.toCursor
		delta_applied.emit(envelope)


func _on_channel_closed(generation: int) -> void:
	if generation != _generation:
		return
	_channel = null
	var attempt := 0
	while generation == _generation:
		var delay: float = BACKOFF_SECONDS[min(attempt, BACKOFF_SECONDS.size() - 1)]
		attempt += 1
		_set_state("offline", delay)
		await sleeper.call(delay)
		if generation != _generation:
			return
		var path := "/visual/replay?%s&sinceTick=%d&sinceSequence=%d" % [
			_scope_query(), int(cursor.get("tick", 0)), int(cursor.get("sequence", 0))]
		var response: Dictionary = await transport.request("GET", path)
		if generation != _generation:
			return
		if response.status == 200 and response.body is Array:
			for envelope in response.body:
				if int(envelope.fromCursor.sequence) == int(cursor.get("sequence", 0)):
					cursor = envelope.toCursor
					delta_applied.emit(envelope)
			_open_channel(generation)
			_set_state("online")
			return
		if response.status == 403:
			state = "denied"
			access_denied.emit()
			return
