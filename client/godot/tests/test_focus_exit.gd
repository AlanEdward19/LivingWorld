extends "res://tests/test_base.gd"
## #237: o foco da casa não fica preso e a câmera não perde o corpo na troca de lugar. Sair do prédio (a pose sem prédio), trocar de
## escopo (cidade, mundo, zoom out) ou receber um snapshot atrasado da casa nunca deixam a casa desenhada nem o mundo escondido; e,
## durante toda a troca, o alvo da câmera é o corpo do jogador a cada quadro (o cenário pode carregar depois).

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const Fx := preload("res://tests/fixtures.gd")

const Grid := preload("res://Scenes/grid.gd")

const BODY := 7


func _interior(occupant: int = BODY) -> Dictionary:
	var cells: Array = []
	for y in 3:
		for x in 3:
			cells.append({"cell": {"x": x, "y": y}, "material": 3})
	var env := Fx.snapshot(0, {
		"id": {"value": 4}, "city": {"value": "city-a"}, "buildingTypeId": -1, "occupancyModeled": true,
		"layout": {"floors": 1, "floorLayouts": [{"level": 0, "cells": cells}]},
		"occupants": [{"npc": {"value": occupant}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}}],
	}, "Interior")
	env.scope.kind = 2
	return env


func _world() -> Dictionary:
	var env := Fx.snapshot(0, {"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")
	env.scope.kind = 0
	return env


## Um app no modo jogador, dono do corpo 7, na cidade.
func _player_app() -> Node:
	var fake := O.transport()
	fake.on("GET /visual/subscribe?scope=Interior", {"status": 200, "body": _interior()})
	fake.on("GET /visual/subscribe?scope=World", {"status": 200, "body": _world()})
	var app = await T.make_app(tree, fake)
	await tree.create_timer(0.5).timeout # o fim da partida do app (a primeira assinatura do mundo) termina antes de a prova começar
	app.rig.city_exit_size = 1000000.0 # o zoom do quadro de teste não pede a troca de escopo por conta própria
	await app.enter_scope("City", "city-a")
	app.player_hud.is_active = true
	app.player_hud.npc_id = BODY
	app.rig.set_mode(app.rig.THIRD)
	app.rig.follow(app.world_view.npc_nodes[BODY])
	return app


func _world_back(app: Node, why: String) -> void:
	eq(app.stage.active, false, why + ": a casa não fica desenhada")
	eq(app.world_view.visible, true, why + ": o mundo volta a ser desenhado")
	eq(app.world_view.process_mode, Node.PROCESS_MODE_INHERIT, why + ": e atualizado")
	eq(app.stage.interior_view.visible, false, why + ": a casa sai do desenho")
	eq(app.stage.interior_view.floor_nodes.size(), 0, why + ": sem pisos nem paredes no lugar do prédio")
	eq(app.stage.floor_label.visible, false, why + ": o rótulo do andar some")
	eq(app.session.interior_building, -1, why + ": nenhum prédio em foco")
	eq(app._inside_by_body, -1, why + ": o app não acha que o corpo está na casa")


func test_leaving_the_building_by_pose_tears_the_house_down() -> void:
	var app = await _player_app()
	await app._on_player_placed(4, 0)
	eq(app.stage.active, true, "a pose com prédio entra na casa")
	eq(app.world_view.visible, true, "#249: e o mundo continua à vista")
	await app._on_player_placed(-1, 0)
	_world_back(app, "pose sem prédio")
	app.queue_free()


## #249: a figura do jogador é a MESMA antes, durante e depois da casa (nenhum boneco novo), e a câmera nunca troca de alvo.
func test_the_body_and_the_camera_cross_the_door_in_the_same_frame() -> void:
	var app = await _player_app()
	var street: Node3D = app.world_view.npc_nodes[BODY]
	await app._on_player_placed(4, 0)
	var inside: Node3D = app.player_actor()
	check(inside == street, "dentro da casa o corpo é o mesmo boneco da rua")
	check(app.rig.following == street, "a câmera segue o mesmo alvo, sem troca")
	inside.set_fine(Vector2(5.5, 6.5)) # o jogador andou dentro da casa
	app.session.leave_focus()
	var outside: Node3D = app.world_view.npc_nodes[BODY]
	check(outside == street, "ao sair continua o mesmo boneco")
	check(Vector2(outside.position.x, outside.position.z).distance_to(Vector2(5.5, 6.5) * Grid.CELL) < 0.01, "onde o jogador estava")
	check(app.rig.following == outside, "a câmera segue o mesmo alvo")
	app.queue_free()


## Um snapshot da casa que chega depois da saída (o canal ainda entrega o que foi pedido) não reabre a casa por conta própria.
func test_a_late_interior_snapshot_after_leaving_does_not_bring_the_house_back() -> void:
	var app = await _player_app()
	await app._on_player_placed(4, 0)
	await app._on_player_placed(-1, 0)
	app.session.on_snapshot(_interior())
	_world_back(app, "snapshot atrasado da casa")
	app.queue_free()


## Qualquer troca de escopo (zoom out cidade -> mundo, outra cidade) limpa um foco que tenha ficado.
func test_any_scope_change_clears_a_house_left_in_focus() -> void:
	var app = await _player_app()
	await app._on_player_placed(4, 0)
	eq(app.stage.active, true)
	app.session.on_snapshot(_world())
	_world_back(app, "snapshot do mundo")
	await app._on_player_placed(4, 0)
	eq(app.stage.active, true, "pode entrar de novo")
	await app.enter_scope("World", "")
	_world_back(app, "zoom out para o mundo")
	app.queue_free()


## A pose que diz "saí" chegou enquanto a entrada ainda estava em andamento: ao terminar, o app concilia com a última pose.
func test_a_pose_that_arrives_while_a_switch_is_running_is_not_lost() -> void:
	var app = await _player_app()
	await app._on_player_placed(4, 0)
	eq(app.stage.active, true, "preparado: dentro")
	app._inside_busy = true
	await app._on_player_placed(-1, 0) # chega no meio de uma troca: não pode ser descartada
	app._inside_busy = false
	await app._reconcile_placement()
	_world_back(app, "pose perdida durante a troca")
	app.queue_free()


## O corpo é a referência: a cada quadro da troca (entrar, sair, mudar de escopo) a câmera mira o corpo e nunca fica sem alvo.
func test_the_camera_target_stays_on_the_body_every_frame_across_the_switch() -> void:
	var app = await _player_app()
	var steps: Array = [
		["entrar", func(): await app._on_player_placed(4, 0)],
		["sair", func(): await app._on_player_placed(-1, 0)],
		["snapshot atrasado da casa", func(): app.session.on_snapshot(_interior())],
		["voltar à cidade", func(): await app.enter_scope("City", "city-a")],
	]
	for step in steps:
		await step[1].call()
		for frame in 6:
			await tree.process_frame
			var own = app.player_actor()
			check(own != null and is_instance_valid(own), "%s (quadro %d): o corpo existe" % [step[0], frame])
			if own == null:
				continue
			check(app.rig.following == own, "%s (quadro %d): a câmera segue o corpo desenhado" % [step[0], frame])
			check(app.rig.target.distance_to(own.position) < 0.01, "%s (quadro %d): alvo no corpo (%s x %s)" % [step[0], frame, app.rig.target, own.position])
	app.queue_free()


## #237: o servidor responde 404 à casa que não existe: sem faixa de contrato, o foco acaba, a vista volta à cidade e a pose
## do motor não faz o app pedir a mesma casa de novo a cada passo.
func test_a_building_the_server_says_does_not_exist_ends_the_focus_without_a_contract_banner() -> void:
	var app = await _player_app()
	app.stream.transport.on("GET /visual/subscribe?scope=Interior", {"status": 404, "body": null})
	app.banner.visible = false
	await app._on_player_placed(9, 0)
	eq(app.banner.visible, false, "404 não acende a faixa de fora do contrato")
	eq(app.contract_errors.is_empty(), true, "nem registra divergência")
	_world_back(app, "casa inexistente")
	eq(app.stream.scope_kind, "City", "a vista volta ao escopo anterior (a cidade)")
	var asks := _asked(app.stream.transport, "scope=Interior&refId=9")
	await app._on_player_placed(9, 0)
	eq(_asked(app.stream.transport, "scope=Interior&refId=9"), asks, "a mesma casa não é pedida de novo")
	app.queue_free()


func _asked(fake, part: String) -> int:
	var n := 0
	for c in fake.calls:
		if part in c:
			n += 1
	return n


## #237: a pose diz "dentro" mas a célula do corpo não é da planta (posto de trabalho no campo): nenhum palco sem fim com a casa longe,
## o corpo segue no mundo e a mesma pose não reabre a casa a cada passo. Com o corpo dentro da planta, a casa abre e o corpo está no palco.
func test_a_pose_inside_with_the_body_outside_the_plan_does_not_open_the_house() -> void:
	var app = await _player_app()
	app.player_hud.fine = Vector2(40.5, 40.5) # fora da planta 3x3 da casa
	await app._on_player_placed(4, 0)
	eq(app.stage.active, false, "corpo fora da planta: a casa não fica desenhada")
	eq(app.world_view.visible, true, "e o mundo continua desenhado")
	eq(app.session.interior_building, -1, "nenhum prédio em foco")
	await app._on_player_placed(4, 0)
	eq(app.stage.active, false, "a mesma pose de novo não reabre a casa")
	app.queue_free()


func test_a_pose_inside_with_the_body_in_the_plan_puts_the_body_on_the_stage() -> void:
	var app = await _player_app()
	app.player_hud.fine = Vector2(1.5, 1.5)
	await app._on_player_placed(4, 0)
	eq(app.stage.active, true, "corpo dentro da planta: a casa abre")
	eq(app.stage.interior_view.guided_id, BODY, "o corpo está na casa em foco (adotado: o mesmo boneco da rua)")
	check(not app.stage.interior_view.occupant_nodes.has(BODY), "sem segundo boneco do corpo")
	check(app.stage.interior_view.covers(Vector2i(1, 1), 0), "a célula do corpo é da planta")
	app.queue_free()
