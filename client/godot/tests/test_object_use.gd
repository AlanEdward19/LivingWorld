extends "res://tests/test_base.gd"
## #47 fatia 2 (ADR-0037): o NPC usa o objeto que o servidor aponta: posição no ponto de uso e pose declarada.

const Anim := preload("res://Scenes/animation_catalog.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const SLEEP := 1
const EAT := 0


func _room() -> Dictionary:
	var cells: Array = []
	for y in 7:
		for x in 7:
			cells.append({"cell": {"x": x, "y": y}, "material": 1 if x == 0 or y == 0 or x == 6 or y == 6 else 3})
	return {"building": 1, "floors": 2, "layout": [{"level": 0, "cells": cells}, {"level": 1, "cells": cells}], "occupants": []}


func _bed(use_points: Array) -> Dictionary:
	return {"id": "bed", "category": "bed", "variationSeed": 0, "usePoints": use_points, "shape": [
		{"shape": "box", "size": [0.5, 0.2, 0.9], "offset": [0.0, 0.1, 0.0], "color": "d8cfb8", "symmetry": "none"}]}


func _objects(use_points: Array, beds: int = 2) -> Dictionary:
	var furniture: Array = []
	for i in beds:
		furniture.append({"id": "bed", "variation": 0})
	return {"format": 1, "catalog": [_bed(use_points)], "furniture": furniture}


func _point(pose: Variant = "sleep", action: Variant = SLEEP) -> Dictionary:
	var point := {"name": "lie", "offset": [0.0, 0.05, 0.3], "facing": 90.0, "action": action}
	if pose != null:
		point["pose"] = pose
	return point


func _occupant(npc: int, action: int, using: Variant = null, level: int = 0) -> Dictionary:
	var occupant := {"npc": {"value": npc}, "floor": {"value": level}, "localCell": {"x": 1, "y": 1}, "action": action}
	if using != null:
		occupant["usingObject"] = using
	return occupant


func _view(objects: Variant, occupants: Array) -> Node3D:
	var info := _room()
	info["occupants"] = occupants
	if objects != null:
		info["objects"] = objects
	var view := InteriorView.new()
	view.catalog = AssetCatalog.load_default()
	tree.root.add_child(view)
	view.show_interior(info, Vector2i(10, 10), ActionLabels.new(), 3)
	return view


func test_a_declared_pose_wins_over_the_action_pose_and_unknown_falls_back() -> void:
	eq(Anim.pose_for(SLEEP), "sleep", "sem objeto: pose da ação")
	eq(Anim.pose_for(SLEEP, "work"), "work", "pose declarada pelo objeto")
	eq(Anim.pose_for(SLEEP, ""), "sleep", "objeto sem pose: pose da ação")
	eq(Anim.pose_for(SLEEP, "levitate"), "sleep", "pose que o cliente não conhece: pose da ação")
	eq(Anim.pose_for(99, "eat"), "eat", "ação desconhecida com pose declarada")
	eq(Anim.pose_for(99), "idle", "ação desconhecida sem objeto: neutra")


func test_a_sleeper_is_placed_at_the_beds_use_point_with_its_declared_pose() -> void:
	var view := _view(_objects([_point("sleep")]), [_occupant(7, SLEEP, "bed")])
	var actor = view.occupant_nodes[7]
	var bed_base: Vector3 = view._object_bases["bed"][0]
	check(actor.position.is_equal_approx(bed_base + Vector3(0.0, 0.05, 0.3)), "no ponto de uso da cama: %s" % str(actor.position))
	check(is_equal_approx(actor.rotation.y, deg_to_rad(90.0)), "virado como o ponto declara")
	eq(actor.pose, "sleep", "pose declarada")
	eq(actor.use_pose, "sleep", "o ator guarda a pose do objeto")
	eq(actor.use_bed, false, "a cama do catálogo substitui a cama do NPC")
	view.free()


func test_the_pose_comes_from_the_object_not_from_the_action() -> void:
	var view := _view(_objects([_point("work")]), [_occupant(7, SLEEP, "bed")])
	eq(view.occupant_nodes[7].pose, "work", "o objeto manda: dormindo na cama que declara 'work'")
	view.free()


func test_an_object_without_a_declared_pose_uses_the_action_pose() -> void:
	var view := _view(_objects([_point(null)]), [_occupant(7, SLEEP, "bed")])
	var actor = view.occupant_nodes[7]
	eq(actor.pose, "sleep", "sem pose declarada: a da ação")
	check(actor.position.is_equal_approx(view._object_bases["bed"][0] + Vector3(0.0, 0.05, 0.3)), "ainda no ponto de uso")
	view.free()


func test_without_using_object_the_npc_stays_in_its_cell_with_its_own_bed() -> void:
	var view := _view(_objects([_point("sleep")]), [_occupant(7, SLEEP)])
	var actor = view.occupant_nodes[7]
	check(actor.position.is_equal_approx(preload("res://Scenes/grid.gd").to_world(Vector2i(11, 11))), "na célula do servidor")
	eq(actor.pose, "sleep", "pose da ação")
	eq(actor.use_bed, true, "cama do próprio NPC")
	view.free()


func test_a_world_without_objects_behaves_as_before() -> void:
	var view := _view(null, [_occupant(7, SLEEP, "bed")])
	var actor = view.occupant_nodes[7]
	check(actor.position.is_equal_approx(preload("res://Scenes/grid.gd").to_world(Vector2i(11, 11))), "na célula")
	eq(actor.use_bed, true, "cama do NPC")
	view.free()


func test_a_use_point_for_another_action_is_ignored() -> void:
	var view := _view(_objects([_point("sleep", EAT)]), [_occupant(7, SLEEP, "bed")])
	check(view.occupant_nodes[7].position.is_equal_approx(preload("res://Scenes/grid.gd").to_world(Vector2i(11, 11))), "ponto de outra ação não atrai")
	view.free()


func test_two_sleepers_take_different_beds_in_npc_id_order() -> void:
	var view := _view(_objects([_point("sleep")]), [_occupant(9, SLEEP, "bed"), _occupant(4, SLEEP, "bed")])
	var first: Vector3 = view._object_bases["bed"][0]
	var second: Vector3 = view._object_bases["bed"][1]
	check(view.occupant_nodes[4].position.is_equal_approx(first + Vector3(0.0, 0.05, 0.3)), "NPC de menor id na 1ª cama")
	check(view.occupant_nodes[9].position.is_equal_approx(second + Vector3(0.0, 0.05, 0.3)), "o outro na 2ª cama")
	view.free()


func test_a_third_sleeper_wraps_around_to_the_first_bed() -> void:
	var view := _view(_objects([_point("sleep")]), [_occupant(1, SLEEP, "bed"), _occupant(2, SLEEP, "bed"), _occupant(3, SLEEP, "bed")])
	check(view.occupant_nodes[3].position.is_equal_approx(view.occupant_nodes[1].position), "sem cama sobrando, volta à primeira")
	view.free()


func test_an_upper_floor_occupant_does_not_use_ground_floor_objects() -> void:
	var view := _view(_objects([_point("sleep")]), [_occupant(7, SLEEP, "bed", 1)])
	check(view.occupant_nodes[7].position.y >= 1.0 - 0.001, "continua no andar de cima")
	eq(view.occupant_nodes[7].use_bed, true, "sem uso: cama do NPC")
	view.free()


func test_the_next_delta_switches_the_pose_when_the_object_in_use_changes() -> void:
	var view := _view(_objects([_point("sleep")]), [_occupant(7, SLEEP, "bed")])
	eq(view.occupant_nodes[7].pose, "sleep")

	view.set_occupants([_occupant(7, EAT)])

	var actor = view.occupant_nodes[7]
	eq(actor.pose, "eat", "acordou e foi comer: pose da ação")
	eq(actor.use_pose, "", "sem objeto, sem pose declarada")
	eq(actor.use_bed, true, "volta à cama do NPC")
	check(actor.position.is_equal_approx(preload("res://Scenes/grid.gd").to_world(Vector2i(11, 11))), "volta à célula do servidor")
	view.free()


func test_the_pose_changes_when_only_the_object_in_use_changes() -> void:
	var catalog := {"format": 1, "furniture": [{"id": "bed", "variation": 0}, {"id": "cot", "variation": 0}], "catalog": [
		_bed([_point("sleep")]),
		{"id": "cot", "category": "bed", "variationSeed": 0, "usePoints": [_point("idle")], "shape": [
			{"shape": "box", "size": [0.4, 0.1, 0.8], "offset": [0.0, 0.05, 0.0], "color": "aaaaaa", "symmetry": "none"}]}]}
	var view := _view(catalog, [_occupant(7, SLEEP, "bed")])
	eq(view.occupant_nodes[7].pose, "sleep")

	view.set_occupants([_occupant(7, SLEEP, "cot")])

	eq(view.occupant_nodes[7].pose, "idle", "mesma ação, outro objeto, outra pose")
	view.free()


func test_reopening_another_building_forgets_the_previous_objects() -> void:
	var view := _view(_objects([_point("sleep")]), [_occupant(7, SLEEP, "bed")])
	check(view.occupant_nodes[7].use_pose == "sleep", "no 1º prédio, usa a cama")
	var other := _room()
	other["occupants"] = [_occupant(7, SLEEP, "bed")]
	other["objects"] = {"format": 1, "furniture": [{"id": "cot", "variation": 0}], "catalog": [
		{"id": "cot", "category": "bed", "variationSeed": 0, "usePoints": [], "shape": [
			{"shape": "box", "size": [0.4, 0.1, 0.8], "offset": [0.0, 0.05, 0.0], "color": "aaaaaa", "symmetry": "none"}]}]}

	view.show_interior(other, Vector2i(30, 30), ActionLabels.new(), 3)

	var actor = view.occupant_nodes[7]
	check(actor.position.is_equal_approx(preload("res://Scenes/grid.gd").to_world(Vector2i(31, 31))), "no 2º prédio, na célula do servidor, não na cama do 1º")
	eq(actor.use_pose, "", "sem pose herdada do 1º prédio")
	eq(actor.use_bed, true, "cama do NPC")
	view.free()


func test_using_object_without_a_placed_piece_falls_back_without_error() -> void:
	var objects := _objects([_point("sleep")], 0)
	var view := _view(objects, [_occupant(7, SLEEP, "bed")])
	var actor = view.occupant_nodes[7]
	check(actor.position.is_equal_approx(preload("res://Scenes/grid.gd").to_world(Vector2i(11, 11))), "peça não colocada: célula do servidor")
	eq(actor.pose, "sleep", "pose da ação")
	eq(actor.use_bed, true, "cama do NPC")
	view.free()


func test_the_store_keeps_using_object_from_snapshot_and_delta() -> void:
	var store := preload("res://Store/world_store.gd").new()
	store.interior = {"occupants": [_occupant(7, SLEEP, "bed")]}

	store._apply_interior_delta({"moved": [_occupant(7, EAT, "table")], "entered": [_occupant(8, SLEEP, "bed")], "exited": []})

	var by_npc := {}
	for occupant in store.interior.occupants:
		by_npc[int(occupant.npc.value)] = occupant
	eq(by_npc[7].usingObject, "table", "o delta troca o objeto em uso")
	eq(by_npc[8].usingObject, "bed", "quem entrou já traz o objeto")
