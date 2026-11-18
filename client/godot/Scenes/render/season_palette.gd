extends RefCounted
## Paleta do terreno e da vegetação pela estação e pelo tempo do escopo (AD-170 `climate`, AD-180 `climate.weather`): primavera
## mais verde, verão como o terreno é (seca: amarelado), outono alaranjado, inverno pálido; neve no chão só quando o motor diz
## (`weather.ground == "snow"` / `snowCm`), chão molhado, lama e congelado pelo `weather.ground`. Só apresentação: estação,
## temperatura e tempo vêm do servidor; o cliente só escolhe a cor. Sem `weather` (tempo desligado), nada de neve nem lama.

const Look := preload("res://Scenes/render/weather_look.gd")

const SNOW := Look.SNOW
const DRY := Color("b8a05a")
const AUTUMN_LEAVES: Array[Color] = [Color("c8702a"), Color("b84a2a"), Color("d8a03a"), Color("8a5a2a")]


static func weather_of(climate: Dictionary) -> Dictionary:
	return climate.get("weather", {}) if climate.get("weather") is Dictionary else {}


## Neve caindo agora (o motor diz `precipitation: snow`).
static func snowing(climate: Dictionary) -> bool:
	return String(weather_of(climate).get("precipitation", "")) == "snow"


## Quanto o chão está coberto de neve (0..1), pelo `snowCm` e pelo `ground` do motor.
static func snow_cover(climate: Dictionary) -> float:
	return Look.snow_cover(weather_of(climate))


static func season_key(climate: Dictionary) -> String:
	return String(climate.get("season_key", "")) if not climate.is_empty() else ""


static func drought(climate: Dictionary) -> bool:
	return bool(climate.get("drought", false)) or bool(weather_of(climate).get("drought", false))


## Cor do chão: `paved` = rua/caminho (a estação não muda, o tempo sim); `water` = leito de rio (não muda).
static func ground(base: Color, climate: Dictionary, paved: bool = false, water: bool = false) -> Color:
	if climate.is_empty() or water:
		return base
	var color := base if paved else _season(base, climate)
	var weather := weather_of(climate)
	return Look.ground_tint(color, String(weather.get("ground", "dry")), float(weather.get("snow_cm", 0.0)),
		bool(weather.get("frost", false)), not paved)


static func _season(base: Color, climate: Dictionary) -> Color:
	var dry := drought(climate)
	match season_key(climate):
		"spring":
			return base.lerp(Color("74c050"), 0.18)
		"summer":
			return base.lerp(DRY, 0.45) if dry else base
		"autumn":
			return base.lerp(Color("a8803a"), 0.32 if not dry else 0.5)
		"winter":
			return base.lerp(Color("8a8a78"), 0.4)
	return base.lerp(DRY, 0.4) if dry else base


## Copa das árvores: folhas de outono variam por árvore (`roll` estável 0..1), inverno sem folha, neve só com neve no chão,
## seca amarela. Queda de folhas não é desenhada: o motor não diz quando a folha cai.
static func crown(base: Color, climate: Dictionary, roll: float) -> Color:
	if climate.is_empty():
		return base
	var cover := snow_cover(climate)
	if cover > 0.0:
		return base.lerp(SNOW, 0.6 * cover)
	match season_key(climate):
		"autumn":
			return base.lerp(AUTUMN_LEAVES[int(roll * AUTUMN_LEAVES.size()) % AUTUMN_LEAVES.size()], 0.7)
		"winter":
			return base.lerp(Color("5a5a4a"), 0.45)
		"spring":
			return base.lerp(Color("8fd060"), 0.25)
	return base.lerp(DRY, 0.35) if drought(climate) else base


## Texto do relógio do topo: "Verão · 24 °C · seca" (temperatura da hora quando o motor manda o tempo do dia).
static func hud_text(climate: Dictionary) -> String:
	if climate.is_empty():
		return ""
	var names := {"winter": "Inverno", "spring": "Primavera", "summer": "Verão", "autumn": "Outono"}
	var weather := weather_of(climate)
	var temperature := float(weather.get("now_c", climate.temperature)) if not weather.is_empty() else float(climate.temperature)
	var text := "%s · %d °C" % [String(names.get(season_key(climate), season_key(climate).capitalize())), roundi(temperature)]
	if drought(climate):
		text += " · seca"
	return text
