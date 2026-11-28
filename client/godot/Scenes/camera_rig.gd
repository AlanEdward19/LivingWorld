extends Node3D
## Câmera do espectador, componente trocável: isométrica ortográfica para espectador e admin; com personagem encarnado,
## perspectiva em 3ª pessoa (padrão) ou 1ª pessoa (tecla C), o mouse olha (AD-197d, ADR-0049/0051). Mesma interface
## (pan/orbit/zoom_by/follow/release); `look` e `heading` só valem nos modos do jogador.
## NÃO conhece transporte: navegar nunca fala com o servidor. Só pede troca de escopo por sinal.

signal scope_requested(kind: String, ref_id: String)
signal mode_changed(mode: String) ## AD-197j: o app mostra a mira no centro e solta o cursor ao voltar à isométrica
signal see_through(ids: Dictionary) ## AD-197p: prédios entre a câmera da 3ª pessoa e o corpo (o app os deixa translúcidos)
signal opened(ids: Dictionary) ## AD-197s: prédios em que o corpo está (o telhado some de vez; a câmera olha de cima)

const Grid := preload("res://Scenes/grid.gd")
const InteriorCamera := preload("res://Scenes/interior_camera.gd")

const ISO_PITCH := deg_to_rad(-35.264)
const ISO := "iso"
const THIRD := "third"
const FIRST := "first"
const THIRD_DISTANCE_M := 4.5 ## metros atrás do personagem (AD-197r; AD-197s3: em metros do motor, a escala do mundo muda)
## AD-197r: a altura dos olhos sai do corpo (a altura em cm que o motor dá vira a escala do boneco), não de constante:
## olhos a 94% da altura, na escala do motor (adulto mediano de 1,70 m -> 1,6 m -> ~1,07 unidade; criança fica mais baixa).
const ADULT_HEIGHT_CM := 170.0
const EYE_RATIO := 0.94
const SHOULDER_M := 0.5 ## a 3ª pessoa mira acima do ombro (olho + isto, metros do corpo): o corpo cai no terço de baixo da tela e o horizonte aparece
const MIN_PITCH := deg_to_rad(-70.0)
const MAX_PITCH := deg_to_rad(35.0)
const LOOK_SPEED := 0.004 ## rad por pixel do mouse
const NEAR_THIRD_M := 0.075
const NEAR_FIRST_M := 0.15 ## AD-197s: ~ raio da cabeça: nada mais perto que isto atravessa o olho
const EYE_CLEAR_M := 0.18 ## e o olho não chega mais perto que isto de uma parede por fora
const AWAIT_LIFT_M := 12.0 ## sem corpo para seguir (a pessoa nova chega no próximo delta) a 3ª pessoa fica no alto, nunca dentro de um prédio
const ROOM_ARM_M := 1.0 ## AD-197s2: dentro de casa a câmera fica no cômodo se couber isto atrás do corpo; senão sobe acima do telhado. #222: 1 m (de ombro, como em RPG); com 2 m quase nenhum cômodo cabia e a câmera ia a prumo
const NEAR_PERSON_M := 1.0 ## na 1ª pessoa quem está a menos disto do olho não se desenha (o espaçamento mínimo entre corpos parados é ~0,9 m)
const CEILING_M := 0.2 ## folga da câmera até o teto do andar
const INDOOR_SHOULDER := 0.3 ## #227: em casa a câmera fica na altura da cabeça (fração do ombro da 3ª pessoa de rua), não sobre o ombro
const INDOOR_ARM_OMEGA := 4.0 ## #250: o braço de dentro de casa (entrar, sair, girar) muda suave nos dois sentidos, sem salto
const INDOOR_ARM_SPEED := 5.0 ## #268: unidades/s: o enquadramento do cômodo muda sem arranco, mesmo ao passar de um cômodo pequeno a um grande
const INDOOR_TRANSITION_S := 0.8 ## depois de entrar ou sair, o braço segue a mola (não o encurtar rápido de quem bate na parede)
const INDOOR_FOV_BONUS := 12.0 ## graus a mais em cômodo apertado (a vista abre sem recuar a câmera)
const INDOOR_OMEGA := 3.5 ## rad/s: altura, FOV e inclinação de dentro de casa mudam suave (~0,8 s, sem salto)
## Distâncias em metros do motor viram unidades da grade (a escala do mundo muda): nada fixo na escala de hoje.
static func third_distance() -> float: return Grid.units(THIRD_DISTANCE_M)
static func near_third() -> float: return Grid.units(NEAR_THIRD_M)
static func min_arm() -> float: return Grid.units(MIN_ARM_M)
static func arm_margin() -> float: return Grid.units(ARM_MARGIN_M)
static func near_first() -> float: return Grid.units(NEAR_FIRST_M)
static func eye_clear() -> float: return Grid.units(EYE_CLEAR_M)
static func await_lift() -> float: return Grid.units(AWAIT_LIFT_M)
static func room_arm() -> float: return Grid.units(ROOM_ARM_M)
static func near_person() -> float: return Grid.units(NEAR_PERSON_M)
const MAX_DOWN := deg_to_rad(-83.0) ## AD-197s: olhando o corpo de cima, a câmera nunca passa da vertical

var mode := ISO
const BUILD_PITCH := deg_to_rad(-64.0) ## #264: a vista do modo comprar: de cima, ainda um pouco de lado para ver a altura dos móveis
var build_view := false ## #264: o modo comprar toma a câmera (o corpo não a segue enquanto ele está aberto)
var build_center := Vector3.ZERO
var build_size := 12.0
var build_yaw := 0.0
var build_pitch := BUILD_PITCH ## #266: só captura (passo `buylook`): a vista de comprar mais de lado para ver a altura dos móveis
var iso_pitch := ISO_PITCH ## #264: o modo comprar olha a casa de cima (mais a prumo que a isométrica); fora dele, a de sempre
var pitch := deg_to_rad(-15.0) ## olhar do jogador (só 1ª/3ª pessoa)

var target := Vector3(15, 0, 15)
var yaw := deg_to_rad(45.0)
var zoom_size := 22.0
var min_size := 3.0
var max_size := 48.0
var bubble_size := 29.0 ## acima disso, no mapa-múndi, NPC vira bolha
var city_enter_size := 9.0
var city_exit_size := 13.0
var scope_kind := "World"
## Injetada pelo app: ponto do mundo -> id da cidade sob ele ("" se nenhuma).
var city_at: Callable = func(_point: Vector3) -> String: return ""

