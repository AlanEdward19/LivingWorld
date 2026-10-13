extends Control
## Criar famílias e pessoas (estilo "criar um sim"): famílias à esquerda, a pessoa escolhida à direita
## (nome, sexo, idade, profissão, personalidade, parentesco). Só monta dados; o cenário final é gerado pelo
## WorldEditor e validado pelo servidor.

signal done(families: Array)
signal cancelled()

const WorldEditor := preload("res://Scenes/world_editor.gd")
const TRAIT_LABELS := ["Extroversão", "Amabilidade", "Disciplina", "Estabilidade", "Abertura", "Ambição", "Lealdade", "Altruísmo", "Impulsividade", "Aversão a risco"]

var families: Array = []
var professions: Dictionary = {} ## id (int) -> nome
var settlements: Array[String] = []
var family_index := 0
var member_index := 0

var tree_list: ItemList
var name_edit: LineEdit
var sex_pick: OptionButton
var age_spin: SpinBox
var profession_pick: OptionButton
var settlement_pick: OptionButton
var spouse_pick: OptionButton
var mother_pick: OptionButton
var father_pick: OptionButton
var sliders: Array[HSlider] = []
var custom_personality: CheckBox
var message: Label
var _rows: Array = [] ## linhas da lista: [família, membro]
var _loading := false
var _rng := RandomNumberGenerator.new()


func _ready() -> void:
	_rng.randomize()
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var shade := ColorRect.new()
	shade.color = Color("0b1020")
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(shade)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	var box := VBoxContainer.new()
	box.custom_minimum_size.x = 980
	box.add_theme_constant_override("separation", 10)
	center.add_child(box)
	var title := Label.new()
	title.text = "Criar famílias"
	title.add_theme_font_size_override("font_size", 34)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 16)
	box.add_child(row)

	var left := VBoxContainer.new()
	left.custom_minimum_size.x = 300
	row.add_child(left)
	tree_list = ItemList.new()
	tree_list.custom_minimum_size = Vector2(300, 380)
	tree_list.item_selected.connect(_on_row_selected)
	left.add_child(tree_list)
	_button("+ Família", left, add_family)
	_button("+ Pessoa nesta família", left, add_member)
	_button("Remover pessoa", left, remove_member)

	var right := VBoxContainer.new()
	right.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	right.add_theme_constant_override("separation", 4)
	row.add_child(right)
	name_edit = _line("Nome", right, LineEdit.new())
	name_edit.text_changed.connect(func(_t): _store())
	sex_pick = _line("Sexo", right, OptionButton.new())
	sex_pick.add_item("Mulher")
	sex_pick.add_item("Homem")
	sex_pick.item_selected.connect(func(_i): _store())
	age_spin = _line("Idade", right, SpinBox.new())
	age_spin.min_value = 0
	age_spin.max_value = 120
	age_spin.value_changed.connect(func(_v): _store())
	profession_pick = _line("Profissão", right, OptionButton.new())
	profession_pick.item_selected.connect(func(_i): _store())
	settlement_pick = _line("Mora em", right, OptionButton.new())
	settlement_pick.item_selected.connect(func(_i): _store())
	spouse_pick = _line("Cônjuge", right, OptionButton.new())
	spouse_pick.item_selected.connect(func(_i): _store())
	mother_pick = _line("Mãe", right, OptionButton.new())
	mother_pick.item_selected.connect(func(_i): _store())
	father_pick = _line("Pai", right, OptionButton.new())
	father_pick.item_selected.connect(func(_i): _store())
	custom_personality = CheckBox.new()
	custom_personality.text = "Personalidade própria (desmarcado = sorteada pelo mundo)"
	custom_personality.toggled.connect(func(_on): _store())
	right.add_child(custom_personality)
	for i in 10:
		var line := HBoxContainer.new()
		right.add_child(line)
		var label := Label.new()
		label.text = TRAIT_LABELS[i]
		label.custom_minimum_size.x = 130
		line.add_child(label)
		var slider := HSlider.new()
		slider.min_value = 0
		slider.max_value = 100
		slider.step = 1
		slider.value = 50
		slider.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		slider.value_changed.connect(func(_v): _store())
		line.add_child(slider)
		sliders.append(slider)
	_button("Sortear personalidade", right, roll_personality)

	message = Label.new()
	message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	message.modulate = Color("e08a8a")
	box.add_child(message)
	var actions := HBoxContainer.new()
	actions.add_theme_constant_override("separation", 12)
	box.add_child(actions)
	_button("Cancelar", actions, func(): cancelled.emit()).size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_button("Concluir", actions, finish).size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_refresh()


