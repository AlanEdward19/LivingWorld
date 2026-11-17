extends Node3D
## Fundo vivo do menu: uma ilha em blocos com água, árvores e casinhas, girando devagar sob um céu de fim de tarde.
## Começa com um mapa sorteado só para enfeite; quando o Novo mundo recebe a prévia real (`/worlds/preview`), mostra
## aquele mapa (água, relevo e assentamentos de verdade). Uma MultiMesh por tipo de peça: custo de desenho mínimo.

const ORBIT_SPEED := 0.06 ## radianos por segundo
const GRASS := Color("6f9b52")
const SAND := Color("d8c58a")
const ROCK := Color("8d8a80")
const SNOW := Color("eef2f5")
const WATER := Color("3b77b5")
const TERRAIN_COLORS: Array[Color] = [Color("6f9b52"), Color("c9b26b"), Color("7b8b6f"), Color("8a7a66"), Color("4f7a64"), Color("a8a39a")]

var camera: Camera3D
var _pivot: Node3D
var _ground: MultiMeshInstance3D
var _trees: MultiMeshInstance3D
var _houses: MultiMeshInstance3D
var _roofs: MultiMeshInstance3D
var _water: MeshInstance3D
var map_size := Vector2i(24, 24)
var spinning := true


func _ready() -> void:
	_pivot = Node3D.new()
	add_child(_pivot)
	camera = Camera3D.new()
	camera.fov = 40
	_pivot.add_child(camera)
	camera.current = true
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-38, -35, 0)
	sun.light_color = Color("ffe2b8")
	sun.light_energy = 1.25
	sun.shadow_enabled = true
	add_child(sun)
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color("1a2338")
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("a9b7cf")
	env.ambient_light_energy = 0.7
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.fog_enabled = true
	env.fog_light_color = Color("2a3554")
	env.fog_density = 0.004
	var world_env := WorldEnvironment.new()
	world_env.environment = env
	add_child(world_env)
	_ground = _multi(BoxMesh.new())
	var cone := CylinderMesh.new()
	cone.top_radius = 0.0
	cone.bottom_radius = 0.32
	cone.height = 0.9
	_trees = _multi(cone)
	var house := BoxMesh.new()
	house.size = Vector3(0.6, 0.45, 0.6)
	_houses = _multi(house)
	var roof := PrismMesh.new()
	roof.size = Vector3(0.72, 0.35, 0.66)
	_roofs = _multi(roof)
	_water = MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(400, 400)
	_water.mesh = plane
	var water_material := StandardMaterial3D.new()
	water_material.albedo_color = WATER
	water_material.roughness = 0.2
	water_material.metallic = 0.1
	_water.material_override = water_material
	add_child(_water)
	show_island(7)


func _multi(mesh: Mesh) -> MultiMeshInstance3D:
	var node := MultiMeshInstance3D.new()
	var multimesh := MultiMesh.new()
	multimesh.transform_format = MultiMesh.TRANSFORM_3D
	multimesh.use_colors = true
	multimesh.mesh = mesh
	node.multimesh = multimesh
	var material := StandardMaterial3D.new()
	material.vertex_color_use_as_albedo = true
	material.roughness = 0.9
	node.material_override = material
	add_child(node)
	return node


func _process(delta: float) -> void:
	if spinning and _pivot != null:
		_pivot.rotate_y(ORBIT_SPEED * delta)


## Ilha sorteada (enfeite, sem relação com o mundo do servidor): relevo por ruído, praia, árvores e uma vila no meio.
func show_island(seed_value: int) -> void:
	var noise := FastNoiseLite.new()
	noise.seed = seed_value
	noise.frequency = 0.09
	var cells: Array = []
	var center := Vector2(map_size) / 2.0
	for y in map_size.y:
		for x in map_size.x:
			var falloff := 1.0 - (Vector2(x, y).distance_to(center) / (map_size.x * 0.5))
			var height := clampf(noise.get_noise_2d(x, y) * 0.6 + falloff * 0.9, -1.0, 1.0)
			cells.append({"x": x, "y": y, "h": height, "water": height < 0.12})
	var village: Array = []
	for i in 9:
		village.append(Vector2i(int(center.x) - 2 + i % 3 * 2, int(center.y) - 2 + i / 3 * 2))
	_build(cells, village, seed_value)


