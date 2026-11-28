extends Node3D
## #234: o anel de alcance do golpe (`reachMeters` do motor) em volta do corpo, só enquanto a mira está numa pessoa. Um nó só,
## que segue o corpo; sem `_process` enquanto escondido (não custa quadro sem alvo).

const Grid := preload("res://Scenes/grid.gd")
const WIDTH := 0.03 ## espessura do anel em unidades do mundo

var follow: Node3D
var reach_m := 0.0
var ring: MeshInstance3D


func _init() -> void:
	visible = false
	set_process(false)
	ring = MeshInstance3D.new()
	ring.mesh = TorusMesh.new()
	ring.scale = Vector3(1.0, 0.2, 1.0)
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.albedo_color = Color(1.0, 0.55, 0.4, 0.45)
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	ring.material_override = material
	ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(ring)


## O anel de `meters` de raio em volta de `body` (o corpo do jogador).
func show_around(body: Node3D, meters: float) -> void:
	if body == null or meters <= 0.0:
		clear()
		return
	follow = body
	reach_m = meters
	var radius := Grid.units(meters)
	var mesh: TorusMesh = ring.mesh
	mesh.outer_radius = radius + WIDTH * 0.5
	mesh.inner_radius = maxf(radius - WIDTH * 0.5, 0.01)
	position = body.position + Vector3(0, 0.05, 0)
	visible = true
	set_process(true)


func clear() -> void:
	follow = null
	visible = false
	set_process(false)


func radius_units() -> float:
	return Grid.units(reach_m) if visible else 0.0


func _process(_delta: float) -> void:
	if follow == null or not is_instance_valid(follow):
		clear()
		return
	position = follow.position + Vector3(0, 0.05, 0)
