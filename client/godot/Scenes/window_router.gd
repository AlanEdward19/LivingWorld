extends RefCounted
## Roteador das janelas de informação (cérebro, narrativa, economia, tecnologias, console): elas convivem em vez de
## uma fechar a outra. As abertas se encaixam lado a lado na área central (1 = área toda, 2 = colunas, 3-4 = 2x2,
## 5 = 3+2); arrastar pela faixa do título solta a janela (passa a flutuar onde o usuário largou). Clicar traz para
## a frente; Esc fecha a da frente.

const HEADER_HEIGHT := 34.0
const MARGIN_LEFT := 310.0
const MARGIN_RIGHT := 316.0
const MARGIN_TOP := 130.0
const MARGIN_BOTTOM := 272.0
const GAP := 6.0
const MIN_HEIGHT := 220.0

var windows: Array = [] ## todas as registradas
var order: Array = [] ## abertas, da mais antiga para a da frente
var floating: Dictionary = {} ## janela -> true quando o usuário arrastou
var host: Control ## onde as janelas moram (para medir a área)
var _dragging = null


func register(window: Control) -> void:
	windows.append(window)
	window.set_anchors_preset(Control.PRESET_TOP_LEFT)
	window.gui_input.connect(func(event: InputEvent): _on_input(window, event))
	if window.has_signal("closed"):
		window.closed.connect(func(): closed(window))


## Depois que uma janela abriu: entra no encaixe (ou mantém o lugar, se flutua) e vem para a frente.
func opened(window: Control) -> void:
	order.erase(window)
	order.append(window)
	window.move_to_front()
	layout()


func closed(window: Control) -> void:
	order.erase(window)
	layout()


func open_windows() -> Array:
	return order.filter(func(w): return w.is_open)


func top():
	var open := open_windows()
	return open.back() if not open.is_empty() else null


## Fecha a janela da frente (Esc); false se não havia nenhuma.
func close_top() -> bool:
	var window = top()
	if window == null:
		return false
	if window.has_method("end_conversation"):
		window.end_conversation()
	else:
		window.close()
	closed(window)
	return true


func area() -> Rect2:
	var size := Vector2(1600, 900)
	if host != null and host.size.x >= 800.0 and host.size.y >= 500.0:
		size = host.size
	return Rect2(MARGIN_LEFT, MARGIN_TOP, size.x - MARGIN_LEFT - MARGIN_RIGHT, size.y - MARGIN_TOP - MARGIN_BOTTOM)


## Encaixe: as abertas entram em fileiras da esquerda para a direita pela largura mínima de cada uma (a sobra da
## fileira é dividida entre elas); cabem tantas fileiras quanto a altura mínima deixa. Se não couberem, caem em
## cascata (cada título visível a 34 px do anterior) para nenhuma esconder a outra por inteiro.
func layout() -> void:
	var docked: Array = open_windows().filter(func(w): return not floating.has(w))
	if docked.is_empty():
		return
	var rect := area()
	var rows: Array = [[]]
	var used := 0.0
	for window in docked:
		var need := _min_width(window)
		if not rows.back().is_empty() and used + GAP + need > rect.size.x:
			rows.append([])
			used = 0.0
		used += (GAP if not rows.back().is_empty() else 0.0) + need
		rows.back().append(window)
	var max_rows := maxi(1, int((rect.size.y + GAP) / (MIN_HEIGHT + GAP)))
	if rows.size() > max_rows:
		_cascade(docked, rect)
		return
	var row_height := (rect.size.y - GAP * (rows.size() - 1)) / rows.size()
	for r in rows.size():
		var row: Array = rows[r]
		var widths: Array = row.map(func(w): return _min_width(w))
		var total: float = widths.reduce(func(acc, v): return acc + v, 0.0) + GAP * (row.size() - 1)
		var extra: float = maxf(rect.size.x - total, 0.0) / row.size()
		var x := rect.position.x
		for i in row.size():
			var window: Control = row[i]
			window.position = Vector2(x, rect.position.y + r * (row_height + GAP))
			window.size = Vector2(widths[i] + extra, row_height)
			x += widths[i] + extra + GAP


func _min_width(window: Control) -> float:
	return minf(maxf(window.get_combined_minimum_size().x, 300.0), area().size.x)


func _cascade(docked: Array, rect: Rect2) -> void:
	for i in docked.size():
		var window: Control = docked[i]
		window.position = rect.position + Vector2(i * HEADER_HEIGHT, i * HEADER_HEIGHT)
		window.size = Vector2(maxf(rect.size.x * 0.7, _min_width(window)), rect.size.y * 0.7)


func _on_input(window: Control, event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		if event.pressed:
			if order.has(window):
				order.erase(window)
				order.append(window)
			window.move_to_front()
			if event.position.y <= HEADER_HEIGHT:
				_dragging = window
		elif _dragging == window:
			_dragging = null
	elif event is InputEventMouseMotion and _dragging == window:
		drag(window, event.relative)


## Arrasta a janela (e a solta do encaixe); as demais se reorganizam.
func drag(window: Control, delta: Vector2) -> void:
	var was_docked := not floating.has(window)
	floating[window] = true
	window.position += delta
	if was_docked:
		layout()


## Devolve a janela ao encaixe (duplo clique no título, ou quando fecha).
func dock(window: Control) -> void:
	floating.erase(window)
	layout()
