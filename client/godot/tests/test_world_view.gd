extends "res://tests/test_base.gd"

const Store := preload("res://Store/world_store.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Fx := preload("res://tests/fixtures.gd")


func _world_snapshot() -> Dictionary:
	var terrain: Array = []
	for y in 4:
		for x in 4:
			terrain.append({"key": {"x": x, "y": y}, "value": {"id": 1 + (x + y) % 2}})
	var payload := {
		"width": 4, "height": 4,
		"cities": [
			{"id": {"value": "city-a"}, "name": "Alfa", "location": {"x": 1, "y": 1}, "population": 12,
				"bounds": {"x": 0, "y": 0, "width": 2, "height": 2}, "boundsAreDerived": true, "knownCarrierCount": 0},
			{"id": {"value": "city-b"}, "name": "", "location": {"x": 3, "y": 3}, "population": 5,
				"bounds": {"x": 3, "y": 3, "width": 1, "height": 1}, "boundsAreDerived": true, "knownCarrierCount": 0},
		],
		"externalNpcs": [
			{"id": {"value": 7}, "location": {"x": 2, "y": 2}},
			{"id": {"value": 8}, "location": {"x": 3, "y": 0}},
			{"id": {"value": 9}, "location": {"x": 0, "y": 3}},
		],
		"activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": true, "payload": terrain}},
		"livingState": {"npcs": [
			{"id": {"value": 7}, "location": {"x": 2, "y": 2}, "currentAction": 4},
			{"id": {"value": 8}, "location": {"x": 3, "y": 0}, "currentAction": 1},
			{"id": {"value": 9}, "location": {"x": 0, "y": 3}, "currentAction": null},
		]},
	}
	return Fx.snapshot(0, payload, "World")


func _view() -> Array:
	var store := Store.new()
	var labels := ActionLabels.new()
	labels.load_from([{"actionType": 4, "label": "Viajando"}, {"actionType": 1, "label": "Dormindo"}])
	var view := WorldView.new()
	view.bind(store, labels)
	return [store, view, labels]


func test_snapshot_builds_cities_and_npcs() -> void:
	var setup := _view()
	var store = setup[0]
	var view = setup[1]

	store.apply_snapshot(_world_snapshot())

	eq(view.city_nodes.size(), 2, "um nó por cidade")
	eq(view.npc_nodes.size(), 3, "um nó por NPC externo")
	eq(view.npc_nodes[7].position, Vector3(2.5, 0, 2.5), "NPC 7 na célula (2,2)")
	eq(view.npc_nodes[8].position, Vector3(3.5, 0, 0.5))
	eq(view.city_nodes["city-a"].get_meta("city_id"), "city-a")
	check(view.terrain_root != null, "terreno desenhado")
	eq(view.city_at(Vector3(1.2, 0, 0.4)), "city-a", "cidade sob um ponto do mundo")
	eq(view.city_at(Vector3(2.5, 0, 0.5)), "", "fora de qualquer cidade")
	view.free()


func test_delta_moves_only_that_npc_interpolated() -> void:
	var setup := _view()
	var store = setup[0]
	var view = setup[1]
	store.apply_snapshot(_world_snapshot())
	var before_other: Vector3 = view.npc_nodes[8].position

	store.apply_delta(Fx.delta(0, 1, {"npcUpserts": [
		{"id": {"value": 7}, "location": {"x": 3, "y": 2}, "currentAction": 4}]}))
	view.npc_nodes[7].advance(0.5 * view.STEP_SECONDS)

	var halfway: Vector3 = view.npc_nodes[7].position
	check(halfway.x > 2.5 and halfway.x < 3.5, "a meio do passo fica entre as duas células (x=%s)" % halfway.x)
	eq(view.npc_nodes[8].position, before_other, "o outro NPC não se moveu")
	view.npc_nodes[7].advance(view.STEP_SECONDS)
	eq(view.npc_nodes[7].position, Vector3(3.5, 0, 2.5), "chega à célula final")
	view.free()
