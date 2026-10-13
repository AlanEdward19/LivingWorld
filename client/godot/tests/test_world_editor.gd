extends "res://tests/test_base.gd"

const WorldEditor := preload("res://Scenes/world_editor.gd")
const MainMenu := preload("res://Scenes/main_menu.gd")
const WorldTouchup := preload("res://Scenes/world_touchup.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")

const SCENARIO := '{"Width":10,"Height":10,"Seed":1,"InitialPopulation":40,"AnnualConceptionChance":0.25,"MaxLongevityYears":90,"VillageX":5,"VillageY":5,"Settlements":[{"Name":"vila","X":5,"Y":5}],"Nested":{"Width":99}}'


func test_read_returns_editable_values() -> void:
	var values: Dictionary = WorldEditor.read(SCENARIO)
	eq(values.width, 10, "largura")
	eq(values.population, 40, "população")
	eq(values.settlements, [{"name": "vila", "x": 5, "y": 5}], "assentamentos")
	check(absf(values.conception - 0.25) < 0.001, "natalidade")


func test_edits_keep_integers_as_integers_and_touch_only_top_level() -> void:
	var out: String = WorldEditor.with_population(SCENARIO, 250)
	out = WorldEditor.with_longevity(out, 80)
	out = WorldEditor.with_conception(out, 0.4)
	check('"InitialPopulation":250' in out, "população inteira")
	check('"MaxLongevityYears":80' in out, "longevidade inteira")
	check('"AnnualConceptionChance":0.400' in out, "natalidade decimal")
	check('"Nested":{"Width":99}' in out, "membro aninhado intacto")
	check(not (".0," in out), "nenhum inteiro virou decimal")


func test_shrinking_the_map_pulls_settlements_inside() -> void:
	var grown: String = WorldEditor.with_settlements(SCENARIO, [{"name": "a", "x": 9, "y": 9}, {"name": "b\"q", "x": 2, "y": 3}])
	check('"VillageX":9' in grown and '"VillageY":9' in grown, "o primeiro é a aldeia inicial")
	check('"Name":"b\\"q"' in grown, "nome com aspas escapado")
	var small: String = WorldEditor.with_size(grown, 8, 8)
	var values: Dictionary = WorldEditor.read(small)
	eq(values.width, 8, "largura nova")
	eq(values.settlements[0].x, 7, "assentamento puxado para dentro")
	eq(WorldEditor.read(WorldEditor.with_size(SCENARIO, 2, 500)).width, WorldEditor.MIN_SIDE, "limite mínimo")


func _transport() -> RefCounted:
	var transport := FakeTransport.new()
	transport.on("GET /periods/medieval/catalog", {"status": 200, "body": {"professionIds": [1], "professionNames": {}}})
	transport.on("GET /periods/medieval", {"status": 200, "body": {}, "text": '{"periodDefinition":' + SCENARIO + '}'})
	transport.on("GET /periods", {"status": 200, "body": [{"periodId": "medieval", "width": 10, "height": 10}]})
	transport.on("POST /worlds/preview", {"status": 200, "body": {"width": 10, "height": 10, "cells": [], "settlements": []}})
	transport.on("POST /worlds/create", {"status": 200, "body": {}})
	transport.on("GET /npcs?", {"status": 200, "body": {"total": 1, "items": [{"id": {"value": 1}, "name": "ana", "action": null, "cityId": {"value": "c"}, "location": {"x": 1, "y": 1}}]}})
	transport.on("GET /authoring/extraordinary/catalog", {"status": 200, "body": []})
	return transport


func test_menu_edits_flow_into_the_scenario_sent_on_create() -> void:
	var transport := _transport()
	var menu := MainMenu.new()
	menu.api = ApiClient.new(transport)
	tree.root.add_child(menu)
	await menu.show_new_world()
	menu.population_spin.value = 300
	menu._add_settlement()
	menu._change_settlement(1, {"x": 2, "y": 3})
	var values: Dictionary = WorldEditor.read(menu.draft)
	eq(values.population, 300, "população editada no rascunho")
	eq(values.settlements.size(), 2, "segundo assentamento")
	eq(values.settlements[1].x, 2, "posição editada")
	menu.selected_settlement = 1
	check(menu.preview_cell_at(menu.preview_rect.size / 2.0).x >= 0, "o centro da prévia é uma célula")
	check("POST /worlds/preview" in transport.calls, "a prévia usa o rascunho")
	await menu._on_create()
	check(menu.touchup != null, "criar abre os retoques")
	menu.free()


