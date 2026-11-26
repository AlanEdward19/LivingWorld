extends HBoxContainer
## AD-197t: o criador de personagem de verdade. Nome, sexo, idade, traços, ofício, saúde, dinheiro, família ou chegada de fora e
## aparência completa. O que não se escolhe o motor sorteia. Cada mudança vai ao motor (`/players/{id}/character/preview`), que
## valida e devolve a pessoa; a prévia e o resumo vêm dessa resposta, nunca da escolha local. Criar só vale com a prévia aceita.

const Preview := preload("res://Scenes/character_preview.gd")

signal created(npc_id: int, name: String, city_id: String)
signal warning(text: String)

const SEXES := [["", "Sortear"], ["Female", "Mulher"], ["Male", "Homem"]]
const ROLES := [["newcomer", "Recém-chegado de fora"], ["child", "Filho(a) de alguém da cidade"], ["grandchild", "Neto(a) de alguém da cidade"],
	["sibling", "Irmão(ã) de alguém da cidade"], ["nephew", "Sobrinho(a) de alguém da cidade"], ["uncle", "Tio(a) de alguém da cidade"],
	["cousin", "Primo(a) de alguém da cidade"], ["spouse", "Cônjuge de alguém da cidade"], ["relative", "Parente agregado a uma casa"]]
const BLOOD := ["child", "grandchild", "sibling", "nephew", "uncle", "cousin"] ## laços de sangue: a idade vem dos pais
const VIA_LABELS := {"grandchild": "Filho(a) de", "nephew": "Filho(a) do irmão", "uncle": "Irmão(ã) do pai/mãe", "cousin": "Filho(a) do tio(a)"}
const TRAITS := [["extroversion", "expansividade"], ["agreeableness", "amabilidade"], ["conscientiousness", "cuidado"],
	["emotionalStability", "serenidade"], ["openness", "curiosidade"], ["ambition", "ambição"], ["loyalty", "lealdade"],
	["altruism", "generosidade"], ["impulsivity", "impulsividade"], ["riskAversion", "cautela"]]
const GENES := [["skinTone", "Pele"], ["hairTone", "Cabelo"], ["hairCurl", "Ondulação"], ["build", "Corpo"], ["eyeTone", "Olhos"],
	["faceShape", "Rosto"]]
const REFRESH_DELAY_S := 0.35

var hud ## o HUD do jogador (sessão, prévia e criação)
var city_id := ""
var options: Dictionary = {} ## o que o motor oferece na cidade
var state: Dictionary = {} ## a pessoa que o motor validou na última prévia (vazio = recusada)
var message := ""
var seed_number := 1
var fields: Dictionary = {} ## chave -> {check, input}
var name_edit: LineEdit
var sex_select: OptionButton
var profession_select: OptionButton
var role_select: OptionButton
var family_select: OptionButton
var via_select: OptionButton ## por quem o laço passa (neto, sobrinho, tio, primo)
var origin_select: OptionButton
var outfit_select: OptionButton
var reroll_button: Button
var create_button: Button
var figure
var summary: Label
var _hint: Label
var _message: Label
var _timer: Timer
var _request := 0
var _loading := false
var _no_one_reason := "" ## por que ninguém serve para o laço escolhido (o primeiro motivo do motor)
var _families: Array = [] ## [{npc_id, label}] já filtradas pelo papel


