extends RefCounted
## #226: o motor modela a parede como célula inteira (1,5 m); o cliente a desenha e a colide como painel fino na borda.
## Parede de contorno: painel rente à borda de fora do prédio (o cômodo ganha a célula toda). Parede de divisória (aberta dos
## dois lados): painel no meio da célula. Ponta de divisória: tampa no meio. A porta é vão (sem painel). Só lê a planta do motor.

const THICKNESS := 0.22 ## unidades (célula = 1)
const STONE := 0
const WOOD := 1


static func is_wall(material: int) -> bool:
	return material == STONE or material == WOOD


## `cells`: célula local (Vector2i) -> material. Devolve [{cell, rect: Rect2 (unidades da célula local), material}].
static func panels(cells: Dictionary) -> Array:
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


## Vizinho aberto = existe na planta e não é parede (chão, porta, escada).
static func _open(cells: Dictionary, cell: Vector2i) -> bool:
	return cells.has(cell) and not is_wall(int(cells[cell]))
