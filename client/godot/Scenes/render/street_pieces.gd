extends RefCounted
## Peças do chão da rua que dependem do estado do motor (AD-181), montadas como caixas (posição, tamanho, cor) para as
## MultiMesh do `ground_renderer.gd`: capim que volta onde pouco se pisa, calçamento estragado, obra (parte já assentada,
## pilha do material, estacas) e o acesso da porta (viela, escada encostada, passarela por cima). Nada aqui decide nada:
## só traduz o campo do motor em forma. `add` = Callable(at: Vector3, size: Vector3, color: Color).

const Grid := preload("res://Scenes/grid.gd")
const Scenery := preload("res://Scenes/scenery.gd")
const Style := preload("res://Scenes/render/street_style.gd")

const WOOD := Color("7a5634")
const FLOOR_HEIGHT := 1.0 ## mesma altura de andar do `building_renderer.gd`


## Superfície por cima do chão base: lajes ou pedrinhas (menos delas quando o calçamento está estragado) e tufos de capim.
static func surface_detail(add: Callable, cell: Vector2i, center: Vector3, style: Dictionary) -> void:
	var w := float(style.width)
	var broken := float(style.get("broken", 0.0))
	match String(style.detail):
		"pebbles":
			for i in 4 - int(round(broken * 3.0)):
				var at := center + Vector3(Scenery.cell_hash(cell, 10 + i) - 0.5, 0.012, Scenery.cell_hash(cell, 20 + i) - 0.5) * w * 0.8
				add.call(at, Vector3(0.07, 0.03, 0.06), Color("a8a093") * (0.9 + Scenery.cell_hash(cell, 30 + i) * 0.2))
		"slabs":
			var half := w / 2.0
			var missing := int(round(broken * 3.0)) # calçamento estragado: lajes faltando, terra à mostra
			for i in 4:
				if i < missing and Scenery.cell_hash(cell, 50 + i) < 0.9:
					continue
				var at := center + Vector3((float(i % 2) - 0.5) * half, 0.006, (float(i / 2) - 0.5) * half)
				var tilt := (Scenery.cell_hash(cell, 60 + i) - 0.5) * 0.02 * broken
				add.call(at + Vector3(0, tilt, 0), Vector3(half - 0.04, 0.025, half - 0.04), Color("8e8c88") * (0.88 + Scenery.cell_hash(cell, 40 + i) * 0.2))
	for i in int(style.get("tufts", 0)): # capim que volta na beira de trilha pouco pisada
		var side := Vector3(Scenery.cell_hash(cell, 70 + i) - 0.5, 0, Scenery.cell_hash(cell, 80 + i) - 0.5).normalized() * (w * 0.5 + 0.04)
		add.call(center + side + Vector3(0, 0.02, 0), Vector3(0.09, 0.05, 0.09), Style.GRASS * (0.9 + Scenery.cell_hash(cell, 90 + i) * 0.2))


## Poças na rua de terra com lama (o motor diz `ground: mud/heavy-mud` na célula): manchas escuras e rasas.
static func puddles(add: Callable, cell: Vector2i, center: Vector3, width: float, count: int) -> void:
	for i in count:
		var at := center + Vector3(Scenery.cell_hash(cell, 110 + i) - 0.5, 0.013, Scenery.cell_hash(cell, 120 + i) - 0.5) * width * 0.6
		add.call(at, Vector3(0.16 + 0.1 * Scenery.cell_hash(cell, 130 + i), 0.022, 0.12), Color("3e4a55"))


