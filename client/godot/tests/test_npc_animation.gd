extends "res://tests/test_base.gd"

const Anim := preload("res://Scenes/animation_catalog.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")


func _labels() -> Object:
	var labels := ActionLabels.new()
	labels.load_from([
		{"actionType": 0, "label": "Comendo"}, {"actionType": 1, "label": "Dormindo"},
		{"actionType": 2, "label": "Trabalhando"}, {"actionType": 3, "label": "Conversando"},
		{"actionType": 4, "label": "Viajando"}, {"actionType": 5, "label": "Parado"},
		{"actionType": 6, "label": "Comprando"}, {"actionType": 7, "label": "Usando um poder"},
	])
	return labels


func test_every_action_type_is_mapped() -> void:
	var seen := {}
	for action in range(8):
		check(Anim.is_mapped(action), "ActionType %d tem pose mapeada" % action)
		var pose: String = Anim.pose_for(action)
		check(Anim.POSES.has(pose), "pose '%s' existe no catálogo de poses" % pose)
		seen[pose] = true
	eq(seen.size(), 8, "as 8 ações usam 8 poses distintas")
	eq(Anim.pose_for(999), Anim.NEUTRAL_POSE, "pose neutra declarada para o desconhecido")


func test_unknown_action_uses_neutral_pose() -> void:
	var actor := NpcActor.new()
	actor.setup(3, Vector2i(1, 1), 999, _labels())
	actor.advance(0.1)

	eq(actor.pose, "idle", "pose neutra")
	eq(actor.label_text, "ação 999", "mostra o id da ação, sem inventar texto")
	actor.free()


func test_label_comes_from_server_catalog() -> void:
	var labels := ActionLabels.new()
	labels.load_from([{"actionType": 1, "label": "RÓTULO-DO-SERVIDOR"}])
	var actor := NpcActor.new()

	actor.setup(3, Vector2i(0, 0), 1, labels)

	eq(actor.label_text, "RÓTULO-DO-SERVIDOR")
	var source := FileAccess.get_file_as_string("res://Scenes/npc_actor.gd") + FileAccess.get_file_as_string("res://Scenes/animation_catalog.gd")
	for word in ["Dormindo", "Trabalhando", "Comendo", "Viajando"]:
		check(not (word in source), "texto de ação '%s' não está escrito no cliente" % word)
	actor.free()


func test_sleeping_legs_lie_at_the_hips_not_standing() -> void:
	var actor := NpcActor.new()
	actor.setup(3, Vector2i(1, 1), 1, _labels())
	actor.advance(0.1)
	for leg in actor._legs:
		check(absf(leg.rotation.x + PI / 2.0) < 0.01, "perna deitada")
		check(leg.position.y < 0.3, "perna desceu até a altura do corpo deitado")
	actor.set_action(5, _labels())
	actor.advance(0.1)
	for leg in actor._legs:
		check(absf(leg.position.y - 0.36) < 0.001, "de pé, a perna volta ao quadril")
	actor.free()


func test_frozen_world_stops_walking_and_animation() -> void:
	var actor := NpcActor.new()
	actor.setup(4, Vector2i(0, 0), 4, _labels())
	actor.move_to(Vector2i(3, 0), 2.0)
	NpcActor.frozen = true
	var before := actor.position
	actor.advance(1.0)
	eq(actor.position, before, "pausado: não anda")
	actor.move_to(Vector2i(5, 0), 2.0)
	eq(actor.is_moving(), false, "passo manual pausado: aparece no destino")
	NpcActor.frozen = false
	actor.free()
