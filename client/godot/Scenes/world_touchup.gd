extends Control
## Retoques do mundo recém-criado: escolhe um NPC e reescreve a personalidade (10 traços) ou concede um poder
## do catálogo do mundo. Usa só as rotas de autoria do servidor; o motor valida e registra no log causal.

signal finished()

const TRAITS := [
	["extroversion", "Extroversão"], ["agreeableness", "Amabilidade"], ["conscientiousness", "Disciplina"],
	["emotionalStability", "Estabilidade"], ["openness", "Abertura"], ["ambition", "Ambição"],
	["loyalty", "Lealdade"], ["altruism", "Altruísmo"], ["impulsivity", "Impulsividade"], ["riskAversion", "Aversão a risco"],
]

var api
var npc_ids: Array[int] = []
var selected := -1
var npc_list: ItemList
var sliders: Dictionary = {}
var power_pick: OptionButton
var power_ids: Array[String] = []
var message: Label
var apply_button: Button
var grant_button: Button


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var shade := ColorRect.new()
	shade.color = Color("0b1020")
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(shade)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	var box := VBoxContainer.new()
	box.custom_minimum_size.x = 820
	box.add_theme_constant_override("separation", 10)
	center.add_child(box)
	var title := Label.new()
	title.text = "Retoques do mundo"
	title.add_theme_font_size_override("font_size", 34)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 16)
	box.add_child(row)
	npc_list = ItemList.new()
	npc_list.custom_minimum_size = Vector2(260, 360)
	npc_list.item_selected.connect(func(i: int): select(npc_ids[i]))
	row.add_child(npc_list)
	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(right)
	for entry in TRAITS:
		var line := HBoxContainer.new()
		right.add_child(line)
		var label := Label.new()
		label.text = entry[1]
		label.custom_minimum_size.x = 130
		line.add_child(label)
		var slider := HSlider.new()
		slider.min_value = 0
		slider.max_value = 100
		slider.step = 1
		slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		line.add_child(slider)
		sliders[entry[0]] = slider
	apply_button = Button.new()
	apply_button.text = "Aplicar personalidade"
	apply_button.disabled = true
	apply_button.pressed.connect(apply_personality)
	right.add_child(apply_button)
	var power_row := HBoxContainer.new()
	right.add_child(power_row)
	power_pick = OptionButton.new()
	power_pick.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	power_row.add_child(power_pick)
	grant_button = Button.new()
	grant_button.text = "Conceder poder"
	grant_button.disabled = true
	grant_button.pressed.connect(grant_power)
	power_row.add_child(grant_button)
	message = Label.new()
	message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(message)
	var play := Button.new()
	play.text = "Começar a jogar"
	play.custom_minimum_size.y = 46
	play.pressed.connect(func(): finished.emit())
	box.add_child(play)


func load_npcs() -> void:
	var page: Dictionary = await api.npc_page("", 0, 200)
	if page.ok:
		for item in page.body.items:
			npc_ids.append(int(item.id.value))
			npc_list.add_item(String(item.name))
	var catalog: Dictionary = await api.power_catalog()
	power_ids.clear()
	power_pick.clear()
	if catalog.ok:
		for descriptor in catalog.body:
			power_ids.append(String(descriptor.get("id", descriptor.get("powerId", ""))))
			power_pick.add_item(String(descriptor.get("name", power_ids.back())))
	if power_ids.is_empty():
		power_pick.add_item("Este mundo não define poderes")
		power_pick.disabled = true
	if not npc_ids.is_empty():
		npc_list.select(0)
		await select(npc_ids[0])


func select(id: int) -> void:
	selected = id
	var detail: Dictionary = await api.npc_detail(id)
	if not detail.ok or selected != id:
		return
	for entry in TRAITS:
		sliders[entry[0]].value = float(detail.body.personality.get(entry[0], 50))
	apply_button.disabled = false
	grant_button.disabled = power_ids.is_empty()
	message.text = ""


func personality_values() -> Dictionary:
	var values := {}
	for entry in TRAITS:
		values[entry[0]] = int(sliders[entry[0]].value)
	return values


func apply_personality() -> void:
	var response: Dictionary = await api.rewrite_personality(selected, personality_values())
	message.text = "Personalidade aplicada." if response.ok else "Recusado: %s" % str(response.body)


func grant_power() -> void:
	if power_ids.is_empty():
		return
	var response: Dictionary = await api.grant_power(selected, power_ids[power_pick.selected])
	message.text = "Poder concedido." if response.ok else "Recusado: %s" % str(response.body)
