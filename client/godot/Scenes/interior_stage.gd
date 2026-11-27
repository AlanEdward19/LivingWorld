extends Control
## #227: foco no interior. A casa é desenhada no próprio mundo, no lugar do prédio e na escala do mundo (mesma câmera, mesmo
## sol, mesmas unidades): não há SubViewport nem outra escala. O resto do mundo deixa de ser desenhado e atualizado (o quadro
## fica mais barato dentro da casa) e um fundo suave e escurecido, sem geometria, faz o papel do mundo fora de foco.

const InteriorView := preload("res://Scenes/interior_view.gd")

var interior_view
var active := false
var floor_label: Label
var world: Node3D ## o mundo desenhado (WorldView): escondido e parado enquanto a casa está em foco
var backdrop: MeshInstance3D ## o mundo fora de foco: chão desfocado que escurece para as bordas

var _material: ShaderMaterial
var _source: Camera3D
var _world_process_mode := Node.PROCESS_MODE_INHERIT
var _light := 1.0


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_preset(Control.PRESET_FULL_RECT)
	var shader := Shader.new()
	shader.code = """
shader_type spatial;
render_mode unshaded, cull_disabled;
uniform vec3 center = vec3(0.0);
uniform vec3 tint : source_color = vec3(0.34, 0.42, 0.28);
uniform float radius = 18.0;
varying vec3 pos;
void vertex() { pos = VERTEX; }
void fragment() {
	float d = length(pos.xz - center.xz) / radius;
	float soft = smoothstep(0.0, 1.0, d);
	ALBEDO = mix(tint, tint * 0.18, soft);
}
"""
	_material = ShaderMaterial.new()
	_material.shader = shader
	backdrop = MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(160, 160)
	backdrop.mesh = plane
	backdrop.material_override = _material
	backdrop.position.y = -0.04
	backdrop.visible = false
	backdrop.name = "Backdrop"
	add_child(backdrop)
	interior_view = InteriorView.new()
	add_child(interior_view)
	floor_label = Label.new()
	floor_label.visible = false
	floor_label.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
	floor_label.position = Vector2(-120, -70)
	floor_label.add_theme_font_size_override("font_size", 22)
	add_child(floor_label)


func enter(info: Dictionary, origin: Vector2i, labels, source_camera: Camera3D, building_type: int = -1, lodging_units: int = 0) -> void:
	_source = source_camera
	interior_view.show_interior(info, origin, labels, building_type, lodging_units)
	_hide_world()
	var middle: Vector3 = interior_view.center()
	backdrop.position = Vector3(middle.x, -0.04, middle.z)
	_material.set_shader_parameter("center", middle)
	backdrop.visible = true
	active = true
	_update_floor_label()
	_sync_camera()


## O mundo some do desenho e do `_process` (custo de quadro cai); volta igual ao sair.
func _hide_world() -> void:
	if world == null or not is_instance_valid(world) or not world.visible:
		return
	_world_process_mode = world.process_mode
	world.visible = false
	world.process_mode = Node.PROCESS_MODE_DISABLED


func _show_world() -> void:
	if world == null or not is_instance_valid(world) or world.visible:
		return
	world.process_mode = _world_process_mode
	world.visible = true


## O sol e o céu são os do mundo; aqui só o fundo acompanha o dia.
func set_daylight(light: float) -> void:
	_light = light
	_material.set_shader_parameter("tint", Vector3(0.34, 0.42, 0.28) * (0.35 + 0.65 * light))


## A câmera é a do mundo (a seleção projeta ocupantes e objetos com ela).
func camera() -> Camera3D:
	return _source


func leave() -> void:
	interior_view.immersive = false
	backdrop.visible = false
	floor_label.visible = false
	active = false
	_show_world()


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
	interior_view.set_view_direction(_source.global_transform.basis.z)