## AD-197p: braço de mola da 3ª pessoa. `blockers` devolve os nós desenhados com a caixa (`meta box`) de cada prédio, qualquer que seja
## o tamanho; a câmera chega perto do corpo quando a parede ou o telhado ficaria no meio e volta devagar quando o caminho abre.
const MIN_ARM_M := 0.75
const ARM_MARGIN_M := 0.375
const ARM_RETURN := 3.0 ## por segundo: a volta é suave; a aproximação, imediata (nunca atravessa a parede)
var blockers: Callable = Callable()
var arm := 3.0 ## inicial; vira third_distance() ao entrar no modo
var third_scale := 1.0 ## #222: + e − (ou a rodinha) aproximam e afastam a 3ª pessoa: 0,4 a 1,8 do braço padrão
const THIRD_SCALE_MIN := 0.4
const THIRD_SCALE_MAX := 1.8
## #231: câmera de jogo. O braço, o seguir do corpo e o olhar são molas críticas (solução exata: o mesmo resultado em qualquer FPS).
const FOLLOW_OMEGA := 22.0 ## rad/s: segue o corpo com inércia curta (sem tremer, sem arrastar)
const AIM_OMEGA := 55.0 ## rad/s: olhar suavizado com atraso < 0,1 s (a mira não fica para trás)
const OTS_OMEGA := 8.0 ## rad/s: troca de ombro e retorno do ombro
const ARM_SHORTEN := 30.0 ## por segundo: ao bater, o braço encurta rápido (e nunca passa de `free + arm_margin()/2`: não atravessa)
const ARM_HOLD_S := 0.4 ## s que o braço espera depois de bater antes de voltar (evita pulsar rente à quina)
const OTS_M := 0.45 ## mira por cima do ombro: deslocamento lateral da câmera, metros do corpo
const BASE_FOV := 70.0
const RUN_FOV_BONUS := 5.0 ## graus a mais correndo
const BLEND_S := 0.3 ## transição 3ª↔1ª (C)
const REVEAL_S := 0.9 ## chegada em outro lugar (cidade↔mundo): a câmera desce de uma vista recuada
var ots_side := 1.0 ## +1 ombro direito, -1 esquerdo (tecla Q)
var ots_m := OTS_M ## 0 desliga a mira por cima do ombro
var view_yaw := deg_to_rad(45.0) ## o que a câmera mostra (suavizado); `yaw` e `pitch` são a mira pedida
var view_pitch := deg_to_rad(-15.0)
var view_target := Vector3(15, 0, 15)
var _v_yaw := 0.0
var _v_pitch := 0.0
var _v_target := Vector3.ZERO
var _ots := 0.0
var _v_ots := 0.0
var _arm_hold := 0.0
var _run_fov := 0.0
var _blend_t := 1.0
var _blend_dur := BLEND_S
var _blend_from := Transform3D.IDENTITY
var _blend_from_fov := BASE_FOV
var _reveal := false
var _last_xf := Transform3D.IDENTITY
var _has_xf := false
var _tracked := false ## já seguiu um corpo em quadros reais (só então a chegada em outro lugar é animada)
var lift := 0.0 ## AD-197r: quanto a 3ª pessoa sobe (olhando o corpo de cima) quando o caminho para trás está fechado
var shoulder := 0.0 ## e quanto passa a ombro (deslocamento lateral), em unidades
var _see_through: Dictionary = {}
var _opened: Dictionary = {}
var _indoor := false ## #227: o enquadramento é o da casa em foco (câmera de ombro baixa, olhar limitado, FOV aberto)
var _indoor_k := 1.0 ## 1 = ombro de rua; INDOOR_SHOULDER dentro de casa (suave)
var _indoor_fov := 0.0
const SCENE_PITCH := deg_to_rad(-58.0) ## #246: durante um uso ou interação em casa a vista fica mais a prumo: parede e móvel da frente não cobrem os corpos
var scene_framing := false ## #246: o corpo está num uso/interação (o app liga); só vale dentro de casa
var framing_with: Node3D = null ## #246: o parceiro da interação: a câmera enquadra o ponto médio dos dois
var _indoor_pitch := deg_to_rad(-45.0) ## #250: o olhar de dentro de casa (o mouse o ajusta); o de rua, `pitch`, fica guardado para a saída
var _int_k := 0.0 ## 0 = olhar de rua; 1 = olhar de dentro de casa (suave)
var _trans := 0.0 ## s restantes de transição entrar/sair
var _in_room := false ## AD-197s3: o enquadramento anterior era dentro do cômodo (histerese contra piscar no limite)
var _snap := true ## AD-197s: o próximo enquadramento vai direto ao plano (corpo novo, modo novo): nenhum quadro na posição velha
var walking: Callable ## AD-197e: verdadeiro com personagem encarnado (as teclas de direção são do personagem)
var camera: Camera3D
var following: Node3D = null
var _zoom_goal := 22.0 ## alvo do zoom suave (roda do mouse); `zoom_by` vai direto
var _goal := Vector3.ZERO
var _goal_active := false
var _requested := false
var _pivot: Node3D
const SHAKE_DECAY := 2.4 ## por segundo: um tranco cheio some em ~0,4 s, em qualquer FPS
const SHAKE_M := 0.05 ## deslocamento máximo (metros do corpo) com o tranco cheio
const SHAKE_ROLL := 0.012 ## rad
var shake_enabled := true ## Opções: desligar o tremor
var shake_amount := 0.0 ## 0..1, decai com o tempo real
var shake_offset := Vector3.ZERO
var shake_roll := 0.0
var _shake_t := 0.0


func _init() -> void:
	_pivot = Node3D.new()
	add_child(_pivot)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.far = 400.0
	camera.position = Vector3(0, 0, 80)
	_pivot.add_child(camera)
	_apply()


