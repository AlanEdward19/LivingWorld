extends "res://tests/test_base.gd"
## #254: arte procedural do NPC: malhas geradas e compartilhadas, figura estável por id, proporções por idade e sexo, roupa por período,
## LOD de longe barato e poses principais.

const NpcActor := preload("res://Scenes/npc_actor.gd")
const NpcMesh := preload("res://Scenes/npc_mesh.gd")
const Look := preload("res://Scenes/npc_look.gd")
const ActionLabels := preload("res://Store/action_labels.gd")


func _obs(sex: String, age: int, stage: String, extra: Dictionary = {}) -> Dictionary:
	var obs := {"name": "X", "sex": sex, "age": age, "stage": stage, "profession_id": 1, "profession_name": "Lavrador",
		"height_cm": 170.0, "growth": 1.0, "pregnancy": 0.0, "ill": false, "symptomatic": false, "bedridden": false,
		"emotions": {}, "dominant": "", "mourning": false, "carried": [], "held": {}}
	obs.merge(extra, true)
	return obs


func _actor(id: int, obs: Dictionary, near: bool = true) -> Node3D:
	var actor := NpcActor.new()
	actor.setup(id, Vector2i(0, 0), 5, ActionLabels.new())
	actor.apply_observable(obs)
	actor.set_detail(near)
	return actor


func _count(node: Node) -> int:
	var total := 1
	for child in node.get_children():
		total += _count(child)
	return total


func test_meshes_are_generated_smooth_and_shared() -> void:
	var torso: Mesh = NpcMesh.torso("male")
	check(torso is ArrayMesh and torso.get_surface_count() == 1, "o tronco é uma malha gerada, não uma caixa")
	check(torso == NpcMesh.torso("male"), "a mesma forma é a mesma malha (cache compartilhado)")
	check(NpcMesh.torso("female") != NpcMesh.torso("male"), "mulher tem tronco próprio")
	var arrays: Array = torso.surface_get_arrays(0)
	check(arrays[Mesh.ARRAY_NORMAL].size() == arrays[Mesh.ARRAY_VERTEX].size(), "normais suaves por vértice")
	check(arrays[Mesh.ARRAY_VERTEX].size() < 300, "poucos vértices por peça (cabe em 500 NPCs)")
	for style in ["short", "buzz", "curly", "fringe", "long", "bob", "ponytail", "bun", "tuft"]:
		check(NpcMesh.hair(style).get_surface_count() >= 1, "estilo de cabelo %s existe" % style)


func test_same_id_and_look_give_the_same_figure() -> void:
	var a := _actor(41, _obs("Female", 30, "Adult"))
	var b := _actor(41, _obs("Female", 30, "Adult"))
	eq(a._style, b._style, "mesmo id e corpo: mesmo estilo")
	check(a._hair_mesh.mesh == b._hair_mesh.mesh and a._torso.mesh == b._torso.mesh, "mesmas malhas")
	eq(_count(a), _count(b), "mesma contagem de nós")
	var styles := {}
	for id in 30:
		styles[String(Look.style_of(id, "adult", true, "medieval").hair)] = true
	check(styles.size() >= 3, "o id varia o cabelo (vários estilos entre as mulheres)")
	a.free()
	b.free()


func test_age_and_sex_change_proportions_as_the_engine_says() -> void:
	var woman := _actor(7, _obs("Female", 30, "Adult"))
	var man := _actor(7, _obs("Male", 30, "Adult"))
	var child := _actor(7, _obs("Male", 8, "Child", {"height_cm": 125.0}))
	var elder := _actor(7, _obs("Male", 72, "Elder"))
	check(woman._torso.mesh != man._torso.mesh, "tronco de mulher difere do de homem")
	check(child._torso.mesh == NpcMesh.torso("child") and elder._torso.mesh == NpcMesh.torso("elder"), "criança e idoso têm tronco próprio")
	check(child.scale.y < man.scale.y, "a altura vem do motor")
	check(child._head.scale.y > man._head.scale.y, "criança tem cabeça maior")
	check(woman._arms[0].get_child(0).scale.x < man._arms[0].get_child(0).scale.x, "braço de mulher é mais fino")
	eq(child._style.hair == "tuft", false, "criança não usa cabelo de bebê")
	eq(Look.style_of(3, "baby", false, "modern").hair, "tuft", "bebê tem tufo")
	for actor in [woman, man, child, elder]:
		actor.free()


func test_clothes_follow_the_period() -> void:
	var modern := Look.style_of(5, "adult", false, "modern")
	var medieval := Look.style_of(5, "adult", false, "medieval")
	check(not bool(modern.boot) and bool(medieval.boot), "tênis no moderno, bota no medieval")
	eq(medieval.outfit, "tunic", "túnica no medieval")
	check(modern.outfit in ["tee", "shirt"], "camiseta ou camisa no moderno")
	var dresses := 0
	for id in 40:
		if Look.style_of(id, "adult", true, "modern").outfit == "dress":
			dresses += 1
	check(dresses > 5 and dresses < 25, "parte das mulheres de época moderna usa vestido")
	eq(Look.era_for("cidade-media"), "medieval", "presets atuais são medievais")
	eq(Look.era_for("sandbox-casal-suburbio"), "modern", "sandbox do casal é moderno")


func test_face_has_eyes_brows_mouth_and_blinks() -> void:
	var actor := _actor(9, _obs("Male", 40, "Adult", {"dominant": "joy"}))
	actor.animate(0.05)
	check(actor._face != null and actor._face.visible, "rosto perto")
	eq(actor._face.get_meta("eyes").size(), 2, "dois olhos")
	var seen_closed := false
	for i in 400:
		actor.animate(0.02)
		if actor._face.get_meta("eyes")[0].scale.y < 0.5:
			seen_closed = true
	check(seen_closed, "o olho pisca em algum momento")
	actor.set_far(true)
	check(actor._rig == null, "longe: sem rig")
	actor.free()


func test_far_lod_stays_cheap_and_main_poses_exist() -> void:
	var far := _actor(11, _obs("Female", 30, "Adult"), false)
	far.set_far(true)
	var near := _actor(12, _obs("Female", 30, "Adult"), true)
	check(_count(far) <= 20, "longe: poucos nós (%d)" % _count(far))
	check(_count(far) < _count(near), "o detalhe de perto traz mais nós")
	check(far._beard == null and far._face == null, "longe: nem rosto nem barba")
	for p in ["idle", "walk", "sleep", "work", "eat", "socialize", "buy", "power", "steal", "cry"]:
		near.pose = p
		near.animate(0.1)
		near.animate(0.1)
	near.pose = "sleep"
	near.animate(0.5)
	check(near._body.rotation.x < -1.0, "deitar")
	far.free()
	near.free()


func test_pose_change_blends_instead_of_snapping() -> void:
	var actor := _actor(21, _obs("Male", 30, "Adult"))
	actor.pose = "idle"
	for i in 5:
		actor.animate(0.05)
	actor.pose = "sleep"
	actor.animate(0.02)
	check(actor._body.rotation.x > -0.5, "primeiro quadro da troca ainda perto da pose anterior")
	for i in 20:
		actor.animate(0.05)
	check(actor._body.rotation.x < -1.2, "depois do tempo de mistura, deitado")
	actor.free()
