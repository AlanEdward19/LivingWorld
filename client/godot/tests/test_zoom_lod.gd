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


func test_settlement_ground_is_paved_only_inside_a_city() -> void:
	var store := Store.new()
	store.kind = "City"
	store.city = {"bounds": Rect2i(0, 0, 3, 2)}
	store.buildings = {1: {"footprint": [{"cell": Vector2i(1, 1), "material": 0}]}}
	var view := WorldView.new()
	view.store = store
	var cells: Dictionary = view._settlement_cells()
	eq(cells.size(), 6, "toda a área do assentamento")
	eq(cells[Vector2i(1, 1)], WorldView.YARD_COLOR, "sob o prédio: terra batida")
	eq(cells[Vector2i(0, 0)], WorldView.STREET_COLOR, "fora dos prédios: rua de pedra")
	store.kind = "World"
	eq(view._settlement_cells().size(), 0, "no mapa-múndi o chão segue natural")
	view.free()
