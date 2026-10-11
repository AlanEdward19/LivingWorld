extends VBoxContainer
## Avisos de eventos notáveis do mundo (texto vem do servidor em `events[].label`), com histórico.

const MAX_VISIBLE := 5
const LIFETIME := 7.0

var history: Array[String] = []
var _items: Array = [] ## [{label: Label, born: float}]
var _clock := 0.0


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	alignment = BoxContainer.ALIGNMENT_END


func push(text: String) -> void:
	history.append(text)
	var label := Label.new()
	label.text = "• " + text
	label.add_theme_color_override("font_color", Color("ffe9a6"))
	label.add_theme_font_size_override("font_size", 16)
	add_child(label)
	_items.append({"label": label, "born": _clock})
	while _items.size() > MAX_VISIBLE:
		_items.pop_front().label.queue_free()


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
