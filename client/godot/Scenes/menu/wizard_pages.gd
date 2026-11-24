extends RefCounted
## As telas do Novo mundo, uma por assunto: começo (lugar), nome e semente, tamanho e pessoas, mapa e resumo. Só montagem:
## cada controle chama o assistente (NewWorldWizard), que guarda o estado e fala com a API.

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const PeriodArt := preload("res://Scenes/menu/period_art.gd")
const WorldEditor := preload("res://Scenes/world_editor.gd")
const TERRAIN_COLORS: Array[Color] = [Color("5f8f4e"), Color("c9b26b"), Color("7b8b6f"), Color("8a7a66"), Color("4f7a64"), Color("a8a39a")]
const WATER_COLOR := Color("3b6ea5")
const SETTLEMENT_COLOR := Color("ffd84a")


static func _page(w, heading: String, hint: String) -> VBoxContainer:
	var page := VBoxContainer.new()
	page.add_theme_constant_override("separation", 12)
	page.size_flags_vertical = Control.SIZE_EXPAND_FILL
	Kit.title(heading, 28, page, Color.WHITE)
	var line := Kit.text(hint, page, 16)
	line.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	line.name = "hint"
	w.pages_box.add_child(page)
	return page


const PERIOD_HINT := "Escolha o lugar. Dá para mudar o tamanho depois."


static func period(w) -> Control:
	var page := _page(w, "Onde a história começa?", PERIOD_HINT)
	w.period_hint = page.get_node("hint")
	w.period_cards = HBoxContainer.new()
	w.period_cards.add_theme_constant_override("separation", 14)
	page.add_child(w.period_cards)
	w.period_info = Kit.text("", page, 15)
	w.period_info.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return page


## Cartões dos períodos que o servidor oferece (GET /periods), com nome, descrição, pessoas e vilas do cartão dele. Se
## todos são da mesma época, a frase do passo diz isso em vez de prometer épocas que não existem.
static func fill_periods(w, periods: Array) -> void:
	for child in w.period_cards.get_children():
		child.queue_free()
	var era := PeriodArt.shared_era(periods)
	w.period_hint.text = PERIOD_HINT if era == "" or periods.size() < 2 else \
		"Todos começam na mesma época: %s. O que muda é o tamanho." % era
	for i in periods.size():
		var item: Dictionary = periods[i]
		var id := String(item.periodId)
		var houses := clampi(int(item.get("width", 10)) / 2, 2, 14)
		var card := Kit.card(PeriodArt.name_for(item), PeriodArt.line_for(item), PeriodArt.picture(houses, i + 3), w.period_cards,
			func(): w.pick_period(id))
		card.name = "period_" + id


static func names(w) -> Control:
	var page := _page(w, "Como vai se chamar?", "Todo mundo precisa de um nome. A semente decide como o mundo nasce.")
	Kit.text("Nome do mundo", page, 17, Color.WHITE)
	var name_row := Kit.row(page)
	w.name_edit = LineEdit.new()
	w.name_edit.placeholder_text = "Ex.: Vale Dourado"
	w.name_edit.max_length = 40
	w.name_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	w.name_edit.custom_minimum_size.y = 44
	w.name_edit.add_theme_font_size_override("font_size", 20)
	w.name_edit.text_changed.connect(func(text: String): w.world_name = text; w.clear_errors())
	w.name_edit.text_submitted.connect(func(_t: String): w.seed_edit.grab_focus())
	name_row.add_child(w.name_edit)
	Kit.button("🎲 Sortear", name_row, w.roll_name)
	w.name_error = Kit.text("", page, 15, Kit.ERROR)
	Kit.spacer(page, 6)
	Kit.text("Semente", page, 17, Color.WHITE)
	var seed_row := Kit.row(page)
	w.seed_edit = LineEdit.new()
	w.seed_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	w.seed_edit.custom_minimum_size.y = 44
	w.seed_edit.add_theme_font_size_override("font_size", 20)
	w.seed_edit.text_changed.connect(func(_t: String): w.clear_errors())
	w.seed_edit.text_submitted.connect(func(_t: String): w.commit_seed(); w.next())
	w.seed_edit.focus_exited.connect(w.commit_seed)
	seed_row.add_child(w.seed_edit)
	Kit.button("🎲 Sortear", seed_row, func(): w.roll_seed(); w.edit(WorldEditor.with_seed(w.draft, w.seed_value)))
	Kit.text("A semente é o número que dá forma ao mundo: a mesma semente cria sempre o mesmo mapa e as mesmas famílias.", page, 14)
	w.seed_error = Kit.text("", page, 15, Kit.ERROR)
	return page


