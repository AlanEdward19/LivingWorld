extends Control
## Menu principal: título com estado do servidor, Continuar o mundo em execução e Novo mundo (escolha do
## período, nome, seed, prévia do mapa real e criação). Só fala com a API pelo ApiClient; o mundo em si
## nunca é decidido aqui.

signal world_ready() ## há um mundo no servidor para jogar (continuado ou recém-criado)

const UiTheme := preload("res://Scenes/ui_theme.gd")
const WorldEditor := preload("res://Scenes/world_editor.gd")
const WorldTouchup := preload("res://Scenes/world_touchup.gd")
const FamilyBuilder := preload("res://Scenes/family_builder.gd")

const TERRAIN_COLORS: Array[Color] = [
	Color("5f8f4e"), Color("c9b26b"), Color("7b8b6f"), Color("8a7a66"), Color("4f7a64"), Color("a8a39a"),
]
const WATER_COLOR := Color("3b6ea5")
const SETTLEMENT_COLOR := Color("ffd84a")
const MAX_SEED := 999999999
const NAME_START: Array[String] = ["Al", "Bel", "Cor", "Dra", "El", "Fen", "Gar", "Hel", "Is", "Kor", "Lum", "Mor", "Nor", "Or", "Tar", "Val"]
const NAME_END: Array[String] = ["dor", "ia", "heim", "mar", "ria", "thal", "vik", "wen", "land", "gard", "mere", "ton"]

var api
var period_ids: Array[String] = []
var selected_period := ""
var world_name := ""
var seed_value := 1
var draft := "" ## cenário do período em texto cru, com as personalizações aplicadas (ver WorldEditor)
var selected_settlement := 0 ## linha cujo ponto o clique na prévia move
var touchup
var family_builder
var professions: Dictionary = {} ## id -> nome, do catálogo do período escolhido
var family_label: Label
var building_rows: VBoxContainer
var custom_box: VBoxContainer
var settlement_rows: VBoxContainer
var width_spin: SpinBox
var height_spin: SpinBox
var population_spin: SpinBox
var conception_spin: SpinBox
var longevity_spin: SpinBox
var server_online := false

var title_screen: Control
var new_world_screen: Control
var status_label: Label
var continue_button: Button
var period_list: VBoxContainer
var period_info: Label
var name_edit: LineEdit
var seed_edit: LineEdit
var preview_rect: TextureRect
var preview_note: Label
var create_button: Button
var message_label: Label

var _rng := RandomNumberGenerator.new()
var _preview_generation := 0
var _busy := false


func _ready() -> void:
	_rng.randomize()
	theme = UiTheme.build()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_build_background()
	_build_title_screen()
	_build_new_world_screen()
	show_title()


## Chamado depois de `api` ser definido: consulta o servidor e habilita o que ele permite.
func refresh_server() -> void:
	var status: Dictionary = await api.clock_status()
	server_online = status.ok
	continue_button.disabled = not server_online
	if server_online:
		status_label.text = "Servidor conectado · ano %d · %s" % [int(status.body.year), "pausado" if status.body.isPaused else "rodando"]
		status_label.modulate = Color("8fd19e")
	else:
		status_label.text = "Servidor offline. Rode: bash scripts/play.sh"
		status_label.modulate = Color("e08a8a")


func show_title() -> void:
	title_screen.visible = true
	new_world_screen.visible = false


func show_new_world() -> void:
	title_screen.visible = false
	new_world_screen.visible = true
	message_label.text = ""
	if world_name == "":
		_roll_name()
		_roll_seed()
	if period_ids.is_empty():
		await _load_periods()


func _build_background() -> void:
	var gradient := Gradient.new()
	gradient.set_color(0, Color("0b1020"))
	gradient.set_color(1, Color("27304a"))
	var texture := GradientTexture2D.new()
	texture.gradient = gradient
	texture.fill_from = Vector2(0.5, 0.0)
	texture.fill_to = Vector2(0.5, 1.0)
	var rect := TextureRect.new()
	rect.texture = texture
	rect.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	rect.stretch_mode = TextureRect.STRETCH_SCALE
	rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(rect)


func _column(parent: Control, width: float) -> VBoxContainer:
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	parent.add_child(center)
	var box := VBoxContainer.new()
	box.custom_minimum_size.x = width
	box.add_theme_constant_override("separation", 12)
	center.add_child(box)
	return box


func _label(text: String, size: int, parent: Control) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	parent.add_child(label)
	return label


