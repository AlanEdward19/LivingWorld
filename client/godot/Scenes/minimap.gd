extends Control
## Minimapa do mundo: terreno, rios, cidades, NPCs visíveis e o ponto de vista da câmera.
## Clicar leva a câmera até lá (só navegação local; nada é enviado ao servidor).

signal pan_requested(point: Vector3)

const SIZE := Vector2(210, 210)

var store
var camera_target := Vector3.ZERO
var _texture_rect: TextureRect
var _scale := 1.0
var _origin := Vector2.ZERO


func _init() -> void:
	custom_minimum_size = SIZE
	size = SIZE
	mouse_filter = Control.MOUSE_FILTER_STOP
	_texture_rect = TextureRect.new()
	_texture_rect.stretch_mode = TextureRect.STRETCH_SCALE
	_texture_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	_texture_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	_texture_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_texture_rect)


func bind(p_store) -> void:
	store = p_store
	store.reloaded.connect(func(_kind): rebuild())
	store.npc_changed.connect(func(_a, _b, _c, _d): queue_redraw())
	store.npc_removed.connect(func(_id): queue_redraw())
	store.cities_changed.connect(func(): queue_redraw())


func rebuild() -> void:
	if store == null or store.terrain.is_empty():
		return
	var map: Vector2i = store.map_size
	var image := Image.create(map.x, map.y, false, Image.FORMAT_RGBA8)
	for cell in store.terrain:
		var color := Color("6aa84f") if store.terrain[cell] == 1 else Color("c9b777")
		if store.rivers.has(cell):
			color = Color("4a86b8")
		image.set_pixelv(cell, color)
	_texture_rect.texture = ImageTexture.create_from_image(image)
	_layout(map)
	queue_redraw()


func _layout(map: Vector2i) -> void:
	_scale = minf(SIZE.x / map.x, SIZE.y / map.y)
	_origin = (SIZE - Vector2(map) * _scale) / 2.0
	_texture_rect.position = _origin
	_texture_rect.size = Vector2(map) * _scale


## Célula do mundo (float) -> posição no minimapa.
func to_minimap(world_point: Vector3) -> Vector2:
	return _origin + Vector2(world_point.x, world_point.z) * _scale


func to_world(minimap_point: Vector2) -> Vector3:
	var cell := (minimap_point - _origin) / _scale
	return Vector3(cell.x, 0, cell.y)


func set_camera_target(point: Vector3) -> void:
	camera_target = point
	queue_redraw()


func _gui_input(event: InputEvent) -> void:
	var pressed: bool = event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT
	var dragging: bool = event is InputEventMouseMotion and (event.button_mask & MOUSE_BUTTON_MASK_LEFT)
	if pressed or dragging:
		pan_requested.emit(to_world(event.position))
		accept_event()


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, SIZE), Color(0, 0, 0, 0.35))
	draw_rect(Rect2(Vector2.ZERO, SIZE), Color(1, 1, 1, 0.25), false, 1.5)
	if store == null:
		return
	for id in store.cities:
		var rect: Rect2i = store.cities[id].bounds
		draw_rect(Rect2(_origin + Vector2(rect.position) * _scale, Vector2(rect.size) * _scale), Color("ffcf4a"), false, 1.5)
	for id in store.npcs:
		var cell: Vector2i = store.npcs[id].cell
		draw_circle(_origin + (Vector2(cell) + Vector2(0.5, 0.5)) * _scale, 1.6, Color("ffffff"))
	var marker := to_minimap(camera_target)
	draw_rect(Rect2(marker - Vector2(9, 9), Vector2(18, 18)), Color("5fd0ff"), false, 2.0)
