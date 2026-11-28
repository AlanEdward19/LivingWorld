extends "res://tests/test_base.gd"
## #237: a mobília é posta pelo motor (célula, andar e giro): o cliente desenha ali, quem dorme fica sobre a cama e o corpo deitado
## não sai da célula da cama (antes a cama caía na porta e o corpo cruzava a parede).

const Grid := preload("res://Scenes/grid.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const SLEEP := 1


## 4x4: paredes no contorno, porta embaixo em (2,3), piso em (1..2, 1..2).
func _house() -> Dictionary:
	var cells: Array = []
	for y in 4:
		for x in 4:
			var material := 3
			if x == 0 or y == 0 or x == 3 or y == 3:
				material = 2 if (x == 2 and y == 3) else 1
			cells.append({"cell": {"x": x, "y": y}, "material": material})
	return {"building": 1, "floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": []}


func _bed_object() -> Dictionary:
	return {"id": "bed", "category": "bed", "variationSeed": 0, "usePoints": [{"name": "lie", "offset": [0.0, 0.05, 0.0], "facing": 0.0, "action": SLEEP, "pose": "sleep"}],
		"shape": [{"shape": "box", "size": [0.6, 0.2, 1.0], "offset": [0.0, 0.1, 0.0], "color": "d8cfb8", "symmetry": "none"}]}


func _view(furniture: Array, occupants: Array) -> Node3D:
	var info := _house()
	info["objects"] = {"format": 1, "catalog": [_bed_object()], "furniture": furniture}
	info["occupants"] = occupants
	var view := InteriorView.new()
	view.catalog = AssetCatalog.load_default()
	tree.root.add_child(view)
	view.show_interior(info, Vector2i(10, 10), ActionLabels.new(), 3)
	return view


func _sleeper(npc: int, cell: Dictionary) -> Dictionary:
	return {"npc": {"value": npc}, "floor": {"value": 0}, "localCell": cell, "action": SLEEP, "usingObject": "bed"}


func test_the_client_draws_the_bed_where_the_engine_put_it() -> void:
	var placed := {"id": "bed", "variation": 0, "floor": 0, "cell": {"x": 1, "y": 1}, "facing": 90}
	var view := _view([placed], [])
	eq(view.furniture.get_child_count(), 1, "uma cama")
	var bed: Node3D = view.furniture.get_child(0)
	check(absf(bed.position.x - 11.5) < 0.01 and absf(bed.position.z - 11.5) < 0.01, "na célula (1,1) do prédio em (10,10): %s" % str(bed.position))
	check(absf(bed.rotation.y - deg_to_rad(90.0)) < 0.001, "girada como o motor diz")
	view.free()


func test_a_piece_the_engine_could_not_place_is_not_drawn() -> void:
	var view := _view([{"id": "bed", "variation": 0, "floor": 0, "cell": {"x": 1, "y": 1}, "facing": 0}, {"id": "bed", "variation": 0}], [])
	eq(view.furniture.get_child_count(), 1, "a peça sem célula não é inventada pelo cliente")
	view.free()


func test_the_sleeper_lies_on_the_bed_inside_its_cell() -> void:
	var placed := {"id": "bed", "variation": 0, "floor": 0, "cell": {"x": 1, "y": 1}, "facing": 0}
	var view := _view([placed], [_sleeper(7, {"x": 2, "y": 2})]) # a célula do motor é outra: manda a cama
	var actor: Node3D = view.occupant_nodes[7]
	var bed_cell := Grid.to_world(Vector2i(11, 11))
	check(absf(actor.position.x - bed_cell.x) < 0.01 and absf(actor.position.z - bed_cell.z) < 0.01, "sobre a cama: %s" % str(actor.position))
	view.free()


func test_the_use_point_turns_with_the_piece() -> void:
	var placed := {"id": "bed", "variation": 0, "floor": 0, "cell": {"x": 1, "y": 1}, "facing": 90}
	var view := _view([placed], [_sleeper(7, {"x": 1, "y": 1})])
	check(absf(view.occupant_nodes[7].rotation.y - deg_to_rad(90.0)) < 0.001, "o corpo gira com a cama")
	view.free()


func test_a_sleeper_upstairs_uses_the_bed_of_that_floor() -> void:
	var info := _house()
	info["floors"] = 2
	info["layout"].append({"level": 1, "cells": info.layout[0].cells})
	info["objects"] = {"format": 1, "catalog": [_bed_object()], "furniture": [{"id": "bed", "variation": 0, "floor": 1, "cell": {"x": 1, "y": 2}, "facing": 0}]}
	info["occupants"] = [{"npc": {"value": 5}, "floor": {"value": 1}, "localCell": {"x": 2, "y": 1}, "action": SLEEP, "usingObject": "bed"}]
	var view := InteriorView.new()
	view.catalog = AssetCatalog.load_default()
	tree.root.add_child(view)
	view.show_interior(info, Vector2i(10, 10), ActionLabels.new(), 3)
	var actor: Node3D = view.occupant_nodes[5]
	var bed_cell := Grid.to_world(Vector2i(11, 12), Grid.floor_height())
	check(absf(actor.position.x - bed_cell.x) < 0.01 and absf(actor.position.z - bed_cell.z) < 0.01 and absf(actor.position.y - bed_cell.y - 0.05) < 0.01, "na cama do andar 1: %s" % str(actor.position))
	view.free()


func test_the_lying_body_stays_inside_one_cell() -> void:
	var actor := NpcActor.new()
	actor.setup(3, Vector2i(0, 0), SLEEP, ActionLabels.new())
	tree.root.add_child(actor)
	actor.position = Vector3.ZERO
	actor.enable_bed(true)
	actor.advance(0.5)
	actor.advance(0.5)
	var lo := Vector3(1e9, 1e9, 1e9)
	var hi := Vector3(-1e9, -1e9, -1e9)
	var stack: Array = [actor]
	while not stack.is_empty():
		var node: Node = stack.pop_back()
		stack.append_array(node.get_children())
		if node is MeshInstance3D and (node as MeshInstance3D).visible:
			var box: AABB = (node as MeshInstance3D).global_transform * (node as MeshInstance3D).get_aabb()
			lo = lo.min(box.position)
			hi = hi.max(box.end)
	check(lo.z > -0.62 and hi.z < 0.62, "o corpo e a cama dele cabem na célula (z %f a %f)" % [lo.z, hi.z])
	check(lo.x > -0.5 and hi.x < 0.5, "e na largura (x %f a %f)" % [lo.x, hi.x])
	actor.free()


func test_without_engine_places_the_old_fallback_keeps_the_door_front_free() -> void:
	var info := _house()
	info["objects"] = {"format": 1, "catalog": [_bed_object()], "furniture": [{"id": "bed", "variation": 0}, {"id": "bed", "variation": 0}, {"id": "bed", "variation": 0}]}
	var view := InteriorView.new()
	view.catalog = AssetCatalog.load_default()
	tree.root.add_child(view)
	view.show_interior(info, Vector2i(0, 0), ActionLabels.new(), 3)
	var door_front := Grid.to_world(Vector2i(2, 2))
	for piece in view.furniture.get_children():
		check(not (absf(piece.position.x - door_front.x) < 0.01 and absf(piece.position.z - door_front.z) < 0.01), "nada à frente da porta")
	view.free()
