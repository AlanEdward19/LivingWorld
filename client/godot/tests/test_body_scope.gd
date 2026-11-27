extends "res://tests/test_base.gd"
## #237: a troca cidade ↔ mundo com o corpo do jogador andando. O escopo certo é pedido assim que a posição fina cruza a borda (com
## histerese de uma célula na entrada), e enquanto o boneco não está no escopo desenhado a câmera segue um ponto de apoio na posição
## fina (nunca "aguardando corpo", nunca solta pelo diretor de seguir) até o boneco novo chegar ao mesmo ponto.

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Fx := preload("res://tests/fixtures.gd")
const BodyScope := preload("res://Scenes/body_scope.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Registry := preload("res://Store/entity_registry.gd")

const BODY := 7
const CITY := {"id": "city-a", "bounds": Rect2i(10, 10, 12, 12)}


func test_wanted_scope_follows_the_border_with_one_cell_of_hysteresis() -> void:
	var cities := {"city-a": {"bounds": Rect2i(10, 10, 12, 12)}}
	eq(BodyScope.wanted("City", Vector2(15.5, 15.5), CITY, cities), [], "dentro da cidade: o escopo serve")
	eq(BodyScope.wanted("City", Vector2(21.9, 21.9), CITY, cities), [], "na última célula da cidade: ainda serve")
	eq(BodyScope.wanted("City", Vector2(22.1, 15.5), CITY, cities), ["World", ""], "uma célula além da borda: pede o mundo")
	eq(BodyScope.wanted("City", Vector2(9.9, 15.5), CITY, cities), ["World", ""], "e pelo outro lado")
	eq(BodyScope.wanted("World", Vector2(10.5, 15.5), CITY, cities), [], "no anel de borda: o mundo serve (histerese)")
	eq(BodyScope.wanted("World", Vector2(11.5, 15.5), CITY, cities), ["City", "city-a"], "uma célula para dentro: pede a cidade")
	eq(BodyScope.wanted("World", Vector2(40.5, 40.5), CITY, cities), [], "longe de qualquer cidade: o mundo serve")
	eq(BodyScope.wanted("City", Vector2.INF, CITY, cities), [], "sem posição fina ainda: nada a pedir")
	eq(BodyScope.wanted("City", Vector2(99.5, 99.5), {"id": "x", "bounds": Rect2i()}, cities), [], "limites desconhecidos: nada a pedir")


func test_asking_is_throttled() -> void:
	var scope := BodyScope.new()
	check(scope.may_ask(10000), "o primeiro pedido passa")
	check(not scope.may_ask(10500), "o mesmo pedido não se repete em 0,5 s")
	check(scope.may_ask(10000 + BodyScope.ASK_EVERY_MS), "e passa de novo depois do intervalo")


func _interior() -> Dictionary:
	return Fx.snapshot(0, {"id": {"value": 4}, "city": {"value": "city-a"}, "buildingTypeId": -1, "occupancyModeled": true,
		"layout": {"floors": 1, "floorLayouts": []}, "occupants": []}, "Interior")


func _world() -> Dictionary:
	var env := Fx.snapshot(0, {"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")
	env.scope.kind = 0
	return env


func _player_app() -> Node:
	var fake := O.transport()
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": _world()})
	var app = await T.make_app(tree, fake)
	await tree.create_timer(0.5).timeout
	app.rig.city_exit_size = 1000000.0
	await app.enter_scope("City", "city-a")
	app.store.city = CITY.duplicate()
	app.player_hud.is_active = true
	app.player_hud.npc_id = BODY
	app.player_hud.fine = Vector2(15.5, 15.5)
	app.rig.set_mode(app.rig.THIRD)
	app.rig.follow(app.world_view.npc_nodes[BODY])
	return app


func _asked(fake, part: String) -> int:
	var n := 0
	for c in fake.calls:
		if part in c:
			n += 1
	return n


## O boneco some do desenho (o servidor já não o manda neste escopo): a câmera segue o ponto de apoio na posição fina, quadro a quadro.
func test_the_camera_follows_the_fine_position_while_the_body_node_is_gone() -> void:
	var app = await _player_app()
	var body: Node3D = app.world_view.npc_nodes[BODY]
	body.position = Vector3(15.5, 0.0, 15.5)
	await tree.process_frame
	var lift_before: float = app.rig.lift
	app.world_view.npc_nodes.erase(BODY)
	body.queue_free()
	for frame in 8:
		app.player_hud.fine = Vector2(15.5 + 0.3 * frame, 15.5) # o corpo continua andando (previsão)
		await tree.process_frame
		check(app.rig.following != null and is_instance_valid(app.rig.following), "quadro %d: a câmera tem o que seguir" % frame)
		check(app.rig.target.distance_to(Vector3(app.player_hud.fine.x, 0.0, app.player_hud.fine.y)) < 0.35,
			"quadro %d: o alvo está no corpo (%s x %s)" % [frame, app.rig.target, app.player_hud.fine])
		eq(app.rig.lift, lift_before, "quadro %d: a câmera não sobe 'aguardando corpo'" % frame)
	check(app.body_scope.anchor != null, "o ponto de apoio existe")
	app.queue_free()


## O diretor de seguir não solta a câmera do corpo do jogador que sumiu do escopo desenhado (era o que a deixava no alto e parada).
func test_the_follow_director_does_not_release_the_camera_from_the_players_body() -> void:
	var app = await _player_app()
	app.director.target = Registry.ref(Registry.PERSON, BODY)
	var body: Node3D = app.world_view.npc_nodes[BODY]
	app.world_view.npc_nodes.erase(BODY)
	body.queue_free()
	await tree.process_frame # o quadro em que o nó sai
	for frame in 5:
		await tree.process_frame
		check(app.rig.following != null and is_instance_valid(app.rig.following), "quadro %d: o diretor não soltou a câmera" % frame)
	app.queue_free()


## O boneco volta ao desenho nascendo longe do corpo (centro da célula): a câmera fica no apoio até os dois coincidirem; só então passa.
func test_the_camera_changes_hands_only_when_the_new_body_reaches_the_anchor() -> void:
	var app = await _player_app()
	var actor: Node3D = app.world_view.npc_nodes[BODY]
	app.world_view.npc_nodes.erase(BODY) # o boneco sai do desenho (o mesmo ator volta depois, como o recém-criado)
	app.player_hud.fine = Vector2(15.8, 15.2)
	await tree.process_frame
	await tree.process_frame
	var anchor: Node3D = app.body_scope.anchor
	check(anchor != null and app.rig.following == anchor, "sem boneco: segue o apoio")
	actor.position = Vector3(15.5, 0.0, 15.5) # o boneco novo nasce no centro da célula
	app.world_view.npc_nodes[BODY] = actor
	await tree.process_frame
	await tree.process_frame
	check(app.rig.following == app.body_scope.anchor, "boneco ainda longe do apoio: a câmera não troca")
	actor.position = Vector3(15.8, 0.0, 15.2)
	await tree.process_frame
	await tree.process_frame
	check(app.rig.following == actor, "boneco no apoio: a câmera passa para ele")
	check(app.body_scope.anchor == null, "e o apoio sai")
	app.queue_free()


## Cruzar a borda pede o mundo na hora (não espera o boneco sumir), uma vez só enquanto a assinatura não chega.
func test_crossing_the_border_asks_for_the_right_scope_at_once() -> void:
	var app = await _player_app()
	await tree.process_frame
	var world_before := _asked(app.stream.transport, "scope=World")
	app.player_hud.fine = Vector2(23.4, 15.5) # fora dos limites da cidade (10..21)
	await tree.process_frame
	await tree.process_frame
	var asked := _asked(app.stream.transport, "scope=World")
	check(asked > world_before, "o mundo é pedido assim que o corpo sai (%d -> %d)" % [world_before, asked])
	for frame in 5:
		await tree.process_frame
	eq(_asked(app.stream.transport, "scope=World"), asked, "e o mesmo pedido não se repete a cada quadro")
	app.queue_free()


## A troca de escopo não recentra a câmera nem muda o zoom enquanto o jogador tem o corpo (era o "buga" até andar mais um pouco).
func test_the_snapshot_of_a_new_scope_does_not_recenter_the_players_camera() -> void:
	var app = await _player_app()
	app.rig.set_zoom(9.0)
	var zoom_before: float = app.rig.zoom_size
	app.session.on_snapshot(_world())
	eq(app.rig.zoom_size, zoom_before, "o zoom do jogador não muda com o snapshot do mundo")
	app.queue_free()