func _button(text: String, parent: Control, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.custom_minimum_size = Vector2(0, 46)
	button.add_theme_font_size_override("font_size", 19)
	button.pressed.connect(action)
	parent.add_child(button)
	return button


func _build_title_screen() -> void:
	title_screen = Control.new()
	title_screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(title_screen)
	var box := _column(title_screen, 360)
	_label("LIVING WORLD", 54, box)
	_label("Um mundo que continua vivo sem você", 17, box).modulate = Color(1, 1, 1, 0.7)
	var gap := Control.new()
	gap.custom_minimum_size.y = 24
	box.add_child(gap)
	continue_button = _button("Continuar mundo atual", box, _on_continue)
	continue_button.disabled = true
	_button("Novo mundo", box, show_new_world)
	_button("Sair", box, func(): get_tree().quit())
	status_label = _label("Procurando o servidor…", 15, box)


func _build_new_world_screen() -> void:
	new_world_screen = Control.new()
	new_world_screen.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(new_world_screen)
	var box := _column(new_world_screen, 920)
	_label("Novo mundo", 38, box)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 20)
	box.add_child(row)

	var left := VBoxContainer.new()
	left.custom_minimum_size.x = 380
	left.add_theme_constant_override("separation", 8)
	row.add_child(left)
	_label("Escolha o período", 17, left)
	period_list = VBoxContainer.new()
	period_list.add_theme_constant_override("separation", 6)
	left.add_child(period_list)
	period_info = Label.new()
	period_info.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	period_info.custom_minimum_size.y = 40
	left.add_child(period_info)
	_build_custom_panel(left)

	var right := VBoxContainer.new()
	right.custom_minimum_size.x = 500
	right.add_theme_constant_override("separation", 8)
	row.add_child(right)
	_label("Nome do mundo", 15, right)
	var name_row := HBoxContainer.new()
	right.add_child(name_row)
	name_edit = LineEdit.new()
	name_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_edit.text_changed.connect(func(text: String): world_name = text; _refresh_create())
	name_row.add_child(name_edit)
	_button("🎲", name_row, _roll_name)
	_label("Seed (o mesmo número gera o mesmo mundo)", 15, right)
	var seed_row := HBoxContainer.new()
	right.add_child(seed_row)
	seed_edit = LineEdit.new()
	seed_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	seed_edit.text_submitted.connect(func(_t: String): _commit_seed())
	seed_edit.focus_exited.connect(_commit_seed)
	seed_row.add_child(seed_edit)
	_button("🎲", seed_row, func(): _roll_seed(); _edit(WorldEditor.with_seed(draft, seed_value)))
	preview_rect = TextureRect.new()
	preview_rect.custom_minimum_size = Vector2(500, 260)
	preview_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	preview_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	preview_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	preview_rect.mouse_filter = Control.MOUSE_FILTER_STOP
	preview_rect.gui_input.connect(_on_preview_input)
	right.add_child(preview_rect)
	preview_note = Label.new()
	preview_note.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	right.add_child(preview_note)

	message_label = _label("", 15, box)
	message_label.modulate = Color("e08a8a")
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 12)
	box.add_child(actions)
	var back := _button("Voltar", actions, show_title)
	back.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	create_button = _button("Criar mundo", actions, _on_create)
	create_button.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	create_button.disabled = true


func _spin(label_text: String, parent: Control, low: float, high: float, step: float, on_change: Callable) -> SpinBox:
	var row := HBoxContainer.new()
	parent.add_child(row)
	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size.x = 150
	row.add_child(label)
	var spin := SpinBox.new()
	spin.min_value = low
	spin.max_value = high
	spin.step = step
	spin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	spin.value_changed.connect(on_change)
	row.add_child(spin)
	return spin


