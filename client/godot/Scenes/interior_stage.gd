extends Control
## Palco do interior: o exterior (mundo na viewport principal) fica escurecido e desfocado por uma
## camada de tela; o interior é desenhado nítido por cima, numa SubViewport com mundo 3D próprio e
## câmera espelhada da câmera do espectador.

const InteriorView := preload("res://Scenes/interior_view.gd")

var dim_rect: ColorRect
var interior_view
var active := false
var floor_label: Label

var _container: SubViewportContainer
var _viewport: SubViewport
var _camera: Camera3D
var _sun: DirectionalLight3D
var _environment: Environment
var _source: Camera3D


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_preset(Control.PRESET_FULL_RECT)
	dim_rect = ColorRect.new()
	dim_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	dim_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var shader := Shader.new()
	shader.code = """
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float blur_lod = 3.0;
uniform float darken = 0.38;
void fragment() {
	vec3 color = textureLod(screen_tex, SCREEN_UV, blur_lod).rgb;
	COLOR = vec4(color * darken, 1.0);
}
"""
	var material := ShaderMaterial.new()
	material.shader = shader
	dim_rect.material = material
	dim_rect.visible = false
	add_child(dim_rect)

	_container = SubViewportContainer.new()
	_container.set_anchors_preset(Control.PRESET_FULL_RECT)
	_container.stretch = true
	_container.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_container.visible = false
	add_child(_container)
	_viewport = SubViewport.new()
	_viewport.own_world_3d = true
	_viewport.transparent_bg = true
	_viewport.handle_input_locally = false
	_container.add_child(_viewport)
	_camera = Camera3D.new()
	_camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	_camera.far = 400.0
	_viewport.add_child(_camera)
	_sun = DirectionalLight3D.new()
	_sun.rotation = Vector3(deg_to_rad(-55), deg_to_rad(30), 0)
	_viewport.add_child(_sun)
	var environment := WorldEnvironment.new()
	_environment = Environment.new()
	_environment.background_mode = Environment.BG_CLEAR_COLOR
	_environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment = _environment
	_viewport.add_child(environment)
	set_daylight(1.0)
	interior_view = InteriorView.new()
	_viewport.add_child(interior_view)
	floor_label = Label.new()
	floor_label.visible = false
	floor_label.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
	floor_label.position = Vector2(-120, -70)
	floor_label.add_theme_font_size_override("font_size", 22)
	add_child(floor_label)


func enter(info: Dictionary, origin: Vector2i, labels, source_camera: Camera3D, building_type: int = -1) -> void:
	_source = source_camera
	interior_view.show_interior(info, origin, labels, building_type)
	dim_rect.visible = true
	_container.visible = true
	active = true
	_update_floor_label()
	_sync_camera()


## Luz do interior segue o dia: de noite fica à luz de lamparina (quente e baixa).
func set_daylight(light: float) -> void:
	_sun.light_energy = 0.25 + 0.75 * light
	_sun.light_color = Color(1.0, 0.78, 0.55).lerp(Color(1, 0.97, 0.92), light)
	_environment.ambient_light_color = Color(0.85, 0.65, 0.5).lerp(Color(0.75, 0.75, 0.8), light)
	_environment.ambient_light_energy = 0.55 + 0.15 * light


func leave() -> void:
	dim_rect.visible = false
	_container.visible = false
	floor_label.visible = false
	active = false


func set_floor(level: int) -> void:
	interior_view.set_floor(level)
	_update_floor_label()


func _update_floor_label() -> void:
	floor_label.visible = active
	floor_label.text = "Andar %d de %d  (PgUp/PgDn)" % [interior_view.current_floor + 1, interior_view.floor_count]


func _process(_delta: float) -> void:
	if active:
		_sync_camera()


func _sync_camera() -> void:
	if not is_inside_tree() or _source == null or not is_instance_valid(_source) or not _source.is_inside_tree():
		return
	_camera.global_transform = _source.global_transform
	_camera.size = _source.size
	interior_view.set_view_direction(_source.global_transform.basis.z)
