extends Control
## #264: modo comprar e construir. A câmera sobe sobre a própria casa; o painel mostra o catálogo que o motor diz (móveis à venda por categoria, com miniatura
## desenhada por código, preço, efeito, beleza e o estoque da loja), a lista do que a casa tem (mover, vender, descartar) e a obra (parede, porta, ampliar,
## pintar). O fantasma do móvel segue o mouse em grade, gira com Q/E (ou R) e fica verde ou vermelho pelo que o MOTOR responde (`/home/check`), com o motivo
## quando não pode. Nada daqui decide: o cliente pergunta, mostra a resposta e manda o comando `interact {kind:"home"}`; o motor valida e aplica.

const Grid := preload("res://Scenes/grid.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")
const FurnitureThumb := preload("res://Scenes/furniture_thumb.gd")

signal opened()
signal closed()
signal acted(note: String) ## o motor aceitou o comando: o app relê a casa e redesenha
signal warning(text: String)

const THUMB_PX := 64
const PANEL_W := 372
const GOOD := Color(0.25, 0.85, 0.40, 0.55)
const BAD := Color(0.90, 0.25, 0.22, 0.55)
const NEUTRAL := Color(0.9, 0.9, 0.9, 0.35)
const NO_CELL := Vector2i(-9999, -9999)
const TABS := [["buy", "Loja"], ["mine", "Na casa"], ["build", "Construir"]] ## #265: Loja = comprar e escolher como trazer; Na casa = o que já está em casa ou no depósito
const TOOLS := [["wall", "Levantar parede"], ["open", "Derrubar parede"], ["door", "Abrir porta"], ["shut", "Fechar porta"], ["extend", "Ampliar a casa"],
	["paint_floor", "Pintar o piso"], ["paint_wall", "Pintar as paredes"]]
const TOOL_HINT := {"wall": "Clique na aresta entre duas células para levantar uma parede.", "open": "Clique numa parede de dentro para derrubá-la.",
	"door": "Clique numa parede de dentro para abrir uma porta.", "shut": "Clique numa porta de dentro para fechá-la.",
	"extend": "Clique numa célula encostada na casa para ampliar o piso.", "paint_floor": "Escolha a tinta e clique no cômodo.",
	"paint_wall": "Escolha a tinta e clique no cômodo: as paredes dele ganham a cor."}
const NEEDS := {"hunger": "Fome", "thirst": "Sede", "sleep": "Sono", "social": "Social"}

var api
var player_id := ""
var rig
var stage
var modal
var building_of: Callable ## () -> int: o prédio em foco (o modo comprar só abre na casa própria)
var catalog_of: Callable ## () -> Array: os descritores do catálogo (o retrato da casa)
var info_of: Callable ## () -> Dictionary: o retrato da casa (planta, cômodos)
var home: Dictionary = {}
var is_open := false
var tab := "buy"
var category := ""
var selected := "" ## id do objeto que se compra
var moving := -1 ## índice da peça que se move
var placing := -1 ## #265: número do móvel do depósito que se está pondo no lugar
var tool := ""
var paint_id := ""
var facing := 0
var floor_level := 0
var hover := NO_CELL ## célula local sob o mouse
var hover_edge: Array = [] ## [ax, ay, bx, by] da aresta mais perto do mouse
var verdict: Dictionary = {}
var verdict_for := ""
var cards: Dictionary = {} ## id do objeto -> card do catálogo
var piece_rows: Dictionary = {} ## índice da peça -> linha da lista
var factory
var descriptors: Dictionary = {}
var ghost: Node3D
var overlay: Node3D
var last_note := ""
var mouse_follow := true ## o fantasma segue o mouse (os passos de captura o desligam e mandam a célula)

var _top: PanelContainer
var _side: PanelContainer
var _rooms_panel: PanelContainer
var _foot: PanelContainer
var _money: Label
var _title: Label
var _list: VBoxContainer
var _tabs_row: HBoxContainer
var _category_row: HFlowContainer
var _reason: Label
var _hint: Label
var _rooms_box: VBoxContainer
var _tab_buttons: Dictionary = {}
var _grid_mesh: MeshInstance3D
var _mark: MeshInstance3D
var _pending := false
var _ghost_id := ""
var _view_size := 12.0


func _init() -> void:
	set_anchors_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	_build_ui()


# --- painel ---------------------------------------------------------------------------------------------------------------------------------------

static func _style(color: Color, radius: int = 10) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(radius)
	style.content_margin_left = 10
	style.content_margin_right = 10
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	return style


static func _label(text: String, size: int = 14, color: Color = Color(0.92, 0.93, 0.96)) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return label