## Personalização do cenário: mapa, população, natalidade, longevidade e assentamentos (clique na prévia move o marcado).
func _build_custom_panel(parent: Control) -> void:
	var toggle := CheckButton.new()
	toggle.text = "Personalizar o mundo"
	parent.add_child(toggle)
	custom_box = VBoxContainer.new()
	custom_box.visible = false
	custom_box.add_theme_constant_override("separation", 4)
	parent.add_child(custom_box)
	toggle.toggled.connect(func(on: bool): custom_box.visible = on)
	width_spin = _spin("Largura do mapa", custom_box, WorldEditor.MIN_SIDE, WorldEditor.MAX_SIDE, 1, func(_v): _apply_size())
	height_spin = _spin("Altura do mapa", custom_box, WorldEditor.MIN_SIDE, WorldEditor.MAX_SIDE, 1, func(_v): _apply_size())
	population_spin = _spin("População inicial", custom_box, 1, WorldEditor.MAX_POPULATION, 1, func(v): _edit(WorldEditor.with_population(draft, int(v))))
	conception_spin = _spin("Natalidade anual", custom_box, 0.0, 1.0, 0.05, func(v): _edit(WorldEditor.with_conception(draft, v)))
	longevity_spin = _spin("Longevidade máxima", custom_box, 30, 150, 1, func(v): _edit(WorldEditor.with_longevity(draft, int(v))))
	_label("Assentamentos (◎ marca o que o clique no mapa move)", 14, custom_box)
	settlement_rows = VBoxContainer.new()
	custom_box.add_child(settlement_rows)
	_button("+ Assentamento", custom_box, _add_settlement)
	_label("Prédios (locais de trabalho)", 14, custom_box)
	building_rows = VBoxContainer.new()
	custom_box.add_child(building_rows)
	_button("+ Prédio", custom_box, _add_building)
	family_label = _label("", 14, custom_box)
	_button("Criar famílias e pessoas…", custom_box, show_family_builder)


func _edit(new_draft: String) -> void:
	draft = new_draft
	_refresh_preview()


func _apply_size() -> void:
	draft = WorldEditor.with_size(draft, int(width_spin.value), int(height_spin.value))
	_sync_custom()
	_refresh_preview()


## Preenche os controles a partir do rascunho sem disparar os sinais de edição.
func _sync_custom() -> void:
	var values: Dictionary = WorldEditor.read(draft)
	width_spin.set_value_no_signal(values.width)
	height_spin.set_value_no_signal(values.height)
	population_spin.set_value_no_signal(values.population)
	conception_spin.set_value_no_signal(values.conception)
	longevity_spin.set_value_no_signal(values.longevity)
	for child in settlement_rows.get_children():
		child.queue_free()
	selected_settlement = clampi(selected_settlement, 0, maxi(values.settlements.size() - 1, 0))
	for i in values.settlements.size():
		_settlement_row(i, values.settlements[i], values)
	_sync_buildings(values)
	var family_count: int = WorldEditor.read_households(draft).size()
	family_label.text = "%d família(s) autorada(s); a população inicial sorteada continua (zere-a para só as suas)." % family_count if family_count > 0 else ""


func _settlement_row(index: int, entry: Dictionary, values: Dictionary) -> void:
	var row := HBoxContainer.new()
	settlement_rows.add_child(row)
	var name_edit_row := LineEdit.new()
	name_edit_row.text = entry.name
	name_edit_row.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_edit_row.text_submitted.connect(func(text: String): _change_settlement(index, {"name": text}))
	row.add_child(name_edit_row)
	for axis in ["x", "y"]:
		var spin := SpinBox.new()
		spin.min_value = 0
		spin.max_value = (values.width if axis == "x" else values.height) - 1
		spin.set_value_no_signal(entry[axis])
		spin.value_changed.connect(func(v: float): _change_settlement(index, {axis: int(v)}))
		row.add_child(spin)
	var mark := Button.new()
	mark.text = "◎"
	mark.toggle_mode = true
	mark.button_pressed = index == selected_settlement
	mark.pressed.connect(func(): selected_settlement = index; _sync_custom())
	row.add_child(mark)
	var remove := Button.new()
	remove.text = "✕"
	remove.disabled = values.settlements.size() <= 1
	remove.pressed.connect(func(): _remove_settlement(index))
	row.add_child(remove)


func _sync_buildings(values: Dictionary) -> void:
	for child in building_rows.get_children():
		child.queue_free()
	var types: Array[int] = WorldEditor.building_types(draft)
	var list: Array = WorldEditor.read_workplaces(draft)
	for i in list.size():
		var row := HBoxContainer.new()
		building_rows.add_child(row)
		var type_pick := OptionButton.new()
		for type_id in types:
			type_pick.add_item("tipo %d" % type_id, type_id)
		type_pick.select(maxi(type_pick.get_item_index(int(list[i].get("LocationTypeId", 0))), 0))
		type_pick.item_selected.connect(func(index: int): _change_building(i, {"LocationTypeId": type_pick.get_item_id(index)}))
		row.add_child(type_pick)
		for axis in ["X", "Y"]:
			var spin := SpinBox.new()
			spin.min_value = 0
			spin.max_value = (values.width if axis == "X" else values.height) - 1
			spin.set_value_no_signal(int(list[i].get(axis, 0)))
			spin.value_changed.connect(func(v: float): _change_building(i, {axis: int(v)}))
			row.add_child(spin)
		var vacancies := SpinBox.new()
		vacancies.min_value = 1
		vacancies.max_value = 100
		vacancies.prefix = "vagas "
		vacancies.set_value_no_signal(int(list[i].get("MaxVacancies", 1)))
		vacancies.value_changed.connect(func(v: float): _change_building(i, {"MaxVacancies": int(v)}))
		row.add_child(vacancies)
		var remove := Button.new()
		remove.text = "✕"
		remove.pressed.connect(func(): _remove_building(i))
		row.add_child(remove)


