extends "res://tests/test_base.gd"
## #235: "Metas e diário" (tecla J) e a meta mais urgente na linha do HUD. O cliente só mostra o que o motor diz (metas, passos,
## habilidades, diário); aqui se confere o que ele pede, como lê o corpo da resposta e que nada é inventado do lado dele.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const GoalsPanel := preload("res://Scenes/goals_panel.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const STEP_FOOD := {"command": "interact", "kind": "stock", "targetId": 3, "verb": "take", "label": "Pegar comida na despensa",
	"x": 5, "y": 5, "distance": 0, "here": true, "resourceId": 1}
const STEP_WORK := {"command": "go", "kind": "", "targetId": 0, "verb": "", "label": "Seu posto de trabalho",
	"x": 9, "y": 5, "distance": 6, "here": false, "resourceId": -1}
const GOALS := {"playerId": "p-1", "npcId": 7, "urgencyLine": 70, "goals": [
	{"kind": "need.hunger", "reason": "Você está com fome", "urgency": 85, "step": STEP_FOOD},
	{"kind": "work.report", "reason": "Seu posto de trabalho espera por você", "urgency": 40, "step": STEP_WORK}],
	"skills": [{"skillId": 1, "name": "Ferreiro", "points": 25.0, "cap": 100.0, "level": 2, "maxLevel": 10, "toNext": 5.0,
		"fraction": 0.5, "practicing": true, "gainPerPractice": 0.1}]}
const DIARY := {"playerId": "p-1", "npcId": 7, "name": "Ana", "controlledSinceMs": 0, "entries": [
	{"atMs": 7200000, "year": 1, "month": 2, "day": 3, "hour": 14, "source": "own", "kind": "Act", "text": "Comeu",
		"significance": 0.0, "milestone": false, "whileControlled": true, "otherId": -1, "otherName": ""},
	{"atMs": 3600000, "year": 1, "month": 1, "day": 1, "hour": 8, "source": "fact", "kind": "Marriage", "text": "Dois habitantes se casaram",
		"significance": 0.8, "milestone": true, "whileControlled": false, "otherId": 9, "otherName": "Caio"}]}


func _hud(goals_reply: Dictionary = {"status": 200, "body": GOALS}) -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("GET /players/p-1/goals", goals_reply)
	transport.on("GET /players/p-1/diary", {"status": 200, "body": DIARY})
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.api.validate_shapes = true # os corpos de teste cabem nos contratos congelados
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	return [hud, transport]


func _free(made: Array) -> void:
	made[0].frame.queue_free()
	made[0].queue_free()


func _texts(panel: Control) -> String:
	var text := ""
	for child in panel.box.get_children():
		text += (String(child.text) + "\n") if child is Label else ""
	return text


func test_step_and_goal_text_come_only_from_the_engine_fields() -> void:
	eq(GoalsPanel.step_text(STEP_FOOD), "Pegar comida na despensa", "ao alcance: só o rótulo")
	eq(GoalsPanel.step_text(STEP_WORK), "Seu posto de trabalho · a 6 células", "longe: o rótulo e a distância em células")
	eq(GoalsPanel.step_text({"command": "none"}), "", "sem passo no mundo, nada")
	eq(GoalsPanel.goal_line(GOALS.goals[0]), "Você está com fome → Pegar comida na despensa", "motivo → passo")
	eq(GoalsPanel.top_line(GOALS), "Você está com fome → Pegar comida na despensa", "a linha do HUD é a primeira (a mais urgente)")
	eq(GoalsPanel.top_line({"goals": []}), "", "sem metas, a linha some")


func test_skill_and_diary_lines_show_level_what_is_missing_and_milestones() -> void:
	eq(GoalsPanel.skill_text(GOALS.skills[0]), "▲ Ferreiro  nível 2/10 · faltam 5.0 pontos", "sobe agora: ▲, nível e o que falta")
	var top: Dictionary = GOALS.skills[0].duplicate()
	top.toNext = 0.0
	top.practicing = false
	check("nível máximo" in GoalsPanel.skill_text(top), "no teto, diz que acabou")
	eq(GoalsPanel.entry_text(DIARY.entries[1]), "★ Dois habitantes se casaram", "marco de vida leva a estrela")
	eq(GoalsPanel.when_text(DIARY.entries[0]), "Ano 1 · mês 2 · dia 3 · 14h", "a data do mundo vem do motor")


func test_the_urgency_line_marks_urgent_goals() -> void:
	check(GoalsPanel.is_urgent(GOALS.goals[0], 70), "85 passa da linha do motor")
	check(not GoalsPanel.is_urgent(GOALS.goals[1], 70), "40 não")


func test_j_opens_the_panel_reads_goals_and_diary_from_the_engine_and_j_closes_it() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	var transport = made[1]
	var key := InputEventKey.new()
	key.keycode = KEY_J
	key.pressed = true
	hud._unhandled_input(key)
	check(hud.frame.goals_open(), "J abre")
	for i in 8:
		await tree.process_frame
	check(transport.count_calls("GET /players/p-1/goals") >= 1 and transport.count_calls("GET /players/p-1/diary") >= 1, "pede metas e diário ao motor")
	var text := _texts(hud.frame.goals)
	check("Você está com fome → Pegar comida na despensa" in text, "a meta e o passo (%s)" % text)
	check("nível 2/10" in text and "Ferreiro" in text, "a habilidade com o nível")
	check("★ Dois habitantes se casaram" in text and "Comeu" in text, "o diário com o marco e o que o jogador fez")
	hud._unhandled_input(key)
	check(not hud.frame.goals_open(), "a mesma tecla fecha")
	_free(made)


func test_the_most_urgent_goal_sits_discreetly_on_the_hud_line_without_opening_the_panel() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	for i in 8:
		await tree.process_frame
	eq(hud.frame.goal_label.text, "Você está com fome → Pegar comida na despensa", "a meta mais urgente na linha do HUD")
	check(not hud.frame.goals_open(), "o painel segue fechado")
	_free(made)


func test_a_failed_reply_leaves_the_hud_line_alone() -> void:
	var made: Array = await _hud({"status": 404, "body": {"error": "not_found"}})
	var hud = made[0]
	for i in 8:
		await tree.process_frame
	eq(hud.frame.goal_label.text, "", "sem resposta boa, nada inventado")
	_free(made)


func test_the_hint_line_mentions_j() -> void:
	check("J metas" in PlayerFrame.HINTS, "a dica de teclas lista o J")
