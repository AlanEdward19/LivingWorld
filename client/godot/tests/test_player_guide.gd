extends "res://tests/test_base.gd"
## #238: o guia da primeira posse (aparece só na primeira vez por mundo; o conteúdo muda com família, solidão, emprego e criança),
## a lista "O que dá para fazer" na ajuda e o rótulo de chão no cartão. O cliente só escreve o que o motor disse.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const PlayerGuide := preload("res://Scenes/player_guide.gd")
const FirstSteps := preload("res://Scenes/first_steps.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Kit := preload("res://Scenes/menu/menu_kit.gd")

const PATH := "user://test_guide_settings.cfg"
const FORWARD := Vector2(0, -1) ## a câmera olha para -z: a frente é "para cima"

const FAMILY := {"child": false, "homeless": false, "pressing": "hunger", "money": 12,
	"home": {"x": 10, "y": 0, "distance": 10, "alone": false,
		"members": [{"id": 8, "name": "Ana", "relation": "spouse"}, {"id": 9, "name": "Caio", "relation": "child"}],
		"pantry": [{"resourceId": 1, "quantity": 4, "price": 0}]},
	"job": {"x": 0, "y": -20, "distance": 20, "locationType": 2}, "vacancy": null, "wild": {"x": -30, "y": 0, "distance": 30}}
const ALONE := {"child": false, "homeless": false, "pressing": "none", "money": 0,
	"home": {"x": 10, "y": 0, "distance": 10, "alone": true, "members": [], "pantry": []},
	"job": null, "vacancy": {"x": 0, "y": 12, "distance": 12, "locationType": 3}, "wild": {"x": -30, "y": 0, "distance": 30}}
const KID := {"child": true, "homeless": false, "pressing": "thirst", "money": 0,
	"home": {"x": 3, "y": 0, "distance": 3, "alone": false, "members": [{"id": 8, "name": "Ana", "relation": "parent"}], "pantry": []},
	"job": null, "vacancy": null, "wild": null}


func _name_of(id: int) -> String:
	return "Comida" if id == 1 else "item"


func _all(steps: Array) -> String:
	var text := ""
	for step in steps:
		text += "%s: %s\n" % [step.title, step.text]
	return text


func test_a_family_member_is_told_who_lives_with_them_where_home_is_and_what_the_pantry_has() -> void:
	var steps := PlayerGuide.steps_for(FAMILY, "Ana", Vector2(0.5, 0.5), FORWARD, _name_of)
	var text := _all(steps)
	check("Ana (cônjuge)" in text and "Caio (filho)" in text, "quem mora com você: " + text)
	check("Sua casa fica a 15 m, à direita" in text, "a casa, em metros do motor e pelo lado da câmera: " + text)
	check("4 Comida" in text and "aperte E" in text, "o que a despensa tem e a tecla: " + text)
	check("Você está com fome" in text, "o que mais aperta")
	check("Seu emprego fica a 30 m, à frente" in text and "segure E para Trabalhar" in text, "emprego: onde e com que tecla: " + text)
	check("Cortar lenha" in text, "a árvore como saída")
	eq(steps.size(), 4, "quem: o que aperta: trabalho: teclas")


func test_someone_alone_without_a_job_is_told_so_and_pointed_to_a_vacancy() -> void:
	var text := _all(PlayerGuide.steps_for(ALONE, "Davi", Vector2(0.5, 0.5), FORWARD, _name_of))
	check("Você mora só" in text and "ninguém vai cuidar de você" in text, "sozinho: " + text)
	check("sem emprego" in text and "vaga a 18 m, atrás" in text, "sem emprego: a vaga e o lado: " + text)
	check(not "Você mora com" in text, "sem família, nada de moradores")
	check("despensa de casa está vazia" in text, "despensa vazia: o aviso e o que fazer: " + text)


func test_a_child_gets_no_job_and_is_told_to_stay_close_and_ask_for_care() -> void:
	var steps := PlayerGuide.steps_for(KID, "Bia", Vector2(0.5, 0.5), FORWARD, _name_of)
	var text := _all(steps)
	check("ainda sem idade de adulto" in text and "Ana (pai ou mãe)" in text, "criança com quem cuida dela: " + text)
	check("Quem ainda não é adulto não trabalha" in text and not "Seu emprego" in text, "sem emprego: " + text)
	check("sede" in text and "beber" in text, "o que aperta: sede")


func test_without_start_data_there_is_no_guide_and_nothing_is_made_up() -> void:
	eq(PlayerGuide.steps_for({}, "X", Vector2.INF, FORWARD).size(), 0, "sem dado do motor, sem guia")
	var homeless := PlayerGuide.steps_for({"child": false, "homeless": true, "home": null, "job": null, "vacancy": null, "wild": null, "pressing": "none"}, "Zé", Vector2.INF, FORWARD)
	check("sem teto" in _all(homeless) and not "Sua casa" in _all(homeless), "sem teto: nenhuma casa inventada")


func test_directions_follow_the_camera_not_a_fixed_compass() -> void:
	eq(PlayerGuide.direction_word(Vector2(0, -5), FORWARD), "à frente", "para onde a câmera olha")
	eq(PlayerGuide.direction_word(Vector2(0, 5), FORWARD), "atrás")
	eq(PlayerGuide.direction_word(Vector2(5, 0), FORWARD), "à direita")
	eq(PlayerGuide.direction_word(Vector2(-5, -5), FORWARD), "à frente e à esquerda")
	eq(PlayerGuide.direction_word(Vector2(5, 0), Vector2(0, 1)), "à esquerda", "virando a câmera, o lado troca")


func _hud(settings, world: String) -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("GET /players/p-1/goals?start=true", {"status": 200, "body": {"playerId": "p-1", "npcId": 7, "urgencyLine": 70,
		"goals": [], "skills": [], "start": FAMILY}})
	transport.on("GET /players/p-1/goals", {"status": 200, "body": {"playerId": "p-1", "npcId": 7, "urgencyLine": 70, "goals": [], "skills": []}})
	transport.on("GET /players/p-1/diary", {"status": 200, "body": {"entries": []}})
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.settings = settings
	hud.world_key = world
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	return [hud, transport]


func _free(made: Array) -> void:
	made[0].frame.queue_free()
	made[0].queue_free()


func test_the_guide_opens_on_the_first_possession_in_a_world_and_not_again_until_asked() -> void:
	DirAccess.remove_absolute(PATH)
	var settings = GameSettings.load_from(PATH)
	var made: Array = await _hud(settings, "Vila 1")
	var hud = made[0]
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	for i in 10:
		await tree.process_frame
	check(hud.frame.guide.visible, "primeira posse: o guia abre")
	eq(made[1].count_calls("GET /players/p-1/goals?start=true"), 1, "pede o ponto de partida ao motor, uma vez")
	check(GameSettings.load_from(PATH).has_seen_guide("guide:Vila 1"), "lembra, nas preferências locais, que viu neste mundo")
	hud.frame.guide.close()
	await hud.logout()
	await hud.incarnate(7, "Ana")
	for i in 10:
		await tree.process_frame
	check(not hud.frame.guide.visible, "segunda posse no mesmo mundo: sem guia")
	eq(made[1].count_calls("GET /players/p-1/goals?start=true"), 1, "e nem pede de novo")
	await hud.show_guide() # F1 → Rever o guia
	check(hud.frame.guide.visible, "a ajuda reabre")
	_free(made)
	var other: Array = await _hud(settings, "Vila 2")
	await other[0].incarnate(7, "Ana")
	for i in 10:
		await tree.process_frame
	check(other[0].frame.guide.visible, "outro mundo: o guia abre de novo")
	_free(other)
	DirAccess.remove_absolute(PATH)


func test_the_help_lists_what_can_be_done_with_the_engine_offer_kinds_and_the_key() -> void:
	var steps := FirstSteps.new()
	var text := ""
	for row in FirstSteps.PLAYER_ACTIONS:
		text += "%s %s\n" % [row[0], row[1]]
	for word in ["Conversar", "Comprar e vender", "Trabalhar", "Beber", "Cortar lenha", "escada", "Furtar", "espólio", "Bater", "segure E"]:
		check(word in text, "a lista cita: " + word)
	check(steps.PLAYER_ACTIONS.size() >= 10, "um tipo de oferta por linha")
	steps.free()


func test_the_card_shows_the_ground_and_how_much_it_slows_the_step_as_the_engine_says() -> void:
	eq(PlayerFrame.ground_label({"slowPct": 40, "cause": "ground", "terrain": 3, "terrainPct": 200}), "Chão pesado −40%", "chão pesado e o quanto atrasa")
	eq(PlayerFrame.ground_label({"slowPct": 0, "cause": "none", "terrain": 1, "terrainPct": 0}), "Chão firme", "sem atraso, só o chão")
	eq(PlayerFrame.ground_label({"slowPct": 30, "cause": "tired", "terrain": 1, "terrainPct": 0}), "Chão firme −30% (cansaço)", "a causa é a do motor")
	eq(PlayerFrame.ground_label({"slowPct": 10, "cause": "ground", "terrain": 2, "terrainPct": 50}), "Chão irregular −10%")
	eq(PlayerFrame.ground_label(null), "", "sem campo (dentro de prédio), nada")
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	frame.set_ground({"slowPct": 40, "cause": "ground", "terrain": 3, "terrainPct": 200})
	frame.set_motion("Andando")
	eq(frame.action_label.text, "Andando · Chão pesado −40%", "o cartão diz como e onde anda")
	frame.set_motion("")
	check(not "Chão" in frame.action_label.text, "parado, volta à ação do motor")
	frame.queue_free()


func test_the_hud_line_points_the_way_of_a_walk_step_by_the_camera() -> void:
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	frame.set_goals({"urgencyLine": 70, "goals": [{"kind": "need.hunger", "reason": "Você está com fome", "urgency": 80,
		"step": {"command": "go", "label": "Despensa de casa", "x": 10, "y": 0, "distance": 10, "here": false}}]})
	frame.set_goal_where(Vector2(0.5, 0.5), FORWARD)
	check("Despensa de casa" in frame.goal_label.text and "à direita" in frame.goal_label.text, frame.goal_label.text)
	frame.set_goal_where(Vector2(0.5, 0.5), Vector2(0, 1))
	check("à esquerda" in frame.goal_label.text, "a câmera girou: " + frame.goal_label.text)
	frame.queue_free()