func _apply() -> void:
	if build_view: # #264: o modo comprar olha a casa de cima, em projeção ortogonal, sem seguir o corpo
		position = build_center
		rotation = Vector3(0, build_yaw, 0)
		_pivot.position = Vector3.ZERO
		_pivot.rotation = Vector3(build_pitch, 0, 0)
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		camera.size = build_size
		camera.near = 0.05
		camera.position = Vector3(0, 0, 80)
		camera.rotation = Vector3.ZERO
		_last_xf = _world_xf()
		_has_xf = true
		return
	if mode == ISO:
		position = target
		rotation = Vector3(0, yaw, 0)
		_pivot.rotation = Vector3(iso_pitch, 0, 0)
		camera.size = zoom_size
		return
	position = view_target
	rotation = Vector3(0, view_yaw, 0)
	_pivot.rotation = Vector3(view_pitch, 0, 0)
	if mode == THIRD:
		var up := lift if following != null and is_instance_valid(following) else maxf(lift, await_lift())
		_pivot.position = Vector3(_ots, eye_height() + shoulder_height() + up, 0)
		camera.position = Vector3(shoulder, 0, arm)
		camera.rotation = Vector3(maxf(-atan2(up, maxf(arm, 0.01)), MAX_DOWN - view_pitch), atan2(shoulder, maxf(arm, 0.01)), 0) # AD-197s4: a ombro, gira para mirar o corpo # sobe olhando para o corpo, sem passar da vertical
	else:
		_pivot.position = Vector3(0, eye_height(), 0)
		camera.position = eye_push
		camera.rotation = Vector3.ZERO
	camera.fov = BASE_FOV + _run_fov + _indoor_fov
	_last_xf = _world_xf()
	_has_xf = true
	if _blend_t < 1.0:
		_apply_blend()
	elif shake_amount > 0.0: # #232: o tremor entra por cima da câmera já posta (as molas e o enquadramento não o veem)
		camera.position += shake_offset
		camera.rotation.z += shake_roll


## Onde a câmera está no mundo, pelas propriedades (vale fora da árvore, onde `global_transform` não vale).
func _world_xf() -> Transform3D:
	return transform * _pivot.transform * camera.transform


## Transição: do enquadramento anterior (ou, em `_reveal`, de uma vista recuada e alta) ao de agora, com ease; a câmera fica no transform
## do mundo até acabar (o rig e o pivô vão à identidade, a conta de `_world_xf` continua valendo).
func _apply_blend() -> void:
	var to := _last_xf
	var s := smoothstep(0.0, 1.0, _blend_t)
	var from := _blend_from
	var fov_from := _blend_from_fov
	if _reveal:
		var back := to.basis * Vector3(0, 0, 1)
		var flat := Vector3(back.x, 0, back.z).normalized()
		from = Transform3D(to.basis.rotated(to.basis.x.normalized(), -0.3), to.origin + Vector3.UP * Grid.units(7.0) + flat * Grid.units(5.0))
		fov_from = BASE_FOV + 8.0
	position = Vector3.ZERO
	rotation = Vector3.ZERO
	_pivot.position = Vector3.ZERO
	_pivot.rotation = Vector3.ZERO
	camera.transform = from.interpolate_with(to, s)
	camera.fov = lerpf(fov_from, camera.fov, s)
	_last_xf = to


## Começa uma transição a partir do quadro mostrado agora (sem quadro anterior: sem transição).
func begin_blend(duration: float = BLEND_S, reveal: bool = false) -> void:
	if mode == ISO or not _has_xf:
		return
	_blend_from = _last_xf
	_blend_from_fov = camera.fov
	_blend_dur = maxf(duration, 0.01)
	_blend_t = 0.0
	_reveal = reveal


func blending() -> bool:
	return _blend_t < 1.0


## Mola críticamente amortecida, solução exata (independe do passo de quadro para alvo constante): devolve [posição, velocidade].
static func spring(x: float, v: float, goal: float, omega: float, dt: float) -> Array:
	var d := x - goal
	var e := exp(-omega * dt)
	var t := (v + omega * d) * dt
	return [goal + (d + t) * e, (v - omega * t) * e]


func _snap_view() -> void:
	view_target = target
	view_yaw = yaw
	view_pitch = pitch
	_v_target = Vector3.ZERO
	_v_yaw = 0.0
	_v_pitch = 0.0


## Avança o seguir do corpo e o olhar pelo tempo do quadro (molas exatas); corpo longe demais (outro lugar) vai direto, com chegada animada.
func _advance_view(delta: float) -> void:
	if view_target.distance_to(target) > Grid.units(40.0):
		begin_blend(REVEAL_S, true)
		_snap_view()
		_snap = true
		return
	if delta <= 0.0:
		return
	var out := Vector3.ZERO
	var vel := Vector3.ZERO
	for k in 3:
		var r := spring(view_target[k], _v_target[k], target[k], FOLLOW_OMEGA, delta)
		out[k] = r[0]
		vel[k] = r[1]
	view_target = out
	_v_target = vel
	var ry := spring(view_yaw, _v_yaw, view_yaw + wrapf(yaw - view_yaw, -PI, PI), AIM_OMEGA, delta)
	view_yaw = wrapf(ry[0], -PI, PI)
	_v_yaw = ry[1]
	_int_k = lerpf(_int_k, 1.0 if _indoor else 0.0, 1.0 - exp(-INDOOR_OMEGA * delta))
	var goal_pitch := lerpf(maxf(pitch, pitch_floor()), scene_pitch(), _int_k) if mode == THIRD else pitch
	var rp := spring(view_pitch, _v_pitch, goal_pitch, AIM_OMEGA, delta)
	view_pitch = rp[0]
	_v_pitch = rp[1]
	var speed_m := _v_target.length() * Grid.cell_meters
	_run_fov = lerpf(_run_fov, RUN_FOV_BONUS * smoothstep(2.4, 3.6, speed_m), 1.0 - exp(-4.0 * delta))
	var blend_in := 1.0 - exp(-INDOOR_OMEGA * delta)
	_indoor_k = lerpf(_indoor_k, INDOOR_SHOULDER if _indoor else 1.0, blend_in)
	var tight := clampf(1.0 - arm / (third_distance() * third_scale * COMFORT), 0.0, 1.0) if _indoor else 0.0
	_indoor_fov = lerpf(_indoor_fov, INDOOR_FOV_BONUS * tight, blend_in)
	if _blend_t < 1.0:
		_blend_t = minf(1.0, _blend_t + delta / (REVEAL_S if _reveal else _blend_dur))


## Escala do corpo seguido (a que a altura do motor deu ao boneco); sem corpo, o adulto mediano.
func body_scale() -> float:
	return following.scale.y if following != null and is_instance_valid(following) and following.scale.y > 0.0 else 1.0


## Altura dos olhos em unidades da grade, pelo corpo (AD-197r).
func eye_height() -> float:
	return eye_height_for(body_scale() * ADULT_HEIGHT_CM)


static func eye_height_for(height_cm: float) -> float:
	return Grid.units(height_cm / 100.0 * EYE_RATIO)


func shoulder_height() -> float:
	return Grid.units(SHOULDER_M) * body_scale() * _indoor_k


## #246: a inclinação de dentro de casa de agora: a do jogador, ou a mais a prumo enquanto um uso/interação enquadra a cena.
func scene_pitch() -> float:
	return minf(_indoor_pitch, SCENE_PITCH) if scene_framing else _indoor_pitch


