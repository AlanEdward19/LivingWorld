extends Node3D
## Câmera do espectador, componente trocável: isométrica ortográfica para espectador e admin; com personagem encarnado,
## perspectiva em 3ª pessoa (padrão) ou 1ª pessoa (tecla C), o mouse olha (AD-197d, ADR-0049/0051). Mesma interface
## (pan/orbit/zoom_by/follow/release); `look` e `heading` só valem nos modos do jogador.
## NÃO conhece transporte: navegar nunca fala com o servidor. Só pede troca de escopo por sinal.

signal scope_requested(kind: String, ref_id: String)

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
	camera.position = Vector3(0, 0, THIRD_DISTANCE if mode == THIRD else 0.0)


## Troca o modo da câmera: ISO (espectador e admin), THIRD ou FIRST (personagem encarnado).
func set_mode(value: String) -> void:
	if value == mode:
		return
	mode = value
	if mode == ISO:
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		camera.position = Vector3(0, 0, 80)
		camera.near = 0.05
		_pivot.position = Vector3.ZERO
	else:
		camera.projection = Camera3D.PROJECTION_PERSPECTIVE
		camera.fov = 70.0
		camera.near = 0.05
		pitch = clampf(pitch, MIN_PITCH, MAX_PITCH)
	_apply()


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
		_apply()


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


## Encarnado: o mouse olha (com o botão direito, ou sempre com o cursor preso), C troca 1ª/3ª pessoa; WASD, E e o clique são do
## personagem (player_hud e app), não da câmera.
func _player_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and (Input.mouse_mode == Input.MOUSE_MODE_CAPTURED or event.button_mask & MOUSE_BUTTON_MASK_RIGHT):
		look(event.relative)
	elif event is InputEventKey and event.pressed and not event.echo and event.keycode == KEY_C:
		toggle_person()
