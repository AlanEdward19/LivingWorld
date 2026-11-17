extends VBoxContainer
## Passo OPCIONAL "Personalizar" do Novo mundo (fora do caminho principal): mapa, natalidade, longevidade,
## assentamentos (clique no mapa move o marcado), prédios e famílias. Edita o rascunho do assistente (`wizard.draft`)
## pelo WorldEditor; nada aqui decide o mundo, só o cenário enviado na criação.

signal family_requested()

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const WorldEditor := preload("res://Scenes/world_editor.gd")

var wizard ## NewWorldWizard: dono do rascunho (draft, edit, refresh_preview)
var selected_settlement := 0 ## linha cujo ponto o clique na prévia move
var width_spin: SpinBox
var height_spin: SpinBox
var population_spin: SpinBox
var conception_spin: SpinBox
var longevity_spin: SpinBox
var settlement_rows: VBoxContainer
var building_rows: VBoxContainer
var family_label: Label
var preview_rect: TextureRect
var touchup_check: CheckButton


func _ready() -> void:
	add_theme_constant_override("separation", 6)
	Kit.text("Tudo aqui é opcional. Se não mexer em nada, o mundo nasce como você já escolheu.", self, 15)
	var columns := Kit.row(self, 18)
	var left := VBoxContainer.new()
	left.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	columns.add_child(left)
	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	columns.add_child(right)
	width_spin = _spin("Largura do mapa", left, WorldEditor.MIN_SIDE, WorldEditor.MAX_SIDE, 1, func(_v): _apply_size())
	height_spin = _spin("Altura do mapa", left, WorldEditor.MIN_SIDE, WorldEditor.MAX_SIDE, 1, func(_v): _apply_size())
	population_spin = _spin("Pessoas no começo", left, 1, WorldEditor.MAX_POPULATION, 1, func(v): wizard.edit(WorldEditor.with_population(wizard.draft, int(v))))
	conception_spin = _spin("Chance de bebê por ano", left, 0.0, 1.0, 0.05, func(v): wizard.edit(WorldEditor.with_conception(wizard.draft, v)))
	longevity_spin = _spin("Idade máxima", left, 30, 150, 1, func(v): wizard.edit(WorldEditor.with_longevity(wizard.draft, int(v))))
	Kit.text("Vilas e cidades (◎ marca a que o clique no mapa move)", left, 14)
	settlement_rows = VBoxContainer.new()
	left.add_child(settlement_rows)
	Kit.button("+ Vila", left, _add_settlement)
	Kit.text("Locais de trabalho", left, 14)
	building_rows = VBoxContainer.new()
	left.add_child(building_rows)
	Kit.button("+ Local de trabalho", left, _add_building)
	family_label = Kit.text("", left, 14)
	Kit.button("Criar famílias e pessoas…", left, func(): family_requested.emit())
	preview_rect = TextureRect.new()
	preview_rect.custom_minimum_size = Vector2(320, 240)
	preview_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	preview_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	preview_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	preview_rect.mouse_filter = Control.MOUSE_FILTER_STOP
	preview_rect.gui_input.connect(_on_preview_input)
	right.add_child(preview_rect)
	Kit.text("Clique no mapa para mudar de lugar a vila marcada.", right, 14)
	touchup_check = CheckButton.new()
	touchup_check.text = "Depois de criar, ajustar o jeito e os dons de algumas pessoas"
	touchup_check.focus_mode = Control.FOCUS_ALL
	touchup_check.toggled.connect(func(on: bool): wizard.touchup_after = on)
	right.add_child(touchup_check)


func _spin(label_text: String, parent: Control, low: float, high: float, step: float, on_change: Callable) -> SpinBox:
	var row := HBoxContainer.new()
	parent.add_child(row)
	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size.x = 190
	row.add_child(label)
	var spin := SpinBox.new()
	spin.min_value = low
	spin.max_value = high
	spin.step = step
	spin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	spin.value_changed.connect(on_change)
	row.add_child(spin)
	return spin


func _apply_size() -> void:
	wizard.draft = WorldEditor.with_size(wizard.draft, int(width_spin.value), int(height_spin.value))
	sync()
	wizard.refresh_preview()


## Preenche os controles a partir do rascunho sem disparar os sinais de edição.
func sync() -> void:
	if width_spin == null or wizard.draft == "":
		return
	var values: Dictionary = WorldEditor.read(wizard.draft)
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
	var family_count: int = WorldEditor.read_households(wizard.draft).size()
	family_label.text = "%d família(s) criada(s) por você; as sorteadas continuam (zere as pessoas do começo para ficar só com as suas)." % family_count if family_count > 0 else ""
	touchup_check.set_pressed_no_signal(wizard.touchup_after)


