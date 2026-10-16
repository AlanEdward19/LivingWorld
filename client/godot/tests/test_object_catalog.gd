extends "res://tests/test_base.gd"
## #47 fatia 1 (ADR-0034): o interior sai do catálogo de objetos do servidor, igual ao de antes, com mesh/material compartilhados.

const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")
const WorldStore := preload("res://Store/world_store.gd")
const ActionLabels := preload("res://Store/action_labels.gd")


func _room() -> Dictionary:
	var cells: Array = []
	for y in 7:
		for x in 7:
			cells.append({"cell": {"x": x, "y": y}, "material": 1 if x == 0 or y == 0 or x == 6 or y == 6 else 3})
	return {"building": 1, "floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": []}


## O que o servidor manda para o tipo de prédio: o mesmo assets.json, na forma de catálogo (caixa única, offset y = altura/2).
func _server_objects(assets: RefCounted, building_type: int, variation: int = 0) -> Dictionary:
	var catalog: Array = []
	for id in assets.data.pieces:
		var spec: Dictionary = assets.data.pieces[id]
		catalog.append({"id": id, "category": "furniture", "variationSeed": 0, "usePoints": [], "shape": [{
			"shape": "box", "size": spec.size, "offset": [0.0, float(spec.size[1]) / 2.0, 0.0], "color": spec.color, "symmetry": "none"}]})
	var furniture: Array = []
	for id in assets.furniture_for(building_type):
		furniture.append({"id": id, "variation": variation})
	return {"format": 1, "catalog": catalog, "furniture": furniture}


func _show(info: Dictionary, building_type: int) -> Node3D:
	var view := InteriorView.new()
	view.catalog = AssetCatalog.load_default()
	tree.root.add_child(view)
	view.show_interior(info, Vector2i(10, 10), ActionLabels.new(), building_type)
	return view


func test_interior_from_the_catalog_matches_the_assets_json_interior() -> void:
	var assets := AssetCatalog.load_default()
	for building_type in [1, 3, 4, 10, 999]:
		var old_view := _show(_room(), building_type)
		var info := _room()
		info["objects"] = _server_objects(assets, building_type)
		var new_view := _show(info, building_type)
		eq(new_view.furniture.get_child_count(), old_view.furniture.get_child_count(), "tipo %d: mesma contagem de peças" % building_type)
		for i in old_view.furniture.get_child_count():
			var before: MeshInstance3D = old_view.furniture.get_child(i)
			var after: MeshInstance3D = new_view.furniture.get_child(i)
			eq(after.position, before.position, "tipo %d peça %d: mesma posição" % [building_type, i])
			eq((after.mesh as BoxMesh).size, (before.mesh as BoxMesh).size, "tipo %d peça %d: mesmo tamanho" % [building_type, i])
			eq((after.material_override as StandardMaterial3D).albedo_color, (before.material_override as StandardMaterial3D).albedo_color, "mesma cor")
		old_view.free()
		new_view.free()


func test_the_view_draws_the_server_catalog_not_the_assets_json() -> void:
	var info := _room()
	info["objects"] = {"format": 1, "furniture": [{"id": "lamp", "variation": 2}, {"id": "lamp", "variation": 0}],
		"catalog": [{"id": "lamp", "category": "light", "variationSeed": 7, "usePoints": [], "shape": [
			{"shape": "cylinder", "size": [0.1, 0.6], "offset": [0.0, 0.3, 0.0], "color": "ff0000", "symmetry": "none"},
			{"shape": "sphere", "size": [0.08], "offset": [0.2, 0.7, 0.0], "color": "00ff00", "symmetry": "x"}]}]}
	var view := _show(info, 4)
	eq(view.furniture.get_child_count(), 2, "duas lâmpadas do servidor, nada do assets.json")
	var plain: Node3D = view.furniture.get_child(1)
	var shaded: Node3D = view.furniture.get_child(0)
	eq(plain.get_child_count(), 3, "cilindro + esfera espelhada em x")
	check(plain.get_child(0).mesh is CylinderMesh, "forma do servidor")
	eq((plain.get_child(0).material_override as StandardMaterial3D).albedo_color, Color("ff0000"), "cor do servidor")
	check((shaded.get_child(0).material_override as StandardMaterial3D).albedo_color != Color("ff0000"), "variação 2 do servidor clareia a cor")
	view.free()


func test_without_objects_the_assets_json_fallback_is_used() -> void:
	var view := _show(_room(), 4)
	eq(view.furniture.get_child_count(), view.catalog.furniture_for(4).size(), "sem objects, mobília do assets.json")
	view.free()


