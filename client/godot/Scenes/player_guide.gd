extends PanelContainer
## #238: o guia da primeira posse. Na primeira vez que o jogador toma alguém num mundo, passos curtos dizem o que fazer e com que
## tecla, montados só com o que o motor diz (`GET /players/{id}/goals?start=true`): casa e família, despensa, emprego ou vaga, a mata
## para coletar, o que mais aperta. Nada aqui é inventado: sem o dado, o passo não aparece. F1 (ajuda) reabre.

signal closed()

const Grid := preload("res://Scenes/grid.gd")

var steps: Array = []
var index := 0
var _title: Label
var _text: Label
var _counter: Label
var _prev: Button
var _next: Button


func _init() -> void:
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.05, 0.06, 0.09, 0.94)
	style.set_corner_radius_all(10)
	style.set_content_margin_all(16)
	add_theme_stylebox_override("panel", style)
	set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP) # no alto: o meio da tela é da mira e da dica do E
	custom_minimum_size.x = 480
	offset_top = 76
	grow_horizontal = Control.GROW_DIRECTION_BOTH
	grow_vertical = Control.GROW_DIRECTION_END
	visible = false
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	add_child(box)
	_counter = _label("", 13, Color(1, 1, 1, 0.55))
	_title = _label("", 20, Color("ffe9a6"))
	_text = _label("", 16, Color.WHITE)
	_text.custom_minimum_size.x = 448
	for label in [_counter, _title, _text]:
		box.add_child(label)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 8)
	box.add_child(row)
	var skip := Button.new()
	skip.text = "Pular guia"
	skip.pressed.connect(close)
	row.add_child(skip)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(spacer)
	_prev = Button.new()
	_prev.text = "◀ Voltar"
	_prev.pressed.connect(go.bind(-1))
	row.add_child(_prev)
	_next = Button.new()
	_next.pressed.connect(go.bind(1))
	row.add_child(_next)


static func _label(text: String, size: int, color: Color) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


## Abre o guia com os passos de `start` (o bloco `start` do motor) e a posição/direção do corpo, que dizem para que lado fica cada lugar.
func open(start: Dictionary, name: String, fine: Vector2, forward: Vector2, name_of: Callable = Callable()) -> void:
	steps = steps_for(start, name, fine, forward, name_of)
	if steps.is_empty():
		return
	index = 0
	visible = true
	_show()


func go(delta: int) -> void:
	index += delta
	if index >= steps.size():
		close()
		return
	index = maxi(index, 0)
	_show()


func close() -> void:
	if visible:
		visible = false
		closed.emit()


func _show() -> void:
	var step: Dictionary = steps[index]
	_counter.text = "Primeiros passos · %d de %d" % [index + 1, steps.size()]
	_title.text = String(step.title)
	_text.text = String(step.text)
	_prev.disabled = index == 0
	_next.text = "Entendi ✔" if index == steps.size() - 1 else "Próximo ▶"
	reset_size()
	reset_size.call_deferred() # o texto quebrado só tem altura depois de um quadro


func _unhandled_key_input(event: InputEvent) -> void:
	if not visible or not event is InputEventKey or not event.pressed or event.echo:
		return
	if event.keycode == KEY_ENTER or event.keycode == KEY_KP_ENTER:
		get_viewport().set_input_as_handled()
		go(1)
	elif event.keycode == KEY_ESCAPE:
		get_viewport().set_input_as_handled()
		close()


# --- o conteúdo (puro: o que o motor disse vira texto) ---------------------------------------------

## "a 18 m, à frente e à direita" (metros do motor; a direção é a da câmera, a mesma do WASD). Sem posição, só a distância.
static func where(place: Dictionary, fine: Vector2, forward: Vector2) -> String:
	var meters := int(round(float(place.get("distance", 0)) * Grid.cell_meters))
	var text := "a %d m" % meters if meters > 0 else "aqui"
	if fine == Vector2.INF or forward == Vector2.ZERO or meters <= 0:
		return text
	var side := direction_word(Vector2(float(place.get("x", 0)) + 0.5, float(place.get("y", 0)) + 0.5) - fine, forward)
	return text if side == "" else "%s, %s" % [text, side]


## Para que lado do corpo fica `offset` (células), olhando para `forward` (x, z): "à frente", "à direita", "atrás e à esquerda"...
static func direction_word(offset: Vector2, forward: Vector2) -> String:
	if offset.length() < 0.5 or forward == Vector2.ZERO:
		return ""
	var f := forward.normalized()
	var right := Vector2(-f.y, f.x)
	var ahead := offset.dot(f) / offset.length()
	var side := offset.dot(right) / offset.length()
	var long_axis := "à frente" if ahead > 0.0 else "atrás"
	var cross_axis := "à direita" if side > 0.0 else "à esquerda"
	if absf(side) < 0.38:
		return long_axis
	if absf(ahead) < 0.38:
		return cross_axis
	return "%s e %s" % [long_axis, cross_axis]