## Obra de calçamento: a parte já feita na superfície de destino (cresce com `progress`), a pilha do material (diminui) e estacas.
static func work(add: Callable, cell: Vector2i, center: Vector3, style: Dictionary) -> void:
	var job: Dictionary = style.work
	var done := float(job.progress) / 100.0
	var target: Dictionary = Style.SURFACES.get(int(job.surface), Style.NEUTRAL)
	var w := maxf(float(style.width), float(target.width))
	if done > 0.0: # trecho assentado: de um lado da célula até a frente da obra
		add.call(center + Vector3(-w / 2.0 + w * done / 2.0, 0.012, 0), Vector3(w * done, 0.026, w * 0.92), Color(target.color))
	var pile: Color = Style.WORK_STYLE.stone_pile if int(job.surface) == 3 else Style.WORK_STYLE.pile
	var corner := center + Vector3(0.36, 0, 0.36) * (1.0 if Scenery.cell_hash(cell, 4) < 0.5 else -1.0)
	var height := 0.06 + 0.22 * (1.0 - done)
	for i in 3: # pedras/cascalho empilhados, menos conforme a obra anda
		var size := Vector3(0.22, height / 3.0, 0.2) * (1.0 - 0.22 * i)
		add.call(corner + Vector3(0, height / 6.0 + i * height / 3.0, 0), size, pile * (0.92 + 0.06 * i))
	for side in [-1.0, 1.0]: # estacas marcando o trecho
		add.call(center + Vector3(side * 0.44, 0.12, -side * 0.44), Vector3(0.035, 0.24, 0.035), Style.WORK_STYLE.stake)


## Acesso da porta como o motor diz (`lot.access`): viela (corredor estreito e escuro), escada encostada (degraus subindo até
## a porta) ou passagem por cima (passarela na altura do andar, com esteios). Rua, caminho e `none` não ganham peça aqui.
static func access(add: Callable, building: Dictionary, via: Dictionary = {}) -> void:
	var kind := Style.access_of(building.get("lot", {}))
	if not kind in ["alley", "stair", "roof"]:
		return
	if not building.lot.get("access_cells", []).is_empty():
		access_path(add, building, via)
		return
	var door := door_of(building)
	if door.is_empty():
		return
	var out: Vector3 = door.out
	var face: Vector3 = Grid.to_world(door.cell) + out * 0.5
	var across := Vector3(absf(out.z), 0, absf(out.x))
	match kind:
		"alley":
			add.call(face + out * 0.5 + Vector3(0, 0.02, 0), across * 0.3 + Vector3(absf(out.x), 0, absf(out.z)) * 1.0 + Vector3(0, 0.025, 0),
				Color(Style.ACCESS_STYLES.alley.plan))
			for side in [-1.0, 1.0]: # sombra das duas paredes que apertam a viela
				add.call(face + out * 0.5 + across * side * 0.2 + Vector3(0, 0.03, 0), across * 0.06 + Vector3(absf(out.x), 0, absf(out.z)) + Vector3(0, 0.02, 0), Color("2e2620"))
		"stair":
			for i in 5: # degraus: do chão longe da porta até a soleira no alto, encostados na parede
				var rise := 0.12 + 0.16 * (4 - i)
				add.call(face + out * (0.12 + 0.16 * i) + Vector3(0, rise / 2.0, 0), across * 0.4 + Vector3(absf(out.x), 0, absf(out.z)) * 0.16 + Vector3(0, rise, 0),
					WOOD * (0.9 + 0.03 * i))
		"roof":
			var deck := FLOOR_HEIGHT * maxf(float(building.get("floors", 1)) - 0.5, 0.6)
			add.call(face + out * 0.6 + Vector3(0, deck, 0), across * 0.36 + Vector3(absf(out.x), 0, absf(out.z)) * 1.2 + Vector3(0, 0.05, 0),
				Color(Style.ACCESS_STYLES.roof.plan))
			for side in [-1.0, 1.0]:
				add.call(face + out * 1.1 + across * side * 0.16 + Vector3(0, deck / 2.0, 0), Vector3(0.05, deck, 0.05), WOOD)
				add.call(face + out * 0.6 + across * side * 0.18 + Vector3(0, deck + 0.18, 0),
					across * 0.03 + Vector3(absf(out.x), 0, absf(out.z)) * 1.2 + Vector3(0, 0.03, 0), WOOD * 0.85) # guarda-corpo


