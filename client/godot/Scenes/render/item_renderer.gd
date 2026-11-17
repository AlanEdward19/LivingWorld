extends Node3D
## Espólio no chão (AD-168/170 `leavings`) e objetos inventados (`objects`) desenhados com a forma e o material do design:
## o espólio é um saco com uma trouxa (e o recipiente, se havia) onde a pessoa morreu; a peça fora de casa (no chão ou
## numa banca) usa a forma da peça raiz e a cor do material de `/designs/{id}` (paleta pelo id até o design chegar). Peça
## na mão de alguém vai no corpo da pessoa; peça dentro de prédio não aparece aqui. Um MultiMesh por forma.

const Grid := preload("res://Scenes/grid.gd")
const Props := preload("res://Scenes/npc_props.gd")

const SHAPES := ["box", "cylinder", "sphere", "capsule"]
const MATERIAL_PALETTE: Array[Color] = [Color("8a6a43"), Color("9a9ea6"), Color("b0603a"), Color("c8b070"), Color("6a8a5a"),
	Color("5a6a8a"), Color("d8cfb8")]
const SIZE := 0.22

var store
var bounds_filter := Rect2i()
var design_of: Callable = func(_id: int) -> Variant: return null
var request_design: Callable = func(_id: int) -> void: pass
var positions: Dictionary = {} ## "leaving:id" | "item:id" -> Vector3 (seleção)
var _multis: Dictionary = {}


func _init() -> void:
	for shape in SHAPES:
		var multi := MultiMesh.new()
		multi.transform_format = MultiMesh.TRANSFORM_3D
		multi.use_colors = true
		multi.mesh = Props.shape_mesh(shape, 1.0)
		var instance := MultiMeshInstance3D.new()
		instance.name = "Items_%s" % shape
		instance.multimesh = multi
		instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		var material := StandardMaterial3D.new()
		material.vertex_color_use_as_albedo = true
		material.roughness = 0.8
		instance.material_override = material
		add_child(instance)
		_multis[shape] = instance


## Forma e cor de um design: {shape, color}; sem o design ainda, a forma da projeção e a paleta pelo material.
func look_of(design_id: int, shape: String, material: int) -> Dictionary:
	var body = design_of.call(design_id)
	if body is Dictionary:
		var parts: Array = body.get("parts", [])
		var materials: Array = body.get("materials", [])
		var drawn := String(parts[0].get("shape", shape)) if not parts.is_empty() else shape
		return {"shape": drawn if drawn in SHAPES else "box",
			"color": Color(String(materials[0].get("color", "#8a6a43"))) if not materials.is_empty() else MATERIAL_PALETTE[0]}
	if design_id > 0:
		request_design.call(design_id)
	return {"shape": shape if shape in SHAPES else "box", "color": MATERIAL_PALETTE[posmod(material, MATERIAL_PALETTE.size())]}


func _covered() -> Dictionary:
	var cells := {}
	for building in store.buildings.values():
		for entry in building.footprint:
			cells[building.cell + entry.cell] = true
	return cells


func _visible(cell: Vector2i) -> bool:
	return bounds_filter.size == Vector2i.ZERO or bounds_filter.has_point(cell)


func rebuild() -> void:
	positions.clear()
	var lists: Dictionary = {}
	for shape in SHAPES:
		lists[shape] = []
	for id in store.leavings:
		var leaving: Dictionary = store.leavings[id]
		if not _visible(leaving.cell):
			continue
		var at := Grid.to_world(leaving.cell) + Vector3(0.18, 0, -0.12)
		lists.sphere.append([Transform3D(Basis().scaled(Vector3(0.3, 0.24, 0.3)), at + Vector3(0, 0.12, 0)), Color("8a7048")])
		lists.box.append([Transform3D(Basis(Vector3.UP, 0.4).scaled(Vector3(0.26, 0.12, 0.18)), at + Vector3(0.2, 0.06, 0.1)), Color("6a5a48")])
		if int(leaving.design_id) > 0:
			var look := look_of(int(leaving.design_id), "cylinder", 0)
			lists[look.shape].append([Transform3D(Basis().scaled(Vector3.ONE * SIZE * 0.8), at + Vector3(-0.2, 0.09, 0.05)), look.color])
		positions["leaving:%d" % int(id)] = at + Vector3(0, 0.2, 0)
	var covered := _covered()
	for id in store.objects:
		var object: Dictionary = store.objects[id]
		if not object.has_cell or int(object.holder_id) >= 0 or covered.has(object.cell) or not _visible(object.cell):
			continue
		var look := look_of(int(object.design_id), String(object.shape), int(object.material))
		var at := Grid.to_world(object.cell) + Vector3(-0.2, 0, 0.2)
		lists[look.shape].append([Transform3D(Basis().scaled(Vector3.ONE * SIZE), at + Vector3(0, SIZE / 2.0, 0)), look.color])
		positions["item:%d" % int(id)] = at + Vector3(0, SIZE, 0)
	for shape in SHAPES:
		var list: Array = lists[shape]
		var multi: MultiMesh = _multis[shape].multimesh
		multi.instance_count = list.size()
		for i in list.size():
			multi.set_instance_transform(i, list[i][0])
			multi.set_instance_color(i, list[i][1])
		_multis[shape].visible = not list.is_empty()


func instance_count() -> int:
	var total := 0
	for shape in SHAPES:
		total += _multis[shape].multimesh.instance_count
	return total
