extends Node3D
## Chão da cidade que o motor expõe (AD-177, AD-181): ruas do escopo (largura pela faixa de uso, chão gasto pelo pisoteio,
## acabamento pela superfície, calçamento estragado pela condição, obra com o trecho assentado e a pilha do material) e o lote
## de cada prédio (quintal cercado, caminho da porta, viela/escada/passarela do acesso). Reserva do traçado sem uso não é rua.
## Nada inventado: sem rua do motor não há rua; sem `lot.croft` não há quintal. Duas MultiMesh (ruas, lotes) com uma caixa
## unitária e UM material compartilhado. Rua que nasce, alarga, muda de superfície ou some faz isso em TRANSITION segundos
## (só nesses quadros a malha de ruas é refeita). H: mapa de calor do pisoteio (`wear`).

const Grid := preload("res://Scenes/grid.gd")
const Scenery := preload("res://Scenes/scenery.gd")
const Style := preload("res://Scenes/render/street_style.gd")
const Pieces := preload("res://Scenes/render/street_pieces.gd")
const Ground := preload("res://Store/city_ground.gd")
const Roads := preload("res://Store/road_book.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
const WeatherLook := preload("res://Scenes/render/weather_look.gd")

const DIRS: Array[Vector2i] = [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]
const ROAD_Y := 0.014
const PATH_Y := 0.018
const CROFT_Y := 0.008
const FENCE_COLOR := Color("6b5236")
const TRANSITION := 0.8 ## segundos de uma rua nascendo, mudando ou sumindo

var store
var roads_instance: MultiMeshInstance3D
var lots_instance: MultiMeshInstance3D
var lot_cells: Dictionary = {} ## célula -> {kind: croft|path, building}
var drawn_roads: Dictionary = {} ## célula -> rua desenhada agora (escopo filtrado, sem reserva do traçado)
var reserved: Dictionary = {} ## célula -> rua do traçado reservado sem uso (só a planta mostra, como traço)
var road_pieces := 0 ## testável: quantas peças a malha de ruas tem
var lot_pieces := 0
var heat := false ## mapa de calor do pisoteio
var top_wear := 0 ## o mais pisado do escopo (escala do mapa de calor)
var anims: Dictionary = {} ## célula -> {t, w0, c0, road (só de quem some), leaving}
var _shown: Dictionary = {} ## célula -> [largura, cor] desenhadas no último quadro (ponto de partida da transição)
var _box := BoxMesh.new()
var _material := StandardMaterial3D.new()
var _pieces: Array = [] ## [Transform3D, Color] em construção
var _add: Callable ## `_piece_flat`, para as peças de `street_pieces.gd`


func _init() -> void:
	_add = _piece_flat
	_box.size = Vector3.ONE
	_material.vertex_color_use_as_albedo = true
	_material.roughness = 0.95
	roads_instance = _instance("Roads")
	lots_instance = _instance("Lots")


func _instance(label: String) -> MultiMeshInstance3D:
	var node := MultiMeshInstance3D.new()
	node.name = label
	node.material_override = _material
	node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	multi.use_colors = true
	multi.mesh = _box
	node.multimesh = multi
	add_child(node)
	return node


## Redesenha ruas e lotes (os dois se ligam: o caminho da porta encosta na rua). `animate`: o que mudou em relação ao que
## estava desenhado entra/sai em transição (delta); sem ele (retrato novo), troca na hora.
func rebuild(animate := false) -> void:
	var before := drawn_roads
	_select_roads()
	if animate:
		_start_transitions(before)
	else:
		anims = {}
		_shown = {}
	rebuild_lots()
	_draw_roads()


## Ruas do escopo: na cidade, as dela (pelo id ou dentro dos limites); no mapa-múndi, todas. Reserva sem uso fica de fora.
func _select_roads() -> void:
	drawn_roads = {}
	reserved = {}
	top_wear = 0
	if store != null and store.kind in ["World", "City"]:
		var bounds: Rect2i = store.city.get("bounds", Rect2i()) if store.kind == "City" else Rect2i()
		var city_id := String(store.city.get("id", "")) if store.kind == "City" else ""
		for cell in store.roads:
			var road: Dictionary = store.roads[cell]
			if city_id == "" or String(road.city) == city_id or bounds.has_point(cell):
				if Roads.is_reserve(road):
					reserved[cell] = road
				else:
					drawn_roads[cell] = road
					top_wear = maxi(top_wear, int(road.get("wear", 0)))


func _start_transitions(before: Dictionary) -> void:
	for cell in drawn_roads:
		var style := Style.style_of(drawn_roads[cell])
		var shown: Array = _shown.get(cell, [0.0, style.color])
		if not before.has(cell):
			anims[cell] = {"t": 0.0, "w0": 0.0 if not anims.has(cell) else float(shown[0]), "c0": shown[1], "leaving": false}
		elif not is_equal_approx(float(shown[0]), float(style.width)) or not Color(shown[1]).is_equal_approx(style.color):
			anims[cell] = {"t": 0.0, "w0": float(shown[0]), "c0": shown[1], "leaving": false}
	for cell in before:
		if not drawn_roads.has(cell) and _shown.has(cell):
			anims[cell] = {"t": 0.0, "w0": float(_shown[cell][0]), "c0": _shown[cell][1], "road": before[cell], "leaving": true}


## Um quadro da transição: só quando há rua mudando a malha de ruas é refeita.
const ANIM_REDRAW := 0.05
var _anim_clock := 0.0


func step(delta: float) -> void:
	if anims.is_empty():
		_anim_clock = 0.0
		return
	# AD-189: a transição redesenha a malha das ruas inteira; a 20 por segundo basta (antes, todo quadro: picos com o mundo rodando)
	_anim_clock += delta
	if _anim_clock < ANIM_REDRAW:
		return
	delta = _anim_clock
	_anim_clock = 0.0
	for cell in anims.keys():
		anims[cell].t += delta / TRANSITION
		if float(anims[cell].t) >= 1.0:
			anims.erase(cell)
			if not drawn_roads.has(cell):
				_shown.erase(cell)
	_draw_roads()


func set_heat(on: bool) -> void:
	heat = on
	_draw_roads()


func _draw_roads() -> void:
	_pieces = []
	for cell in drawn_roads:
		_road_cell(cell, _animated(cell, Style.style_of(drawn_roads[cell])))
	for cell in anims:
		if bool(anims[cell].leaving):
			_road_cell(cell, _animated(cell, Style.style_of(anims[cell].road)))
	road_pieces = _flush(roads_instance)


## Estilo no ponto da transição (largura e cor entre o desenhado antes e o novo; quem some encolhe até nada).
func _animated(cell: Vector2i, style: Dictionary) -> Dictionary:
	if heat:
		style.color = Style.heat_color(int(drawn_roads.get(cell, {}).get("wear", 0)), top_wear)
		style.detail = ""
		style.tufts = 0
		style.work = {}
	var anim = anims.get(cell)
	if anim != null:
		var k := smoothstep(0.0, 1.0, clampf(float(anim.t), 0.0, 1.0))
		style.width = lerpf(float(anim.w0), 0.0 if bool(anim.leaving) else float(style.width), k)
		style.color = Color(anim.c0).lerp(style.color, k)
	_shown[cell] = [float(style.width), style.color]
	return style


func _road_cell(cell: Vector2i, style: Dictionary) -> void:
	var w := float(style.width)
	if w <= 0.01:
		return
	var jitter := float(style.jitter)
	var shade := 0.93 + Scenery.cell_hash(cell, 5) * 0.12
	var ground := "" if heat else WeatherLook.road_ground(style.road)
	var color: Color = _season(Color(style.color) * shade, true) if ground == "" \
		else WeatherLook.ground_tint(Color(style.color) * shade, ground, float(style.road.get("snow_cm", 0.0)))
	color.a = 1.0
	var center := Grid.to_world(cell, ROAD_Y)
	center += Vector3(Scenery.cell_hash(cell, 1) - 0.5, 0, Scenery.cell_hash(cell, 2) - 0.5) * 2.0 * jitter
	var turn := (Scenery.cell_hash(cell, 3) - 0.5) * jitter * 4.0
	_piece(center, Vector3(w, 0.02, w), color, turn)
	if w < 1.0:
		for dir in DIRS:
			var next := cell + dir
			if not drawn_roads.has(next) and not (lot_cells.get(next, {}).get("kind", "") == "path"):
				continue
			var other := w if not drawn_roads.has(next) else minf(w, float(_shown.get(next, [w])[0]))
			_arm(center, cell, dir, other, color, ROAD_Y)
	Pieces.surface_detail(_add, cell, center, style)
	Pieces.puddles(_add, cell, center, w, WeatherLook.puddles(ground))
	if not style.work.is_empty():
		Pieces.work(_add, cell, center, style)
	if bool(style.main) and not bool(style.square):
		_piece(center + Vector3(0, 0.008, 0), Vector3(0.08, 0.022, 0.08), Color("5e5348")) # marco da rua principal


## Braço do centro da célula até a borda na direção `dir` (rua ou caminho contínuo de uma célula à outra).
func _arm(center: Vector3, cell: Vector2i, dir: Vector2i, width: float, color: Color, y: float) -> void:
	var edge := Grid.to_world(cell, y) + Vector3(dir.x, 0, dir.y) * 0.5
	var mid := (center + edge) / 2.0
	var length := maxf(absf(edge.x - center.x) if dir.x != 0 else absf(edge.z - center.z), 0.05) + width * 0.25
	var size := Vector3(length, 0.02, width) if dir.x != 0 else Vector3(width, 0.02, length)
	_piece(Vector3(mid.x, y, mid.z), size, color)


## Lotes do escopo cidade: quintal (chão + cerca em volta, com a porteira para o caminho), caminho da porta à rua e o acesso.
func rebuild_lots() -> void:
	lot_cells = {}
	_pieces = []
	if store != null and store.kind == "City":
		lot_cells = Ground.lot_cells(store.buildings)
		for id in store.buildings:
			_lot(store.buildings[id])
	lot_pieces = _flush(lots_instance)


func _lot(building: Dictionary) -> void:
	var parcel: Dictionary = building.get("lot", {})
	if parcel.is_empty():
		return
	var footprint := {}
	for entry in building.footprint:
		footprint[building.cell + entry.cell] = true
	var croft := {}
	for cell in parcel.croft:
		croft[cell] = true
	var path := {}
	for cell in parcel.front_path:
		path[cell] = true
	for cell in croft:
		_piece(Grid.to_world(cell, CROFT_Y), Vector3(0.98, 0.016, 0.98), _season(Style.CROFT_COLOR * (0.92 + Scenery.cell_hash(cell, 7) * 0.14), false))
		for dir in DIRS:
			var next: Vector2i = cell + dir
			if not croft.has(next) and not footprint.has(next) and not path.has(next):
				_fence(cell, dir)
	var street = parcel.street
	for cell in path:
		var center := Grid.to_world(cell, PATH_Y)
		_piece(center, Vector3(0.34, 0.02, 0.34), _season(Style.PATH_COLOR, true))
		for dir in DIRS:
			var next: Vector2i = cell + dir
			if path.has(next) or footprint.has(next) or (street != null and next == street) or drawn_roads.has(next):
				_arm(center, cell, dir, 0.34, Style.PATH_COLOR, PATH_Y)
	Pieces.access(_add, building, store.buildings.get(int(parcel.get("access_via", -1)), {}))


## Cerca de varas no lado `dir` da célula: dois postes e duas travessas.
func _fence(cell: Vector2i, dir: Vector2i) -> void:
	var edge := Grid.to_world(cell) + Vector3(dir.x, 0, dir.y) * 0.47
	var along := Vector3(absf(dir.y), 0, absf(dir.x))
	for rail in [0.16, 0.32]:
		_piece(edge + Vector3(0, rail, 0), along * 0.94 + Vector3(0.035, 0.035, 0.035) * (Vector3.ONE - along), FENCE_COLOR)
	for side in [-0.45, 0.45]:
		_piece(edge + along * side + Vector3(0, 0.2, 0), Vector3(0.06, 0.4, 0.06), FENCE_COLOR * 0.85)


## Estação no chão da cidade (09b item 12): neve cobre rua e caminho; o quintal muda como o terreno (verde, seco, outono).
func _season(color: Color, paved: bool) -> Color:
	return Season.ground(color, store.climate, paved) if store != null and not heat else color


func _piece(at: Vector3, size: Vector3, color: Color, turn := 0.0) -> void:
	var basis := Basis(Vector3.UP, turn) * Basis.from_scale(size) if turn != 0.0 else Basis.from_scale(size)
	_pieces.append([Transform3D(basis, at), color])


func _piece_flat(at: Vector3, size: Vector3, color: Color) -> void:
	_pieces.append([Transform3D(Basis.from_scale(size), at), color])


func _flush(node: MultiMeshInstance3D) -> int:
	var multi := node.multimesh
	multi.instance_count = _pieces.size()
	for i in _pieces.size():
		multi.set_instance_transform(i, _pieces[i][0])
		multi.set_instance_color(i, _pieces[i][1])
	var count := _pieces.size()
	_pieces = []
	node.visible = count > 0
	return count


## O que o chão diz nesta célula: rua (ficha da rua) ou lote (ficha do prédio dono); {} = terreno comum.
func entity_at(cell: Vector2i) -> Dictionary:
	if drawn_roads.has(cell):
		return Registry.ref(Registry.STREET, "%d,%d" % [cell.x, cell.y])
	if lot_cells.has(cell):
		return Registry.ref(Registry.BUILDING, int(lot_cells[cell].building))
	return {}
