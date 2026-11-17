extends Node3D
## Lavoura (AD-170 `crops`): cada lote é um canteiro na célula da lavoura, desenhado pelo estágio do motor — sem semear
## (sulcos), plantado (brotos), crescendo (altura pela fração), maduro (cor da cultura), colhido (restolho), pousio (mato
## ralo) — e com sede (amarelado). Dois MultiMesh (chão e plantas): uma chamada de desenho cada, refeitos só quando a
## lavoura muda. `positions` serve à seleção.

const Grid := preload("res://Scenes/grid.gd")

const PLANTS_PER_ROW := 3
const SOIL := {"unsown": Color("5a3d24"), "planted": Color("6b4a2e"), "growing": Color("6b4a2e"), "mature": Color("6b4a2e"),
	"harvested": Color("8a7048"), "fallow": Color("6f6a3e")}
const SPROUT := Color("9fcf6a")
const GREEN := Color("5f9a3a")
## Cultura pelo nome do recurso (catálogo do período): [palavras, cor madura, altura madura].
const CULTURES := [
	[["trigo", "wheat", "cevada", "barley", "centeio", "rye", "aveia", "oat", "grão", "grain"], Color("d9b44a"), 0.55],
	[["milho", "corn", "maize"], Color("d8c25a"), 0.85],
	[["arroz", "rice"], Color("b8cf7a"), 0.45],
	[["feij", "bean", "ervilh", "pea", "lentil"], Color("3f7a3a"), 0.4],
	[["batat", "potato", "nabo", "turnip", "cenour", "carrot", "horta", "veget", "cebol", "onion", "repolho", "cabbage"], Color("2f6a3a"), 0.25],
	[["linho", "flax", "algod", "cotton"], Color("c8d8e8"), 0.5],
	[["uva", "vinh", "grape"], Color("6a3a7a"), 0.6],
]
const OTHER_COLORS: Array[Color] = [Color("d9b44a"), Color("8fbf5a"), Color("c8803a"), Color("9a6a9a")]

var store
var bounds_filter := Rect2i() ## escopo cidade: só o que cai dentro (vazio = tudo)
var positions: Dictionary = {} ## id da lavoura -> Vector3 (seleção)
var cells: Dictionary = {} ## célula -> id da lavoura (o processo de plantar/regar/colher vira barra, não outro canteiro)
var _soil: MultiMeshInstance3D
var _plants: MultiMeshInstance3D


func _init() -> void:
	var soil_mesh := BoxMesh.new()
	soil_mesh.size = Vector3.ONE
	_soil = _multi("CropSoil", soil_mesh)
	var cone := CylinderMesh.new()
	cone.top_radius = 0.0
	cone.bottom_radius = 0.07
	cone.height = 1.0
	cone.radial_segments = 5
	cone.rings = 1
	_plants = _multi("CropPlants", cone)


func _multi(node_name: String, mesh: Mesh) -> MultiMeshInstance3D:
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.use_colors = true
	multi.mesh = mesh
	var instance := MultiMeshInstance3D.new()
	instance.name = node_name
	instance.multimesh = multi
	instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.roughness = 0.95
	instance.material_override = material
	add_child(instance)
	return instance


## [cor madura, altura madura] da cultura.
static func culture_of(crop: Dictionary) -> Array:
	var name := String(crop.get("resource_name", "")).to_lower()
	for entry in CULTURES:
		for word in entry[0]:
			if word in name:
				return [entry[1], entry[2]]
	return [OTHER_COLORS[posmod(int(crop.get("resource_id", 0)), OTHER_COLORS.size())], 0.5]


## Plantas do canteiro pelo estágio: {count, height, color}. Sem planta = count 0.
static func plants_of(crop: Dictionary) -> Dictionary:
	var culture := culture_of(crop)
	var fraction := float(crop.get("fraction", 0.0))
	var thirsty := bool(crop.get("thirsty", false))
	match String(crop.get("stage", "growing")):
		"unsown":
			return {"count": 0, "height": 0.0, "color": GREEN}
		"planted":
			return {"count": PLANTS_PER_ROW * PLANTS_PER_ROW, "height": 0.06, "color": SPROUT.lerp(Color("c8b05a"), 0.4) if thirsty else SPROUT}
		"mature":
			return {"count": PLANTS_PER_ROW * PLANTS_PER_ROW, "height": float(culture[1]), "color": culture[0]}
		"harvested":
			return {"count": PLANTS_PER_ROW * PLANTS_PER_ROW, "height": 0.05, "color": Color("b89a5a")}
		"fallow":
			return {"count": 3, "height": 0.12, "color": Color("7a8a4a")}
	var height: float = lerpf(0.08, float(culture[1]) * 0.85, clampf(fraction, 0.0, 1.0))
	var color := GREEN.lerp(Color(culture[0]), clampf(fraction - 0.6, 0.0, 0.4) / 0.4 * 0.5)
	if thirsty:
		color = color.lerp(Color("b8a04a"), 0.45)
	return {"count": PLANTS_PER_ROW * PLANTS_PER_ROW, "height": height, "color": color}


func visible_crops() -> Array:
	var out: Array = []
	for id in store.crops:
		var crop: Dictionary = store.crops[id]
		if bounds_filter.size != Vector2i.ZERO and not bounds_filter.has_point(crop.cell):
			continue
		out.append(crop)
	return out


func rebuild() -> void:
	positions.clear()
	cells.clear()
	var shown := visible_crops()
	var soil: MultiMesh = _soil.multimesh
	var plants: MultiMesh = _plants.multimesh
	soil.instance_count = shown.size()
	var plant_specs: Array = []
	var plant_total := 0
	for crop in shown:
		var spec := plants_of(crop)
		plant_specs.append(spec)
		plant_total += int(spec.count)
	plants.instance_count = plant_total
	var index := 0
	for i in shown.size():
		var crop: Dictionary = shown[i]
		var at := Grid.to_world(crop.cell)
		soil.set_instance_transform(i, Transform3D(Basis().scaled(Vector3(0.94, 0.06, 0.94)), at + Vector3(0, 0.03, 0)))
		soil.set_instance_color(i, SOIL.get(String(crop.stage), SOIL.growing))
		positions[int(crop.id)] = at + Vector3(0, 0.1, 0)
		cells[crop.cell] = int(crop.id)
		var spec: Dictionary = plant_specs[i]
		for n in int(spec.count):
			var gx := n % PLANTS_PER_ROW
			var gy := n / PLANTS_PER_ROW
			var offset := Vector3((gx - 1) * 0.28 + (gy % 2) * 0.06, 0, (gy - 1) * 0.28)
			if int(spec.count) < PLANTS_PER_ROW * PLANTS_PER_ROW:
				offset = Vector3((n - 1) * 0.3, 0, (n % 2) * 0.2 - 0.1)
			var height: float = float(spec.height) * (0.85 + 0.3 * fmod(float(int(crop.id) * 7 + n * 13) * 0.37, 1.0))
			plants.set_instance_transform(index, Transform3D(Basis().scaled(Vector3(1, height, 1)), at + offset + Vector3(0, 0.06 + height / 2.0, 0)))
			plants.set_instance_color(index, spec.color)
			index += 1
	_soil.visible = not shown.is_empty()
	_plants.visible = plant_total > 0


func plant_count() -> int:
	return _plants.multimesh.instance_count