func _button(text: String, parent: Control, action: Callable) -> Button:
	var button := Button.new()
	button.text = text
	button.pressed.connect(action)
	parent.add_child(button)
	return button


func _line(label_text: String, parent: Control, control: Control) -> Control:
	var line := HBoxContainer.new()
	parent.add_child(line)
	var label := Label.new()
	label.text = label_text
	label.custom_minimum_size.x = 130
	line.add_child(label)
	control.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	line.add_child(control)
	return control


## Define o catálogo (profissões e assentamentos) e as famílias de partida; chamado antes de mostrar.
func setup(p_families: Array, p_professions: Dictionary, p_settlements: Array[String]) -> void:
	families = p_families.duplicate(true)
	professions = p_professions
	settlements = p_settlements
	family_index = 0
	member_index = 0
	if is_node_ready():
		_refresh()


func new_member(adult: bool = true) -> Dictionary:
	var profession := int(professions.keys()[0]) if not professions.is_empty() else 0
	return {"name": "Pessoa %d" % (_member_total() + 1), "sex": "Female", "age": 28 if adult else 6,
		"profession": profession, "personality": null, "spouse": -1, "mother": -1, "father": -1}


func _member_total() -> int:
	var total := 0
	for family in families:
		total += family.members.size()
	return total


func add_family() -> void:
	families.append({"settlement": "", "members": [new_member()]})
	family_index = families.size() - 1
	member_index = 0
	_refresh()


func add_member() -> void:
	if families.is_empty():
		add_family()
		return
	families[family_index].members.append(new_member(false))
	member_index = families[family_index].members.size() - 1
	_refresh()


func remove_member() -> void:
	if families.is_empty():
		return
	var members: Array = families[family_index].members
	members.remove_at(member_index)
	for person in members: # índices de parentesco acima do removido descem; o que apontava para ele zera
		for link in ["spouse", "mother", "father"]:
			if person[link] == member_index:
				person[link] = -1
			elif person[link] > member_index:
				person[link] -= 1
	if members.is_empty():
		families.remove_at(family_index)
	family_index = clampi(family_index, 0, maxi(families.size() - 1, 0))
	member_index = 0
	_refresh()


func roll_personality() -> void:
	if families.is_empty():
		return
	custom_personality.set_pressed_no_signal(true)
	for slider in sliders:
		slider.set_value_no_signal(_rng.randi_range(10, 90))
	_store()


func _on_row_selected(index: int) -> void:
	family_index = _rows[index][0]
	member_index = _rows[index][1]
	_load_form()


func _current() -> Dictionary:
	return families[family_index].members[member_index] if not families.is_empty() else {}


## Lista à esquerda e formulário à direita a partir do estado.
func _refresh() -> void:
	tree_list.clear()
	_rows.clear()
	for f in families.size():
		tree_list.add_item("Família %d" % (f + 1))
		tree_list.set_item_disabled(tree_list.item_count - 1, true)
		_rows.append([f, -1])
		for m in families[f].members.size():
			tree_list.add_item("   %s (%d)" % [families[f].members[m].name, int(families[f].members[m].age)])
			_rows.append([f, m])
			if f == family_index and m == member_index:
				tree_list.select(tree_list.item_count - 1)
	_load_form()