## #246: liga/desliga o enquadramento de cena (uso de móvel, interação social); `partner` entra no quadro (ponto médio dos dois).
func set_scene_framing(on: bool, partner: Node3D = null) -> void:
	scene_framing = on
	framing_with = partner if on else null


## Menor inclinação do olhar de rua (rad). Dentro de casa o mouse ajusta `_indoor_pitch` (faixa de InteriorCamera), não este.
func pitch_floor() -> float:
	return MIN_PITCH


## #264: liga o modo comprar (olha `center` de cima, `size` unidades de altura na tela) ou o solta: a câmera do jogador volta no próximo quadro, sem transição seca.
func set_build_view(on: bool, center: Vector3 = Vector3.ZERO, size: float = 12.0, yaw_rad: float = 0.0) -> void:
	if on == build_view and not on:
		return
	build_view = on
	build_center = center
	build_size = size
	build_yaw = yaw_rad
	if not on:
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL if mode == ISO else Camera3D.PROJECTION_PERSPECTIVE
		camera.fov = BASE_FOV
		camera.near = 0.05 if mode == ISO else (near_first() if mode == FIRST else near_third())
		if mode != ISO:
			_snap = true
			_snap_view()
			begin_blend(0.35)
	_apply()


## Troca o modo da câmera: ISO (espectador e admin), THIRD ou FIRST (personagem encarnado).
func set_mode(value: String) -> void:
	if value == mode:
		return
	mode = value
	if mode == ISO:
		_tracked = false
		_blend_t = 1.0
		arm = third_distance()
		lift = 0.0
		shoulder = 0.0
		if not _see_through.is_empty():
			_see_through = {}
			see_through.emit({})
		if not _opened.is_empty():
			_opened = {}
			opened.emit({})
		set_cursor_captured(false)
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		camera.position = Vector3(0, 0, 80)
		camera.rotation = Vector3.ZERO
		camera.near = 0.05
		_pivot.position = Vector3.ZERO
	else:
		set_cursor_captured(true) # AD-197k (emenda do dono ao ADR-0051): o mouse olha só de mexer, sem botão
		camera.projection = Camera3D.PROJECTION_PERSPECTIVE
		camera.fov = BASE_FOV
		camera.near = near_first() if mode == FIRST else near_third()
		pitch = clampf(pitch, MIN_PITCH, MAX_PITCH)
		_snap = true
		_snap_view()
		eye_push = Vector3.ZERO
		if mode == THIRD:
			arm = third_distance()
			lift = 0.0
			shoulder = 0.0
		if following != null and is_instance_valid(following):
			target = following.position
			_settle()
	_apply()
	mode_changed.emit(mode)


var eye_push := Vector3.ZERO ## AD-197s: na 1ª pessoa, o quanto o olho recua para não encostar na parede (espaço do pivô)
var cursor_captured := false ## AD-197k: preso ao entrar no modo jogador (o mouse olha sem botão); Z solta e prende de novo


func set_cursor_captured(value: bool) -> void:
	cursor_captured = value and mode != ISO
	if DisplayServer.get_name() != "headless":
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED if cursor_captured else Input.MOUSE_MODE_VISIBLE


## C alterna 1ª e 3ª pessoa (só encarnado).
func toggle_person() -> void:
	if mode != ISO:
		begin_blend()
		set_mode(FIRST if mode == THIRD else THIRD)


## Q troca o ombro da mira por cima do ombro (a câmera passa para o outro lado do corpo, animada).
func swap_shoulder() -> void:
	ots_side = -ots_side


## Mouse olha: giro livre, inclinação limitada (não vira de cabeça para baixo).
func look(relative: Vector2) -> void:
	if mode == ISO:
		return
	yaw = wrapf(yaw - relative.x * LOOK_SPEED, -PI, PI)
	if mode == THIRD and _indoor: # #250: o mouse gira em volta do corpo e ajusta a inclinação da vista de dentro; a de rua fica guardada
		_indoor_pitch = clampf(_indoor_pitch - relative.y * LOOK_SPEED, InteriorCamera.PITCH_MIN, InteriorCamera.PITCH_MAX)
		return
	pitch = clampf(pitch - relative.y * LOOK_SPEED, pitch_floor(), MAX_PITCH)


## Para onde a câmera olha no chão (x, z), unitário: WASD anda relativo a isto.
func heading() -> Vector2:
	var forward := Vector3.FORWARD.rotated(Vector3.UP, yaw)
	return Vector2(forward.x, forward.z).normalized()


## Movimento em coordenadas de tela (x direita, y para baixo), convertido pelo giro atual.
func pan(screen_delta: Vector2) -> void:
	var scale := zoom_size / 18.0 * 0.04
	var right := Vector3.RIGHT.rotated(Vector3.UP, yaw)
	var forward := Vector3.FORWARD.rotated(Vector3.UP, yaw)
	following = null
	_goal_active = false
	target += (right * -screen_delta.x + forward * screen_delta.y) * scale
	_apply()


func orbit(delta_yaw: float) -> void:
	yaw = wrapf(yaw + delta_yaw, -PI, PI)
	_apply()


## Alcance do zoom acompanha o tamanho do mapa: tudo visível no máximo; a bolha começa em 60% dele.
func fit_to_map(map_cells: int) -> void:
	max_size = maxf(48.0, map_cells * 1.4)
	bubble_size = maxf(24.0, max_size * 0.6)
	_zoom_goal = clampf(_zoom_goal, min_size, max_size)


func zoom_by(factor: float) -> void:
	_zoom_goal = clampf(zoom_size * factor, min_size, max_size)
	_apply_zoom(_zoom_goal)


## Roda do mouse: soma ao alvo e o `_process` aproxima o zoom devagar (zoom contínuo, sem saltos).
func zoom_smooth(factor: float) -> void:
	_zoom_goal = clampf(_zoom_goal * factor, min_size, max_size)


func _apply_zoom(size: float) -> void:
	zoom_size = clampf(size, min_size, max_size)
	_apply()
	_check_scope()


func _check_scope() -> void:
	if scope_kind == "World" and not _requested and zoom_size <= city_enter_size:
		var city_id: String = city_at.call(target)
		if city_id != "":
			_requested = true
			scope_requested.emit("City", city_id)
	elif scope_kind == "City" and not _requested and zoom_size >= city_exit_size:
		_requested = true
		scope_requested.emit("World", "")


## O app avisa qual escopo está ativo; libera novos pedidos de troca.
func set_scope(kind: String) -> void:
	scope_kind = kind
	_requested = false


func focus(point: Vector3) -> void:
	_goal = point
	_goal_active = true


func set_zoom(size: float) -> void:
	_zoom_goal = clampf(size, min_size, max_size)
	_apply_zoom(_zoom_goal)


