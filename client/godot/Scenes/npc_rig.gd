extends RefCounted
## #230: esqueleto articulado do NpcActor, só de perto (LOD). Longe nada disto existe: o ator segue com tronco, cabeça e quatro
## caixas, o caminho barato de sempre. Perto (câmera próxima, o corpo do jogador, a prévia do criador) o rig liga: cotovelos, joelhos,
## pés, mãos, quadril, cinto, nariz e orelhas; camisa, calça e sapato em camadas; passada contínua andar→correr→parar (peso suave, fase
## contínua, passo pela velocidade real) e IK de pé no chão (degrau) e de mão no objeto (gestos), com a parte de cima independente das pernas.
## Pivôs `Node3D` (sem Skeleton3D: a hierarquia de 4 membros já existia e um esqueleto custaria mais para o mesmo resultado).

const Pose := preload("res://Scenes/body_pose.gd")
const NpcMesh := preload("res://Scenes/npc_mesh.gd")
const SHOE := Color("4a3524")
const BELT := Color("3a2a1c")
const LEG_X := 0.08

var active := false
var built := false
var gait := 0.0 ## peso da passada (0 parado .. 1 andando); sobe e desce suave
var run := 0.0 ## peso extra de corrida (0 anda .. 1 corre)
var phase := 0.0 ## fase contínua do ciclo (rad): nunca reinicia, a frequência é que muda
var speed := 0.0 ## unidades/s medidas do movimento desenhado (suavizadas)
var _last := Vector3.INF
var _uppers: Array[MeshInstance3D] = []
var _elbows: Array[Node3D] = []
var _hands: Array[Node3D] = []
var _knees: Array[Node3D] = []
var _ankles: Array[Node3D] = []
var _skin_parts: Array[MeshInstance3D] = []
var _forearms: Array[MeshInstance3D] = []
var _shins: Array[MeshInstance3D] = []
var _feet: Array[MeshInstance3D] = []
var _sleeve_long := false
var _pelvis: MeshInstance3D
var _belt: MeshInstance3D
var _hem: MeshInstance3D
var _extras: Array[Node3D] = []
# camadas do quadro (zeradas a cada pose): o gesto e o pulo pedem, a finalização aplica
var _drop := 0.0 ## quanto o quadril desce (agachar, passada, gesto)
var _foot_dy: Array[float] = [0.0, 0.0]
var _foot_dz: Array[float] = [0.0, 0.0]
var _ik_w: Array[float] = [0.0, 0.0]
var _ik_target: Array = [null, null]
var _air := 0.0
var _tuck := 0.0
var _stride := 0.3
var _lift := 0.05
var _duty := 0.6


## ---- ciclo de vida ----------------------------------------------------------------------------------------------------

func activate(a) -> void:
	if not built:
		_build(a)
	active = true
	_last = Vector3.INF
	for i in 2:
		var upper := _uppers[i]
		upper.scale.y = Pose.UPPER_ARM / 0.34
		upper.position.y = -Pose.UPPER_ARM / 2.0
		var leg_upper := _uppers[2 + i]
		leg_upper.scale.y = Pose.THIGH / 0.36
		leg_upper.position.y = -Pose.THIGH / 2.0
		_elbows[i].visible = true
		_knees[i].visible = true
	for node in _extras:
		node.visible = true
	_attach_props(a, true)
	refit(a)


func deactivate(a, free_nodes: bool) -> void:
	if not built:
		active = false
		return
	active = false
	_attach_props(a, false)
	for i in 2:
		_uppers[i].scale.y = 1.0
		_uppers[i].position.y = -0.17
		_uppers[2 + i].scale.y = 1.0
		_uppers[2 + i].position.y = -0.18
	a._head_mult = Vector3.ONE
	a._apply_head_scale()
	if free_nodes:
		for node in _extras:
			if is_instance_valid(node):
				node.free()
		_extras.clear()
		_uppers.clear()
		_elbows.clear()
		_hands.clear()
		_knees.clear()
		_ankles.clear()
		_skin_parts.clear()
		_forearms.clear()
		_shins.clear()
		_feet.clear()
		_pelvis = null
		_belt = null
		_hem = null
		built = false
	else:
		for node in _extras:
			node.visible = false