func _init() -> void:
	add_theme_constant_override("separation", 12)
	var scroll := ScrollContainer.new() # a área central é baixa: o formulário rola dentro da janela
	scroll.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.custom_minimum_size = Vector2(430, 260)
	add_child(scroll)
	var form := VBoxContainer.new()
	form.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	form.add_theme_constant_override("separation", 5)
	scroll.add_child(form)

	name_edit = LineEdit.new()
	name_edit.placeholder_text = "Nome (em branco: o motor sorteia)"
	name_edit.max_length = 60
	name_edit.text_changed.connect(func(_t: String): _changed())
	form.add_child(name_edit)
	sex_select = _select(form, "Sexo", SEXES.map(func(p): return p[1]))
	_opt(form, "age", "Idade", 18, 90, 30, 1.0, false)
	profession_select = _select(form, "Ofício", ["Sortear"])
	_section(form, "Família")
	role_select = _select(form, "Lugar", ROLES.map(func(p): return p[1]))
	role_select.item_selected.connect(func(_i: int): _refill_families())
	origin_select = _select(form, "Veio de", ["De fora do mundo"])
	family_select = _select(form, "Parente", [])
	family_select.item_selected.connect(func(_i: int): _refill_vias())
	via_select = _select(form, "Por meio de", [])
	_hint = Label.new()
	_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_hint.modulate = Color(1, 1, 1, 0.7)
	form.add_child(_hint)
	_section(form, "Corpo e dinheiro")
	_opt(form, "health", "Saúde", 1, 100, 80, 1.0, true)
	_opt(form, "money", "Dinheiro inicial", 0, 500, 50, 1.0, false)
	_opt(form, "heightM", "Altura (m)", 1.4, 2.1, 1.7, 0.01, false)
	for pair in GENES:
		_opt(form, "look." + pair[0], pair[1], 0, 100, 50, 1.0, true)
	outfit_select = _select(form, "Roupa", ["Sortear"])
	_section(form, "Traços (os que ficarem desmarcados o motor sorteia)")
	for pair in TRAITS:
		_opt(form, "trait." + pair[0], String(pair[1]).capitalize(), 0, 100, 50, 1.0, true)

	var side := VBoxContainer.new()
	side.add_theme_constant_override("separation", 6)
	add_child(side)
	figure = Preview.new()
	side.add_child(figure)
	summary = Label.new()
	summary.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	summary.custom_minimum_size = Vector2(220, 0)
	side.add_child(summary)
	_message = Label.new()
	_message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_message.add_theme_color_override("font_color", Color("ffcf4a"))
	_message.custom_minimum_size = Vector2(220, 0)
	side.add_child(_message)
	reroll_button = Button.new()
	reroll_button.text = "Sortear de novo"
	reroll_button.tooltip_text = "O motor sorteia outra vez o que você deixou em branco"
	reroll_button.pressed.connect(func(): reroll())
	side.add_child(reroll_button)
	create_button = Button.new()
	create_button.text = "Criar e entrar"
	create_button.disabled = true
	create_button.pressed.connect(func(): create())
	side.add_child(create_button)
	_timer = Timer.new()
	_timer.one_shot = true
	_timer.wait_time = REFRESH_DELAY_S
	_timer.timeout.connect(func(): refresh())
	add_child(_timer)


func _section(parent: Control, text: String) -> void:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 15)
	parent.add_child(label)


func _select(parent: Control, label: String, items: Array) -> OptionButton:
	var row := HBoxContainer.new()
	var title := Label.new()
	title.text = label
	title.custom_minimum_size = Vector2(120, 0)
	row.add_child(title)
	var select := OptionButton.new()
	select.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for item in items:
		select.add_item(String(item))
	select.item_selected.connect(func(_i: int): _changed())
	row.add_child(select)
	parent.add_child(row)
	return select


## Uma linha opcional: marcada, o valor vai na escolha; desmarcada, o motor sorteia.
func _opt(parent: Control, key: String, label: String, lo: float, hi: float, value: float, step: float, slider: bool) -> void:
	var row := HBoxContainer.new()
	var check := CheckBox.new()
	check.text = label
	check.custom_minimum_size = Vector2(150, 0)
	row.add_child(check)
	var input = HSlider.new() if slider else SpinBox.new()
	input.min_value = lo
	input.max_value = hi
	input.step = step
	input.value = value
	input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	input.editable = false if input is SpinBox else true
	row.add_child(input)
	check.toggled.connect(func(on: bool):
		if input is SpinBox:
			input.editable = on
		_changed())
	input.value_changed.connect(func(_v: float):
		if check.button_pressed:
			_changed())
	parent.add_child(row)
	fields[key] = {"check": check, "input": input}


## Marca o campo e dá o valor (a tela do jogador e os testes).
func set_field(key: String, value: float) -> void:
	var field: Dictionary = fields[key]
	field.input.value = value
	field.check.button_pressed = true


func _on(key: String) -> bool:
	return fields[key].check.button_pressed


func _value(key: String) -> float:
	return float(fields[key].input.value)


## Abre a cidade: pergunta ao motor o que ela oferece (faixas, teto de dinheiro, ofícios, trajes, famílias) e faz a primeira prévia.
func open_city(id: String) -> void:
	city_id = id
	_request += 1
	var mine := _request
	var response: Dictionary = await hud.api.player_city_options(id)
	if mine != _request:
		return
	if not response.ok:
		options = {}
		_show_message(_reason(response))
		_set_state({})
		return
	_loading = true
	options = response.body
	_fill_options()
	_loading = false
	await refresh()


func _fill_options() -> void:
	profession_select.clear()
	profession_select.add_item("Sortear")
	profession_select.set_item_metadata(0, -1)
	for p in options.get("professions", []):
		profession_select.add_item(String(p.name) if String(p.name) != "" else "ofício nº %d" % int(p.id))
		profession_select.set_item_metadata(profession_select.item_count - 1, int(p.id))
	outfit_select.clear()
	outfit_select.add_item("Sortear")
	for name in options.get("outfits", []):
		outfit_select.add_item(String(name))
	origin_select.clear()
	origin_select.add_item("De fora do mundo")
	origin_select.set_item_metadata(0, "")
	for origin in options.get("origins", []):
		origin_select.add_item("De %s" % String(origin.name))
		origin_select.set_item_metadata(origin_select.item_count - 1, String(origin.cityId))
	var age: Range = fields.age.input
	age.min_value = int(options.get("ageMin", 18))
	age.max_value = int(options.get("ageMax", 90))
	var money: Range = fields.money.input
	money.max_value = int(options.get("moneyCap", 0))
	money.value = int(options.get("typicalMoney", 0))
	fields.money.check.disabled = not bool(options.get("economy", true))
	_set_height_range()
	_refill_families()


