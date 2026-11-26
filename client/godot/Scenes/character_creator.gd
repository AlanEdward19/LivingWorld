extends Control
## Criar a pessoa e entrar no mundo (#222): uma janela grande no centro da tela, em etapas (`character_designer.gd`). O motor diz de
## onde dá para sair (`/players/origins`). Dois modos:
## - "Montar" (AD-197t, o padrão): o criador de verdade: origem, identidade, aparência e atributos; o motor valida e devolve a pessoa.
## - "Sortear alguém da cidade" (AD-197q, atalho discreto): candidatos do agregado (`/players/{id}/candidates`), escolhido um, o motor o
##   materializa e encarna (`/players/{id}/create`). Aqui não se monta nada: se estreita o filtro, se pede outra amostra e se entra.

signal closed()
signal warning(text: String)
signal entered(npc_id: int, name: String, city_id: String)

const Designer := preload("res://Scenes/character_designer.gd")
const MenuKit := preload("res://Scenes/menu/menu_kit.gd")
const SEXES := [["", "Qualquer sexo"], ["Female", "Mulher"], ["Male", "Homem"]]
const TRAITS := {"extroversion": "expansividade", "agreeableness": "amabilidade", "conscientiousness": "cuidado", "emotionalStability": "serenidade",
	"openness": "curiosidade", "ambition": "ambição", "loyalty": "lealdade", "altruism": "generosidade", "impulsivity": "impulsividade",
	"riskAversion": "cautela"}
const WIDTH := 1180.0
const HEIGHT := 760.0

var mode := "design" ## "design" (montar) ou "sample" (sortear do agregado)
var designer
var frame: PanelContainer
var sample_button: Button
var _sample_box: VBoxContainer
var _city_row: HBoxContainer
var hud ## o HUD do jogador (sessão, playerId, e quem fica ativo ao entrar)
var api
var is_open := false
var origins: Array = [] ## [{cityId, name, aggregateCount, detailedCount}]
var city_id := ""
var sample: Dictionary = {} ## a última amostra do motor
var cards: Array = [] ## [{npc_id, button, text}]
var professions: Dictionary = {} ## professionId -> nome, visto nas amostras (para o filtro)
var message := ""
var city_select: OptionButton
var sex_select: OptionButton
var min_age: SpinBox
var max_age: SpinBox
var profession_select: OptionButton
var resample_button: Button
var _message: Label
var _grid: GridContainer
var _request := 0


func _init() -> void:
	visible = false
	mouse_filter = Control.MOUSE_FILTER_STOP # a janela cobre a tela: nada por baixo recebe clique
	var dim := ColorRect.new()
	dim.color = Color(0.02, 0.03, 0.05, 0.62)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	center.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(center)
	frame = PanelContainer.new()
	frame.custom_minimum_size = Vector2(760, 520)
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.06, 0.08, 0.12, 0.97)
	style.set_corner_radius_all(18)
	style.set_border_width_all(1)
	style.border_color = Color(1, 1, 1, 0.12)
	style.shadow_color = Color(0, 0, 0, 0.5)
	style.shadow_size = 24
	for side in [SIDE_LEFT, SIDE_RIGHT]:
		style.set_content_margin(side, 28)
	style.set_content_margin(SIDE_TOP, 20)
	style.set_content_margin(SIDE_BOTTOM, 22)
	frame.add_theme_stylebox_override("panel", style)
	center.add_child(frame)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 12)
	frame.add_child(box)
	var header := HBoxContainer.new()
	header.add_theme_constant_override("separation", 12)
	box.add_child(header)
	var title := Label.new()
	title.text = "Criar a minha pessoa"
	title.add_theme_font_size_override("font_size", 28)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	sample_button = MenuKit.button("Sortear alguém da cidade", header, func(): set_mode("design" if mode == "sample" else "sample"))
	sample_button.tooltip_text = "Atalho: o motor tira alguém da população da cidade (ninguém é criado do nada)"
	sample_button.flat = true
	var close_button := MenuKit.button("Fechar (Esc)", header, func(): close())
	close_button.custom_minimum_size.x = 140
	city_select = OptionButton.new()
	city_select.custom_minimum_size = Vector2(0, 40)
	city_select.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	city_select.item_selected.connect(func(_i: int): _city_changed())
	designer = Designer.new()
	designer.hud = hud
	designer.size_flags_vertical = Control.SIZE_EXPAND_FILL
	designer.created.connect(func(id: int, who: String, city: String): _entered(id, who, city))
	designer.warning.connect(func(text: String): warning.emit(text))
	box.add_child(designer)
	var city_title := Label.new()
	city_title.text = "Cidade"
	city_title.custom_minimum_size.x = 190
	_city_row = HBoxContainer.new()
	_city_row.add_theme_constant_override("separation", 10)
	_city_row.add_child(city_title)
	_city_row.add_child(city_select)
	designer.city_slot.add_child(_city_row)
	_sample_box = VBoxContainer.new()
	_sample_box.add_theme_constant_override("separation", 8)
	_sample_box.size_flags_vertical = Control.SIZE_EXPAND_FILL
	box.add_child(_sample_box)
	var filters := HFlowContainer.new()
	_sample_box.add_child(filters)
	sex_select = OptionButton.new()
	for pair in SEXES:
		sex_select.add_item(pair[1])
	filters.add_child(sex_select)
	min_age = _age_box("idade de", 18)
	filters.add_child(min_age)
	max_age = _age_box("até", 120)
	filters.add_child(max_age)
	profession_select = OptionButton.new()
	profession_select.add_item("Qualquer ofício")
	profession_select.set_item_metadata(0, -1)
	filters.add_child(profession_select)
	resample_button = Button.new()
	resample_button.text = "Outra amostra"
	resample_button.tooltip_text = "O motor sorteia outras pessoas do agregado (o pedido fica registrado)"
	resample_button.pressed.connect(func(): resample())
	filters.add_child(resample_button)
	_message = Label.new()
	_message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_message.add_theme_color_override("font_color", Color("ffcf4a"))
	_sample_box.add_child(_message)
	var scroll := ScrollContainer.new() # os cartões rolam dentro da janela
	scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.custom_minimum_size.y = 220
	_sample_box.add_child(scroll)
	_grid = GridContainer.new()
	_grid.columns = 3
	_grid.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_grid.add_theme_constant_override("h_separation", 8)
	_grid.add_theme_constant_override("v_separation", 8)
	scroll.add_child(_grid)
	resized.connect(_fit)
	_apply_mode()