static func size(w) -> Control:
	var page := _page(w, "De que tamanho?", "Mais gente, mais histórias (e mais trabalho para o computador).")
	var row := Kit.row(page, 14)
	w.size_buttons.clear()
	for i in PeriodArt.SIZES.size():
		var spec: Dictionary = PeriodArt.SIZES[i]
		var card := Kit.card("%s" % spec.name, "%s\n%d pessoas · mapa %d×%d" % [spec.line, spec.population, spec.side, spec.side],
			PeriodArt.picture([3, 7, 13][i], 20 + i), row, func(): w.pick_size(i))
		card.custom_minimum_size.y = 270
		w.size_buttons.append(card)
	Kit.spacer(page, 4)
	var people := Kit.row(page)
	var caption := Label.new()
	caption.text = "Pessoas no começo"
	caption.add_theme_font_size_override("font_size", 17)
	people.add_child(caption)
	w.population_slider = HSlider.new()
	w.population_slider.min_value = 10
	w.population_slider.max_value = WorldEditor.MAX_POPULATION
	w.population_slider.step = 10
	w.population_slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	w.population_slider.focus_mode = Control.FOCUS_ALL
	w.population_slider.value_changed.connect(func(v: float): w.set_population(int(v)))
	people.add_child(w.population_slider)
	w.population_label = Label.new()
	w.population_label.custom_minimum_size.x = 190
	w.population_label.add_theme_font_size_override("font_size", 17)
	people.add_child(w.population_label)
	return page


static func map(w) -> Control:
	var page := _page(w, "Este é o seu mundo", "Água em azul, a sua vila em amarelo. Não gostou? Sorteie outro.")
	w.preview_rect = TextureRect.new()
	w.preview_rect.custom_minimum_size = Vector2(560, 300)
	w.preview_rect.size_flags_vertical = Control.SIZE_EXPAND_FILL
	w.preview_rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	w.preview_rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	w.preview_rect.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	page.add_child(w.preview_rect)
	w.preview_note = Kit.text("", page, 15)
	w.preview_note.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var row := Kit.row(page)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	Kit.button("🎲 Outro mapa", row, func(): w.roll_seed(); w.edit(WorldEditor.with_seed(w.draft, w.seed_value)))
	w.customize_button = Kit.button("Personalizar mais… (opcional)", row, w.show_customize)
	return page


static func summary(w) -> Control:
	var page := _page(w, "Tudo pronto?", "Confira e crie. O mundo começa a viver assim que nascer.")
	var row := Kit.row(page, 20)
	w.summary_map = TextureRect.new()
	w.summary_map.custom_minimum_size = Vector2(260, 200)
	w.summary_map.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	w.summary_map.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	w.summary_map.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	row.add_child(w.summary_map)
	w.summary_label = Kit.text("", row, 19, Color.WHITE)
	w.summary_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	w.message_label = Kit.text("", page, 16, Kit.ERROR)
	w.message_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	return page


## Mapa real devolvido pelo servidor para esta semente: um pixel por terra, água azul, vilas em amarelo.
static func preview_image(body: Dictionary) -> Image:
	var width := int(body.width)
	var height := int(body.height)
	var image := Image.create(maxi(width, 1), maxi(height, 1), false, Image.FORMAT_RGB8)
	for cell in body.cells:
		var color: Color = WATER_COLOR if cell.hasWater else TERRAIN_COLORS[int(cell.terrain) % TERRAIN_COLORS.size()]
		image.set_pixel(int(cell.x), int(cell.y), color.lightened(clampf(float(cell.altitude) / 400.0, 0.0, 0.35)))
	for settlement in body.settlements:
		image.set_pixel(int(settlement.x), int(settlement.y), SETTLEMENT_COLOR)
	return image
