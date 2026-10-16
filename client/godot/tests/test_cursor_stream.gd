extends "res://tests/test_base.gd"

const CursorStream := preload("res://Contract/cursor_stream.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")


func _stream(transport) -> Array:
	var stream = CursorStream.new(transport, tree)
	stream.validate_shapes = false
	var delays: Array = []
	stream.sleeper = func(seconds: float): delays.append(seconds)
	return [stream, delays]


func _online(snapshot: Dictionary = Fx.snapshot()) -> Array:
	var transport := FakeTransport.new()
	transport.on("GET /visual/subscribe", {"status": 200, "body": snapshot})
	var pair := _stream(transport)
	return [transport, pair[0], pair[1]]


func test_snapshot_then_ordered_deltas() -> void:
	var setup := _online()
	var transport = setup[0]
	var stream = setup[1]
	var events: Array = []
	stream.snapshot_received.connect(func(_e): events.append("snapshot"))
	stream.delta_applied.connect(func(e): events.append(int(e.toCursor.sequence)))

	await stream.subscribe("World")
	transport.channels[0].push(Fx.delta(0, 1))
	transport.channels[0].push(Fx.delta(1, 2))

	eq(events, ["snapshot", 1, 2], "snapshot antes dos deltas, em ordem")
	eq(stream.state, "online")
	eq(int(stream.cursor.sequence), 2)


func test_gap_triggers_replay_and_discards_delta() -> void:
	var setup := _online()
	var transport = setup[0]
	var stream = setup[1]
	var applied: Array = []
	stream.delta_applied.connect(func(e): applied.append(e))
	transport.on("GET /visual/replay", {"status": 200, "body": [Fx.delta(0, 1), Fx.delta(1, 2)]})

	await stream.subscribe("World")
	transport.channels[0].push(Fx.delta(2, 3)) # lacuna: o cliente está em 0

	eq(transport.count_calls("GET /visual/replay"), 1, "uma chamada de replay")
	check(transport.calls.any(func(c): return "sinceSequence=0" in c and "sinceTick=10" in c), "replay parte do último cursor aplicado")
	eq(Fx.sequences(applied), [1, 2], "o delta fora de ordem não foi aplicado diretamente")
	eq(int(stream.cursor.sequence), 2)


func test_channel_drop_reconnects_and_replays() -> void:
	var setup := _online()
	var transport = setup[0]
	var stream = setup[1]
	var delays = setup[2]
	var applied: Array = []
	stream.delta_applied.connect(func(e): applied.append(e))
	transport.on("GET /visual/replay", {"status": 200, "body": [Fx.delta(1, 2)]})

	await stream.subscribe("World")
	transport.channels[0].push(Fx.delta(0, 1))
	transport.channels[0].drop()

	eq(transport.channels.size(), 2, "novo canal aberto após a queda")
	check(transport.calls.any(func(c): return c.begins_with("GET /visual/replay") and "sinceSequence=1" in c), "replay a partir do cursor 1")
	eq(Fx.sequences(applied), [1, 2])
	eq(stream.state, "online")
	eq(delays, [1.0], "uma espera antes de reconectar")


func test_403_is_access_denied_without_payload() -> void:
	var transport := FakeTransport.new()
	transport.on("GET /visual/subscribe", {"status": 403, "body": null})
	var stream = _stream(transport)[0]
	var denied: Array = []
	var snapshots: Array = []
	stream.access_denied.connect(func(): denied.append(true))
	stream.snapshot_received.connect(func(e): snapshots.append(e))

	await stream.subscribe("World")

	eq(denied.size(), 1)
	eq(snapshots.size(), 0, "nenhum payload chega à store")
	eq(transport.channels.size(), 0, "nenhum canal aberto")
	eq(stream.state, "denied")


func test_offline_backoff_is_capped() -> void:
	var transport := FakeTransport.new()
	var attempts: Array = []
	transport.on("GET /visual/subscribe", func(_path):
		attempts.append(1)
		if attempts.size() <= 6:
			return {"status": 0, "body": null}
		return {"status": 200, "body": Fx.snapshot()})
	var pair := _stream(transport)
	var stream = pair[0]
	var delays = pair[1]
	var states: Array = []
	stream.connection_changed.connect(func(s, _r): states.append(s))

	await stream.subscribe("World")

	eq(delays, [1.0, 2.0, 4.0, 8.0, 8.0, 8.0], "espera dobra e para em 8 s")
	eq(states[0], "offline")
	eq(states[states.size() - 1], "online")
