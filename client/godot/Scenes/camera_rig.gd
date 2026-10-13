extends Node3D
## Câmera do espectador, componente trocável: hoje isométrica ortográfica; 1ª e 3ª pessoa entram
## na fase do jogador implementando a mesma interface (pan/orbit/zoom_by/follow/release).
## NÃO conhece transporte: navegar nunca fala com o servidor. Só pede troca de escopo por sinal.

signal scope_requested(kind: String, ref_id: String)

const ISO_PITCH := deg_to_rad(-35.264)

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
	_pivot.rotation = Vector3(ISO_PITCH, 0, 0)
	camera.size = zoom_size


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
		target = target.lerp(following.position, clampf(delta * 6.0, 0.0, 1.0))
		_apply()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion and (event.button_mask & MOUSE_BUTTON_MASK_RIGHT or event.button_mask & MOUSE_BUTTON_MASK_MIDDLE):
		if Input.is_key_pressed(KEY_SHIFT):
			orbit(-event.relative.x * 0.008)
		else:
			pan(event.relative)
	elif event is InputEventMouseButton and event.pressed:
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			zoom_smooth(0.85)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			zoom_smooth(1.18)
	elif event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_Q: orbit(deg_to_rad(15))
			KEY_E: orbit(deg_to_rad(-15))
			KEY_W, KEY_UP: pan(Vector2(0, -30))
			KEY_S, KEY_DOWN: pan(Vector2(0, 30))
			KEY_A, KEY_LEFT: pan(Vector2(-30, 0))
			KEY_D, KEY_RIGHT: pan(Vector2(30, 0))
