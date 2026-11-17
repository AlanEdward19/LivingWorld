extends Control
## Novo mundo como jornada em passos: época → nome e semente → tamanho → mapa → resumo, com Voltar/Avançar, barra de
## progresso e "Surpreenda-me" (preenche tudo). "Personalizar" é um desvio opcional a partir do mapa. Guarda o
## rascunho do cenário (texto cru do período com as escolhas) e só fala com a API pelo ApiClient.

signal cancelled()
signal creating() ## pedido de criação enviado (o menu mostra o carregamento)
signal created(touchup: bool) ## o servidor criou o mundo
signal create_failed()
signal map_previewed(body: Dictionary) ## mapa real para o fundo 3D do menu
signal family_requested()

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const Pages := preload("res://Scenes/menu/wizard_pages.gd")
const PeriodArt := preload("res://Scenes/menu/period_art.gd")
const CustomizePanel := preload("res://Scenes/menu/customize_panel.gd")
const WorldEditor := preload("res://Scenes/world_editor.gd")

const STEP_NAMES: Array[String] = ["Época", "Nome", "Tamanho", "Mapa", "Resumo"]
const MAP_STEP := 3
const SUMMARY_STEP := 4
const CUSTOM_STEP := 5 ## fora da barra: desvio opcional
const MAX_SEED := 999999999

var api
var step := 0
var periods: Array = []
var period_ids: Array[String] = []
var selected_period := ""
var world_name := ""
var seed_value := 1
var draft := "" ## cenário do período em texto cru, com as escolhas aplicadas (ver WorldEditor)
var professions: Dictionary = {}
var skills: Dictionary = {}
var size_choice := -1
var touchup_after := false
var preview_body: Dictionary = {}
var busy := false

var pages_box: Control
var pages: Array[Control] = []
var progress: ProgressBar
var step_label: Label
var back_button: Button
var surprise_button: Button
var next_button: Button
var period_cards: HBoxContainer
var period_info: Label
var name_edit: LineEdit
var name_error: Label
var seed_edit: LineEdit
var seed_error: Label
var size_buttons: Array = []
var population_slider: HSlider
var population_label: Label
var preview_rect: TextureRect
var preview_note: Label
var customize_button: Button
var summary_map: TextureRect
var summary_label: Label
var message_label: Label
var custom: VBoxContainer

var _rng := RandomNumberGenerator.new()
var _preview_generation := 0


func _ready() -> void:
	_rng.randomize()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var box := Kit.screen(self, 980)
	var top := Kit.row(box)
	step_label = Label.new()
	step_label.add_theme_font_size_override("font_size", 16)
	step_label.add_theme_color_override("font_color", Kit.GOLD)
	top.add_child(step_label)
	progress = ProgressBar.new()
	progress.max_value = STEP_NAMES.size()
	progress.show_percentage = false
	progress.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	progress.custom_minimum_size.y = 10
	progress.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	top.add_child(progress)
	pages_box = VBoxContainer.new()
	pages_box.custom_minimum_size.y = 430
	box.add_child(pages_box)
	pages = [Pages.period(self), Pages.names(self), Pages.size(self), Pages.map(self), Pages.summary(self)]
	var custom_page := VBoxContainer.new()
	Kit.title("Personalizar (opcional)", 28, custom_page)
	custom = CustomizePanel.new()
	custom.wizard = self
	custom.family_requested.connect(func(): family_requested.emit())
	custom_page.add_child(custom)
	pages_box.add_child(custom_page)
	pages.append(custom_page)
	var footer := Kit.row(box)
	back_button = Kit.big_button("◀ Voltar", footer, back)
	surprise_button = Kit.big_button("🎲 Surpreenda-me", footer, surprise)
	next_button = Kit.big_button("Avançar ▶", footer, next)
	for b in [back_button, surprise_button, next_button]:
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	go_to(0)


## Abre o assistente: nome e semente sorteados, períodos do servidor.
func open() -> void:
	if world_name == "":
		roll_name()
		roll_seed()
	if period_ids.is_empty():
		await load_periods()
	go_to(0)


func go_to(index: int) -> void:
	step = index
	for i in pages.size():
		pages[i].visible = i == index
	if period_cards != null:
		_mark_choices()
	var in_bar := mini(index, SUMMARY_STEP)
	progress.value = in_bar + 1
	step_label.text = "Passo %d de %d · %s" % [in_bar + 1, STEP_NAMES.size(), STEP_NAMES[in_bar]] if index != CUSTOM_STEP else "Desvio opcional · Personalizar"
	next_button.text = "Criar mundo ✔" if index == SUMMARY_STEP else ("Pronto ▶" if index == CUSTOM_STEP else "Avançar ▶")
	back_button.text = "◀ Voltar ao menu" if index == 0 else "◀ Voltar"
	surprise_button.visible = index != CUSTOM_STEP
	clear_errors()
	if index == MAP_STEP or index == SUMMARY_STEP or index == CUSTOM_STEP:
		if index == CUSTOM_STEP:
			custom.sync()
		await refresh_preview()
	if index == SUMMARY_STEP:
		_fill_summary()
	if is_inside_tree():
		Kit.fade_in(pages[index])
		focus_default()


