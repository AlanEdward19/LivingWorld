extends Node3D
## Câmera do espectador, componente trocável: isométrica ortográfica para espectador e admin; com personagem encarnado,
## perspectiva em 3ª pessoa (padrão) ou 1ª pessoa (tecla C), o mouse olha (AD-197d, ADR-0049/0051). Mesma interface
## (pan/orbit/zoom_by/follow/release); `look` e `heading` só valem nos modos do jogador.
## NÃO conhece transporte: navegar nunca fala com o servidor. Só pede troca de escopo por sinal.

signal scope_requested(kind: String, ref_id: String)
signal mode_changed(mode: String) ## AD-197j: o app mostra a mira no centro e solta o cursor ao voltar à isométrica
signal see_through(ids: Dictionary) ## AD-197p: prédios entre a câmera da 3ª pessoa e o corpo (o app os deixa translúcidos)

const ISO_PITCH := deg_to_rad(-35.264)
const ISO := "iso"
const THIRD := "third"
const FIRST := "first"
const THIRD_DISTANCE := 4.5 ## metros atrás do personagem (uma célula = 1 unidade)
const EYE_HEIGHT := 1.6 ## olhos na 1ª pessoa; a 3ª mira um pouco acima do ombro
const MIN_PITCH := deg_to_rad(-70.0)
const MAX_PITCH := deg_to_rad(35.0)
const LOOK_SPEED := 0.004 ## rad por pixel do mouse

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
const MIN_ARM := 0.5
const ARM_MARGIN := 0.25
const ARM_RETURN := 3.0 ## por segundo: a volta é suave; a aproximação, imediata (nunca atravessa a parede)
var blockers: Callable = Callable()
var arm := THIRD_DISTANCE
var _see_through: Dictionary = {}
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
	_pivot.position = Vector3(0, EYE_HEIGHT, 0)
	camera.position = Vector3(0, 0, arm if mode == THIRD else 0.0)


## Troca o modo da câmera: ISO (espectador e admin), THIRD ou FIRST (personagem encarnado).
func set_mode(value: String) -> void:
	if value == mode:
		return
	mode = value
	if mode == ISO:
		arm = THIRD_DISTANCE
		if not _see_through.is_empty():
			_see_through = {}
			see_through.emit({})
		set_cursor_captured(false)
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		camera.position = Vector3(0, 0, 80)
		camera.near = 0.05
		_pivot.position = Vector3.ZERO
	else:
		set_cursor_captured(true) # AD-197k (emenda do dono ao ADR-0051): o mouse olha só de mexer, sem botão
		camera.projection = Camera3D.PROJECTION_PERSPECTIVE
		camera.fov = 70.0
		camera.near = 0.05
		pitch = clampf(pitch, MIN_PITCH, MAX_PITCH)
	_apply()
	mode_changed.emit(mode)


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
		if mode == THIRD:
			_spring_arm(delta)
		_apply()


## AD-197p: o braço é o menor caminho livre entre o olho (acima do ombro) e o ponto atrás dele; o que ainda cobrir o corpo fica
## translúcido. Só as caixas desenhadas dos prédios; quem tem o olho dentro (o corpo dentro de casa) não conta.
func _spring_arm(delta: float) -> void:
	if not blockers.is_valid():
		return
	var boxes: Array = blockers.call()
	var eye := target + Vector3(0, EYE_HEIGHT, 0)
	var back := Basis(Vector3.UP, yaw) * Basis(Vector3.RIGHT, pitch) * Vector3(0, 0, 1)
	var free := free_arm(eye, back, THIRD_DISTANCE, boxes)
	arm = free if free < arm else minf(free, arm + (THIRD_DISTANCE - MIN_ARM) * ARM_RETURN * delta)
	var seen := covering(eye + back * arm, target + Vector3(0, 0.9, 0), boxes)
	if seen != _see_through:
		_see_through = seen
		see_through.emit(seen)


## Comprimento livre do braço: até a primeira caixa que o segmento olho → câmera cruza, menos uma folga; nunca menos que MIN_ARM.
static func free_arm(eye: Vector3, back: Vector3, length: float, boxes: Array) -> float:
	var best := length
	var far := eye + back * length
	for entry in boxes:
		var box: AABB = entry[1]
		if box.has_point(eye):
			continue
		var hit = box.intersects_segment(eye, far)
		if hit != null:
			best = minf(best, eye.distance_to(hit) - ARM_MARGIN)
	return clampf(best, MIN_ARM, length)


## Prédios cujas caixas cortam a linha da câmera ao corpo (ids, para a translucidez).
static func covering(from: Vector3, body: Vector3, boxes: Array) -> Dictionary:
	var out := {}
	for entry in boxes:
		var box: AABB = entry[1]
		# o prédio em que o corpo está (ou em que a câmera ficou) também abre: de dentro, a parede e o telhado cobririam tudo
		if box.has_point(body) or box.has_point(from) or box.intersects_segment(from, body) != null:
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
