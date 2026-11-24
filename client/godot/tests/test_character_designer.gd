extends "res://tests/test_base.gd"
## AD-197t: o criador de personagem de verdade. A tela monta a escolha, o motor valida e devolve a pessoa; a prévia e o resumo vêm
## da resposta, nunca da escolha local. Transporte falso: nenhum servidor.

const Creator := preload("res://Scenes/character_creator.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")

const CITY := "11111111-1111-1111-1111-111111111111"
const OTHER := "22222222-2222-2222-2222-222222222222"
const PREVIEW := "POST /players/p-1/character/preview"
const CREATE := "POST /players/p-1/character"


func _roles(child: bool, sibling: bool, spouse: bool) -> Dictionary:
	var role := func(on: bool) -> Dictionary:
		return {"available": on, "minAge": 18 if on else 0, "maxAge": 24 if on else 0, "reason": "" if on else "sem pais registrados"}
	return {"child": role.call(child), "sibling": role.call(sibling), "spouse": role.call(spouse), "relative": role.call(true)}


func _options() -> Dictionary:
	var range_of := func(lo: float, hi: float, typical: float) -> Dictionary: return {"min": lo, "max": hi, "typical": typical}
	return {"cityId": CITY, "name": "Alfa", "ageMin": 18, "ageMax": 89, "economy": true, "typicalMoney": 60, "moneyCap": 500,
		"heightM": {"female": range_of.call(1.4, 1.9, 1.62), "male": range_of.call(1.5, 2.0, 1.75)},
		"outfits": ["Camponês", "Artesão", "Burguês", "Nobre", "Clérigo", "Viajante"],
		"professions": [{"id": 1, "name": "Lavrador"}, {"id": 2, "name": "Ferreiro"}],
		"roles": ["newcomer", "child", "sibling", "spouse", "relative"],
		"origins": [{"cityId": OTHER, "name": "Beta"}],
		"families": [{"householdId": 7, "head": "Ana Souza", "members": [
			{"npcId": 11, "name": "Ana Souza", "sex": "Female", "ageYears": 41, "spouseId": 12, "roles": _roles(true, false, false)},
			{"npcId": 13, "name": "Davi Souza", "sex": "Male", "ageYears": 20, "spouseId": null, "roles": _roles(false, true, true)}]}]}


## A pessoa como o motor a devolve: o que a escolha trouxe e o que ele sorteou; `look` pode divergir da escolha (o motor manda).
func _character(spec: Dictionary, look: Dictionary = {}) -> Dictionary:
	var chosen: Dictionary = spec.get("look", {})
	var made := {"skinTone": int(chosen.get("skinTone", 55)), "hairTone": 40, "hairCurl": 20, "build": int(chosen.get("build", 50)),
		"eyeTone": 33, "faceShape": 61, "outfit": int(chosen.get("outfit", 2)), "heightM": float(chosen.get("heightM", 1.7)), "weightKg": 66.5}
	made.merge(look, true)
	var role: Dictionary = spec.get("role", {"kind": "newcomer"})
	return {"name": String(spec.get("name", "Sorteada Dias")), "sex": String(spec.get("sex", "Female")), "ageYears": int(spec.get("ageYears", 31)),
		"cityId": CITY, "cityName": "Alfa", "professionId": 1, "profession": "Lavrador", "cultureId": 1, "health": int(spec.get("health", 80)),
		"money": int(spec.get("money", 60)), "personality": {"extroversion": 50, "agreeableness": 50, "conscientiousness": 50,
			"emotionalStability": 50, "openness": 50, "ambition": 50, "loyalty": 50, "altruism": 50, "impulsivity": 50, "riskAversion": 50},
		"role": {"kind": String(role.kind), "relativeId": role.get("relativeId"), "relativeName": "Ana Souza" if role.has("relativeId") else "",
			"fromCityId": role.get("fromCityId")}, "look": made, "outfitName": "Nobre", "household": null}


