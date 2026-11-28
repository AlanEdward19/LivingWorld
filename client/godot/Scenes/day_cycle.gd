extends Node
## Ciclo dia/noite dirigido pelo relógio do servidor: o cliente só interpola entre as leituras de
## `/simulation/status` (tick = hora do mundo; o tamanho do dia vem de `GET /world/calendar`). Pausado, o céu para onde o servidor parou.
## AD-180: nascer e pôr do sol vêm do tempo do dia do motor (`weather.sunriseHour/sunsetHour`, pela latitude e o dia do ano);
## sem tempo, 5 h e 19 h de sempre. Chuva e tempestade encobrem a luz; onda de calor/frio muda a cor dela.

const CLOCK_HOURS := 24.0 ## a luz e o relógio de parede sempre usam 24 h; o calendário do mundo só fixa quantos ticks formam um dia

var sun: DirectionalLight3D
var environment: Environment
var window_material: StandardMaterial3D
var lamp_material: StandardMaterial3D

const Look := preload("res://Scenes/render/weather_look.gd")
const DEFAULT_SUNRISE := 5.0
const DEFAULT_SUNSET := 19.0

var hour := 9.0 ## hora do dia, 0–24
var sunrise := DEFAULT_SUNRISE ## hora do nascer do sol (do motor)
var sunset := DEFAULT_SUNSET
var sky := {"dim": 0.0, "tint": Color(1, 1, 1)} ## céu encoberto e cor da luz pelo tempo
var hours_per_day := 24.0 ## vem de GET /world/calendar
var days_per_month := 30
var months_per_year := 12
var _tick := 0.0
var _ticks_per_second := 1.0
var _paused := true
var _synced_at := 0.0
var _error := 0.0 ## #241: com o tempo acelerado, o erro entre o céu e o motor se dilui em vez de saltar (sol sem trancos)
var _synced_once := false
const SMOOTH_RATE := 2.0 ## horas de mundo por segundo a partir das quais a leitura do motor corrige aos poucos
const SMOOTH_MAX_ERROR := 3.0 ## erro maior que isto (horas) é salto de verdade (carga, outro mundo): corrige de uma vez


func bind(p_sun: DirectionalLight3D, p_environment: Environment, p_window_material: StandardMaterial3D, p_lamp_material: StandardMaterial3D = null) -> void:
	sun = p_sun
	environment = p_environment
	window_material = p_window_material
	lamp_material = p_lamp_material
	_apply()


## Calendário do cenário (`GET /world/calendar`): quantos ticks formam dia, mês e ano.
func set_calendar(calendar: Dictionary) -> void:
	hours_per_day = float(calendar.get("hoursPerDay", hours_per_day))
	days_per_month = int(calendar.get("daysPerMonth", days_per_month))
	months_per_year = int(calendar.get("monthsPerYear", months_per_year))


## Tempo do dia do motor (AD-180): sol e céu. {} = volta ao padrão.
func set_weather(weather: Dictionary) -> void:
	var rise := float(weather.get("sunrise", -1.0))
	var set_hour := float(weather.get("sunset", -1.0))
	var valid := rise >= 0.0 and set_hour > rise
	sunrise = rise if valid else DEFAULT_SUNRISE
	sunset = set_hour if valid else DEFAULT_SUNSET
	sky = Look.sky(weather)
	_apply()


func sync(tick: float, ticks_per_second: float, paused: bool, calendar: Dictionary = {}) -> void:
	if not calendar.is_empty():
		set_calendar(calendar)
	var smooth := _synced_once and not paused and not _paused and ticks_per_second >= SMOOTH_RATE and _ticks_per_second >= SMOOTH_RATE \
		and absf(tick - _tick) <= SMOOTH_MAX_ERROR
	_synced_once = true
	_ticks_per_second = ticks_per_second
	_paused = paused
	_synced_at = Time.get_ticks_msec() / 1000.0
	if smooth:
		_error = tick - _tick
		return
	_error = 0.0
	_tick = tick
	hour = _hour_of(tick)
	_apply()


