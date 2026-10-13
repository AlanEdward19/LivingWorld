extends RefCounted
## Painéis de consulta (UI de espectador): lista de NPCs, ficha de NPC, ficha de cidade e relógio.
## Cada painel é um Control com API pública simples; todos os dados vêm do servidor pelo `api` (ApiClient).

const PAGE_SIZE := 50


class NpcListPanel extends PanelContainer:
	signal npc_selected(id: int)
	var api
	var rows: Array = [] ## [{id, name}]
	var total := 0
	var query := ""
	var skip := 0
	var empty_label: Label
	var _list: ItemList
	var _search: LineEdit
	var _page_label: Label
	var city_filter := ""
	var failed := false

	func _init() -> void:
		custom_minimum_size = Vector2(280, 0)
		var box := VBoxContainer.new()
		add_child(box)
		var title := Label.new()
		title.text = "Pessoas"
		title.add_theme_font_size_override("font_size", 18)
		box.add_child(title)
		_search = LineEdit.new()
		_search.placeholder_text = "buscar por nome"
		_search.text_submitted.connect(func(text: String): search(text))
		box.add_child(_search)
		_list = ItemList.new()
		_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
		_list.item_selected.connect(func(index: int): npc_selected.emit(rows[index].id))
		box.add_child(_list)
		empty_label = Label.new()
		empty_label.text = "nenhum NPC"
		empty_label.visible = false
		box.add_child(empty_label)
		var pager := HBoxContainer.new()
		var prev := Button.new()
		prev.text = "◀"
		prev.pressed.connect(func(): page(-1))
		var next := Button.new()
		next.text = "▶"
		next.pressed.connect(func(): page(1))
		_page_label = Label.new()
		pager.add_child(prev)
		pager.add_child(_page_label)
		pager.add_child(next)
		box.add_child(pager)

	func search(text: String) -> void:
		query = text
		skip = 0
		await load_page()

	func page(direction: int) -> void:
		var next_skip := skip + direction * PAGE_SIZE
		if next_skip < 0 or next_skip >= total:
			return
		skip = next_skip
		await load_page()

	func load_page() -> void:
		var response: Dictionary = await api.npc_page(query, skip, PAGE_SIZE, city_filter)
		rows.clear()
		_list.clear()
		failed = not response.ok
		if failed:
			total = 0
		else:
			total = int(response.body.total)
			for item in response.body.items:
				var id: int = int(item.id.value)
				rows.append({"id": id, "name": item.name})
				_list.add_item("%s  #%d" % [item.name, id])
		empty_label.text = "erro ao carregar a lista" if failed else "nenhum NPC"
		empty_label.visible = rows.is_empty()
		_page_label.text = "%d–%d de %d" % [skip + 1 if total > 0 else 0, skip + rows.size(), total]


class NpcDetailPanel extends PanelContainer:
	signal highlight(id: int)
	signal follow_requested(id: int)
	signal model_known(id: int, profession: int, age_years: float)
	var api
	var labels
	var npc_id := -1
	var text := ""
	var bars: Dictionary = {} ## "Fome" -> ProgressBar
	var _title: Label
	var _subtitle: Label
	var _grid: VBoxContainer

	func _init() -> void:
		custom_minimum_size = Vector2(290, 0)
		var box := VBoxContainer.new()
		add_child(box)
		_title = Label.new()
		_title.add_theme_font_size_override("font_size", 20)
		box.add_child(_title)
		_subtitle = Label.new()
		_subtitle.modulate = Color(1, 1, 1, 0.75)
		box.add_child(_subtitle)
		_grid = VBoxContainer.new()
		box.add_child(_grid)
		for spec in ["Saúde", "Fome", "Sede", "Sono", "Social"]:
			var row := HBoxContainer.new()
			var name := Label.new()
			name.text = spec
			name.custom_minimum_size.x = 56
			row.add_child(name)
			var bar := ProgressBar.new()
			bar.max_value = 100
			bar.show_percentage = false
			bar.custom_minimum_size = Vector2(150, 14)
			bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
			row.add_child(bar)
			_grid.add_child(row)
			bars[spec] = bar
		var follow := Button.new()
		follow.text = "Seguir (F)"
		follow.pressed.connect(func(): follow_requested.emit(npc_id))
		box.add_child(follow)
		visible = false

	func show_npc(id: int) -> void:
		var response: Dictionary = await api.npc_detail(id)
		if not response.ok or not (response.body is Dictionary):
			return
		npc_id = id
		var npc: Dictionary = response.body
		var action_text := ""
		if labels != null and npc.get("currentAction") != null:
			action_text = labels.label_for(int(npc.currentAction))
		_title.text = npc.name
		_subtitle.text = "%d anos%s" % [int(npc.ageYears), (" · " + action_text) if action_text != "" else ""]
		var values := {"Saúde": npc.health, "Fome": npc.hunger, "Sede": npc.thirst, "Sono": npc.sleep, "Social": npc.social}
		for key in values:
			bars[key].value = float(values[key])
			bars[key].modulate = Color("5fb36b") if float(values[key]) >= 40.0 else Color("d9a441") if float(values[key]) >= 20.0 else Color("d9534f")
		text = "%s\nIdade %d · saúde %d\nFome %d · Sede %d · Sono %d · Social %d" % [
			npc.name, int(npc.ageYears), int(npc.health), int(npc.hunger), int(npc.thirst), int(npc.sleep), int(npc.social)]
		visible = true
		highlight.emit(id)
		var profession = npc.get("profession")
		model_known.emit(id, int(profession.id) if profession is Dictionary else -1, float(npc.ageYears))