## Mapa real devolvido pelo servidor (o mesmo da prévia 2D do Novo mundo).
func show_map(body: Dictionary) -> void:
	map_size = Vector2i(maxi(int(body.get("width", 1)), 1), maxi(int(body.get("height", 1)), 1))
	var cells: Array = []
	for cell in body.get("cells", []):
		var water := bool(cell.get("hasWater", false))
		cells.append({"x": int(cell.x), "y": int(cell.y), "h": -0.2 if water else 0.2 + clampf(float(cell.get("altitude", 0)) / 300.0, 0.0, 0.8),
			"water": water, "terrain": int(cell.get("terrain", 0))})
	var village: Array = []
	for settlement in body.get("settlements", []):
		for offset in [Vector2i.ZERO, Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 0), Vector2i(0, -1)]:
			village.append(Vector2i(int(settlement.x), int(settlement.y)) + offset)
	_build(cells, village, map_size.x * 31 + map_size.y)


func _build(cells: Array, village: Array, seed_value: int) -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = seed_value
	var half := Vector3(map_size.x / 2.0, 0, map_size.y / 2.0)
	var heights := {}
	var ground: Array = []
	var trees: Array = []
	for cell in cells:
		var key := Vector2i(cell.x, cell.y)
		var land := not bool(cell.water)
		var top := 0.05 if not land else 0.25 + float(cell.h) * 1.4
		heights[key] = top
		var color: Color = SAND if land and float(cell.h) < 0.16 else GRASS.lerp(Color("4f7a3c"), clampf(float(cell.h), 0.0, 1.0) * 0.6)
		if cell.has("terrain") and land:
			color = TERRAIN_COLORS[int(cell.terrain) % TERRAIN_COLORS.size()]
		if land and float(cell.h) > 0.72:
			color = ROCK if float(cell.h) < 0.9 else SNOW
		if not land:
			color = SAND.darkened(0.35)
		ground.append([Transform3D(Basis.from_scale(Vector3(1, maxf(top, 0.05) + 0.4, 1)), Vector3(cell.x, (maxf(top, 0.05) + 0.4) / 2.0 - 0.4, cell.y) - half), color.lerp(Color(rng.randf(), rng.randf(), rng.randf()), 0.04)])
		if land and float(cell.h) > 0.2 and float(cell.h) < 0.7 and not village.has(key) and rng.randf() < 0.35:
			var at := Vector3(cell.x + rng.randf_range(-0.25, 0.25), top + 0.45, cell.y + rng.randf_range(-0.25, 0.25)) - half
			trees.append([Transform3D(Basis.from_scale(Vector3.ONE * rng.randf_range(0.8, 1.3)), at), Color("3f6e3a").lerp(Color("5c8a3c"), rng.randf())])
	var houses: Array = []
	var roofs: Array = []
	for spot in village:
		if not heights.has(spot) or heights[spot] <= 0.06:
			continue
		var base := Vector3(spot.x, heights[spot], spot.y) - half
		var turn := Basis(Vector3.UP, rng.randf_range(-0.3, 0.3))
		houses.append([Transform3D(turn, base + Vector3(0, 0.22, 0)), Color("e8dcc4").lerp(Color("c9b48f"), rng.randf())])
		roofs.append([Transform3D(turn, base + Vector3(0, 0.62, 0)), Color("a4553a").lerp(Color("7a3f2c"), rng.randf())])
	_fill(_ground, ground)
	_fill(_trees, trees)
	_fill(_houses, houses)
	_fill(_roofs, roofs)
	_water.position.y = 0.0
	var reach := maxf(map_size.x, map_size.y)
	camera.position = Vector3(0, reach * 0.75, reach * 1.15)
	camera.look_at_from_position(camera.position, Vector3.ZERO, Vector3.UP)


func _fill(node: MultiMeshInstance3D, items: Array) -> void:
	node.multimesh.instance_count = items.size()
	for i in items.size():
		node.multimesh.set_instance_transform(i, items[i][0])
		node.multimesh.set_instance_color(i, items[i][1])