## Os passos do guia. Cada um: {title, text}. Só entra o que o motor trouxe (casa, família, despensa, emprego, vaga, mata, o que aperta).
static func steps_for(start: Dictionary, name: String, fine: Vector2, forward: Vector2, name_of: Callable = Callable()) -> Array:
	var out: Array = []
	if start.is_empty():
		return out
	var home: Variant = start.get("home")
	var child := bool(start.get("child", false))
	out.append({"title": "Quem você é", "text": _who(start, home, name, fine, forward, child)})
	var needs := _needs(start, home, fine, forward, name_of)
	if needs != "":
		out.append({"title": "O que aperta agora", "text": needs})
	var livelihood := _livelihood(start, fine, forward, child)
	if livelihood != "":
		out.append({"title": "Trabalho e dinheiro" if not child else "O que fazer", "text": livelihood})
	out.append({"title": "Teclas que você vai usar", "text": "W A S D andam (Shift corre, Espaço pula). E faz a ação sob a mira; segure E para abrir a roda de ações. J mostra metas e diário, I a mochila. F1 lista tudo o que dá para fazer e reabre este guia."})
	return out


static func _who(start: Dictionary, home: Variant, name: String, fine: Vector2, forward: Vector2, child: bool) -> String:
	var who := "Você é %s%s." % [name if name != "" else "uma pessoa", ", ainda sem idade de adulto" if child else ""]
	if home is Dictionary:
		var place := "Sua casa fica %s. Na porta, aperte E para entrar." % where(home, fine, forward)
		if bool(home.get("inside", false)): # #260: já está dentro: nada de "entrar"; o que fazer lá dentro vem do estado do motor
			place = "Você está em casa. Ande pelos cômodos e aperte E perto de um móvel para usá-lo (segure E para a roda de ações)."
		var members: Array = home.get("members", []) if home.get("members") is Array else []
		if members.is_empty() or bool(home.get("alone", false)):
			return "%s Você mora só: ninguém vai cuidar de você, então comer, beber e dormir ficam por sua conta. %s" % [who, place]
		var names: Array[String] = []
		for member in members.slice(0, 4):
			names.append("%s (%s)" % [member.get("name", ""), _relation(String(member.get("relation", "")))])
		return "%s Você mora com %s. %s" % [who, ", ".join(names), place]
	if bool(start.get("homeless", false)):
		return "%s Você está sem teto: não há casa nem despensa para você. Arrume trabalho e dinheiro, e durma onde der." % who
	return who


static func _relation(kind: String) -> String:
	match kind:
		"spouse":
			return "cônjuge"
		"child":
			return "filho"
		"parent":
			return "pai ou mãe"
	return "mora com você"


static func _needs(start: Dictionary, home: Variant, fine: Vector2, forward: Vector2, name_of: Callable) -> String:
	var pantry := ""
	if home is Dictionary and home.get("pantry") is Array and not home.pantry.is_empty():
		var items: Array[String] = []
		for item in home.pantry.slice(0, 3):
			var label := String(name_of.call(int(item.get("resourceId", 0)))) if name_of.is_valid() else ""
			items.append("%d %s" % [int(item.get("quantity", 0)), label if label != "" else "item"])
		pantry = "A despensa de casa tem %s: chegue perto e aperte E para pegar." % ", ".join(items)
	elif home is Dictionary:
		pantry = "A despensa de casa está vazia: compre comida numa banca (E perto dela) ou trabalhe para ter dinheiro."
	match String(start.get("pressing", "none")):
		"hunger":
			return "Você está com fome. %s" % (pantry if pantry != "" else "Compre comida numa banca (E perto dela).")
		"thirst":
			return "Você está com sede. Vá até a água e aperte E para beber."
		"sleep":
			return "Você está cansado. Na cama de casa, aperte E e escolha Dormir."
		"money":
			return "Sua carteira está vazia. Trabalhe para ganhar dinheiro."
	return pantry


static func _livelihood(start: Dictionary, fine: Vector2, forward: Vector2, child: bool) -> String:
	if child:
		return "Quem ainda não é adulto não trabalha. Fique perto de casa, converse com quem estiver ao lado (E) e peça comida a quem cuida de você."
	var parts: Array[String] = []
	var job: Variant = start.get("job")
	var vacancy: Variant = start.get("vacancy")
	if job is Dictionary:
		parts.append("Seu emprego fica %s. Chegue ao posto e segure E para Trabalhar." % where(job, fine, forward))
	elif vacancy is Dictionary:
		parts.append("Você está sem emprego. Há uma vaga %s: vá até lá e fale com quem cuida do lugar (E)." % where(vacancy, fine, forward))
	else:
		parts.append("Você está sem emprego e não há vaga aberta por perto.")
	var wild: Variant = start.get("wild")
	if wild is Dictionary:
		parts.append("Outra saída: a árvore mais perto, %s. Fora da cidade, chegue a 1,5 m do tronco, segure E e escolha Cortar lenha." % where(wild, fine, forward))
	return " ".join(parts)
