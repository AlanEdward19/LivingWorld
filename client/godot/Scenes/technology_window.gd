extends PanelContainer
## Tecnologias que a cultura de um NPC descobriu (`GET /cultures/{id}/technologies`). Sem nome: o motor só entrega o vetor
## (ADR-0013), então cada tecnologia aparece pela chave e pelos eixos ritmo/carga/alcance em barras.

signal closed()
signal warning(text: String)

const AXES := [["pace", "Ritmo"], ["load", "Carga"], ["reach", "Alcance"]]

var api
var date_of: Callable = func(tick: int) -> String: return "tick %d" % tick
var culture_id := -1
var is_open := false
var technologies: Array = []
var rows: Array = [] ## [{title, detail, bars: Array}]
var empty_text := ""
var _title: Label
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
	_title.text = "Tecnologias da cultura"
	_title.add_theme_font_size_override("font_size", 20)
	_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_title)
	var close_button := Button.new()
	close_button.text = "Fechar (Esc)"
	close_button.pressed.connect(func(): close())
	header.add_child(close_button)
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
	_list.add_theme_constant_override("separation", 10)
	scroll.add_child(_list)


func show_culture(id: int) -> void:
	culture_id = id
	is_open = true
	visible = true
	_clear()
	_request += 1
	var mine := _request
	var response: Dictionary = await api.technologies(id)
	if mine != _request or not is_open:
		return
	_render(response)


func close() -> void:
	if not is_open:
		return
	is_open = false
	visible = false
	_request += 1
	closed.emit()


func _clear() -> void:
	technologies = []
	rows = []
	empty_text = ""
	_empty.text = ""
	for child in _list.get_children():
		_list.remove_child(child)
		child.queue_free()


func _render(response: Dictionary) -> void:
	_clear()
	if response.status == 404:
		_say("Esta cultura não existe mais.")
		return
	if not response.ok:
		var why := "resposta fora do formato esperado" if response.status == 200 else "erro %d" % response.status
		_say("Não foi possível carregar (%s)." % why)
		warning.emit("Não foi possível carregar as tecnologias (%s)." % why)
		return
	technologies = response.body
	if technologies.is_empty():
		_say("Esta cultura ainda não descobriu nenhuma tecnologia.")
		return
	var scale := 1.0
	for tech in technologies:
		for axis in tech.vector:
			scale = maxf(scale, float(axis))
	for tech in technologies:
		_list.add_child(_tech_node(tech, scale))


func _tech_node(tech: Dictionary, scale: float) -> Control:
	var box := VBoxContainer.new()
	var title := "Tecnologia %s" % String(tech.key)
	if bool(tech.isVariation):
		title += " (variação de %s)" % String(tech.variationOfKey)
	var head := Label.new()
	head.text = title
	head.add_theme_color_override("font_color", Color("ffe9a6"))
	box.add_child(head)
	var detail := "%s · descoberta em %s · adotada por %d cidade%s" % [
		String(tech.category), date_of.call(int(tech.discoveredAtTick)), int(tech.adoptedByCities), "" if int(tech.adoptedByCities) == 1 else "s"]
	var info := Label.new()
	info.text = detail
	info.modulate = Color(1, 1, 1, 0.8)
	box.add_child(info)
	var bars: Array = []
	var axis_labels: Array = []
	for i in AXES.size():
		var row := HBoxContainer.new()
		var name := Label.new()
		name.text = AXES[i][1]
		name.custom_minimum_size.x = 70
		row.add_child(name)
		var value := float(tech.vector[i]) if i < tech.vector.size() else 0.0
		var bar := ProgressBar.new()
		bar.max_value = scale
		bar.value = value
		bar.show_percentage = false
		bar.custom_minimum_size = Vector2(200, 12)
		bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		row.add_child(bar)
		var number := Label.new()
		number.text = str(int(value))
		row.add_child(number)
		box.add_child(row)
		bars.append(bar)
		axis_labels.append(name.text)
	rows.append({"title": title, "detail": detail, "bars": bars, "axes": axis_labels})
	return box


func _say(text: String) -> void:
	empty_text = text
	_empty.text = text
