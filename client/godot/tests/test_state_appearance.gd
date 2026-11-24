extends "res://tests/test_base.gd"
## Etapa 3 do ADR-0047: a store consome a projeção do observável (AD-170) e o corpo, a lavoura, a fauna, o espólio, a
## estação e a cidade no mapa-múndi são desenhados a partir do estado (formas por código).

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Store := preload("res://Store/world_store.gd")
const Look := preload("res://Scenes/npc_look.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const CropRenderer := preload("res://Scenes/render/crop_renderer.gd")
const CityRenderer := preload("res://Scenes/render/city_renderer.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const Registry := preload("res://Store/entity_registry.gd")


func test_store_reads_every_observable_collection_from_the_city_snapshot() -> void:
	var store := Store.new()
	store.apply_snapshot(O.city())
	eq(store.names.get(7), "Ana", "o nome vem do escopo")
	eq(store.npcs[7].obs.pregnancy, 0.6, "gravidez aparente")
	eq(store.npcs[8].obs.mourning, true)
	eq(store.npcs[8].obs.carried[0].quantity, 6, "carga pessoal")
	eq(store.building_states[4].owner_id, 8, "dono do prédio")
	eq(store.building_states[4].stock[0].name, "Trigo")
	eq(store.crops[70].stage, "growing")
	eq(store.crops[70].expected, 120, "colheita esperada")
	eq(store.animals[90].species, "rabbit")
	eq(store.leavings[30].dead_id, 9)
	eq(store.objects[501].shape, "cylinder")
	eq(store.climate.season_key, "summer")
	eq(store.climate.temperature, 24.0)


func test_delta_without_observable_keeps_the_body_and_a_change_is_announced() -> void:
	var store := Store.new()
	store.apply_snapshot(O.city())
	var observed: Array = []
	store.npc_observed.connect(func(id): observed.append(id))
	store.apply_delta(O.delta({"npcUpserts": [{"id": {"value": 7}, "location": {"x": 4, "y": 4}, "currentAction": 4}]}))
	eq(store.npcs[7].obs.pregnancy, 0.6, "observable omitido = não mudou (o NPC só andou)")
	eq(observed, [], "andar não refaz o corpo")
	store.apply_delta(O.delta({"npcUpserts": [{"id": {"value": 7}, "location": {"x": 4, "y": 4}, "currentAction": 3,
		"actionStartedAtMs": 11 * 3600000, "actionTarget": {"kind": "npc", "id": "8"}, "observable": O.observable("Ana", {"illness": {"diseaseId": 2, "symptomatic": true,
		"bedridden": true}})}]}))
	eq(observed, [7], "sinal visível novo avisa a cena")
	eq(store.npcs[7].obs.bedridden, true)
	eq(store.npcs[7].target, {"kind": "npc", "id": "8"}, "alvo da ação")


func test_delta_upserts_and_removals_of_crops_fauna_items_states_and_climate() -> void:
	var store := Store.new()
	store.apply_snapshot(O.world_with_density())
	eq(store.cities["city-a"].density.inside, 12, "densidade da cidade no mapa-múndi")
	eq(store.fauna_groups["wolf:1"].count, 7)
	var signals: Array = []
	for name in ["crops_changed", "fauna_changed", "items_changed", "climate_changed"]:
		store.connect(name, func(): signals.append(name))
	store.apply_delta(O.delta({"cropUpserts": [O.crop(71, "mature")], "faunaGroupRemoved": ["wolf:1"],
		"leavingUpserts": [{"id": 31, "location": {"x": 1, "y": 1}, "deadId": 5, "atMs": 3600000, "units": 2}],
		"climate": {"month": 0, "seasonIndex": 0, "seasonKey": "winter", "temperatureC": -3.0, "regionId": 0,
			"cropYearFactorMilli": 1000, "drought": false},
		"cityUpserts": [{"id": {"value": "city-a"}, "name": "Alfa", "location": {"x": 2, "y": 2}, "population": 4,
			"bounds": {"x": 0, "y": 0, "width": 6, "height": 6}}]}))
	eq(store.crops[71].stage, "mature")
	eq(store.fauna_groups.size(), 0, "bando removido pela chave espécie:região")
	eq(store.leavings[31].units, 2)
	eq(store.climate.season_key, "winter")
	eq(store.cities["city-a"].density.inside, 12, "cidade sem densidade no delta mantém a última")
	eq(signals.size(), 4, "um aviso por coleção: " + str(signals))
	store.apply_delta(O.delta({"cropRemoved": [71], "buildingStateRemoved": [4]}))
	eq(store.crops.has(71), false)


func test_look_derives_the_body_from_age_height_sex_trade_pregnancy_illness_and_mourning() -> void:
	var baby := Look.spec_of({"age": 1, "stage": "Child", "height_cm": 70, "sex": "Male", "profession_id": -1})
	var child := Look.spec_of({"age": 8, "stage": "Child", "height_cm": 120, "profession_id": 1, "profession_name": "Lavrador"})
	var adult := Look.spec_of({"age": 30, "stage": "Adult", "height_cm": 170, "sex": "Female", "profession_id": 1,
		"profession_name": "Lavradora", "pregnancy": 0.7})
	var elder := Look.spec_of({"age": 75, "stage": "Elder", "height_cm": 160, "profession_id": 2, "profession_name": "Ferreiro"})
	eq(baby.stage, "baby")
	check(baby.scale < child.scale and child.scale < adult.scale and elder.scale < adult.scale, "escala por altura")
	eq(child.hat, "none", "criança sem chapéu nem ferramenta")
	eq(child.tool, "")
	eq(adult.hat, "straw", "lavrador: chapéu de palha")
	eq(adult.tool, "hoe")
	eq(elder.tool, "hammer", "ferreiro: martelo")
	eq(adult.belly, 0.7, "barriga de gravidez")
	eq(adult.female, true)
	var mourning := Look.spec_of({"age": 30, "stage": "Adult", "mourning": true, "profession_id": 3})
	eq(mourning.cloth, Look.MOURNING_CLOTH, "roupa de luto")
	eq(mourning.expression, "grief")
	eq(mourning.posture, "down", "cabeça baixa")
	var sick := Look.spec_of({"age": 30, "ill": true, "bedridden": true, "symptomatic": true})
	eq(sick.sick_pose, "bed", "de cama")
	var water := Look.spec_of({"age": 30, "carried": [{"resource_id": 1, "quantity": 3, "name": "Água"}]})
	eq(water.sack.water, true, "água vira jarro")
	check("carrega 3 Água" in Look.hint_of({"age": 30, "carried": [{"resource_id": 1, "quantity": 3, "name": "Água"}]}))


func test_actor_wears_the_state_and_shows_tool_and_face_only_up_close() -> void:
	var actor := NpcActor.new()
	tree.root.add_child(actor)
	actor.setup(1, Vector2i.ZERO, 2, null)
	actor.apply_observable({"age": 30, "stage": "Adult", "height_cm": 170, "sex": "Female", "profession_id": 1,
		"profession_name": "Lavradora", "pregnancy": 0.6, "dominant": "joy",
		"carried": [{"resource_id": 2, "quantity": 5, "name": "Trigo"}], "held": {"item_id": 4, "design_id": 77}})
	check(actor._hat != null, "chapéu do ofício")
	check(actor._belly != null and actor._belly.visible, "barriga")
	check(actor._sack != null, "saco nas costas")
	eq(actor._face, null, "longe: sem rosto")
	check(actor._tool == null or not actor._tool.visible, "longe: sem ferramenta")
	actor.set_detail(true)
	check(actor._face != null and actor._face.visible, "perto: rosto com expressão")
	check(actor._tool != null and actor._tool.visible, "perto: ferramenta na mão no trabalho")
	check(actor._held != null and actor._held.visible, "perto: recipiente na mão")
	actor.apply_observable({"age": 30, "stage": "Adult", "height_cm": 170, "profession_id": 1, "ill": true, "bedridden": true})
	check(actor._sickbed != null and actor._sickbed.visible, "doente de cama ganha a esteira")
	actor.animate(0.1)
	check(absf(actor._body.rotation.x) > 1.0, "deitado")
	actor.queue_free()


func test_city_bodies_come_from_the_scope_for_everyone() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	var ana = app.world_view.npc_nodes[7]
	var beto = app.world_view.npc_nodes[8]
	check(ana._belly != null and ana._belly.visible, "Ana grávida, sem abrir a ficha")
	eq(beto.spec.cloth, Look.MOURNING_CLOTH, "Beto de luto")
	check(beto._sack != null, "Beto carrega trigo")
	app.stream.delta_applied.emit(O.delta({"npcUpserts": [{"id": {"value": 7}, "location": {"x": 3, "y": 3}, "currentAction": 3,
		"actionStartedAtMs": 11 * 3600000, "actionTarget": {"kind": "npc", "id": "8"}}, {"id": {"value": 8}, "location": {"x": 4, "y": 3}, "currentAction": 3,
		"actionStartedAtMs": 11 * 3600000, "actionTarget": {"kind": "npc", "id": "7"}}]}))
	app.world_view.npcs.refresh_links()
	eq(app.world_view.npcs.link_pairs, [[7, 8]], "linha discreta entre quem conversa")
	check(ana.face_point.distance_to(beto.position) < 0.01, "vira para quem conversa")
	check("com Beto" in app.describe_hit(Registry.ref(Registry.PERSON, 7)), app.describe_hit(Registry.ref(Registry.PERSON, 7)))
	app.queue_free()


func test_crops_are_drawn_by_stage_and_culture() -> void:
	eq(CropRenderer.plants_of({"stage": "unsown"}).count, 0, "sem semear: só sulcos")
	var mature := CropRenderer.plants_of({"stage": "mature", "resource_name": "Trigo"})
	eq(mature.color, Color("d9b44a"), "trigo maduro é dourado")
	var corn := CropRenderer.plants_of({"stage": "mature", "resource_name": "Milho"})
	check(corn.height > mature.height, "milho é mais alto")
	check(CropRenderer.plants_of({"stage": "harvested"}).height < 0.1, "colhido: restolho")
	check(CropRenderer.plants_of({"stage": "growing", "fraction": 0.9}).height > CropRenderer.plants_of({"stage": "growing", "fraction": 0.1}).height)
	var thirsty := CropRenderer.plants_of({"stage": "growing", "fraction": 0.5, "thirsty": true})
	check(thirsty.color != CropRenderer.plants_of({"stage": "growing", "fraction": 0.5}).color, "com sede: amarelado")
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	eq(app.world_view.crops.plant_count(), 9, "um canteiro com 9 plantas")
	app.stream.delta_applied.emit(O.delta({"cropUpserts": [O.crop(70, "harvested")]}))
	check(app.world_view.crops.positions.has(70))
	app.queue_free()


func test_world_map_shows_density_fauna_groups_and_lod_levels() -> void:
	var fake = O.transport()
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": O.world_with_density()})
	var app = await T.make_app(tree, fake)
	eq(app.world_view.cities.dot_count, 12, "um ponto por morador na rua")
	var text: String = app.world_view.cities.label_text(app.store.cities["city-a"])
	check("12 na rua" in text and "40 no pool" in text and "trabalhando 8" in text, text)
	check(app.world_view.fauna.group_positions.has("wolf:1"), "bando de lobos no mapa")
	eq(WorldView.lod_for("World", 30.0), "world")
	eq(WorldView.lod_for("World", 5.0), "near", "quem está no campo também, de perto")
	eq(WorldView.lod_for("City", 12.0), "city")
	eq(WorldView.lod_for("City", 5.0), "near")
	app.queue_free()


const WEATHER_SNOW := {"precipitation": "snow", "intensity": "moderate", "precipitationMm": 4.0, "storm": false, "hail": false,
	"frost": true, "drought": false, "heatWave": false, "coldWave": false, "windMs": 3.0, "temperatureNowC": -4.0, "feelsLikeC": -7.0,
	"minC": -6.0, "maxC": -1.0, "daylightHours": 8.0, "sunriseHour": 8.0, "sunsetHour": 16.0, "ground": "snow", "snowCm": 6.0,
	"groundWetPm": 300, "outdoorLostDays": 4}


func test_season_recolors_ground_snow_falls_and_hud_shows_temperature() -> void:
	var winter := {"season_key": "winter", "temperature": -4.0, "drought": false,
		"weather": {"precipitation": "snow", "intensity": "moderate", "ground": "snow", "snow_cm": 10.0, "now_c": -4.0}}
	check(Season.snowing(winter))
	check(not Season.snowing({"season_key": "winter", "temperature": -4.0}), "sem o tempo do motor, nada de neve inventada")
	check(Season.ground(Color("6aa84f"), winter).v > Color("6aa84f").v + 0.2, "neve clareia o chão")
	check(Season.ground(Color("6aa84f"), {"season_key": "summer", "temperature": 30.0, "drought": true}).r > 0.5, "seca amarela")
	check(Season.crown(Color("4a8a3a"), {"season_key": "autumn", "temperature": 10.0, "weather": {"foliage": {"fraction": 1.0,
		"phase": "full", "turning": 1.0}}}, 0.1).r > 0.5, "a copa que o motor diz virada fica alaranjada")
	eq(Season.crown(Color("4a8a3a"), {"season_key": "autumn", "temperature": 10.0}, 0.1), Color("4a8a3a"),
		"sem folhagem do motor, a estação não inventa cor")
	check("Inverno · -4 °C" in Season.hud_text(winter), Season.hud_text(winter))
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	eq(app.world_view.snow.emitting, false)
	app.stream.delta_applied.emit(O.delta({"climate": {"month": 0, "seasonIndex": 0, "seasonKey": "winter", "temperatureC": -4.0,
		"regionId": 0, "cropYearFactorMilli": 1000, "drought": false, "weather": WEATHER_SNOW}}))
	eq(app.world_view.snow.emitting, true, "o clima diz neve")
	app._update_time_label({"atMs": 5 * 3600000, "year": 1, "isPaused": false})
	check("-4 °C" in app.time_label.text, app.time_label.text)
	app.queue_free()


func test_city_renderer_label_without_density_stays_simple() -> void:
	var renderer := CityRenderer.new()
	eq(renderer.label_text({"name": "Alfa", "population": 3}), "Alfa · 3 hab.")
	eq(CityRenderer.dots_of({"id": "x", "bounds": Rect2i(0, 0, 4, 4), "density": {"inside": 100, "actions": []}}).size(),
		CityRenderer.MAX_DOTS, "no máximo MAX_DOTS por cidade")
	renderer.free()
