extends Node3D
## Eventos com lugar (`events[].location` do delta) viram um marcador no mapa: haste e bola na cor do tipo, que
## pulsa, vai encolhendo e some depois de LIFETIME segundos. Clicável pela seleção (`positions`). O aviso de texto
## continua (toasts); o marcador só diz ONDE aconteceu.

const Grid := preload("res://Scenes/grid.gd")

const LIFETIME := 30.0
const MAX_MARKERS := 40
const COLORS: Array[Color] = [Color("ff6b5a"), Color("ffd84a"), Color("6bd0ff"), Color("b38bff"), Color("7be08a")]

var positions: Dictionary = {} ## id local do evento -> Vector3 do topo do marcador
var _markers: Dictionary = {} ## id -> {node, born}
var _clock := 0.0
var _next_id := 1
var _pole: Mesh
var _ball: Mesh
var _materials: Dictionary = {}


func _init() -> void:
	var pole := CylinderMesh.new()
	pole.top_radius = 0.03
	pole.bottom_radius = 0.03
	pole.height = 1.6
	_pole = pole
	var ball := SphereMesh.new()
	ball.radius = 0.18
	ball.height = 0.36
	ball.radial_segments = 10
	ball.rings = 5
	_ball = ball


func _material(kind: int) -> StandardMaterial3D:
	var color: Color = COLORS[absi(kind) % COLORS.size()]
	if not _materials.has(color):
		var material := StandardMaterial3D.new()
		material.albedo_color = color
		material.emission_enabled = true
		material.emission = color
		material.emission_energy_multiplier = 0.8
		material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		_materials[color] = material
	return _materials[color]


## Põe um marcador e devolve o id (0 se o evento não tem lugar). Evento já lido (Parse.event) usa o id do servidor;
## o formato cru do delta ganha um id local.
func add(event: Dictionary) -> int:
	var cell: Vector2i
	var id: int
	if event.has("has_cell"):
		if not bool(event.has_cell):
			return 0
		cell = event.cell
		id = int(event.id)
		remove(id)
	else:
		if not event.get("location") is Dictionary:
			return 0
		cell = Vector2i(int(event.location.x), int(event.location.y))
		id = _next_id
		_next_id += 1
	var root := Node3D.new()
	root.name = "Event_%d" % id
	root.position = Grid.to_world(cell)
	var material := _material(int(event.get("kind", 0)))
	for spec in [[_pole, Vector3(0, 0.8, 0)], [_ball, Vector3(0, 1.75, 0)]]:
		var part := MeshInstance3D.new()
		part.mesh = spec[0]
		part.material_override = material
		part.position = spec[1]
		part.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		root.add_child(part)
	add_child(root)
	_markers[id] = {"node": root, "born": _clock}
	positions[id] = root.position + Vector3(0, 1.75, 0)
	while _markers.size() > MAX_MARKERS:
		var oldest: int = _markers.keys()[0]
		for key in _markers:
			if float(_markers[key].born) < float(_markers[oldest].born):
				oldest = key
		remove(oldest)
	return id


func remove(id: int) -> void:
	var marker = _markers.get(id)
	if marker == null:
		return
	if marker.node.is_inside_tree():
		marker.node.queue_free()
	else:
		marker.node.free()
	_markers.erase(id)
	positions.erase(id)


func clear() -> void:
	for id in _markers.keys():
		remove(id)


func count() -> int:
	return _markers.size()


func advance(delta: float) -> void:
	if _markers.is_empty():
		return
	_clock += delta
	for id in _markers.keys():
		var marker: Dictionary = _markers[id]
		var age: float = _clock - float(marker.born)
		if age >= LIFETIME:
			remove(id)
			continue
		var fade := 1.0 - maxf(0.0, age - LIFETIME * 0.6) / (LIFETIME * 0.4)
		marker.node.scale = Vector3.ONE * maxf(fade, 0.05) * (1.0 + 0.08 * sin(age * 4.0))