func _settlement_row(index: int, entry: Dictionary, values: Dictionary) -> void:
	var row := HBoxContainer.new()
	settlement_rows.add_child(row)
	var name_edit := LineEdit.new()
	name_edit.text = entry.name
	name_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_edit.text_submitted.connect(func(text: String): change_settlement(index, {"name": text}))
	row.add_child(name_edit)
	for axis in ["x", "y"]:
		var spin := SpinBox.new()
		spin.min_value = 0
		spin.max_value = (values.width if axis == "x" else values.height) - 1
		spin.set_value_no_signal(entry[axis])
		spin.value_changed.connect(func(v: float): change_settlement(index, {axis: int(v)}))
		row.add_child(spin)
	var mark := Button.new()
	mark.text = "◎"
	mark.toggle_mode = true
	mark.button_pressed = index == selected_settlement
	mark.tooltip_text = "O clique no mapa move esta"
	mark.pressed.connect(func(): selected_settlement = index; sync())
	row.add_child(mark)
	var remove := Button.new()
	remove.text = "✕"
	remove.tooltip_text = "Tirar esta vila"
	remove.disabled = values.settlements.size() <= 1
	remove.pressed.connect(func(): _remove_settlement(index))
	row.add_child(remove)


func _sync_buildings(values: Dictionary) -> void:
	for child in building_rows.get_children():
		child.queue_free()
	var types: Array[int] = WorldEditor.building_types(wizard.draft)
	var list: Array = WorldEditor.read_workplaces(wizard.draft)
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
	var list: Array = WorldEditor.read_workplaces(wizard.draft)
	if index < list.size():
		list[index].merge(changes, true)
		wizard.edit(WorldEditor.with_workplaces(wizard.draft, list))


func _add_building() -> void:
	var list: Array = WorldEditor.read_workplaces(wizard.draft)
	var values: Dictionary = WorldEditor.read(wizard.draft)
	var types: Array[int] = WorldEditor.building_types(wizard.draft)
	if types.is_empty():
		wizard.show_error("Este cenário não tem tipos de local de trabalho.")
		return
	var clone: Dictionary = (list[0] as Dictionary).duplicate(true) if not list.is_empty() else {"Treasury": 100000, "Stock": {}, "Prices": {}}
	clone.merge({"LocationTypeId": types[0], "X": values.width / 2, "Y": values.height / 2, "MaxVacancies": 6}, true)
	list.append(clone)
	wizard.edit(WorldEditor.with_workplaces(wizard.draft, list))
	sync()


func _remove_building(index: int) -> void:
	var list: Array = WorldEditor.read_workplaces(wizard.draft)
	if index < list.size():
		list.remove_at(index)
		wizard.edit(WorldEditor.with_workplaces(wizard.draft, list))
		sync()


func change_settlement(index: int, changes: Dictionary) -> void:
	var list: Array = WorldEditor.read(wizard.draft).settlements
	if index >= list.size():
		return
	list[index].merge(changes, true)
	wizard.edit(WorldEditor.with_settlements(wizard.draft, list))
	sync()


func add_settlement() -> void:
	_add_settlement()


func _add_settlement() -> void:
	var values: Dictionary = WorldEditor.read(wizard.draft)
	var list: Array = values.settlements
	list.append({"name": "vila %d" % (list.size() + 1), "x": values.width / 2, "y": values.height / 2})
	selected_settlement = list.size() - 1
	wizard.edit(WorldEditor.with_settlements(wizard.draft, list))
	sync()


func _remove_settlement(index: int) -> void:
	var list: Array = WorldEditor.read(wizard.draft).settlements
	if list.size() <= 1 or index >= list.size():
		return
	list.remove_at(index)
	wizard.edit(WorldEditor.with_settlements(wizard.draft, list))
	sync()


## Clique na prévia: move o assentamento marcado para a célula clicada.
func _on_preview_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		var cell := preview_cell_at(event.position)
		if cell.x >= 0:
			change_settlement(selected_settlement, {"x": cell.x, "y": cell.y})


## Célula do mapa sob um ponto da prévia (imagem centralizada, sem distorção); (-1,-1) fora do mapa.
func preview_cell_at(point: Vector2) -> Vector2i:
	var values: Dictionary = WorldEditor.read(wizard.draft)
	var scale := minf(preview_rect.size.x / values.width, preview_rect.size.y / values.height)
	var offset := (preview_rect.size - Vector2(values.width, values.height) * scale) / 2.0
	var local := (point - offset) / scale
	if local.x < 0 or local.y < 0 or local.x >= values.width or local.y >= values.height:
		return Vector2i(-1, -1)
	return Vector2i(int(local.x), int(local.y))
