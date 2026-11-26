extends VBoxContainer
## AD-197t / #222: o criador de personagem em etapas (1 Origem, 2 Identidade, 3 Aparência, 4 Atributos, 5 Revisar). Nome, sexo, idade,
## traços, ofício, saúde, dinheiro, família ou chegada de fora e aparência completa. O que não se escolhe o motor sorteia. Cada mudança
## vai ao motor (`/players/{id}/character/preview`, com atraso de ~0,4 s ou ao trocar de etapa), que valida e devolve a pessoa; a prévia,
## o resumo e o que as barras mostram do que ficou em branco vêm dessa resposta, nunca da escolha local. Criar só vale com a prévia aceita.

const Preview := preload("res://Scenes/character_preview.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const MenuKit := preload("res://Scenes/menu/menu_kit.gd")

signal created(npc_id: int, name: String, city_id: String)
signal warning(text: String)

const STEPS := ["Origem", "Identidade", "Aparência", "Atributos", "Revisar"]
const SEXES := [["", "Sortear"], ["Female", "Mulher"], ["Male", "Homem"]]
const ROLES := [["newcomer", "Recém-chegado de fora"], ["child", "Filho(a) de alguém da cidade"], ["grandchild", "Neto(a) de alguém da cidade"],
	["sibling", "Irmão(ã) de alguém da cidade"], ["nephew", "Sobrinho(a) de alguém da cidade"], ["uncle", "Tio(a) de alguém da cidade"],
	["cousin", "Primo(a) de alguém da cidade"], ["spouse", "Cônjuge de alguém da cidade"], ["relative", "Parente agregado a uma casa"]]
const BLOOD := ["child", "grandchild", "sibling", "nephew", "uncle", "cousin"] ## laços de sangue: a idade vem dos pais
const VIA_LABELS := {"grandchild": "Filho(a) de", "nephew": "Filho(a) do irmão", "uncle": "Irmão(ã) do pai/mãe", "cousin": "Filho(a) do tio(a)"}
const TRAITS := [["extroversion", "expansividade"], ["agreeableness", "amabilidade"], ["conscientiousness", "cuidado"],
	["emotionalStability", "serenidade"], ["openness", "curiosidade"], ["ambition", "ambição"], ["loyalty", "lealdade"],
	["altruism", "generosidade"], ["impulsivity", "impulsividade"], ["riskAversion", "cautela"]]
const GENES := [["skinTone", "Tom da pele"], ["hairTone", "Cor do cabelo"], ["hairCurl", "Ondulação do cabelo"], ["build", "Corpo"],
	["eyeTone", "Cor dos olhos"], ["faceShape", "Formato do rosto"]]
const WORDS := {
	"look.skinTone": ["muito clara", "clara", "morena", "escura", "muito escura"],
	"look.hairTone": ["loiro claro", "loiro", "castanho", "castanho escuro", "preto"],
	"look.hairCurl": ["liso", "quase liso", "ondulado", "cacheado", "crespo"],
	"look.build": ["magro", "esguio", "médio", "forte", "robusto"],
	"look.eyeTone": ["azuis", "verdes", "cor de mel", "castanhos", "muito escuros"],
	"look.faceShape": ["fino", "oval", "médio", "quadrado", "largo"],
}
const TRAIT_WORDS := ["muito baixa", "baixa", "média", "alta", "muito alta"]
const GROUPS := [["Pele", ["look.skinTone"]], ["Cabelo", ["look.hairTone", "look.hairCurl"]], ["Rosto", ["look.faceShape", "look.eyeTone"]],
	["Corpo", ["look.build", "heightM"]]]
const REFRESH_DELAY_S := 0.4
const GOLD := Color("ffd27a")

var hud ## o HUD do jogador (sessão, prévia e criação)
var city_id := ""
var options: Dictionary = {} ## o que o motor oferece na cidade
var state: Dictionary = {} ## a pessoa que o motor validou na última prévia (vazio = recusada)
var message := ""
var error_step := -1 ## a etapa que causou o erro do motor (-1 = nenhum)
var step := 0
var reached := 0 ## a etapa mais avançada já visitada (as anteriores a ela se abrem pelo topo)
var seed_number := 1
var fields: Dictionary = {} ## chave -> {check, input, label, swatch, known}
var name_edit: LineEdit
var sex_select: OptionButton
var profession_select: OptionButton
var role_select: OptionButton
var family_select: OptionButton
var via_select: OptionButton ## por quem o laço passa (neto, sobrinho, tio, primo)
var origin_select: OptionButton
var outfit_select: OptionButton
var city_slot: VBoxContainer ## onde a janela põe a escolha da cidade
var arrive_buttons: Array[Button] = []
var step_buttons: Array[Button] = []
var back_button: Button
var next_button: Button ## "Avançar"; na última etapa vira "Criar e entrar"
var create_button: Button ## o mesmo botão do rodapé (só vale criar com a prévia aceita)
var attrs_toggle: Button
var reroll_button: Button
var figure
var summary: Label
var _hint: Label
var _message: Label ## o aviso do rodapé
var _banners: Array[Label] = [] ## o erro do motor na página da etapa que o causou
var _pages: Array[Control] = []
var _attrs_box: VBoxContainer
var _attrs_note: Label
var _review: Label
var _age_range: Label
var _money_range: Label
var _newcomer_box: Control
var _family_box: Control
var _timer: Timer
var _rng := RandomNumberGenerator.new() ## o "aleatório" da interface (cores e traços da aparência); a pessoa de verdade é o motor que valida
var _request := 0
var _loading := false
var _syncing := false
var _no_one_reason := "" ## por que ninguém serve para o laço escolhido (o primeiro motivo do motor)
var _families: Array = [] ## [{npc_id, label}] já filtradas pelo papel


func _init() -> void:
	add_theme_constant_override("separation", 14)
	_rng.randomize()
	_build_step_bar()
	var body := HBoxContainer.new()
	body.add_theme_constant_override("separation", 18)
	body.size_flags_vertical = Control.SIZE_EXPAND_FILL
	add_child(body)
	var pages := Control.new() # as cinco páginas dividem o mesmo lugar; só a da etapa atual aparece
	pages.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pages.size_flags_vertical = Control.SIZE_EXPAND_FILL
	pages.custom_minimum_size = Vector2(520, 300)
	body.add_child(pages)
	for i in STEPS.size():
		pages.add_child(_new_page(i))
	_build_origin(_page_box(0))
	_build_identity(_page_box(1))
	_build_look(_page_box(2))
	_build_attributes(_page_box(3))
	_build_review(_page_box(4))
	body.add_child(_build_side())
	_build_footer()
	_timer = Timer.new()
	_timer.one_shot = true
	_timer.wait_time = REFRESH_DELAY_S
	_timer.timeout.connect(func(): refresh())
	add_child(_timer)
	_show_step()


# --- esqueleto: topo, páginas, lateral e rodapé --------------------------------------------------------------------------

func _build_step_bar() -> void:
	var bar := HBoxContainer.new()
	bar.add_theme_constant_override("separation", 8)
	add_child(bar)
	for i in STEPS.size():
		var chip := Button.new()
		chip.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		chip.custom_minimum_size = Vector2(0, 44)
		chip.add_theme_font_size_override("font_size", 17)
		chip.focus_mode = Control.FOCUS_ALL
		chip.pressed.connect(func(): go_to(i))
		bar.add_child(chip)
		step_buttons.append(chip)


func _new_page(index: int) -> ScrollContainer:
	var scroll := ScrollContainer.new()
	scroll.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	var margin := MarginContainer.new()
	margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	margin.add_theme_constant_override("margin_right", 14)
	scroll.add_child(margin)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 10)
	margin.add_child(box)
	var banner := Label.new() # o erro do motor aparece aqui, na etapa que o causou
	banner.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	banner.add_theme_color_override("font_color", MenuKit.ERROR)
	banner.visible = false
	box.add_child(banner)
	_banners.append(banner)
	_pages.append(scroll)
	return scroll