func _build_ui() -> void:
	_top = PanelContainer.new()
	_top.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.92)))
	_top.set_anchors_preset(Control.PRESET_TOP_WIDE)
	_top.offset_left = 12
	_top.offset_right = -12
	_top.offset_top = 8
	var top_row := HBoxContainer.new()
	top_row.add_theme_constant_override("separation", 10)
	_top.add_child(top_row)
	_title = _label("Modo comprar", 20)
	_title.autowrap_mode = TextServer.AUTOWRAP_OFF
	top_row.add_child(_title)
	_tabs_row = HBoxContainer.new()
	for entry in TABS:
		var button := Button.new()
		button.text = String(entry[1])
		button.toggle_mode = true
		button.name = "tab_" + String(entry[0])
		var key := String(entry[0])
		button.pressed.connect(func(): set_tab(key))
		_tabs_row.add_child(button)
		_tab_buttons[key] = button
	top_row.add_child(_tabs_row)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	top_row.add_child(spacer)
	_money = _label("", 18, Color("f2d36b"))
	_money.name = "money"
	_money.autowrap_mode = TextServer.AUTOWRAP_OFF
	top_row.add_child(_money)
	var close_button := Button.new()
	close_button.text = "Fechar (B)"
	close_button.pressed.connect(close)
	top_row.add_child(close_button)
	add_child(_top)

	_side = PanelContainer.new()
	_side.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.92)))
	_side.set_anchors_preset(Control.PRESET_LEFT_WIDE)
	_side.offset_left = 12
	_side.offset_top = 64
	_side.offset_bottom = -64
	_side.offset_right = 12 + PANEL_W
	var side_box := VBoxContainer.new()
	side_box.add_theme_constant_override("separation", 6)
	_side.add_child(side_box)
	_category_row = HFlowContainer.new()
	side_box.add_child(_category_row)
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	side_box.add_child(scroll)
	_list = VBoxContainer.new()
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_list.add_theme_constant_override("separation", 6)
	scroll.add_child(_list)
	add_child(_side)

	_rooms_panel = PanelContainer.new()
	_rooms_panel.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.88)))
	_rooms_panel.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_rooms_panel.offset_left = -300
	_rooms_panel.offset_right = -12
	_rooms_panel.offset_top = 64
	_rooms_box = VBoxContainer.new()
	_rooms_panel.add_child(_rooms_box)
	add_child(_rooms_panel)

	_foot = PanelContainer.new()
	_foot.add_theme_stylebox_override("panel", _style(Color(0.05, 0.06, 0.09, 0.92)))
	_foot.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	_foot.offset_left = 12 + PANEL_W + 12
	_foot.offset_right = -12
	_foot.offset_bottom = -12
	_foot.offset_top = -56
	var foot_box := VBoxContainer.new()
	_foot.add_child(foot_box)
	_reason = _label("", 16)
	_reason.name = "reason"
	foot_box.add_child(_reason)
	_hint = _label("", 13, Color(0.7, 0.74, 0.82))
	foot_box.add_child(_hint)
	add_child(_foot)
	for panel in [_top, _side, _rooms_panel, _foot]:
		panel.mouse_filter = Control.MOUSE_FILTER_STOP


# --- abrir e fechar ---------------------------------------------------------------------------------------------------------------------------------

## Lê o modo comprar do motor e sobe a câmera. Devolve falso (e avisa) sem casa própria ou sem o modo neste mundo.
func open() -> bool:
	if is_open:
		return true
	var response: Dictionary = await api.player_home(player_id)
	if not response.ok or not response.body is Dictionary:
		warning.emit("Sem modo comprar aqui: precisa de casa própria num mundo com loja de móveis.")
		return false
	home = response.body
	if building_of.is_valid() and int(building_of.call()) != int(home.get("building", -1)):
		warning.emit("O modo comprar abre dentro da sua casa.")
		home = {}
		return false
	_prepare()
	is_open = true
	visible = true
	if stage != null and stage.interior_view != null:
		floor_level = stage.interior_view.current_floor
		_raise_camera()
		stage.interior_view.wall_mode = stage.interior_view.WALLS_DOWN # de cima: todas as paredes baixas, a casa se vê inteira
		stage.interior_view._last_fade_at = Vector3(INF, INF, INF)
	set_tab("buy")
	_rebuild_overlay()
	opened.emit()
	return true


func close() -> void:
	if not is_open:
		return
	is_open = false
	visible = false
	_clear_selection()
	if stage != null and stage.interior_view != null:
		stage.interior_view.wall_mode = stage.interior_view.WALLS_CUT
		stage.interior_view._last_fade_at = Vector3(INF, INF, INF)
	if rig != null:
		rig.set_build_view(false)
	if overlay != null and is_instance_valid(overlay):
		overlay.queue_free()
		overlay = null
	if ghost != null and is_instance_valid(ghost):
		ghost.queue_free()
		ghost = null
	_ghost_id = ""
	closed.emit()


func _prepare() -> void:
	descriptors = {}
	var list: Array = catalog_of.call() if catalog_of.is_valid() else []
	for descriptor in list:
		descriptors[String(descriptor.id)] = descriptor
	factory = ObjectFactory.new(list)


func _raise_camera() -> void:
	if rig == null or stage == null:
		return
	var view = stage.interior_view
	var span: float = view.span_cells()
	_view_size = maxf(span * 1.05 + 3.0, 9.0)
	var middle: Vector3 = view.center()
	var height := float(get_viewport().get_visible_rect().size.y) if is_inside_tree() else 720.0
	var shift := _view_size / maxf(height, 1.0) * float(PANEL_W) * 0.5 # a casa fica no meio do que o painel não cobre
	rig.set_build_view(true, Vector3(middle.x - shift, middle.y + float(floor_level) * Grid.floor_height(), middle.z), _view_size, 0.0)


