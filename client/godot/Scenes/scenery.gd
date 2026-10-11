extends Node3D
## Cenário decorativo do mundo: árvores, pedras e água animada. Determinístico por célula (mesma
## célula, mesma árvore); nada aqui é dado do servidor, só apresentação sobre o terreno servido.

const Grid := preload("res://Scenes/grid.gd")

const WATER_SHADER := """
shader_type spatial;
render_mode cull_disabled;
uniform vec4 shallow : source_color = vec4(0.30, 0.58, 0.78, 0.82);
uniform vec4 deep : source_color = vec4(0.16, 0.38, 0.62, 0.9);
varying vec3 wpos;
void vertex() {
	wpos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}
void fragment() {
	float wave = sin(wpos.x * 1.7 + wpos.z * 0.9 + TIME * 1.3) * 0.5 + sin(wpos.z * 2.1 - wpos.x * 0.6 - TIME * 1.1) * 0.5;
	float mixv = 0.5 + 0.5 * wave;
	vec4 c = mix(deep, shallow, mixv);
	ALBEDO = c.rgb + vec3(0.06) * pow(mixv, 4.0);
	ALPHA = c.a;
	ROUGHNESS = 0.15;
	SPECULAR = 0.8;
}
"""


static func cell_hash(cell: Vector2i, salt: int = 0) -> float:
	var h: int = (cell.x * 73856093) ^ (cell.y * 19349663) ^ (salt * 83492791)
	h = (h ^ (h >> 13)) * 1274126177
	return float(abs(h) % 10000) / 10000.0


## `terrain`: Vector2i -> id; `rivers`: Vector2i -> true; `cities`: Array[Rect2i] (sem árvores dentro).
func build(terrain: Dictionary, rivers: Dictionary, cities: Array) -> void:
	for child in get_children():
		child.free()
	var trunks: Array = []
	var crowns: Array = []
	var rocks: Array = []
	var water: Array = []
	for cell in terrain:
		if rivers.has(cell):
			water.append(cell)
			continue
		var inside_city := false
		for rect in cities:
			if (rect as Rect2i).grow(1).has_point(cell):
				inside_city = true
				break
		if inside_city:
			continue
		var roll := cell_hash(cell)
		var jitter := Vector3((cell_hash(cell, 1) - 0.5) * 0.6, 0, (cell_hash(cell, 2) - 0.5) * 0.6)
		if roll < 0.16:
			var scale := 0.8 + cell_hash(cell, 3) * 0.7
			trunks.append(Transform3D(Basis().scaled(Vector3(1, scale, 1)), Grid.to_world(cell) + jitter))
			crowns.append([Transform3D(Basis().scaled(Vector3(scale, scale, scale)), Grid.to_world(cell, 0.55 * scale) + jitter),
				Color.from_hsv(0.28 + cell_hash(cell, 4) * 0.06, 0.55, 0.45 + cell_hash(cell, 5) * 0.2)])
		elif roll < 0.2:
			rocks.append(Transform3D(Basis().scaled(Vector3.ONE * (0.6 + cell_hash(cell, 6))), Grid.to_world(cell, 0.04) + jitter))
	_multi("Trunks", _cylinder(0.07, 0.5), Color("5b3d27"), trunks.map(func(t): return t), [], 0.25)
	_multi("Crowns", _cone(0.34, 0.9), Color.WHITE, crowns.map(func(c): return c[0]), crowns.map(func(c): return c[1]), 0.0)
	_multi("Rocks", _rock(), Color("8d9199"), rocks, [], 0.0)
	_water(water)


func _cylinder(radius: float, height: float) -> Mesh:
	var mesh := CylinderMesh.new()
	mesh.top_radius = radius * 0.8
	mesh.bottom_radius = radius
	mesh.height = height
	return mesh


func _cone(radius: float, height: float) -> Mesh:
	var mesh := CylinderMesh.new()
	mesh.top_radius = 0.0
	mesh.bottom_radius = radius
	mesh.height = height
	mesh.radial_segments = 8
	return mesh


func _rock() -> Mesh:
	var mesh := SphereMesh.new()
	mesh.radius = 0.16
	mesh.height = 0.22
	mesh.radial_segments = 6
	mesh.rings = 3
	return mesh


func _multi(node_name: String, mesh: Mesh, color: Color, transforms: Array, colors: Array, lift: float) -> void:
	if transforms.is_empty():
		return
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.use_colors = not colors.is_empty()
	multi.mesh = mesh
	multi.instance_count = transforms.size()
	for i in transforms.size():
		var xf: Transform3D = transforms[i]
		xf.origin.y += lift * xf.basis.y.y
		multi.set_instance_transform(i, xf)
		if not colors.is_empty():
			multi.set_instance_color(i, colors[i])
	var instance := MultiMeshInstance3D.new()
	instance.name = node_name
	instance.multimesh = multi
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.vertex_color_use_as_albedo = not colors.is_empty()
	material.roughness = 0.9
	instance.material_override = material
	add_child(instance)


func _water(cells: Array) -> void:
	if cells.is_empty():
		return
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	var quad := PlaneMesh.new()
	quad.size = Vector2(Grid.CELL, Grid.CELL)
	multi.mesh = quad
	multi.instance_count = cells.size()
	for i in cells.size():
		multi.set_instance_transform(i, Transform3D(Basis(), Grid.to_world(cells[i], -0.07)))
	var instance := MultiMeshInstance3D.new()
	instance.name = "Water"
	instance.multimesh = multi
	var shader := Shader.new()
	shader.code = WATER_SHADER
	var material := ShaderMaterial.new()
	material.shader = shader
	instance.material_override = material
	add_child(instance)