func _page_box(index: int) -> VBoxContainer:
	return _pages[index].get_child(0).get_child(0)


func _build_side() -> Control:
	var panel := PanelContainer.new()
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 6)
	panel.add_child(box)
	figure = Preview.new()
	box.add_child(figure)
	summary = Label.new()
	summary.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	summary.custom_minimum_size = Vector2(300, 0)
	summary.add_theme_font_size_override("font_size", 15)
	box.add_child(summary)
	reroll_button = Button.new()
	reroll_button.text = "Sortear de novo o que ficou em branco"
	reroll_button.tooltip_text = "O motor sorteia outra vez o que você deixou em branco"
	reroll_button.pressed.connect(func(): reroll())
	box.add_child(reroll_button)
	return panel


func _build_footer() -> void:
	var bar := HBoxContainer.new()
	bar.add_theme_constant_override("separation", 14)
	add_child(bar)
	back_button = MenuKit.button("‹  Voltar", bar, func(): go_to(step - 1))
	back_button.custom_minimum_size = Vector2(150, 50)
	_message = Label.new()
	_message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_message.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_message.add_theme_color_override("font_color", Color("ffcf4a"))
	_message.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	bar.add_child(_message)
	next_button = MenuKit.button("Avançar  ›", bar, func(): _next())
	next_button.custom_minimum_size = Vector2(230, 50)
	next_button.add_theme_font_size_override("font_size", 19)
	create_button = next_button


