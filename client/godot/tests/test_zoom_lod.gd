extends "res://tests/test_base.gd"

const CameraRig := preload("res://Scenes/camera_rig.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const Store := preload("res://Store/world_store.gd")
const ActionLabels := preload("res://Store/action_labels.gd")


func test_zoom_range_follows_map_size() -> void:
	var rig := CameraRig.new()
	eq(rig.max_size, 48.0, "mapa pequeno mantém o alcance base")
	rig.fit_to_map(100)
	eq(rig.max_size, 140.0, "mapa grande amplia o zoom out")
	check(rig.bubble_size < rig.max_size and rig.bubble_size > rig.city_exit_size, "bolha entre a cidade e o máximo")
	rig.zoom_by(100.0)
	eq(rig.zoom_size, rig.max_size, "zoom out para no máximo")
	rig.free()


func test_wheel_zoom_eases_toward_goal_without_jumping() -> void:
	var rig := CameraRig.new()
	var start: float = rig.zoom_size
	rig.zoom_smooth(0.5)
	eq(rig.zoom_size, start, "a roda não salta de uma vez")
	rig._process(0.05)
	check(rig.zoom_size < start and rig.zoom_size > start * 0.5, "aproxima do alvo aos poucos")
	for i in 60:
		rig._process(0.05)
	check(absf(rig.zoom_size - start * 0.5) < 0.05, "chega ao alvo")
	rig.free()


func test_far_zoom_in_world_turns_npcs_into_bubbles() -> void:
	var labels := ActionLabels.new()
	var actor := NpcActor.new()
	actor.setup(3, Vector2i(1, 1), 4, labels)
	actor.set_bubble(true)
	eq(actor._body.visible, false, "sem corpo")
	check(actor._bubble_node != null and actor._bubble_node.visible, "bolha visível")
	var pose_before: Vector3 = actor._body.rotation
	actor.animate(1.0)
	eq(actor._body.rotation, pose_before, "bolha não anima")
	actor.set_bubble(false)
	eq(actor._body.visible, true, "volta o corpo")
	eq(actor._bubble_node.visible, false, "some a bolha")
	actor.free()


func test_city_ground_is_the_map_terrain_outside_engine_streets_and_lots() -> void:
	# AD-177: nada de "cidade inteira = rua" nem quintal em volta de todo prédio; o chão da cidade é o terreno do mapa
	var store := Store.new()
	store.kind = "City"
	store.city = {"id": "c1", "bounds": Rect2i(0, 0, 4, 2)}
	for x in 4:
		for y in 2:
			store.terrain[Vector2i(x, y)] = 1
	store.buildings = {1: {"id": 1, "cell": Vector2i.ZERO, "footprint": [{"cell": Vector2i(1, 1), "material": 0}], "lot": {}}}
	var view := WorldView.new()
	view.bind(store, ActionLabels.new())
	view.terrain.rebuild(store)
	var index := 0
	for cell in store.terrain:
		var shade := 0.94 + float((cell.x * 7 + cell.y * 13) % 5) * 0.03
		check(view.terrain._base[index][0].is_equal_approx(WorldView.TERRAIN_COLORS[0] * shade),
			"dentro da cidade o chão tem a cor do terreno do mapa em %s" % cell)
		index += 1
	view.ground.rebuild()
	eq(view.ground.road_pieces, 0, "sem camada Roads: nenhuma rua")
	eq(view.ground.lot_pieces, 0, "prédio sem lote: nenhum quintal")
	eq(view.ground.entity_at(Vector2i(0, 0)), {}, "em volta do prédio: terreno comum")
	eq(view.ground.entity_at(Vector2i(3, 0)), {}, "fora dos lotes: terreno comum, não rua")
	store.roads = {Vector2i(3, 0): {"cell": Vector2i(3, 0), "city": "c1", "surface": 1, "users": 4, "main": false, "square": false, "extra": {}}}
	view.ground.rebuild()
	check(view.ground.road_pieces > 0, "com a camada: a rua aparece")
	eq(view.ground.entity_at(Vector2i(3, 0)).get("kind", ""), "street", "e só onde o motor disse")
	eq(view.ground.entity_at(Vector2i(2, 0)), {}, "a célula do lado continua terreno")
	store.kind = "World"
	view.ground.rebuild()
	eq(view.ground.drawn_roads.size(), 1, "no mapa-múndi as ruas das cidades também aparecem")
	eq(view.ground.lot_pieces, 0, "e lote só no escopo cidade")
	view.free()