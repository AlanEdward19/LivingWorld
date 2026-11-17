extends Control
## Planta baixa (P, AD-177): a cidade vista de cima, em 2D, só com o que o motor expôs: ruas pela superfície (largo e rua
## principal marcados), lotes (quintal cercado, caminho da porta), prédios coloridos pela função e água; legenda ao lado.
## No mapa-múndi, o mapa inteiro com as ruas de todas as cidades e os limites delas. Clique abre a ficha; a dica diz o
## que é. Desenha só quando abre ou quando a store muda (nada por quadro).

const Style := preload("res://Scenes/render/street_style.gd")
const Describe := preload("res://Scenes/inspector_describe.gd")
const Labels := preload("res://Scenes/render/building_labels.gd")
const Registry := preload("res://Store/entity_registry.gd")

const FUNCTION_COLORS := {"dwelling": Color("d9a066"), "lodging": Color("c77dba"), "workplace": Color("5b8fd1"),
	"structure": Color("9a9a9a")}
const UNKNOWN_BUILDING := Color("b9a48a")
const WATER := Color("3f6f99")
const CROFT_PLAN := Color("7fa65a")
const FENCE_PLAN := Color("4b3a24")
const BACKGROUND := Color(0.11, 0.12, 0.11, 0.95)
const LEGEND_WIDTH := 250.0

var app
var area := Rect2i() ## células desenhadas
var cell_px := 1.0 ## lado de uma célula na tela
var origin := Vector2.ZERO
var buildings_at: Dictionary = {} ## célula -> id do prédio
var roads: Dictionary = {}
var lots: Dictionary = {}


func _init() -> void:
	visible = false
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP


func refresh() -> void:
	var store = app.store
	roads = app.world_view.ground.drawn_roads
	lots = app.world_view.ground.lot_cells
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
	var room := size - Vector2(LEGEND_WIDTH + 40.0, 70.0)
	cell_px = clampf(minf(room.x / maxf(area.size.x, 1), room.y / maxf(area.size.y, 1)), 1.0, 48.0)
	origin = Vector2(LEGEND_WIDTH + 20.0, 50.0) + (room - Vector2(area.size) * cell_px) / 2.0


func cell_rect(cell: Vector2i, inset := 0.0) -> Rect2:
	var at := origin + Vector2(cell - area.position) * cell_px
	return Rect2(at + Vector2.ONE * inset * cell_px, Vector2.ONE * cell_px * (1.0 - 2.0 * inset))


func cell_at(point: Vector2) -> Vector2i:
	return area.position + Vector2i(((point - origin) / cell_px).floor())


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
		else:
			draw_rect(cell_rect(cell, 0.3), Style.PATH_COLOR)
	_fences()
	for cell in roads:
		var style := Style.style_of(roads[cell])
		draw_rect(cell_rect(cell, 0.5 - float(style.width) / 2.0), Color(style.plan))
		if bool(style.main) and not bool(style.square):
			draw_rect(cell_rect(cell, 0.42), Color("3b3128"))
	var font := ThemeDB.fallback_font
	for id in store.buildings:
		var info: Dictionary = store.buildings[id]
		var state: Dictionary = store.building_states.get(id, {})
		var color: Color = FUNCTION_COLORS.get(String(state.get("function", "")).get_slice(",", 0), UNKNOWN_BUILDING)
		for entry in info.footprint:
			draw_rect(cell_rect(info.cell + entry.cell, 0.02), color)
			if int(entry.material) == 2:
				draw_rect(cell_rect(info.cell + entry.cell, 0.3), Color("3b2a20"))
		if cell_px >= 14.0 and not info.footprint.is_empty():
			var at := cell_rect(info.cell + info.footprint[0].cell).position + Vector2(2, cell_px * 0.45)
			draw_string(font, at, Labels.text_for(info, state, app.registry), HORIZONTAL_ALIGNMENT_LEFT, cell_px * 3.0,
				int(clampf(cell_px * 0.4, 9, 14)), Color.WHITE)
	_legend(font)


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
	draw_string(font, Vector2(16, 30), title, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color.WHITE)
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
	var y := 56.0
	for row in rows:
		draw_rect(Rect2(16, y - 12, 16, 16), row[0])
		draw_string(font, Vector2(40, y + 1), row[1], HORIZONTAL_ALIGNMENT_LEFT, LEGEND_WIDTH - 30, 13, Color(1, 1, 1, 0.9))
		y += 22.0
	draw_string(font, Vector2(16, y + 14), "Clique: ficha · P ou Esc: fecha", HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color(1, 1, 1, 0.6))


func update_tooltip(mouse: Vector2) -> void:
	var entity := entity_at(cell_at(mouse)) if get_global_rect().has_point(mouse) else {}
	var text: String = app.selection.describe(entity) if not entity.is_empty() else ""
	app.tooltip.visible = text != ""
	if app.tooltip.visible:
		app.tooltip_label.text = text
		app.tooltip.position = mouse + Vector2(16, 18)


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var entity := entity_at(cell_at(event.position))
		if not entity.is_empty():
			app.inspect(entity)
		accept_event()