# --- páginas ---------------------------------------------------------------------------------------------------------

func _heading(parent: Control, title: String, line: String) -> void:
	var label := Label.new()
	label.text = title
	label.add_theme_font_size_override("font_size", 24)
	label.add_theme_color_override("font_color", GOLD)
	parent.add_child(label)
	var sub := Label.new()
	sub.text = line
	sub.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	sub.modulate = Color(1, 1, 1, 0.75)
	parent.add_child(sub)


func _section(parent: Control, text: String) -> void:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", 17)
	parent.add_child(label)


func _line(parent: Control, label: String) -> HBoxContainer:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	var title := Label.new()
	title.text = label
	title.custom_minimum_size = Vector2(190, 0)
	row.add_child(title)
	parent.add_child(row)
	return row


func _select(parent: Control, label: String, items: Array) -> OptionButton:
	var row := _line(parent, label)
	var select := OptionButton.new()
	select.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	select.custom_minimum_size = Vector2(0, 38)
	for item in items:
		select.add_item(String(item))
	select.item_selected.connect(func(_i: int): _changed())
	row.add_child(select)
	return select


func _build_origin(box: VBoxContainer) -> void:
	_heading(box, "De onde você vem?", "Escolha a cidade e como chega a ela: sozinho, de fora, ou como parte de uma família que já vive lá.")
	city_slot = VBoxContainer.new()
	box.add_child(city_slot)
	_section(box, "Como você chega?")
	var how := HBoxContainer.new()
	how.add_theme_constant_override("separation", 10)
	box.add_child(how)
	for pair in [["Recém-chegado", "Chega de fora, sem casa e sem família na cidade, como um migrante."],
			["Parte de uma família", "Entra numa casa que já existe: como filho, irmão, cônjuge ou agregado."]]:
		var card := Button.new()
		card.toggle_mode = true
		card.text = "%s\n%s" % [pair[0], pair[1]]
		card.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		card.custom_minimum_size = Vector2(0, 84)
		card.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		card.focus_mode = Control.FOCUS_ALL
		var index := arrive_buttons.size()
		card.pressed.connect(func(): _arrive_as(index == 0))
		how.add_child(card)
		arrive_buttons.append(card)
	_newcomer_box = VBoxContainer.new()
	box.add_child(_newcomer_box)
	origin_select = _select(_newcomer_box, "Veio de", ["De fora do mundo"])
	_family_box = VBoxContainer.new()
	box.add_child(_family_box)
	role_select = _select(_family_box, "O que você é para eles", ROLES.map(func(p): return p[1]))
	role_select.item_selected.connect(func(_i: int): _refill_families())
	family_select = _select(_family_box, "Quem da família", [])
	family_select.item_selected.connect(func(_i: int): _refill_vias())
	via_select = _select(_family_box, "Por meio de", [])
	_hint = Label.new()
	_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_hint.modulate = Color(1, 1, 1, 0.8)
	box.add_child(_hint)


func _build_identity(box: VBoxContainer) -> void:
	_heading(box, "Quem é você?", "Tudo aqui é opcional: o que ficar em branco o motor sorteia (a barra mostra o que ele escolheu).")
	var name_row := _line(box, "Nome")
	name_edit = LineEdit.new()
	name_edit.placeholder_text = "Em branco: o motor sorteia um nome"
	name_edit.max_length = 60
	name_edit.custom_minimum_size = Vector2(0, 40)
	name_edit.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	name_edit.text_changed.connect(func(_t: String): _changed())
	name_row.add_child(name_edit)
	sex_select = _select(box, "Sexo", SEXES.map(func(p): return p[1]))
	_opt(box, "age", "Idade", 18, 90, 30, 1.0)
	_age_range = Label.new()
	_age_range.modulate = Color(1, 1, 1, 0.7)
	box.add_child(_age_range)
	profession_select = _select(box, "Ofício", ["Sortear"])
	_opt(box, "money", "Dinheiro inicial", 0, 500, 50, 1.0)
	_money_range = Label.new()
	_money_range.modulate = Color(1, 1, 1, 0.7)
	box.add_child(_money_range)


