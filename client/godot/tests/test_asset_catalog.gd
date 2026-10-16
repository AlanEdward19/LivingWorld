extends "res://tests/test_base.gd"

const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const Ambience := preload("res://Scenes/ambience.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const SCENARIOS := ["default", "test-living-village", "test-scifi"]


func _catalog() -> RefCounted:
	return AssetCatalog.load_default()


func _room() -> Dictionary:
	var cells: Array = []
	for y in 5:
		for x in 5:
			var material := 3
			if x == 0 or y == 0 or x == 4 or y == 4:
				material = 1
			cells.append({"cell": {"x": x, "y": y}, "material": material})
	return {"building": 1, "floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": []}


func _building_types(scenario: String) -> Array:
	var file := FileAccess.open("res://../../scenarios/%s.json" % scenario, FileAccess.READ)
	var data: Dictionary = JSON.parse_string(file.get_as_text())
	var ids := {}
	for id in data.get("LocationTypeIds", []):
		ids[int(id)] = true
	for workplace in data.get("Workplaces", []):
		ids[int(workplace.LocationTypeId)] = true
	for id in data.get("BuildingRecipes", {}):
		ids[int(id)] = true
	return ids.keys()


func test_every_scenario_building_type_has_furniture_entry() -> void:
	var catalog := _catalog()
	for scenario in SCENARIOS:
		var types := _building_types(scenario)
		check(not types.is_empty(), "%s tem tipos de prédio" % scenario)
		for type_id in types:
			check(catalog.has_furniture_entry(type_id), "%s: tipo %d tem mobília no catálogo" % [scenario, type_id])
			for piece_id in catalog.furniture_for(type_id):
				check(catalog.data.pieces.has(piece_id), "peça '%s' existe" % piece_id)


func test_every_action_has_sound_with_procedural_spec() -> void:
	var catalog := _catalog()
	for action in range(8):
		var sound_id: String = catalog.sound_id_for_action(action)
		if action == 5: # parado: silêncio
			eq(sound_id, "", "ação parada não toca")
			continue
		check(catalog.sound_spec(sound_id).has("freq"), "ação %d tem som procedural" % action)


func test_partial_catalog_falls_back_without_error() -> void:
	var catalog := AssetCatalog.new()
	catalog.load_from({"actions": {"1": {"sound": "snore"}}, "sounds": {"snore": {"file": "res://nao_existe.ogg"}}})
	eq(catalog.sound_id_for_action(4), "", "ação sem entrada não tem som")
	eq(catalog.sound_file("snore"), null, "arquivo ausente cai no procedural")
	eq(catalog.furniture_for(99), [], "tipo sem entrada nem default: sem mobília, sem erro")
	eq(catalog.profession_model(7), {}, "profissão sem entrada: modelo base")
	check(catalog.age_model(30.0).get("scale", 1.0) == 1.0, "idade sem faixa: escala 1")
	var ambience := Ambience.new()
	ambience.catalog = catalog
	tree.root.add_child(ambience)
	eq(ambience.play_action(4), "", "sem som para a ação")
	eq(ambience.play_action(1), "snore", "som do catálogo parcial toca (sintetizado)")
	ambience.queue_free()


func test_action_sound_respects_mute() -> void:
	var ambience := Ambience.new()
	ambience.catalog = _catalog()
	tree.root.add_child(ambience)
	eq(ambience.play_action(0), "munch", "comer toca munch")
	ambience.set_muted(true)
	eq(ambience.play_action(0), "", "mudo não toca")
	ambience.queue_free()


func test_period_change_restarts_fade() -> void:
	var ambience := Ambience.new()
	tree.root.add_child(ambience)
	ambience.set_daylight(1.0)
	eq(ambience.period, "day", "meio-dia")
	ambience.period_fade = 1.0
	ambience.set_daylight(0.5)
	eq(ambience.period, "dusk", "luz caindo = entardecer")
	eq(ambience.period_fade, 0.0, "troca de período reinicia o fade")
	ambience._process(1.0)
	check(ambience.period_fade > 0.0 and ambience.period_fade < 1.0, "fade em andamento")
	ambience._process(2.0)
	eq(ambience.period_fade, 1.0, "fade completa")
	ambience.set_daylight(0.1)
	eq(ambience.period, "night", "noite")
	ambience.set_daylight(0.5)
	eq(ambience.period, "dawn", "luz subindo = amanhecer")
	ambience.queue_free()


func test_interior_shows_furniture_for_building_type() -> void:
	var view := InteriorView.new()
	view.catalog = _catalog()
	tree.root.add_child(view)
	var labels := ActionLabels.new()
	view.show_interior(_room(), Vector2i(10, 10), labels, 4)
	eq(view.furniture.get_child_count(), view.catalog.furniture_for(4).size(), "peças do tipo 4")
	var first: Vector3 = view.furniture.get_child(0).position
	view.show_interior(_room(), Vector2i(10, 10), labels, 4)
	eq(view.furniture.get_child(0).position, first, "mesma casa, mesma disposição")
	view.show_interior(_room(), Vector2i(10, 10), labels, 999)
	eq(view.furniture.get_child_count(), view.catalog.furniture_for(-1).size(), "tipo desconhecido usa o default")
	view.free()


func test_npc_model_follows_profession_and_age() -> void:
	var catalog := _catalog()
	var labels := ActionLabels.new()
	var child := NpcActor.new()
	child.setup(1, Vector2i.ZERO, 5, labels)
	child.apply_model(catalog, 1, 8.0)
	check(child.scale.y < 0.8, "criança é menor")
	check(child._hat != null, "agricultor usa chapéu")
	child.apply_model(catalog, 6, 30.0)
	eq(child.scale.y, 1.0, "adulto tem escala 1")
	eq(child._hat, null, "profissão sem chapéu remove o chapéu")
	child.apply_model(catalog, 999, 70.0)
	check(child.scale.y < 1.0, "idoso é um pouco mais baixo")
	child.free()


func test_lodging_building_shows_one_bed_per_unit() -> void:
	var view := InteriorView.new()
	view.catalog = _catalog()
	tree.root.add_child(view)
	view.show_interior(_room(), Vector2i(10, 10), ActionLabels.new(), -1, 0)
	var plain: int = view.furniture.get_child_count()
	view.show_interior(_room(), Vector2i(10, 10), ActionLabels.new(), -1, 4)
	eq(view.furniture.get_child_count(), plain + 4, "mais um leito por vaga de hospedagem")
	view.free()


func test_lodging_beds_fill_available_floor_without_an_extra_twelve_bed_limit() -> void:
	var view := InteriorView.new()
	view.catalog = _catalog()
	tree.root.add_child(view)
	var cells: Array = []
	for x in 20:
		cells.append({"cell": {"x": x, "y": 0}, "material": 3})
	var room := {"floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": []}
	view.show_interior(room, Vector2i(10, 10), ActionLabels.new(), -1, 18)
	var beds := 0
	for piece in view.furniture.get_children():
		if String(piece.name).begins_with("bed") or String(piece.name).begins_with("@MeshInstance3D"):
			beds += 1
	eq(beds, 18, "dezoito vagas e piso livre devem produzir dezoito leitos")
	view.free()


func test_lodging_uses_free_floor_on_upper_storeys_when_ground_is_full() -> void:
	var view := InteriorView.new()
	view.catalog = _catalog()
	tree.root.add_child(view)
	var cells: Array = []
	for x in 3:
		cells.append({"cell": {"x": x, "y": 0}, "material": 3})
	var room := {"floors": 2, "layout": [{"level": 0, "cells": cells}, {"level": 1, "cells": cells}], "occupants": []}
	view.show_interior(room, Vector2i(10, 10), ActionLabels.new(), -1, 5)
	var ground: Node3D = view.floor_nodes[0].get_node_or_null("Furniture")
	var upper: Node3D = view.floor_nodes[1].get_node_or_null("Furniture")
	check(upper != null, "piso livre do andar superior recebe leitos")
	if upper != null:
		eq(ground.get_child_count() + upper.get_child_count(), 5, "cinco vagas, cinco leitos")
	view.free()
