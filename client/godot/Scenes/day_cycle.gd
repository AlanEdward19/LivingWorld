extends Node
## Ciclo dia/noite dirigido pelo relógio do servidor: o cliente só interpola entre as leituras de
## `/simulation/status` (tick = hora do mundo; o tamanho do dia vem de `GET /world/calendar`). Pausado, o céu para onde o servidor parou.

const CLOCK_HOURS := 24.0 ## a luz e o relógio de parede sempre usam 24 h; o calendário do mundo só fixa quantos ticks formam um dia

var sun: DirectionalLight3D
var environment: Environment
var window_material: StandardMaterial3D
var lamp_material: StandardMaterial3D

var hour := 9.0 ## hora do dia, 0–24
var hours_per_day := 24.0 ## vem de GET /world/calendar
var days_per_month := 30
var months_per_year := 12
var _tick := 0.0
var _ticks_per_second := 1.0
var _paused := true
var _synced_at := 0.0


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


func sync(tick: float, ticks_per_second: float, paused: bool, calendar: Dictionary = {}) -> void:
	if not calendar.is_empty():
		set_calendar(calendar)
	_tick = tick
	_ticks_per_second = ticks_per_second
	_paused = paused
	_synced_at = Time.get_ticks_msec() / 1000.0
	hour = _hour_of(tick)
	_apply()


func advance(seconds: float) -> void:
	if _paused:
		return
	_tick += seconds * _ticks_per_second
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


func daylight() -> float:
	return clampf(sin((hour - 5.0) / 14.0 * PI) * 1.5 + 0.1, 0.0, 1.0)


func phase_name() -> String:
	if hour >= 5.0 and hour < 8.0:
		return "Amanhecer"
	if hour >= 8.0 and hour < 17.0:
		return "Dia"
	if hour >= 17.0 and hour < 19.0:
		return "Entardecer"
	return "Noite"


func clock_text() -> String:
	return "%02d:%02d" % [int(hour), int(fmod(hour, 1.0) * 60.0)]


func _apply() -> void:
	if sun == null:
		return
	var light := daylight()
	var arc := (hour - 5.0) / 14.0 * PI # 0 no nascer (5h), PI no pôr do sol (19h)
	var elevation := sin(arc)
	var night := hour < 5.0 or hour >= 19.0
	if night:
		var moon_arc := (fmod(hour + 6.0, 24.0)) / 12.0 * PI
		sun.rotation = Vector3(-deg_to_rad(20.0 + maxf(0.0, sin(moon_arc)) * 40.0), deg_to_rad(210.0), 0)
		sun.light_color = Color(0.6, 0.7, 1.0)
		sun.light_energy = 0.5
	else:
		sun.rotation = Vector3(-deg_to_rad(8.0 + elevation * 52.0), deg_to_rad(-35.0 + (hour - 12.0) * 9.0), 0)
		var warm := clampf(1.0 - elevation * 2.2, 0.0, 1.0)
		sun.light_color = Color(1.0, 0.97, 0.9).lerp(Color(1.0, 0.62, 0.35), warm)
		sun.light_energy = 0.35 + 0.65 * clampf(elevation, 0.0, 1.0)
	environment.background_color = Color("101c36").lerp(Color("a9c7d8"), light)
	if light > 0.0 and light < 0.6:
		environment.background_color = environment.background_color.lerp(Color("e9a56d"), (1.0 - light / 0.6) * 0.45)
	environment.ambient_light_color = Color(0.35, 0.42, 0.7).lerp(Color(0.78, 0.82, 0.9), light)
	environment.ambient_light_energy = 0.55 + 0.1 * light
	if window_material != null:
		window_material.emission_energy_multiplier = (1.0 - light) * 1.3
	if lamp_material != null:
		lamp_material.emission_energy_multiplier = (1.0 - light) * 3.0