func _build_look(box: VBoxContainer) -> void:
	_heading(box, "Como você é?", "Mexa nas barras ou use Aleatório em cada grupo. Barra apagada: o motor decide.")
	var top := HBoxContainer.new()
	box.add_child(top)
	MenuKit.button("Tudo aleatório", top, func(): _randomize_look(_all_look_keys()))
	MenuKit.button("Deixar tudo com o motor", top, func(): _clear_keys(_all_look_keys()))
	for group in GROUPS:
		var keys: Array = group[1]
		var head := HBoxContainer.new()
		head.add_theme_constant_override("separation", 8)
		box.add_child(head)
		var title := Label.new()
		title.text = group[0]
		title.add_theme_font_size_override("font_size", 18)
		title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		head.add_child(title)
		MenuKit.button("Aleatório", head, func(): _randomize_look(keys))
		MenuKit.button("Motor decide", head, func(): _clear_keys(keys))
		for key in keys:
			if key == "heightM":
				_opt(box, key, "Altura", 1.4, 2.1, 1.7, 0.01)
			else:
				var label: String = GENES[_gene_index(String(key).trim_prefix("look."))][1]
				_opt(box, key, label, 0, 100, 50, 1.0)
	var outfit_head := HBoxContainer.new()
	box.add_child(outfit_head)
	var outfit_title := Label.new()
	outfit_title.text = "Roupa"
	outfit_title.add_theme_font_size_override("font_size", 18)
	outfit_title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	outfit_head.add_child(outfit_title)
	MenuKit.button("Aleatório", outfit_head, func(): _randomize_look(["outfit"]))
	MenuKit.button("Motor decide", outfit_head, func(): _clear_keys(["outfit"]))
	outfit_select = _select(box, "Traje", ["Sortear"])


func _build_attributes(box: VBoxContainer) -> void:
	_heading(box, "Saúde e personalidade", "Você não precisa mexer em nada aqui: o motor sorteia uma pessoa coerente com a cidade e o ofício.")
	_attrs_note = Label.new()
	_attrs_note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	box.add_child(_attrs_note)
	attrs_toggle = MenuKit.button("Escolher eu mesmo", box, func(): _toggle_attrs())
	_attrs_box = VBoxContainer.new()
	_attrs_box.add_theme_constant_override("separation", 6)
	_attrs_box.visible = false
	box.add_child(_attrs_box)
	_opt(_attrs_box, "health", "Saúde", 1, 100, 80, 1.0)
	_section(_attrs_box, "Personalidade (as barras apagadas o motor sorteia)")
	for pair in TRAITS:
		_opt(_attrs_box, "trait." + pair[0], String(pair[1]).capitalize(), 0, 100, 50, 1.0)


func _build_review(box: VBoxContainer) -> void:
	_heading(box, "Tudo pronto?", "Confira a ficha ao lado e o resumo abaixo. O motor confere tudo de novo ao criar; se recusar, o motivo aparece aqui.")
	_review = Label.new()
	_review.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_review.add_theme_font_size_override("font_size", 17)
	box.add_child(_review)
	var note := Label.new()
	note.text = "Para mudar algo, volte pela barra de etapas no topo: a prévia se atualiza sozinha."
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	note.modulate = Color(1, 1, 1, 0.65)
	box.add_child(note)


## Uma linha opcional: marcada, o valor vai na escolha; desmarcada, o motor sorteia e a barra mostra o que ele sorteou.
func _opt(parent: Control, key: String, label: String, lo: float, hi: float, value: float, step_size: float) -> void:
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 10)
	var check := CheckBox.new()
	check.text = label
	check.tooltip_text = "Marcado: vale o que você escolheu. Desmarcado: o motor decide."
	check.custom_minimum_size = Vector2(190, 0)
	for style_name in ["normal", "hover", "pressed", "hover_pressed", "disabled"]:
		check.add_theme_stylebox_override(style_name, StyleBoxEmpty.new()) # sem a caixa de botão: só a marca e o nome
	row.add_child(check)
	var cell := VBoxContainer.new()
	cell.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	cell.add_theme_constant_override("separation", 2)
	row.add_child(cell)
	var ramp = _ramp_of(key)
	if ramp != null:
		var strip := TextureRect.new()
		var gradient := Gradient.new()
		gradient.offsets = PackedFloat32Array(range(ramp.size()).map(func(i): return float(i) / float(ramp.size() - 1)))
		gradient.colors = PackedColorArray(ramp)
		var texture := GradientTexture1D.new()
		texture.gradient = gradient
		strip.texture = texture
		strip.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		strip.stretch_mode = TextureRect.STRETCH_SCALE
		strip.custom_minimum_size = Vector2(0, 9)
		cell.add_child(strip)
	else:
		var gap := Control.new() # a mesma altura das linhas com faixa de cor, para as barras alinharem
		gap.custom_minimum_size = Vector2(0, 9)
		cell.add_child(gap)
	var input := HSlider.new()
	input.min_value = lo
	input.max_value = hi
	input.step = step_size
	input.value = value
	input.custom_minimum_size = Vector2(0, 24)
	input.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	cell.add_child(input)
	var swatch: ColorRect = null
	if ramp != null:
		swatch = ColorRect.new()
		swatch.custom_minimum_size = Vector2(28, 28)
		row.add_child(swatch)
	else:
		var pad := Control.new()
		pad.custom_minimum_size = Vector2(28, 28)
		row.add_child(pad)
	var value_label := Label.new()
	value_label.custom_minimum_size = Vector2(150, 0)
	value_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	row.add_child(value_label)
	fields[key] = {"check": check, "input": input, "label": value_label, "swatch": swatch, "known": false}
	input.drag_started.connect(func(): check.button_pressed = true) # mexer na barra é escolher
	check.toggled.connect(func(_on: bool):
		_refresh_row(key)
		if not _syncing:
			_changed())
	input.value_changed.connect(func(_v: float):
		_refresh_row(key)
		if check.button_pressed and not _syncing:
			_changed())
	parent.add_child(row)
	_refresh_row(key)