## Relê o motor (dinheiro, estoque, peças, beleza) e redesenha o painel; chamado depois de cada comando aceito.
func reload() -> void:
	var response: Dictionary = await api.player_home(player_id)
	if response.ok and response.body is Dictionary:
		home = response.body
		if moving >= 0 and piece_by_index(moving).is_empty():
			moving = -1
		_rebuild()
		_rebuild_overlay()


## A casa foi redesenhada (a obra mudou a planta): a grade e as marcas se refazem sobre a nova planta e o catálogo do retrato.
func after_redraw() -> void:
	if not is_open:
		return
	_prepare()
	if stage != null and stage.interior_view != null:
		stage.interior_view.wall_mode = stage.interior_view.WALLS_DOWN
		stage.interior_view._last_fade_at = Vector3(INF, INF, INF)
		_raise_camera()
	if ghost != null and is_instance_valid(ghost):
		ghost.queue_free()
		ghost = null
		_ghost_id = ""
	_rebuild_overlay()
	_rebuild()


# --- abas, categorias, seleção -------------------------------------------------------------------------------------------------------------------

func set_tab(value: String) -> void:
	tab = value
	_clear_selection()
	for key in _tab_buttons:
		_tab_buttons[key].button_pressed = key == tab
	_title.text = "Modo comprar" if tab != "build" else "Modo construir"
	_hint.text = {"buy": "Compre na loja: o móvel fica reservado em seu nome. Traga nas mãos, de carro ou peça a entrega; depois ele vai para o depósito da casa.",
		"mine": "Posicione o que está no depósito, ou clique numa peça da casa (ou em Mover) para mexer nela.",
		"build": "Escolha a obra, depois clique na casa. O motor recusa o que prenderia alguém."}.get(tab, "")
	_rebuild()


func set_category(value: String) -> void:
	category = value
	_rebuild()


func _clear_selection() -> void:
	selected = ""
	moving = -1
	placing = -1
	tool = ""
	verdict = {}
	verdict_for = ""
	hover = NO_CELL
	hover_edge = []
	_update_ghost()
	_show_verdict()


## #265: escolher o móvel na loja mostra o peso, o volume e como trazer; comprar é o botão do cartão (o móvel não aparece em casa: fica na loja, em seu nome).
func select_item(object_id: String) -> void:
	if home_item(object_id).is_empty():
		return
	moving = -1
	placing = -1
	tool = ""
	selected = object_id
	verdict = {}
	verdict_for = ""
	_update_ghost()
	_hint.text = "Compre pelo botão do cartão; depois escolha como trazer."


## #265: pôr no lugar um móvel do depósito da casa: o fantasma segue o mouse e o motor valida o lugar (`place:seq:andar:x:y:giro`).
func start_place(seq: int) -> void:
	var data := parcel_by_seq(seq)
	if data.is_empty() or String(data.where) != "depot":
		return
	selected = String(data.id)
	placing = seq
	moving = -1
	tool = ""
	verdict = {}
	verdict_for = ""
	_update_ghost()
	_hint.text = "Q / E giram · clique põe no lugar · botão direito ou Esc cancela"


func start_move(piece: int) -> void:
	var data := piece_by_index(piece)
	if data.is_empty():
		return
	selected = String(data.id)
	moving = piece
	facing = int(data.facing)
	tool = ""
	verdict = {}
	verdict_for = ""
	_update_ghost()
	_hint.text = "Movendo %s: Q / E giram · clique põe aqui · botão direito cancela" % String(data.name)


func use_tool(tool_name: String) -> void:
	tab = "build"
	selected = ""
	moving = -1
	tool = tool_name
	verdict = {}
	verdict_for = ""
	if tool_name == "paint_floor" or tool_name == "paint_wall":
		var kind := "floor" if tool_name == "paint_floor" else "wall"
		var found := false
		for paint in home.get("paints", []):
			if String(paint.kind) == kind and paint_id == String(paint.id):
				found = true
		if not found:
			paint_id = ""
			for paint in home.get("paints", []):
				if String(paint.kind) == kind and float(paint.get("price", 0)) > 0 and paint_id == "":
					paint_id = String(paint.id)
	_update_ghost()
	_hint.text = String(TOOL_HINT.get(tool_name, ""))
	_rebuild()


func rotate_by(steps: int) -> void:
	facing = posmod(facing + 90 * steps, 360)
	verdict = {}
	verdict_for = ""
	_update_ghost()


# --- o que o motor disse -------------------------------------------------------------------------------------------------------------------------

func home_item(object_id: String) -> Dictionary:
	for item in home.get("items", []):
		if String(item.object) == object_id:
			return item
	return {}


## Os móveis pagos ainda não posicionados (na loja, nas mãos, no carro, a caminho ou no depósito), como o motor os conta.
func parcels() -> Array:
	var haul: Variant = home.get("haul")
	return haul.get("parcels", []) if haul is Dictionary else []


func parcel_by_seq(seq: int) -> Dictionary:
	for data in parcels():
		if int(data.seq) == seq:
			return data
	return {}


func piece_by_index(piece: int) -> Dictionary:
	for data in home.get("pieces", []):
		if int(data.piece) == piece:
			return data
	return {}


func piece_at(cell: Vector2i) -> Dictionary:
	for data in home.get("pieces", []):
		if int(data.floor) == floor_level and int(data.x) == cell.x and int(data.y) == cell.y:
			return data
	return {}