func focus_default() -> void:
	if not is_inside_tree():
		return
	match step:
		0:
			var chosen := period_ids.find(selected_period)
			if chosen >= 0 and chosen < period_cards.get_child_count():
				(period_cards.get_child(chosen) as Control).grab_focus()
			else:
				next_button.grab_focus()
		1:
			name_edit.grab_focus()
		2:
			if size_choice >= 0:
				size_buttons[size_choice].grab_focus()
		_:
			next_button.grab_focus()


func next() -> void:
	if busy:
		return
	if step == CUSTOM_STEP:
		go_to(MAP_STEP)
		return
	var error := validate(step)
	if error != "":
		return
	if step == SUMMARY_STEP:
		await create()
		return
	go_to(step + 1)


func back() -> void:
	if busy:
		return
	if step == 0:
		cancelled.emit()
	elif step == CUSTOM_STEP:
		go_to(MAP_STEP)
	else:
		go_to(step - 1)


func show_customize() -> void:
	go_to(CUSTOM_STEP)


## Erro em frase humana ao lado do campo; devolve o texto ("" = ok).
func validate(index: int) -> String:
	clear_errors()
	match index:
		0:
			if selected_period == "" or draft == "":
				period_info.text = "Escolha um lugar para começar (se a lista está vazia, o mundo pode estar desligado)."
				period_info.add_theme_color_override("font_color", Kit.ERROR)
				return period_info.text
		1:
			if world_name.strip_edges() == "":
				name_error.text = "Dê um nome ao seu mundo (ou aperte Sortear)."
				return name_error.text
			if not seed_edit.text.strip_edges().is_valid_int() or int(seed_edit.text) < 1 or int(seed_edit.text) > MAX_SEED:
				seed_error.text = "A semente é um número inteiro de 1 a 999.999.999."
				return seed_error.text
			commit_seed()
	return ""


func clear_errors() -> void:
	if name_error != null:
		name_error.text = ""
		seed_error.text = ""
		message_label.text = ""
		period_info.remove_theme_color_override("font_color")


func show_error(text: String) -> void:
	message_label.text = text


func roll_name() -> void:
	world_name = PeriodArt.random_name(_rng)
	if name_edit != null:
		name_edit.text = world_name


func roll_seed() -> void:
	seed_value = _rng.randi_range(1, MAX_SEED)
	if seed_edit != null:
		seed_edit.text = str(seed_value)


func commit_seed() -> void:
	if not seed_edit.text.strip_edges().is_valid_int():
		return
	var parsed := clampi(int(seed_edit.text), 1, MAX_SEED)
	seed_edit.text = str(parsed)
	if parsed != seed_value:
		seed_value = parsed
		edit(WorldEditor.with_seed(draft, seed_value))


func edit(new_draft: String) -> void:
	draft = new_draft
	if step == MAP_STEP or step == SUMMARY_STEP or step == CUSTOM_STEP:
		refresh_preview()


func load_periods() -> void:
	var response: Dictionary = await api.periods()
	period_ids.clear()
	periods.clear()
	if not response.ok:
		period_info.text = "Não deu para ver os lugares disponíveis. O mundo está ligado?"
		return
	for item in response.body:
		periods.append(item)
		period_ids.append(String(item.periodId))
	Pages.fill_periods(self, periods)
	if not period_ids.is_empty():
		await select_period(period_ids[0])


func select_period(id: String) -> void:
	selected_period = id
	for i in period_cards.get_child_count():
		(period_cards.get_child(i) as Button).set_pressed_no_signal(i < period_ids.size() and period_ids[i] == id)
	period_info.text = "Carregando…"
	var scenario: String = await api.period_scenario(id, seed_value)
	if selected_period != id:
		return
	draft = scenario
	var catalog: Dictionary = await api.period_catalog(id)
	if selected_period != id:
		return # escolheu outro período enquanto este carregava
	if catalog.ok:
		professions.clear()
		for profession_id in catalog.body.professionIds:
			professions[int(profession_id)] = String(catalog.body.professionNames.get(str(int(profession_id)), "Profissão %d" % int(profession_id)))
		skills.clear()
		for skill_id in catalog.body.get("skillIds", []):
			skills[int(skill_id)] = String(catalog.body.get("skillNames", {}).get(str(int(skill_id)), "Habilidade %d" % int(skill_id)))
		period_info.text = "Ofícios deste lugar: %s" % ", ".join(professions.values())
	else:
		period_info.text = ""
	if draft != "":
		var values: Dictionary = WorldEditor.read(draft)
		size_choice = PeriodArt.nearest_size(int(values.population))
		_sync_size()
		custom.sync()