static func _ramp_of(key: String):
	match key:
		"look.skinTone":
			return NpcActor.SKIN_RAMP
		"look.hairTone":
			return NpcActor.HAIR_RAMP
		"look.eyeTone":
			return NpcActor.EYE_RAMP
	return null


static func _gene_index(gene: String) -> int:
	for i in GENES.size():
		if GENES[i][0] == gene:
			return i
	return 0


## O texto da barra: o valor em palavras de gente (nunca o número cru do motor).
func format_value(key: String, v: float) -> String:
	if key == "age":
		return "%d anos" % int(v)
	if key == "money":
		return "%d moedas" % int(v)
	if key == "heightM":
		return "%.2f m" % v
	if key == "health":
		return "%d de 100" % int(v)
	var bucket := mini(4, int(v / 20.0))
	if WORDS.has(key):
		return String(WORDS[key][bucket])
	return "%s (%d)" % [TRAIT_WORDS[bucket], int(v)]


func _refresh_row(key: String) -> void:
	var field: Dictionary = fields[key]
	var on: bool = field.check.button_pressed
	var text := format_value(key, float(field.input.value))
	if on:
		field.label.text = text
	elif field.known:
		field.label.text = "motor: %s" % text
	else:
		field.label.text = "o motor decide"
	field.input.modulate = Color(1, 1, 1, 1.0 if on else 0.5)
	field.label.modulate = Color(1, 1, 1, 1.0 if on else 0.7)
	var ramp = _ramp_of(key)
	if field.swatch != null and ramp != null:
		field.swatch.color = NpcActor.ramp_color(ramp, int(field.input.value))
		field.swatch.modulate = Color(1, 1, 1, 1.0 if on else 0.55)


## Marca o campo e dá o valor (a tela do jogador e os testes).
func set_field(key: String, value: float) -> void:
	var field: Dictionary = fields[key]
	field.input.value = value
	field.check.button_pressed = true


func _on(key: String) -> bool:
	return fields[key].check.button_pressed


func _value(key: String) -> float:
	return float(fields[key].input.value)


# --- aleatório da interface e "motor decide" ------------------------------------------------------------------------------

func _all_look_keys() -> Array:
	var keys: Array = []
	for group in GROUPS:
		keys.append_array(group[1])
	keys.append("outfit")
	return keys


## Sorteia no cliente os valores do grupo (a interface só propõe; o motor valida na prévia). `outfit` sorteia o traje.
func _randomize_look(keys: Array) -> void:
	_syncing = true
	for key in keys:
		if key == "outfit":
			if outfit_select.item_count > 1:
				outfit_select.select(_rng.randi_range(1, outfit_select.item_count - 1))
			continue
		var field: Dictionary = fields[key]
		var input: Range = field.input
		input.value = snappedf(_rng.randf_range(input.min_value, input.max_value), input.step)
		field.check.button_pressed = true
	_syncing = false
	_changed()


func _clear_keys(keys: Array) -> void:
	_syncing = true
	for key in keys:
		if key == "outfit":
			outfit_select.select(0)
		else:
			fields[key].check.button_pressed = false
	_syncing = false
	_changed()