func follow(node: Node3D) -> void:
	following = node
	if node != null and mode != ISO: # AD-197s: corpo novo (encarnou, chegou no delta): vai direto ao enquadramento, sem quadro na posição velha
		if _tracked and node.position.distance_to(view_target) > Grid.units(3.0):
			begin_blend(REVEAL_S, true) # #231: outro lugar: a câmera chega descendo, sem corte seco
		target = node.position
		_snap = true
		_snap_view()
		_settle()
		_apply()


func release() -> void:
	following = null


## #232: tranco curto (golpe, pouso forte). Soma até 1; nada em 1ª/3ª pessoa fora do modo jogador ou com a opção desligada.
func add_shake(amount: float) -> void:
	if shake_enabled and mode != ISO:
		shake_amount = minf(shake_amount + amount, 1.0)


## Decai o tranco e calcula o deslocamento deste quadro (independe do FPS: só `delta`).
func step_shake(delta: float) -> void:
	if shake_amount <= 0.0:
		return
	shake_amount = maxf(shake_amount - SHAKE_DECAY * delta, 0.0)
	_shake_t += delta
	var k := shake_amount * shake_amount
	shake_offset = Vector3(sin(_shake_t * 47.0), sin(_shake_t * 61.0 + 1.3), 0.0) * (k * Grid.units(SHAKE_M))
	shake_roll = sin(_shake_t * 53.0 + 0.7) * k * SHAKE_ROLL
	if shake_amount <= 0.0:
		shake_offset = Vector3.ZERO
		shake_roll = 0.0


func _process(delta: float) -> void:
	step_shake(delta)
	if shake_amount > 0.0 and following == null and mode != ISO:
		_apply()
	if absf(_zoom_goal - zoom_size) > 0.001:
		_apply_zoom(zoom_size * pow(_zoom_goal / zoom_size, clampf(delta * 9.0, 0.0, 1.0)))
	if _goal_active:
		target = target.lerp(_goal, clampf(delta * 5.0, 0.0, 1.0))
		_apply()
		if target.distance_to(_goal) < 0.02:
			_goal_active = false
	if following != null and is_instance_valid(following):
		# no modo do jogador a câmera cola no corpo (o corpo já anda suave pela previsão); na isométrica, segue devagar
		var aim: Vector3 = following.position
		if scene_framing and framing_with != null and is_instance_valid(framing_with) and framing_with != following:
			aim = aim.lerp(framing_with.position, 0.5) # #246: os dois corpos da interação no quadro
		target = aim if mode != ISO else target.lerp(aim, clampf(delta * 6.0, 0.0, 1.0))
		if mode != ISO:
			_tracked = true
			_advance_view(delta)
		_settle(delta)
		_apply()
	elif mode != ISO and (_blend_t < 1.0 or view_yaw != yaw or view_pitch != pitch):
		_advance_view(delta)
		_apply()


## Prédios que cobrem o corpo agora (a conta da 3ª pessoa); o app os deixa translúcidos a cada quadro.
func seen_ids() -> Dictionary:
	return _see_through


## Prédios em que o corpo está agora (a câmera olha de cima ou do cômodo).
func opened_ids() -> Dictionary:
	return _opened


## Enquadramento do modo do jogador a cada quadro: 3ª pessoa (braço de mola) ou 1ª (olho afastado da parede). `delta` 0 = ir direto.
func _settle(delta: float = 0.0) -> void:
	if mode == THIRD:
		_spring_arm(delta)
	elif mode == FIRST and blockers.is_valid():
		var eye := target + Vector3(0, eye_height(), 0)
		var pushed := push_out(eye, blockers.call())
		eye_push = (Basis(Vector3.UP, view_yaw) * Basis(Vector3.RIGHT, view_pitch)).inverse() * (pushed - eye)


## AD-197p/197r/197s: a 3ª pessoa escolhe o primeiro enquadramento livre (reto atrás; mais alto; mais alto e a ombro); sem nenhum livre,
## fica o de caminho mais longo, sempre subindo para olhar o corpo de cima. Com o corpo dentro de um prédio, a câmera sobe para
## fora dele (acima do telhado) e o prédio abre. O que ainda cobrir o corpo fica translúcido.
func _spring_arm(delta: float) -> void:
	if not blockers.is_valid():
		return
	var boxes: Array = blockers.call()
	var eye := target + Vector3(0, eye_height() + shoulder_height(), 0)
	var cap := third_distance() * third_scale
	var plan := plan_third(eye, view_yaw, scene_pitch() if _indoor else view_pitch, cap, boxes, body_scale(), _in_room)
	var now_indoor := bool(plan.get("indoor", false))
	if now_indoor and not _indoor: # #250: entrou na casa: o olhar de dentro nasce da planta e o braço é refeito com ele
		_indoor_pitch = float(plan.pitch)
		plan = plan_third(eye, view_yaw, scene_pitch(), cap, boxes, body_scale(), _in_room)
	if now_indoor != _indoor:
		_trans = INDOOR_TRANSITION_S
	_in_room = bool(plan.get("room", false))
	_indoor = now_indoor
	var free: float = plan.arm
	var body_center := target + Vector3(0, eye_height() * 0.55, 0)
	var within := containing(body_center, boxes)
	if _snap:
		_snap = false
		_indoor_k = INDOOR_SHOULDER if _indoor else 1.0
		_int_k = 1.0 if _indoor else 0.0
		_trans = 0.0
		if _indoor:
			view_pitch = _indoor_pitch
			_v_pitch = 0.0
		eye = target + Vector3(0, eye_height() + shoulder_height(), 0)
		arm = free
		lift = plan.lift
		shoulder = plan.shoulder
		_arm_hold = 0.0
		_ots = _ots_goal(eye, boxes, cap, not within.is_empty())
		_v_ots = 0.0
	else:
		if _indoor or _trans > 0.0: # #250: dentro de casa a câmera é de enquadramento (não de colisão): mola nos dois sentidos
			_trans = maxf(0.0, _trans - delta)
			arm = move_toward(arm, lerpf(arm, free, 1.0 - exp(-INDOOR_ARM_OMEGA * delta)), INDOOR_ARM_SPEED * delta) # #268: e nunca mais rápido que isto (cômodo grande ↔ pequeno)
			_arm_hold = 0.0
		else:
			arm = _next_arm(free, cap, delta)
		var blend := 1.0 - exp(-(4.0 if _trans > 0.0 else 8.0) * delta)
		lift = lerpf(lift, plan.lift, blend)
		shoulder = lerpf(shoulder, plan.shoulder, blend)
		var r := spring(_ots, _v_ots, _ots_goal(eye, boxes, cap, not within.is_empty()), OTS_OMEGA, delta)
		_ots = r[0]
		_v_ots = r[1]
	var seen := covering(third_spot(eye), body_center, boxes)
	if seen != _see_through:
		_see_through = seen
		see_through.emit(seen)
	if within != _opened:
		_opened = within
		opened.emit(within)


