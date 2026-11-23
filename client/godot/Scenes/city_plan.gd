extends Control
## Planta baixa (P, AD-177): a cidade vista de cima, em 2D, só com o que o motor expôs: ruas pela superfície (largo e rua
## principal marcados), lotes (quintal cercado, caminho da porta), prédios coloridos pela função e água; legenda ao lado.
## No mapa-múndi, o mapa inteiro com as ruas de todas as cidades e os limites delas. Clique abre a ficha; a dica diz o
## que é. AD-181: ruas pela superfície e pelo uso (mais larga com mais passagens), obras com a barra do quanto já foi
## feito, acesso de cada casa (viela, escada, passagem por cima; sem acesso em vermelho), reserva do traçado sem uso como
## traço discreto, as pessoas como pontos onde estão desenhadas (amarelo = andando pela rota) e, com H, o mapa de calor do
## pisoteio. Redesenha quando a store muda e PEOPLE_REFRESH vezes por segundo enquanto aberta (as pessoas andam).

const Style := preload("res://Scenes/render/street_style.gd")
const Describe := preload("res://Scenes/inspector_describe.gd")
const Labels := preload("res://Scenes/render/building_labels.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Works := preload("res://Store/work_book.gd")
const SITE_PLAN := Color("e0b04a") ## AD-193: canteiro de obra (contorno; a barra é o quanto já foi feito)
const SITE_HALT := Color("c0564a") ## obra parada (tempo, falta de gente ou de material)

const FUNCTION_COLORS := {"dwelling": Color("d9a066"), "lodging": Color("c77dba"), "workplace": Color("5b8fd1"),
	"structure": Color("9a9a9a")}
const UNKNOWN_BUILDING := Color("b9a48a")
const WATER := Color("3f6f99")
const CROFT_PLAN := Color("7fa65a")
const FENCE_PLAN := Color("4b3a24")
const BACKGROUND := Color(0.11, 0.12, 0.11, 0.95)
const LEGEND_WIDTH := 250.0
const TOP := 64.0 ## abaixo da barra do topo
const RIGHT := 320.0 ## a ficha fica à direita, por cima: a planta não se esconde atrás dela
const DIRS: Array[Vector2i] = [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]
const PEOPLE_REFRESH := 4.0
const PERSON := Color(0.95, 0.95, 0.95)
const WALKER := Color("ffd84a")
const RESERVE := Color(1, 1, 1, 0.28)
const Marks := preload("res://Scenes/render/street_pieces.gd")

var app
var area := Rect2i() ## células desenhadas
var cell_px := 1.0 ## lado de uma célula na tela
var origin := Vector2.ZERO
var buildings_at: Dictionary = {} ## célula -> id do prédio
var roads: Dictionary = {}
var lots: Dictionary = {}
var reserved: Dictionary = {}
var people: Dictionary = {} ## id -> ponto na tela (desenhado no último quadro; testável e para o clique)
var _people_clock := 0.0


func _init() -> void:
	visible = false
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP


func refresh() -> void:
	var store = app.store
	roads = app.world_view.ground.drawn_roads
	lots = app.world_view.ground.lot_cells
	reserved = app.world_view.ground.reserved
	buildings_at = {}
	for id in store.buildings:
		var info: Dictionary = store.buildings[id]
		for entry in info.footprint:
			buildings_at[info.cell + entry.cell] = int(id)
	if store.kind == "City" and store.city.has("bounds"):
		area = store.city.bounds
		for cell in roads.keys() + buildings_at.keys() + lots.keys():
			area = area.expand(cell).expand(cell + Vector2i.ONE)
		area = area.grow(1)
	else:
		area = Rect2i(Vector2i.ZERO, store.map_size)
	queue_redraw()


func _layout() -> void:
	var room := (size - Vector2(LEGEND_WIDTH + 30.0 + RIGHT, TOP + 40.0)).max(Vector2(40, 40))
	cell_px = clampf(minf(room.x / maxf(area.size.x, 1), room.y / maxf(area.size.y, 1)), 1.0, 48.0)
	origin = Vector2(LEGEND_WIDTH + 20.0, TOP) + (room - Vector2(area.size) * cell_px) / 2.0


func cell_rect(cell: Vector2i, inset := 0.0) -> Rect2:
	var at := origin + Vector2(cell - area.position) * cell_px
	return Rect2(at + Vector2.ONE * inset * cell_px, Vector2.ONE * cell_px * (1.0 - 2.0 * inset))


func cell_at(point: Vector2) -> Vector2i:
	return area.position + Vector2i(((point - origin) / cell_px).floor())


func _process(delta: float) -> void:
	if not visible:
		return
	_people_clock += delta
	if _people_clock >= 1.0 / PEOPLE_REFRESH:
		_people_clock = 0.0
		queue_redraw()


## Ponto da planta de um lugar do mundo 3D (x, z em células).
func plan_point(at: Vector3) -> Vector2:
	return origin + (Vector2(at.x, at.z) - Vector2(area.position)) * cell_px


## Pessoa desenhada perto do ponto da tela (até meia célula, no mínimo 6 px); -1 se nenhuma.
func person_at(point: Vector2) -> int:
	var best := -1
	var best_distance := maxf(cell_px * 0.5, 6.0)
	for id in people:
		var distance: float = (people[id] as Vector2).distance_to(point)
		if distance < best_distance:
			best_distance = distance
			best = int(id)
	return best


## Pessoa > prédio > rua > lote; {} = terreno comum.
func entity_at_point(point: Vector2) -> Dictionary:
	var person := person_at(point)
	if person >= 0:
		return Registry.ref(Registry.PERSON, person)
	return entity_at(cell_at(point))


## Prédio > rua > lote; {} = terreno comum.
func entity_at(cell: Vector2i) -> Dictionary:
	if buildings_at.has(cell):
		return Registry.ref(Registry.BUILDING, int(buildings_at[cell]))
	return app.world_view.ground.entity_at(cell)


func _draw() -> void:
	_layout()
	var store = app.store
	draw_rect(Rect2(Vector2.ZERO, size), BACKGROUND)
	draw_rect(Rect2(origin, Vector2(area.size) * cell_px), Color(0.36, 0.42, 0.3))
	for cell in store.rivers:
		if area.has_point(cell):
			draw_rect(cell_rect(cell), WATER)
	if store.kind == "World":
		for id in store.cities:
			var bounds: Rect2i = store.cities[id].bounds
			draw_rect(Rect2(origin + Vector2(bounds.position - area.position) * cell_px, Vector2(bounds.size) * cell_px),
				Color(1, 1, 1, 0.5), false, 1.0)
	for cell in lots:
		if String(lots[cell].kind) == "croft":
			draw_rect(cell_rect(cell), CROFT_PLAN)
		else: # caminho da porta: liga às células vizinhas do mesmo lote, à casa e à rua
			var box := cell_rect(cell, 0.33)
			draw_rect(box, Style.PATH_COLOR)
			var owner := int(lots[cell].building)
			for dir in DIRS:
				var next: Vector2i = cell + dir
				if int(lots.get(next, {}).get("building", -1)) == owner or int(buildings_at.get(next, -1)) == owner or roads.has(next):
					var edge := box.get_center() + Vector2(dir) * cell_px * 0.5
					draw_line(box.get_center(), edge, Style.PATH_COLOR, cell_px * 0.34)
	_fences()
	for cell in reserved: # traçado reservado sem uso: só um traço discreto, não é rua
		if area.has_point(cell):
			draw_rect(cell_rect(cell, 0.44), RESERVE)
	var heat: bool = app.world_view.ground.heat
	var top: int = app.world_view.ground.top_wear
	for cell in roads:
		var style := Style.style_of(roads[cell])
		if heat:
			style.plan = Style.heat_color(int(roads[cell].get("wear", 0)), top)
		var width := float(style.width)
		var box := cell_rect(cell, 0.5 - width / 2.0)
		draw_rect(box, Color(style.plan))
		for dir in DIRS: # braço até a rua vizinha: a rua se lê como linha contínua, não como pontos
			if roads.has(cell + dir):
				var arm := minf(width, float(Style.style_of(roads[cell + dir]).width)) * cell_px
				var center := box.get_center()
				var edge := center + Vector2(dir) * cell_px * 0.5
				draw_rect(Rect2(center.min(edge) - Vector2(absf(dir.y), absf(dir.x)) * arm / 2.0,
					(edge - center).abs() + Vector2(absf(dir.y), absf(dir.x)) * arm), Color(style.plan))
		if bool(style.main) and not bool(style.square):
			draw_rect(cell_rect(cell, 0.42), Color("3b3128"))
		if not style.work.is_empty(): # obra: contorno e barra do quanto já foi feito
			var frame := cell_rect(cell, 0.06)
			draw_rect(frame, Style.WORK_STYLE.plan, false, maxf(cell_px * 0.08, 1.0))
			draw_rect(Rect2(frame.position + Vector2(0, frame.size.y * 0.78), Vector2(frame.size.x * float(style.work.progress) / 100.0,
				frame.size.y * 0.18)), Style.WORK_STYLE.plan)
	var font := ThemeDB.fallback_font
	for id in store.buildings:
		var info: Dictionary = store.buildings[id]
		var state: Dictionary = store.building_states.get(id, {})
		var color: Color = FUNCTION_COLORS.get(String(state.get("function", "")).get_slice(",", 0), UNKNOWN_BUILDING)
		for entry in info.footprint:
			draw_rect(cell_rect(info.cell + entry.cell, 0.02), color)
			if int(entry.material) == 2:
				draw_rect(cell_rect(info.cell + entry.cell, 0.3), Color("3b2a20"))
		var site: Dictionary = Works.of_building(store, int(id))
		if not site.is_empty() and not info.footprint.is_empty(): # AD-193: canteiro: contorno e barra do quanto a obra já fez
			var lo: Vector2i = info.cell + info.footprint[0].cell
			var hi: Vector2i = lo
			for entry in info.footprint:
				lo = Vector2i(mini(lo.x, info.cell.x + entry.cell.x), mini(lo.y, info.cell.y + entry.cell.y))
				hi = Vector2i(maxi(hi.x, info.cell.x + entry.cell.x), maxi(hi.y, info.cell.y + entry.cell.y))
			var frame := cell_rect(lo, 0.04).merge(cell_rect(hi, 0.04))
			var tone: Color = SITE_HALT if String(site.get("halt", "")) != "" else SITE_PLAN
			draw_rect(frame, tone, false, maxf(cell_px * 0.12, 1.5))
			draw_rect(Rect2(frame.position + Vector2(0, frame.size.y * 0.8), Vector2(frame.size.x * float(site.progress), frame.size.y * 0.16)), tone)
		if cell_px >= 14.0 and not info.footprint.is_empty():
			var at := cell_rect(info.cell + info.footprint[0].cell).position + Vector2(2, cell_px * 0.45)
			draw_string(font, at, Labels.text_for(info, state, app.registry), HORIZONTAL_ALIGNMENT_LEFT, cell_px * 3.0,
				int(clampf(cell_px * 0.4, 9, 14)), Color.WHITE)
		_access_mark(info)
	_people()
	_legend(font)


## Acesso da porta que não é rua nem caminho: quadradinho na cor do tipo na porta (sem acesso: vermelho, maior).
func _access_mark(info: Dictionary) -> void:
	var access := Style.access_of(info.get("lot", {}))
	if access in ["", "street", "path"]:
		return
	var door := Marks.door_of(info)
	var cell: Vector2i = door.cell if not door.is_empty() else info.cell
	var look := Style.access_style(access)
	var lot: Dictionary = info.get("lot", {})
	for along in lot.get("access_cells", []): # AD-185: o caminho do acesso, na cor de onde desemboca
		draw_rect(cell_rect(along, 0.36), Style.access_state_color(String(lot.get("access_state", ""))))
	var inset := 0.18 if bool(look.get("alert", false)) else 0.3
	draw_rect(cell_rect(cell, inset), Color(look.plan))
	if not door.is_empty():
		var center := cell_rect(cell).get_center()
		draw_line(center, center + Vector2(door.out.x, door.out.z) * cell_px * 0.7, Color(look.plan), maxf(cell_px * 0.12, 1.5))


## Pessoas como pontos onde o boneco está desenhado agora (quem anda pela rota em amarelo; a selecionada com anel).
func _people() -> void:
	people = {}
	var nodes: Dictionary = app.world_view.npc_nodes
	var radius := clampf(cell_px * 0.16, 2.0, 6.0)
	for id in nodes:
		var actor = nodes[id]
		if not is_instance_valid(actor) or not actor.visible:
			continue
		var at := plan_point(actor.position)
		if not Rect2(origin, Vector2(area.size) * cell_px).grow(radius).has_point(at):
			continue
		people[int(id)] = at
		draw_circle(at, radius, WALKER if actor.walk != null else PERSON)
		if int(id) == app.selected_npc:
			draw_arc(at, radius + 3.0, 0.0, TAU, 16, WALKER, 1.5)


## Cerca nas bordas do quintal que não encostam no próprio quintal, na casa nem no caminho (a porteira).
func _fences() -> void:
	for cell in lots:
		if String(lots[cell].kind) != "croft":
			continue
		var owner := int(lots[cell].building)
		var r := cell_rect(cell)
		for dir in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
			var next: Vector2i = cell + dir
			if int(lots.get(next, {}).get("building", -1)) == owner or int(buildings_at.get(next, -1)) == owner:
				continue
			var a := r.get_center() + Vector2(dir) * cell_px * 0.5 + Vector2(dir.y, dir.x) * cell_px * 0.5
			var b := r.get_center() + Vector2(dir) * cell_px * 0.5 - Vector2(dir.y, dir.x) * cell_px * 0.5
			draw_line(a, b, FENCE_PLAN, maxf(cell_px * 0.08, 1.0))


func _legend(font: Font) -> void:
	var title := "Planta de %s" % String(app.store.city.get("name", "")) if app.store.kind == "City" else "Planta do mundo"
	draw_string(font, Vector2(16, TOP + 18), title, HORIZONTAL_ALIGNMENT_LEFT, LEGEND_WIDTH, 18, Color.WHITE)
	var rows: Array = []
	for surface in Style.SURFACES:
		rows.append([Style.SURFACES[surface].plan, "Rua: %s" % Style.SURFACES[surface].name])
	rows.append([Style.PLAN_SQUARE, "Largo"])
	rows.append([Color("3b3128"), "Rua principal (marco no meio)"])
	rows.append([CROFT_PLAN, "Quintal (cercado)"])
	rows.append([Style.PATH_COLOR, "Caminho da porta até a rua"])
	for key in FUNCTION_COLORS:
		rows.append([FUNCTION_COLORS[key], "Prédio: %s" % Describe.FUNCTION_NAMES.get(key, key)])
	rows.append([UNKNOWN_BUILDING, "Prédio sem função conhecida"])
	rows.append([Color("3b2a20"), "Porta"])
	rows.append([WATER, "Água"])
	rows.append([Style.WORK_STYLE.plan, "Obra de calçamento (barra = feito)"])
	rows.append([SITE_PLAN, "Canteiro de obra (barra = feito)"])
	rows.append([SITE_HALT, "Obra parada (tempo, gente ou material)"])
	rows.append([RESERVE, "Traçado reservado, sem uso"])
	for key in ["alley", "stair", "roof", "none"]:
		rows.append([Style.ACCESS_STYLES[key].plan, "Acesso: %s" % Style.ACCESS_STYLES[key].name])
	rows.append([PERSON, "Pessoa"])
	rows.append([WALKER, "Pessoa andando pela rota"])
	if app.world_view.ground.heat:
		rows.append([Style.heat_color(0, 1), "Pisoteio: pouco"])
		rows.append([Style.heat_color(1, 1), "Pisoteio: o mais pisado (%d)" % app.world_view.ground.top_wear])
	var y := TOP + 48.0
	for row in rows:
		draw_rect(Rect2(16, y - 12, 16, 16), row[0])
		draw_string(font, Vector2(40, y + 1), row[1], HORIZONTAL_ALIGNMENT_LEFT, LEGEND_WIDTH - 30, 13, Color(1, 1, 1, 0.9))
		y += 22.0
	draw_string(font, Vector2(16, y + 14), "Clique: ficha · H: calor do pisoteio · P ou Esc: fecha", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(1, 1, 1, 0.6))


func update_tooltip(mouse: Vector2) -> void:
	var entity := entity_at_point(mouse) if get_global_rect().has_point(mouse) else {}
	var text: String = app.selection.describe(entity) if not entity.is_empty() else ""
	app.tooltip.visible = text != ""
	if app.tooltip.visible:
		app.tooltip_label.text = text
		app.tooltip.position = mouse + Vector2(16, 18)


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var entity := entity_at_point(event.position)
		if not entity.is_empty():
			app.inspect(entity)
		accept_event()