func _toggle_attrs() -> void:
	_attrs_box.visible = not _attrs_box.visible
	if not _attrs_box.visible:
		var keys: Array = ["health"]
		for pair in TRAITS:
			keys.append("trait." + pair[0])
		_clear_keys(keys)
	_update_attrs()


func _update_attrs() -> void:
	attrs_toggle.text = "Deixar o motor decidir" if _attrs_box.visible else "Escolher eu mesmo"
	var text := "O motor decide a saúde e a personalidade."
	if not state.is_empty():
		var personality: Dictionary = state.get("personality", {})
		var traits: Array = TRAITS.duplicate()
		traits.sort_custom(func(a, b): return int(personality.get(a[0], 0)) > int(personality.get(b[0], 0)))
		var strongest: Array = traits.slice(0, 3).map(func(p): return p[1])
		# #222: frase inteira (antes "saúde 50 de 100 e, no jeito de ser, cuidado, ...")
		var listed: String = ", ".join(strongest.slice(0, strongest.size() - 1)) + " e " + String(strongest.back()) if strongest.size() > 1 else ", ".join(strongest)
		text = "O motor sorteou: saúde %d de 100; traços mais fortes: %s." % [int(state.health), listed]
	_attrs_note.text = text


# --- cidade, família e faixas que o motor dá -------------------------------------------------------------------------------

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
		_show_message(_reason(response), step)
		_set_state({})
		return
	_loading = true
	options = response.body
	_fill_options()
	_loading = false
	await refresh()


## Recomeça do zero na primeira etapa (ao abrir a janela).
func restart() -> void:
	step = 0
	reached = 0
	state = {}
	error_step = -1
	_show_message("", -1)
	_show_step()


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
	_money_range.text = "O motor deixa começar com até %d moedas; o típico da cidade é %d." % [int(options.get("moneyCap", 0)), int(options.get("typicalMoney", 0))]
	_set_height_range()
	_refill_families()
	for key in fields:
		_refresh_row(key)


func _set_height_range() -> void:
	var range_of: Dictionary = options.get("heightM", {}).get("male" if sex_select.selected == 2 else "female", {})
	if range_of.is_empty():
		return
	var input: Range = fields.heightM.input
	input.min_value = float(range_of.min)
	input.max_value = float(range_of.max)
	if not fields.heightM.check.button_pressed:
		input.value = float(range_of.typical)


func _arrive_as(newcomer: bool) -> void:
	if newcomer:
		role_select.select(0)
	elif role_select.selected == 0:
		for i in ROLES.size():
			if not role_select.is_item_disabled(i) and i > 0:
				role_select.select(i)
				break
		if role_select.selected == 0:
			role_select.select(1)
	_refill_families()


## Os parentes que o papel escolhido admite (o motor diz quem e em que idade); só eles entram na lista.
func _refill_families() -> void:
	var kind := _role_kind()
	_families.clear()
	family_select.clear()
	var newcomer := kind == "newcomer"
	_newcomer_box.visible = newcomer
	_family_box.visible = not newcomer
	arrive_buttons[0].button_pressed = newcomer
	arrive_buttons[1].button_pressed = not newcomer
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
	_mark_roles()
	_no_one_reason = first_reason
	_refill_vias()


## Os laços que ninguém da cidade permite agora aparecem apagados na lista (o motivo vem ao escolher).
func _mark_roles() -> void:
	for i in ROLES.size():
		var kind := String(ROLES[i][0])
		var possible := kind == "newcomer"
		for family in options.get("families", []):
			for member in family.members:
				if bool(member.roles.get(kind, {}).get("available", false)):
					possible = true
		var text := String(ROLES[i][1]) + ("" if possible or options.is_empty() else "  (ninguém agora)")
		role_select.set_item_text(i, text)
	role_select.set_item_disabled(0, true) # "recém-chegado" se escolhe pelo cartão de cima


## Nos laços que passam por alguém, a lista de quem serve de passagem para o parente escolhido (e a faixa de idade de cada um).
func _refill_vias() -> void:
	var kind := _role_kind()
	via_select.clear()
	via_select.get_parent().visible = kind in VIA_LABELS and not _families.is_empty()
	family_select.get_parent().visible = kind != "newcomer"
	if kind in VIA_LABELS:
		for via in _anchor().get("vias", []):
			via_select.add_item("%s, %d anos" % [via.name, int(via.ageYears)])
			via_select.set_item_metadata(via_select.item_count - 1, int(via.npcId))
	_update_hint()
	_changed()


