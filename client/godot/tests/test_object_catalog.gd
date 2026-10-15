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
