extends Node3D
## #233: o realce do que está na mira: um único anel brilhante no chão sob a pessoa ou coisa que o E alcança. Um nó só, movido
## para o alvo (nunca um por NPC) e sem `_process` enquanto escondido: não custa quadro nenhum sem alvo.

const Grid := preload("res://Scenes/grid.gd")

var kind := ""
var target_id := -1
var ring: MeshInstance3D
var _t := 0.0


func _init() -> void:
	visible = false
	set_process(false)
	ring = MeshInstance3D.new()
	var mesh := TorusMesh.new()
	mesh.inner_radius = 0.34
	mesh.outer_radius = 0.42
	mesh.rings = 24
	mesh.ring_segments = 6
	ring.mesh = mesh
	ring.scale = Vector3(1.0, 0.12, 1.0)
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.albedo_color = Color(1.0, 0.92, 0.55, 0.9)
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.no_depth_test = true
	ring.material_override = material
	ring.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(ring)


## Onde fica o alvo no mundo: a pessoa onde ela está desenhada; a coisa na célula que o motor ofereceu.
static func spot(cell: Vector2, person_at: Variant, base_y: float) -> Vector3:
	if person_at is Vector3:
		return Vector3(person_at.x, base_y + 0.05, person_at.z)
	return Vector3((cell.x + 0.5) * Grid.CELL, base_y + 0.05, (cell.y + 0.5) * Grid.CELL)


func show_at(p_kind: String, id: int, at: Vector3) -> void:
	kind = p_kind
	target_id = id
	position = at
	visible = true
	set_process(true)


func clear() -> void:
	kind = ""
	target_id = -1
	visible = false
	set_process(false)


func _process(delta: float) -> void:
	_t += delta
	var pulse := 1.0 + 0.08 * sin(_t * 5.0)
	ring.scale = Vector3(pulse, 0.12, pulse)
