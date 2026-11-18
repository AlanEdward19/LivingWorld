extends Node3D
## O que cai do céu, pelo tempo do dia do motor (AD-180): chuva (gotas esticadas), neve (flocos) e granizo (pedrinhas rápidas),
## com a quantidade pela intensidade e um teto fixo (`Look.RAIN_CAP`/`SNOW_CAP`). Duas GPUParticles3D com malha e material
## próprios, sem sombra, presas ao ponto de vista; LOD por `amount_ratio` (não reinicia as partículas): mapa-múndi afastado
## nada, câmera longe metade. O vento do motor inclina a queda (a direção não vem no contrato: inclina sempre para o mesmo lado).

const Look := preload("res://Scenes/render/weather_look.gd")

var rain: GPUParticles3D
var snow: GPUParticles3D
var weather: Dictionary = {}
var ratio := 0.0 ## testável: fração das partículas ligadas agora (intensidade × LOD)
var _lod := 1.0
var _extent := 22.0
var _key := "-"


func _init() -> void:
	rain = _particles("Rain", Look.RAIN_CAP, 1.1, _drop_mesh(Vector3(0.012, 0.28, 0.012), Color(0.72, 0.8, 0.9, 0.55)))
	snow = _particles("Snow", Look.SNOW_CAP, 5.0, _flake_mesh())
	add_child(rain)
	add_child(snow)


## Tempo novo (escopo ou região sob a câmera).
func apply(p_weather: Dictionary) -> void:
	weather = p_weather
	var fall := Look.falling(weather)
	# o clima chega a cada tick (a temperatura da hora muda): só reconfigura quando o que cai ou o vento mudou
	var key := "%s|%s|%s|%d" % [fall.kind, fall.ratio, fall.hail, roundi(float(weather.get("wind", 0.0)))]
	if key == _key:
		return
	_key = key
	var wind := clampf(float(weather.get("wind", 0.0)) / 20.0, 0.0, 1.0)
	_configure(rain.process_material, Vector3(wind * 0.6, -1, 0), 14.0, 18.0, 0.0)
	if bool(fall.hail):
		_configure(snow.process_material, Vector3(wind * 0.3, -1, 0), 9.0, 12.0, 0.0)
		snow.draw_pass_1.radius = 0.05
		snow.draw_pass_1.height = 0.1
	else:
		_configure(snow.process_material, Vector3(0.1 + wind * 0.9, -1, 0), 1.6, 2.4, 0.4)
		snow.draw_pass_1.radius = 0.04
		snow.draw_pass_1.height = 0.08
	_refresh()


## LOD e posição pelo ponto de vista (o app chama a cada quadro; só mexe no que mudou).
func follow(center: Vector3, radius: float, lod: float) -> void:
	if not is_equal_approx(lod, _lod):
		_lod = lod
		_refresh()
	if ratio <= 0.0:
		return
	position = center + Vector3(0, 12, 0)
	var extent := clampf(radius, 8.0, 30.0)
	if absf(extent - _extent) > 2.0:
		_extent = extent
		for node in [rain, snow]:
			(node.process_material as ParticleProcessMaterial).emission_box_extents = Vector3(extent, 0.5, extent)
			node.visibility_aabb = AABB(Vector3(-extent - 4, -16, -extent - 4), Vector3(extent * 2 + 8, 20, extent * 2 + 8))


func _refresh() -> void:
	var fall := Look.falling(weather)
	ratio = float(fall.ratio) * _lod
	var rain_on: bool = ratio > 0.0 and fall.kind == "rain"
	var snow_on: bool = ratio > 0.0 and (fall.kind == "snow" or bool(fall.hail))
	rain.amount_ratio = ratio if rain_on else 0.0
	snow.amount_ratio = (ratio if fall.kind == "snow" else ratio * 0.3) if snow_on else 0.0
	for pair in [[rain, rain_on], [snow, snow_on]]:
		pair[0].emitting = pair[1]
		pair[0].visible = pair[1]


static func _configure(process: ParticleProcessMaterial, direction: Vector3, v_min: float, v_max: float, gravity: float) -> void:
	process.direction = direction.normalized()
	process.initial_velocity_min = v_min
	process.initial_velocity_max = v_max
	process.gravity = Vector3(0, -gravity, 0)


func _particles(label: String, amount: int, lifetime: float, mesh: Mesh) -> GPUParticles3D:
	var particles := GPUParticles3D.new()
	particles.name = label
	particles.amount = amount
	particles.amount_ratio = 0.0
	particles.lifetime = lifetime
	particles.emitting = false
	particles.visible = false
	particles.visibility_aabb = AABB(Vector3(-26, -16, -26), Vector3(52, 20, 52))
	var process := ParticleProcessMaterial.new()
	process.emission_shape = ParticleProcessMaterial.EMISSION_SHAPE_BOX
	process.emission_box_extents = Vector3(_extent, 0.5, _extent)
	process.spread = 4.0
	process.particle_flag_align_y = true # a gota acompanha a direção da queda
	particles.process_material = process
	particles.draw_pass_1 = mesh
	particles.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	return particles


static func _drop_mesh(size: Vector3, color: Color) -> Mesh:
	var drop := BoxMesh.new()
	drop.size = size
	drop.material = _unshaded(color)
	return drop


static func _flake_mesh() -> Mesh:
	var flake := SphereMesh.new()
	flake.radius = 0.04
	flake.height = 0.08
	flake.radial_segments = 4
	flake.rings = 2
	flake.material = _unshaded(Color(1, 1, 1, 0.9))
	return flake


static func _unshaded(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA if color.a < 1.0 else BaseMaterial3D.TRANSPARENCY_DISABLED
	return material
