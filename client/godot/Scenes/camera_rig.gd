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
const ROOM_ARM_M := 2.0 ## AD-197s2: dentro de casa a câmera fica no cômodo se couber isto atrás do corpo; senão sobe acima do telhado
const NEAR_PERSON_M := 1.0 ## na 1ª pessoa quem está a menos disto do olho não se desenha (o espaçamento mínimo entre corpos parados é ~0,9 m)
const CEILING_M := 0.2 ## folga da câmera até o teto do andar
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
var lift := 0.0 ## AD-197r: quanto a 3ª pessoa sobe (olhando o corpo de cima) quando o caminho para trás está fechado
var shoulder := 0.0 ## e quanto passa a ombro (deslocamento lateral), em unidades
var _see_through: Dictionary = {}
var _opened: Dictionary = {}
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
	position = target
	rotation = Vector3(0, yaw, 0)
	if mode == ISO:
		_pivot.rotation = Vector3(ISO_PITCH, 0, 0)
		camera.size = zoom_size
		return
	_pivot.rotation = Vector3(pitch, 0, 0)
	if mode == THIRD:
		var up := lift if following != null and is_instance_valid(following) else maxf(lift, await_lift())
		_pivot.position = Vector3(0, eye_height() + shoulder_height() + up, 0)
		camera.position = Vector3(shoulder, 0, arm)
		camera.rotation = Vector3(maxf(-atan2(up, maxf(arm, 0.01)), MAX_DOWN - pitch), 0, 0) # sobe olhando para o corpo, sem passar da vertical
	else:
		_pivot.position = Vector3(0, eye_height(), 0)
		camera.position = eye_push
		camera.rotation = Vector3.ZERO


## Escala do corpo seguido (a que a altura do motor deu ao boneco); sem corpo, o adulto mediano.
func body_scale() -> float:
	return following.scale.y if following != null and is_instance_valid(following) and following.scale.y > 0.0 else 1.0


## Altura dos olhos em unidades da grade, pelo corpo (AD-197r).
func eye_height() -> float:
	return eye_height_for(body_scale() * ADULT_HEIGHT_CM)


static func eye_height_for(height_cm: float) -> float:
	return Grid.units(height_cm / 100.0 * EYE_RATIO)


func shoulder_height() -> float:
	return Grid.units(SHOULDER_M) * body_scale()


## Troca o modo da câmera: ISO (espectador e admin), THIRD ou FIRST (personagem encarnado).
func set_mode(value: String) -> void:
	if value == mode:
		return
	mode = value
	if mode == ISO:
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
		camera.fov = 70.0
		camera.near = near_first() if mode == FIRST else near_third()
		pitch = clampf(pitch, MIN_PITCH, MAX_PITCH)
		_snap = true
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
		set_mode(FIRST if mode == THIRD else THIRD)


## Mouse olha: giro livre, inclinação limitada (não vira de cabeça para baixo).
func look(relative: Vector2) -> void:
	if mode == ISO:
		return
	yaw = wrapf(yaw - relative.x * LOOK_SPEED, -PI, PI)
	pitch = clampf(pitch - relative.y * LOOK_SPEED, MIN_PITCH, MAX_PITCH)
	_apply()


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
		target = node.position
		_snap = true
		_settle()
		_apply()


func release() -> void:
	following = null


func _process(delta: float) -> void:
	if absf(_zoom_goal - zoom_size) > 0.001:
		_apply_zoom(zoom_size * pow(_zoom_goal / zoom_size, clampf(delta * 9.0, 0.0, 1.0)))
	if _goal_active:
		target = target.lerp(_goal, clampf(delta * 5.0, 0.0, 1.0))
		_apply()
		if target.distance_to(_goal) < 0.02:
			_goal_active = false
	if following != null and is_instance_valid(following):
		# no modo do jogador a câmera cola no corpo (o corpo já anda suave pela previsão); na isométrica, segue devagar
		target = following.position if mode != ISO else target.lerp(following.position, clampf(delta * 6.0, 0.0, 1.0))
		_settle(delta)
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
		eye_push = (Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, pitch)).inverse() * (pushed - eye)


## AD-197p/197r/197s: a 3ª pessoa escolhe o primeiro enquadramento livre (reto atrás; mais alto; mais alto e a ombro); sem nenhum livre,
## fica o de caminho mais longo, sempre subindo para olhar o corpo de cima. Com o corpo dentro de um prédio, a câmera sobe para
## fora dele (acima do telhado) e o prédio abre. O que ainda cobrir o corpo fica translúcido.
func _spring_arm(delta: float) -> void:
	if not blockers.is_valid():
		return
	var boxes: Array = blockers.call()
	var eye := target + Vector3(0, eye_height() + shoulder_height(), 0)
	var plan := plan_third(eye, yaw, pitch, third_distance(), boxes, body_scale(), _in_room)
	_in_room = bool(plan.get("room", false))
	var free: float = plan.arm
	if _snap:
		_snap = false
		arm = free
		lift = plan.lift
		shoulder = plan.shoulder
	else:
		arm = free if free < arm else minf(free, arm + (third_distance() - min_arm()) * ARM_RETURN * delta)
		var blend := clampf(delta * 8.0, 0.0, 1.0)
		lift = lerpf(lift, plan.lift, blend)
		shoulder = lerpf(shoulder, plan.shoulder, blend)
	var body_center := target + Vector3(0, eye_height() * 0.55, 0)
	var seen := covering(third_spot(eye), body_center, boxes)
	if seen != _see_through:
		_see_through = seen
		see_through.emit(seen)
	var within := containing(body_center, boxes)
	if within != _opened:
		_opened = within
		opened.emit(within)


## Onde a câmera da 3ª pessoa está agora (mundo): do olho, sobe `lift`, passa `shoulder` à direita e recua `arm` pelo olhar.
func third_spot(eye: Vector3) -> Vector3:
	var back := Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, pitch) * Vector3(0, 0, 1)
	var right := Basis(Vector3.UP, yaw) * Vector3.RIGHT
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
	var room := roof_lift(eye, boxes)
	if room > 0.0:
		# histerese (AD-197s3): quem já está no cômodo só sai com 15% a menos de espaço; quem está em cima só entra com 15% a mais
		var inside := room_plan(eye, back, length, boxes, room_arm() * (0.85 if was_room else 1.15))
		if not inside.is_empty():
			inside["room"] = true
			return inside
	if room > 0.0: # por cima do telhado e um pouco atrás (olhar inclinado, não a prumo), enquanto o ponto estiver livre de casca
		var arm := min_arm()
		for reach in [length * 0.5, Grid.units(1.5)]:
			if not inside_any(eye + Vector3(0, room, 0) + back * reach, boxes):
				arm = reach
				break
		return {"arm": arm, "lift": room, "shoulder": 0.0}
	var right := Basis(Vector3.UP, yaw) * Vector3.RIGHT
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
func _player_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and (cursor_captured or event.button_mask & MOUSE_BUTTON_MASK_RIGHT):
		look(event.relative)
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_C:
		toggle_person()
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_Z:
		set_cursor_captured(not cursor_captured)
