extends "res://tests/test_base.gd"

const ShapeValidator := preload("res://Contract/shape_validator.gd")
const CursorStream := preload("res://Contract/cursor_stream.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")
const ApiClient := preload("res://Contract/api_client.gd")


func test_rejects_missing_and_mistyped_fields() -> void:
	var shape = ShapeValidator.load_shape("simulation-status")
	check(shape != null, "contrato simulation-status existe em tests/contracts")
	var good := {}
	for key in shape:
		good[key] = _sample_for(shape[key])

	eq(ShapeValidator.validate(good, shape), [], "resposta fiel ao contrato é aceita")

	var key: String = shape.keys().filter(func(k): return not String(k).ends_with("?"))[0] # o primeiro obrigatório
	var missing := good.duplicate(true)
	missing.erase(key)
	check(not ShapeValidator.validate(missing, shape).is_empty(), "campo ausente é rejeitado")

	var wrong := good.duplicate(true)
	wrong[key] = [] if not (shape[key] is Array) else "texto"
	check(not ShapeValidator.validate(wrong, shape).is_empty(), "tipo diferente é rejeitado")

	# E no fluxo real: snapshot que não cumpre a forma não chega à store.
	var transport := FakeTransport.new()
	transport.on("GET /visual/subscribe", {"status": 200, "body": {"payload": {}}})
	var stream = CursorStream.new(transport, tree)
	var snapshots: Array = []
	var errors: Array = []
	stream.snapshot_received.connect(func(e): snapshots.append(e))
	stream.contract_error.connect(func(e): errors.append(e))
	await stream.subscribe("World")
	eq(snapshots.size(), 0, "snapshot fora do contrato é descartado")
	check(errors.size() == 1, "o erro de contrato é anunciado")


## #237: escopo que não existe (404) não é snapshot nem divergência de contrato: o stream avisa `scope_gone`, sem faixa laranja.
func test_stream_404_is_scope_gone_not_a_contract_error() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/subscribe", {"status": 404, "body": null})
	var stream = CursorStream.new(transport, tree)
	var snapshots: Array = []
	var errors: Array = []
	var gone: Array = []
	stream.snapshot_received.connect(func(e): snapshots.append(e))
	stream.contract_error.connect(func(e): errors.append(e))
	stream.scope_gone.connect(func(kind, ref): gone.append([kind, ref]))
	await stream.subscribe("Interior", "-1")
	eq(snapshots.size(), 0, "404 não vira snapshot")
	eq(errors.size(), 0, "404 não é divergência de contrato")
	eq(gone, [["Interior", "-1"]], "o escopo sumido é anunciado")
	eq(stream.state, "gone", "o stream para (sem tentar de novo para sempre)")


func test_stream_ignores_a_snapshot_message_with_null_payload() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/subscribe", {"status": 200, "body": Fx.snapshot()})
	var stream = CursorStream.new(transport, tree)
	stream.validate_shapes = false
	await stream.subscribe("World")
	stream.validate_shapes = true
	var snapshots: Array = []
	var errors: Array = []
	stream.snapshot_received.connect(func(e): snapshots.append(e))
	stream.contract_error.connect(func(e): errors.append(e))
	transport.channels[0].push({"scope": {"kind": 2, "refId": "9"}, "cursor": {"atMs": 1, "scopeKey": "x", "sequence": 1}, "payload": null})
	eq(snapshots.size(), 0, "snapshot sem projeção é ignorado")
	eq(errors.size(), 0, "e não aparece como fora do contrato")


func _sample_for(shape: Variant) -> Variant:
	if shape is Dictionary:
		var out := {}
		for k in shape:
			out[k] = _sample_for(shape[k])
		return out
	if shape is Array:
		return [] if shape.is_empty() else [_sample_for(shape[0])]
	match str(shape):
		"string": return "x"
		"number": return 1
		"boolean": return true
		"[]": return []
		_: return null


func test_api_client_rejects_mistyped_status() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": "sim", "ticksPerSecond": 1.0, "atMs": 1 * 3600000, "tickDeltaMs": 3600000, "year": 0}})
	var api := ApiClient.new(transport)
	var errors: Array = []
	api.contract_error.connect(func(_route, e): errors.append(e))

	var response: Dictionary = await api.clock_status()

	eq(response.ok, false, "campo de tipo errado (isPaused como texto) é rejeitado")
	eq(errors.size(), 1, "o erro de contrato é anunciado")

	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 1 * 3600000, "tickDeltaMs": 3600000, "year": 0}})
	var good: Dictionary = await api.clock_status()
	eq(good.ok, true, "resposta fiel ao contrato passa")


func test_stream_rejects_mistyped_snapshot() -> void:
	var transport := FakeTransport.new()
	var bad := Fx.snapshot(0, {"width": "trinta", "height": 30, "cities": [], "externalNpcs": [], "activeEvents": [],
		"layers": {}, "portals": [], "livingState": {}}, "World")
	transport.on("GET /visual/subscribe", {"status": 200, "body": bad})
	var stream = CursorStream.new(transport, tree)
	var snapshots: Array = []
	stream.snapshot_received.connect(func(e): snapshots.append(e))

	await stream.subscribe("World")

	eq(snapshots.size(), 0, "snapshot com campo de tipo errado é descartado")


