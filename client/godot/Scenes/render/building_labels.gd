extends Node3D
## Rótulo de cada prédio da cidade (AD-177, "não dá para analisar nada"): nome que o servidor deu, senão o tipo de
## local, o tipo de prédio ou a função. Só aparecem de perto (zoom ≤ LABEL_ZOOM): de longe não poluem. Um Label3D por
## prédio, criado ao redesenhar a cidade; mostrar/esconder só quando o zoom cruza o limite.

const Describe := preload("res://Scenes/inspector_describe.gd")

const LABEL_ZOOM := 11.0
const DECLUTTER_EVERY := 0.25 ## s entre duas conferências de sobreposição (as câmeras andam; o rótulo não precisa piscar a cada quadro)
const SIGN_GROUP := "building_signs" ## as placas de texto da fachada (building_dress) entram no mesmo corte
const PAD_PX := 6.0 ## folga entre dois rótulos na tela
const SCREEN_TEXT_PX := 22.0 ## #269: altura máxima do texto do rótulo na tela: colado ao prédio (câmera de rua na soleira) ele encolhe em vez de virar um letreiro gigante e borrado
const GLYPH_PX := 64 ## tamanho da fonte desenhada (nítida mesmo encolhida): o tamanho no mundo vem do `pixel_size`
var labels: Dictionary = {} ## id do prédio -> Label3D
var shown := false
var _clock := 0.0
var hidden_by_overlap: Dictionary = {} ## id -> true: o rótulo que cederia lugar a outro mais perto (#267)


## #267: quais rótulos ficam escondidos para nenhum cobrir outro ("Loja de móveis" e "Mercado" viravam "Ljarcadoóvei"). `items`: [{id, rect: Rect2 (tela), priority: float}]; o de menor
## prioridade (mais perto da câmera) fica, o que o cobre some. Pura: a ordem de entrada não muda o resultado.
static func hidden_ids(items: Array) -> Dictionary:
	var order := items.duplicate()
	order.sort_custom(func(a, b): return float(a.priority) < float(b.priority) or (float(a.priority) == float(b.priority) and int(a.id) < int(b.id)))
	var kept: Array[Rect2] = []
	var hidden := {}
	for item in order:
		var rect: Rect2 = (item.rect as Rect2).grow(PAD_PX * 0.5)
		var clash := false
		for other in kept:
			if rect.intersects(other):
				clash = true
				break
		if clash:
			hidden[int(item.id)] = true
		else:
			kept.append(rect)
	return hidden


func _process(delta: float) -> void:
	_clock += delta
	if _clock < DECLUTTER_EVERY:
		return
	_clock = 0.0
	var camera := get_viewport().get_camera_3d() if is_inside_tree() else null
	if camera != null:
		declutter(camera)


## Esconde os rótulos que cobririam outros pela tela da câmera `camera`: os rótulos de prédio (só quando aparecem) e as placas de texto dos prédios (grupo `building_signs`, o texto da fachada).
func declutter(camera: Camera3D) -> void:
	var items: Array = []
	var nodes := {}
	var height := float(get_viewport().get_visible_rect().size.y) if is_inside_tree() else 900.0
	var fov := deg_to_rad(camera.fov) if camera.projection == Camera3D.PROJECTION_PERSPECTIVE else 1.0
	var candidates: Array = []
	if shown:
		for id in labels:
			candidates.append([int(id), labels[id]])
	if is_inside_tree():
		for sign_text in get_tree().get_nodes_in_group(SIGN_GROUP):
			if sign_text is Label3D:
				candidates.append([(sign_text as Label3D).get_instance_id(), sign_text])
	for entry in candidates:
		var label: Label3D = entry[1]
		if not is_instance_valid(label) or not label.is_inside_tree() or camera.is_position_behind(label.global_position):
			continue
		var world := label.global_position
		var screen := camera.unproject_position(world)
		var distance := maxf(0.5, camera.global_position.distance_to(world))
		var scale_px := height / (2.0 * distance * tan(fov * 0.5)) if camera.projection == Camera3D.PROJECTION_PERSPECTIVE else height / maxf(camera.size, 0.1)
		fit_to_screen(label, scale_px)
		var meters := float(label.text.length()) * 0.55 * float(label.font_size) * label.pixel_size
		var px := meters * scale_px
		var tall := float(label.font_size) * label.pixel_size * scale_px * 1.2
		items.append({"id": entry[0], "rect": Rect2(screen - Vector2(px * 0.5, tall * 0.5), Vector2(px, tall)), "priority": distance})
		nodes[entry[0]] = label
	hidden_by_overlap = hidden_ids(items)
	for id in nodes:
		var label: Label3D = nodes[id]
		label.visible = not hidden_by_overlap.has(id) and (shown or not labels.has(id))


## #269: o tamanho do texto no mundo nunca passa do de origem (`meta max_pixel`, o `pixel_size` de quando foi criado) e na tela nunca passa de SCREEN_TEXT_PX: de longe fica como sempre,
## colado (a câmera a 1,3 m da fachada) encolhe. A fonte é desenhada grande (GLYPH_PX), então não borra ao ser reduzida. Pura sobre o rótulo e a escala (pixels de tela por metro).
static func fit_to_screen(label: Label3D, scale_px: float) -> void:
	if not label.has_meta("max_pixel"):
		label.set_meta("max_pixel", label.pixel_size * float(label.font_size) / float(GLYPH_PX))
		label.font_size = GLYPH_PX
		label.outline_size = maxi(label.outline_size, 10)
	var cap := float(label.get_meta("max_pixel"))
	label.pixel_size = clampf(SCREEN_TEXT_PX / (float(GLYPH_PX) * maxf(scale_px, 0.001)), cap * 0.2, cap)


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
	hidden_by_overlap = {}
	for label in labels.values():
		label.visible = want
