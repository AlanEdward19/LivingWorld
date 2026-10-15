extends PanelContainer
## Janela de narrativa: biografia de um NPC, crônica de uma cidade e relatos circulando (`/narratives/*`).
## O texto é o que o servidor escreveu a partir das crenças da comunidade; o cliente só o mostra, com as
## afirmações (`claims`) e os eventos que as sustentam. Datas dos períodos vêm do calendário do servidor.

signal closed()
signal warning(text: String)

const PERIODS := [["month", "Último mês"], ["year", "Último ano"], ["decade", "Última década"]]
const MEDIA := {"LivingMemory": "memória viva", "OralTradition": "tradição oral", "Book": "livro", "Monument": "monumento", "Song": "canção"}
const TOP_K := 5

var api
var calendar_of: Callable = func() -> Dictionary: return {"hoursPerDay": 24, "daysPerMonth": 30, "monthsPerYear": 12}
var tick_now: Callable = func() -> int: return 0
var mode := "" ## biography | chronicle | reports
var subject_id := ""
var subject_name := ""
var period_kind := "year"
var is_open := false
var prose := ""
var claims: Array = []
var reports: Array = []
var empty_text := ""
var period_option: OptionButton
var report_rows: Array = [] ## [{text, confidence, bar}]
var _title: Label
var _prose: Label
var _list: VBoxContainer
var _empty: Label
var _request := 0


func _init() -> void:
	visible = false
	var box := VBoxContainer.new()
	add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	_title = Label.new()
	_title.add_theme_font_size_override("font_size", 20)
	_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_title)
	period_option = OptionButton.new()
	for period in PERIODS:
		period_option.add_item(period[1])
	period_option.select(1)
	period_option.item_selected.connect(func(index: int): set_period(PERIODS[index][0]))
	period_option.visible = false
	header.add_child(period_option)
	var close_button := Button.new()
	close_button.text = "Fechar (Esc)"
	close_button.pressed.connect(func(): close())
	header.add_child(close_button)
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	box.add_child(scroll)
	var body := VBoxContainer.new()
	body.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(body)
	_prose = Label.new()
	_prose.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_prose.custom_minimum_size.x = 560
	body.add_child(_prose)
	_empty = Label.new()
	_empty.modulate = Color(1, 1, 1, 0.8)
	_empty.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_empty.custom_minimum_size.x = 560
	body.add_child(_empty)
	_list = VBoxContainer.new()
	_list.add_theme_constant_override("separation", 4)
	body.add_child(_list)


func show_biography(npc_id: int, npc_name: String) -> void:
	await _open("biography", str(npc_id), npc_name)


func show_chronicle(city_id: String, city_name: String) -> void:
	await _open("chronicle", city_id, city_name)


func show_reports() -> void:
	await _open("reports", "", "")


func set_period(kind: String) -> void:
	period_kind = kind
	for i in PERIODS.size():
		if PERIODS[i][0] == kind:
			period_option.select(i)
	if is_open and mode == "chronicle":
		await refresh()


func close() -> void:
	if not is_open:
		return
	is_open = false
	visible = false
	_request += 1
	closed.emit()


## [início, fim] em ticks do período terminando agora, pelo calendário do servidor (nunca 24 h / 30 dias fixos).
func period_ticks(kind: String) -> Array:
	var calendar: Dictionary = calendar_of.call()
	var month: int = int(calendar.hoursPerDay) * int(calendar.daysPerMonth)
	var year: int = month * int(calendar.monthsPerYear)
	var span: int = {"month": month, "year": year, "decade": year * 10}.get(kind, year)
	var now: int = int(tick_now.call())
	return [maxi(0, now - span), now]


func refresh() -> void:
	_request += 1
	var mine := _request
	var response: Dictionary
	match mode:
		"biography":
			response = await api.biography(int(subject_id))
		"chronicle":
			var period := period_ticks(period_kind)
			response = await api.chronicle(subject_id, period[0], period[1], TOP_K)
		_:
			response = await api.reports()
	if mine != _request or not is_open:
		return # trocou de assunto ou fechou durante a espera
	_render(response)


func _open(p_mode: String, id: String, name: String) -> void:
	mode = p_mode
	subject_id = id
	subject_name = name
	is_open = true
	visible = true
	period_option.visible = mode == "chronicle"
	_title.text = {"biography": "Biografia de %s" % name, "chronicle": "Crônica de %s" % name, "reports": "Relatos que circulam"}[mode]
	_clear()
	await refresh()


func _clear() -> void:
	prose = ""
	claims = []
	reports = []
	report_rows = []
	empty_text = ""
	_prose.text = ""
	_empty.text = ""
	for child in _list.get_children():
		_list.remove_child(child)
		child.queue_free()


func _render(response: Dictionary) -> void:
	_clear()
	if response.status == 404 and mode == "biography":
		_say("Sem biografia: este NPC não existe mais ou nunca foi registrado.")
		return
	if not response.ok:
		var why := "resposta fora do formato esperado" if response.status == 200 else "erro %d" % response.status
		_say("Não foi possível carregar (%s)." % why)
		warning.emit("Não foi possível carregar a narrativa (%s)." % why)
		return
	if mode == "reports":
		_render_reports(response.body)
		return
	prose = String(response.body.get("prose", ""))
	claims = response.body.get("claims", [])
	_prose.text = prose
	if claims.is_empty():
		_say("Nada foi registrado ainda sobre %s." % subject_name if mode == "biography" else "Nada de notável aconteceu neste período.")
		return
	var header := Label.new()
	header.text = "Afirmações"
	header.add_theme_color_override("font_color", Color("ffe9a6"))
	_list.add_child(header)
	for claim in claims:
		var label := Label.new()
		label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		label.custom_minimum_size.x = 560
		var events: Array = claim.get("eventIds", [])
		label.text = "• %s%s" % [claim.text, "  [eventos: %s]" % ", ".join(events.map(func(e): return str(int(e)))) if not events.is_empty() else ""]
		_list.add_child(label)


func _render_reports(items: Variant) -> void:
	reports = items if items is Array else []
	if reports.is_empty():
		_say("Nenhum relato circulando no mundo.")
		return
	for item in reports:
		var row := HBoxContainer.new()
		var text := "%s · fato %d" % [MEDIA.get(String(item.medium), String(item.medium)), int(item.originFactId)]
		var label := Label.new()
		label.text = text
		label.custom_minimum_size.x = 240
		row.add_child(label)
		var bar := ProgressBar.new()
		bar.max_value = 100
		bar.value = clampf(float(item.confidence), 0.0, 1.0) * 100.0
		bar.show_percentage = false
		bar.custom_minimum_size = Vector2(160, 14)
		bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(bar)
		var percent := Label.new()
		percent.text = "confiança %d%%" % int(round(bar.value))
		row.add_child(percent)
		_list.add_child(row)
		report_rows.append({"text": text, "confidence": float(item.confidence), "bar": bar})


func _say(text: String) -> void:
	empty_text = text
	_empty.text = text