func test_api_client_validates_npc_detail_shape() -> void:
	var shape = ShapeValidator.load_shape("npc-inspect")
	check(shape != null, "contrato npc-inspect existe")
	var good := {}
	for key in shape:
		good[key] = _sample_for(shape[key])
	var transport := FakeTransport.new()
	transport.on("GET /npcs/7", {"status": 200, "body": good})
	var api := ApiClient.new(transport)

	eq((await api.npc_detail(7)).ok, true, "resposta fiel ao contrato passa")

	var broken: Dictionary = good.duplicate(true)
	broken.erase("name")
	transport.on("GET /npcs/7", {"status": 200, "body": broken})
	eq((await api.npc_detail(7)).ok, false, "campo ausente é rejeitado")


func test_stream_discards_a_delta_that_breaks_the_contract() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/subscribe", {"status": 200, "body": Fx.snapshot()})
	transport.on("GET /visual/replay", {"status": 200, "body": []})
	var stream = CursorStream.new(transport, tree)
	stream.validate_shapes = false # o snapshot sintético não cumpre o contrato; só o delta é julgado
	await stream.subscribe("World")
	stream.validate_shapes = true
	var applied: Array = []
	var errors: Array = []
	stream.delta_applied.connect(func(e): applied.append(e))
	stream.contract_error.connect(func(e): errors.append(e))

	var shape = ShapeValidator.load_shape("visual-delta")
	var good := {}
	for key in shape:
		good[key] = _sample_for(shape[key])
	good.fromCursor.sequence = 0
	good.toCursor.sequence = 1
	transport.channels[0].push(good)
	eq(applied.size(), 1, "delta fiel ao contrato é aplicado")

	var broken: Dictionary = good.duplicate(true)
	broken.fromCursor.sequence = 1
	broken.toCursor.sequence = 2
	broken.payload.erase("npcUpserts")
	transport.channels[0].push(broken)
	eq(applied.size(), 1, "delta sem campo do contrato é descartado")
	eq(errors.size(), 1, "e o erro de contrato é anunciado")


func test_stream_validates_interior_deltas_and_accepts_null_action() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/subscribe", {"status": 200, "body": Fx.snapshot(0, null, "Interior")})
	transport.on("GET /visual/replay", {"status": 200, "body": []})
	var stream = CursorStream.new(transport, tree)
	stream.validate_shapes = false
	await stream.subscribe("Interior", "7")
	stream.validate_shapes = true
	var applied: Array = []
	var errors: Array = []
	stream.delta_applied.connect(func(e): applied.append(e))
	stream.contract_error.connect(func(e): errors.append(e))

	var shape = ShapeValidator.load_shape("interior-delta")
	var good := {}
	for key in shape:
		good[key] = _sample_for(shape[key])
	good.payload.entered[0].action = null # ação nula é legítima no motor (NPC sem ação)
	good.fromCursor.sequence = 0
	good.toCursor.sequence = 1
	transport.channels[0].push(good)
	eq(applied.size(), 1, "delta de interior com ação nula é aceito")

	var broken: Dictionary = good.duplicate(true)
	broken.fromCursor.sequence = 1
	broken.toCursor.sequence = 2
	broken.payload.erase("exited")
	transport.channels[0].push(broken)
	eq(applied.size(), 1, "delta de interior sem `exited` é descartado")
	eq(errors.size(), 1, "e o erro de contrato é anunciado")


func test_world_info_and_pause_status_shapes_are_frozen() -> void:
	var transport := FakeTransport.new()
	var scale := {"cellMeters": 1.5, "doorMeters": 2.0, "doorWidthMeters": 0.9, "floorMeters": 3.0, "windowMeters": 1.0, "windowSillMeters": 1.0}
	transport.on("GET /world/info", {"status": 200, "body": {"name": "Vale", "periodId": "", "technologyAxes": ["pace"], "scale": scale}})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": true, "ticksPerSecond": 1.0, "atMs": 3 * 3600000, "tickDeltaMs": 3600000, "year": 0, "pausedBy": "alan", "pauseReason": "x", "pausedAtMs": 3 * 3600000, "pauseMaxMs": 48 * 3600000}})
	var api := ApiClient.new(transport)
	eq((await api.world_info()).ok, true)
	eq((await api.clock_status()).ok, true, "pausa de admin preenchida passa")

	transport.on("GET /world/info", {"status": 200, "body": {"name": "Vale", "technologyAxes": ["pace"], "scale": scale}})
	eq((await api.world_info()).ok, false, "sem periodId reprova")
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": true, "ticksPerSecond": 1.0, "atMs": 3 * 3600000, "tickDeltaMs": 3600000, "year": 0, "pausedBy": 7}})
	eq((await api.clock_status()).ok, false, "pausedBy com tipo errado reprova (opcional tipado)")
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 3 * 3600000, "tickDeltaMs": 3600000, "year": 0, "pausedBy": null}})
	eq((await api.clock_status()).ok, true, "nulo passa")