func _transport() -> Object:
	var transport := FakeTransport.new()
	transport.on("GET /players/origins/" + CITY, {"status": 200, "body": _options()})
	transport.on("GET /players/origins", {"status": 200, "body": {"cities": [{"cityId": CITY, "name": "Alfa", "aggregateCount": 40, "detailedCount": 12}]}})
	transport.on(PREVIEW, func(_path):
		var spec: Dictionary = transport.bodies[PREVIEW].back()
		return {"status": 200, "body": {"character": _character(spec)}})
	transport.on(CREATE, func(_path):
		var spec: Dictionary = transport.bodies[CREATE].back()
		return {"status": 200, "body": {"npcId": 501, "playerId": "p-1", "cityId": CITY, "character": _character(spec)}})
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


func test_opening_loads_what_the_engine_offers_and_the_first_preview_sends_only_the_city_and_the_role() -> void:
	var transport := _transport()
	var creator = _creator(transport)

	await creator.open()

	var designer = creator.designer
	eq(creator.mode, "design", "o padrão é montar")
	eq(designer.profession_select.item_count, 3, "sortear + os dois ofícios do motor")
	eq(designer.outfit_select.get_item_text(4), "Nobre", "os trajes são os do motor")
	eq(designer.fields.money.input.max_value, 500.0, "o teto de dinheiro é o do motor")
	eq(designer.fields.money.input.value, 60.0, "o dinheiro típico da cidade")
	eq(transport.bodies[PREVIEW][0], {"cityId": CITY, "seed": 1, "role": {"kind": "newcomer"}}, "nada escolhido, nada enviado")
	eq(designer.create_button.disabled, false)
	eq(designer.state.name, "Sorteada Dias")
	_free(creator)


func test_every_choice_goes_to_the_engine_and_only_what_was_chosen() -> void:
	var transport := _transport()
	var creator = _creator(transport)
	await creator.open()
	var designer = creator.designer

	designer.name_edit.text = "Marina Teixeira"
	designer.sex_select.select(1)
	designer.profession_select.select(2)
	designer.set_field("age", 29)
	designer.set_field("money", 120)
	designer.set_field("health", 90)
	designer.set_field("heightM", 1.65)
	designer.set_field("look.skinTone", 25)
	designer.set_field("look.build", 70)
	designer.set_field("trait.openness", 80)
	designer.outfit_select.select(4)
	designer.origin_select.select(1)
	await designer.refresh()

	eq(transport.bodies[PREVIEW].back(), {"cityId": CITY, "seed": 1, "name": "Marina Teixeira", "sex": "Female", "ageYears": 29,
		"professionId": 2, "health": 90, "money": 120, "traits": {"openness": 80},
		"look": {"heightM": 1.65, "skinTone": 25, "build": 70, "outfit": 3}, "role": {"kind": "newcomer", "fromCityId": OTHER}})
	_free(creator)


func test_the_preview_is_drawn_from_the_state_the_engine_returned_not_from_the_local_choice() -> void:
	var transport := _transport()
	transport.on(PREVIEW, func(_path):
		var spec: Dictionary = transport.bodies[PREVIEW].back()
		return {"status": 200, "body": {"character": _character(spec, {"skinTone": 77, "heightM": 1.58, "outfit": 5})}})
	var creator = _creator(transport)
	await creator.open()
	var designer = creator.designer

	designer.set_field("look.skinTone", 10)
	designer.set_field("heightM", 1.9)
	await designer.refresh()

	eq(designer.figure.last_look.skin_tone, 77, "a pele que o motor devolveu, não a que se escolheu")
	eq(designer.figure.last_look.outfit, 5)
	eq(designer.figure.last_height_m, 1.58)
	check(designer.figure.actor != null and designer.figure.actor.look.has("eye_tone"), "o corpo do mundo recebe olhos e rosto do motor")
	check("1.58 m" in designer.summary.text, designer.summary.text)
	_free(creator)


func test_an_incoherent_choice_shows_the_engine_reason_and_blocks_creating() -> void:
	var transport := _transport()
	var creator = _creator(transport)
	await creator.open()
	var designer = creator.designer
	transport.on(PREVIEW, {"status": 409, "body": {"error": "conflict: idade fora da longevidade: de 18 a 89 anos"}})

	designer.set_field("age", 80)
	await designer.refresh()

	eq(designer.message, "idade fora da longevidade: de 18 a 89 anos")
	eq(designer.create_button.disabled, true, "sem prévia aceita não cria")
	eq(designer.figure.actor, null, "nada desenhado do que o motor recusou")
	await designer.create()
	check(not transport.bodies.has(CREATE), "a criação nem sai")
	_free(creator)


