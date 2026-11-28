extends "res://tests/test_base.gd"
## #255 (S2 do sandbox): necessidades e usos de objeto como o motor diz. As barras do catálogo e a mais urgente destacada, a dica "bexiga cheia → Vaso",
## a roda com um item por objeto (usos num submenu) e paginada, a barra de progresso do uso, a fila visível e o gesto vindo da pose do catálogo.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const BodyBusy := preload("res://Scenes/body_busy.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const SHEET := {"health": 90, "hunger": 80, "thirst": 70, "sleep": 60, "social": 50,
	"catalogNeeds": [{"id": "hygiene", "name": "Higiene", "value": 20, "urgent": true, "color": "5fc7c0"},
		{"id": "fun", "name": "Diversão", "value": 70, "urgent": false, "color": "e07fb4"},
		{"id": "bladder", "name": "Bexiga", "value": 8, "urgent": true, "color": "d9c34a"}],
	"needHint": {"need": "bladder", "alert": "Você está com a bexiga cheia", "object": "Vaso", "use": "Usar o vaso"}}

const SHOWER := {"kind": "object", "id": 8, "label": "Chuveiro", "verbs": ["use:shower"], "x": 16, "y": 14, "resources": [],
	"uses": [{"id": "shower", "name": "Tomar banho", "minutes": 15, "need": "hygiene", "busy": false}]}
const SOFA := {"kind": "object", "id": 6, "label": "Sofá", "verbs": ["use:relax", "use:chat"], "x": 14, "y": 18, "resources": [],
	"uses": [{"id": "relax", "name": "Sentar e relaxar", "minutes": 20, "need": "fun", "busy": false},
		{"id": "chat", "name": "Sentar para conversar", "minutes": 20, "need": "social", "busy": true}]}


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
	await hud.incarnate(7, "Ana")
	hud.set_rpg(true)
	return [hud, transport]


func _free(made: Array) -> void:
	made[0].frame.queue_free()
	made[0].queue_free()


func test_the_card_draws_one_bar_per_catalog_need_with_the_most_urgent_highlighted_and_the_hint() -> void:
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	frame.set_body("Ana", "Parada", SHEET)
	eq(frame.catalog_bars.keys().size(), 3, "uma barra por necessidade do catálogo, criada pelo que o motor diz")
	eq(float(frame.catalog_bars.bladder.bar.value), 8.0, "o nível vem do motor")
	eq(PlayerFrame.most_urgent(SHEET.catalogNeeds), "bladder", "a mais urgente é a de menor nível entre as que o motor marca urgentes")
	check(String(frame.catalog_bars.bladder.name.text).begins_with("▶"), "a mais urgente vem destacada")
	check(not String(frame.catalog_bars.hygiene.name.text).begins_with("▶"), "só uma é destacada")
	eq(frame.need_hint.text, "Você está com a bexiga cheia → Vaso", "o alerta é do cenário e o objeto, do catálogo")
	frame.set_body("Ana", "Parada", {"health": 90, "catalogNeeds": []})
	eq(frame.catalog_bars.keys().size(), 0, "sem catálogo, sem barras extras")
	check(not frame.need_hint.visible, "e sem dica")
	frame.queue_free()


func test_the_wheel_has_one_entry_per_object_with_the_uses_in_a_submenu() -> void:
	var entries := PlayerHud.menu_entries([SHOWER, SOFA])
	eq(entries.map(func(e): return String(e.text)), ["Chuveiro ▸", "Sofá ▸"], "um item por objeto, nenhum uso solto na roda")
	eq(String(entries[1].verb), "menu", "escolher o objeto abre os usos")
	var uses := PlayerHud.use_entries(entries[1], false)
	eq(uses.map(func(e): return String(e.text)), ["Sentar e relaxar · 20 min", "Sentar para conversar · 20 min (ocupado)"], "o uso ocupado por outra pessoa vem marcado")
	eq(uses.map(func(e): return String(e.verb)), ["use:relax", "use:chat"], "o verbo é o uso do catálogo")
	var queued := PlayerHud.use_entries(entries[0], true)
	eq(String(queued[0].text), "Fila: Tomar banho · 15 min", "com o corpo ocupado, escolher enfileira")
	eq(String(queued[0].verb), "queue:shower", "o verbo vira o de fila")


func test_the_wheel_pages_so_no_object_disappears() -> void:
	var all: Array = []
	for i in 12:
		all.append({"text": "Objeto %d" % i, "kind": "object", "id": i, "verb": "menu", "resource": -1})
	var first := PlayerFrame.page_of(all, 0)
	eq(first.size(), PlayerFrame.MENU_MAX, "uma página cabe na roda")
	eq(String(first[-1].verb), "more", "a última linha pagina")
	var second := PlayerFrame.page_of(all, 1)
	eq(String(second[0].text), "Objeto 7", "a segunda página continua de onde a primeira parou")
	eq(String(PlayerFrame.page_of(all, 2)[0].text), "Objeto 0", "ao fim volta ao começo")
	var seen := {}
	for page in 3:
		for entry in PlayerFrame.page_of(all, page):
			if String(entry.verb) != "more":
				seen[int(entry.id)] = true
	eq(seen.size(), 12, "todos os objetos aparecem em alguma página")
	eq(PlayerFrame.page_of(all.slice(0, 5), 0).size(), 5, "cabendo, não pagina")


