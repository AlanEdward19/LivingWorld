extends "res://tests/test_base.gd"
## #226: a parede é aresta entre células (o motor diz onde); o cliente desenha e colide só ali, e a previsão pela máscara do motor concorda.

const WallPanels := preload("res://Scenes/wall_panels.gd")
const GroundGrid := preload("res://Scenes/ground_grid.gd")
const PlayerBody := preload("res://Scenes/player_body.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const DT := 1.0 / 60.0
const DIRS: Array[Vector2i] = [Vector2i(0, -1), Vector2i(-1, 0), Vector2i(1, 0), Vector2i(0, 1)]


func _seg(a: Vector2i, b: Vector2i, material: int) -> Dictionary:
	return {"a": {"x": a.x, "y": a.y}, "b": {"x": b.x, "y": b.y}, "material": material}


## Casa 3x3 como o motor manda: contorno em arestas (porta aberta embaixo de (1,2)) e uma parede de dentro entre (0,0) e (1,0), com porta de dentro
## entre (0,1) e (1,1)... que fica aberta (sem trecho de parede).
func _plan() -> Dictionary:
	var cells := {}
	var edges: Array = []
	var doors: Array = []
	for y in 3:
		for x in 3:
			cells[Vector2i(x, y)] = 3
	for y in 3:
		for x in 3:
			for d in DIRS:
				var n: Vector2i = Vector2i(x, y) + d
				if cells.has(n):
					continue
				var a: Vector2i = Vector2i(x, y)
				var b: Vector2i = n
				if b.y < a.y or (b.y == a.y and b.x < a.x):
					a = n
					b = Vector2i(x, y)
				if x == 1 and y == 2 and d == Vector2i(0, 1):
					doors.append(_seg(a, b, 2))
				else:
					edges.append(_seg(a, b, 1))
	edges.append(_seg(Vector2i(0, 0), Vector2i(1, 0), 1)) # parede de dentro (linha vertical x=1, entre as duas primeiras da linha de cima)
	return {"cells": cells, "walls": edges, "doors": doors}


func test_each_wall_edge_gets_one_thin_panel_on_its_line_and_the_door_gap_none() -> void:
	var plan := _plan()
	var panels := WallPanels.panels(plan.walls, plan.cells)
	eq(panels.size(), plan.walls.size(), "um painel por aresta de parede")
	for panel in panels:
		var rect: Rect2 = panel.rect
		check(minf(rect.size.x, rect.size.y) <= WallPanels.THICKNESS + 0.001, "painel fino (%s)" % rect.size)
		check(Rect2(0, 0, 3, 3).encloses(rect), "nunca invade a rua nem a vizinha de fora (%s)" % rect)
	var inner: Dictionary = {}
	for panel in panels:
		if panel.outward == Vector2i.ZERO:
			inner = panel
	check(not inner.is_empty(), "a parede de dentro não tem lado de fora")
	check(absf(inner.rect.get_center().x - 1.0) < 0.001, "a de dentro fica centrada na aresta (x=%s)" % inner.rect.get_center().x)
	var door := WallPanels.openings(plan.doors, plan.cells)
	eq(door.size(), 1, "uma abertura de porta")
	eq(door[0].outward, Vector2i(0, 1), "a porta abre para baixo")
	for panel in panels:
		check(not (panel.rect as Rect2).intersects(Rect2(1.1, 2.7, 0.8, 0.3)), "nenhum painel no vão da porta (%s)" % panel.rect)


## A máscara que o motor manda, montada da mesma regra (aresta fechada bloqueia o passo, ortogonal e diagonal): onde ela fecha há painel no
## caminho entre os centros; onde abre não há.
func test_the_mask_and_the_drawn_panels_agree_on_every_edge() -> void:
	var plan := _plan()
	var panels := WallPanels.panels(plan.walls, plan.cells)
	var closed := {}
	for wall in plan.walls:
		closed[_key(WallPanels.cell(wall.a), WallPanels.cell(wall.b))] = true
	var checked := 0
	for cell: Vector2i in plan.cells:
		for d in DIRS:
			var next: Vector2i = cell + d
			if not plan.cells.has(next):
				continue
			var shut: bool = closed.has(_key(cell, next))
			var from := Vector2(cell) + Vector2(0.5, 0.5)
			var to := Vector2(next) + Vector2(0.5, 0.5)
			var hit := false
			for panel in panels:
				var rect: Rect2 = panel.rect
				if rect.intersects(Rect2(minf(from.x, to.x) - 0.01, minf(from.y, to.y) - 0.01, absf(to.x - from.x) + 0.02, absf(to.y - from.y) + 0.02)):
					hit = true
			eq(hit, shut, "painel entre %s e %s só onde a aresta é parede" % [cell, next])
			checked += 1
	check(checked >= 24, "o teste conferiu as arestas de dentro (%d)" % checked)


## `GroundGrid` com a máscara do motor: o passo por cima da parede de dentro é barrado e pela abertura passa (o cliente não tem regra própria).
func test_the_prediction_by_the_engine_mask_never_crosses_an_edge() -> void:
	var plan := _plan()
	var closed := {}
	for wall in plan.walls:
		closed[_key(WallPanels.cell(wall.a), WallPanels.cell(wall.b))] = true
	var house := Vector2i(40, 40) # a origem da casa no mundo
	var window := Vector2i(36, 36) # o canto de cima à esquerda da janela da máscara
	var side := 9
	var mask: Array = []
	for j in side:
		for i in side:
			var local: Vector2i = window + Vector2i(i, j) - house
			var bits := 0
			for dy in range(-1, 2):
				for dx in range(-1, 2):
					if dx == 0 and dy == 0:
						continue
					var to: Vector2i = local + Vector2i(dx, dy)
					var ok: bool = plan.cells.has(local) and plan.cells.has(to)
					if ok and (dx == 0 or dy == 0):
						ok = not closed.has(_key(local, to))
					elif ok:
						var c1: Vector2i = local + Vector2i(dx, 0)
						var c2: Vector2i = local + Vector2i(0, dy)
						ok = not (closed.has(_key(local, c1)) or closed.has(_key(c1, to)) or closed.has(_key(local, c2)) or closed.has(_key(c2, to)))
					if ok:
						bits |= 1 << ((dy + 1) * 3 + (dx + 1))
			mask.append(bits)
	var grid = GroundGrid.from_body({"x": window.x, "y": window.y, "size": side, "floor": 0, "mask": mask})
	check(grid.is_valid(), "a máscara do motor é lida")
	check(not grid.can_step(house, house + Vector2i(1, 0)), "a parede de dentro barra o passo de (0,0) para (1,0)")
	check(grid.can_step(house + Vector2i(0, 1), house + Vector2i(1, 1)), "a abertura entre (0,1) e (1,1) deixa passar")
	check(not grid.can_step(house, house + Vector2i(1, 1)), "a diagonal não corta a ponta da parede")
	check(grid.can_step(house + Vector2i(2, 1), house + Vector2i(2, 2)), "dentro do cômodo livre o passo é permitido")


func _key(a: Vector2i, b: Vector2i) -> String:
	var first := a
	var second := b
	if b.y < a.y or (b.y == a.y and b.x < a.x):
		first = b
		second = a
	return "%d,%d|%d,%d" % [first.x, first.y, second.x, second.y]


## O interior desenha as paredes onde o motor diz e o corpo para no painel de dentro, mas passa pela abertura.
func test_the_interior_draws_the_engine_walls_and_the_body_stops_at_the_inner_wall_but_passes_the_gap() -> void:
	var plan := _plan()
	var cells: Array = []
	for cell: Vector2i in plan.cells:
		cells.append({"cell": {"x": cell.x, "y": cell.y}, "material": 2 if cell == Vector2i(1, 2) else 3})
	var view := InteriorView.new()
	view.show_interior({"building": 4, "floors": 1, "layout": [{"level": 0, "cells": cells, "walls": plan.walls, "doors": plan.doors}], "occupants": []},
		Vector2i(10, 10), ActionLabels.new())
	var rects: Array = view._panels[0]
	eq(rects.size(), plan.walls.size(), "um painel por aresta que o motor mandou")
	var body := PlayerBody.new()
	body.place(Vector2(10.5, 10.5)) # célula (0,0): a parede de dentro fica a leste dela
	body.set_neighborhood(view.obstacles_near(body.pos, 4.0), [])
	for i in 120:
		body.advance(Vector2.RIGHT, 3.0, DT)
	check(body.pos.x < 11.0, "a parede de dentro segura o corpo (x=%s)" % body.pos.x)
	body.place(Vector2(10.5, 11.5)) # célula (0,1): entre ela e (1,1) não há parede
	body.set_neighborhood(view.obstacles_near(body.pos, 4.0), [])
	for i in 120:
		body.advance(Vector2.RIGHT, 3.0, DT)
	check(body.pos.x > 11.3, "pela abertura o corpo passa (x=%s)" % body.pos.x)
	view.free()


## Os cômodos do motor (tipo e células) ganham um tom de piso por tipo; o mesmo tipo, o mesmo tom; as paredes de dentro vêm de `rooms`.
func test_rooms_from_the_engine_tint_the_floor_by_kind_and_bring_their_own_walls() -> void:
	var plan := _plan()
	var cells: Array = []
	for cell: Vector2i in plan.cells:
		cells.append({"cell": {"x": cell.x, "y": cell.y}, "material": 2 if cell == Vector2i(1, 2) else 3})
	var outer: Array = []
	for wall in plan.walls:
		if not (wall.a.x == 0 and wall.a.y == 0 and wall.b.x == 1 and wall.b.y == 0): # a de dentro vem de `rooms`, não da planta
			outer.append(wall)
	var rooms := [{"rooms": [
		{"kind": "sala", "cells": [{"x": 1, "y": 0}, {"x": 2, "y": 0}, {"x": 1, "y": 1}, {"x": 2, "y": 1}, {"x": 0, "y": 1}, {"x": 0, "y": 2}, {"x": 1, "y": 2}, {"x": 2, "y": 2}]},
		{"kind": "quarto", "cells": [{"x": 0, "y": 0}]}],
		"walls": [_seg(Vector2i(0, 0), Vector2i(1, 0), 1)], "doors": []}]
	var view := InteriorView.new()
	view.show_interior({"building": 4, "floors": 1, "layout": [{"level": 0, "cells": cells, "walls": outer, "doors": plan.doors}], "rooms": rooms, "occupants": []},
		Vector2i(10, 10), ActionLabels.new())
	eq(view._panels[0].size(), plan.walls.size(), "as paredes de fora da planta mais a de dentro dos cômodos")
	check(view._tint[Vector2i(0, 0)] != view._tint[Vector2i(1, 1)], "o quarto e a sala têm tons diferentes")
	eq(view._tint[Vector2i(1, 1)], view._tint[Vector2i(2, 2)], "o mesmo tipo, o mesmo tom")
	view.free()


## O prédio visto de fora: parede nas arestas do motor, vão da porta com verga e folha, e o corpo sai pela porta e para ao lado dela.
func test_a_world_building_draws_the_edge_walls_and_the_door_opening() -> void:
	var WorldView := preload("res://Scenes/world_view.gd")
	var view := WorldView.new()
	var plan := _plan()
	var footprint: Array = []
	for cell: Vector2i in plan.cells:
		footprint.append({"cell": cell, "material": 2 if cell == Vector2i(1, 2) else 3})
	var walls: Array = []
	for wall in plan.walls:
		walls.append({"a": WallPanels.cell(wall.a), "b": WallPanels.cell(wall.b), "material": int(wall.material)})
	var doors: Array = []
	for door in plan.doors:
		doors.append({"a": WallPanels.cell(door.a), "b": WallPanels.cell(door.b), "material": 2})
	var node: Node3D = view._make_building({"id": 1, "type": -1, "cell": Vector2i(20, 20), "floors": 1, "orientation": 0,
		"footprint": footprint, "walls": walls, "doors": doors})
	var solid: Array = node.get_meta("solid")
	eq(solid.size(), walls.size(), "um painel sólido por aresta de parede")
	var obstacles: Array = []
	for wall: AABB in solid:
		check(minf(wall.size.x, wall.size.z) < 0.3, "painel fino (%s)" % wall.size)
		obstacles.append([Rect2(wall.position.x, wall.position.z, wall.size.x, wall.size.z), wall.size.y])
	check(node.find_child("Door", true, false) != null, "a folha da porta no vão")
	var body := PlayerBody.new()
	body.place(Vector2(21.5, 22.5)) # dentro, na célula da porta (1,2)
	body.set_neighborhood(obstacles, [])
	for i in 120:
		body.advance(Vector2.DOWN, 2.0, DT)
	check(body.pos.y > 23.4, "sai pela abertura da porta (y=%s)" % body.pos.y)
	body.place(Vector2(22.5, 22.5)) # ao lado da porta
	body.set_neighborhood(obstacles, [])
	for i in 120:
		body.advance(Vector2.DOWN, 2.0, DT)
	check(body.pos.y < 23.0, "ao lado da porta a parede segura (y=%s)" % body.pos.y)
	view.free()
