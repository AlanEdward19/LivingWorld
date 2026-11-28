extends PanelContainer
## #259: a relação com a pessoa sob a mira, como o motor a diz (`relation` da oferta `person`): o nome, o estado do par ("Casados"), o humor dela,
## afeição e confiança dela por quem joga (barras), o que quem joga sente e a última coisa que aconteceu entre os dois. Só desenha; nada é calculado aqui.

const BARS := [["affection", "Afeição", Color("e07fb4")], ["trust", "Confiança", Color("4aa3df")]]

var name_label: Label
var state_label: Label
var mood_label: Label
var last_label: Label
var mine_label: Label
var bars: Dictionary = {}
var shown_id := -1


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	visible = false
	set_anchors_and_offsets_preset(Control.PRESET_CENTER_LEFT)
	offset_left = 16
	custom_minimum_size.x = 250
	grow_vertical = Control.GROW_DIRECTION_BOTH
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.05, 0.06, 0.09, 0.78)
	style.set_corner_radius_all(10)
	style.content_margin_left = 12
	style.content_margin_right = 12
	style.content_margin_top = 8
	style.content_margin_bottom = 8
	add_theme_stylebox_override("panel", style)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 3)
	add_child(box)
	name_label = _label(18, Color.WHITE)
	box.add_child(name_label)
	state_label = _label(14, Color("ffe9a6"))
	box.add_child(state_label)
	mood_label = _label(13, Color(1, 1, 1, 0.8))
	box.add_child(mood_label)
	for spec in BARS:
		var row := HBoxContainer.new()
		var text := _label(13, Color(1, 1, 1, 0.8))
		text.text = spec[1]
		text.custom_minimum_size.x = 74
		row.add_child(text)
		var bar := ProgressBar.new()
		bar.show_percentage = false
		bar.max_value = 100.0
		bar.custom_minimum_size = Vector2(150, 10)
		bar.size_flags_vertical = Control.SIZE_SHRINK_CENTER
		bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
		bar.add_theme_stylebox_override("background", _bar_style(Color(1, 1, 1, 0.12)))
		bar.add_theme_stylebox_override("fill", _bar_style(spec[2]))
		row.add_child(bar)
		box.add_child(row)
		bars[spec[0]] = bar
	mine_label = _label(12, Color(1, 1, 1, 0.6))
	box.add_child(mine_label)
	last_label = _label(13, Color("9ad0ff"))
	last_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	last_label.custom_minimum_size.x = 226
	box.add_child(last_label)


static func _label(size: int, color: Color) -> Label:
	var label := Label.new()
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return label


static func _bar_style(color: Color) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.set_corner_radius_all(4)
	return style


## O que o painel diz para uma oferta `person` com `relation`: {title, state, mood, affection, trust, mine, last}. Vazio se o motor não mandou a relação.
## `now_ms` (ms de mundo, 0 = não sei) permite dizer há quanto tempo foi o último fato.
static func lines(offer: Dictionary, now_ms: int = 0) -> Dictionary:
	if String(offer.get("kind", "")) != "person" or not offer.get("relation") is Dictionary:
		return {}
	var rel: Dictionary = offer.relation
	var last := String(rel.get("last", "")) if rel.get("last") != null else ""
	if last != "" and now_ms > 0 and rel.get("lastMs") != null:
		last += " · " + ago_text(now_ms - int(rel.lastMs))
	var name := String(offer.get("label", ""))
	return {"title": name, "state": String(rel.get("stateLabel", "")), "mood": "Humor: %s" % String(rel.get("mood", "")),
		"affection": int(rel.get("affection", 0)), "trust": int(rel.get("trust", 0)),
		"mine": "Você: afeição %d · confiança %d" % [int(rel.get("mineAffection", 0)), int(rel.get("mineTrust", 0))], "last": last}


## "agora", "há 40 s", "há 5 min", "há 2 h": o tempo de mundo desde o fato.
static func ago_text(ms: int) -> String:
	var seconds := maxi(0, ms / 1000)
	if seconds < 10:
		return "agora"
	if seconds < 90:
		return "há %d s" % seconds
	if seconds < 5400:
		return "há %d min" % int(round(seconds / 60.0))
	return "há %d h" % int(round(seconds / 3600.0))


## Mostra a relação da oferta (ou esconde, se `offer` está vazia ou não é uma pessoa com relação).
func show_for(offer: Dictionary, now_ms: int = 0) -> void:
	var data := lines(offer, now_ms)
	if data.is_empty():
		visible = false
		shown_id = -1
		return
	shown_id = int(offer.get("id", -1))
	name_label.text = data.title
	state_label.text = data.state
	state_label.visible = data.state != ""
	mood_label.text = data.mood
	bars.affection.value = data.affection
	bars.trust.value = data.trust
	mine_label.text = data.mine
	last_label.text = data.last
	last_label.visible = data.last != ""
	visible = true
