extends "res://tests/test_base.gd"
## #46 fatia 2: aparência herdada do motor no cliente (store -> ator), editor de pessoa e cenário gerado.

const Store := preload("res://Store/world_store.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const WorldEditor := preload("res://Scenes/world_editor.gd")
const FamilyBuilder := preload("res://Scenes/family_builder.gd")
const Fx := preload("res://tests/fixtures.gd")

const SCENARIO := '{"Width":10,"Height":10,"Seed":1,"InitialPopulation":0,"VillageX":5,"VillageY":5,"Settlements":[{"Name":"vila","X":5,"Y":5}]}'
const LOOK := {"skin_tone": 10, "hair_tone": 90, "hair_curl": 100, "build": 100}


func _world(with_look: bool) -> Dictionary:
	var marker := {"id": {"value": 7}, "location": {"x": 1, "y": 1}}
	if with_look:
		marker["appearance"] = {"skinTone": 10, "hairTone": 90, "hairCurl": 100, "build": 100}
	var payload := {"width": 2, "height": 2, "cities": [], "externalNpcs": [marker], "activeEvents": [], "portals": [],
		"layers": {}, "livingState": {"npcs": []}}
	return Fx.snapshot(0, payload, "World")


func _actor(look: Dictionary) -> Node3D:
	var actor := NpcActor.new()
	actor.setup(7, Vector2i(1, 1), -1, ActionLabels.new(), look)
	return actor


func _person(appearance: Variant, skills: Dictionary) -> Dictionary:
	return {"name": "Ana", "sex": "Female", "age": 30, "profession": 1, "personality": null, "spouse": -1, "mother": -1,
		"father": -1, "appearance": appearance, "skills": skills}


# --- store -> view -> ator ---

func test_snapshot_carries_the_servers_looks_to_the_actor_and_without_them_the_look_is_empty() -> void:
	var store := Store.new()
	var view := WorldView.new()
	view.bind(store, ActionLabels.new())
	store.apply_snapshot(_world(true))
	eq(store.npcs[7].look, LOOK, "genes do servidor no formato do cliente")
	eq(view.npc_nodes[7].look, LOOK, "o ator recebe os genes")
	store.apply_snapshot(_world(false))
	eq(store.npcs[7].look, {}, "mundo sem aparência: vazio, o sorteio de sempre")
	eq(view.npc_nodes[7].look, {}, "o ator cai no sorteio local")
	view.free()


func test_a_delta_keeps_the_look_of_the_npc() -> void:
	var store := Store.new()
	store.apply_snapshot(_world(true))
	store.apply_delta(Fx.delta(0, 1, {"npcUpserts": [{"id": {"value": 7}, "location": {"x": 0, "y": 1}, "currentAction": 2}]}))
	eq(store.npcs[7].look, LOOK, "o delta de posição não apaga os genes")


func test_actor_wears_the_genes_and_the_extremes_differ() -> void:
	var dark := _actor({"skin_tone": 100, "hair_tone": 100, "hair_curl": 0, "build": 0})
	var light := _actor({"skin_tone": 0, "hair_tone": 0, "hair_curl": 100, "build": 100})
	var dark_skin: Color = dark._skin_mesh.material_override.albedo_color
	var light_skin: Color = light._skin_mesh.material_override.albedo_color
	check(light_skin.v > dark_skin.v, "pele 0 é mais clara que pele 100")
	check(light._hair_mesh.material_override.albedo_color.v > dark._hair_mesh.material_override.albedo_color.v, "cabelo 0 é mais claro")
	check(light._hair_mesh.scale.x > dark._hair_mesh.scale.x, "cabelo mais ondulado tem mais volume")
	check(light._torso.scale.x > dark._torso.scale.x, "porte robusto é mais largo")
	dark.free()
	light.free()


func test_actor_without_a_look_keeps_the_local_draw_of_before() -> void:
	var a := _actor({})
	var b := _actor({})
	eq(a._skin_mesh.material_override.albedo_color, b._skin_mesh.material_override.albedo_color, "mesmo id, mesma pele")
	check(NpcActor.SKIN_TONES.has(a._skin_mesh.material_override.albedo_color), "pele vem da paleta antiga")
	eq(a._torso.scale, Vector3.ONE, "porte neutro")
	a.free()
	b.free()


func test_genes_are_snapped_so_materials_are_shared_across_npcs() -> void:
	var first := _actor({"skin_tone": 41, "hair_tone": 42, "hair_curl": 50, "build": 50})
	var second := _actor({"skin_tone": 39, "hair_tone": 38, "hair_curl": 50, "build": 50})
	check(first._skin_mesh.material_override == second._skin_mesh.material_override, "genes próximos usam o mesmo material de pele")
	check(first._hair_mesh.material_override == second._hair_mesh.material_override, "e o mesmo de cabelo")
	eq(NpcActor.ramp_color(NpcActor.SKIN_RAMP, 0), NpcActor.SKIN_RAMP[0], "extremo claro")
	eq(NpcActor.ramp_color(NpcActor.SKIN_RAMP, 100), NpcActor.SKIN_RAMP[NpcActor.SKIN_RAMP.size() - 1], "extremo escuro")
	first.free()
	second.free()


# --- cenário gerado pelo editor ---

func test_scenario_carries_looks_and_skills_and_adds_the_period_block() -> void:
	var family := [{"settlement": "vila", "members": [_person({"SkinTone": 12, "HairTone": 34, "HairCurl": 56, "Build": 78}, {7: 15, 0: 40})]}]
	var out: String = WorldEditor.with_households(SCENARIO, family)
	check('"Appearance":{"SkinTone":12,"HairTone":34,"HairCurl":56,"Build":78}' in out, "genes da pessoa")
	check('"Skills":{"0":40,"7":15}' in out, "habilidades em ordem de id")
	check('"Appearance":{"Heritability":0.6}' in out, "bloco do período acrescentado")
	var back: Array = WorldEditor.read_households(out)
	eq(back[0].members[0].appearance, {"SkinTone": 12, "HairTone": 34, "HairCurl": 56, "Build": 78}, "genes lidos de volta")
	eq(back[0].members[0].skills, {0: 40, 7: 15}, "habilidades lidas de volta")
	check(not (".0," in out.replace("0.6", "")), "nenhum inteiro virou decimal")


func test_scenario_without_looks_has_no_trace_of_them() -> void:
	var out: String = WorldEditor.with_households(SCENARIO, [{"settlement": "", "members": [_person(null, {})]}])
	check(not ("Appearance" in out) and not ("Skills" in out), "pessoa comum: nada novo no cenário")


func test_an_existing_period_block_is_not_duplicated() -> void:
	var text: String = SCENARIO.replace('"Seed":1', '"Seed":1,"Appearance":{"Heritability":0.2}')
	var out: String = WorldEditor.with_households(text, [{"settlement": "", "members": [_person({"SkinTone": 1, "HairTone": 1, "HairCurl": 1, "Build": 1}, {})]}])
	eq(out.count('"Heritability"'), 1, "um bloco só")
	check('"Heritability":0.2' in out, "a herdabilidade do período é preservada")


# --- formulário "Criar pessoa" ---

func _builder(skills: Dictionary) -> Control:
	var builder := FamilyBuilder.new()
	builder.setup([{"settlement": "", "members": [_person(null, {})]}], {1: "A"}, [], skills)
	tree.root.add_child(builder)
	return builder


func test_form_stores_genes_only_when_the_look_is_custom_and_previews_them() -> void:
	var builder := _builder({})
	eq(builder.families[0].members[0].appearance, null, "começa herdada/sorteada")
	eq(builder.preview_look(), {}, "sem aparência própria a prévia usa o sorteio")
	builder.custom_appearance.button_pressed = true
	builder.gene_sliders[0].value = 80
	builder.gene_sliders[3].value = 20
	var stored: Variant = builder.families[0].members[0].appearance
	eq(stored.SkinTone, 80, "gene gravado")
	eq(stored.Build, 20, "outro gene")
	eq(builder.preview_look().skin_tone, 80, "a prévia usa o mesmo valor")
	eq(builder.preview_actor.look.skin_tone, 80, "o boneco da prévia foi vestido")
	builder.custom_appearance.button_pressed = false
	eq(builder.families[0].members[0].appearance, null, "desmarcar volta ao sorteio")
	builder.free()


func test_roll_appearance_marks_it_custom_and_stays_in_range() -> void:
	var builder := _builder({})
	builder.roll_appearance()
	check(builder.custom_appearance.button_pressed, "sortear marca como própria")
	for gene in builder.families[0].members[0].appearance.values():
		check(gene >= 0 and gene <= 100, "gene em [0,100]")
	builder.free()


func test_form_offers_a_level_per_declared_skill_and_stores_only_nonzero() -> void:
	var builder := _builder({0: "Agricultura", 7: "Artesanato"})
	eq(builder.skill_spins.size(), 2, "um campo por habilidade do período")
	builder.skill_spins[7].value = 25
	eq(builder.families[0].members[0].skills, {7: 25}, "só o nível maior que zero")
	builder.skill_spins[7].value = 0
	eq(builder.families[0].members[0].skills, {}, "zerar remove")
	builder.free()
	var none := _builder({})
	check(not none.skills_box.visible, "período sem habilidades: seção escondida")
	none.free()