## #231: o braço ao bater encurta rápido (exponencial, e nunca acima de `free + metade da folga`: não atravessa a parede); depois de bater
## espera ARM_HOLD_S e volta devagar (linear). `free` é o que o plano deixa livre; `cap` o braço cheio (com o zoom).
func _next_arm(free: float, cap: float, delta: float) -> float:
	if free < arm - 0.0005:
		_arm_hold = ARM_HOLD_S
		var soft := free + (arm - free) * exp(-ARM_SHORTEN * delta)
		return minf(soft, free + arm_margin() * 0.5)
	if free < cap - 0.0005:
		_arm_hold = ARM_HOLD_S
	if _arm_hold > 0.0:
		_arm_hold = maxf(0.0, _arm_hold - delta)
		return minf(arm, free)
	return minf(free, arm + (cap - min_arm()) * ARM_RETURN * delta)


## Deslocamento lateral da mira por cima do ombro: some (suave) onde a lateral não cabe: câmera curta, subida, desvio, dentro de prédio
## ou lateral dentro de uma casca. Poucas contas de caixa, nenhum raycast.
func _ots_goal(eye: Vector3, boxes: Array, cap: float, inside: bool) -> float:
	if ots_m <= 0.0 or inside or absf(lift) > 0.01 or absf(shoulder) > 0.01:
		return 0.0
	var side := Grid.units(ots_m) * body_scale() * ots_side
	var right := Basis(Vector3.UP, view_yaw) * Vector3.RIGHT
	var spot := third_spot(eye)
	for point in [eye, (eye + spot) * 0.5, spot]:
		if inside_any(point + right * side, boxes):
			return 0.0
	return side * clampf(arm / (cap * COMFORT), 0.0, 1.0)


## Onde a câmera da 3ª pessoa está agora (mundo): do olho, sobe `lift`, passa `shoulder` à direita e recua `arm` pelo olhar.
func third_spot(eye: Vector3) -> Vector3:
	var back := Basis(Vector3.UP, view_yaw) * Basis(Vector3.RIGHT, view_pitch) * Vector3(0, 0, 1)
	var right := Basis(Vector3.UP, view_yaw) * Vector3.RIGHT
	return eye + Vector3(0, lift, 0) + right * shoulder + back * arm


const COMFORT := 0.6 ## fração do braço cheio que já basta para a vista ser boa (abaixo disso tenta subir e passar a ombro)


## Enquadramentos tentados, na ordem: [subida em unidades da grade, ombro em unidades]. O corpo é escalado (criança sobe menos).
static func third_options(scale: float) -> Array:
	var up := Grid.units(1.0) * scale
	var side := Grid.units(0.6) * scale
	var wide := Grid.units(1.2) * scale # AD-197s3: passar a ombro, nos dois lados, ANTES de subir (prédio colado atrás: dobra a esquina)
	return [[0.0, 0.0], [0.0, wide], [0.0, -wide], [up, 0.0], [up * 2.0, 0.0], [up * 2.0, side], [up * 2.0, -side], [up * 3.0, 0.0]]


## Escolhe {arm, lift, shoulder} da 3ª pessoa: o primeiro enquadramento com caminho livre >= COMFORT do braço cheio e câmera fora de
## qualquer caixa; senão o de maior caminho (empate: o menos deslocado), com a subida mínima de quem ficou colado ao corpo.
## Cada entrada de `boxes` é [id, caixa] ou [id, caixa, casca]: a caixa diz onde o corpo está; a casca (caixa + beiral e cumeeira do
## telhado) é o que a câmera não pode tocar. Corpo dentro de um prédio: câmera acima da casca (AD-197s), nunca dentro das paredes.
static func plan_third(eye: Vector3, yaw: float, pitch: float, length: float, boxes: Array, scale: float = 1.0, was_room: bool = false) -> Dictionary:
	var back := Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, pitch) * Vector3(0, 0, 1)
	var inner := interior_entry(eye, boxes) # #227: a casa em foco (sem casca de prédio)
	if not inner.is_empty(): # #250: de dentro, a câmera sobe e olha o cômodo em diagonal (parede e móvel no caminho ficam translúcidos)
		var others: Array = boxes.filter(func(entry): return not (entry.size() > 4 and bool(entry[4])))
		return InteriorCamera.plan(eye, back, pitch, inner, length / third_distance(), third_distance(), others, min_arm())
	var room := roof_lift(eye, boxes)
	if room > 0.0:
		# histerese (AD-197s3): quem já está no cômodo só sai com 15% a menos de espaço; quem está em cima só entra com 15% a mais
		var inside := room_plan(eye, back, length, boxes, room_arm() * (0.85 if was_room else 1.15))
		if not inside.is_empty():
			inside["room"] = true
			return inside
	if room > 0.0: # #222: corte de RPG: a câmera fica atrás, na distância cheia, fora da casca (a parede entre ela e o corpo fica
		# translúcida e o telhado abre); sobe só o necessário: no olho, acima da parede do andar, metade do telhado, o telhado todo
		for rise in [floor_lift(eye, boxes), room * 0.5]:
			if rise <= room and not inside_any(eye + Vector3(0, rise, 0) + back * length, boxes):
				return {"arm": length, "lift": rise, "shoulder": 0.0, "cut": true}
	if room > 0.0: # #228: com paredes conhecidas nunca há vista aérea: o corpo está num cômodo apertado, a câmera fica nele, curta e na altura de gente
		var close := close_plan(eye, back, length, boxes)
		if not close.is_empty():
			return close
	if room > 0.0: # por cima do telhado e um pouco atrás (olhar inclinado, não a prumo), enquanto o ponto estiver livre de casca
		var arm := min_arm()
		for reach in [length * 0.5, Grid.units(1.5)]:
			if not inside_any(eye + Vector3(0, room, 0) + back * reach, boxes):
				arm = reach
				break
		return {"arm": arm, "lift": room, "shoulder": 0.0}
	var right := Basis(Vector3.UP, yaw) * Vector3.RIGHT
	boxes = under_eave(eye, boxes) # #222: encostado na parede, o olho fica sob o beiral: dali conta a parede, não o beiral
	var best := {"arm": -1.0, "lift": 0.0, "shoulder": 0.0}
	for option in third_options(scale):
		var origin: Vector3 = eye + Vector3(0, option[0], 0) + right * option[1]
		var free := free_arm(origin, back, length, boxes)
		if inside_any(origin + back * free, boxes):
			free = min_arm()
		if free >= length * COMFORT:
			return {"arm": free, "lift": option[0], "shoulder": option[1]}
		if free > float(best.arm) + length * 0.15: # só troca por um enquadramento deslocado se ele ganha caminho de verdade (nada de subir à toa)
			best = {"arm": free, "lift": option[0], "shoulder": option[1]}
	if float(best.arm) < length * COMFORT: # nada abriu: curta e um pouco acima, olhando o corpo (o que cobrir fica translúcido); só sobe a prumo se colada
		var shallow := float(best.arm) * tan(deg_to_rad(SHALLOW_DEG))
		best.lift = maxf(float(best.lift), shallow)
	return best