## A janela ocupa a tela com folga, até um teto legível.
func _fit() -> void:
	frame.custom_minimum_size = Vector2(clampf(size.x - 80.0, 760.0, WIDTH), clampf(size.y - 70.0, 540.0, HEIGHT))


## Troca entre montar a pessoa e sortear do agregado; abre a cidade escolhida no modo novo.
func set_mode(next: String) -> void:
	mode = next
	_apply_mode()
	await _city_changed()


func _apply_mode() -> void:
	designer.visible = mode == "design"
	_sample_box.visible = mode == "sample"
	sample_button.text = "Sortear alguém da cidade" if mode == "design" else "Voltar a montar a minha pessoa"
	var home: Control = designer.city_slot if mode == "design" else _sample_box # a escolha da cidade mora na etapa 1 ou nos filtros
	if _city_row.get_parent() != home:
		_city_row.reparent(home, false)
		if home == _sample_box:
			_sample_box.move_child(_city_row, 0)


func _city_changed() -> void:
	if city_select.item_count == 0:
		return
	city_id = String(city_select.get_item_metadata(max(0, city_select.selected)))
	if mode == "design":
		designer.hud = hud
		await designer.open_city(city_id)
	else:
		await resample()


func _age_box(prefix: String, value: int) -> SpinBox:
	var spin := SpinBox.new()
	spin.prefix = prefix
	spin.min_value = 0
	spin.max_value = 150
	spin.value = value
	return spin


## Fica aberta já (o roteador a põe na frente; ela não se encaixa: é a janela central da tela).
func reveal() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT) # cobre a tela; a janela fica no centro
	is_open = true
	visible = true
	_fit()


## Abre e pergunta ao motor de onde dá para sair; a cidade com mais gente no agregado vem primeiro. `open_mode`: "design" (montar,
## o padrão) ou "sample" (sortear alguém do agregado).
func open(open_mode: String = "design") -> void:
	mode = open_mode
	_apply_mode()
	designer.restart()
	reveal()
	_request += 1
	var mine := _request
	var response: Dictionary = await api.player_origins()
	if mine != _request or not is_open:
		return
	origins = response.body.get("cities", []) if response.ok and response.body is Dictionary else []
	city_select.clear()
	for city in origins:
		city_select.add_item("%s · %d no agregado" % [String(city.name), int(city.aggregateCount)])
		city_select.set_item_metadata(city_select.item_count - 1, String(city.cityId))
	if origins.is_empty():
		_show_message("Não há cidade no mundo agora.")
		designer._show_message(message, 0)
		return
	await _city_changed()


func close() -> void:
	if not is_open:
		return
	is_open = false
	visible = false
	_request += 1
	closed.emit()


