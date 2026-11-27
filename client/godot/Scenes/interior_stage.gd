extends Control
## #227/#249: foco no interior. A casa é desenhada no próprio mundo, no lugar do prédio e na escala do mundo (mesma câmera, mesmo
## sol, mesmas unidades): não há SubViewport nem outra escala. O mundo NÃO some: a cidade segue desenhada, atualizada e com os deltas
## chegando (a rua continua visível e viva em volta da casa). Só o prédio em foco sai do desenho (a casa o substitui: telhado e
## andares acima do corpo ficam de fora, paredes do lado da câmera baixas ou translúcidas) e quem está dentro é desenhado pela casa.

const InteriorView := preload("res://Scenes/interior_view.gd")

var interior_view
var active := false
var floor_label: Label
var world: Node3D ## o mundo desenhado (WorldView): segue visível e vivo; só o prédio em foco sai do desenho
var covered_building := -1 ## #249: prédio do mundo que a casa em foco substitui (some do desenho enquanto ela está)

var _source: Camera3D


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	set_anchors_preset(Control.PRESET_FULL_RECT)
	interior_view = InteriorView.new()
	add_child(interior_view)
	floor_label = Label.new()
	floor_label.visible = false
	floor_label.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
	floor_label.position = Vector2(-120, -70)
	floor_label.add_theme_font_size_override("font_size", 22)
	add_child(floor_label)


func enter(info: Dictionary, origin: Vector2i, labels, source_camera: Camera3D, building_type: int = -1, lodging_units: int = 0, building_id: int = -1) -> void:
	_source = source_camera
	interior_view.show_interior(info, origin, labels, building_type, lodging_units)
	covered_building = building_id
	_cover_building()
	active = true
	_update_floor_label()
	_sync_camera()


## O prédio do mundo some do desenho (a casa tomou o lugar dele); volta igual ao sair. O desenho do prédio pode ser refeito por delta
## com a casa em foco: a cada quadro garante que o novo nó também fica de fora.
func _cover_building() -> void:
	if covered_building < 0 or world == null or not is_instance_valid(world) or not ("buildings" in world):
		return
	var node = world.buildings.nodes.get(covered_building)
	if node != null and node.visible:
		node.visible = false


func _uncover_building() -> void:
	if covered_building >= 0 and world != null and is_instance_valid(world) and "buildings" in world:
		var node = world.buildings.nodes.get(covered_building)
		if node != null:
			node.visible = true
	covered_building = -1
	if world != null and is_instance_valid(world) and "npcs" in world:
		world.npcs.set_interior_hidden({})


## Ocupantes da casa (e seus nomes): o interior os desenha, o mundo deixa de desenhar o boneco da mesma pessoa (exceto o do corpo do
## jogador, que é a mesma figura nos dois e fica sempre à vista).
func sync_occupants(occupants: Array, names: Dictionary) -> void:
	interior_view.set_occupants(occupants)
	interior_view.apply_names(names)
	if world == null or not is_instance_valid(world) or not ("npcs" in world):
		return
	var hidden := {}
	for occupant in occupants:
		var id: int = int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc)
		if id != interior_view.guided_id:
			hidden[id] = true
	world.npcs.set_interior_hidden(hidden)


## A câmera é a do mundo (a seleção projeta ocupantes e objetos com ela).
func camera() -> Camera3D:
	return _source


func leave() -> void:
	interior_view.immersive = false
	floor_label.visible = false
	active = false
	interior_view.close() # #237: a casa sai do desenho de vez (antes ficava no lugar, escura, por cima do mundo)
	_uncover_building()


func set_floor(level: int) -> void:
	interior_view.set_floor(level)
	_update_floor_label()


func _update_floor_label() -> void:
	floor_label.visible = active
	floor_label.text = "Andar %d de %d  (PgUp/PgDn)" % [interior_view.current_floor + 1, interior_view.floor_count]


func _process(_delta: float) -> void:
	if active:
		_cover_building()
		_sync_camera()


func _sync_camera() -> void:
	if not is_inside_tree() or _source == null or not is_instance_valid(_source) or not _source.is_inside_tree():
		return
	interior_view.set_view_direction(_source.global_transform.basis.z)
	interior_view.update_ceiling(_source.global_position.y)
	interior_view.update_near(_source.global_position)
