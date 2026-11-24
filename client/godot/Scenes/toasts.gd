extends VBoxContainer
## Avisos de eventos notáveis do mundo (texto vem do servidor em `events[].label`), com histórico.

const MAX_VISIBLE := 5
const LIFETIME := 7.0
## AD-197p: no modo jogador os avisos são discretos: menores, no máximo três, centralizados acima do rodapé.
var compact := false

var history: Array[String] = []
var _items: Array = [] ## [{label: Label, born: float}]
var _clock := 0.0


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	alignment = BoxContainer.ALIGNMENT_END


## `event` marca aviso de acontecimento do mundo (o modo jogador limpa esses; os avisos do motor ao jogador ficam).
func push(text: String, event: bool = false) -> void:
	history.append(text)
	var label := Label.new()
	label.text = "• " + text
	label.add_theme_color_override("font_color", Color("ffe9a6") if not compact else Color(1, 1, 1, 0.9))
	label.add_theme_font_size_override("font_size", 16 if not compact else 14)
	label.add_theme_constant_override("outline_size", 0 if not compact else 4)
	label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.6))
	if compact:
		label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(label)
	_items.append({"label": label, "born": _clock, "event": event})
	while _items.size() > (MAX_VISIBLE if not compact else 3):
		_items.pop_front().label.queue_free()


## AD-197p: tira da tela os avisos de acontecimento (o modo jogador começa sem eles sobre a cena).
func clear_events() -> void:
	for item in _items.duplicate():
		if item.get("event", false):
			item.label.queue_free()
			_items.erase(item)


func visible_texts() -> Array:
	return _items.map(func(item): return item.label.text.trim_prefix("• "))


func _process(delta: float) -> void:
	_clock += delta
	for item in _items.duplicate():
		var age: float = _clock - item.born
		item.label.modulate.a = clampf((LIFETIME - age) / 1.5, 0.0, 1.0)
		if age >= LIFETIME:
			item.label.queue_free()
			_items.erase(item)