func test_an_id_missing_from_the_catalog_falls_back_to_the_assets_json_piece() -> void:
	var info := _room()
	info["objects"] = {"format": 1, "catalog": [], "furniture": [{"id": "table", "variation": 0}]}
	var view := _show(info, 4)
	eq(view.furniture.get_child_count(), 1, "peça fora do catálogo ainda aparece pelo fallback")
	view.free()


func test_instances_of_the_same_id_and_variation_share_mesh_and_material() -> void:
	var factory := ObjectFactory.new(_server_objects(AssetCatalog.load_default(), 10).catalog)
	var first: MeshInstance3D = factory.build("bed", 0)
	var second: MeshInstance3D = factory.build("bed", 0)
	check(first.mesh == second.mesh, "mesmo mesh")
	check(first.material_override == second.material_override, "mesmo material")
	eq(factory.meshes_built, 1, "um mesh só para (bed, 0)")
	var other: MeshInstance3D = factory.build("bed", 2)
	check(other.mesh != first.mesh, "outra variação, outro mesh")
	check((other.material_override as StandardMaterial3D).albedo_color != (first.material_override as StandardMaterial3D).albedo_color, "variação muda a cor")
	for node in [first, second, other]:
		node.free()


func test_fifty_objects_build_only_one_mesh_per_id_and_variation() -> void:
	var assets := AssetCatalog.load_default()
	var factory := ObjectFactory.new(_server_objects(assets, 3).catalog)
	var nodes: Array = []
	for i in 50:
		nodes.append(factory.build(["bed", "table", "chest"][i % 3], i % 2))
	eq(factory.meshes_built, 6, "3 ids x 2 variações, não 50")
	for node in nodes:
		node.free()


func test_parts_and_symmetry_build_a_composite_with_mirrored_children() -> void:
	var descriptor := {"id": "stool", "category": "furniture", "variationSeed": 0, "usePoints": [], "shape": [
		{"shape": "cylinder", "size": [0.2, 0.05], "offset": [0.0, 0.3, 0.0], "color": "7a5a38", "symmetry": "none"},
		{"shape": "capsule", "size": [0.03, 0.3], "offset": [0.1, 0.15, 0.1], "color": "5c4630", "symmetry": "xz"},
		{"shape": "sphere", "size": [0.04], "offset": [0.0, 0.35, 0.0], "color": "d8cfb8", "symmetry": "none"}]}
	var factory := ObjectFactory.new([descriptor])
	var node: Node3D = factory.build("stool", 0)
	eq(node.get_child_count(), 6, "1 cilindro + 4 pernas espelhadas + 1 esfera")
	check(node.get_child(0).mesh is CylinderMesh and node.get_child(5).mesh is SphereMesh, "formas primitivas")
	var xs := {}
	for i in range(1, 5):
		xs[node.get_child(i).position] = true
	eq(xs.size(), 4, "as quatro pernas em posições distintas")
	node.free()


func test_the_store_keeps_the_objects_of_the_interior_snapshot() -> void:
	var store := WorldStore.new()
	var objects := {"format": 1, "catalog": [], "furniture": []}
	store._load_interior({"id": {"value": 4}, "city": {"value": "c"}, "layout": {"floors": 1, "floorLayouts": []},
		"occupants": [], "objects": objects})
	eq(store.interior.objects, objects, "objects chega à vista do interior")
	store._load_interior({"id": {"value": 4}, "city": {"value": "c"}, "layout": {"floors": 1, "floorLayouts": []}, "occupants": []})
	eq(store.interior.objects, null, "sem objects no snapshot: nulo (fallback)")


func test_each_primitive_gets_the_declared_dimensions() -> void:
	var factory := ObjectFactory.new([{"id": "p", "category": "x", "variationSeed": 0, "usePoints": [], "shape": [
		{"shape": "cylinder", "size": [0.2, 0.6], "offset": [0.0, 0.0, 0.0], "color": "aaaaaa", "symmetry": "none"},
		{"shape": "sphere", "size": [0.3], "offset": [0.0, 0.0, 0.0], "color": "aaaaaa", "symmetry": "none"},
		{"shape": "capsule", "size": [0.1, 0.5], "offset": [0.0, 0.0, 0.0], "color": "aaaaaa", "symmetry": "none"},
		{"shape": "box", "size": [0.4, 0.5, 0.6], "offset": [0.0, 0.0, 0.0], "color": "aaaaaa", "symmetry": "none"}]}])
	var node: Node3D = factory.build("p", 0)
	var cylinder: CylinderMesh = node.get_child(0).mesh
	near([cylinder.top_radius, cylinder.bottom_radius, cylinder.height], [0.2, 0.2, 0.6], "cilindro: raio e altura")
	var sphere: SphereMesh = node.get_child(1).mesh
	near([sphere.radius, sphere.height], [0.3, 0.6], "esfera: raio e diâmetro")
	var capsule: CapsuleMesh = node.get_child(2).mesh
	near([capsule.radius, capsule.height], [0.1, 0.5], "cápsula: raio e altura")
	eq((node.get_child(3).mesh as BoxMesh).size, Vector3(0.4, 0.5, 0.6), "caixa")
	node.free()