func test_touchup_rewrites_personality_through_authoring_route() -> void:
	var transport := _transport()
	transport.on("GET /npcs/1", {"status": 200, "body": {"personality": {"extroversion": 10}}})
	transport.on("PUT /authoring/npcs/1/personality", {"status": 200, "body": {}})
	var touchup := WorldTouchup.new()
	touchup.api = ApiClient.new(transport)
	touchup.api.validate_shapes = false
	tree.root.add_child(touchup)
	await touchup.load_npcs()
	eq(touchup.npc_ids, [1] as Array[int], "lista os NPCs")
	eq(touchup.power_pick.disabled, true, "catálogo vazio desabilita poderes")
	touchup.sliders.extroversion.value = 80
	check(touchup.personality_values().extroversion == 80, "slider vira valor inteiro")
	await touchup.apply_personality()
	check("PUT /authoring/npcs/1/personality" in transport.calls, "chamou a rota de autoria")
	check("aplicada" in touchup.message.text, "avisa o resultado")
	touchup.free()


const FamilyBuilder := preload("res://Scenes/family_builder.gd")
const JsonRaw := preload("res://Contract/json_raw.gd")


func _family() -> Array:
	return [{"settlement": "vila", "members": [
		{"name": "Ana", "sex": "Female", "age": 34, "profession": 1, "personality": {"Extroversion": 90}, "spouse": 1, "mother": -1, "father": -1},
		{"name": "Beto", "sex": "Male", "age": 36, "profession": 2, "personality": null, "spouse": 0, "mother": -1, "father": -1},
		{"name": "Cai", "sex": "Male", "age": 6, "profession": 1, "personality": null, "spouse": -1, "mother": 0, "father": 1},
	]}]


func test_set_member_inserts_when_absent_and_replaces_when_present() -> void:
	var added := JsonRaw.set_member('{"A":1}', "B", "[2]")
	eq(added, '{"A":1,"B":[2]}', "acrescenta antes do fecha-chaves")
	eq(JsonRaw.set_member(added, "B", "[3]"), '{"A":1,"B":[3]}', "troca quando existe")


func test_households_round_trip_as_integers() -> void:
	var out: String = WorldEditor.with_households(SCENARIO, _family())
	check('"AuthoredHouseholds":[{"Settlement":"vila","Members":[' in out, "bloco acrescentado")
	check('"AgeYears":34' in out and '"Spouse":1' in out and '"Mother":0' in out, "inteiros e vínculos")
	check('"Personality":{"Extroversion":90,"Agreeableness":50' in out, "personalidade completa com padrão 50")
	check(not (".0" in out.replace("0.25", "")), "nenhum inteiro virou decimal")
	var back: Array = WorldEditor.read_households(out)
	eq(back.size(), 1, "uma família")
	eq(back[0].members[2].mother, 0, "vínculo lido de volta")
	eq(back[0].members[1].personality, null, "personalidade sorteada continua ausente")


func test_workplaces_keep_untouched_fields_and_integers() -> void:
	var text := '{"Workplaces":[{"LocationTypeId":1,"X":4,"Y":5,"MaxVacancies":16,"Treasury":500000,"Stock":{"3":30},"Prices":{"1":5}}],"LocationTypeIds":[1,2]}'
	var list: Array = WorldEditor.read_workplaces(text)
	list[0].MaxVacancies = 20
	var out: String = WorldEditor.with_workplaces(text, list)
	check('"MaxVacancies":20' in out and '"Treasury":500000' in out and '"Stock":{"3":30}' in out, "campos preservados")
	check(not (".0" in out), "sem decimais")
	eq(WorldEditor.building_types(text), [1, 2] as Array[int], "tipos do cenário")


func test_builder_validates_like_the_server() -> void:
	var builder := FamilyBuilder.new()
	builder.setup(_family(), {1: "A", 2: "B"}, ["vila"])
	eq(builder.validation_error(), "", "família coerente passa")
	builder.families[0].members[2].mother = 1 # mãe que é homem
	check("Mãe" in builder.validation_error(), "mãe precisa ser mulher mais velha")
	builder.families[0].members[2].mother = 0
	builder.families[0].members[0].age = 5
	builder.families[0].members[1].age = 6
	check("adulto" in builder.validation_error(), "família sem adulto")
	builder.free()


func test_builder_removing_a_member_fixes_links() -> void:
	var builder := FamilyBuilder.new()
	builder.setup(_family(), {1: "A"}, [])
	tree.root.add_child(builder)
	builder.member_index = 0
	builder.remove_member()
	var members: Array = builder.families[0].members
	eq(members.size(), 2, "removeu Ana")
	eq(members[0].spouse, -1, "cônjuge sem par")
	eq(members[1].mother, -1, "mãe removida zera o vínculo")
	eq(members[1].father, 0, "pai desce um índice")
	builder.free()
