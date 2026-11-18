extends RefCounted
## PONTO ÚNICO da aparência do tempo do dia (AD-180, `climate.weather` e `roads[].ground/snowCm/wetPm`): o que cai do céu e
## quanto, a cor do chão molhado/lama/neve/congelado, as poças, a luz encoberta e o texto do indicador. Tudo é tradução de
## campo do motor: sem `weather`, nada cai e o chão fica como é. Funções puras (testáveis).

const SNOW := Color("eef2f5")
const MUD := Color("4e3b2a")
const FROST := Color("c9d8e2")
const PUDDLE := Color("3e4a55")
const RAIN_CAP := 1400 ## teto de gotas (chuva forte, câmera de cidade); LOD e intensidade só reduzem
const SNOW_CAP := 700
const INTENSITY := {"light": 0.25, "moderate": 0.55, "heavy": 1.0}
const NAMES := {"rain": "chuva", "snow": "neve"}
const INTENSITY_NAMES := {"light": "fraca", "moderate": "moderada", "heavy": "forte"}
const GROUND_NAMES := {"dry": "seco", "wet": "molhado", "mud": "lama", "heavy-mud": "lamaçal", "snow": "neve", "frozen": "congelado"}


## O que cai agora: {kind: "rain"|"snow"|"", ratio 0..1 pela intensidade, hail}.
static func falling(weather: Dictionary) -> Dictionary:
	var kind := String(weather.get("precipitation", "none"))
	if not kind in ["rain", "snow"]:
		return {"kind": "", "ratio": 0.0, "hail": false}
	return {"kind": kind, "ratio": float(INTENSITY.get(String(weather.get("intensity", "")), 0.25)),
		"hail": bool(weather.get("hail", false))}


## Fração das partículas pelo nível de detalhe: mapa-múndi afastado não chove na tela; câmera longe, metade.
static func lod_ratio(lod: String, far: bool, bubble: bool) -> float:
	if bubble or (lod == "world" and far):
		return 0.0
	return 0.5 if far else 1.0


## Cobertura de neve no chão (0..1): `ground: snow` ou `snowCm` > 0; 10 cm cobre tudo.
static func snow_cover(weather: Dictionary) -> float:
	var cm := float(weather.get("snow_cm", 0.0))
	if cm <= 0.0 and String(weather.get("ground", "")) != "snow":
		return 0.0
	return clampf(0.35 + cm / 10.0 * 0.65, 0.35, 1.0)


## Cor do chão pelo estado do motor. `ground`: dry/wet/mud/heavy-mud/snow/frozen; `soft` = terreno/quintal (geada aparece).
static func ground_tint(base: Color, ground: String, snow_cm: float, frost := false, soft := false) -> Color:
	var cover := snow_cover({"ground": ground, "snow_cm": snow_cm})
	if cover > 0.0:
		return base.lerp(SNOW, cover * (0.85 if soft else 0.7))
	var color := base
	match ground:
		"wet":
			color = base.darkened(0.2)
		"mud":
			color = base.lerp(MUD, 0.45).darkened(0.1)
		"heavy-mud":
			color = base.lerp(MUD, 0.7).darkened(0.18)
		"frozen":
			color = base.lerp(FROST, 0.35)
	if frost and soft and ground != "frozen":
		color = color.lerp(FROST, 0.2)
	return color


## Chão de uma célula de rua: o que a própria célula diz (`ground`, `snow_cm`); "" = o motor não mandou (servidor sem tempo).
static func road_ground(road: Dictionary) -> String:
	return String(road.get("ground", ""))


## Poças na rua: lama 1, lamaçal 2; cascalho e pedra o motor nunca deixa em lama (só molhada), então não têm.
static func puddles(ground: String) -> int:
	return {"mud": 1, "heavy-mud": 2}.get(ground, 0)


## Céu encoberto: quanto a luz cai (0..0,5) e a cor de fundo do tempo; ondas de calor/frio esquentam/esfriam a luz.
static func sky(weather: Dictionary) -> Dictionary:
	var fall := falling(weather)
	var dim := 0.0
	if fall.kind != "":
		dim = 0.08 + 0.22 * float(fall.ratio)
	if bool(weather.get("storm", false)):
		dim = 0.45
	var tint := Color(1, 1, 1)
	if bool(weather.get("heat_wave", false)):
		tint = Color(1.0, 0.93, 0.8)
	elif bool(weather.get("cold_wave", false)):
		tint = Color(0.85, 0.92, 1.0)
	return {"dim": dim, "tint": tint}


## Indicador do topo: "chuva forte · sente 1 °C · vento 9 m/s · lama · onda de frio" ("" sem tempo).
static func hud(weather: Dictionary) -> String:
	if weather.is_empty():
		return ""
	var parts: Array[String] = []
	parts.append(state_text(weather))
	parts.append("sente %d °C" % roundi(float(weather.get("feels_c", 0.0))))
	parts.append("vento %d m/s" % roundi(float(weather.get("wind", 0.0))))
	var ground := String(weather.get("ground", "dry"))
	if ground != "dry":
		parts.append(("neve %d cm" % roundi(float(weather.get("snow_cm", 0.0)))) if ground == "snow" else String(GROUND_NAMES.get(ground, ground)))
	for flag in [["drought", "seca"], ["heat_wave", "onda de calor"], ["cold_wave", "onda de frio"], ["frost", "geada"]]:
		if bool(weather.get(flag[0], false)):
			parts.append(flag[1])
	return " · ".join(parts)


## Estado do céu: "tempestade com granizo", "chuva forte", "neve fraca", "sem chuva".
static func state_text(weather: Dictionary) -> String:
	var fall := falling(weather)
	var text := "sem chuva" if fall.kind == "" else "%s %s" % [NAMES[fall.kind], INTENSITY_NAMES.get(String(weather.get("intensity", "")), "")]
	if bool(weather.get("storm", false)):
		text = "tempestade" + (" com granizo" if bool(weather.get("hail", false)) else "")
	elif bool(weather.get("hail", false)):
		text += " com granizo"
	return text.strip_edges()


## Dica do indicador: os números do dia que o motor mandou.
static func tooltip(weather: Dictionary) -> String:
	if weather.is_empty():
		return "O motor não manda o tempo do dia (tempo desligado)."
	return "Agora %.1f °C (sensação %.1f °C) · mínima %.1f, máxima %.1f\nChuva do dia %.1f mm · vento %.1f m/s\nSol: %s às %s (%.1f h de luz)\nChão: %s · umidade %d‰ · neve %.0f cm\nDias perdidos no campo no ano: %d" % [
		float(weather.now_c), float(weather.feels_c), float(weather.min_c), float(weather.max_c), float(weather.mm), float(weather.wind),
		hour_text(float(weather.sunrise)), hour_text(float(weather.sunset)), float(weather.daylight),
		String(GROUND_NAMES.get(String(weather.ground), String(weather.ground))), int(weather.wet_pm), float(weather.snow_cm),
		int(weather.lost_days)]


static func hour_text(hour: float) -> String:
	return "--:--" if hour < 0.0 else "%02d:%02d" % [int(hour), int(fmod(hour, 1.0) * 60.0)]
