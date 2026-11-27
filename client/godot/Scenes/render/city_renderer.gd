extends Node3D
## Regiões das cidades no mapa-múndi: chão translúcido, nome e população, e o corpo clicável (meta `city_id`). Com a
## densidade do AD-170 a cidade deixa de ser vazia: um ponto por morador na rua (até MAX_DOTS por cidade, um único
## MultiMesh para todas), na cor da ação mais comum, e a placa diz quantos estão dentro, no pool e o que fazem.

const Grid := preload("res://Scenes/grid.gd")

const MAX_DOTS := 48
const ACTION_COLORS := {0: Color("e0a24f"), 1: Color("7fa8e0"), 2: Color("c9a36b"), 3: Color("ffd84a"), 4: Color("d8d8d8"),
	5: Color("b0b0b0"), 6: Color("7be08a"), 7: Color("9f7fe0"), 8: Color("ff6b5a"), 9: Color("8fc8ff"), -1: Color("a0a0a0")}

var nodes: Dictionary = {} ## id -> Node3D
var labels ## ActionLabels (rótulo da ação na placa); nulo = número
var dot_count := 0
var _dots: MultiMeshInstance3D


func _init() -> void:
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.use_colors = true
	var mesh := CapsuleMesh.new()
	mesh.radius = 0.16
	mesh.height = 0.6
	mesh.radial_segments = 6
	mesh.rings = 2
	multi.mesh = mesh
	_dots = MultiMeshInstance3D.new()
	_dots.name = "CityDensity"
	_dots.multimesh = multi
	_dots.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_dots.material_override = material
	add_child(_dots)


## Placa da cidade: nome, população, quem está na cidade (dentro dos limites) e, do que o motor diz, quem está na rua (fora de prédio), o pool, as
## ações mais comuns e a temperatura.
func label_text(info: Dictionary) -> String:
	var lines: Array[String] = ["%s · %d hab." % [info.name if info.name != "" else "Cidade", int(info.population)]]
	var density: Dictionary = info.get("density", {})
	if density.is_empty():
		return lines[0]
	var outdoors := int(density.get("outdoors", -1))
	var where := "%d na cidade" % int(density.inside) if outdoors < 0 else "%d na cidade · %d na rua" % [int(density.inside), outdoors]
	lines.append("%s · %d no pool · %d °C" % [where, int(density.pooled), roundi(float(density.temperature))])
	var doing: Array[String] = []
	for entry in density.actions:
		var action := int(entry.action)
		var name: String = labels.label_for(action) if labels != null and action >= 0 else ("parado" if action < 0 else "ação %d" % action)
		doing.append("%s %d" % [name.to_lower(), int(entry.count)])
	if not doing.is_empty():
		lines.append(" · ".join(doing))
	return "\n".join(lines)


## Pontos da multidão de uma cidade: cor por ação na proporção das contagens, lugar estável por cidade e índice.
static func dots_of(info: Dictionary) -> Array:
	var density: Dictionary = info.get("density", {})
	var inside := int(density.get("inside", 0))
	if inside <= 0:
		return []
	var shown := mini(inside, MAX_DOTS)
	var colors: Array = []
	var counted := 0
	for entry in density.get("actions", []):
		counted += int(entry.count)
	for entry in density.get("actions", []):
		var share := roundi(float(entry.count) / maxf(counted, 1) * shown)
		for i in share:
			colors.append(ACTION_COLORS.get(int(entry.action), ACTION_COLORS[-1]))
	while colors.size() < shown:
		colors.append(ACTION_COLORS[-1])
	var bounds: Rect2i = info.bounds
	var out: Array = []
	var seed_value := absi(hash(String(info.id)))
	for i in shown:
		var fx := fmod(float(seed_value % 997) * 0.013 + i * 0.6180339, 1.0)
		var fy := fmod(float(seed_value % 991) * 0.017 + i * 0.7548776, 1.0)
		var at := Vector3(bounds.position.x + 0.5 + fx * maxf(bounds.size.x - 1, 0.1), 0.3, bounds.position.y + 0.5 + fy * maxf(bounds.size.y - 1, 0.1))
		out.append([at, colors[i]])
	return out


func rebuild(store) -> void:
	for node in nodes.values():
		if node.is_inside_tree():
			node.queue_free()
		else:
			node.free()
	nodes.clear()
	var dots: Array = []
	if store.kind != "World":
		_set_dots(dots)
		return
	for id in store.cities:
		var info: Dictionary = store.cities[id]
		var root := Node3D.new()
		root.name = "City_%s" % str(id).left(8)
		root.set_meta("city_id", id)
		var bounds: Rect2i = info.bounds
		var plane := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(bounds.size.x, 0.06, bounds.size.y)
		plane.mesh = box
		var material := StandardMaterial3D.new()
		material.albedo_color = Color(1.0, 0.8, 0.3, 0.35)
		material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		plane.material_override = material
		plane.position = Vector3(bounds.position.x + bounds.size.x / 2.0, 0.03, bounds.position.y + bounds.size.y / 2.0)
		root.add_child(plane)
		var label := Label3D.new()
		label.name = "CityLabel"
		label.text = label_text(info)
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		label.pixel_size = 0.012
		label.font_size = 32
		label.outline_size = 12
		label.outline_modulate = Color(0.05, 0.05, 0.08, 0.95)
		label.no_depth_test = true
		label.position = Vector3(info.location.x + 0.5, 1.6, info.location.y + 0.5)
		root.add_child(label)
		var body := StaticBody3D.new()
		body.set_meta("city_id", id)
		var shape := CollisionShape3D.new()
		var collider := BoxShape3D.new()
		collider.size = Vector3(bounds.size.x, 1.0, bounds.size.y)
		shape.shape = collider
		shape.position = plane.position + Vector3(0, 0.5, 0)
		body.add_child(shape)
		root.add_child(body)
		add_child(root)
		nodes[id] = root
		dots.append_array(dots_of(info))
	_set_dots(dots)


func _set_dots(dots: Array) -> void:
	var multi: MultiMesh = _dots.multimesh
	multi.instance_count = dots.size()
	for i in dots.size():
		multi.set_instance_transform(i, Transform3D(Basis(), dots[i][0]))
		multi.set_instance_color(i, dots[i][1])
	dot_count = dots.size()
	_dots.visible = dot_count > 0
