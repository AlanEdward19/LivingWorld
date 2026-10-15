extends "res://tests/test_base.gd"
## #46 fatia 4 (ADR-0033): criar família e local de trabalho com o mundo rodando, pelo console de admin.

const AdminWindow := preload("res://Scenes/admin_window.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const WorldEditor := preload("res://Scenes/world_editor.gd")
const Fx := preload("res://tests/fixtures.gd")


func _family() -> Dictionary:
	return {"settlement": "", "members": [
		{"name": "Ana", "sex": "Female", "age": 30, "profession": 1, "personality": null, "spouse": 1, "mother": -1, "father": -1,
			"appearance": {"SkinTone": 12, "HairTone": 34, "HairCurl": 56, "Build": 78}, "skills": {7: 15, 0: 40}},
		{"name": "Beto", "sex": "Male", "age": 32, "profession": 2, "personality": null, "spouse": 0, "mother": -1, "father": -1,
			"appearance": null, "skills": {}},
	]}


func _window(transport) -> Object:
	var window := AdminWindow.new()
	var api := ApiClient.new(transport)
	api.validate_shapes = true # usa os contratos congelados em tests/contracts
	window.api = api
	window.actor = "alan"
	window.open()
	return window


func test_household_body_is_the_scenario_item_with_integers_as_integers() -> void:
	var body: Dictionary = WorldEditor.household_body(_family())
	var text := JSON.stringify(body)
	check('"AgeYears":30' in text and '"Spouse":1' in text, "inteiros e vínculos: " + text)
	check(not ('"AgeYears":30.0' in text), "nenhum inteiro virou decimal")
	check('"Appearance":{' in text and '"SkinTone":12' in text, "aparência da Ana")
	check('"Skills":{"0":40,"7":15}' in text or '"Skills":{"7":15,"0":40}' in text, "habilidades por id: " + text)
	check(not body.Members[1].has("Appearance") and not body.Members[1].has("Skills"), "quem não tem nada não leva campos extras")
	check(not body.has("Settlement"), "assentamento vazio = cidade mais antiga")
	var named := _family()
	named.settlement = "vila"
	eq(WorldEditor.household_body(named).Settlement, "vila", "assentamento nomeado vai junto")


func test_workplace_body_matches_the_scenario_item() -> void:
	var text := JSON.stringify(WorldEditor.workplace_body(1, Vector2i(5, 6), 4, 1000))
	check('"LocationTypeId":1' in text and '"X":5' in text and '"Y":6' in text, text)
	check('"MaxVacancies":4' in text and '"Treasury":1000' in text and '"Stock":{}' in text, text)
	check(not (".0" in text), "inteiros como inteiros")


func test_creating_families_posts_each_one_and_summarises() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /authoring/households", {"status": 200, "body": {"householdId": 5, "cityId": "c1", "npcIds": [41, 42]}})
	var window := _window(transport)

	await window.create_households([_family(), _family()])

	eq(transport.bodies["POST /authoring/households"].size(), 2, "uma chamada por família")
	eq(transport.headers_of["POST /authoring/households"][0], {"X-Admin-Actor": "alan"}, "operador no cabeçalho")
	check("2 família(s) chegaram (4 pessoa(s))" in window.create_state, window.create_state)
	window.free()


func test_server_refusals_are_shown_in_words() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /authoring/households", {"status": 400, "body": {"error": "AuthoredHouseholds[0].Members[0].ProfessionId: fora do catálogo do período"}})
	transport.on("POST /authoring/workplaces", {"status": 409, "body": {"error": "o mundo está em pausa de admin"}})
	var window := _window(transport)
	var warnings: Array = []
	window.warning.connect(func(text: String): warnings.append(text))

	await window.create_households([_family()])
	check("ProfessionId" in window.create_state, window.create_state)
	await window.create_workplace()
	check("pausa de admin" in window.create_state, window.create_state)
	eq(warnings.size(), 2, "cada recusa também vira aviso")
	window.free()


func test_a_workplace_is_requested_from_the_form_values() -> void:
	var transport := FakeTransport.new()
	transport.on("POST /authoring/workplaces", {"status": 200, "body": {"workplaceId": 9, "buildingId": 3, "cityId": "c1", "x": 5, "y": 6}})
	var window := _window(transport)
	window.location_type_spin.value = 2
	window.cell_x_spin.value = 5
	window.cell_y_spin.value = 6
	window.vacancies_spin.value = 8
	window.treasury_spin.value = 500

	await window.create_workplace()

	var sent: Dictionary = transport.bodies["POST /authoring/workplaces"][0]
	eq([sent.LocationTypeId, sent.X, sent.Y, sent.MaxVacancies, sent.Treasury], [2, 5, 6, 8, 500], "valores do formulário")
	check("célula (5, 6)" in window.create_state, window.create_state)
	window.free()


func test_the_family_button_asks_the_app_for_the_builder() -> void:
	var window := _window(FakeTransport.new())
	var asked := [false]
	window.family_requested.connect(func(): asked[0] = true)
	window.family_button.pressed.emit()
	eq(asked[0], true, "o botão pede o criador de pessoas")
	window.free()