## O texto de um campo do motor; nulo (omitido no JSON) é vazio.
static func _text(value: Variant) -> String:
	return "" if value == null else String(value)


static func _num(value: float) -> String:
	return str(int(value)) if is_equal_approx(value, roundf(value)) else "%.1f" % value


static func beauty_text(beauty: float) -> String:
	return "Beleza %s%s" % ["+" if beauty > 0 else "", _num(beauty)]


## "Em estoque: 2" / "Esgotado: sob encomenda por 22, pronto em 8 h" / "Esgotado".
static func stock_text(item: Dictionary) -> String:
	if int(item.get("stock", 0)) > 0:
		return "Em estoque: %d" % int(item.stock)
	if bool(item.get("orderable", false)):
		return "Esgotado: sob encomenda por %d, pronto em %d h" % [int(item.get("orderPrice", item.get("price", 0))), int(item.get("craftHours", 0))]
	return "Esgotado na loja"


static func effect_text(item: Dictionary) -> String:
	var parts: Array = []
	for effect in item.get("effects", []):
		var need := _text(effect.get("needName"))
		if need == "":
			need = String(NEEDS.get(String(effect.need), String(effect.need).capitalize()))
		parts.append("%s %s%s/h" % [need, "+" if float(effect.perHour) > 0 else "", _num(float(effect.perHour))])
		if parts.size() >= 2:
			break
	return ", ".join(parts)


func _rebuild() -> void:
	_money.text = "Dinheiro: %d" % int(home.get("wallet", 0))
	for child in _category_row.get_children():
		child.queue_free()
	for child in _list.get_children():
		child.queue_free()
	cards = {}
	piece_rows = {}
	_category_row.visible = tab == "buy"
	if tab == "buy":
		_add_category("", "Tudo")
		for entry in home.get("categories", []):
			_add_category(String(entry.id), String(entry.name))
		if _text(home.get("shopNote")) != "":
			_list.add_child(_label(_text(home.get("shopNote")), 14, Color("e8a090")))
		var coming := false
		for data in parcels():
			if String(data.where) != "depot":
				if not coming:
					_list.add_child(_label("Seus pedidos (pagos, ainda não estão em casa)", 15, Color("f2d36b")))
					coming = true
				_list.add_child(_parcel_row(data))
		for item in home.get("items", []):
			if category == "" or String(item.category) == category:
				_list.add_child(_card(item))
	elif tab == "mine":
		var depot := parcels().filter(func(p): return String(p.where) == "depot")
		if not depot.is_empty():
			_list.add_child(_label("Depósito da casa (já em casa, falta pôr no lugar)", 15, Color("f2d36b")))
			for data in depot:
				_list.add_child(_parcel_row(data))
		_list.add_child(_label("Clique numa peça para mexer nela. Guardar leva ao depósito; vender devolve %d%% do preço (a loja do bairro paga do caixa dela); descartar não devolve nada." % int(home.get("resalePercent", 0)), 13, Color(0.7, 0.74, 0.82)))
		for data in home.get("pieces", []):
			_list.add_child(_piece_row(data))
	else:
		_build_tools()
	_rebuild_rooms()
	_show_verdict()


func _add_category(id: String, title: String) -> void:
	var button := Button.new()
	button.text = title
	button.toggle_mode = true
	button.button_pressed = id == category
	button.pressed.connect(func(): set_category(id))
	_category_row.add_child(button)


func _card(item: Dictionary) -> Control:
	var id := String(item.object)
	var panel := PanelContainer.new()
	panel.name = "card_" + id
	var selected_now := id == selected and moving < 0
	panel.add_theme_stylebox_override("panel", _style(Color(0.18, 0.30, 0.42, 0.95) if selected_now else Color(0.11, 0.13, 0.18, 0.95), 8))
	panel.mouse_filter = Control.MOUSE_FILTER_STOP
	panel.gui_input.connect(func(event: InputEvent):
		if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
			select_item(id)
			_rebuild())
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	panel.add_child(row)
	if factory != null and descriptors.has(id):
		var thumb := FurnitureThumb.make(factory, descriptors[id], THUMB_PX)
		thumb.mouse_filter = Control.MOUSE_FILTER_IGNORE
		row.add_child(thumb)
	var column := VBoxContainer.new()
	column.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(column)
	var out_of_stock := int(item.get("stock", 0)) <= 0
	var price := int(item.get("orderPrice", item.price)) if out_of_stock and bool(item.get("orderable", false)) else int(item.price)
	var title := _label("%s · %d" % [String(item.name), price], 16)
	title.name = "title"
	column.add_child(title)
	column.add_child(_label("%s%s" % [beauty_text(float(item.beauty)), " · " + effect_text(item) if effect_text(item) != "" else ""], 12, Color(0.78, 0.84, 0.95)))
	var stock := _label(stock_text(item), 12, Color("9fd6b0") if not out_of_stock else Color("e8c070") if bool(item.get("orderable", false)) else Color("e8a090"))
	stock.name = "stock"
	column.add_child(stock)
	var haul: Variant = home.get("haul")
	var heft: Variant = haul.get("items", {}).get(id) if haul is Dictionary else null
	if heft is Dictionary:
		var heft_label := _label(heft_text(heft), 12, Color(0.78, 0.84, 0.95))
		heft_label.name = "heft"
		column.add_child(heft_label)
	if selected_now:
		var buy := Button.new()
		buy.name = "buy_button"
		buy.text = "Comprar por %d" % price
		buy.disabled = int(home.get("wallet", 0)) < price or (out_of_stock and not bool(item.get("orderable", false)))
		buy.pressed.connect(func(): command("buy:%s" % id))
		column.add_child(buy)
	cards[id] = panel
	return panel


