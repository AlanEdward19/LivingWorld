extends "res://tests/test_base.gd"
## O que já chegava e o cliente descartava (ADR-0047, etapa 1): moved, indicadores, relocação, prédio novo por delta,
## eventos com lugar no mapa; poses de Steal e Cry; seguir que sobrevive à troca de escopo; tempo travado sem admin.

const T := preload("res://tests/test_inspector.gd")
const Fx := preload("res://tests/fixtures.gd")
const Store := preload("res://Store/world_store.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Anim := preload("res://Scenes/animation_catalog.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const EventMarkers := preload("res://Scenes/render/event_markers.gd")


func _city_store() -> Store:
	var store := Store.new()
	store.apply_snapshot(T.city())
	return store


func test_moved_indicators_and_relocation_reach_the_store() -> void:
	var store := _city_store()
	var moves: Array = []
	store.npc_changed.connect(func(id, from, to, _a): moves.append([id, from, to]))
	var indicator_events: Array = []
	store.indicators_changed.connect(func(): indicator_events.append(1))
	store.apply_delta(Fx.delta(0, 1, {
		"moved": [{"npcId": {"value": 8}, "location": {"x": 5, "y": 3}}],
		"npcUpserts": [{"id": {"value": 7}, "location": {"x": 3, "y": 4}, "currentAction": 4, "relocationDestination": {"x": 9, "y": 9}}],
		"indicators": [{"key": "wealth", "value": 42.0}]}))
	eq(store.npcs[8].cell, Vector2i(5, 3), "`moved` move quem não veio inteiro em npcUpserts")
	eq(store.npcs[7].relocation, Vector2i(9, 9), "destino de mudança guardado")
	eq(moves.size(), 2)
	eq(float(store.city.indicators.wealth), 42.0, "indicador do delta atualiza a ficha da cidade")
	eq(indicator_events.size(), 1)


func test_new_building_from_a_delta_is_drawn_with_its_plan_and_no_extra_read() -> void:
	var fake := T.transport()
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	var subscribes: int = fake.count_calls("GET /visual/subscribe?scope=City")

	# AD-170: o delta traz a planta completa (andares, planta, valor): nada de ler o retrato da cidade de novo
	app.stream.delta_applied.emit(Fx.delta(0, 1, {"buildingUpserts": [{"id": {"value": 5}, "buildingTypeId": 3,
		"cityId": {"value": "city-a"}, "location": {"x": 3, "y": 0}, "orientation": 0, "floors": 3, "value": 50, "lodging": null,
		"groundFootprint": [{"cell": {"x": 0, "y": 0}, "material": 2}, {"cell": {"x": 1, "y": 0}, "material": 1}]}]}))
	await tree.process_frame
	check(app.world_view.building_nodes.has(5), "o prédio novo aparece na hora")
	eq(app.world_view.building_nodes.size(), 2, "os outros não foram redesenhados à toa")
	eq(app.store.buildings[5].floors, 3, "a planta e os andares vêm do próprio delta")
	eq(app.store.buildings[5].footprint.size(), 2)
	eq(app.store.pending_footprints.size(), 0)
	eq(fake.count_calls("GET /visual/subscribe?scope=City"), subscribes, "nenhuma leitura extra do retrato da cidade")
	eq(app.store.kind, "City")
	app.queue_free()


func test_located_event_becomes_a_clickable_marker_that_fades() -> void:
	var app = await T.make_app(tree, T.transport())
	app.stream.delta_applied.emit(Fx.delta(0, 1, {"events": [
		{"tick": 12, "kind": 3, "label": "Incêndio no celeiro", "location": {"x": 2, "y": 3}},
		{"tick": 12, "kind": 1, "label": "Sem lugar", "location": null}]}))
	eq(app.world_view.events.count(), 1, "só o evento com lugar vira marcador")
	check(app.toasts.history.has("Incêndio no celeiro") and app.toasts.history.has("Sem lugar"), "o aviso de texto continua")
	var id: int = app.world_view.events.positions.keys()[0]
	await app.inspect(Registry.ref(Registry.EVENT, id))
	check("Incêndio no celeiro" in app.inspector.text, app.inspector.text)
	eq(app.inspector.focus_button.visible, true, "Ir até lá")
	app.world_view.events.advance(EventMarkers.LIFETIME + 1.0)
	eq(app.world_view.events.count(), 0, "o marcador some com o tempo")
	app.queue_free()


func test_steal_and_cry_have_their_own_pose() -> void:
	eq(Anim.pose_for(8), "steal")
	eq(Anim.pose_for(9), "cry")
	check(Anim.ACTION_ONLY_POSES.has("steal") and not Anim.POSES.has("steal"), "pose só de ação: o vocabulário de uso de objeto (do servidor) não muda")
	eq(Anim.pose_for(5, "steal"), "idle", "objeto não pode declarar pose que não é de uso")
	var idle := NpcActor.new()
	idle.setup(1, Vector2i.ZERO, 5, null)
	var thief := NpcActor.new()
	thief.setup(2, Vector2i.ZERO, 8, null)
	var crying := NpcActor.new()
	crying.setup(3, Vector2i.ZERO, 9, null)
	for actor in [idle, thief, crying]:
		actor.animate(0.1)
	check(absf(thief._arms[1].rotation.x - idle._arms[1].rotation.x) > 0.5, "furtar estende o braço")
	check(crying._head.rotation.x > 0.3 and crying._arms[0].rotation.x < -2.0, "chorar: cabeça baixa e mãos no rosto")
	for actor in [idle, thief, crying]:
		actor.free()


func test_follow_survives_scope_changes_and_entering_a_building() -> void:
	var fake := T.transport()
	fake.on("GET /visual/subscribe?scope=Interior", {"status": 200, "body": T.interior_body()})
	var app = await T.make_app(tree, fake)
	await app.enter_scope("City", "city-a")
	app.follow(Registry.ref(Registry.PERSON, 7))
	eq(app.rig.following, app.world_view.npc_nodes[7], "segue o nó da pessoa")

	await app.enter_building(4)
	app.director.step(0.1)
	eq(app.rig.following, app.stage.interior_view.occupant_nodes[7], "dentro do prédio segue o ocupante")
	await app.exit_building()
	app.director.step(0.1)
	eq(app.rig.following, app.world_view.npc_nodes[7], "de volta à cidade, o nó novo")

	await app.enter_scope("World", "")
	app.director.step(2.5)
	await tree.process_frame
	eq(app.director.target, Registry.ref(Registry.PERSON, 7), "o alvo continua no mundo")
	eq(app.rig.following, null, "sem nó na cena, a câmera vai à última posição conhecida")
	check(app.director.resolve() != Vector3.INF, "a ficha diz onde a pessoa está")
	check("Ana" in app.director.label() or "7" in app.director.label(), app.director.label())

	app.follow(Registry.ref(Registry.CITY, "city-a"))
	eq(app.director.resolve(), Vector3(2.5, 0, 2.5), "seguir a cidade no mapa-múndi")
	app.unfollow()
	eq(app.director.active(), false)
	app.queue_free()


func test_follow_a_household_uses_the_known_members() -> void:
	var app = await T.make_app(tree, T.transport())
	await app.enter_scope("City", "city-a")
	await app.select_npc(7) # a ficha diz que Ana mora na casa 3
	app.follow(Registry.ref(Registry.HOUSEHOLD, 3))
	eq(app.director.resolve(), app.world_view.npc_nodes[7].position, "centro dos moradores conhecidos")
	app.queue_free()


func test_time_controls_are_disabled_with_the_reason_without_admin_token() -> void:
	var fake := T.transport()
	var app := T.App.new()
	app.admin_enabled = false
	tree.root.add_child(app)
	await app.start(fake, false)
	eq(app.clock_panel.can_control, false)
	check("admin" in app.clock_panel.locked_reason, app.clock_panel.locked_reason)
	await app.clock_panel.command("pause")
	await app.clock_panel.set_speed(4.0)
	eq(fake.count_calls("POST /simulation"), 0, "nada é mandado (sem 403)")
	check("só admin" in app.clock_panel.status_text, app.clock_panel.status_text)
	app.queue_free()