func test_the_family_list_comes_from_the_engine_and_only_lists_who_the_role_allows() -> void:
	var transport := _transport()
	var creator = _creator(transport)
	await creator.open()
	var designer = creator.designer

	designer.role_select.select(1) # filho
	designer._refill_families()
	eq(designer.family_select.item_count, 1, "só Ana pode ter um filho adulto")
	check("Ana Souza, 41 anos" in designer.family_select.get_item_text(0), designer.family_select.get_item_text(0))
	designer.role_select.select(3) # cônjuge
	designer._refill_families()
	eq(designer.family_select.item_count, 1, "só Davi está livre")
	check("Davi Souza" in designer.family_select.get_item_text(0))
	await designer.refresh()

	eq(transport.bodies[PREVIEW].back().role, {"kind": "spouse", "relativeId": 13})
	check("Casa com Ana Souza" in designer.summary.text or "Casa com" in designer.summary.text, designer.summary.text)
	designer.role_select.select(1)
	designer._refill_families()
	eq(designer.fields.age.input.max_value, 24.0, "a faixa de idade do laço, que o motor deu")
	_free(creator)


func test_reroll_asks_the_engine_for_another_draw_of_what_was_left_blank() -> void:
	var transport := _transport()
	var creator = _creator(transport)
	await creator.open()

	await creator.designer.reroll()

	eq(transport.bodies[PREVIEW].map(func(b): return b.seed), [1, 2])
	_free(creator)


func test_creating_uses_the_validated_choice_and_enters_the_world_as_that_person() -> void:
	var transport := _transport()
	var creator = _creator(transport)
	await creator.open()
	var entered := []
	creator.entered.connect(func(id, name, city): entered.append([id, name, city]))
	creator.designer.name_edit.text = "Marina Teixeira"
	await creator.designer.refresh()

	await creator.designer.create()

	eq(transport.bodies[CREATE], [{"cityId": CITY, "seed": 1, "name": "Marina Teixeira", "role": {"kind": "newcomer"}}])
	eq(entered, [[501, "Marina Teixeira", CITY]])
	eq(creator.is_open, false)
	eq(creator.hud.is_active, true)
	eq(creator.hud._title.text, "Você é Marina Teixeira")
	_free(creator)


func test_a_refused_creation_keeps_the_screen_open_with_the_reason() -> void:
	var transport := _transport()
	var creator = _creator(transport)
	await creator.open()
	transport.on(CREATE, {"status": 409, "body": {"error": "conflict: Davi Souza não aceita: sem afinidade com essa pessoa"}})

	await creator.designer.create()

	eq(creator.is_open, true)
	eq(creator.hud.is_active, false)
	eq(creator.designer.message, "Davi Souza não aceita: sem afinidade com essa pessoa")
	_free(creator)


func test_the_old_draw_from_the_city_stays_as_an_option() -> void:
	var transport := _transport()
	transport.on("POST /players/p-1/candidates", {"status": 200, "body": {"cityId": CITY, "cityName": "Alfa", "sample": 4, "aggregateCount": 40,
		"matching": 40, "candidates": []}})
	var creator = _creator(transport)

	await creator.open("design")
	await creator.set_mode("sample")

	eq(creator.designer.visible, false)
	eq(creator._sample_box.visible, true)
	check(transport.bodies.has("POST /players/p-1/candidates"), "pediu a amostra do agregado")
	_free(creator)


func test_in_the_app_the_created_person_enters_the_city_and_goes_straight_to_third_person() -> void:
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
	transport.on("GET /npcs/501", Fx.npc_inspect([]))
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	app.player_hud.session_enabled = false

	await app.open_creator()
	await app.creator.designer.create()
	for _i in 4:
		await tree.process_frame

	check(transport.calls.any(func(c): return c.begins_with("GET /visual/subscribe?scope=City&refId=" + CITY)), "assina a cidade")
	eq(app.rig.mode, app.rig.THIRD, "entrou: 3ª pessoa do modo jogador")
	eq(app.selected_npc, 501)
	eq(app.player_hud.is_active, true)
	app.queue_free()
