extends "res://tests/test_base.gd"
## #227: de qualquer célula andável (piso, escada, porta) de uma casa o corpo do jogador chega à porta e sai pela rua. Plantas reais do
## motor (cidade-media, seed 2): paredes = célula inteira (0/1), porta 2, piso 3, escada 4. A colisão é a do cliente: painéis finos
## de `wall_panels.gd` contra o círculo de `player_body.gd` (RADIUS). O motor decide as células; aqui prova-se que o desenho não fecha passagem.

const InteriorView := preload("res://Scenes/interior_view.gd")
const PlayerBody := preload("res://Scenes/player_body.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const PLANS := {
	"4x4 térreo": ["1111", "1431", "1331", "1121"],
	"4x4 sem escada": ["1111", "1331", "1331", "1121"],
	"3x3 pedra": ["000", "030", "020"],
	"3x3 porta embaixo": ["111", "142", "111"],
	"3x3 porta em cima": ["121", "141", "111"],
	"3x3 porta à esquerda": ["111", "241", "111"],
	"3x3 porta embaixo ao centro": ["111", "141", "121"],
}
const ORIGIN := Vector2i(20, 30)
const STEP := 0.05


func _info(rows: Array) -> Dictionary:
	var cells: Array = []
	for y in rows.size():
		for x in String(rows[y]).length():
			cells.append({"cell": {"x": x, "y": y}, "material": int(String(rows[y])[x])})
	return {"building": 2, "floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": []}


func _material(rows: Array, cell: Vector2i) -> int:
	if cell.y < 0 or cell.y >= rows.size() or cell.x < 0 or cell.x >= String(rows[cell.y]).length():
		return -1
	return int(String(rows[cell.y])[cell.x])


## Centro de uma célula local (absoluta, em células do mundo).
func _center(cell: Vector2i) -> Vector2:
	return Vector2(ORIGIN + cell) + Vector2(0.5, 0.5)


func _free(point: Vector2, panels: Array) -> bool:
	for rect: Rect2 in panels:
		var nearest := Vector2(clampf(point.x, rect.position.x, rect.end.x), clampf(point.y, rect.position.y, rect.end.y))
		if point.distance_to(nearest) < PlayerBody.RADIUS - 0.0001:
			return false
	return true


## Os pontos (grade de STEP) alcançáveis do corpo a partir de `start`, sem atravessar painel; devolve o mapa ponto -> pai.
func _reach(start: Vector2, panels: Array, limit: Rect2) -> Dictionary:
	var parents := {Vector2i.ZERO: Vector2i.ZERO}
	var queue: Array[Vector2i] = [Vector2i.ZERO]
	var head := 0
	while head < queue.size():
		var here := queue[head]
		head += 1
		for dy in range(-1, 2):
			for dx in range(-1, 2):
				if dx == 0 and dy == 0:
					continue
				var next := here + Vector2i(dx, dy)
				if parents.has(next):
					continue
				var point := start + Vector2(next) * STEP
				if not limit.has_point(point) or not _free(point, panels):
					continue
				if dx != 0 and dy != 0 and (not _free(start + Vector2(here.x + dx, here.y) * STEP, panels) or not _free(start + Vector2(here.x, here.y + dy) * STEP, panels)):
					continue # na diagonal, sem cortar o canto
				parents[next] = here
				queue.append(next)
	return parents


func _door_of(rows: Array) -> Vector2i:
	for y in rows.size():
		for x in String(rows[y]).length():
			if _material(rows, Vector2i(x, y)) == 2:
				return Vector2i(x, y)
	return Vector2i(-1, -1)


## Para fora: a direção da porta até a borda da planta.
func _outward(rows: Array, door: Vector2i) -> Vector2i:
	if door.y == rows.size() - 1:
		return Vector2i.DOWN
	if door.y == 0:
		return Vector2i.UP
	if door.x == 0:
		return Vector2i.LEFT
	return Vector2i.RIGHT


func test_from_every_walkable_cell_the_door_is_reachable_and_leads_outside() -> void:
	for name in PLANS:
		var rows: Array = PLANS[name]
		var view := InteriorView.new()
		view.show_interior(_info(rows), ORIGIN, ActionLabels.new())
		var panels: Array = view._panels[0]
		var door := _door_of(rows)
		var outside := _center(door) + Vector2(_outward(rows, door)) * 1.2
		var limit := Rect2(Vector2(ORIGIN) - Vector2(2, 2), Vector2(rows[0].length(), rows.size()) + Vector2(4, 4))
		var walkable := 0
		for y in rows.size():
			for x in String(rows[y]).length():
				var cell := Vector2i(x, y)
				if _material(rows, cell) < 2:
					continue
				walkable += 1
				var start := _center(cell)
				check(_free(start, panels), "%s: o centro da célula %s é livre" % [name, cell])
				var reach := _reach(start, panels, limit)
				var target := Vector2i(((outside - start) / STEP).round())
				check(reach.has(target), "%s: de %s se chega à rua pela porta %s" % [name, cell, door])
		check(walkable >= 2, "%s: há células andáveis" % name)
		view.free()


