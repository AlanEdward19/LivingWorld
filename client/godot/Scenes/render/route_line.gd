extends Node3D
## Linha discreta no chão com o que falta da rota (AD-181) de quem está selecionado ou sendo seguido: do ponto em que o
## boneco está desenhado até a chegada, pelas células que o motor mandou. Um MultiMesh de traços finos (material
## compartilhado, sem sombra), refeito poucas vezes por segundo e só com alguém andando.
## AD-188: da pessoa selecionada sai também um tracejado até o alvo da ação ou da intenção que o motor diz (`actionTarget`,
## `intentTarget` "tipo:id"), com um anel no alvo; sem alvo do motor, nada.

const REFRESH := 0.2
const Y := 0.05
const COLOR := Color(1.0, 0.97, 0.82, 0.5)

const DASH := 0.35 ## passo do tracejado até o alvo (traço + vão)
const MAX_DASHES := 60

var drawn_for: Array = [] ## ids cuja rota está desenhada agora (testável)
var target_drawn := Vector3.INF ## alvo tracejado agora (testável)
var target_of: Callable = Callable() ## id -> Vector3 do alvo (INF = sem alvo); o world_view liga ao npc_renderer
var segments := 0 ## testável: quantos traços
var _instance: MultiMeshInstance3D
var _clock := 0.0


func _init() -> void:
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	var mesh := BoxMesh.new()
	mesh.size = Vector3.ONE
	multi.mesh = mesh
	_instance = MultiMeshInstance3D.new()
	_instance.name = "RouteLine"
	_instance.multimesh = multi
	_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := StandardMaterial3D.new()
	material.albedo_color = COLOR
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_instance.material_override = material
	_instance.visible = false
	add_child(_instance)


## A cada quadro; redesenha a cada REFRESH segundos (ou já, com `now`).
func step(delta: float, ids: Array, nodes: Dictionary, now := false) -> void:
	_clock += delta
	if not now and _clock < REFRESH:
		return
	_clock = 0.0
	rebuild(ids, nodes)


func rebuild(ids: Array, nodes: Dictionary) -> void:
	drawn_for = []
	var transforms: Array = []
	_target(transforms, ids[0] if not ids.is_empty() else -1, nodes)
	for id in ids:
		var actor = nodes.get(id)
		if actor == null or actor.walk == null or actor.walk.done or not actor.visible or drawn_for.has(id):
			continue
		var points: PackedVector3Array = actor.walk.remaining(actor.position)
		for i in points.size() - 1:
			var a := Vector3(points[i].x, Y, points[i].z)
			var b := Vector3(points[i + 1].x, Y, points[i + 1].z)
			var length := a.distance_to(b)
			if length < 0.02:
				continue
			var basis := Basis.looking_at((b - a) / length, Vector3.UP) * Basis.from_scale(Vector3(0.06, 0.01, length))
			transforms.append(Transform3D(basis, (a + b) / 2.0))
		var end: Vector3 = points[points.size() - 1]
		transforms.append(Transform3D(Basis.from_scale(Vector3(0.22, 0.012, 0.22)), Vector3(end.x, Y, end.z))) # chegada
		drawn_for.append(id)
	var multi := _instance.multimesh
	if multi.instance_count != transforms.size():
		multi.instance_count = transforms.size()
	for i in transforms.size():
		multi.set_instance_transform(i, transforms[i])
	segments = transforms.size()
	_instance.visible = segments > 0


## Tracejado do selecionado até o alvo que o motor diz, e anel no alvo.
func _target(transforms: Array, id: int, nodes: Dictionary) -> void:
	target_drawn = Vector3.INF
	var actor = nodes.get(id)
	if actor == null or not target_of.is_valid() or not actor.visible:
		return
	var point: Vector3 = target_of.call(id)
	if point == Vector3.INF:
		return
	var a := Vector3(actor.position.x, Y, actor.position.z)
	var b := Vector3(point.x, Y, point.z)
	var length := a.distance_to(b)
	if length < 0.3:
		return
	var dir := (b - a) / length
	var basis := Basis.looking_at(dir, Vector3.UP) * Basis.from_scale(Vector3(0.05, 0.01, DASH * 0.5))
	for i in mini(int(length / DASH), MAX_DASHES):
		transforms.append(Transform3D(basis, a + dir * (DASH * (i + 0.25))))
	for k in 4: # anel (quadrado) no alvo
		var side := Vector3(cos(k * PI / 2.0), 0, sin(k * PI / 2.0)) * 0.3
		transforms.append(Transform3D(Basis.looking_at(Vector3(-side.z, 0, side.x).normalized(), Vector3.UP) * Basis.from_scale(Vector3(0.05, 0.012, 0.6)), b + side))
	target_drawn = point
