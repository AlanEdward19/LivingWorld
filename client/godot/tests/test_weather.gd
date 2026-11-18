extends "res://tests/test_base.gd"
## AD-188 bloco 1: o tempo do dia do motor (AD-180) na tela. Formas de tests/contracts/shape.city-subscribe.json
## (`livingState.climate.weather`, `layers.Climate.payload.regions[].weather`, `roads[].ground/snowCm/wetPm`).

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const S := preload("res://tests/test_street_life.gd")
const Look := preload("res://Scenes/render/weather_look.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
const Parse := preload("res://Store/observable_parse.gd")
const DayCycle := preload("res://Scenes/day_cycle.gd")
const Style := preload("res://Scenes/render/street_style.gd")


static func weather(extra := {}) -> Dictionary:
	var body := {"precipitation": "rain", "intensity": "heavy", "precipitationMm": 18.0, "storm": true, "hail": false,
		"frost": false, "drought": false, "heatWave": false, "coldWave": true, "windMs": 15.0, "temperatureNowC": 6.0,
		"feelsLikeC": 1.0, "minC": 3.0, "maxC": 8.0, "daylightHours": 8.5, "sunriseHour": 7.75, "sunsetHour": 16.25,
		"ground": "mud", "snowCm": 0.0, "groundWetPm": 520, "outdoorLostDays": 12}
	body.merge(extra, true)
	return body


static func climate(extra := {}) -> Dictionary:
	return {"month": 10, "seasonIndex": 3, "seasonKey": "autumn", "temperatureC": 7.0, "regionId": 0,
		"cropYearFactorMilli": 1000, "drought": false, "weather": weather(extra)}


func test_parse_reads_every_weather_field_and_absent_weather_is_empty() -> void:
	var parsed := Parse.climate(climate())
	eq([parsed.weather.precipitation, parsed.weather.intensity, parsed.weather.ground], ["rain", "heavy", "mud"])
	eq([parsed.weather.sunrise, parsed.weather.sunset, parsed.weather.wind, parsed.weather.feels_c], [7.75, 16.25, 15.0, 1.0])
	check(parsed.weather.storm and parsed.weather.cold_wave and not parsed.weather.heat_wave, "flags")
	eq(Parse.climate({"seasonKey": "winter", "temperatureC": -5.0}).weather, {}, "sem tempo do motor = vazio")
	eq(Look.falling({}).kind, "", "sem tempo, nada cai")


func test_rain_falls_with_intensity_and_lod_and_hud_shows_the_day() -> void:
	var app = await S.city_app(tree)
	eq(app.world_view.weather.rain.emitting, false, "verão do fixture: sem chuva")
	app.stream.delta_applied.emit(O.delta({"climate": climate()}))
	var view = app.world_view
	view.weather.follow(view.view_center, 20.0, Look.lod_ratio("city", false, false))
	check(view.weather.rain.emitting, "chuva forte cai")
	eq(view.weather.ratio, 1.0, "intensidade forte, câmera de cidade: todas as gotas")
	eq(view.weather.rain.amount, Look.RAIN_CAP, "teto fixo de partículas")
	view.weather.follow(view.view_center, 20.0, Look.lod_ratio("city", true, false))
	eq(view.weather.ratio, 0.5, "câmera longe: metade")
	view.weather.follow(view.view_center, 20.0, Look.lod_ratio("world", false, true))
	check(not view.weather.rain.emitting, "mapa-múndi afastado: nada")
	check("tempestade" in app.weather_label.text and "sente 1 °C" in app.weather_label.text and "vento 15 m/s" in app.weather_label.text
		and "lama" in app.weather_label.text and "onda de frio" in app.weather_label.text, app.weather_label.text)
	check("07:45" in app.weather_label.tooltip_text and "16:15" in app.weather_label.tooltip_text, app.weather_label.tooltip_text)
	app._update_time_label({"tick": 5, "year": 1, "isPaused": false})
	check("6 °C" in app.time_label.text, "temperatura da hora: " + app.time_label.text)
	eq([app.day_cycle.sunrise, app.day_cycle.sunset], [7.75, 16.25], "sol do motor")
	app.stream.delta_applied.emit(O.delta({"climate": climate({"precipitation": "snow", "intensity": "light", "storm": false})}))
	view.weather.follow(view.view_center, 20.0, 1.0)
	check(view.weather.snow.emitting and not view.weather.rain.emitting, "neve fraca no lugar da chuva")
	eq(view.weather.ratio, 0.25)
	app.queue_free()


func test_sunrise_and_sunset_come_from_the_engine() -> void:
	var cycle := DayCycle.new()
	cycle.set_weather(Parse.weather(weather({"sunriseHour": 8.0, "sunsetHour": 16.0})))
	cycle.hour = 7.5
	eq([cycle.daylight(), cycle.phase_name()], [0.0, "Noite"], "antes do nascer do motor é noite")
	cycle.hour = 12.0
	eq(cycle.phase_name(), "Dia")
	cycle.hour = 17.0
	eq(cycle.phase_name(), "Noite", "o sol de inverno do motor já se pôs às 16 h")
	cycle.set_weather({})
	cycle.hour = 17.0
	eq(cycle.phase_name(), "Entardecer", "sem tempo do motor: 5 h–19 h de antes")
	cycle.free()


func test_road_cell_ground_tints_and_puddles_come_from_each_cell() -> void:
	var dry := Color("8a6f4e")
	check(Look.ground_tint(dry, "mud", 0.0).get_luminance() < dry.get_luminance() - 0.05, "lama escurece")
	check(Look.ground_tint(dry, "wet", 0.0).get_luminance() < dry.get_luminance(), "molhado escurece")
	check(Look.ground_tint(dry, "snow", 12.0).get_luminance() > dry.get_luminance() + 0.3, "neve clareia")
	eq(Look.ground_tint(dry, "dry", 0.0), dry, "seco fica como é")
	eq([Look.puddles("mud"), Look.puddles("heavy-mud"), Look.puddles("wet")], [1, 2, 0], "poças só na lama")
	var snapshot := S.city()
	var roads: Array = snapshot.payload.livingState.roads
	roads[0]["ground"] = "heavy-mud"
	roads[0]["wetPm"] = 820
	roads[1]["ground"] = "dry"
	var app = await S.city_app(tree, snapshot)
	var ground = app.world_view.ground
	eq(app.store.roads[Vector2i(1, 4)].ground, "heavy-mud", "chão da célula lido")
	eq(app.store.roads[Vector2i(1, 4)].wet_pm, 820)
	check(not app.store.roads[Vector2i(1, 4)].extra.has("ground"), "campo conhecido não vai para extra")
	var muddy: int = ground.road_pieces
	app.store.roads[Vector2i(1, 4)].ground = "dry"
	ground.rebuild()
	eq(muddy - ground.road_pieces, 2, "lamaçal tem duas poças")
	app.queue_free()


func test_terrain_snow_only_when_the_engine_says_and_world_uses_region_under_camera() -> void:
	var autumn := {"season_key": "autumn", "temperature": -2.0}
	var base := Color("6aa84f")
	eq(Season.snow_cover(autumn), 0.0, "frio sem neve no chão do motor: sem neve")
	var covered := autumn.duplicate()
	covered.weather = Parse.weather(weather({"ground": "snow", "snowCm": 10.0, "precipitation": "none"}))
	check(Season.ground(base, covered).get_luminance() > base.get_luminance() + 0.3, "neve do motor cobre o terreno")
	var env := T.world()
	env.payload.layers["Climate"] = {"isModeled": true, "payload": {"month": 1, "seasonIndex": 3, "seasonKey": "winter", "regions": [
		{"regionId": 0, "center": {"x": 1, "y": 1}, "temperatureC": 2.0, "cropYearFactorMilli": 1000, "drought": false, "weather": weather()},
		{"regionId": 1, "center": {"x": 7, "y": 7}, "temperatureC": -3.0, "cropYearFactorMilli": 1000, "drought": false,
			"weather": weather({"precipitation": "snow", "intensity": "moderate", "storm": false})}]}}
	var fake = T.transport()
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": env})
	var app = await T.make_app(tree, fake)
	eq(app.store.weather_at(Vector2i(0, 0)).precipitation, "rain", "região 0")
	eq(app.store.weather_at(Vector2i(7, 6)).precipitation, "snow", "região 1, a do centro mais perto")
	app.queue_free()