func advance(seconds: float) -> void:
	if _paused:
		return
	_tick += seconds * _ticks_per_second
	if _error != 0.0:
		var fix := _error * minf(1.0, seconds * 4.0) # a diferença com o motor some em ~meio segundo
		_tick += fix
		_error -= fix
	hour = _hour_of(_tick)
	_apply()


func _process(delta: float) -> void:
	advance(delta)


## 0 = noite plena, 1 = meio-dia.
func _hour_of(tick: float) -> float:
	return fmod(tick, hours_per_day) / hours_per_day * CLOCK_HOURS


## Ano, mês e dia (a partir de 1) do tick, pelo calendário que o servidor informou.
func date_parts(tick: float) -> Dictionary:
	var day_index := int(tick / hours_per_day)
	var days_per_year := days_per_month * months_per_year
	return {
		"year": day_index / days_per_year,
		"month": (day_index % days_per_year) / days_per_month + 1,
		"day": day_index % days_per_month + 1,
	}


## 0 = noite plena, 1 = meio-dia; o arco vai do nascer ao pôr do sol do motor.
func daylight() -> float:
	return clampf(sin(_arc()) * 1.5 + 0.1, 0.0, 1.0) if not _night() else 0.0


func _arc() -> float:
	return (hour - sunrise) / (sunset - sunrise) * PI


func _night() -> bool:
	return hour < sunrise or hour >= sunset


func phase_name() -> String:
	if hour >= sunrise and hour < sunrise + 3.0:
		return "Amanhecer"
	if hour >= sunrise + 3.0 and hour < sunset - 2.0:
		return "Dia"
	if hour >= sunset - 2.0 and hour < sunset:
		return "Entardecer"
	return "Noite"


func clock_text() -> String:
	return "%02d:%02d" % [int(hour), int(fmod(hour, 1.0) * 60.0)]


func _apply() -> void:
	if sun == null:
		return
	var light := daylight()
	var elevation := sin(_arc()) # 0 no nascer, PI no pôr do sol (horas do motor)
	var night := _night()
	if night:
		var moon_arc := (fmod(hour + 6.0, 24.0)) / 12.0 * PI
		sun.rotation = Vector3(-deg_to_rad(20.0 + maxf(0.0, sin(moon_arc)) * 40.0), deg_to_rad(210.0), 0)
		sun.light_color = Color(0.6, 0.7, 1.0)
		sun.light_energy = 0.5
	else:
		sun.rotation = Vector3(-deg_to_rad(8.0 + elevation * 52.0), deg_to_rad(-35.0 + (hour - 12.0) * 9.0), 0)
		var warm := clampf(1.0 - elevation * 2.2, 0.0, 1.0)
		sun.light_color = Color(1.0, 0.97, 0.9).lerp(Color(1.0, 0.62, 0.35), warm)
		sun.light_energy = (0.35 + 0.65 * clampf(elevation, 0.0, 1.0)) * (1.0 - float(sky.dim))
		sun.light_color *= Color(sky.tint)
	environment.background_color = Color("101c36").lerp(Color("a9c7d8"), light)
	if light > 0.0 and light < 0.6:
		environment.background_color = environment.background_color.lerp(Color("e9a56d"), (1.0 - light / 0.6) * 0.45)
	environment.background_color = environment.background_color.lerp(Color("5d6670"), float(sky.dim) * light) # céu encoberto
	environment.ambient_light_color = Color(0.35, 0.42, 0.7).lerp(Color(0.78, 0.82, 0.9), light)
	environment.ambient_light_energy = 0.55 + 0.1 * light
	if window_material != null:
		window_material.emission_energy_multiplier = (1.0 - light) * 1.3
	if lamp_material != null:
		lamp_material.emission_energy_multiplier = (1.0 - light) * 3.0