## "12 kg · 120 L · cabe nas mãos" / "45 kg · 900 L · precisa de carro ou entrega": do peso e do volume que o motor diz (dado do cenário).
static func heft_text(heft: Dictionary) -> String:
	var how: String = {"hands": "cabe nas mãos", "helper": "precisa de duas pessoas, carro ou entrega", "vehicle": "precisa de carro ou entrega"}.get(String(heft.get("carry", "")), "")
	return "%s kg · %s L · %s" % [_num(float(heft.get("kg", 0))), _num(float(heft.get("liters", 0))), how]


## Um móvel pago: onde está de verdade e o que dá para fazer com ele agora (cada botão é uma pergunta já respondida pelo motor; o que não vale mostra o porquê).
func _parcel_row(data: Dictionary) -> Control:
	var panel := PanelContainer.new()
	panel.name = "parcel_%d" % int(data.seq)
	panel.add_theme_stylebox_override("panel", _style(Color(0.11, 0.15, 0.13, 0.95), 8))
	var column := VBoxContainer.new()
	column.add_theme_constant_override("separation", 4)
	panel.add_child(column)
	var id := String(data.id)
	var head := HBoxContainer.new()
	head.add_theme_constant_override("separation", 8)
	column.add_child(head)
	if factory != null and descriptors.has(id):
		head.add_child(FurnitureThumb.make(factory, descriptors[id], 40))
	var title := _label("%s · %s" % [String(data.name), parcel_status(data)], 14)
	title.name = "status"
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	head.add_child(title)
	var buttons := HFlowContainer.new()
	column.add_child(buttons)
	if String(data.where) == "depot":
		var put := Button.new()
		put.name = "place_button"
		put.text = "Posicionar"
		put.pressed.connect(func():
			start_place(int(data.seq))
			_rebuild())
		buttons.add_child(put)
	for action in data.get("actions", []):
		var verb := String(action.verb)
		var kind: String = verb.split(":")[0]
		var button := Button.new()
		button.name = "act_" + kind
		button.text = action_title(kind, action)
		button.disabled = not bool(action.ok)
		button.tooltip_text = String(action.get("reason", ""))
		button.pressed.connect(func(): command(verb))
		buttons.add_child(button)
		if not bool(action.ok) and kind == "fetch" and String(action.get("reason", "")) != "":
			var why := _label(String(action.reason), 12, Color("e8a090"))
			why.name = "why_fetch"
			column.add_child(why)
	return panel


static func parcel_status(data: Dictionary) -> String:
	match String(data.where):
		"shop":
			return "esperando na loja" if int(data.get("readyInMs", 0)) <= 0 else "a loja está fazendo (%d min)" % int(ceil(float(data.readyInMs) / 60000.0))
		"hands":
			return "nas mãos de %s" % String(data.get("carrier", "alguém"))
		"car":
			return "no porta-malas"
		"courier":
			return "%s está trazendo (%d min)" % [String(data.get("courier", "o entregador")), maxi(1, int(ceil(float(data.get("dueInMs", 0)) / 60000.0)))]
		"depot":
			return "no depósito"
	return ""


static func action_title(kind: String, action: Dictionary) -> String:
	return {"fetch": "Levar nas mãos", "fetchhelp": "Levar com ajuda", "load": "Carregar no carro", "deliver": "Pedir entrega (%d)" % int(action.get("price", 0)),
		"cancel": "Cancelar compra", "drop": "Deixar no depósito", "unload": "Descarregar", "sellp": "Vender"}.get(kind, kind)


func _piece_row(data: Dictionary) -> Control:
	var panel := PanelContainer.new()
	panel.name = "piece_%d" % int(data.piece)
	panel.add_theme_stylebox_override("panel", _style(Color(0.11, 0.13, 0.18, 0.95) if int(data.piece) != moving else Color(0.18, 0.30, 0.42, 0.95), 8))
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 6)
	panel.add_child(row)
	var id := String(data.id)
	if factory != null and descriptors.has(id):
		var thumb := FurnitureThumb.make(factory, descriptors[id], 44)
		row.add_child(thumb)
	var title := _label("%s · %s" % [String(data.name), beauty_text(float(data.beauty))], 14)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(title)
	var index := int(data.piece)
	var busy := bool(data.get("busy", false))
	var move := Button.new()
	move.text = "Mover"
	move.disabled = busy
	move.pressed.connect(func(): start_move(index))
	row.add_child(move)
	var stash := Button.new()
	stash.name = "stash"
	stash.text = "Guardar"
	stash.disabled = busy or not bool(data.sellable)
	stash.pressed.connect(func(): command("stash:%d" % index))
	row.add_child(stash)
	var sell := Button.new()
	sell.text = "Vender +%d" % int(data.resale)
	sell.disabled = busy or not bool(data.sellable)
	sell.pressed.connect(func(): command("sell:%d" % index))
	row.add_child(sell)
	var drop := Button.new()
	drop.text = "Descartar"
	drop.disabled = busy
	drop.pressed.connect(func(): command("discard:%d" % index))
	row.add_child(drop)
	piece_rows[index] = panel
	return panel