class CityPanel extends PanelContainer:
	var text := ""
	var _title: Label
	var _body: Label

	func _init() -> void:
		custom_minimum_size = Vector2(290, 0)
		var box := VBoxContainer.new()
		add_child(box)
		_title = Label.new()
		_title.add_theme_font_size_override("font_size", 20)
		box.add_child(_title)
		_body = Label.new()
		_body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		_body.custom_minimum_size.x = 270
		box.add_child(_body)
		visible = false

	## `info` = WorldStore.city: {name, indicators:{population, wealth, health, inequality, economy, housing}}
	func show_city(info: Dictionary) -> void:
		var indicators: Dictionary = info.indicators
		var name: String = info.name if info.name != "" else "Cidade"
		text = "%s\nPopulação %d\nRiqueza %d · Saúde %d\nDesigualdade %s · Economia %d · Habitação %d" % [
			name, int(indicators.population), int(indicators.wealth), int(indicators.health),
			str(snappedf(float(indicators.inequality), 0.01)), int(indicators.economy), int(indicators.housing)]
		_title.text = name
		_body.text = text.substr(text.find("\n") + 1)
		visible = true


class ClockPanel extends HBoxContainer:
	const SPEEDS: Array[float] = [1.0, 4.0, 12.0, 48.0, 200.0]
	var api
	var status: Dictionary = {}
	var status_text := ""
	var _label: Label
	var _timer: Timer
	var _speed_buttons: Array = []
	signal changed()
	signal status_changed(status: Dictionary)

	func _init() -> void:
		add_theme_constant_override("separation", 4)
		_label = Label.new()
		add_child(_label)
		for spec in [["⏸", "pause"], ["▶", "resume"], ["Passo", "step"], ["+1 ano", "advance-year"]]:
			var button := Button.new()
			button.text = spec[0]
			var action: String = spec[1]
			button.pressed.connect(func(): command(action))
			add_child(button)
		for speed in SPEEDS:
			var button := Button.new()
			button.text = "%d×" % int(speed)
			button.toggle_mode = true
			var value: float = speed
			button.pressed.connect(func(): set_speed(value))
			add_child(button)
			_speed_buttons.append(button)
		_timer = Timer.new()
		_timer.wait_time = 1.0
		_timer.autostart = true
		_timer.timeout.connect(refresh)
		add_child(_timer)

	func command(action: String) -> void:
		await api.clock_command(action)
		await refresh()
		changed.emit()

	func set_speed(ticks_per_second: float) -> void:
		await api.set_speed(ticks_per_second)
		await refresh()

	func refresh() -> void:
		var response: Dictionary = await api.clock_status()
		if not response.ok:
			status_text = "sem conexão"
		else:
			status = response.body
			status_text = "%s · %s ticks/s · tick %d · ano %d" % [
				"PAUSADO" if status.isPaused else "rodando", str(status.ticksPerSecond), int(status.tick), int(status.year)]
			for i in _speed_buttons.size():
				_speed_buttons[i].button_pressed = is_equal_approx(SPEEDS[i], float(status.ticksPerSecond))
			status_changed.emit(status)
		_label.text = status_text
