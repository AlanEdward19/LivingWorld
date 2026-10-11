extends RefCounted
## Painéis de consulta (UI de espectador): lista de NPCs, ficha de NPC, ficha de cidade e relógio.
## Cada painel é um Control com API pública simples; todos os dados vêm do servidor por `transport`.

const PAGE_SIZE := 50


class NpcListPanel extends PanelContainer:
	signal npc_selected(id: int)
	var transport
	var rows: Array = [] ## [{id, name}]
	var total := 0
	var query := ""
	var skip := 0
	var empty_label: Label
	var _list: ItemList
	var _search: LineEdit
	var _page_label: Label
	var city_filter := ""

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
		var path := "/npcs?skip=%d&take=%d" % [skip, PAGE_SIZE]
		if query != "":
			path += "&q=" + query.uri_encode()
		if city_filter != "":
			path += "&cityId=" + city_filter.uri_encode()
		var response: Dictionary = await transport.request("GET", path)
		rows.clear()
		_list.clear()
		if response.status != 200 or not (response.body is Dictionary):
			total = 0
		else:
			total = int(response.body.total)
			for item in response.body.items:
				var id: int = int(item.id.value)
				rows.append({"id": id, "name": item.name})
				_list.add_item("%s  #%d" % [item.name, id])
		empty_label.visible = rows.is_empty()
		_page_label.text = "%d–%d de %d" % [skip + 1 if total > 0 else 0, skip + rows.size(), total]


class NpcDetailPanel extends PanelContainer:
	signal highlight(id: int)
	var transport
	var npc_id := -1
	var text := ""
	var _label: RichTextLabel

	func _init() -> void:
		custom_minimum_size = Vector2(280, 0)
		_label = RichTextLabel.new()
		_label.fit_content = true
		_label.bbcode_enabled = true
		add_child(_label)
		visible = false

	func show_npc(id: int) -> void:
		var response: Dictionary = await transport.request("GET", "/npcs/%d" % id)
		if response.status != 200 or not (response.body is Dictionary):
			return
		npc_id = id
		var npc: Dictionary = response.body
		text = "%s\nIdade %d · saúde %d\nFome %d · Sede %d · Sono %d · Social %d" % [
			npc.name, int(npc.ageYears), int(npc.health), int(npc.hunger), int(npc.thirst), int(npc.sleep), int(npc.social)]
		_label.text = "[b]%s[/b]\nIdade %d · saúde %d\nFome %d · Sede %d · Sono %d · Social %d" % [
			npc.name, int(npc.ageYears), int(npc.health), int(npc.hunger), int(npc.thirst), int(npc.sleep), int(npc.social)]
		visible = true
		highlight.emit(id)


class CityPanel extends PanelContainer:
	var text := ""
	var _label: RichTextLabel

	func _init() -> void:
		custom_minimum_size = Vector2(280, 0)
		_label = RichTextLabel.new()
		_label.fit_content = true
		_label.bbcode_enabled = true
		add_child(_label)
		visible = false

	## `info` = WorldStore.city: {name, indicators:{population, wealth, health, inequality, economy, housing}}
	func show_city(info: Dictionary) -> void:
		var indicators: Dictionary = info.indicators
		var name: String = info.name if info.name != "" else "Cidade"
		text = "%s\nPopulação %d\nRiqueza %d · Saúde %d\nDesigualdade %s · Economia %d · Habitação %d" % [
			name, int(indicators.population), int(indicators.wealth), int(indicators.health),
			str(snappedf(float(indicators.inequality), 0.01)), int(indicators.economy), int(indicators.housing)]
		_label.text = "[b]%s[/b]\n%s" % [name, text.get_slice("\n", 1) + "\n" + text.get_slice("\n", 2) + "\n" + text.get_slice("\n", 3)]
		visible = true


class ClockPanel extends HBoxContainer:
	var transport
	var status: Dictionary = {}
	var status_text := ""
	var _label: Label
	var _speed: SpinBox
	signal changed()
	var _timer: Timer

	func _init() -> void:
		_label = Label.new()
		add_child(_label)
		for spec in [["⏸", "pause"], ["▶", "resume"], ["Passo", "step"], ["+1 ano", "advance-year"]]:
			var button := Button.new()
			button.text = spec[0]
			var action: String = spec[1]
			button.pressed.connect(func(): command(action))
			add_child(button)
		_speed = SpinBox.new()
		_speed.min_value = 0.25
		_speed.max_value = 1000
		_speed.step = 0.25
		_speed.value = 1
		_speed.suffix = " t/s"
		_speed.value_changed.connect(func(v: float): set_speed(v))
		add_child(_speed)
		_timer = Timer.new()
		_timer.wait_time = 1.0
		_timer.autostart = true
		_timer.timeout.connect(refresh)
		add_child(_timer)

	func command(action: String) -> void:
		await transport.request("POST", "/simulation/" + action)
		await refresh()
		changed.emit()

	func set_speed(ticks_per_second: float) -> void:
		await transport.request("POST", "/simulation/speed", {"ticksPerSecond": ticks_per_second})
		await refresh()

	func refresh() -> void:
		var response: Dictionary = await transport.request("GET", "/simulation/status")
		if response.status != 200 or not (response.body is Dictionary):
			status_text = "sem conexão"
		else:
			status = response.body
			status_text = "%s · %s ticks/s · tick %d · ano %d" % [
				"PAUSADO" if status.isPaused else "rodando", str(status.ticksPerSecond), int(status.tick), int(status.year)]
		_label.text = status_text