func _build_tools() -> void:
	var build: Dictionary = home.get("build", {})
	var prices := {"wall": int(build.get("wall", 0)), "open": int(build.get("demolish", 0)), "door": int(build.get("door", 0)), "shut": int(build.get("wall", 0)),
		"extend": int(build.get("cell", 0))}
	_list.add_child(_label("A obra é paga ao marceneiro do bairro. Nada prende ninguém: o motor recusa o que fecha o caminho até a porta.", 13, Color(0.7, 0.74, 0.82)))
	for entry in TOOLS:
		var tool_name := String(entry[0])
		var button := Button.new()
		button.name = "tool_" + tool_name
		button.toggle_mode = true
		button.button_pressed = tool_name == tool
		button.text = "%s%s" % [String(entry[1]), " · %d" % int(prices[tool_name]) if prices.has(tool_name) else ""]
		button.pressed.connect(func(): use_tool(tool_name))
		_list.add_child(button)
		if tool_name == tool and (tool_name == "paint_floor" or tool_name == "paint_wall"):
			_list.add_child(_paint_swatches("floor" if tool_name == "paint_floor" else "wall"))


func _paint_swatches(kind: String) -> Control:
	var flow := HFlowContainer.new()
	for paint in home.get("paints", []):
		if String(paint.kind) != kind:
			continue
		var button := Button.new()
		button.name = "paint_" + String(paint.id)
		button.toggle_mode = true
		button.button_pressed = String(paint.id) == paint_id
		button.text = "%s · %d/cél. · %s" % [String(paint.name), int(paint.price), beauty_text(float(paint.beauty))]
		button.add_theme_color_override("font_color", Color(String(paint.color)).lightened(0.35))
		var id := String(paint.id)
		button.pressed.connect(func():
			paint_id = id
			verdict = {}
			verdict_for = ""
			_rebuild())
		flow.add_child(button)
	return flow


func _rebuild_rooms() -> void:
	for child in _rooms_box.get_children():
		child.queue_free()
	_rooms_box.add_child(_label("Cômodos", 16))
	var any := false
	for room in home.get("rooms", []):
		if int(room.floor) != floor_level:
			continue
		any = true
		var beauty := float(room.beauty)
		var color := Color("9fd6b0") if beauty >= 6.0 else Color("e8a090") if beauty < -1.0 else Color(0.85, 0.88, 0.94)
		var line := _label("%s · %s" % [_text(room.get("kind")).capitalize(), beauty_text(beauty)], 14, color)
		_rooms_box.add_child(line)
	_rooms_panel.visible = any


func _show_verdict() -> void:
	if _reason == null:
		return
	if verdict.is_empty():
		_reason.text = ""
		return
	if bool(verdict.get("ok", false)):
		var price := int(verdict.get("price", 0))
		var refund := int(verdict.get("refund", 0))
		_reason.text = "%s%s" % [String(verdict.get("note", "")), " · custa %d" % price if price > 0 else " · devolve %d" % refund if refund > 0 else ""]
		_reason.add_theme_color_override("font_color", Color("9fe0b0"))
	else:
		_reason.text = String(verdict.get("reason", "Não pode aqui."))
		_reason.add_theme_color_override("font_color", Color("f0948a"))


# --- o mouse na casa ------------------------------------------------------------------------------------------------------------------------------

## O ponto do chão do andar sob o mouse (espaço do mundo), ou INF fora da vista.
func floor_point(mouse: Vector2) -> Vector3:
	if rig == null or rig.camera == null:
		return Vector3.INF
	var camera: Camera3D = rig.camera
	var from := camera.project_ray_origin(mouse)
	var direction := camera.project_ray_normal(mouse)
	if absf(direction.y) < 0.0001:
		return Vector3.INF
	var t := (float(floor_level) * Grid.floor_height() - from.y) / direction.y
	return from + direction * t if t > 0.0 else Vector3.INF


## A aresta entre a célula `cell` e a vizinha mais perto de `fraction` (0..1 dentro da célula), na forma do motor: [ax, ay, bx, by], a antes de b por linha e coluna.
static func edge_near(cell: Vector2i, fraction: Vector2) -> Array:
	var left := fraction.x
	var right := 1.0 - fraction.x
	var up := fraction.y
	var down := 1.0 - fraction.y
	var best := minf(minf(left, right), minf(up, down))
	var other := cell
	if is_equal_approx(best, left):
		other = cell + Vector2i(-1, 0)
	elif is_equal_approx(best, right):
		other = cell + Vector2i(1, 0)
	elif is_equal_approx(best, up):
		other = cell + Vector2i(0, -1)
	else:
		other = cell + Vector2i(0, 1)
	var first := cell
	var second := other
	if other.y < cell.y or (other.y == cell.y and other.x < cell.x):
		first = other
		second = cell
	return [first.x, first.y, second.x, second.y]


