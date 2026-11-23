extends Node3D
## Processos em andamento que o delta já traz (`processUpserts`): lavoura vira um canteiro no chão que muda de cor
## com o progresso (terra → broto → verde → dourado na colheita); descanso, refeição, cozinha, água, carga e obra viram
## uma barrinha de progresso sobre a célula. Tudo num único MultiMesh (uma chamada de desenho), refeito só quando
## os processos mudam (no máximo uma vez por hora do mundo). `positions` serve à seleção (clique e dica).

const Grid := preload("res://Scenes/grid.gd")

const BAR_HEIGHT := 1.42
const BAR_WIDTH := 0.6
const KIND_COLORS := {"rest": Color("7fa8e0"), "food": Color("e0a24f"), "cook": Color("e07a4f"), "water": Color("4fb3e0"),
	"construction": Color("c9a36b"), "build-work": Color("c9a36b"), "extraordinary-construct": Color("9f7fe0"), "process": Color("bdbdbd")}
const SOIL := Color("6b4a2e")
const SPROUT := Color("8fbf5a")
const RIPE := Color("d9b44a")

var store
var bounds_filter := Rect2i() ## escopo cidade: só o que cai dentro da cidade (lavoura e recurso vêm do mundo todo)
var positions: Dictionary = {} ## id do processo -> Vector3 do marcador (para seleção)
var _instance: MultiMeshInstance3D
var _multi: MultiMesh


func _init() -> void:
	_multi = MultiMesh.new()
	_multi.transform_format = MultiMesh.TRANSFORM_3D
	_multi.use_colors = true
	var mesh := BoxMesh.new()
	mesh.size = Vector3.ONE
	_multi.mesh = mesh
	_instance = MultiMeshInstance3D.new()
	_instance.multimesh = _multi
	_instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_instance.material_override = material
	add_child(_instance)


## Cor do canteiro: plantio vai da terra ao broto, rega do broto ao verde, colheita do verde ao dourado.
static func crop_color(descriptor: String, progress: float) -> Color:
	match descriptor:
		"harvest-crop":
			return SPROUT.lerp(RIPE, progress)
		"water-crop":
			return SPROUT.darkened(0.25).lerp(SPROUT, progress)
	return SOIL.lerp(SPROUT, progress)


func visible_processes() -> Array:
	var out: Array = []
	for id in store.processes:
		var process: Dictionary = store.processes[id]
		if not process.has_cell:
			continue
		if bounds_filter.size != Vector2i.ZERO and not bounds_filter.has_point(process.cell):
			continue
		out.append(process)
	return out


func rebuild() -> void:
	positions.clear()
	var shown := visible_processes()
	var crop_cells := {} # com a lavoura do AD-170 desenhada, o trabalho nela vira barra (não outro canteiro)
	for crop in store.crops.values() if "crops" in store else []:
		crop_cells[crop.cell] = true
	var count := 0
	for process in shown:
		count += 1 if process.kind == "crop" and not crop_cells.has(process.cell) else 2
	if _multi.instance_count != count: # AD-189: realocar a cada delta custava (o número quase nunca muda)
		_multi.instance_count = count
	var index := 0
	for process in shown:
		var at := Grid.to_world(process.cell)
		if process.kind == "crop" and not crop_cells.has(process.cell):
			_put(index, at + Vector3(0, 0.03, 0), Vector3(0.92, 0.05, 0.92), crop_color(process.descriptor, process.progress))
			index += 1
			positions[process.id] = at + Vector3(0, 0.05, 0)
			continue
		var color: Color = KIND_COLORS.get(process.kind, KIND_COLORS.process)
		var top := at + Vector3(0, BAR_HEIGHT, 0)
		_put(index, top, Vector3(BAR_WIDTH + 0.04, 0.09, 0.09), Color(0.08, 0.08, 0.1))
		var fill: float = maxf(process.progress, 0.04) * BAR_WIDTH
		_put(index + 1, top + Vector3(-(BAR_WIDTH - fill) / 2.0, 0.0, 0.0), Vector3(fill, 0.1, 0.1), color)
		index += 2
		positions[process.id] = top
	_instance.visible = count > 0


func _put(index: int, at: Vector3, size: Vector3, color: Color) -> void:
	_multi.set_instance_transform(index, Transform3D(Basis().scaled(size), at))
	_multi.set_instance_color(index, color)


func instance_count() -> int:
	return _multi.instance_count
