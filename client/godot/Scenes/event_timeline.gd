extends PanelContainer
## Linha do tempo dos acontecimentos recentes (ADR-0047 etapa 3): o mais novo em cima, cada linha clicável (leva a câmera
## ao lugar e abre a ficha do evento), "Próximo evento" (N) e "Assistir" (V: a câmera acompanha sozinha). Crime só de
## registro aparece como registro estruturado, nunca com o texto narrado (o título vem de `registry.event_title`).

signal event_chosen(id: int)
signal next_requested()
signal watch_toggled(on: bool)

const SHOWN := 10

var registry
var date_of: Callable = func(tick: int) -> String: return "tick %d" % tick
var rows: Array = [] ## ids mostrados agora, na ordem (testável)
var next_button: Button
var watch_button: Button
var _list: VBoxContainer
var _collapse: Button


func _init() -> void:
	custom_minimum_size = Vector2(360, 0)
	var box := VBoxContainer.new()
	add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	_collapse = Button.new()
	_collapse.text = "▾ Acontecimentos"
	_collapse.flat = true
	_collapse.pressed.connect(func(): _list.visible = not _list.visible; _collapse.text = ("▾" if _list.visible else "▸") + " Acontecimentos")
	header.add_child(_collapse)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(spacer)
	next_button = Button.new()
	next_button.text = "Próximo (N)"
	next_button.tooltip_text = "Pula para o próximo evento: câmera no lugar e ficha aberta"
	next_button.pressed.connect(func(): next_requested.emit())
	header.add_child(next_button)
	watch_button = Button.new()
	watch_button.text = "Assistir (V)"
	watch_button.toggle_mode = true
	watch_button.tooltip_text = "A câmera acompanha sozinha o que está acontecendo"
	watch_button.toggled.connect(func(on: bool): watch_toggled.emit(on))
	header.add_child(watch_button)
	_list = VBoxContainer.new()
	box.add_child(_list)


func set_watching(on: bool) -> void:
	watch_button.set_pressed_no_signal(on)


## Refaz as linhas a partir do registro (só quando chega evento; nada por quadro).
func refresh() -> void:
	var order: Array = registry.event_order
	var wanted: Array = []
	for i in range(order.size() - 1, maxi(order.size() - 1 - SHOWN, -1), -1):
		wanted.append(int(order[i]))
	if wanted == rows and _list.get_child_count() == wanted.size():
		visible = not wanted.is_empty() or watch_button.button_pressed
		return
	rows = wanted
	# AD-189: com o mundo rodando chega evento a quase todo delta; as linhas são reaproveitadas (só o texto muda) em vez de
	# 10 botões novos por delta
	while _list.get_child_count() > wanted.size():
		var extra := _list.get_child(_list.get_child_count() - 1)
		_list.remove_child(extra)
		extra.queue_free()
	while _list.get_child_count() < wanted.size():
		var index := _list.get_child_count()
		var button := Button.new()
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.clip_text = true
		button.custom_minimum_size.x = 380
		button.pressed.connect(func(): if index < rows.size(): event_chosen.emit(int(rows[index])))
		_list.add_child(button)
	for i in wanted.size():
		var event: Dictionary = registry.events.get(wanted[i], {})
		# #222: o que aconteceu primeiro (a data comprida cortava o título); a data inteira fica na dica do mouse
		var when: String = date_of.call(int(event.get("tick", 0)))
		var text := "%s%s · %s" % [registry.event_title(event), "" if bool(event.get("has_cell", false)) else " (sem lugar)",
			when.get_slice(" · ", when.get_slice_count(" · ") - 1)]
		var button := _list.get_child(i) as Button
		if button.text != text:
			button.text = text
			button.tooltip_text = "%s · %s" % [when, registry.event_title(event)]
	visible = not wanted.is_empty() or watch_button.button_pressed


func row_text(index: int) -> String:
	return (_list.get_child(index) as Button).text if index < _list.get_child_count() else ""