func test_picking_the_more_entry_turns_the_page_and_number_keys_follow_it() -> void:
	var all: Array = []
	for i in 12:
		all.append({"text": "Objeto %d" % i, "kind": "object", "id": i, "verb": "menu", "resource": -1})
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	frame.show_menu(all)
	eq(frame.entries.size(), PlayerFrame.MENU_MAX, "a roda mostra a primeira página")
	frame.choose(frame.entries[-1])
	eq(String(frame.entries[0].text), "Objeto 7", "Mais ▸ vira a página")
	eq(int(frame.entry_for_key(KEY_1).id), 7, "a tecla 1 escolhe a primeira linha da página mostrada")
	frame.queue_free()


func test_the_busy_bar_names_the_use_walking_first_then_doing_and_the_pose_picks_the_gesture() -> void:
	var busy := BodyBusy.new()
	var walking := {"busy": {"action": "use", "untilMs": 9000, "totalMs": 8000, "remainingMs": 6000, "use": "shower", "label": "Tomar banho", "phase": "walk",
		"pose": "work", "slotFx": 16.5, "slotFy": 14.5, "facing": 180.0}}
	check(busy.note_pose(walking, 0.0, 1.0), "começou")
	eq(busy.text(0.0), "Indo: Tomar banho…", "andando até a peça, a barra diz para onde")
	eq(busy.kind(), "use:walk", "sem gesto enquanto anda")
	eq(BodyBusy.gesture_for(busy.kind()), "", "o corpo anda, não gesticula")
	var doing := walking.duplicate(true)
	doing.busy.phase = "do"
	doing.busy.totalMs = 900000
	doing.busy.remainingMs = 450000
	check(busy.note_pose(doing, 1000.0, 1.0), "a fase mudou: o app reanima")
	eq(busy.text(1000.0), "Tomar banho — 50% · faltam 8 min", "a barra mostra o progresso do uso")
	eq(busy.kind(), "use:work", "a pose do catálogo")
	eq(BodyBusy.gesture_for("use:work"), "work", "a pose vira o gesto do rig que já existe")
	eq(BodyBusy.gesture_for("use:shower"), "rest", "pose sem gesto cai no mais próximo")
	eq(busy.stopped_text(), "Parou: Tomar banho.", "o nome é o do catálogo")
	eq(busy.slot, Vector2(16.5, 14.5), "o lugar de uso vem do motor")


func test_the_queue_is_shown_and_choosing_a_use_while_busy_sends_the_queue_verb() -> void:
	var made: Array = await _hud({"POST /players/p-1/interact": {"status": 200, "body": {"kind": "object", "id": 8, "verb": "queue:shower", "quantity": 0, "value": 0,
		"pose": {"playerId": "p-1", "npcId": 7, "x": 3, "y": 4, "readyInMs": 0, "fx": 3.5, "fy": 4.5, "speedMps": 1.4, "cellMeters": 1.5, "running": false,
			"busy": {"action": "use", "untilMs": 900000, "totalMs": 900000, "remainingMs": 800000, "use": "watch", "label": "Assistir TV", "phase": "do", "pose": "idle"},
			"queue": [{"use": "shower", "label": "Tomar banho", "object": "Chuveiro"}, {"use": "snack", "label": "Pegar um lanche", "object": "Geladeira"}]}}}})
	var hud = made[0]
	var transport = made[1]
	hud.apply_pose({"playerId": "p-1", "npcId": 7, "x": 3, "y": 4, "readyInMs": 0, "fx": 3.5, "fy": 4.5, "speedMps": 1.4, "cellMeters": 1.5, "running": false,
		"busy": {"action": "use", "untilMs": 900000, "totalMs": 900000, "remainingMs": 800000, "use": "watch", "label": "Assistir TV", "phase": "do", "pose": "idle"}})
	check(hud.busy.active(), "o corpo está usando a TV")
	await hud.interact({"text": "Tomar banho · 15 min", "kind": "object", "id": 8, "verb": "use:shower", "resource": -1})
	var sent: String = str(transport.bodies.get("POST /players/p-1/interact", []).back())
	check(sent.contains("queue:shower"), "ocupado, o uso entra na fila em vez de pedir de novo")
	eq(hud.frame.queue_size(), 2, "a fila do motor aparece no cartão")
	eq(hud.frame.queue_label.text, "Fila: 1 Tomar banho · 2 Pegar um lanche\n(Shift+1–2 tira da fila)", "a fila numerada, com a tecla que cancela")
	_free(made)


func test_cancelling_a_queued_action_asks_the_engine_and_shows_the_new_queue() -> void:
	var made: Array = await _hud({"POST /players/p-1/interact": {"status": 200, "body": {"kind": "queue", "id": 0, "verb": "cancel", "quantity": 0, "value": 0,
		"pose": {"playerId": "p-1", "npcId": 7, "x": 3, "y": 4, "readyInMs": 0, "fx": 3.5, "fy": 4.5, "speedMps": 1.4, "cellMeters": 1.5, "running": false,
			"queue": [{"use": "snack", "label": "Pegar um lanche", "object": "Geladeira"}]}}}})
	var hud = made[0]
	await hud.cancel_queued(0)
	eq(hud.frame.queue_size(), 1, "sobrou uma")
	check(hud.frame.queue_label.text.begins_with("Fila: 1 Pegar um lanche"), "a que sobrou")
	_free(made)
