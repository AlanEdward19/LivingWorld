extends PanelContainer
## Economia: tesouro da cidade (saldo e o que entrou e saiu mês a mês) e vendas de uma oficina (dia a dia, por recurso).
## Só leitura do que o servidor registrou (`/cities/{id}/treasury`, `/workplaces/{id}/trades`); o nome do recurso vem do
## catálogo do período quando o cliente o conhece, senão aparece o número.

signal closed()
signal warning(text: String)

var api
var calendar_of: Callable = func() -> Dictionary: return {"hoursPerDay": 24, "daysPerMonth": 30, "monthsPerYear": 12}
var date_of: Callable = func(tick: int) -> String: return "tick %d" % tick
var resource_name_of: Callable = func(id: int) -> String: return "recurso %d" % id
var mode := "" ## treasury | trades
var subject_id := ""
var subject_name := ""
var is_open := false
var balance := 0
var month_rows: Array = [] ## [{label, revenue, spending, revenue_bar, spending_bar}]
var trade_rows: Array = [] ## [{text, value}]
var empty_text := ""
var _title: Label
var _summary: Label
var _empty: Label
var _list: VBoxContainer
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
	var close_button := Button.new()
	close_button.text = "Fechar (Esc)"
	close_button.pressed.connect(func(): close())
	header.add_child(close_button)
	_summary = Label.new()
	_summary.add_theme_color_override("font_color", Color("ffe9a6"))
	box.add_child(_summary)
	_empty = Label.new()
	_empty.modulate = Color(1, 1, 1, 0.8)
	_empty.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_empty.custom_minimum_size.x = 520
	box.add_child(_empty)
	var scroll := ScrollContainer.new()
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	box.add_child(scroll)
	_list = VBoxContainer.new()
	_list.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(_list)


func show_treasury(city_id: String, city_name: String) -> void:
	await _open("treasury", city_id, city_name)


func show_trades(workplace_id: int, place_name: String) -> void:
	await _open("trades", str(workplace_id), place_name)


func close() -> void:
	if not is_open:
		return
	is_open = false
	visible = false
	_request += 1
	closed.emit()


## "Ano 2 · Mês 5" do índice de mês absoluto, pelo calendário do servidor.
func month_label(month: int) -> String:
	var per_year: int = maxi(1, int(calendar_of.call().get("monthsPerYear", 12)))
	return "Ano %d · Mês %d" % [month / per_year, month % per_year + 1]


func _open(p_mode: String, id: String, name: String) -> void:
	mode = p_mode
	subject_id = id
	subject_name = name
	is_open = true
	visible = true
	_title.text = "Tesouro de %s" % name if mode == "treasury" else "Vendas de %s" % name
	_clear()
	_request += 1
	var mine := _request
	var response: Dictionary
	if mode == "treasury":
		response = await api.treasury(id)
	else:
		response = await api.workplace_trades(int(id))
	if mine != _request or not is_open:
		return # trocou de assunto ou fechou durante a espera
	_render(response)


func _clear() -> void:
	balance = 0
	month_rows = []
	trade_rows = []
	empty_text = ""
	_summary.text = ""
	_empty.text = ""
	for child in _list.get_children():
		_list.remove_child(child)
		child.queue_free()


func _render(response: Dictionary) -> void:
	_clear()
	if response.status == 404:
		_say("Esta cidade não existe mais." if mode == "treasury" else "Esta oficina não existe mais.")
		return
	if not response.ok:
		var why := "resposta fora do formato esperado" if response.status == 200 else "erro %d" % response.status
		_say("Não foi possível carregar (%s)." % why)
		warning.emit("Não foi possível carregar a economia (%s)." % why)
		return
	if mode == "treasury":
		_render_treasury(response.body)
	else:
		_render_trades(response.body)


func _render_treasury(body: Dictionary) -> void:
	balance = int(body.balance)
	_summary.text = "Saldo: %d" % balance
	var months: Array = body.months
	if months.is_empty():
		_say("Sem movimento no tesouro neste período.")
		return
	var scale := 1.0
	for entry in months:
		scale = maxf(scale, maxf(float(entry.revenue), float(entry.spending)))
	for entry in months:
		var row := _bar_row(month_label(int(entry.month)), float(entry.revenue), float(entry.spending), scale)
		_list.add_child(row.node)
		month_rows.append({"numbers": row.numbers, "label": row.label, "revenue": int(entry.revenue), "spending": int(entry.spending), "revenue_bar": row.revenue_bar, "spending_bar": row.spending_bar})


## Duas barras por mês, proporcionais ao maior valor da janela: verde = entrou, vermelho = saiu.
func _bar_row(label_text: String, revenue: float, spending: float, scale: float) -> Dictionary:
	var node := HBoxContainer.new()
	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size.x = 130
	node.add_child(label)
	var bars := VBoxContainer.new()
	node.add_child(bars)
	var revenue_bar := _bar(revenue, scale, Color("5fb36b"))
	var spending_bar := _bar(spending, scale, Color("d9534f"))
	bars.add_child(revenue_bar)
	bars.add_child(spending_bar)
	var numbers := Label.new()
	numbers.text = "+%d  −%d" % [int(revenue), int(spending)]
	node.add_child(numbers)
	return {"node": node, "numbers": numbers.text, "label": label_text, "revenue_bar": revenue_bar, "spending_bar": spending_bar}


func _bar(value: float, scale: float, color: Color) -> ProgressBar:
	var bar := ProgressBar.new()
	bar.max_value = scale
	bar.value = value
	bar.show_percentage = false
	bar.custom_minimum_size = Vector2(220, 10)
	var fill := StyleBoxFlat.new() # só o preenchimento colorido: o fundo da barra continua legível
	fill.bg_color = color
	fill.set_corner_radius_all(3)
	bar.add_theme_stylebox_override("fill", fill)
	return bar


func _render_trades(body: Dictionary) -> void:
	var days: Array = body.days
	if days.is_empty():
		_say("Sem vendas registradas neste período.")
		return
	var total := 0
	for entry in days:
		total += int(entry.value)
		var calendar: Dictionary = calendar_of.call()
		var when: String = date_of.call(int(entry.day) * int(calendar.get("hoursPerDay", 24)))
		var text := "%s · %s · %d un. por %d (%d compra%s)" % [when, resource_name_of.call(int(entry.resourceId)), int(entry.quantity), int(entry.value), int(entry.purchases), "" if int(entry.purchases) == 1 else "s"]
		var label := Label.new()
		label.text = text
		_list.add_child(label)
		trade_rows.append({"text": text, "value": int(entry.value)})
	_summary.text = "Total vendido: %d" % total


func _say(text: String) -> void:
	empty_text = text
	_empty.text = text