## AD-185: o acesso pela geometria do motor (`lot.accessCells`, soleira primeiro): célula sobre o prédio vizinho
## (`accessViaBuildingId`) vira laje/passarela na altura do telhado dele; a célula do chão que encosta numa alta ganha a escada
## que sobe até ela; as do chão são o corredor da viela ou o caminho. `accessState` diz onde desemboca: `trodden` = chão
## pisado, `planned` = caminho claro, `reserved` = só marcado (traço e estacas: a rua do traçado ainda não tem uso).
static func access_path(add: Callable, building: Dictionary, via: Dictionary) -> void:
	var lot: Dictionary = building.lot
	var cells: Array = lot.access_cells
	var high := {}
	for entry in via.get("footprint", []):
		high[via.cell + entry.cell] = FLOOR_HEIGHT * maxf(float(via.get("floors", 1)), 1.0)
	var state := String(lot.get("access_state", ""))
	var ground: Color = Color(Style.ACCESS_STYLES.alley.plan) if Style.access_of(lot) == "alley" else Style.access_state_color(state)
	for i in cells.size():
		var cell: Vector2i = cells[i]
		var h := float(high.get(cell, 0.0))
		var center := Grid.to_world(cell)
		if h > 0.0: # por cima: laje do vizinho com guarda-corpo
			add.call(center + Vector3(0, h + 0.03, 0), Vector3(0.5, 0.05, 0.5), Color(Style.ACCESS_STYLES.roof.plan))
			add.call(center + Vector3(0.24, h + 0.2, 0), Vector3(0.03, 0.3, 0.5), WOOD * 0.85)
			continue
		var up := -1.0
		for j in [i - 1, i + 1]: # vizinha alta na rota: a escada sobe dela para lá
			if j >= 0 and j < cells.size() and float(high.get(cells[j], 0.0)) > 0.0:
				up = float(high[cells[j]])
				_stair(add, center, Grid.to_world(cells[j]) - center, up)
		if up > 0.0:
			continue
		var pieces := 2 if state == "reserved" else 1
		for k in pieces: # reserva: traço (dois pedaços com vão), sem chão pisado
			var size := 0.34 if pieces == 1 else 0.14
			var shift := Vector3.ZERO if pieces == 1 else Vector3((k - 0.5) * 0.3, 0, 0)
			add.call(center + shift + Vector3(0, 0.02, 0), Vector3(size, 0.022, size), ground)
	if state == "reserved" and not cells.is_empty(): # estacas na ponta: daqui sai a rua reservada
		var tip := Grid.to_world(cells[cells.size() - 1])
		for side in [-1.0, 1.0]:
			add.call(tip + Vector3(side * 0.3, 0.12, side * 0.3), Vector3(0.035, 0.24, 0.035), Style.WORK_STYLE.stake)


## Escada da célula do chão subindo até a vizinha alta (`toward` = rumo até ela, `top` = altura do telhado).
static func _stair(add: Callable, center: Vector3, toward: Vector3, top: float) -> void:
	var out := toward.normalized()
	var across := Vector3(absf(out.z), 0, absf(out.x))
	for i in 5:
		var rise := top * float(i + 1) / 5.0
		add.call(center + out * (-0.3 + 0.15 * i) + Vector3(0, rise / 2.0, 0),
			across * 0.4 + Vector3(absf(out.x), 0, absf(out.z)) * 0.15 + Vector3(0, rise, 0), WOOD * (0.9 + 0.03 * i))


## Célula da porta (material 2 da planta) e para onde ela abre: {cell, out: Vector3}; {} sem porta. Abre para o caminho, a
## rua da frente ou, sem eles, para a primeira vizinha fora da planta.
static func door_of(building: Dictionary) -> Dictionary:
	var mine := {}
	var doors: Array = []
	for entry in building.get("footprint", []):
		var cell: Vector2i = building.cell + entry.cell
		mine[cell] = true
		if int(entry.material) == 2:
			doors.append(cell)
	var lot: Dictionary = building.get("lot", {})
	var wanted := {}
	for cell in lot.get("front_path", []):
		wanted[cell] = true
	if lot.get("street") != null:
		wanted[lot.street] = true
	for door in doors:
		var fallback = null
		for dir in [Vector2i(0, 1), Vector2i(1, 0), Vector2i(0, -1), Vector2i(-1, 0)]:
			var next: Vector2i = door + dir
			if mine.has(next):
				continue
			if wanted.has(next):
				return {"cell": door, "out": Vector3(dir.x, 0, dir.y)}
			if fallback == null:
				fallback = dir
		if fallback != null:
			return {"cell": door, "out": Vector3(fallback.x, 0, fallback.y)}
	return {}