func point_hovered(point: Vector3) -> void:
	if point == Vector3.INF or stage == null:
		hover = NO_CELL
		hover_edge = []
		return
	var origin: Vector2i = stage.interior_view.origin
	var local := Vector2(point.x / Grid.CELL - origin.x, point.z / Grid.CELL - origin.y)
	var cell := Vector2i(floori(local.x), floori(local.y))
	hover = cell
	hover_edge = edge_near(cell, Vector2(local.x - cell.x, local.y - cell.y))


## O verbo que o clique mandaria agora (vazio: nada a fazer onde o mouse está).
func current_verb() -> String:
	if hover == NO_CELL:
		return ""
	if tab == "build":
		match tool:
			"wall", "open", "door", "shut":
				return "" if hover_edge.is_empty() else "%s:%d:%d:%d:%d:%d" % [tool, floor_level, hover_edge[0], hover_edge[1], hover_edge[2], hover_edge[3]]
			"extend":
				return "extend:%d:%d" % [hover.x, hover.y]
			"paint_floor", "paint_wall":
				return "" if paint_id == "" else "paint:%s:%d:%d:%d:%s" % ["floor" if tool == "paint_floor" else "wall", floor_level, hover.x, hover.y, paint_id]
		return ""
	if moving >= 0:
		return "move:%d:%d:%d:%d:%d" % [moving, floor_level, hover.x, hover.y, facing]
	if placing >= 0:
		return "place:%d:%d:%d:%d:%d" % [placing, floor_level, hover.x, hover.y, facing]
	return ""


## Pergunta ao motor se o verbo vale e guarda a resposta (cor do fantasma e motivo).
func ask(verb: String) -> void:
	if verb == "":
		verdict = {}
		verdict_for = ""
		_show_verdict()
		return
	_pending = true
	var response: Dictionary = await api.player_home_check(player_id, verb)
	_pending = false
	if response.ok and response.body is Dictionary:
		verdict = response.body
		verdict_for = verb
	else:
		verdict = {"ok": false, "reason": "Sem resposta do motor."}
		verdict_for = verb
	_show_verdict()
	_update_ghost()


## Manda o comando de casa; o motor valida e aplica (409 com o motivo se não vale).
func command(verb: String) -> bool:
	var response: Dictionary = await api.player_interact(player_id, "home", int(home.get("building", 0)), verb)
	if not response.ok:
		var text := ""
		if response.body is Dictionary:
			text = String(response.body.get("error", ""))
		warning.emit(text.trim_prefix("conflict:").strip_edges() if text != "" else "O motor recusou (%d)." % int(response.status))
		return false
	var note := ""
	if response.body is Dictionary:
		note = String(response.body.get("note", ""))
	last_note = note
	acted.emit(note)
	verdict = {}
	verdict_for = ""
	await reload()
	return true


## O clique na casa: o verbo do mouse (se o motor disse que pode); na aba da casa, sem nada escolhido, pega a peça da célula.
func click() -> void:
	if tab == "mine" and selected == "" and moving < 0 and placing < 0:
		var data := piece_at(hover)
		if not data.is_empty():
			start_move(int(data.piece))
			_rebuild()
		return
	var verb := current_verb()
	if verb == "":
		return
	if verdict_for != verb:
		await ask(verb)
	if not bool(verdict.get("ok", false)):
		warning.emit(String(verdict.get("reason", "Não pode aqui.")))
		return
	var ok: bool = await command(verb)
	if ok and (moving >= 0 or placing >= 0):
		moving = -1
		placing = -1
		selected = ""
		_update_ghost()
		_rebuild()


func cancel() -> void:
	if selected != "" or moving >= 0 or placing >= 0 or tool != "":
		_clear_selection()
		_rebuild()
	else:
		close()


func over_panel(position: Vector2) -> bool:
	for panel in [_top, _side, _rooms_panel, _foot]:
		if panel.visible and panel.get_global_rect().has_point(position):
			return true
	return false


func _input(event: InputEvent) -> void:
	if not is_open:
		return
	if event is InputEventKey and event.pressed and not event.echo:
		match event.keycode:
			KEY_Q:
				rotate_by(-1)
			KEY_E, KEY_R:
				rotate_by(1)
			KEY_B:
				close()
			KEY_ESCAPE:
				cancel()
			_:
				return
		get_viewport().set_input_as_handled()
	elif event is InputEventMouseButton and event.pressed and not over_panel(event.position):
		match event.button_index:
			MOUSE_BUTTON_LEFT:
				click()
			MOUSE_BUTTON_RIGHT:
				cancel()
			MOUSE_BUTTON_WHEEL_UP:
				_zoom(0.9)
			MOUSE_BUTTON_WHEEL_DOWN:
				_zoom(1.1)
			_:
				return
		get_viewport().set_input_as_handled()


func _zoom(factor: float) -> void:
	_view_size = clampf(_view_size * factor, 5.0, 40.0)
	if rig != null:
		rig.build_size = _view_size
		rig._apply()


func _process(_delta: float) -> void:
	if not is_open or not is_inside_tree():
		return
	var mouse := get_viewport().get_mouse_position()
	if mouse_follow and not over_panel(mouse):
		point_hovered(floor_point(mouse))
	var verb := current_verb()
	if verb != verdict_for and not _pending:
		if verb == "":
			verdict = {}
			verdict_for = ""
			_show_verdict()
		else:
			ask(verb)
	_update_ghost()
	_update_marks()


