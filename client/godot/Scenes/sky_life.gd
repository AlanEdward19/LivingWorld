extends Node3D
## Vida no céu: sombras de nuvens que passam devagar sobre o mundo (as nuvens em si não são desenhadas,
## para não tapar a vista isométrica). Só apresentação.

const CLOUD_COUNT := 9
const HEIGHT := 14.0

var map_size := Vector2(30, 30)
var clouds: Array[Node3D] = []
var speed := 0.35
var paused := false ## simulação pausada: as nuvens também param


func build(p_map_size: Vector2) -> void:
	map_size = p_map_size
	for cloud in clouds:
		cloud.free()
	clouds.clear()
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(1, 1, 1, 0.92)
	material.roughness = 1.0
	for i in CLOUD_COUNT:
		var cloud := Node3D.new()
		var puffs := 3 + i % 3
		for j in puffs:
			var puff := MeshInstance3D.new()
			var sphere := SphereMesh.new()
			sphere.radius = 1.4 + float((i * 7 + j * 3) % 5) * 0.25
			sphere.height = sphere.radius * 1.3
			puff.mesh = sphere
			puff.material_override = material
			puff.position = Vector3(j * 1.8 - puffs * 0.9, float(j % 2) * 0.3, float((i + j) % 3) * 0.7)
			puff.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_SHADOWS_ONLY
			cloud.add_child(puff)
		cloud.position = Vector3(fmod(i * 11.3, map_size.x * 1.6) - map_size.x * 0.3, HEIGHT + float(i % 3), fmod(i * 7.7, map_size.y * 1.4))
		add_child(cloud)
		clouds.append(cloud)


func advance(delta: float) -> void:
	if paused:
		return
	for cloud in clouds:
		cloud.position.x += speed * delta
		if cloud.position.x > map_size.x * 1.3:
			cloud.position.x = -map_size.x * 0.3


func _process(delta: float) -> void:
	advance(delta)