func _load_form() -> void:
	_loading = true
	var has_person := not families.is_empty()
	for control in [name_edit, sex_pick, age_spin, profession_pick, settlement_pick, spouse_pick, mother_pick, father_pick, custom_personality]:
		if control is LineEdit or control is SpinBox:
			control.set("editable", has_person)
		else:
			control.set("disabled", not has_person)
	if has_person:
		var person := _current()
		name_edit.text = person.name
		sex_pick.select(0 if person.sex == "Female" else 1)
		age_spin.set_value_no_signal(person.age)
		profession_pick.clear()
		for id in professions:
			profession_pick.add_item(String(professions[id]), int(id))
		profession_pick.select(maxi(profession_pick.get_item_index(int(person.profession)), 0))
		settlement_pick.clear()
		settlement_pick.add_item("Vila inicial")
		for settlement_name in settlements:
			settlement_pick.add_item(settlement_name)
		var current_settlement := String(families[family_index].settlement)
		settlement_pick.select(maxi(settlements.find(current_settlement) + 1, 0))
		_fill_relation(spouse_pick, person.spouse)
		_fill_relation(mother_pick, person.mother)
		_fill_relation(father_pick, person.father)
		custom_personality.set_pressed_no_signal(person.personality is Dictionary)
		for i in 10:
			sliders[i].set_value_no_signal(float(person.personality.get(WorldEditor.TRAITS[i], 50)) if person.personality is Dictionary else 50.0)
	_loading = false


## "Ninguém" + os outros membros da mesma família (o item guarda o índice do membro).
func _fill_relation(pick: OptionButton, current: int) -> void:
	pick.clear()
	pick.add_item("Ninguém", 0)
	var members: Array = families[family_index].members
	for m in members.size():
		if m != member_index:
			pick.add_item(String(members[m].name), m + 1)
	pick.select(maxi(pick.get_item_index(current + 1), 0))


## Formulário -> estado (a cada edição).
func _store() -> void:
	if _loading or families.is_empty():
		return
	var person := _current()
	person.name = name_edit.text
	person.sex = "Female" if sex_pick.selected == 0 else "Male"
	person.age = int(age_spin.value)
	if profession_pick.selected >= 0:
		person.profession = profession_pick.get_item_id(profession_pick.selected)
	families[family_index].settlement = "" if settlement_pick.selected <= 0 else settlements[settlement_pick.selected - 1]
	_apply_relation(person, "spouse", spouse_pick)
	_apply_relation(person, "mother", mother_pick)
	_apply_relation(person, "father", father_pick)
	if custom_personality.button_pressed:
		var traits := {}
		for i in 10:
			traits[WorldEditor.TRAITS[i]] = int(sliders[i].value)
		person.personality = traits
	else:
		person.personality = null


## Cônjuge é recíproco: marcar um lado marca o outro (e desfaz o par anterior).
func _apply_relation(person: Dictionary, key: String, pick: OptionButton) -> void:
	var members: Array = families[family_index].members
	var chosen: int = pick.get_item_id(pick.selected) - 1
	if key == "spouse":
		for other in members:
			if other.spouse == member_index:
				other.spouse = -1
		if person.spouse >= 0 and person.spouse < members.size():
			members[person.spouse].spouse = -1
		person.spouse = chosen
		if chosen >= 0:
			members[chosen].spouse = member_index
	else:
		person[key] = chosen


## Mesmas regras que o servidor aplica ao carregar o cenário, para avisar antes; "" se tudo certo.
func validation_error() -> String:
	for f in families.size():
		var members: Array = families[f].members
		if not members.any(func(p: Dictionary) -> bool: return int(p.age) >= 18):
			return "Família %d precisa de ao menos um adulto." % (f + 1)
		for i in members.size():
			var person: Dictionary = members[i]
			if String(person.name).strip_edges() == "":
				return "Família %d: toda pessoa precisa de nome." % (f + 1)
			for link in [["mother", "Female", "Mãe"], ["father", "Male", "Pai"]]:
				var parent_index: int = person[link[0]]
				if parent_index >= 0 and (members[parent_index].sex != link[1] or members[parent_index].age <= person.age):
					return "%s: %s precisa ser mais %s e do sexo certo." % [person.name, link[2], "velha" if link[1] == "Female" else "velho"]
	return ""


func finish() -> void:
	message.text = validation_error()
	if message.text == "":
		done.emit(families)
