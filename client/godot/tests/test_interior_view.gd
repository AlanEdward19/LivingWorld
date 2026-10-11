extends "res://tests/test_base.gd"

const InteriorStage := preload("res://Scenes/interior_stage.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const ActionLabels := preload("res://Store/action_labels.gd")


func _info() -> Dictionary:
	var cells: Array = []
	for y in 3:
		for x in 3:
			var material := 3
			if x == 0 or y == 0 or x == 2 or y == 2:
				material = 1
			if x == 1 and y == 2:
				material = 2
			if x == 1 and y == 1:
				material = 4
			cells.append({"cell": {"x": x, "y": y}, "material": material})
	return {
		"building": 4, "floors": 3,
		"layout": [{"level": 0, "cells": cells}, {"level": 1, "cells": cells}, {"level": 2, "cells": cells}],
		"occupants": [
			{"npc": {"value": 1}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}},
			{"npc": {"value": 2}, "floor": {"value": 1}, "localCell": {"x": 1, "y": 1}},
			{"npc": {"value": 3}, "floor": {"value": 1}, "localCell": {"x": 1, "y": 1}},
			{"npc": {"value": 4}, "floor": {"value": 2}, "localCell": {"x": 1, "y": 1}},
		],
	}


func test_upper_floors_and_roof_hidden() -> void:
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())

	view.set_floor(1)

	eq(view.floor_nodes[0].visible, true, "andar abaixo visível")
	eq(view.floor_nodes[1].visible, true, "andar escolhido visível")
	eq(view.floor_nodes[2].visible, false, "andar acima oculto")
	eq(view.roof.visible, false, "teto oculto")
	view.set_floor(0)
	eq(view.floor_nodes[1].visible, false)
	eq(view.floor_nodes[2].visible, false)
	view.free()


func test_floor_switch_shows_only_its_occupants() -> void:
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())

	view.set_floor(1)
	eq(view.visible_occupant_ids(), [2, 3], "andar 1: só quem está nele")
	view.set_floor(0)
	eq(view.visible_occupant_ids(), [1])
	view.set_floor(2)
	eq(view.visible_occupant_ids(), [4])
	eq(view.occupant_nodes[4].position.y, 2.0, "o ocupante fica na altura do seu andar")
	view.free()


func test_stage_enter_and_leave_toggle_dimming() -> void:
	var stage := InteriorStage.new()
	stage.enter(_info(), Vector2i(10, 10), ActionLabels.new(), Camera3D.new())
	eq(stage.dim_rect.visible, true, "exterior escurecido e desfocado ao entrar")
	eq(stage.active, true)

	stage.leave()

	eq(stage.dim_rect.visible, false, "exterior restaurado ao sair")
	eq(stage.active, false)
	stage.free()