func _update_hint() -> void:
	var kind := _role_kind()
	if kind == "newcomer":
		_hint.text = "Você chega sem casa e sem família na cidade, como um migrante."
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
		_hint.text = "Você entra na casa dele como agregado, sem laço de sangue."
	if kind not in BLOOD:
		var age_range: Range = fields.age.input
		age_range.min_value = int(options.get("ageMin", 18))
		age_range.max_value = int(options.get("ageMax", 90))
	var lo := int(fields.age.input.min_value)
	var hi := int(fields.age.input.max_value)
	_age_range.text = "Faixa permitida: de %d a %d anos." % [lo, hi]
	_refresh_row("age")


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


# --- etapas -------------------------------------------------------------------------------------------------------------

## A etapa está pronta para avançar? Local (cidade, parente do laço) e sem recusa do motor atribuída a ela ou a uma anterior.
func step_valid(index: int) -> bool:
	if message != "" and error_step >= 0 and error_step <= index:
		return false
	if index == 0:
		return city_id != "" and (_role_kind() == "newcomer" or not _families.is_empty())
	return true


func go_to(target: int) -> void:
	target = clampi(target, 0, STEPS.size() - 1)
	if target == step:
		return
	if target > step:
		if target > reached + 1: # só se abre o que já foi visitado ou a próxima: nada de pular etapas
			return
		for i in range(step, target):
			if not step_valid(i):
				return
	step = target
	reached = maxi(reached, step)
	_show_step()
	if step == STEPS.size() - 1 or not state.is_empty():
		_timer.stop()
		await refresh() # trocar de etapa roda a prévia do motor na hora


func _next() -> void:
	if step == STEPS.size() - 1:
		await create()
		return
	_timer.stop()
	await refresh()
	if step_valid(step):
		await go_to(step + 1)


func _show_step() -> void:
	for i in _pages.size():
		_pages[i].visible = i == step
	for i in step_buttons.size():
		_style_chip(i)
	back_button.visible = step > 0
	var last := step == STEPS.size() - 1
	next_button.text = "Criar e entrar  ✓" if last else "Avançar  ›"
	if last: # #222: a ação final em destaque (antes tinha o mesmo peso de "Voltar")
		var accent := StyleBoxFlat.new()
		accent.bg_color = Color("3a7d4f")
		accent.set_corner_radius_all(8)
		next_button.add_theme_stylebox_override("normal", accent)
	else:
		next_button.remove_theme_stylebox_override("normal")
	next_button.disabled = not step_valid(step) or (last and state.is_empty())
	_update_attrs()
	_update_review()
	_footer_message()


func _style_chip(i: int) -> void:
	var chip := step_buttons[i]
	var bad := error_step == i and message != ""
	var current := i == step
	var done := i <= reached and not current
	var mark := "!" if bad else ("✓" if done else str(i + 1))
	chip.text = "%s   %s" % [mark, STEPS[i]]
	chip.disabled = i > reached
	var color := Color(0.14, 0.17, 0.23, 0.9)
	var font := Color(1, 1, 1, 0.55)
	if current:
		color = GOLD
		font = Color("1b1408")
	elif bad:
		color = Color(0.55, 0.2, 0.18, 0.95)
		font = Color.WHITE
	elif done:
		color = Color(0.2, 0.34, 0.26, 0.95)
		font = Color("dff5e2")
	for state_name in ["normal", "hover", "pressed", "disabled"]:
		var box := StyleBoxFlat.new()
		box.bg_color = color if state_name != "hover" else color.lightened(0.12)
		box.set_corner_radius_all(10)
		chip.add_theme_stylebox_override(state_name, box)
	for color_name in ["font_color", "font_hover_color", "font_pressed_color", "font_disabled_color", "font_focus_color"]:
		chip.add_theme_color_override(color_name, font)


func _update_review() -> void:
	if state.is_empty():
		_review.text = "O motor ainda não aceitou esta escolha. Volte e corrija o que ele apontou."
		return
	var role: Dictionary = state.role
	var lines: Array[String] = []
	lines.append("1 · Origem: %s" % _role_text(state))
	lines.append("2 · Identidade: %s, %d anos, %s. Começa com %d moedas." % [state.name, int(state.ageYears),
		_profession_text(state), int(state.money)])
	var look: Dictionary = state.look
	lines.append("3 · Aparência: %s, %.2f m, %.0f kg." % [String(state.get("outfitName", "")), float(look.heightM), float(look.weightKg)])
	lines.append("4 · Atributos: %s" % _attrs_note.text.trim_prefix("O motor sorteou: ").trim_suffix("."))
	_review.text = "\n\n".join(lines)
	if String(role.kind) == "newcomer" and String(role.get("fromCityId", "")) != "":
		_review.text += "\n\nVocê veio de outra cidade do mundo."