func test_a_single_part_with_symmetry_is_mirrored() -> void:
	var factory := ObjectFactory.new([{"id": "m", "category": "x", "variationSeed": 0, "usePoints": [], "shape": [
		{"shape": "box", "size": [0.1, 0.1, 0.1], "offset": [0.2, 0.05, 0.0], "color": "aaaaaa", "symmetry": "x"}]}])
	var node: Node3D = factory.build("m", 0)
	eq(node.get_child_count(), 2, "uma parte com simetria x vira duas")
	near([node.get_child(0).position.x, node.get_child(1).position.x], [0.2, -0.2], "espelhada em x")
	node.free()


func test_variation_lightens_the_color_by_a_fixed_step_and_unknown_ids_build_nothing() -> void:
	var factory := ObjectFactory.new([{"id": "c", "category": "x", "variationSeed": 1, "usePoints": [], "shape": [
		{"shape": "box", "size": [0.1, 0.1, 0.1], "offset": [0.0, 0.05, 0.0], "color": "804020", "symmetry": "none"}]}])
	for variation in [0, 1, 3]:
		var node: MeshInstance3D = factory.build("c", variation)
		var expected := Color("804020") if variation == 0 else Color("804020").lightened(0.05 * variation)
		eq((node.material_override as StandardMaterial3D).albedo_color, expected, "variação %d" % variation)
		check(is_equal_approx((node.material_override as StandardMaterial3D).roughness, 0.9), "rugosidade de sempre")
		node.free()
	eq(factory.build("nao-existe", 0), null, "id fora do catálogo não constrói nada")


func near(actual: Array, expected: Array, message: String) -> void:
	var ok := actual.size() == expected.size()
	for i in mini(actual.size(), expected.size()):
		ok = ok and is_equal_approx(float(actual[i]), float(expected[i]))
	check(ok, "%s esperado %s, veio %s" % [message, str(expected), str(actual)])


func test_server_objects_and_lodging_beds_share_the_available_floors() -> void:
	for units in [2, 5]:
		var cells := [{"cell": {"x": 0, "y": 0}, "material": 3}, {"cell": {"x": 1, "y": 0}, "material": 3}, {"cell": {"x": 2, "y": 0}, "material": 3}]
		var info := {"floors": 2, "layout": [{"level": 0, "cells": cells}, {"level": 1, "cells": cells}], "occupants": [],
			"objects": {"format": 1, "furniture": [{"id": "server_lamp", "variation": 2}], "catalog": [
				{"id": "bed", "shape": [{"shape": "box", "size": [0.6, 0.4, 1.4], "offset": [0.0, 0.2, 0.0], "color": "ff0000", "symmetry": "none"}]},
				{"id": "server_lamp", "shape": [{"shape": "box", "size": [0.2, 0.8, 0.2], "offset": [0.0, 0.4, 0.0], "color": "224466", "symmetry": "none"}]}]}}
		var view := InteriorView.new()
		view.catalog = AssetCatalog.load_default()
		tree.root.add_child(view)
		view.show_interior(info, Vector2i(10, 10), ActionLabels.new(), 4, units)
		var beds := 0
		var lamps := 0
		var bed_mesh: Mesh
		for floor_node in view.floor_nodes.values():
			var pieces: Node3D = floor_node.get_node_or_null("Furniture")
			if pieces == null:
				continue
			for piece in pieces.get_children():
				if piece.mesh.size == Vector3(0.6, 0.4, 1.4):
					beds += 1
					if bed_mesh == null:
						bed_mesh = piece.mesh
					check(piece.mesh == bed_mesh, "leitos compartilham o mesh do servidor entre andares")
				else:
					lamps += 1
					eq((piece.material_override as StandardMaterial3D).albedo_color, Color("224466").lightened(0.1), "variação da mobília enviada pelo servidor")
		eq(beds, units, "leitos do catálogo para todas as vagas")
		eq(lamps, 1 if units == 2 else 0, "mobília do servidor ocupa apenas piso térreo livre")
		if units == 5:
			var upper: Node3D = view.floor_nodes[1].get_node("Furniture")
			eq(upper.get_child_count(), 2, "dois leitos ficam no andar superior")
			view.set_floor(0)
			for bed in upper.get_children():
				check(not bed.is_visible_in_tree(), "corte esconde os leitos do andar superior")
			view.set_floor(1)
			for bed in upper.get_children():
				check(bed.is_visible_in_tree(), "andar superior volta a mostrar os leitos")
		view.free()