## Com o `player_body` de verdade, guiado até a porta e além, o corpo sai da planta (e não fica preso em quina nem em divisória).
func test_the_body_steers_out_through_the_door_from_every_walkable_cell() -> void:
	for name in PLANS:
		var rows: Array = PLANS[name]
		var view := InteriorView.new()
		view.show_interior(_info(rows), ORIGIN, ActionLabels.new())
		var door := _door_of(rows)
		var out := _outward(rows, door)
		var goals: Array[Vector2] = [_center(door), _center(door) + Vector2(out) * 1.2]
		for y in rows.size():
			for x in String(rows[y]).length():
				var cell := Vector2i(x, y)
				if _material(rows, cell) < 2:
					continue
				var body := PlayerBody.new()
				body.place(_center(cell))
				var goal_index := 0
				for frame in 1200:
					body.set_neighborhood(view.obstacles_near(body.pos, 4.0), [])
					var to_goal := goals[goal_index] - body.pos
					if to_goal.length() < 0.15:
						if goal_index == goals.size() - 1:
							break
						goal_index += 1
						continue
					body.advance(to_goal.normalized(), 1.5, 1.0 / 60.0)
				var beyond := Vector2(ORIGIN + door) + Vector2(0.5, 0.5) + Vector2(out) * 1.0
				check(goal_index == goals.size() - 1 and body.pos.distance_to(goals[1]) < 0.3, "%s: do centro de %s o corpo sai pela porta (parou em %s)" % [name, cell, body.pos])
				var crossed: bool = (body.pos - beyond).dot(Vector2(out)) > -0.01
				check(crossed, "%s: o corpo passou do limite da planta (%s)" % [name, body.pos])
		view.free()


## O canto noroeste (onde o motor pôs o morador preso) é parede na planta: o app o sabe e avisa; o piso e a porta não são.
func test_the_view_knows_which_cells_are_walls() -> void:
	var rows: Array = PLANS["4x4 térreo"]
	var view := InteriorView.new()
	view.show_interior(_info(rows), ORIGIN, ActionLabels.new())
	check(view.is_wall_cell(ORIGIN), "o canto noroeste é parede")
	check(not view.is_wall_cell(ORIGIN + Vector2i(2, 3)), "a porta não é parede")
	check(not view.is_wall_cell(ORIGIN + Vector2i(2, 2)), "o piso não é parede")
	check(not view.is_wall_cell(ORIGIN + Vector2i(20, 20)), "fora da planta não é parede")
	view.free()


## A porta é vão: nenhum painel nela, e as paredes vizinhas dela (a soleira) não a fecham.
func test_the_door_cell_has_no_panel_and_the_gap_is_wider_than_the_body() -> void:
	for name in PLANS:
		var rows: Array = PLANS[name]
		var view := InteriorView.new()
		view.show_interior(_info(rows), ORIGIN, ActionLabels.new())
		var door := _door_of(rows)
		var area := Rect2(Vector2(ORIGIN + door) + Vector2(0.3, 0.3), Vector2(0.4, 0.4))
		for rect: Rect2 in view._panels[0]:
			check(not rect.intersects(area), "%s: nenhum painel no meio da porta (%s)" % [name, rect])
		var span := _gap_across(view._panels[0], Vector2(ORIGIN + door), _outward(rows, door))
		check(span >= PlayerBody.RADIUS * 2.0, "%s: o vão da porta (%s) comporta o corpo" % [name, span])
		view.free()


## Largura livre do vão da porta, medida na linha do meio da célula, atravessando-o.
func _gap_across(panels: Array, door_origin: Vector2, outward: Vector2i) -> float:
	var horizontal := outward.y != 0 # porta numa borda de cima/baixo: o vão corre em x
	var lo := door_origin.x if horizontal else door_origin.y
	var hi := lo + 1.0
	var line := (door_origin.y if horizontal else door_origin.x) + 0.5
	for rect: Rect2 in panels:
		var crosses := (rect.position.y <= line and rect.end.y >= line) if horizontal else (rect.position.x <= line and rect.end.x >= line)
		if not crosses:
			continue
		var a := rect.position.x if horizontal else rect.position.y
		var b := rect.end.x if horizontal else rect.end.y
		if b <= lo + 0.0001 or a >= hi - 0.0001:
			continue
		lo = maxf(lo, b) if a <= lo + 0.0001 else lo
		hi = minf(hi, a) if b >= hi - 0.0001 else hi
	return hi - lo
