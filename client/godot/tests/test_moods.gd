extends "res://tests/test_base.gd"
## #262: humor, momentos e desejos no cartão do jogador. O cliente só desenha o que o motor diz (`mood` da ficha e das metas): a emoção dominante com o rosto,
## a lista de momentos com causa e tempo restante, o painel de desejos/aspiração (tecla J) e o aviso ao cumprir um desejo.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const GoalsPanel := preload("res://Scenes/goals_panel.gd")
const NeedIcon := preload("res://Scenes/need_icon.gd")

const MOOD := {"score": 71, "label": "Bem", "emotion": "happy", "emotionName": "Feliz", "icon": "joy", "face": "joy", "color": "f2c94c",
	"moments": [
		{"id": "rested", "name": "Bem descansado", "emotion": "happy", "weight": 2.0, "count": 1, "remainingMs": 10_800_000, "cause": null},
		{"id": "quarrel", "name": "Brigou com Rafael", "emotion": "angry", "weight": -6.0, "count": 2, "remainingMs": 2_400_000, "cause": "Rafael"},
		{"id": "smelly", "name": "Cheira mal", "emotion": "embarrassed", "weight": -2.0, "count": 1, "remainingMs": null, "cause": null}],
	"aspiration": "knowledge", "aspirationName": "Conhecimento", "points": 20, "done": 2, "nextMilestone": "Mente curiosa", "nextMilestonePoints": 30,
	"wishes": [{"id": "hug", "text": "Abraçar Rafael", "other": 2, "sinceMs": 1000, "expiresMs": 99999, "points": 10},
		{"id": "shower", "text": "Tomar banho", "other": -1, "sinceMs": 1000, "expiresMs": 99999, "points": 10}],
	"recent": [{"id": "cook", "text": "Cozinhar uma refeição", "atMs": 5000, "points": 10, "milestone": false}]}


func _frame() -> Control:
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	return frame


func test_moments_read_as_cause_weight_and_time_left() -> void:
	var lines := PlayerFrame.moment_lines(MOOD.moments)
	eq(lines.map(func(l): return String(l.text)), ["Bem descansado +2 · 3 h", "Brigou com Rafael ×2 −6 · 40 min", "Cheira mal −2"],
		"nome do motor (já com a causa), peso e quanto falta; sem prazo, só o peso")
	check(bool(lines[0].good) and not bool(lines[1].good), "positivo verde, negativo vermelho")
	eq(PlayerFrame.remaining_text(null), "", "sem prazo: dura enquanto a condição valer")
	eq(PlayerFrame.remaining_text(90_000), "2 min", "arredonda para cima")
	eq(PlayerFrame.remaining_text(86_400_000 * 3), "3 d", "dias")
	var many: Array = []
	for i in 7:
		many.append({"name": "M%d" % i, "weight": 1.0, "count": 1, "remainingMs": 60_000})
	var shown := PlayerFrame.moment_lines(many)
	eq(shown.size(), PlayerFrame.MOMENTS_SHOWN + 1, "o resto vira uma linha de resumo")
	eq(String(shown[-1].text), "+2 momentos", "quantos ficaram de fora")


func test_the_card_shows_the_engine_emotion_with_the_action_instead_of_a_bare_idle() -> void:
	var frame := _frame()
	frame.set_body("Marina", "Parado", {"health": 90, "hunger": 80, "thirst": 80, "sleep": 70, "mood": MOOD})
	check(frame.mood_row.visible, "a linha do humor aparece quando o motor manda o humor")
	eq(frame.mood_label.text, "Feliz · Parado", "o 'Parado' vira humor + ação")
	check(not frame.action_label.visible, "a ação sozinha some: ela está na linha do humor")
	eq(frame.mood_icon.kind, "joy", "o ícone é o do catálogo")
	check(NeedIcon.is_face("joy") and NeedIcon.is_face("anger") and not NeedIcon.is_face("food"), "rostos feitos por código")
	eq(frame.moments_box.get_child_count(), 3, "um rótulo por momento")
	eq(String(frame.moments_box.get_child(0).text), "Bem descansado +2 · 3 h")

	frame.set_busy("Tomando banho — 10%", 0.1)
	eq(frame.mood_label.text, "Feliz · Tomando banho", "o que o corpo faz acompanha o humor")

	frame.set_mood(null)
	check(not frame.mood_row.visible and not frame.moments_box.visible, "sem humor do motor (cenário sem catálogo), o cartão é o de antes")
	check(frame.action_label.visible, "volta a ação sozinha")
	frame.queue_free()


func test_the_goals_panel_lists_aspiration_wishes_and_what_was_done_without_repeating_them_as_goals() -> void:
	eq(GoalsPanel.aspiration_text(MOOD), "Aspiração: Conhecimento · 20 pontos · próximo marco: Mente curiosa (30)")
	eq(GoalsPanel.wish_line(MOOD.wishes[0]), "Abraçar Rafael (+10)")
	eq(GoalsPanel.done_line(MOOD.recent[0]), "Cumprido: Cozinhar uma refeição +10")
	eq(GoalsPanel.done_line({"text": "Mente curiosa", "milestone": true}), "Marco: Mente curiosa")
	var panel := GoalsPanel.new()
	tree.root.add_child(panel)
	panel.set_goals({"goals": [{"kind": "need.hunger", "reason": "Você está com fome", "urgency": 80, "step": null},
		{"kind": "wish.hug", "reason": "Abraçar Rafael", "urgency": 25, "step": null}], "skills": [], "mood": MOOD})
	var text := ""
	for child in panel.box.get_children():
		text += (String(child.text) + "\n") if child is Label else ""
	check("Desejos" in text and "★ Abraçar Rafael (+10)" in text and "★ Tomar banho (+10)" in text, "os desejos do motor, com a recompensa")
	check("Aspiração: Conhecimento" in text and "✓ Cumprido: Cozinhar uma refeição +10" in text, "aspiração e o último cumprido")
	eq(text.count("Abraçar Rafael"), 1, "o desejo não repete na lista de metas")
	check("Você está com fome" in text, "as metas de verdade continuam")
	panel.queue_free()


func test_a_fulfilled_wish_or_milestone_warns_once_and_the_first_read_only_learns() -> void:
	var hud := PlayerHud.new()
	var seen: Array = []
	hud.warning.connect(func(text: String): seen.append(text))
	hud._note_mood({"recent": [{"text": "Cozinhar uma refeição", "atMs": 5000, "points": 10, "milestone": false}]})
	eq(seen, [], "a primeira leitura só aprende o que já existia")
	hud._note_mood({"recent": [{"text": "Cozinhar uma refeição", "atMs": 5000, "points": 10, "milestone": false}]})
	eq(seen, [], "o mesmo instante não avisa de novo")
	hud._note_mood({"recent": [{"text": "Mente curiosa", "atMs": 9000, "points": 0, "milestone": true},
		{"text": "Abraçar Rafael", "atMs": 8000, "points": 10, "milestone": false},
		{"text": "Cozinhar uma refeição", "atMs": 5000, "points": 10, "milestone": false}]})
	eq(seen, ["Desejo cumprido: Abraçar Rafael (+10 pontos)", "Marco alcançado: Mente curiosa"], "do mais antigo ao mais novo, cada um uma vez")
	hud.free()