## Pele da pessoa nas partes que a mostram (antebraço, mão); a doença escurece junto com a cabeça. Manga comprida cobre o antebraço.
func set_skin(material: Material) -> void:
	for part in _skin_parts:
		part.material_override = material
	for part in _forearms:
		part.material_override = _forearm_sleeve if _sleeve_long else material


var _forearm_sleeve: Material = null


## Roupa e proporções pelo estado (ADR-0047) e pelo estilo do período (#254): túnica/vestido/saia, cinto, manga, calça, sapato e a
## espessura dos membros pelo sexo e idade; cabeça por idade.
func refit(a) -> void:
	if not built:
		return
	var stage := String(a.spec.get("stage", "adult"))
	var head := Pose.head_factor(stage)
	a._head_mult = Vector3.ONE * head
	a._apply_head_scale()
	var style: Dictionary = a._style
	var skirt: bool = bool(style.get("skirt", bool(a.spec.get("female", false)))) and stage != "baby"
	_hem.visible = active and skirt
	if skirt:
		var dress := String(style.get("outfit", "tunic")) == "dress"
		_hem.mesh = NpcMesh.skirt(0.22 if dress else (0.19 if stage != "child" else 0.16), 0.07 if dress else 0.05)
	_belt.visible = active and bool(style.get("belt", true))
	_belt.scale = Vector3(a._torso.scale.x, 1.0, a._torso.scale.z * 0.78)
	_sleeve_long = bool(style.get("long_sleeves", false))
	_forearm_sleeve = a._shirt
	var pants: Material = a._shared_mat(a._pants_color)
	_pelvis.material_override = pants
	var girth: float = a._arms[0].get_child(0).scale.x
	for i in 2:
		_shins[i].material_override = pants
		_shins[i].scale = Vector3(girth, 1.0, girth)
		_forearms[i].scale = Vector3(girth, 1.0, girth)
		var boot := bool(style.get("boot", true))
		_feet[i].mesh = NpcMesh.shoe(boot)
		_feet[i].material_override = a._shared_mat(Color(style.get("shoe", SHOE)))
	set_skin(a._skin_mesh.material_override)


func reset_joints() -> void:
	if not built:
		return
	for i in 2:
		_elbows[i].basis = Basis.IDENTITY
		_knees[i].basis = Basis.IDENTITY
		_ankles[i].basis = Basis.IDENTITY
		_ik_w[i] = 0.0
		_ik_target[i] = null
		_foot_dy[i] = 0.0
		_foot_dz[i] = 0.0
	_pelvis.rotation = Vector3.ZERO
	_drop = 0.0
	_air = 0.0
	_tuck = 0.0