func filter() -> Dictionary:
	var result := {}
	var sex: String = SEXES[max(0, sex_select.selected)][0]
	if sex != "":
		result["sex"] = sex
	if int(min_age.value) > 18:
		result["minAge"] = int(min_age.value)
	if int(max_age.value) < 120:
		result["maxAge"] = int(max_age.value)
	var profession = profession_select.get_item_metadata(max(0, profession_select.selected))
	if profession != null and int(profession) >= 0:
		result["professionId"] = int(profession)
	return result


## Pede ao motor uma amostra nova da cidade escolhida com o filtro de agora.
func resample() -> void:
	if city_select.item_count == 0:
		return
	city_id = String(city_select.get_item_metadata(max(0, city_select.selected)))
	_request += 1
	var mine := _request
	resample_button.disabled = true
	var response: Dictionary = await hud.candidates(city_id, filter())
	if mine != _request or not is_open:
		return
	resample_button.disabled = false
	if not response.ok:
		sample = {}
		_render([])
		_show_message(_reason(response))
		return
	sample = response.body
	_show_message("")
	_render(sample.get("candidates", []))


func _render(candidates: Array) -> void:
	for child in _grid.get_children():
		child.queue_free()
	cards.clear()
	for c in candidates:
		_remember_profession(int(c.professionId), String(c.get("profession", "")))
		var card := PanelContainer.new()
		card.custom_minimum_size = Vector2(240, 0)
		var box := VBoxContainer.new()
		card.add_child(box)
		var name := Label.new()
		name.text = String(c.name)
		name.add_theme_font_size_override("font_size", 17)
		box.add_child(name)
		var text := describe(c, String(sample.get("cityName", "")))
		var body := Label.new()
		body.text = text
		body.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		box.add_child(body)
		var enter := Button.new()
		enter.text = "Entrar como %s" % String(c.name).get_slice(" ", 0)
		enter.disabled = bool(c.get("willPrevents", false))
		if enter.disabled:
			enter.tooltip_text = "A vontade desta pessoa impede a posse agora."
		var id := int(c.npcId)
		var who := String(c.name)
		enter.pressed.connect(func(): choose(id, who))
		box.add_child(enter)
		_grid.add_child(card)
		cards.append({"npc_id": id, "button": enter, "text": text})


## O que o motor sabe da pessoa, em texto (nada aqui é inventado: cada linha é um campo da amostra).
static func describe(c: Dictionary, city_name: String) -> String:
	var sex := "Mulher" if String(c.sex) == "Female" else "Homem"
	var lines: Array[String] = ["%s, %d anos" % [sex, int(c.ageYears)]]
	var profession := String(c.get("profession", ""))
	lines.append("Ofício: %s" % (profession if profession != "" else "nº %d" % int(c.professionId)))
	var born := String(c.get("bornIn", ""))
	if born != "" and born != city_name:
		lines.append("Nasceu em %s · vive em %s há %d anos" % [born, city_name, int(c.yearsInCity)])
	else:
		lines.append("Nasceu em %s" % city_name)
	lines.append("Família: os parentes seguem no agregado (nenhum detalhado)")
	lines.append("Saúde %d · Bens %d" % [int(c.health), int(c.wealth)])
	lines.append("Traços: %s" % ", ".join(strongest_traits(c.get("personality", {}))))
	return "\n".join(lines)


## Os três traços mais fortes da personalidade sorteada pelo motor.
static func strongest_traits(personality: Dictionary) -> Array[String]:
	var keys: Array = personality.keys().filter(func(k): return TRAITS.has(String(k)))
	keys.sort_custom(func(a, b): return int(personality[a]) > int(personality[b]) or (int(personality[a]) == int(personality[b]) and String(a) < String(b)))
	var result: Array[String] = []
	for k in keys.slice(0, 3):
		result.append(TRAITS[String(k)])
	return result


## Entra como a pessoa escolhida; recusa do motor vira aviso e a tela continua aberta.
func choose(id: int, name: String) -> void:
	var error: String = await hud.enter_as_new(city_id, id, int(sample.get("sample", -1)), name)
	if error != "":
		_show_message(_strip(error))
		warning.emit(_strip(error))
		return
	var city := city_id
	close()
	entered.emit(id, name, city)


## A pessoa montada foi criada e encarnada: fecha e avisa o app (a cidade, o corpo e a 3ª pessoa seguem como ao encarnar).
func _entered(id: int, who: String, city: String) -> void:
	close()
	entered.emit(id, who, city)


func _remember_profession(id: int, name: String) -> void:
	if professions.has(id):
		return
	professions[id] = name
	profession_select.add_item(name if name != "" else "ofício nº %d" % id)
	profession_select.set_item_metadata(profession_select.item_count - 1, id)


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