const SHALLOW_DEG := 35.0 ## sem caminho livre, a subida extra mantém a inclinação até este ângulo (não olha a prumo para a cabeça)


## AD-197s2: corpo dentro de um prédio com paredes conhecidas (4ª coluna da entrada: caixas das células de parede): a câmera fica DENTRO do
## cômodo se couber `room_arm()` atrás do corpo (até a parede com folga, o teto do andar e a caixa do prédio); senão {} e a câmera sobe.
static func room_plan(eye: Vector3, back: Vector3, length: float, boxes: Array, need: float = -1.0) -> Dictionary:
	for entry in boxes:
		var raw: AABB = entry[1]
		if not raw.has_point(eye):
			continue
		var walls: Array = entry[3] if entry.size() > 3 else []
		if walls.is_empty():
			return {}
		for wall in walls:
			if (wall as AABB).has_point(eye):
				return {}
		var floor_h := Grid.floor_height()
		var ceiling := raw.position.y + (floorf((eye.y - raw.position.y) / floor_h) + 1.0) * floor_h - Grid.units(CEILING_M)
		var free := minf(length, exit_distance(eye, back, raw) - arm_margin())
		if back.y > 0.0001:
			free = minf(free, (ceiling - eye.y) / back.y)
		var far := eye + back * length
		for wall in walls:
			var hit = (wall as AABB).intersects_segment(eye, far)
			if hit != null:
				free = minf(free, eye.distance_to(hit) - arm_margin())
		if free >= (need if need > 0.0 else room_arm()):
			return {"arm": free, "lift": 0.0, "shoulder": 0.0}
		return {}
	return {}


## #228: último recurso dentro de um prédio com paredes: arma curta no cômodo (até a parede mais próxima atrás, com folga; nunca menos
## que meio braço mínimo), com subida de gente. O que cobrir o corpo fica translúcido. {} sem paredes conhecidas (aí vale o telhado).
static func close_plan(eye: Vector3, back: Vector3, length: float, boxes: Array) -> Dictionary:
	for entry in boxes:
		var raw: AABB = entry[1]
		if not raw.has_point(eye):
			continue
		var walls: Array = entry[3] if entry.size() > 3 else []
		if walls.is_empty():
			return {}
		var free := minf(length, exit_distance(eye, back, raw) - arm_margin())
		if back.y > 0.0001: # o forro também limita (a câmera sobe com o olhar para baixo)
			var floor_h := Grid.floor_height()
			var ceiling := raw.position.y + (floorf((eye.y - raw.position.y) / floor_h) + 1.0) * floor_h - Grid.units(CEILING_M)
			free = minf(free, (ceiling - eye.y) / back.y)
		var far := eye + back * length
		for wall in walls:
			if (wall as AABB).has_point(eye):
				continue # encostado ou na porta: a parede do próprio olho não encurta (ela fica translúcida)
			var hit = (wall as AABB).intersects_segment(eye, far)
			if hit != null:
				free = minf(free, eye.distance_to(hit) - arm_margin())
		return {"arm": maxf(free, min_arm() * 0.6), "lift": Grid.units(CLOSE_LIFT_M), "shoulder": 0.0, "room": true}
	return {}


## O ponto está dentro de algum painel de parede da entrada da casa em foco.
static func inside_any_wall(point: Vector3, entry: Array) -> bool:
	var walls: Array = entry[3] if entry.size() > 3 else []
	for wall in walls:
		if (wall as AABB).has_point(point):
			return true
	return false


## #227: a entrada da casa em foco ([-1, caixa, caixa, painéis, true]): o quinto elemento a distingue da casca de um prédio do mundo.
static func interior_entry(eye: Vector3, boxes: Array) -> Array:
	for entry in boxes:
		if entry.size() > 4 and bool(entry[4]) and (entry[1] as AABB).has_point(eye):
			return entry
	return []


const CLOSE_LIFT_M := 0.0 ## sobre o olho, (0: a câmera fica na altura da cabeça, nunca olha o topo dela)


## Quanto o raio sai de dentro da caixa (eixos x e z; o teto vem da conta do andar).
static func exit_distance(from: Vector3, dir: Vector3, box: AABB) -> float:
	var best := INF
	if absf(dir.x) > 0.0001:
		best = minf(best, ((box.end.x if dir.x > 0.0 else box.position.x) - from.x) / dir.x)
	if absf(dir.z) > 0.0001:
		best = minf(best, ((box.end.z if dir.z > 0.0 else box.position.z) - from.z) / dir.z)
	return best


## Quanto subir (sobre o olho) para a câmera ficar acima da casca do prédio em que o olho está; 0 se o olho não está em prédio.
static func roof_lift(eye: Vector3, boxes: Array) -> float:
	var need := 0.0
	for entry in boxes:
		if (entry[1] as AABB).has_point(eye):
			need = maxf(need, shell_of(entry).end.y + arm_margin() - eye.y)
	return need


## #222: quanto subir (sobre o olho) para passar do teto do andar em que o olho está, na caixa que o contém; 0 se fora.
static func floor_lift(eye: Vector3, boxes: Array) -> float:
	for entry in boxes:
		var raw: AABB = entry[1]
		if raw.has_point(eye):
			var floor_h := Grid.floor_height()
			var ceiling := raw.position.y + (floorf((eye.y - raw.position.y) / floor_h) + 1.0) * floor_h
			return maxf(0.0, ceiling + arm_margin() - eye.y)
	return 0.0