func _build(a) -> void:
	var skin_mat: Material = a._skin_mesh.material_override
	var pants: Material = a._shared_mat(a._pants_color)
	var fore_mesh: Mesh = NpcMesh.limb(Pose.FOREARM, 0.042, 0.03)
	var shin_mesh: Mesh = NpcMesh.limb(Pose.SHIN, 0.05, 0.034)
	for i in 2:
		var arm: Node3D = a._arms[i]
		_uppers.append(arm.get_child(0))
		var elbow := Node3D.new()
		elbow.position = Vector3(0, -Pose.UPPER_ARM, 0)
		arm.add_child(elbow)
		_elbows.append(elbow)
		_extras.append(elbow)
		_forearms.append(_part(a, elbow, fore_mesh, skin_mat, Vector3(0, -Pose.FOREARM / 2.0, 0), false))
		var hand := Node3D.new()
		hand.position = Vector3(0, -Pose.FOREARM, 0)
		elbow.add_child(hand)
		_hands.append(hand)
		_part(a, hand, NpcMesh.hand(), skin_mat, Vector3.ZERO, true)
		_extras.append(_part(a, arm, NpcMesh.shoulder(), a._shirt, Vector3.ZERO, false))
	for i in 2:
		var leg: Node3D = a._legs[i]
		_uppers.append(leg.get_child(0))
		var knee := Node3D.new()
		knee.position = Vector3(0, -Pose.THIGH, 0)
		leg.add_child(knee)
		_knees.append(knee)
		_extras.append(knee)
		_shins.append(_part(a, knee, shin_mesh, pants, Vector3(0, -Pose.SHIN / 2.0, 0), false))
		var ankle := Node3D.new()
		ankle.position = Vector3(0, -Pose.SHIN, 0)
		knee.add_child(ankle)
		_ankles.append(ankle)
		_feet.append(_part(a, ankle, NpcMesh.shoe(true), a._shared_mat(SHOE), Vector3.ZERO, false))
	_pelvis = _part(a, a, NpcMesh.pelvis(), pants, Vector3(0, Pose.HIP_Y, 0), false)
	_belt = _part(a, a._body, a._shared_mesh("rig_belt", func():
		var m := CylinderMesh.new()
		m.top_radius = 0.156
		m.bottom_radius = 0.156
		m.height = 0.03
		m.radial_segments = 12
		return m), a._shared_mat(BELT), Vector3(0, 0.43, 0), false)
	_hem = _part(a, a._body, NpcMesh.skirt(0.19, 0.05), a._shirt, Vector3(0, 0.43, 0), false)
	_hem.visible = false
	_extras.append_array([_pelvis, _belt, _hem])
	built = true


func _part(a, parent: Node3D, mesh: Mesh, material: Material, at: Vector3, is_skin: bool) -> MeshInstance3D:
	var node: MeshInstance3D = a._part(parent, mesh, material, at)
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	if is_skin:
		_skin_parts.append(node)
	return node


## Ferramenta e recipiente seguem a MÃO (não o ombro) quando o rig está ligado: dobrar o cotovelo leva a ferramenta junto.
func _attach_props(a, to_hands: bool) -> void:
	if a._tool != null:
		_move(a._tool, _hands[1] if to_hands else a._arms[1], Vector3(0, Pose.UPPER_ARM + Pose.FOREARM, 0) if to_hands else Vector3.ZERO)
	if a._held != null:
		_move(a._held, _hands[0] if to_hands else a._arms[0], Vector3(0, -0.09, 0.06) if to_hands else Vector3(0, -0.4, 0.06))


func attach_new_prop(node: Node3D, left: bool) -> void:
	if active:
		_move(node, _hands[0 if left else 1], Vector3(0, -0.09, 0.06) if left else Vector3(0, Pose.UPPER_ARM + Pose.FOREARM, 0))


func _move(node: Node3D, parent: Node3D, at: Vector3) -> void:
	if node.get_parent() != null:
		node.get_parent().remove_child(node)
	parent.add_child(node)
	node.position = at


## ---- passada (parte de baixo) e parte de cima ----------------------------------------------------------------------

