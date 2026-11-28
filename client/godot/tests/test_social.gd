extends "res://tests/test_base.gd"
## #259 (S3 do sandbox): o menu social e a relação visível como o motor diz. A roda do E sobre a pessoa ganha "Interagir ▸" (categorias; as interações que
## não valem agora esmaecidas com o motivo), o painel de relação mostra afeição, confiança, estado, humor e o último fato, e o fato novo vira um aviso curto.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const SocialMenu := preload("res://Scenes/social_menu.gd")
const RelationPanel := preload("res://Scenes/relation_panel.gd")
const BodyBusy := preload("res://Scenes/body_busy.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const SOCIAL := [
	{"id": "chat", "name": "Bater papo", "category": "friendly", "categoryName": "Amigável", "available": true, "seconds": 40},
	{"id": "hug", "name": "Abraçar", "category": "friendly", "categoryName": "Amigável", "available": true, "seconds": 8},
	{"id": "kiss", "name": "Beijar", "category": "romantic", "categoryName": "Romântica", "available": false, "seconds": 6,
		"reason": "Marina ainda não confia em você o bastante"},
	{"id": "tv-together", "name": "Convidar para ver TV", "category": "invite", "categoryName": "Convite", "available": true, "seconds": 1800}]

const MARINA := {"kind": "person", "id": 9, "label": "Marina", "verbs": ["talk"], "x": 5, "y": 5, "resources": [], "social": SOCIAL,
	"relation": {"state": "spouse", "stateLabel": "Casados", "affection": 72, "trust": 62, "mineAffection": 70, "mineTrust": 60, "mood": "Bem", "moodScore": 64,
		"last": "Marina gostou do elogio (+5)", "lastMs": 600000}}


func _hud(replies: Dictionary = {}) -> Array:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	for route in replies:
		transport.on(route, replies[route])
	var hud := PlayerHud.new()
	hud.api = ApiClient.new(transport)
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	hud.frame = PlayerFrame.new()
	tree.root.add_child(hud)
	tree.root.add_child(hud.frame)
	await hud.incarnate(7, "Rafael")
	hud.set_rpg(true)
	return [hud, transport]


func _free(made: Array) -> void:
	made[0].frame.queue_free()
	made[0].queue_free()


func test_the_person_gets_talk_and_an_interact_line_and_the_categories_come_from_the_engine() -> void:
	var entries := PlayerHud.menu_entries([MARINA])
	eq(entries.map(func(e): return String(e.text)), ["Marina: Interagir ▸"], "#261: o Conversar de texto é o Bater papo do menu social: uma entrada só")
	var cats := SocialMenu.category_entries(entries[0])
	eq(cats.map(func(e): return String(e.text)), ["Amigável (2/2) ▸", "Romântica (0/1) ▸", "Convite (1/1) ▸", "‹ Voltar"], "as categorias e quantas valem agora")
	check(bool(cats[1].dim), "a categoria sem nenhuma interação disponível vem esmaecida")
	check(entries.size() <= PlayerFrame.MENU_MAX, "não quebra a paginação da roda")


func test_unavailable_interactions_are_dimmed_with_the_reason_and_the_duration_is_in_words() -> void:
	var main := SocialMenu.main_entry(MARINA, "Marina")
	var kiss := SocialMenu.interaction_entries(SocialMenu.category_entries(main)[1])
	eq(kiss[0].text, "Beijar · 6 s", "o nome e a duração")
	check(bool(kiss[0].disabled) and bool(kiss[0].dim), "não vale: desabilitada e esmaecida")
	eq(kiss[0].reason, "Marina ainda não confia em você o bastante", "o motivo é o do motor")
	eq(kiss[1].verb, "social_back", "a volta")
	var friendly := SocialMenu.interaction_entries(SocialMenu.category_entries(main)[0])
	eq(friendly.map(func(e): return String(e.verb)), ["social:chat", "social:hug", "social_back"], "o verbo é a interação do catálogo")
	check(not bool(friendly[0].disabled), "disponível")
	eq(SocialMenu.duration_text(1800), "30 min", "a atividade longa em minutos")


func test_choosing_interact_opens_the_categories_and_a_dimmed_line_says_why_without_asking_the_engine() -> void:
	var made: Array = await _hud({"GET /players/p-1/reach": {"status": 200, "body": {"offers": [MARINA]}}})
	var hud = made[0]
	var transport = made[1]
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	hud.fine = Vector2(5.5, 5.5)
	await hud.open_interactions()
	check(hud.frame.menu_open(), "E abre a roda")
	var main: Dictionary = hud.frame.entries.filter(func(e): return String(e.verb) == "social_menu")[0]
	hud.interact(main)
	eq(hud.frame.entries.map(func(e): return String(e.text)).slice(0, 3), ["Amigável (2/2) ▸", "Romântica (0/1) ▸", "Convite (1/1) ▸"], "a segunda roda")
	hud.interact(hud.frame.entries[1])
	check(hud.frame.entries[0].disabled, "a interação indisponível aparece")
	var before: int = transport.requests.size() if "requests" in transport else 0
	hud.interact(hud.frame.entries[0])
	eq(warnings.back(), "Marina ainda não confia em você o bastante", "tocar na esmaecida diz o motivo")
	eq(transport.requests.size() if "requests" in transport else 0, before, "nada foi pedido ao motor")
	hud.interact(hud.frame.entries[hud.frame.entries.size() - 1]) # ‹ Voltar
	eq(hud.frame.entries.map(func(e): return String(e.text)).slice(0, 3), ["Amigável (2/2) ▸", "Romântica (0/1) ▸", "Convite (1/1) ▸"], "volta às categorias")
	_free(made)