# --- fantasma, grade e marcas na casa --------------------------------------------------------------------------------------------------------------

func _tint(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.no_depth_test = false
	return material


func _verdict_color() -> Color:
	if verdict.is_empty() or verdict_for != current_verb():
		return NEUTRAL
	return GOOD if bool(verdict.get("ok", false)) else BAD


func ghost_is_green() -> bool:
	return _verdict_color() == GOOD


func ghost_is_red() -> bool:
	return _verdict_color() == BAD


func _update_ghost() -> void:
	var wanted := selected if tab != "build" and (placing >= 0 or moving >= 0) else ""
	if wanted == "" or stage == null or factory == null or not descriptors.has(wanted):
		if ghost != null and is_instance_valid(ghost):
			ghost.visible = false
		return
	if ghost == null or not is_instance_valid(ghost) or _ghost_id != wanted:
		if ghost != null and is_instance_valid(ghost):
			ghost.queue_free()
		ghost = factory.build(wanted, 0)
		_ghost_id = wanted
		if ghost == null:
			return
		stage.add_child(ghost)
	var color := _verdict_color()
	for mesh in _meshes_of(ghost):
		mesh.material_override = _tint(color)
		mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	ghost.visible = hover != NO_CELL
	if hover != NO_CELL:
		var origin: Vector2i = stage.interior_view.origin
		ghost.position = Grid.to_world(origin + hover, float(floor_level) * Grid.floor_height())
		ghost.rotation.y = deg_to_rad(float(facing))


static func _meshes_of(node: Node) -> Array:
	var out: Array = []
	if node is MeshInstance3D:
		out.append(node)
	for child in node.get_children():
		out.append_array(_meshes_of(child))
	return out


func _rebuild_overlay() -> void:
	if stage == null or not is_open:
		return
	if overlay != null and is_instance_valid(overlay):
		overlay.queue_free()
	overlay = Node3D.new()
	overlay.name = "BuyOverlay"
	stage.add_child(overlay)
	var info: Dictionary = info_of.call() if info_of.is_valid() else {}
	var origin: Vector2i = stage.interior_view.origin
	var height := float(floor_level) * Grid.floor_height() + 0.03
	var mesh := ImmediateMesh.new()
	mesh.surface_begin(Mesh.PRIMITIVE_LINES)
	for floor_layout in info.get("layout", []):
		if int(floor_layout.level) != floor_level:
			continue
		for entry in floor_layout.cells:
			var x := float(origin.x + int(entry.cell.x))
			var z := float(origin.y + int(entry.cell.y))
			for pair in [[Vector3(x, height, z), Vector3(x + 1, height, z)], [Vector3(x + 1, height, z), Vector3(x + 1, height, z + 1)],
					[Vector3(x + 1, height, z + 1), Vector3(x, height, z + 1)], [Vector3(x, height, z + 1), Vector3(x, height, z)]]:
				mesh.surface_add_vertex(pair[0])
				mesh.surface_add_vertex(pair[1])
	mesh.surface_end()
	_grid_mesh = MeshInstance3D.new()
	_grid_mesh.mesh = mesh
	var line := StandardMaterial3D.new()
	line.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	line.albedo_color = Color(1, 1, 1, 0.22)
	line.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_grid_mesh.material_override = line
	_grid_mesh.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	overlay.add_child(_grid_mesh)
	_mark = MeshInstance3D.new()
	_mark.mesh = BoxMesh.new()
	_mark.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	overlay.add_child(_mark)


## O quadrado ou a barra que mostra o alvo da ferramenta de obra (cor do veredito do motor).
func _update_marks() -> void:
	if _mark == null or not is_instance_valid(_mark) or stage == null:
		return
	var origin: Vector2i = stage.interior_view.origin
	var base := float(floor_level) * Grid.floor_height()
	_mark.visible = false
	if tab == "build" and hover != NO_CELL:
		_mark.material_override = _tint(_verdict_color())
		var box: BoxMesh = _mark.mesh
		if tool in ["wall", "open", "door", "shut"] and not hover_edge.is_empty():
			var a := Vector2(hover_edge[0], hover_edge[1])
			var b := Vector2(hover_edge[2], hover_edge[3])
			var middle := (a + b) / 2.0 + Vector2(0.5, 0.5) + Vector2(origin)
			var horizontal: bool = hover_edge[0] == hover_edge[2]
			box.size = Vector3(1.0 if horizontal else 0.14, 0.6, 0.14 if horizontal else 1.0)
			_mark.position = Vector3(middle.x * Grid.CELL, base + 0.3, middle.y * Grid.CELL)
			_mark.visible = true
		elif tool == "extend" or tool.begins_with("paint"):
			box.size = Vector3(1.0, 0.05, 1.0)
			_mark.position = Grid.to_world(origin + hover, base + 0.04)
			_mark.visible = true
	elif tab != "build" and (placing >= 0 or moving >= 0) and hover != NO_CELL:
		_mark.material_override = _tint(Color(_verdict_color(), 0.28))
		var cell_box: BoxMesh = _mark.mesh
		cell_box.size = Vector3(1.0, 0.04, 1.0)
		_mark.position = Grid.to_world(origin + hover, base + 0.03)
		_mark.visible = true