## Cartão apertado: escolhe; apertar de novo o já escolhido (Enter duas vezes) avança.
func pick_period(id: String) -> void:
	if id == selected_period and draft != "":
		_mark_choices()
		next()
	else:
		await select_period(id)


func pick_size(index: int) -> void:
	if index == size_choice:
		_mark_choices()
		next()
	else:
		choose_size(index)


func _mark_choices() -> void:
	for i in period_cards.get_child_count():
		(period_cards.get_child(i) as Button).set_pressed_no_signal(i < period_ids.size() and period_ids[i] == selected_period)
	for i in size_buttons.size():
		size_buttons[i].set_pressed_no_signal(i == size_choice)


func choose_size(index: int) -> void:
	size_choice = index
	var spec: Dictionary = PeriodArt.SIZES[index]
	draft = WorldEditor.with_size(draft, int(spec.side), int(spec.side))
	draft = WorldEditor.with_population(draft, int(spec.population))
	_sync_size()


func set_population(count: int) -> void:
	draft = WorldEditor.with_population(draft, count)
	_sync_size()


func _sync_size() -> void:
	if draft == "" or population_slider == null:
		return
	var values: Dictionary = WorldEditor.read(draft)
	population_slider.set_value_no_signal(int(values.population))
	population_label.text = "%d pessoas (%s)" % [int(values.population), PeriodArt.crowd_label(int(values.population))]
	for i in size_buttons.size():
		size_buttons[i].set_pressed_no_signal(i == size_choice)


## Preenche tudo de um jeito bom e vai ao resumo, onde ainda dá para voltar e mudar.
func surprise() -> void:
	if busy or period_ids.is_empty():
		return
	roll_name()
	roll_seed()
	await select_period(period_ids[_rng.randi() % period_ids.size()])
	choose_size(_rng.randi() % PeriodArt.SIZES.size())
	draft = WorldEditor.with_seed(draft, seed_value)
	await go_to(SUMMARY_STEP)


func refresh_preview() -> void:
	if selected_period == "" or draft == "":
		return
	_preview_generation += 1
	var generation := _preview_generation
	preview_note.text = "Desenhando o mapa…"
	var response: Dictionary = await api.world_preview(draft)
	if generation != _preview_generation:
		return # uma prévia mais nova já está a caminho
	if not response.ok:
		preview_rect.texture = null
		preview_note.text = "Não deu para desenhar este mapa. Tente outra semente ou outro tamanho."
		return
	preview_body = response.body
	var texture := ImageTexture.create_from_image(Pages.preview_image(response.body))
	preview_rect.texture = texture
	summary_map.texture = texture
	custom.preview_rect.texture = texture
	var count: int = response.body.settlements.size()
	preview_note.text = "Mapa de %d por %d terras · %d %s" % [int(response.body.width), int(response.body.height), count, "vila" if count == 1 else "vilas"]
	map_previewed.emit(response.body)


func _fill_summary() -> void:
	var values: Dictionary = WorldEditor.read(draft) if draft != "" else {"population": 0, "width": 0, "height": 0}
	var period: Dictionary = periods[period_ids.find(selected_period)] if selected_period in period_ids else {}
	summary_label.text = "Nome: %s\nLugar: %s\nPessoas: %d (%s)\nMapa: %d por %d terras\nSemente: %d%s" % [
		world_name.strip_edges(), PeriodArt.name_for(period), int(values.population), PeriodArt.crowd_label(int(values.population)),
		int(values.width), int(values.height), seed_value,
		"\nDepois de criar: ajustar algumas pessoas" if touchup_after else ""]


## Manda criar. Erro vira frase humana no resumo; o assistente fica onde está.
func create() -> void:
	if busy:
		return
	busy = true
	clear_errors()
	creating.emit()
	var response: Dictionary = await api.world_create(draft, world_name.strip_edges(), selected_period) if draft != "" else {"ok": false, "status": 0, "body": "lugar indisponível"}
	busy = false
	if response.ok:
		created.emit(touchup_after)
		return
	if int(response.get("status", 0)) == 409:
		message_label.text = "Já existe um mundo com esta semente. Sorteie outra semente ou abra esse mundo em Meus mundos."
	elif int(response.get("status", 0)) == 0:
		message_label.text = "O mundo não respondeu. Ele ainda está ligado?"
	else:
		message_label.text = "Não deu para criar este mundo: %s" % str(response.body)
	create_failed.emit()


func _unhandled_input(event: InputEvent) -> void:
	if not is_visible_in_tree():
		return
	if event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		back()
