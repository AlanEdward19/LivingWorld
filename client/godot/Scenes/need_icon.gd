extends Control
## #260: o ícone pequeno de uma necessidade e a seta de tendência, desenhados por código (a fonte do jogo não tem emoji). O nome do ícone vem do
## catálogo do motor (`icon` da necessidade) ou, para as quatro de sempre, de `DEFAULT_ICONS`; nome desconhecido desenha um ponto.
## `trend`: -2..2 (queda rápida, queda, estável, subida, subida rápida): o motor diz os pontos por hora e `level_for` os converte.

const DEFAULT_ICONS := {"health": "heart", "hunger": "food", "thirst": "drop", "sleep": "moon", "social": "chat"}
const FLAT_BELOW := 0.5 ## pontos por hora de mundo: menos que isto a barra está parada
const FAST_FROM := 6.0 ## e daqui a mudança é rápida (duas setas)

var kind := ""
var color := Color.WHITE
var trend_mode := false
var trend := 0


static func level_for(per_hour: float) -> int:
	var size := absf(per_hour)
	if size < FLAT_BELOW:
		return 0
	var level := 2 if size >= FAST_FROM else 1
	return level if per_hour > 0.0 else -level


## "Subindo", "Caindo devagar"... para a dica ao passar o mouse e para os testes.
static func trend_text(level: int) -> String:
	match level:
		2:
			return "subindo rápido"
		1:
			return "subindo"
		-1:
			return "caindo"
		-2:
			return "caindo rápido"
	return "estável"


func _init(icon_kind: String = "", tint: Color = Color.WHITE, is_trend: bool = false) -> void:
	kind = icon_kind
	color = tint
	trend_mode = is_trend
	custom_minimum_size = Vector2(14, 14) if is_trend else Vector2(16, 16)
	mouse_filter = Control.MOUSE_FILTER_IGNORE


func set_trend(level: int) -> void:
	if level == trend:
		return
	trend = level
	queue_redraw()


func _draw() -> void:
	if trend_mode:
		_draw_trend()
	else:
		_draw_icon()


func _draw_trend() -> void:
	if trend == 0:
		draw_rect(Rect2(3, 6, 8, 2), Color(1, 1, 1, 0.45))
		return
	var up := trend > 0
	var tint := Color("7fd68a") if up else Color("ff8f7f")
	for i in absi(trend):
		var y := 3.0 + 5.0 * i if up else 11.0 - 5.0 * i
		var points := PackedVector2Array([Vector2(7, y - 3.0), Vector2(11, y + 2.0), Vector2(3, y + 2.0)]) if up \
			else PackedVector2Array([Vector2(7, y + 3.0), Vector2(3, y - 2.0), Vector2(11, y - 2.0)])
		draw_colored_polygon(points, tint)


func _draw_icon() -> void:
	var c := Vector2(8, 8)
	match kind:
		"heart":
			draw_circle(Vector2(5.5, 6), 3.2, color)
			draw_circle(Vector2(10.5, 6), 3.2, color)
			draw_colored_polygon(PackedVector2Array([Vector2(2.4, 7.2), Vector2(13.6, 7.2), Vector2(8, 14)]), color)
		"food":
			draw_circle(Vector2(8, 9.5), 5.0, color)
			draw_rect(Rect2(7.4, 1.5, 1.4, 3.5), Color("8a6a43"))
		"drop":
			draw_circle(Vector2(8, 10), 4.2, color)
			draw_colored_polygon(PackedVector2Array([Vector2(8, 1.5), Vector2(12, 8.5), Vector2(4, 8.5)]), color)
		"moon":
			draw_circle(c, 6.0, color)
			draw_circle(Vector2(11, 6), 5.0, Color(0.05, 0.06, 0.09, 1.0))
		"soap":
			draw_circle(Vector2(5, 10), 3.0, color)
			draw_circle(Vector2(11, 6.5), 2.4, color)
			draw_circle(Vector2(9.5, 12.5), 1.6, color)
		"star":
			var pts := PackedVector2Array()
			for i in 10:
				var r := 7.0 if i % 2 == 0 else 3.0
				var a := -PI / 2.0 + TAU * float(i) / 10.0
				pts.append(c + Vector2(cos(a), sin(a)) * r)
			draw_colored_polygon(pts, color)
		"chat":
			draw_rect(Rect2(2, 2.5, 12, 8), color)
			draw_colored_polygon(PackedVector2Array([Vector2(4, 10.4), Vector2(9, 10.4), Vector2(4, 14.5)]), color)
		_:
			draw_circle(c, 3.0, color)
