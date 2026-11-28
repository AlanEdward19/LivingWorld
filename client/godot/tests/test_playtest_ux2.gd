extends "res://tests/test_base.gd"
## #246 (2º teste de jogo): o enquadramento de cena (uso de móvel e interação social) deixa a vista mais a prumo, enquadra os dois corpos,
## e um uso do motor (`outcome` é um dicionário de efeitos) não derruba o retorno do clique.

const CameraRig := preload("res://Scenes/camera_rig.gd")
const Grid := preload("res://Scenes/grid.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")


func _entry(size: float) -> Array:
	var h := Grid.floor_height()
	var box := AABB(Vector3.ZERO, Vector3(size, h, size))
	return [-1, box, box, [], true]


func _settle(rig: CameraRig, frames: int = 240) -> void:
	for i in frames:
		rig._process(0.016)


func _rig(body: Node3D) -> CameraRig:
	var rig := CameraRig.new()
	rig.add_child(body)
	var entries: Array = [_entry(6.0)]
	rig.blockers = func() -> Array: return entries
	rig.set_mode(rig.THIRD)
	rig.yaw = 0.0
	rig.follow(body)
	return rig


func test_scene_framing_looks_more_from_above_and_returns_when_it_ends() -> void:
	var kept := Grid.floor_meters
	Grid.floor_meters = 3.0
	var body := Node3D.new()
	body.position = Vector3(3, 0, 3)
	var rig := _rig(body)
	_settle(rig)
	var normal := rig.view_pitch
	rig.set_scene_framing(true)
	_settle(rig)
	check(rig.view_pitch <= CameraRig.SCENE_PITCH + 0.02, "no uso a vista fica a prumo (%s)" % rad_to_deg(rig.view_pitch))
	check(rig.view_pitch < normal, "mais a prumo que o olhar de antes")
	rig.set_scene_framing(false)
	_settle(rig)
	check(absf(rig.view_pitch - normal) < 0.02, "ao acabar volta ao olhar de antes")
	rig.free()
	Grid.floor_meters = kept


func test_scene_framing_frames_the_midpoint_of_both_bodies() -> void:
	var body := Node3D.new()
	body.position = Vector3(2, 0, 2)
	var rig := _rig(body)
	var partner := Node3D.new()
	partner.position = Vector3(4, 0, 2)
	rig.add_child(partner)
	rig.set_scene_framing(true, partner)
	_settle(rig, 5)
	check(rig.target.distance_to(Vector3(3, 0, 2)) < 0.001, "o alvo é o ponto médio dos dois (%s)" % rig.target)
	rig.set_scene_framing(false)
	_settle(rig, 5)
	check(rig.target.distance_to(body.position) < 0.001, "sem a cena a câmera volta ao corpo")
	rig.free()


func test_a_use_reply_with_an_effects_dictionary_is_not_a_refusal() -> void:
	var entry := {"text": "Chuveiro", "verb": "use:shower"}
	eq(PlayerHud.done_text(entry, {"outcome": {"hygiene": 15}, "note": null}), "✓ Chuveiro", "outcome em dicionário não quebra o texto")
	eq(PlayerHud.done_text(entry, {"outcome": "done", "note": "Marina riu"}), "✓ Marina riu", "outcome em texto segue valendo")


func test_the_body_card_says_what_the_body_is_doing_instead_of_idle() -> void:
	var frame := PlayerFrame.new()
	frame.set_body("Marina", "Parado", {"health": 100, "hunger": 100, "thirst": 100, "sleep": 100})
	eq(frame.action_label.text, "Parado", "sem ação em curso o cartão diz o que o motor diz")
	frame.set_busy("Tomar banho — 12% · faltam 3 min", 0.12)
	eq(frame.action_label.text, "Tomar banho", "no uso o cartão diz o uso, não Parado")
	frame.set_busy("Indo: Abraçar…", 0.1)
	eq(frame.action_label.text, "Parado", "caminhando até a pessoa o cartão não finge que já abraça")
	frame.set_busy("", 0.0)
	eq(frame.action_label.text, "Parado", "ao acabar volta ao motor")
	frame.free()
