extends RefCounted
## #226: o motor modela a parede como ARESTA entre duas células vizinhas (a célula de cada lado é piso inteiro): `walls` = trechos
## `{a, b, material}` (a e b vizinhas de 4, a antes de b por linha e coluna). O cliente só desenha e colide onde o motor diz.
## Parede de fora (um lado fora da planta): painel rente à borda, por dentro (nunca invade a rua). Parede de dentro (cômodo): painel
## centrado na aresta, cortado pela caixa da planta. A porta é a aresta que o motor não fecha: sem painel (`doors` traz a abertura).

const THICKNESS := 0.22 ## unidades (célula = 1)
const STONE := 0
const WOOD := 1
const DOOR := 2


static func is_wall(material: int) -> bool:
	return material == STONE or material == WOOD


## `{x, y}` do JSON do motor ou Vector2i.
static func cell(value: Variant) -> Vector2i:
	if value is Vector2i:
		return value
	return Vector2i(int(value.x), int(value.y))


## `walls`: trechos do motor ({a, b, material}, a/b como {x,y} ou Vector2i). `cells`: células da planta (Vector2i -> qualquer), na mesma
## origem que `offset` leva as coordenadas locais de a/b (0 para a planta local, a origem do prédio para células absolutas).
## Devolve [{cell: a célula de dentro, rect: Rect2 (unidades de célula, absolutas), material, outward: Vector2i (para fora; ZERO se de dentro)}].
static func panels(walls: Array, cells: Dictionary, offset: Vector2i = Vector2i.ZERO) -> Array:
	var out: Array = []
	if cells.is_empty():
		return out
	var lo := Vector2(1e9, 1e9)
	var hi := Vector2(-1e9, -1e9)
	for c: Vector2i in cells:
		lo = Vector2(minf(lo.x, c.x), minf(lo.y, c.y))
		hi = Vector2(maxf(hi.x, c.x + 1.0), maxf(hi.y, c.y + 1.0))
	var bounds := Rect2(lo, hi - lo)
	var half := THICKNESS / 2.0
	for wall in walls:
		var a: Vector2i = cell(wall.a) + offset
		var b: Vector2i = cell(wall.b) + offset
		var a_in := cells.has(a)
		var b_in := cells.has(b)
		var horizontal := a.x == b.x # vizinhas na vertical: a aresta é uma linha horizontal
		var line := float(b.y) if horizontal else float(b.x)
		var from_inside := 0.0 # 0 = centrada; -1 = faixa antes da linha (a dentro); +1 = depois (b dentro)
		if a_in and not b_in:
			from_inside = -1.0
		elif b_in and not a_in:
			from_inside = 1.0
		var low := line - half
		var high := line + half
		if from_inside < 0.0:
			low = line - THICKNESS
			high = line
		elif from_inside > 0.0:
			low = line
			high = line + THICKNESS
		var rect: Rect2
		if horizontal:
			var x0 := float(a.x) - (half if from_inside == 0.0 else 0.0)
			var w := 1.0 + (THICKNESS if from_inside == 0.0 else 0.0)
			rect = Rect2(x0, low, w, high - low)
		else:
			var y0 := float(a.y) - (half if from_inside == 0.0 else 0.0)
			var h := 1.0 + (THICKNESS if from_inside == 0.0 else 0.0)
			rect = Rect2(low, y0, high - low, h)
		var clipped := rect.intersection(bounds)
		if clipped.size.x <= 0.0 or clipped.size.y <= 0.0:
			continue
		var inner := a if a_in else b
		var outward := Vector2i.ZERO
		if from_inside < 0.0:
			outward = b - a
		elif from_inside > 0.0:
			outward = a - b
		out.append({"cell": inner, "rect": clipped, "material": int(wall.get("material", STONE)), "outward": outward})
	return out


## O vão da porta como retângulo (a mesma geometria do painel que ele deixa de ser), para a verga e a folha.
static func openings(doors: Array, cells: Dictionary, offset: Vector2i = Vector2i.ZERO) -> Array:
	return panels(doors, cells, offset)


## Legado (servidor sem arestas de parede, placeholder do cliente): a parede é a célula de material de parede; o painel fino fica rente à
## borda de fora, e a divisória (aberta dos dois lados) no meio. `cells`: célula (Vector2i) -> material.
static func legacy_panels(cells: Dictionary) -> Array:
	var out: Array = []
	var half := THICKNESS / 2.0
	for cell: Vector2i in cells:
		var material := int(cells[cell])
		if not is_wall(material):
			continue
		var x := float(cell.x)
		var y := float(cell.y)
		var left_open := _open(cells, cell + Vector2i(-1, 0))
		var right_open := _open(cells, cell + Vector2i(1, 0))
		var up_open := _open(cells, cell + Vector2i(0, -1))
		var down_open := _open(cells, cell + Vector2i(0, 1))
		var made := false
		if not cells.has(cell + Vector2i(-1, 0)):
			out.append({"cell": cell, "rect": Rect2(x, y, THICKNESS, 1.0), "material": material})
			made = true
		if not cells.has(cell + Vector2i(1, 0)):
			out.append({"cell": cell, "rect": Rect2(x + 1.0 - THICKNESS, y, THICKNESS, 1.0), "material": material})
			made = true
		if not cells.has(cell + Vector2i(0, -1)):
			out.append({"cell": cell, "rect": Rect2(x, y, 1.0, THICKNESS), "material": material})
			made = true
		if not cells.has(cell + Vector2i(0, 1)):
			out.append({"cell": cell, "rect": Rect2(x, y + 1.0 - THICKNESS, 1.0, THICKNESS), "material": material})
			made = true
		if left_open and right_open:
			out.append({"cell": cell, "rect": Rect2(x + 0.5 - half, y, THICKNESS, 1.0), "material": material})
		elif up_open and down_open:
			out.append({"cell": cell, "rect": Rect2(x, y + 0.5 - half, 1.0, THICKNESS), "material": material})
		elif not made and (left_open or right_open):
			out.append({"cell": cell, "rect": Rect2(x + 0.5 - half, y, THICKNESS, 1.0), "material": material})
		elif not made and (up_open or down_open):
			out.append({"cell": cell, "rect": Rect2(x, y + 0.5 - half, 1.0, THICKNESS), "material": material})
	return out


static func _open(cells: Dictionary, cell: Vector2i) -> bool:
	return cells.has(cell) and not is_wall(int(cells[cell]))
