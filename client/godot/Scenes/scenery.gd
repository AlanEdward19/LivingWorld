extends Node3D
## Cenário decorativo do mundo: árvores, pedras e água animada. Determinístico por célula (mesma
## célula, mesma árvore); nada aqui é dado do servidor, só apresentação sobre o terreno servido.

const Grid := preload("res://Scenes/grid.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
const TREE_CHANCE := 0.16 ## #241: o motor (PlayerNature.TreeAt) usa o mesmo sorteio e o mesmo cell_hash: a oferta de cortar lenha só existe onde se desenha árvore
const TREE_JITTER := 0.6 ## deslocamento do tronco na célula (±0,3)

var _crown_colors: Array = [] ## cor de verão de cada copa (a estação recolore a partir dela)
var _crown_transforms: Array = [] ## AD-189: a copa cheia de cada árvore (a folhagem do motor encolhe a partir dela)
var crown_scale := 1.0 ## testável: tamanho da copa agora (folhagem do motor)
const TRUNK_LIFT := 0.25 ## o tronco é desenhado com o pé no chão: a malha sobe meia altura
const STUMP_HEIGHT := 0.22 ## #243: o toco é este tanto da altura do tronco
const CUT_CROWN_MIN := 0.4 ## #243: copa da árvore quase toda cortada (a fração do que resta a leva até 1)
var _trunk_raw: Array = [] ## #243: o tronco de cada árvore sem a elevação (para encolher até o toco)
var _tree_index: Dictionary = {} ## célula -> índice da árvore nas malhas (as que o cliente desenha pela regra do motor)
var _crown_factor: PackedFloat32Array = PackedFloat32Array() ## #243: quanto da copa resta (1 inteira, 0 sem copa), do estado do motor
var _stage_of: Dictionary = {} ## índice -> "cut" | "stump" aplicados agora
var clear_zones: Array = [] ## #258: onde o motor diz que não há árvore (quintal limpo, rua e calçada): [{x, y, w, h}]
var _hidden: Dictionary = {} ## índice da árvore -> true: a árvore que a regra do cliente poria mas o motor não aceita (em zona limpa)
var trees_state: Array = [] ## #243: o que o motor diz das árvores tocadas ({x, y, left, capacity, stage}), reaplicado quando o terreno se refaz

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
	_tree_index = {}
	_stage_of = {}
	_hidden = {}
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
		var jitter := Vector3((cell_hash(cell, 1) - 0.5) * TREE_JITTER, 0, (cell_hash(cell, 2) - 0.5) * TREE_JITTER)
		if roll < TREE_CHANCE:
			var scale := 0.8 + cell_hash(cell, 3) * 0.7
			_tree_index[cell] = trunks.size()
			trunks.append(Transform3D(Basis().scaled(Vector3(1, scale, 1)), Grid.to_world(cell) + jitter))
			crowns.append([Transform3D(Basis().scaled(Vector3(scale, scale, scale)), Grid.to_world(cell, 0.55 * scale) + jitter),
				Color.from_hsv(0.28 + cell_hash(cell, 4) * 0.06, 0.55, 0.45 + cell_hash(cell, 5) * 0.2)])
		elif roll < 0.2:
			rocks.append(Transform3D(Basis().scaled(Vector3.ONE * (0.6 + cell_hash(cell, 6))), Grid.to_world(cell, 0.04) + jitter))
	_trunk_raw = trunks.duplicate()
	_crown_factor = PackedFloat32Array()
	_crown_factor.resize(trunks.size())
	_crown_factor.fill(1.0)
	_multi("Trunks", _cylinder(0.07, 0.5), Color("5b3d27"), trunks.map(func(t): return t), [], TRUNK_LIFT)
	_crown_colors = crowns.map(func(c): return c[1])
	_crown_transforms = crowns.map(func(c): return c[0])
	_multi("Crowns", _cone(0.34, 0.9), Color.WHITE, crowns.map(func(c): return c[0]), _crown_colors, 0.0)
	crown_scale = 1.0 # a malha nova nasce cheia
	_multi("Rocks", _rock(), Color("8d9199"), rocks, [], 0.0)
	_water(water)
	apply_clear(clear_zones) # #258: e a que o motor não aceita (zona limpa) segue escondida
	apply_trees(trees_state) # #243: o terreno refeito volta a mostrar as árvores cortadas


## Copas pela folhagem do motor (AD-189: cor do outono pelo quanto virou, copa rala e nua pela fração; neve; seca amarelada).
func apply_season(climate: Dictionary) -> void:
	var node := get_node_or_null("Crowns") as MultiMeshInstance3D
	if node == null:
		return
	var scale := Season.crown_scale(climate)
	var resize := not is_equal_approx(scale, crown_scale)
	crown_scale = scale
	for i in _crown_colors.size():
		node.multimesh.set_instance_color(i, Season.crown(_crown_colors[i], climate, fmod(float(i) * 0.618, 1.0)))
		if resize and i < _crown_transforms.size():
			_place_crown(node.multimesh, i)


## A copa `i` pelo tamanho da folhagem do motor e pelo que resta da árvore cortada (#243).
func _place_crown(multi: MultiMesh, i: int) -> void:
	var full: Transform3D = _crown_transforms[i]
	var factor := _crown_factor[i] if i < _crown_factor.size() else 1.0
	multi.set_instance_transform(i, Transform3D(full.basis.scaled(Vector3.ONE * maxf(crown_scale * factor, 0.0001)), full.origin))


## #243: o estado das árvores tocadas, como o motor o diz (`GET /world/trees`: x, y, left, capacity, stage). A árvore inteira segue a regra
## do cliente (nada a desenhar); a cortada ganha copa menor, na proporção do que resta; o toco perde a copa e o tronco encolhe. O que
## deixou de estar na lista (cresceu de volta) volta inteiro. Só mexe nas instâncias que mudaram: custo por quadro zero.
func apply_trees(list: Array) -> void:
	trees_state = list
	var crowns := get_node_or_null("Crowns") as MultiMeshInstance3D
	var trunks := get_node_or_null("Trunks") as MultiMeshInstance3D
	var wanted: Dictionary = {}
	for tree in list:
		if not tree is Dictionary:
			continue
		var cell := Vector2i(int(tree.get("x", 0)), int(tree.get("y", 0)))
		if not _tree_index.has(cell) or _hidden.has(int(_tree_index[cell])):
			continue # a regra do cliente não põe árvore aqui (ou o motor a tirou): nada a mudar
		wanted[int(_tree_index[cell])] = tree
	for i in _stage_of.keys():
		if not wanted.has(i):
			_restore_tree(i, crowns, trunks)
	for i in wanted:
		var tree: Dictionary = wanted[i]
		var stage := String(tree.get("stage", "whole"))
		var capacity := maxf(float(tree.get("capacity", 1)), 1.0)
		var factor := 1.0
		if stage == "stump":
			factor = 0.0
		elif stage == "cut":
			factor = lerpf(CUT_CROWN_MIN, 1.0, clampf(float(tree.get("left", capacity)) / capacity, 0.0, 1.0))
		if _crown_factor[i] == factor and String(_stage_of.get(i, "")) == stage:
			continue
		_crown_factor[i] = factor
		_stage_of[i] = stage
		if crowns != null:
			_place_crown(crowns.multimesh, i)
		if trunks != null:
			_place_trunk(trunks.multimesh, i, stage == "stump")


## #258: as zonas sem árvore que o motor diz (`GET /world/trees`: `clear`). A árvore que a regra do cliente poria dentro de uma zona some (copa e tronco); a que
## deixou de estar numa zona volta. Sem zonas, nada a fazer: o mundo de antes não paga nada.
func apply_clear(zones: Array) -> void:
	clear_zones = zones
	var crowns := get_node_or_null("Crowns") as MultiMeshInstance3D
	var trunks := get_node_or_null("Trunks") as MultiMeshInstance3D
	var want: Dictionary = {}
	if not zones.is_empty():
		for cell in _tree_index:
			for zone in zones:
				if zone is Dictionary and cell.x >= int(zone.get("x", 0)) and cell.x < int(zone.get("x", 0)) + int(zone.get("w", 0)) \
						and cell.y >= int(zone.get("y", 0)) and cell.y < int(zone.get("y", 0)) + int(zone.get("h", 0)):
					want[int(_tree_index[cell])] = true
					break
	for i in _hidden.keys():
		if not want.has(i):
			_hidden.erase(i)
			_restore_tree(i, crowns, trunks)
	for i in want:
		if _hidden.has(i):
			continue
		_hidden[i] = true
		_crown_factor[i] = 0.0
		if crowns != null:
			_place_crown(crowns.multimesh, i)
		if trunks != null:
			_place_trunk(trunks.multimesh, i, false)


## Quantas árvores da regra do cliente o motor deixou de aceitar (zona limpa) (testável).
func hidden_count() -> int:
	return _hidden.size()


func _restore_tree(i: int, crowns: MultiMeshInstance3D, trunks: MultiMeshInstance3D) -> void:
	_crown_factor[i] = 1.0
	_stage_of.erase(i)
	if crowns != null:
		_place_crown(crowns.multimesh, i)
	if trunks != null:
		_place_trunk(trunks.multimesh, i, false)


func _place_trunk(multi: MultiMesh, i: int, stump: bool) -> void:
	var xf: Transform3D = _trunk_raw[i]
	if _hidden.has(i):
		xf.basis = xf.basis.scaled(Vector3.ONE * 0.0001) # #258: o motor não aceita árvore aqui
	elif stump:
		xf.basis = xf.basis.scaled(Vector3(1.0, STUMP_HEIGHT, 1.0))
	xf.origin.y += TRUNK_LIFT * xf.basis.y.y
	multi.set_instance_transform(i, xf)


## O estágio desenhado da árvore da célula ("whole", "cut", "stump"), ou "" se o cliente não põe árvore ali (testável).
func stage_at(cell: Vector2i) -> String:
	if not _tree_index.has(cell):
		return ""
	if _hidden.has(int(_tree_index[cell])):
		return "cleared"
	return String(_stage_of.get(int(_tree_index[cell]), "whole"))


## O fator de copa da árvore da célula (1 inteira, 0 toco), ou -1 sem árvore (testável).
func crown_factor_at(cell: Vector2i) -> float:
	return float(_crown_factor[int(_tree_index[cell])]) if _tree_index.has(cell) else -1.0


## Quantas árvores o cliente desenha pela regra (testável).
func tree_count() -> int:
	return _tree_index.size()


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
