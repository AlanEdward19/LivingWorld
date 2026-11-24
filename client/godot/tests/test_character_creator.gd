extends "res://tests/test_base.gd"
## AD-197q: criador de personagem pelo motor. A tela só mostra o que o motor sorteou do agregado e pede: cidade, filtro, outra
## amostra, entrar. Transporte falso: nenhum servidor.

const Creator := preload("res://Scenes/character_creator.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")

const CITY := "11111111-1111-1111-1111-111111111111"


func _candidate(id: int, name: String, sex: String, prevents: bool = false) -> Dictionary:
	return {"npcId": id, "name": name, "sex": sex, "ageYears": 34, "professionId": 2, "profession": "Ferreiro", "cultureId": 1,
		"wealth": 300, "health": 70, "personality": {"extroversion": 20, "agreeableness": 50, "conscientiousness": 40,
			"emotionalStability": 30, "openness": 10, "ambition": 75, "loyalty": 80, "altruism": 90, "impulsivity": 5, "riskAversion": 60},
		"bornInCityId": CITY, "bornIn": "Alfa", "yearsInCity": 34, "willPrevents": prevents}


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /players/origins", {"status": 200, "body": {"cities": [{"cityId": CITY, "name": "Alfa", "aggregateCount": 40, "detailedCount": 12}]}})
	var samples := [0]
	transport.on("POST /players/p-1/candidates", func(_path):
		samples[0] += 1
		var list := [_candidate(101, "Ana Souza", "Female"), _candidate(102, "Rui Lima", "Male", true)] if samples[0] == 1 \
			else [_candidate(103, "Clara Dias", "Female")]
		return {"status": 200, "body": {"cityId": CITY, "cityName": "Alfa", "sample": samples[0] * 10, "aggregateCount": 40,
			"matching": 40, "candidates": list}})
	transport.on("POST /players/p-1/create", {"status": 200, "body": {"npcId": 103, "playerId": "p-1", "cityId": CITY}})
	return transport


func _creator(transport) -> Object:
	var api := ApiClient.new(transport)
	api.validate_shapes = true
	var hud := PlayerHud.new()
	hud.api = api
	hud.player_id = "p-1"
	var creator := Creator.new()
	creator.api = api
	creator.hud = hud
	tree.root.add_child(creator)
	tree.root.add_child(hud)
	return creator


func _free(creator) -> void:
	creator.hud.queue_free()
	creator.queue_free()


func test_opening_lists_the_cities_and_shows_the_cards_the_engine_drew() -> void:
	var transport := _transport()
	var creator = _creator(transport)

	await creator.open()

	eq(creator.city_select.get_item_text(0), "Alfa · 40 no agregado")
	eq(creator.cards.size(), 2, "um cartão por candidato do motor")
	check("Mulher, 34 anos" in creator.cards[0].text and "Ofício: Ferreiro" in creator.cards[0].text, creator.cards[0].text)
	check("Traços: generosidade, lealdade, ambição" in creator.cards[0].text, "os três traços mais fortes")
	check("parentes seguem no agregado" in creator.cards[0].text, "família: nada inventado")
	eq(creator.cards[1].button.disabled, true, "a vontade impede: não dá para entrar")
	eq(transport.bodies["POST /players/p-1/candidates"], [{"cityId": CITY}], "sem filtro, só a cidade")
	_free(creator)


func test_filter_and_another_sample_go_to_the_engine_and_entering_uses_the_shown_sample() -> void:
	var transport := _transport()
	var creator = _creator(transport)
	await creator.open()
	var entered := []
	creator.entered.connect(func(id, name, city): entered.append([id, name, city]))

	creator.sex_select.select(1)
	creator.min_age.value = 30
	await creator.resample()
	await creator.choose(103, "Clara Dias")

	eq(transport.bodies["POST /players/p-1/candidates"][1], {"cityId": CITY, "sex": "Female", "minAge": 30})
	eq(transport.bodies["POST /players/p-1/create"], [{"cityId": CITY, "npcId": 103, "sample": 20}], "a amostra que o motor deu")
	eq(entered, [[103, "Clara Dias", CITY]])
	eq(creator.is_open, false)
	eq(creator.hud.is_active, true)
	eq(creator.hud._title.text, "Você é Clara Dias")
	_free(creator)


func test_an_empty_aggregate_shows_the_engine_reason_and_no_card() -> void:
	var transport := _transport()
	transport.on("POST /players/p-1/candidates", {"status": 409, "body": {"error": "conflict: o agregado de Alfa não tem ninguém para materializar"}})
	var creator = _creator(transport)

	await creator.open()

	eq(creator.cards.size(), 0)
	eq(creator.message, "o agregado de Alfa não tem ninguém para materializar")
	_free(creator)


func test_a_refused_entry_keeps_the_screen_open_with_the_reason() -> void:
	var transport := _transport()
	transport.on("POST /players/p-1/create", {"status": 409, "body": {"error": "conflict: essa pessoa não está mais na amostra: peça outra"}})
	var creator = _creator(transport)
	await creator.open()

	await creator.choose(101, "Ana Souza")

	eq(creator.is_open, true)
	eq(creator.hud.is_active, false)
	eq(creator.message, "essa pessoa não está mais na amostra: peça outra")
	_free(creator)


func test_in_the_app_the_new_person_enters_the_city_of_origin_and_goes_straight_to_third_person() -> void:
	var transport := _transport()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 5 * 3600000, "tickDeltaMs": 3600000, "year": 0}})
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	var city := Fx.snapshot(0, {
		"id": {"value": CITY}, "name": "Alfa", "location": {"x": 1, "y": 1}, "aggregatePool": {}, "residents": [],
		"pendingResidentIds": [], "buildings": [], "layers": {}, "portals": [],
		"indicators": {"population": 3, "wealth": 1, "health": 1, "inequality": 0, "economy": 1, "housing": 0},
		"livingState": {"npcs": []}, "bounds": {"x": 0, "y": 0, "width": 2, "height": 2}, "boundsAreDerived": true}, "City")
	city.scope.kind = 1
	transport.on("GET /visual/subscribe?scope=City", {"status": 200, "body": city})
	transport.on("GET /npcs/103", Fx.npc_inspect([]))
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	app.player_hud.session_enabled = false

	await app.open_creator()
	var placed: Vector2 = app.creator.position
	check(placed.y >= app.router.MARGIN_TOP and placed.x >= app.router.MARGIN_LEFT,
		"encaixada na área central, abaixo da barra de cima (não no canto): %s" % placed)
	await app.creator.choose(103, "Clara Dias")
	for _i in 4:
		await tree.process_frame

	check(transport.calls.any(func(c): return c.begins_with("GET /visual/subscribe?scope=City&refId=" + CITY)), "assina a cidade de onde a pessoa saiu")
	eq(app.rig.mode, app.rig.THIRD, "entrou: 3ª pessoa do modo jogador")
	eq(app.selected_npc, 103)
	eq(app.player_hud.is_active, true)
	eq(app.creator.is_open, false)
	app.queue_free()