func _set_height_range() -> void:
	var range_of: Dictionary = options.get("heightM", {}).get("male" if sex_select.selected == 2 else "female", {})
	if range_of.is_empty():
		return
	var input: Range = fields.heightM.input
	input.min_value = float(range_of.min)
	input.max_value = float(range_of.max)
	if not fields.heightM.check.button_pressed:
		input.value = float(range_of.typical)


## Os parentes que o papel escolhido admite (o motor diz quem e em que idade); só eles entram na lista.
func _refill_families() -> void:
	var kind := _role_kind()
	_families.clear()
	family_select.clear()
	var newcomer := kind == "newcomer"
	family_select.get_parent().visible = not newcomer
	origin_select.get_parent().visible = newcomer
	var first_reason := ""
	if not newcomer:
		for family in options.get("families", []):
			for member in family.members:
				var role: Dictionary = member.roles.get(kind, {})
				if not bool(role.get("available", false)):
					if first_reason == "":
						first_reason = String(role.get("reason", ""))
					continue
				var label := "%s, %d anos (casa de %s)" % [member.name, int(member.ageYears), family.head]
				family_select.add_item(label)
				family_select.set_item_metadata(family_select.item_count - 1, int(member.npcId))
				_families.append({"npc_id": int(member.npcId), "label": label, "min": int(role.minAge), "max": int(role.maxAge),
					"vias": role.get("vias", [])})
	_no_one_reason = first_reason
	_refill_vias()


## Nos laços que passam por alguém, a lista de quem serve de passagem para o parente escolhido (e a faixa de idade de cada um).
func _refill_vias() -> void:
	var kind := _role_kind()
	via_select.clear()
	via_select.get_parent().visible = kind in VIA_LABELS and not _families.is_empty()
	if kind in VIA_LABELS:
		for via in _anchor().get("vias", []):
			via_select.add_item("%s, %d anos" % [via.name, int(via.ageYears)])
			via_select.set_item_metadata(via_select.item_count - 1, int(via.npcId))
	_update_hint()
	_changed()


func _update_hint() -> void:
	var kind := _role_kind()
	if kind == "newcomer":
		_hint.text = "Chega sem casa e sem família na cidade, como um migrante."
	elif _families.is_empty():
		_hint.text = "Ninguém na cidade permite esse laço agora: %s" % (_no_one_reason if _no_one_reason != "" else "o motor não lista quem não pode.")
	elif kind in BLOOD:
		var window := _window()
		var age: Range = fields.age.input
		age.min_value = maxi(int(options.get("ageMinMinor", 6)), int(window.min))
		age.max_value = mini(int(options.get("ageMax", 90)), int(window.max))
		_hint.text = "Com esse laço a idade vai de %d a %d anos (os pais precisam poder ter gerado você). Menor de idade mora com quem cuida, sem ofício nem dinheiro." % [window.min, window.max]
	elif kind == "spouse":
		_hint.text = "O parente precisa estar livre e aceitar: o motor recusa se não aceitar."
	else:
		_hint.text = "Entra na casa dele como agregado, sem laço de sangue."
	if kind not in BLOOD:
		var age_range: Range = fields.age.input
		age_range.min_value = int(options.get("ageMin", 18))
		age_range.max_value = int(options.get("ageMax", 90))


## A faixa de idade do laço: a do parente de passagem escolhido, ou a do parente.
func _window() -> Dictionary:
	var picked := _anchor()
	var index := via_select.selected
	var vias: Array = picked.get("vias", [])
	if _role_kind() in VIA_LABELS and index >= 0 and index < vias.size():
		return {"min": int(vias[index].minAge), "max": int(vias[index].maxAge)}
	return {"min": int(picked.min), "max": int(picked.max)}


func _anchor() -> Dictionary:
	var index := family_select.selected
	return _families[index] if index >= 0 and index < _families.size() else {"npc_id": -1, "min": 0, "max": 0}


func _role_kind() -> String:
	return String(ROLES[max(0, role_select.selected)][0])