# --- prévia e criação ---------------------------------------------------------------------------------------------------

func _changed() -> void:
	if _loading or _syncing or not is_inside_tree() or _timer == null:
		return
	_set_height_range()
	_update_hint()
	_show_step()
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
		_show_message(_reason(response), _step_of_error(_reason(response)))
		return
	_show_message("", -1)
	_set_state(response.body.character)


func _set_state(character: Dictionary) -> void:
	state = character
	if character.is_empty():
		figure.clear()
		summary.text = ""
	else:
		figure.show_character(character)
		summary.text = describe(character)
		_sync_unchosen(character)
	_show_step()


## As barras que o jogador deixou em branco mostram o que o motor sorteou (nunca o contrário).
func _sync_unchosen(c: Dictionary) -> void:
	_syncing = true
	var look: Dictionary = c.get("look", {})
	var personality: Dictionary = c.get("personality", {})
	var drawn := {"age": c.get("ageYears"), "health": c.get("health"), "money": c.get("money"), "heightM": look.get("heightM")}
	for pair in GENES:
		drawn["look." + pair[0]] = look.get(pair[0])
	for pair in TRAITS:
		drawn["trait." + pair[0]] = personality.get(pair[0])
	for key in drawn:
		if drawn[key] == null or not fields.has(key) or _on(key):
			continue
		fields[key].input.value = float(drawn[key])
		fields[key].known = true
		_refresh_row(key)
	name_edit.placeholder_text = "Sorteado: %s (escreva para trocar)" % String(c.name)
	_syncing = false


## Em que etapa o motivo da recusa do motor se resolve (palavras que o motor usa; o resto fica na etapa de agora).
func _step_of_error(text: String) -> int:
	var low := text.to_lower()
	for word in ["família", "casa de", "parente", "laço", "aceita", "cônjuge", "pais", "irmão", "recém", "cidade", "origem"]:
		if low.contains(word):
			return 0
	for word in ["idade", "nome", "dinheiro", "ofício", "sexo", "moeda", "longevidade"]:
		if low.contains(word):
			return 1
	for word in ["altura", "traje", "roupa", "pele", "cabelo", "rosto", "olhos", "peso", "aparência", "corpo"]:
		if low.contains(word):
			return 2
	for word in ["saúde", "traço", "personalidade"]:
		if low.contains(word):
			return 3
	return step


## O resumo da pessoa como o motor a validou (cada linha é um campo da resposta).
static func describe(c: Dictionary) -> String:
	var sex := "Mulher" if String(c.sex) == "Female" else "Homem"
	var look: Dictionary = c.look
	var lines: Array[String] = ["%s · %s, %d anos" % [c.name, sex, int(c.ageYears)]]
	lines.append("Ofício: %s" % _profession_text(c).trim_prefix("ofício "))
	lines.append("%s · %.2f m · %.0f kg" % [String(c.get("outfitName", "")), float(look.heightM), float(look.weightKg)])
	lines.append("Saúde %d · Dinheiro %d" % [int(c.health), int(c.money)])
	lines.append(_role_text(c))
	return "\n".join(lines)


static func _profession_text(c: Dictionary) -> String:
	var name := String(c.get("profession", ""))
	return name if name != "" else "ofício nº %d" % int(c.professionId)


static func _role_text(c: Dictionary) -> String:
	var role: Dictionary = c.role
	var who := String(role.get("relativeName", ""))
	match String(role.kind):
		"child":
			return "Filho(a) de %s" % who
		"grandchild":
			return "Neto(a) de %s" % who
		"sibling":
			return "Irmão(ã) de %s" % who
		"nephew":
			return "Sobrinho(a) de %s" % who
		"uncle":
			return "Tio(a) de %s" % who
		"cousin":
			return "Primo(a) de %s" % who
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
		_show_message(error, _step_of_error(error))
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


## O aviso: na página da etapa que o causou (em vermelho) e no rodapé, que diz onde corrigir se não for a etapa de agora.
func _show_message(text: String, cause: int) -> void:
	message = text
	error_step = cause if text != "" else -1
	for i in _banners.size():
		_banners[i].visible = text != "" and i == cause
		_banners[i].text = text
	_show_step()


func _footer_message() -> void:
	if message == "":
		_message.text = ""
	elif error_step == step or error_step < 0:
		_message.text = "O motor não aceitou: veja o aviso acima."
	else:
		_message.text = "Corrija na etapa %d · %s: %s" % [error_step + 1, STEPS[error_step], message]
