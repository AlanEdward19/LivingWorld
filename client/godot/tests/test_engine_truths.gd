extends "res://tests/test_base.gd"
## AD-189: o que o cliente desenhava sem o motor dizer passa a vir do motor — direção do vento (`windFromDeg`), folhagem
## (`foliage`), curva do dia por hora (`hourlyC`/`hourlyFeelsLikeC`, o clima não volta a cada tick), luz do prédio (`lit`) e
## os alvos novos de `intentTarget` (`city`, `water`, `site`, `leaving`).

const O := preload("res://tests/observable_fixtures.gd")
const S := preload("res://tests/test_street_life.gd")
const W := preload("res://tests/test_weather.gd")
const Look := preload("res://Scenes/render/weather_look.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
const Parse := preload("res://Store/observable_parse.gd")


func test_rain_leans_where_the_engine_wind_blows_and_falls_straight_without_a_direction() -> void:
	eq(Look.wind_push({"wind": 10.0}), Vector2.ZERO, "sem direção do motor: nada inclina")
	var from_west := Look.wind_push({"wind": 10.0, "wind_from": 270.0})
	check(from_west.x > 0.4 and absf(from_west.y) < 0.01, "vento de oeste empurra para leste (+x): %s" % [from_west])
	var from_north := Look.wind_push({"wind": 20.0, "wind_from": 0.0})
	check(from_north.y > 0.9, "vento norte empurra para o sul (+y do mapa = +z): %s" % [from_north])
	var app = await S.city_app(tree)
	app.stream.delta_applied.emit(O.delta({"climate": W.climate({"windFromDeg": 225})}))
	var tilt: Vector3 = app.world_view.weather.tilt
	check(tilt.x > 0.0 and tilt.z < 0.0, "sudoeste: a chuva vai para nordeste (+x, −z): %s" % [tilt])
	app.stream.delta_applied.emit(O.delta({"climate": W.climate()}))
	eq(app.world_view.weather.tilt, Vector3.ZERO, "o motor deixou de dizer a direção: queda reta")
	app.queue_free()


func test_crowns_are_full_turn_and_go_bare_only_by_the_engine_foliage() -> void:
	var base := Color("4a8a3a")
	var full := {"weather": {"foliage": {"fraction": 1.0, "phase": "full", "turning": 0.0}}}
	var turning := {"weather": {"foliage": {"fraction": 1.0, "phase": "full", "turning": 1.0}}}
	var bare := {"weather": {"foliage": {"fraction": 0.0, "phase": "bare", "turning": 0.0}}}
	eq(Season.crown(base, full, 0.2), base, "cheia: a cor de verão")
	check(Season.crown(base, turning, 0.2).r > 0.5, "virada: alaranjada")
	eq(Season.crown_scale(full), 1.0)
	check(Season.crown_scale(bare) < 0.5, "nua: só galho")
	eq(Season.crown_scale({"season_key": "winter"}), 1.0, "sem folhagem do motor, o inverno não tira a copa")
	var app = await S.city_app(tree)
	app.stream.delta_applied.emit(O.delta({"climate": W.climate({"foliage": {"fraction": 0.0, "phase": "bare", "turning": 0.0}})}))
	var scenery = app.world_view.terrain.scenery
	if scenery != null and scenery.get_node_or_null("Crowns") != null:
		check(scenery.crown_scale < 0.5, "a cena encolhe as copas pela folhagem do motor")
	app.queue_free()


func test_the_hour_comes_from_the_engine_curve_when_the_climate_is_not_resent() -> void:
	var hourly: Array = []
	var feels: Array = []
	for h in 24:
		hourly.append(float(h))
		feels.append(float(h) - 3.0)
	var parsed := Parse.climate(W.climate({"hourlyC": hourly, "hourlyFeelsLikeC": feels}))
	check(Parse.at_hour(parsed.weather, 24 * 3 + 15), "a hora mudou")
	eq([parsed.weather.now_c, parsed.weather.feels_c], [15.0, 12.0], "15 h da curva do motor")
	check(not Parse.at_hour(parsed.weather, 15), "a mesma hora: nada muda")
	var app = await S.city_app(tree)
	var envelope := O.delta({"climate": W.climate({"hourlyC": hourly, "hourlyFeelsLikeC": feels})})
	envelope.payload.second = 5 * 3600
	app.stream.delta_applied.emit(envelope)
	eq(app.store.climate.weather.now_c, 5.0)
	var next := O.delta({})
	next.payload.second = 9 * 3600
	app.stream.delta_applied.emit(next)
	eq(app.store.climate.weather.now_c, 9.0, "delta sem clima: a hora sai da curva")
	app.queue_free()


func test_parse_reads_wind_direction_foliage_and_light() -> void:
	var parsed := Parse.climate(W.climate({"windFromDeg": 200, "foliage": {"fraction": 0.4, "phase": "falling", "turning": 1.0}}))
	eq(parsed.weather.wind_from, 200.0)
	eq(parsed.weather.foliage, {"fraction": 0.4, "phase": "falling", "turning": 1.0})
	eq(Parse.climate(W.climate()).weather.wind_from, -1.0, "ausente: o motor não disse")
	eq(Parse.climate(W.climate()).weather.foliage, {}, "ausente: sem folhagem")
	check(Parse.building_state(O.building_state_lit(4, true)).lit, "lit do motor")
	check(not Parse.building_state({"buildingId": 4}).lit, "sem lit: escuro")


func test_dashed_line_reaches_the_new_engine_targets() -> void:
	var app = await S.city_app(tree)
	app.world_view.highlight_npc(7)
	var renderer = app.world_view.npcs
	for target in ["water:3,4", "site:2,2", "leaving:30"]:
		app.store.npcs[7].intent_target = target
		check(renderer.goal_point(7) != Vector3.INF, "alvo %s desenhado" % target)
	app.store.npcs[7].intent_target = "water:3,4"
	eq(renderer.goal_point(7), Vector3(3.5, 0, 4.5), "a fonte na célula que o motor diz")
	app.store.npcs[7].intent_target = "city:%s" % String(app.store.city.get("id", "city-a"))
	if app.store.city.get("location") is Vector2i:
		check(renderer.goal_point(7) != Vector3.INF, "o largo da cidade")
	app.queue_free()