func _change_building(index: int, changes: Dictionary) -> void:
	var list: Array = WorldEditor.read_workplaces(draft)
	if index < list.size():
		list[index].merge(changes, true)
		_edit(WorldEditor.with_workplaces(draft, list))


func _add_building() -> void:
	var list: Array = WorldEditor.read_workplaces(draft)
	var values: Dictionary = WorldEditor.read(draft)
	var types: Array[int] = WorldEditor.building_types(draft)
	if types.is_empty():
		message_label.text = "Este período não define tipos de prédio."
		return
	var clone: Dictionary = (list[0] as Dictionary).duplicate(true) if not list.is_empty() else {"Treasury": 100000, "Stock": {}, "Prices": {}}
	clone.merge({"LocationTypeId": types[0], "X": values.width / 2, "Y": values.height / 2, "MaxVacancies": 6}, true)
	list.append(clone)
	_edit(WorldEditor.with_workplaces(draft, list))
	_sync_custom()


func _remove_building(index: int) -> void:
	var list: Array = WorldEditor.read_workplaces(draft)
	if index < list.size():
		list.remove_at(index)
		_edit(WorldEditor.with_workplaces(draft, list))
		_sync_custom()


func show_family_builder() -> void:
	new_world_screen.visible = false
	family_builder = FamilyBuilder.new()
	var settlement_names: Array[String] = []
	for settlement in WorldEditor.read(draft).settlements:
		settlement_names.append(settlement.name)
	add_child(family_builder)
	family_builder.setup(WorldEditor.read_households(draft), professions, settlement_names)
	family_builder.done.connect(func(families: Array):
		draft = WorldEditor.with_households(draft, families)
		_close_family_builder())
	family_builder.cancelled.connect(_close_family_builder)


func _close_family_builder() -> void:
	family_builder.queue_free()
	family_builder = null
	new_world_screen.visible = true
	_sync_custom()
	_refresh_preview()


func _change_settlement(index: int, changes: Dictionary) -> void:
	var list: Array = WorldEditor.read(draft).settlements
	if index >= list.size():
		return
	list[index].merge(changes, true)
	_edit(WorldEditor.with_settlements(draft, list))
	_sync_custom()


func _add_settlement() -> void:
	var values: Dictionary = WorldEditor.read(draft)
	var list: Array = values.settlements
	list.append({"name": "assentamento %d" % (list.size() + 1), "x": values.width / 2, "y": values.height / 2})
	selected_settlement = list.size() - 1
	_edit(WorldEditor.with_settlements(draft, list))
	_sync_custom()


func _remove_settlement(index: int) -> void:
	var list: Array = WorldEditor.read(draft).settlements
	if list.size() <= 1 or index >= list.size():
		return
	list.remove_at(index)
	_edit(WorldEditor.with_settlements(draft, list))
	_sync_custom()


## Clique na prévia: move o assentamento marcado para a célula clicada.
func _on_preview_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var cell := preview_cell_at(event.position)
		if cell.x >= 0:
			_change_settlement(selected_settlement, {"x": cell.x, "y": cell.y})


## Célula do mapa sob um ponto do TextureRect da prévia (imagem centralizada, sem distorção); (-1,-1) fora do mapa.
func preview_cell_at(point: Vector2) -> Vector2i:
	var values: Dictionary = WorldEditor.read(draft)
	var scale := minf(preview_rect.size.x / values.width, preview_rect.size.y / values.height)
	var offset := (preview_rect.size - Vector2(values.width, values.height) * scale) / 2.0
	var local := (point - offset) / scale
	if local.x < 0 or local.y < 0 or local.x >= values.width or local.y >= values.height:
		return Vector2i(-1, -1)
	return Vector2i(int(local.x), int(local.y))


func _roll_name() -> void:
	world_name = NAME_START[_rng.randi() % NAME_START.size()] + NAME_END[_rng.randi() % NAME_END.size()]
	if name_edit != null:
		name_edit.text = world_name
	_refresh_create()


func _roll_seed() -> void:
	seed_value = _rng.randi_range(1, MAX_SEED)
	if seed_edit != null:
		seed_edit.text = str(seed_value)


