extends RefCounted
## Paleta do terreno e da vegetação pela estação e pelo clima do escopo (AD-170 `climate`): primavera mais verde, verão
## como o terreno é (seca: amarelado), outono alaranjado, inverno pálido e, abaixo de zero, neve. Só apresentação: a
## estação e a temperatura vêm do servidor; o cliente só escolhe a cor.

const SNOW := Color("eef2f5")
const DRY := Color("b8a05a")
const AUTUMN_LEAVES: Array[Color] = [Color("c8702a"), Color("b84a2a"), Color("d8a03a"), Color("8a5a2a")]


static func snowing(climate: Dictionary) -> bool:
	return not climate.is_empty() and String(climate.get("season_key", "")) == "winter" and float(climate.get("temperature", 10.0)) <= 0.0


static func season_key(climate: Dictionary) -> String:
	return String(climate.get("season_key", "")) if not climate.is_empty() else ""


## Cor do chão: `paved` = rua/quintal (só a neve muda); `water` = leito de rio (não muda).
static func ground(base: Color, climate: Dictionary, paved: bool = false, water: bool = false) -> Color:
	if climate.is_empty() or water:
		return base
	if snowing(climate):
		return base.lerp(SNOW, 0.45 if paved else 0.78)
	if paved:
		return base
	var drought := bool(climate.get("drought", false))
	match season_key(climate):
		"spring":
			return base.lerp(Color("74c050"), 0.18)
		"summer":
			return base.lerp(DRY, 0.45) if drought else base
		"autumn":
			return base.lerp(Color("a8803a"), 0.32 if not drought else 0.5)
		"winter":
			return base.lerp(Color("8a8a78"), 0.4)
	return base.lerp(DRY, 0.4) if drought else base


## Copa das árvores: folhas de outono variam por árvore (`roll` estável 0..1), inverno sem folha/neve, seca amarela.
static func crown(base: Color, climate: Dictionary, roll: float) -> Color:
	if climate.is_empty():
		return base
	if snowing(climate):
		return base.lerp(SNOW, 0.6)
	match season_key(climate):
		"autumn":
			return base.lerp(AUTUMN_LEAVES[int(roll * AUTUMN_LEAVES.size()) % AUTUMN_LEAVES.size()], 0.7)
		"winter":
			return base.lerp(Color("5a5a4a"), 0.45)
		"spring":
			return base.lerp(Color("8fd060"), 0.25)
	return base.lerp(DRY, 0.35) if bool(climate.get("drought", false)) else base


## Texto do HUD: "Verão · 24 °C · seca".
static func hud_text(climate: Dictionary) -> String:
	if climate.is_empty():
		return ""
	var names := {"winter": "Inverno", "spring": "Primavera", "summer": "Verão", "autumn": "Outono"}
	var text := "%s · %d °C" % [String(names.get(season_key(climate), season_key(climate).capitalize())), roundi(float(climate.temperature))]
	if bool(climate.get("drought", false)):
		text += " · seca"
	if snowing(climate):
		text += " · neve"
	return text
