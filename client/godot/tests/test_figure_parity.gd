extends "res://tests/test_base.gd"
## #247: a mesma pessoa tem a mesma aparência na rua e dentro da casa (camisa do ofício, chapéu, escala), inclusive o corpo do
## jogador (o guiado). As duas vistas passam pela fábrica única `npc_figure.gd` com o mesmo dado da store.

const Obs := preload("res://tests/observable_fixtures.gd")
const Store := preload("res://Store/world_store.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const NpcRenderer := preload("res://Scenes/render/npc_renderer.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const T := preload("res://tests/test_inspector.gd")


func _info(ids: Array) -> Dictionary:
	var cells: Array = []
	for y in 4:
		for x in 4:
			var wall := x == 0 or y == 0 or x == 3 or y == 3
			cells.append({"cell": {"x": x, "y": y}, "material": 1 if wall else 3})
	var occupants: Array = []
	for id in ids:
		occupants.append({"npc": {"value": id}, "floor": {"value": 0}, "localCell": {"x": 1 + occupants.size() % 2, "y": 1}})
	return {"building": 4, "floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": occupants}


func _setup() -> Dictionary:
	var store := Store.new()
	store.apply_snapshot(Obs.city())
	var catalog := AssetCatalog.load_default()
	var renderer := NpcRenderer.new()
	renderer.store = store
	renderer.labels = ActionLabels.new()
	renderer.catalog = catalog
	renderer.rebuild()
	var view := InteriorView.new()
	view.figure_context = renderer.figure_context()
	view.show_interior(_info([7, 8]), Vector2i(10, 10), ActionLabels.new())
	return {"store": store, "renderer": renderer, "view": view}


func _same_look(a, b, who: String) -> void:
	eq(a._shirt.albedo_color, b._shirt.albedo_color, "%s: mesma camisa na rua e na casa" % who)
	eq(a.scale, b.scale, "%s: mesma escala" % who)
	eq(a._hat != null, b._hat != null, "%s: mesmo chapéu" % who)
	eq(a.spec, b.spec, "%s: mesma aparência derivada do estado" % who)


func test_same_person_looks_the_same_in_the_street_and_inside() -> void:
	var world := _setup()
	var renderer = world.renderer
	var view = world.view
	for id in [7, 8]:
		check(not renderer.nodes[id].spec.is_empty(), "a rua veste %d pelo observável" % id)
		check(not view.occupant_nodes[id].spec.is_empty(), "a casa veste %d pelo mesmo dado" % id)
		_same_look(renderer.nodes[id], view.occupant_nodes[id], "npc %d" % id)
	check(renderer.nodes[7]._shirt.albedo_color != renderer.nodes[8]._shirt.albedo_color or renderer.nodes[7].spec != renderer.nodes[8].spec,
		"os dois ofícios se distinguem (a casa não cai no corpo base)")
	view.free()
	renderer.free()


func test_the_players_guided_body_keeps_the_street_look() -> void:
	var world := _setup()
	var renderer = world.renderer
	var view = world.view
	var inside = view.guide_body(8, renderer.nodes[8], 0)
	_same_look(renderer.nodes[8], inside, "o corpo do jogador")
	view.free()
	renderer.free()


func test_a_late_observable_dresses_the_figure_already_inside() -> void:
	var world := _setup()
	var store = world.store
	var renderer = world.renderer
	var view = world.view
	var fresh := InteriorView.new()
	var obs: Dictionary = store.npcs[7].obs
	store.npcs[7].obs = {}
	fresh.figure_context = renderer.figure_context()
	fresh.show_interior(_info([7]), Vector2i(10, 10), ActionLabels.new())
	check(fresh.occupant_nodes[7].spec.is_empty(), "sem o dado do motor a figura é o corpo base")
	store.npcs[7].obs = obs
	fresh.set_occupants(_info([7]).occupants)
	check(not fresh.occupant_nodes[7].spec.is_empty(), "o dado chegou: a figura da casa se veste")
	fresh.free()
	view.free()
	renderer.free()