## Mede a velocidade desenhada e avança pesos e fase. Chamada a cada pose, com `dt` o tempo acumulado desde a anterior.
func locomotion(a, dt: float, moving: bool) -> void:
	if dt > 0.0001:
		var v := 0.0
		if _last != Vector3.INF:
			v = minf(a.position.distance_to(_last) / dt, 3.0)
		speed = Pose.approach(speed, v if moving else 0.0, 8.0, dt)
		gait = Pose.approach(gait, 1.0 if moving else 0.0, 10.0 if moving else 7.0, dt)
		run = Pose.approach(run, 1.0 if (moving and a.running) else 0.0, 5.0, dt)
	_last = a.position
	var stage := String(a.spec.get("stage", "adult"))
	var factor := Pose.stride_factor(stage)
	_stride = lerpf(0.30, 0.46, run) * factor
	_lift = lerpf(0.05, 0.11, run)
	_duty = lerpf(0.6, 0.4, run)
	if gait > 0.004:
		var f := clampf(speed * _duty / _stride, lerpf(1.1, 2.0, run), 3.4)
		phase = fmod(phase + TAU * f * maxf(dt, 0.0), TAU * 64.0)
	else:
		gait = 0.0
	if stage == "elder":
		a._body.rotation.x += 0.08
	if gait <= 0.0:
		return
	var amp := lerpf(0.5, 0.95, run)
	for i in 2:
		var other := 1 - i # o braço vai à frente com a perna do outro lado
		var u := fposmod(phase / TAU + (0.0 if other == 0 else 0.5), 1.0)
		var zn := Pose.foot_path(u, _stride, 0.0, _duty).x / (_stride * 0.5)
		a._arms[i].rotation.x = lerpf(a._arms[i].rotation.x, -amp * zn, gait)
		a._arms[i].rotation.z = lerpf(a._arms[i].rotation.z, 0.0, gait * 0.5)
	var lean := lerpf(0.05, 0.26, run) * gait
	a._body.rotation.x += lean
	var twist := 0.12 * sin(phase) * gait * (1.0 + run)
	a._body.rotation.y += twist
	a._body.rotation.z += 0.045 * sin(phase) * gait * (1.0 + 0.5 * run) # balanço dos ombros contra o quadril
	_pelvis.rotation.z = -0.07 * sin(phase) * gait
	_pelvis.rotation.y = -twist * 0.6
	a._head.rotation.y -= twist * 0.6
	a._head.rotation.x -= lean * 0.6
	_drop += (lerpf(0.025, 0.06, run) + 0.012 * (1.0 + run) * 0.5 * (1.0 + cos(2.0 * phase))) * gait


func air(lift: float, tuck: float) -> void:
	_air = lift
	_tuck = tuck


## Gesto por cima da pose, com envelope de platô: a pose do FK antigo (amplitude cheia) entra e sai misturada com o que o corpo já
## fazia (a caminhada continua nas pernas); as pernas e as mãos vêm do IK (agachar, apoiar, alcançar o objeto).
func gesture(a) -> void:
	var t: float = (a._clock - a._gesture_from) / a._gesture_len
	var w := Pose.loop_weight(a._clock - a._gesture_from, a._gesture_len) if a._gesture_loop else Pose.plateau(t)
	var nodes: Array[Node3D] = [a._arms[0], a._arms[1], a._body, a._head]
	var rot: Array[Vector3] = []
	var pos: Array[Vector3] = []
	for node in nodes:
		rot.append(node.rotation)
		pos.append(node.position)
	a._apply_gesture(1.0)
	for i in nodes.size():
		nodes[i].rotation = rot[i].lerp(nodes[i].rotation, w)
		nodes[i].position = pos[i].lerp(nodes[i].position, w)
	var kind: String = a._gesture
	var aim: Variant = a._gesture_target if a._gesture_target != Vector3.INF else null
	match kind:
		"reach": # agacha de verdade: quadril desce e as mãos descem ao alvo (chão/prateleira)
			_drop += 0.17 * w
			a._body.rotation.x += 0.3 * w
			var p: Vector3 = aim if aim != null else Vector3(0, 0.12, 0.36)
			_aim(0, p + Vector3(-0.07, 0, 0), w)
			_aim(1, p + Vector3(0.07, 0, 0), w)
		"give":
			_aim(1, aim if aim != null else Vector3(0.12, 0.80, 0.46), w)
		"drink", "eat":
			_aim(1, Vector3(0.035, 0.93, 0.115), w)
		"push":
			_foot_dz[1] += 0.07 * w
			var p: Vector3 = aim if aim != null else Vector3(0, 0.86, 0.42)
			_aim(0, p + Vector3(-0.12, 0, 0), w)
			_aim(1, p + Vector3(0.12, 0, 0), w)
		"work":
			var p: Vector3 = aim if aim != null else Vector3(0.1, 0.62, 0.42)
			_aim(1, p + Vector3(0, 0.2 * sin(t * TAU * 2.0), 0), w)
			_aim(0, p + Vector3(-0.16, -0.02, -0.04), w * 0.8)
		"climb":
			for i in 2:
				var s := sin(t * TAU * 2.0) * (1.0 if i == 0 else -1.0)
				_foot_dy[i] += 0.14 * maxf(0.0, s) * w
				_foot_dz[i] += 0.08 * maxf(0.0, s) * w
				_aim(i, Vector3((-1 if i == 0 else 1) * 0.15, 0.95 + 0.09 * s * (1.0 if i == 0 else 1.0), 0.3), w * 0.9)
		"rest":
			_drop += 0.12 * w
		"chop": # #241: machado em laço: as mãos juntas sobem atrás da cabeça e descem num golpe à frente, o quadril acompanha
			var swing := Pose.chop_swing(a._clock / Pose.CHOP_PERIOD)
			var p := Vector3(0, lerpf(0.5, 1.28, swing), lerpf(0.46, -0.06, swing))
			_drop += 0.05 * (1.0 - swing) * w
			a._body.rotation.x += lerpf(0.3, -0.12, swing) * w
			_aim(0, p + Vector3(-0.05, 0, 0), w)
			_aim(1, p + Vector3(0.05, 0, 0), w)


