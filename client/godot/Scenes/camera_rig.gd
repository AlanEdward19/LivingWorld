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
var city_enter_size := 9.0
var city_exit_size := 13.0
var scope_kind := "World"
## Injetada pelo app: ponto do mundo -> id da cidade sob ele ("" se nenhuma).
var city_at: Callable = func(_point: Vector3) -> String: return ""

var camera: Camera3D
var following: Node3D = null
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


func zoom_by(factor: float) -> void:
	zoom_size = clampf(zoom_size * factor, min_size, max_size)
	_apply()
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
	zoom_size = clampf(size, min_size, max_size)
	_apply()


func follow(node: Node3D) -> void:
	following = node


func release() -> void:
	following = null


func _process(delta: float) -> void:
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
			zoom_by(0.9)
		elif event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			zoom_by(1.1)
	elif event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_Q: orbit(deg_to_rad(15))
			KEY_E: orbit(deg_to_rad(-15))
			KEY_W, KEY_UP: pan(Vector2(0, -30))
			KEY_S, KEY_DOWN: pan(Vector2(0, 30))
			KEY_A, KEY_LEFT: pan(Vector2(-30, 0))
			KEY_D, KEY_RIGHT: pan(Vector2(30, 0))
