extends Control
## Menu principal: título com estado do servidor, Continuar o mundo em execução e Novo mundo (escolha do
## período, nome, seed, prévia do mapa real e criação). Só fala com a API pelo ApiClient; o mundo em si
## nunca é decidido aqui.

signal world_ready() ## há um mundo no servidor para jogar (continuado ou recém-criado)

const UiTheme := preload("res://Scenes/ui_theme.gd")

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
	period_info.custom_minimum_size.y = 70
	left.add_child(period_info)

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
	_button("🎲", seed_row, func(): _roll_seed(); _refresh_preview())
	preview_rect = TextureRect.new()
	preview_rect.custom_minimum_size = Vector2(500, 260)
	preview_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	preview_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	preview_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
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
		_refresh_preview()


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
	var catalog: Dictionary = await api.period_catalog(id)
	if selected_period != id:
		return # escolheu outro período enquanto este carregava
	if catalog.ok:
		var professions: Array = catalog.body.professionNames.values()
		period_info.text = "Profissões: %s" % (", ".join(professions) if not professions.is_empty() else "%d no total" % catalog.body.professionIds.size())
	else:
		period_info.text = ""
	await _refresh_preview()


func _refresh_preview() -> void:
	if selected_period == "":
		return
	_preview_generation += 1
	var generation := _preview_generation
	preview_note.text = "Gerando prévia…"
	var scenario: String = await api.period_scenario(selected_period, seed_value)
	var response: Dictionary = await api.world_preview(scenario) if scenario != "" else {"ok": false}
	if generation != _preview_generation:
		return # uma prévia mais nova já está a caminho
	if not response.ok:
		preview_rect.texture = null
		preview_note.text = "Prévia indisponível"
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


func _on_continue() -> void:
	world_ready.emit()


func _on_create() -> void:
	if _busy:
		return
	_busy = true
	_refresh_create()
	message_label.text = ""
	var scenario: String = await api.period_scenario(selected_period, seed_value)
	var response: Dictionary = await api.world_create(scenario, world_name.strip_edges()) if scenario != "" else {"ok": false, "body": "período indisponível"}
	_busy = false
	_refresh_create()
	if response.ok:
		world_ready.emit()
	else:
		message_label.text = "Não foi possível criar o mundo: %s" % str(response.body)