func _commit_seed() -> void:
	var parsed := clampi(int(seed_edit.text), 1, MAX_SEED)
	seed_edit.text = str(parsed)
	if parsed != seed_value:
		seed_value = parsed
		_edit(WorldEditor.with_seed(draft, seed_value))


func _refresh_create() -> void:
	if create_button != null:
		create_button.disabled = _busy or world_name.strip_edges() == "" or selected_period == ""


func _load_periods() -> void:
	var response: Dictionary = await api.periods()
	for child in period_list.get_children():
		child.queue_free()
	period_ids.clear()
	if not response.ok:
		message_label.text = "Não foi possível listar os períodos (servidor offline?)."
		return
	for item in response.body:
		var id := String(item.periodId)
		period_ids.append(id)
		var button := Button.new()
		button.text = "%s   %d×%d" % [String(item.get("source", id.capitalize())), int(item.width), int(item.height)]
		button.toggle_mode = true
		button.custom_minimum_size.y = 40
		button.pressed.connect(func(): _select_period(id))
		period_list.add_child(button)
	if not period_ids.is_empty():
		await _select_period(period_ids[0])


func _select_period(id: String) -> void:
	selected_period = id
	for i in period_list.get_child_count():
		(period_list.get_child(i) as Button).button_pressed = period_ids[i] == id
	_refresh_create()
	period_info.text = "Carregando…"
	var scenario: String = await api.period_scenario(id, seed_value)
	if selected_period != id:
		return
	draft = scenario
	if draft != "":
		_sync_custom()
	var catalog: Dictionary = await api.period_catalog(id)
	if selected_period != id:
		return # escolheu outro período enquanto este carregava
	if catalog.ok:
		professions.clear()
		for profession_id in catalog.body.professionIds:
			professions[int(profession_id)] = String(catalog.body.professionNames.get(str(int(profession_id)), "Profissão %d" % int(profession_id)))
		var names: Array = professions.values()
		period_info.text = "Profissões: %s" % ", ".join(names)
	else:
		period_info.text = ""
	await _refresh_preview()


func _refresh_preview() -> void:
	if selected_period == "":
		return
	_preview_generation += 1
	var generation := _preview_generation
	preview_note.text = "Gerando prévia…"
	var response: Dictionary = await api.world_preview(draft) if draft != "" else {"ok": false}
	if generation != _preview_generation:
		return # uma prévia mais nova já está a caminho
	if not response.ok:
		preview_rect.texture = null
		preview_note.text = "Prévia indisponível: %s" % str(response.get("body", "")).left(80)
		return
	preview_rect.texture = ImageTexture.create_from_image(preview_image(response.body))
	preview_note.text = "%d×%d células · %d assentamentos" % [int(response.body.width), int(response.body.height), response.body.settlements.size()]


## Mapa real devolvido pelo servidor para esta seed: um pixel por célula, água azul, assentamentos em amarelo.
static func preview_image(body: Dictionary) -> Image:
	var width := int(body.width)
	var height := int(body.height)
	var image := Image.create(width, height, false, Image.FORMAT_RGB8)
	for cell in body.cells:
		var color: Color = WATER_COLOR if cell.hasWater else TERRAIN_COLORS[int(cell.terrain) % TERRAIN_COLORS.size()]
		image.set_pixel(int(cell.x), int(cell.y), color.lightened(clampf(float(cell.altitude) / 400.0, 0.0, 0.35)))
	for settlement in body.settlements:
		image.set_pixel(int(settlement.x), int(settlement.y), SETTLEMENT_COLOR)
	return image


## Retoques depois de criado: personalidade e poderes de NPCs do mundo recém-criado (rotas de autoria).
func show_touchup() -> void:
	new_world_screen.visible = false
	touchup = WorldTouchup.new()
	touchup.api = api
	touchup.finished.connect(func(): touchup.queue_free(); world_ready.emit())
	add_child(touchup)
	await touchup.load_npcs()


func _on_continue() -> void:
	selected_period = "" # o mundo continuado não nasceu do período que se olhou na tela de novo mundo
	world_ready.emit()


func _on_create() -> void:
	if _busy:
		return
	_busy = true
	_refresh_create()
	message_label.text = ""
	var response: Dictionary = await api.world_create(draft, world_name.strip_edges()) if draft != "" else {"ok": false, "body": "período indisponível"}
	_busy = false
	_refresh_create()
	if response.ok:
		await show_touchup()
	else:
		message_label.text = "Não foi possível criar o mundo: %s" % str(response.body)
