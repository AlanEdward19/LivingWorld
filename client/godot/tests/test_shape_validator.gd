extends "res://tests/test_base.gd"

const ShapeValidator := preload("res://Contract/shape_validator.gd")
const CursorStream := preload("res://Contract/cursor_stream.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const Fx := preload("res://tests/fixtures.gd")


func test_rejects_missing_and_mistyped_fields() -> void:
	var shape = ShapeValidator.load_shape("simulation-status")
	check(shape != null, "contrato simulation-status existe em tests/contracts")
	var good := {}
	for key in shape:
		good[key] = _sample_for(shape[key])

	eq(ShapeValidator.validate(good, shape), [], "resposta fiel ao contrato é aceita")

	var missing := good.duplicate(true)
	missing.erase(shape.keys()[0])
	check(not ShapeValidator.validate(missing, shape).is_empty(), "campo ausente é rejeitado")

	var wrong := good.duplicate(true)
	var key: String = shape.keys()[0]
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
