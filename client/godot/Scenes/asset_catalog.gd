extends RefCounted
## Catálogo de assets do cliente (assets.json): ação -> som, tipo de prédio -> mobília, profissão e idade
## -> modelo. Qualquer chave ou arquivo ausente cai no procedural, sem erro. Só apresentação.

const PATH := "res://assets.json"

var data: Dictionary = {}


static func load_default() -> RefCounted:
	var catalog := new()
	var file := FileAccess.open(PATH, FileAccess.READ)
	if file != null:
		catalog.load_from(JSON.parse_string(file.get_as_text()))
	return catalog


func load_from(parsed: Variant) -> void:
	data = parsed if parsed is Dictionary else {}


## Período do dia para a luz atual; `rising` distingue amanhecer de entardecer.
static func period_for(light: float, rising: bool) -> String:
	if light < 0.3:
		return "night"
	if light < 0.7:
		return "dawn" if rising else "dusk"
	return "day"


func sound_id_for_action(action: int) -> String:
	return String(data.get("actions", {}).get(str(action), {}).get("sound", ""))


func sound_spec(sound_id: String) -> Dictionary:
	return data.get("sounds", {}).get(sound_id, {})


## Stream do arquivo do catálogo, ou null (o chamador sintetiza o procedural).
func sound_file(sound_id: String) -> AudioStream:
	var path := String(sound_spec(sound_id).get("file", ""))
	if path != "" and ResourceLoader.exists(path):
		return load(path) as AudioStream
	return null


func furniture_for(building_type: int) -> Array:
	var table: Dictionary = data.get("furniture", {})
	return table.get(str(building_type), table.get("default", []))


func has_furniture_entry(building_type: int) -> bool:
	return data.get("furniture", {}).has(str(building_type))


func piece(piece_id: String) -> Dictionary:
	return data.get("pieces", {}).get(piece_id, {"size": [0.4, 0.4, 0.4], "color": "8a6a43"})


func profession_model(profession: int) -> Dictionary:
	var table: Dictionary = data.get("models", {}).get("professions", {})
	return table.get(str(profession), table.get("default", {}))


func age_model(age_years: float) -> Dictionary:
	for band in data.get("models", {}).get("ages", []):
		if age_years < float(band.under):
			return band
	return {"scale": 1.0}