## #222: o olho fora da caixa mas dentro da casca (rente à parede, sob o beiral): a casca desse prédio vira a própria caixa. Antes o raio
## ignorava o prédio e o ponto final caía no beiral, e o braço ia ao mínimo (câmera colada no corpo).
static func under_eave(eye: Vector3, boxes: Array) -> Array:
	var out: Array = []
	for entry in boxes:
		if not (entry[1] as AABB).has_point(eye) and shell_of(entry).has_point(eye):
			out.append([entry[0], entry[1], entry[1]] + (entry.slice(3) if entry.size() > 3 else []))
		else:
			out.append(entry)
	return out


## A casca de uma entrada: a 3ª coluna se houver, senão a própria caixa.
static func shell_of(entry: Array) -> AABB:
	return entry[2] if entry.size() > 2 else entry[1]


## Ids dos prédios (caixa) que contêm o ponto: o corpo está dentro deles.
static func containing(point: Vector3, boxes: Array) -> Dictionary:
	var out := {}
	for entry in boxes:
		if (entry[1] as AABB).has_point(point):
			out[entry[0]] = true
	return out


static func inside_any(point: Vector3, boxes: Array) -> bool:
	for entry in boxes:
		if shell_of(entry).has_point(point):
			return true
	return false


## Comprimento livre do braço: até a primeira casca que o segmento olho → câmera cruza, menos uma folga; nunca menos que min_arm().
static func free_arm(eye: Vector3, back: Vector3, length: float, boxes: Array) -> float:
	var best := length
	var far := eye + back * length
	for entry in boxes:
		var box := shell_of(entry)
		if box.has_point(eye):
			continue
		var hit = box.intersects_segment(eye, far)
		if hit != null:
			best = minf(best, eye.distance_to(hit) - arm_margin())
	return clampf(best, min_arm(), length)


## AD-197s: o olho da 1ª pessoa fora de um prédio não chega a eye_clear() da parede: sai pelo lado mais próximo (dentro, não mexe).
static func push_out(eye: Vector3, boxes: Array) -> Vector3:
	var at := eye
	for entry in boxes:
		var box: AABB = entry[1]
		if box.has_point(at) or at.y > box.end.y or at.y < box.position.y:
			continue
		var wide := AABB(box.position - Vector3(eye_clear(), 0, eye_clear()), box.size + Vector3(eye_clear() * 2.0, 0, eye_clear() * 2.0))
		if not wide.has_point(at):
			continue
		var gaps := [at.x - box.position.x, box.end.x - at.x, at.z - box.position.z, box.end.z - at.z]
		var side := 0 # a face de que o olho está por fora: a de menor folga (negativa)
		for k in 4:
			if gaps[k] < gaps[side]:
				side = k
		match side:
			0: at.x = box.position.x - eye_clear()
			1: at.x = box.end.x + eye_clear()
			2: at.z = box.position.z - eye_clear()
			_: at.z = box.end.z + eye_clear()
	return at


## Prédios cujas caixas cortam a linha da câmera ao corpo (ids, para a translucidez).
static func covering(from: Vector3, body: Vector3, boxes: Array) -> Dictionary:
	var out := {}
	for entry in boxes:
		if entry.size() > 4 and bool(entry[4]):
			continue # a casa em foco não tem casca a deixar translúcida
		var box: AABB = entry[1]
		var shell := shell_of(entry) # o beiral e a cumeeira também cobrem
		# o prédio em que o corpo está (ou em que a câmera ficou) também abre: de dentro, a parede e o telhado cobririam tudo
		if box.has_point(body) or shell.has_point(from) or shell.intersects_segment(from, body) != null:
			out[entry[0]] = true
	return out


func _unhandled_input(event: InputEvent) -> void:
	if mode != ISO:
		_player_input(event)
		return
	if event is InputEventMouseMotion and (event.button_mask & MOUSE_BUTTON_MASK_RIGHT or event.button_mask & MOUSE_BUTTON_MASK_MIDDLE):
		if Input.is_key_pressed(KEY_SHIFT):
			orbit(-event.relative.x * 0.008)
		else:
			pan(event.relative)
	elif event is InputEventMouseButton and event.pressed and not event.alt_pressed: # Alt+roda alterna o prédio sob o cursor
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			zoom_smooth(0.85)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			zoom_smooth(1.18)
	elif event is InputEventKey and event.pressed and event.keycode in ZOOM_IN_KEYS:
		zoom_smooth(0.85) # #222: zoom sem mouse
	elif event is InputEventKey and event.pressed and event.keycode in ZOOM_OUT_KEYS:
		zoom_smooth(1.18)
	elif event is InputEventKey and event.pressed:
		if walking.is_valid() and walking.call() and event.keycode in [KEY_W, KEY_UP, KEY_S, KEY_DOWN, KEY_A, KEY_LEFT, KEY_D, KEY_RIGHT]:
			return # encarnado, as teclas de direção andam com o personagem (player_hud), não movem a câmera
		match event.keycode:
			KEY_Q: orbit(deg_to_rad(15))
			KEY_E: orbit(deg_to_rad(-15))
			KEY_W, KEY_UP: pan(Vector2(0, -30))
			KEY_S, KEY_DOWN: pan(Vector2(0, 30))
			KEY_A, KEY_LEFT: pan(Vector2(-30, 0))
			KEY_D, KEY_RIGHT: pan(Vector2(30, 0))


## Encarnado: o mouse olha (com o botão direito, ou sempre com o cursor preso por Z), C troca 1ª/3ª pessoa; WASD, E e o clique são do
## personagem (player_hud e app), não da câmera.
const ZOOM_IN_KEYS := [KEY_EQUAL, KEY_PLUS, KEY_KP_ADD]
const ZOOM_OUT_KEYS := [KEY_MINUS, KEY_KP_SUBTRACT]


## #222: aproxima (<1) ou afasta (>1) a câmera da 3ª pessoa, entre os limites.
func zoom_third(factor: float) -> void:
	third_scale = clampf(third_scale * factor, THIRD_SCALE_MIN, THIRD_SCALE_MAX)


func _player_input(event: InputEvent) -> void:
	if mode == THIRD and event is InputEventKey and event.pressed and event.keycode in ZOOM_IN_KEYS:
		zoom_third(0.85)
		return
	if mode == THIRD and event is InputEventKey and event.pressed and event.keycode in ZOOM_OUT_KEYS:
		zoom_third(1.18)
		return
	if mode == THIRD and event is InputEventMouseButton and event.pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
		zoom_third(0.85 if event.button_index == MOUSE_BUTTON_WHEEL_UP else 1.18)
		return
	if event is InputEventMouseMotion and (cursor_captured or event.button_mask & MOUSE_BUTTON_MASK_RIGHT):
		look(event.relative)
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_C:
		toggle_person()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_Q and mode == THIRD:
		swap_shoulder()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_Z:
		set_cursor_captured(not cursor_captured)