## A escolha inteira, só com o que o jogador marcou; o resto o motor sorteia.
func spec() -> Dictionary:
	var result := {"cityId": city_id, "seed": seed_number}
	var name := name_edit.text.strip_edges()
	if name != "":
		result["name"] = name
	if sex_select.selected > 0:
		result["sex"] = SEXES[sex_select.selected][0]
	if _on("age"):
		result["ageYears"] = int(_value("age"))
	var profession = profession_select.get_item_metadata(max(0, profession_select.selected))
	if profession != null and int(profession) >= 0:
		result["professionId"] = int(profession)
	if _on("health"):
		result["health"] = int(_value("health"))
	if _on("money"):
		result["money"] = int(_value("money"))
	var traits := {}
	for pair in TRAITS:
		if _on("trait." + pair[0]):
			traits[pair[0]] = int(_value("trait." + pair[0]))
	if not traits.is_empty():
		result["traits"] = traits
	var look := {}
	if _on("heightM"):
		look["heightM"] = roundf(_value("heightM") * 100.0) / 100.0
	for pair in GENES:
		if _on("look." + pair[0]):
			look[pair[0]] = int(_value("look." + pair[0]))
	if outfit_select.selected > 0:
		look["outfit"] = outfit_select.selected - 1
	if not look.is_empty():
		result["look"] = look
	var kind := _role_kind()
	var role := {"kind": kind}
	if kind == "newcomer":
		var origin = origin_select.get_item_metadata(max(0, origin_select.selected))
		if origin != null and String(origin) != "":
			role["fromCityId"] = String(origin)
	elif _anchor().npc_id >= 0:
		role["relativeId"] = int(_anchor().npc_id)
		if kind in VIA_LABELS and via_select.selected >= 0:
			role["viaId"] = int(via_select.get_item_metadata(via_select.selected))
	result["role"] = role
	return result


func _changed() -> void:
	if _loading or not is_inside_tree() or _timer == null:
		return
	_set_height_range()
	_update_hint()
	_timer.start()


## Pede ao motor que valide a escolha de agora e devolva a pessoa: a prévia, o resumo e o botão de criar vêm dela.
func refresh() -> void:
	if city_id == "":
		return
	_set_height_range()
	_request += 1
	var mine := _request
	var response: Dictionary = await hud.character_preview(spec())
	if mine != _request:
		return
	if not response.ok:
		_set_state({})
		_show_message(_reason(response))
		return
	_show_message("")
	_set_state(response.body.character)


func _set_state(character: Dictionary) -> void:
	state = character
	create_button.disabled = character.is_empty()
	if character.is_empty():
		figure.clear()
		summary.text = ""
		return
	figure.show_character(character)
	summary.text = describe(character)


## O resumo da pessoa como o motor a validou (cada linha é um campo da resposta).
static func describe(c: Dictionary) -> String:
	var sex := "Mulher" if String(c.sex) == "Female" else "Homem"
	var look: Dictionary = c.look
	var lines: Array[String] = ["%s · %s, %d anos" % [c.name, sex, int(c.ageYears)]]
	var profession := String(c.get("profession", ""))
	lines.append("Ofício: %s" % (profession if profession != "" else "nº %d" % int(c.professionId)))
	lines.append("%s · %.2f m · %.0f kg" % [String(c.get("outfitName", "")), float(look.heightM), float(look.weightKg)])
	lines.append("Saúde %d · Dinheiro %d" % [int(c.health), int(c.money)])
	lines.append(_role_text(c))
	return "\n".join(lines)


static func _role_text(c: Dictionary) -> String:
	var role: Dictionary = c.role
	var who := String(role.get("relativeName", ""))
	match String(role.kind):
		"child":
			return "Filho(a) de %s" % who
		"sibling":
			return "Irmão(ã) de %s" % who
		"spouse":
			return "Casa com %s" % who
		"relative":
			return "Agregado à casa de %s" % who
	return "Recém-chegado a %s" % String(c.cityName)


func reroll() -> void:
	seed_number += 1
	await refresh()


## Cria e entra. Recusa do motor vira aviso e a tela continua aberta.
func create() -> void:
	if state.is_empty():
		return
	var reply: Dictionary = await hud.create_character(spec())
	if reply.has("error"):
		var error := _strip(String(reply.error))
		_show_message(error)
		warning.emit(error)
		return
	created.emit(int(reply.npcId), String(reply.character.name), String(reply.cityId))


func _reason(response: Dictionary) -> String:
	var error := String(response.body.get("error", "")) if response.body is Dictionary else ""
	return _strip(error) if error != "" else "O motor não respondeu (erro %d)." % response.status


static func _strip(error: String) -> String:
	for prefix in ["conflict: recusa: ", "conflict: ", "not_found: "]:
		if error.begins_with(prefix):
			return error.substr(prefix.length())
	return error


func _show_message(text: String) -> void:
	message = text
	_message.text = text
	_message.visible = text != ""