func test_a_refusal_shows_the_engine_sentence_and_an_accepted_one_the_catalog_name() -> void:
	var made: Array = await _hud({"POST /players/p-1/interact": {"status": 200, "body": {"kind": "person", "id": 9, "verb": "social:kiss", "quantity": 0, "value": 0,
		"outcome": "refused", "note": "Marina recusou o beijo: Rafael está precisando de um banho antes", "pose": {"x": 5, "y": 5, "npcId": 7, "playerId": "p-1"}}}})
	var hud = made[0]
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	await hud.interact({"kind": "person", "id": 9, "verb": "social:kiss", "resource": -1, "text": "Beijar · 6 s"})
	eq(warnings.back(), "Marina recusou o beijo: Rafael está precisando de um banho antes", "a recusa vem com o motivo")
	eq(PlayerHud.done_text({"text": "Elogiar · 6 s"}, {"outcome": "done", "note": "Elogiar", "quantity": 0}), "✓ Elogiar", "aceita: o nome do catálogo")
	_free(made)


func test_the_relation_panel_shows_state_mood_bars_and_the_last_fact_and_hides_without_a_person() -> void:
	var data := RelationPanel.lines(MARINA, 660000)
	eq(data.title, "Marina", "o nome")
	eq(data.state, "Casados", "o estado do par")
	eq(data.mood, "Humor: Bem", "o humor dela")
	eq([data.affection, data.trust], [72, 62], "afeição e confiança dela por quem joga")
	eq(data.last, "Marina gostou do elogio (+5) · há 60 s", "o último fato, com o tempo")
	check(RelationPanel.lines({"kind": "stall", "id": 3}).is_empty(), "só pessoa com relação")
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	frame.set_relation(MARINA, 660000)
	check(frame.relation.visible, "a relação aparece sob a mira")
	eq(frame.relation.bars.affection.value, 72.0, "a barra de afeição")
	eq(frame.relation.state_label.text, "Casados", "o estado")
	frame.set_relation({}, 0)
	check(not frame.relation.visible, "sem pessoa, some")
	frame.queue_free()


func test_a_new_social_fact_becomes_one_short_notice_and_the_first_read_only_learns() -> void:
	var made: Array = await _hud()
	var hud = made[0]
	var warnings: Array = []
	hud.warning.connect(func(t: String): warnings.append(t))
	hud._note_social({"text": "Marina e Rafael discutiram (-6)", "atMs": 1000, "with": 9, "delta": -6, "accepted": true})
	eq(warnings.size(), 0, "o que já havia não é avisado")
	hud._note_social({"text": "Marina gostou do elogio (+5)", "atMs": 5000, "with": 9, "delta": 5, "accepted": true})
	eq(warnings, ["Marina gostou do elogio (+5)"], "o fato novo vira aviso")
	hud._note_social({"text": "Marina gostou do elogio (+5)", "atMs": 5000, "with": 9, "delta": 5, "accepted": true})
	eq(warnings.size(), 1, "uma vez por instante")
	_free(made)


func test_the_busy_bar_names_the_social_interaction_and_the_gesture_comes_from_the_catalog_pose() -> void:
	var busy := BodyBusy.new()
	var walking := {"busy": {"action": "social", "untilMs": 9000, "totalMs": 8000, "remainingMs": 6000, "use": "hug", "label": "Abraçar", "phase": "walk",
		"pose": "hug", "with": 9, "partnerPose": "hug", "initiator": true}}
	check(busy.note_pose(walking, 0.0, 1.0), "começou")
	eq(busy.text(0.0), "Indo: Abraçar…", "andando até a pessoa")
	eq(busy.kind(), "social:walk", "sem gesto enquanto anda")
	eq(busy.partner, 9, "o outro corpo vem do motor")
	var doing := walking.duplicate(true)
	doing.busy.phase = "do"
	doing.busy.totalMs = 8000
	doing.busy.remainingMs = 4000
	check(busy.note_pose(doing, 1000.0, 1.0), "a fase mudou")
	eq(busy.text(1000.0), "Abraçar — 50%", "o progresso")
	eq(busy.kind(), "social:hug", "o gesto é o da pose do catálogo")
	eq(BodyBusy.gesture_for("social:hug"), "hug", "gesto simples novo")
	eq(BodyBusy.gesture_for("social:plead"), "plead", "outro gesto novo")
	eq(BodyBusy.gesture_for("social:nada-disso"), "talk", "pose desconhecida cai em conversar")
	eq(busy.stopped_text(), "Parou: Abraçar.", "interromper diz o nome")
