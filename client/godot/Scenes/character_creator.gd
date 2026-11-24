extends PanelContainer
## AD-197q: entrar como pessoa nova. O motor diz de onde dá para sair (`/players/origins`), sorteia candidatos do agregado da
## cidade (`/players/{id}/candidates`) e, escolhido um, materializa e encarna (`/players/{id}/create`). Aqui não se monta atributo
## nenhum: só se escolhe a cidade, se estreita o filtro, se pede outra amostra e se entra.

signal closed()
signal warning(text: String)
signal entered(npc_id: int, name: String, city_id: String)

const SEXES := [["", "Qualquer sexo"], ["Female", "Mulher"], ["Male", "Homem"]]
const TRAITS := {"extroversion": "expansividade", "agreeableness": "amabilidade", "conscientiousness": "cuidado", "emotionalStability": "serenidade",
	"openness": "curiosidade", "ambition": "ambição", "loyalty": "lealdade", "altruism": "generosidade", "impulsivity": "impulsividade",
	"riskAversion": "cautela"}

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
	custom_minimum_size = Vector2(760, 0)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	var title := Label.new()
	title.text = "Entrar como pessoa nova"
	title.add_theme_font_size_override("font_size", 20)
	title.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(title)
	var close_button := Button.new()
	close_button.text = "Fechar (Esc)"
	close_button.pressed.connect(func(): close())
	header.add_child(close_button)
	var note := Label.new()
	note.text = "Quem você pode ser sai da população da cidade que o mundo já conta: ninguém é criado do nada."
	note.modulate = Color(1, 1, 1, 0.75)
	note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	box.add_child(note)
	var filters := HFlowContainer.new()
	box.add_child(filters)
	city_select = OptionButton.new()
	city_select.item_selected.connect(func(_i: int): resample())
	filters.add_child(city_select)
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
	box.add_child(_message)
	_grid = GridContainer.new()
	_grid.columns = 3
	_grid.add_theme_constant_override("h_separation", 8)
	_grid.add_theme_constant_override("v_separation", 8)
	box.add_child(_grid)


func _age_box(prefix: String, value: int) -> SpinBox:
	var spin := SpinBox.new()
	spin.prefix = prefix
	spin.min_value = 0
	spin.max_value = 150
	spin.value = value
	return spin


## Abre e pergunta ao motor de onde dá para sair; a cidade com mais gente no agregado vem primeiro.
func open() -> void:
	is_open = true
	visible = true
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
		return
	await resample()


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
