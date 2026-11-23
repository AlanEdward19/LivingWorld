extends Node3D
## Rótulo de cada prédio da cidade (AD-177, "não dá para analisar nada"): nome que o servidor deu, senão o tipo de
## local, o tipo de prédio ou a função. Só aparecem de perto (zoom ≤ LABEL_ZOOM): de longe não poluem. Um Label3D por
## prédio, criado ao redesenhar a cidade; mostrar/esconder só quando o zoom cruza o limite.

const Describe := preload("res://Scenes/inspector_describe.gd")

const LABEL_ZOOM := 11.0
var labels: Dictionary = {} ## id do prédio -> Label3D
var shown := false


func rebuild(store, registry, building_nodes: Dictionary) -> void:
	for label in labels.values():
		if label.is_inside_tree():
			label.queue_free()
		else:
			label.free()
	labels.clear()
	if store == null or store.kind != "City":
		return
	for id in store.buildings:
		var node: Node3D = building_nodes.get(id)
		if node == null:
			continue
		var box: AABB = node.get_meta("box", AABB())
		var label := Label3D.new()
		label.name = "Label_%d" % int(id)
		label.text = text_for(store.buildings[id], store.building_states.get(id, {}), registry)
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		label.no_depth_test = true
		label.pixel_size = 0.0045
		label.font_size = 40
		label.outline_size = 12
		label.outline_modulate = Color(0, 0, 0, 0.85)
		label.position = Vector3(box.position.x + box.size.x / 2.0, box.end.y + 0.35, box.position.z + box.size.z / 2.0)
		label.visible = shown
		add_child(label)
		labels[id] = label


## Nome que o servidor deu (ficha já lida) > tipo de local > tipo de prédio > função.
static func text_for(building: Dictionary, state: Dictionary, registry) -> String:
	var id := int(building.id)
	var work: Dictionary = building.get("work", {})
	if not work.is_empty(): # AD-193: canteiro — o prédio ainda não existe
		var kind := String(work.get("kind", ""))
		return "Obra: %s" % ("casa" if kind == "casa" else "oficina" if kind == "oficina" else
			(registry.work_kind_text(kind) if registry != null else "prédio"))
	if registry != null:
		var known := String(registry.entity_names.get("building:%d" % id, ""))
		if known != "":
			return known
	if String(state.get("location_type_name", "")) != "":
		return String(state.location_type_name)
	if registry != null and int(building.get("type", -1)) >= 0:
		var type_name: String = registry.building_type_name(int(building.type))
		if type_name != "" and not type_name.begins_with("Prédio tipo"):
			return type_name
	var functions: Array = Array(String(state.get("function", "")).split(",", false))
	if not functions.is_empty():
		var first := String(functions[0])
		return String(Describe.FUNCTION_NAMES.get(first, first)).capitalize()
	return "Casa" if int(building.get("type", -1)) == -1 else "Prédio %d" % id


## Mostra os rótulos só de perto (e só na cidade).
func set_zoom(zoom_size: float, city_lod: bool) -> void:
	var want := city_lod and zoom_size <= LABEL_ZOOM
	if want == shown:
		return
	shown = want
	for label in labels.values():
		label.visible = want
