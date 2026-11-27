extends "res://tests/test_base.gd"

const Grid := preload("res://Scenes/grid.gd")

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


func test_stage_enter_and_leave_hide_and_restore_the_world() -> void:
	var world := Node3D.new()
	var stage := InteriorStage.new()
	stage.world = world
	var camera := Camera3D.new()
	stage.enter(_info(), Vector2i(10, 10), ActionLabels.new(), camera)
	eq(world.visible, false, "o mundo não é desenhado com a casa em foco")
	eq(world.process_mode, Node.PROCESS_MODE_DISABLED, "nem atualizado")
	eq(stage.backdrop.visible, true, "fundo fora de foco")
	eq(stage.active, true)
	check(stage.find_children("*", "SubViewport", true, false).is_empty(), "sem cena separada")
	var middle: Vector3 = stage.interior_view.center()
	eq(Vector2(middle.x, middle.z), Vector2(10 + 1.5, 10 + 1.5), "no lugar do prédio, em células do mundo")

	stage.leave()

	eq(world.visible, true, "mundo de volta ao sair")
	eq(world.process_mode, Node.PROCESS_MODE_INHERIT)
	eq(stage.backdrop.visible, false)
	eq(stage.active, false)
	stage.free()
	world.free()
	camera.free()


## #226: a parede do motor (célula inteira) vira painel fino; o contorno fica rente à borda de fora e o cômodo ganha a célula toda.
func test_walls_are_thin_panels_and_the_room_keeps_its_whole_cells() -> void:
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())
	var rects: Array = view._panels[0]
	check(not rects.is_empty(), "há painéis")
	for rect: Rect2 in rects:
		check(minf(rect.size.x, rect.size.y) <= InteriorView.WallPanels.THICKNESS + 0.001, "painel fino (%s)" % rect.size)
	var area := 0.0
	for x in range(10, 13):
		for y in range(10, 13):
			area += 1.0
	var covered := 0.0
	for rect: Rect2 in rects:
		covered += rect.get_area()
	check(covered < area * 0.35, "as paredes ocupam menos de 35%% da planta (%s de %s)" % [covered, area])
	view.free()


## #226/#229: o corpo anda por toda a célula do cômodo (inclusive a da antiga parede) e só para no painel; a porta passa.
func test_the_body_walks_the_whole_room_and_stops_at_the_thin_wall_but_not_at_the_door() -> void:
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())
	var Body := preload("res://Scenes/player_body.gd")
	var body := Body.new()
	body.place(Vector2(10.5, 10.5)) # a célula (0,0), que o motor diz ser parede: agora é piso útil
	body.set_neighborhood(view.obstacles_near(body.pos, 4.0), [])
	for i in 120:
		body.advance(Vector2.LEFT, 4.0, 1.0 / 60.0)
	check(body.pos.x >= 10.0 + InteriorView.WallPanels.THICKNESS + body.radius - 0.001, "o painel de fora segura o corpo (x=%s)" % body.pos.x)
	check(body.pos.x < 10.5, "encostou na borda de fora (x=%s)" % body.pos.x)
	body.place(Vector2(11.5, 11.5)) # a célula da escada (1,1)
	body.set_neighborhood(view.obstacles_near(body.pos, 4.0), [])
	for i in 120:
		body.advance(Vector2.DOWN, 4.0, 1.0 / 60.0)
	check(body.pos.y > 12.6, "a porta (1,2) deixa passar para fora (y=%s)" % body.pos.y)
	view.free()


## #222: no interior a parede tem a altura do andar (mais alta que uma pessoa), as do lado da câmera ficam na altura do joelho
## (corte de maquete), a porta é um vão com verga e a escada tem degraus.
func test_interior_walls_are_floor_tall_with_a_cutaway_door_gap_and_steps() -> void:
	var grid = InteriorView.Grid
	var kept := [grid.cell_meters, grid.floor_meters, grid.door_meters]
	grid.cell_meters = 1.5 # a escala do motor (ADR-0052)
	grid.floor_meters = 3.0
	grid.door_meters = 2.0
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())
	var walls: Array = view._walls.filter(func(w): return not w.get("lintel", false))
	var tallest := 0.0
	for wall in walls:
		tallest = maxf(tallest, (wall.mesh as BoxMesh).size.y)
	check(tallest > InteriorView.Grid.units(1.8), "parede mais alta que uma pessoa (%s unidades)" % tallest)
	check(view._walls.any(func(w): return w.get("lintel", false)), "a porta tem verga (um vão, não um bloco)")
	view.set_view_direction(Vector3(0, 0, 1))
	var low := walls.filter(func(w): return (w.mesh as BoxMesh).size.y < InteriorView.Grid.units(1.0))
	check(not low.is_empty() and low.size() < walls.size(), "só as paredes do lado da câmera ficam baixas")
	var steps := 0
	for child in view.floor_nodes[0].get_children():
		if child is MeshInstance3D and (child.mesh as BoxMesh).size.y > 0.1 and (child.mesh as BoxMesh).size.x < 1.0:
			steps += 1
	check(steps >= 3, "a escada tem degraus (%d)" % steps)
	view.free()
	grid.cell_meters = kept[0]
	grid.floor_meters = kept[1]
	grid.door_meters = kept[2]