func _aim(i: int, target: Vector3, weight: float) -> void:
	_ik_target[i] = target
	_ik_w[i] = weight


## ---- finalização: tudo que depende do FK já escrito (quadril, pivô da coluna, pernas por IK, braços por IK, cotovelo) ----------

func finalize(a, lying: bool) -> void:
	if lying:
		_drop = 0.0
	_pelvis.visible = not lying
	a._body.position.y -= _drop
	var hip_y: float = Pose.HIP_Y + a._body.position.y
	# a coluna gira em torno do quadril, não do chão: a inclinação (passada, reverência, agachar) mantém o tronco sobre as pernas
	if not lying:
		var lean := Basis(Vector3.RIGHT, a._body.rotation.x)
		var pivot := Vector3(0, Pose.HIP_Y, 0)
		a._body.position += pivot - lean * pivot
	_pelvis.position.y = hip_y
	_hem.position.y = 0.43
	_finalize_arms(a)
	if lying:
		return
	var g := gait
	for i in 2:
		var u := fposmod(phase / TAU + (0.0 if i == 0 else 0.5), 1.0)
		var fp := Pose.foot_path(u, _stride, _lift, _duty)
		var tz := fp.x * g + _foot_dz[i]
		var ground: float = a.foot_ground(tz) * g
		var y := Pose.ANKLE_H + ground + fp.y * g + _foot_dy[i] + _air * 0.9 + _tuck * 0.10
		a._legs[i].position = Vector3((-1 if i == 0 else 1) * LEG_X, hip_y + _air, 0)
		var sol := Pose.solve_limb(Vector3(0, y - (hip_y + _air), tz), Pose.THIGH, Pose.SHIN, Vector3(0, 0, 1))
		a._legs[i].basis = sol.basis
		_knees[i].basis = Basis(Vector3.RIGHT, float(sol.flex))
		# pé chato no plano do chão (a sola acompanha o terreno, sem herdar a inclinação da perna); dedão sobe um pouco no balanço
		var foot_pitch := -0.35 * (fp.y / _lift) * g if _lift > 0.0 else 0.0
		_ankles[i].basis = (sol.basis * _knees[i].basis).inverse() * Basis(Vector3.RIGHT, foot_pitch)


func _finalize_arms(a) -> void:
	var inverse: Transform3D = a._body.transform.affine_inverse()
	for i in 2:
		var arm: Node3D = a._arms[i]
		var rx := -arm.rotation.x
		var flex := clampf(0.10 + 0.32 * maxf(0.0, rx), 0.0, 2.3) + 0.9 * run * gait
		var weight: float = _ik_w[i]
		if weight > 0.001 and _ik_target[i] != null:
			var side := -1.0 if i == 0 else 1.0
			var local: Vector3 = inverse * Vector3(_ik_target[i])
			var sol := Pose.solve_limb(local - arm.position, Pose.UPPER_ARM, Pose.FOREARM + 0.03, Vector3(side * 0.55, -0.7, -0.45))
			arm.basis = Pose.blend_basis(arm.basis, sol.basis, weight)
			flex = lerpf(flex, float(sol.flex), weight)
		_elbows[i].basis = Basis(Vector3.RIGHT, flex)