func test_player_inside_keeps_every_wall_full_height_and_exposes_the_floor_boxes() -> void:
	var view := InteriorView.new()
	view.immersive = true
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())
	view.set_view_direction(Vector3(1, 0, 0))
	for wall in view._walls:
		if wall.get("lintel", false):
			continue
		check(absf((wall.mesh as BoxMesh).size.y - InteriorView.wall_height()) < 0.001, "parede inteira, sem corte de maquete")
	var box: AABB = view.floor_box()
	check(box.size.x > 0.0 and box.has_point(Vector3(11.5 * Grid.CELL, 0.5, 11.5 * Grid.CELL)), "caixa do andar cobre a planta (%s)" % box)
	check(not view.wall_boxes().is_empty(), "paredes finas do andar para a câmera")
	view.set_floor(1)
	check(is_equal_approx(view.floor_box().position.y, Grid.floor_height()), "a caixa acompanha o andar visível")


const LOOK := {"skin_tone": 0.4, "hair_tone": 0.2, "hair_curl": 0.5, "build": 0.5}


func test_the_body_walking_the_plan_is_drawn_as_a_guided_occupant_that_survives_updates_and_is_released() -> void:
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())
	var world_actor = preload("res://Scenes/npc_actor.gd").new()
	world_actor.setup(77, Vector2i(11, 11), -1, ActionLabels.new(), LOOK)
	world_actor.position = Vector3(11.5, 0, 11.5)
	var guided = view.guide_body(77, world_actor, 1)
	eq(view.occupant_nodes.get(77), guided, "o corpo vira ocupante guiado")
	eq(guided.driven, true, "guiado pela posição fina do motor")
	eq(guided.look, LOOK, "com a aparência do boneco do mundo")
	check(is_equal_approx(guided.position.y, Grid.floor_height()), "no andar que o motor diz")
	view.set_floor(1)
	eq(guided.visible, true, "visível no andar dele")
	view.set_occupants(_info().occupants)
	check(view.occupant_nodes.has(77), "uma atualização dos ocupantes não o apaga")
	view.release_guided()
	check(not view.occupant_nodes.has(77), "solto, some")
	world_actor.free()


## #227: o forro só aparece com o jogador dentro e a câmera abaixo dele; fica sobre as paredes do andar visível (sem céu entre elas).
func test_the_ceiling_shows_only_when_the_camera_is_inside_and_below_it() -> void:
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(10, 10), ActionLabels.new())
	check(view.ceiling != null and not view.ceiling.visible, "sem jogador, sem forro")
	view.update_ceiling(0.5)
	check(not view.ceiling.visible, "o observador vê a casa aberta, de maquete")
	view.immersive = true
	view.update_ceiling(view.ceiling.position.y - 0.3)
	check(view.ceiling.visible, "câmera de gente dentro da casa: forro")
	check(view.ceiling.position.y >= InteriorView.wall_height(), "o forro assenta sobre as paredes (%s)" % view.ceiling.position.y)
	view.update_ceiling(view.ceiling.position.y + 3.0)
	check(not view.ceiling.visible, "câmera acima do forro: some")
	view.set_floor(1)
	check(view.ceiling.position.y > InteriorView.wall_height() + InteriorView.Grid.floor_height() * 0.5, "o forro sobe com o andar")
	view.free()


## #227: com a câmera do jogador colada na parede ou sob o forro, essas peças esmaecem (nada opaco tampa a tela); longe, ficam opacas.
func test_walls_and_ceiling_near_the_player_camera_fade_and_far_ones_stay_opaque() -> void:
	var view := InteriorView.new()
	view.show_interior(_info(), Vector2i(0, 0), ActionLabels.new())
	view.immersive = true
	view.set_view_direction(Vector3.ZERO)
	var spot := Vector3(1.5, 1.2, 1.5)
	view.update_near(spot)
	var lid_under: MeshInstance3D = view.ceiling.get_child(4) # a célula central
	check(lid_under.transparency > 0.99, "o forro logo acima da câmera some (%s)" % lid_under.transparency)
	var lid_far: MeshInstance3D = view.ceiling.get_child(0)
	check(lid_far.transparency < 0.5, "o forro longe fica (%s)" % lid_far.transparency)
	var picked: MeshInstance3D = null
	for wall in view._walls:
		if not wall.get("lintel", false):
			picked = wall.node
			break
	view.update_near(Vector3(picked.position.x, 1.2, picked.position.z))
	check(picked.transparency > 0.9, "a parede colada na câmera some (%s)" % picked.transparency)
	view.update_near(Vector3(50, 1.2, 50))
	check(picked.transparency < 0.01, "parede longe fica opaca (%s)" % picked.transparency)
	view.immersive = false
	view.update_near(spot)
	check(lid_under.transparency == 0.0, "fora do jogador tudo volta opaco")
	view.free()
