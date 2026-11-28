extends "res://tests/test_base.gd"
## #267 (3º teste de jogo): o clique vai até / interage (como o Sims, golpe no R), o aviso dos dois fora, os botões do turno, os painéis que não se cobrem, os rótulos de prédio que
## não se sobrepõem, a câmera de interior sobre a casa e o traje que o motor deu a cada pessoa. O cliente só desenha o que o motor diz.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const BuildingLabels := preload("res://Scenes/render/building_labels.gd")
const InteriorCamera := preload("res://Scenes/interior_camera.gd")
const BodyBusy := preload("res://Scenes/body_busy.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const Grid := preload("res://Scenes/grid.gd")

const JOB := {"career": "escritorio", "careerName": "Escritório", "title": "Auxiliar administrativo", "levelIndex": 0, "levels": 3, "wagePerHour": 9,
	"startHour": 9, "hours": 8, "days": [0, 1, 2, 3, 4], "score": 78, "shifts": 3, "earned": 216, "good": 2, "promoteAfter": 3, "promoteAt": 65.0, "demoteBelow": 35.0,
	"bad": 0, "demoteAfter": 3, "absences": 0, "absencesToFire": 3, "nextTitle": "Analista", "nextWagePerHour": 14, "away": false, "returnMs": 0, "workEndMs": 0,
	"arriveMs": 0, "nextShiftText": "amanhã 09:00", "canGo": false, "skillName": "Administração", "skill": 21.5}

const OFFERS := [
	{"kind": "person", "id": 5, "label": "Rafael", "verbs": ["talk"], "x": 9, "y": 7},
	{"kind": "object", "id": 4001, "label": "Geladeira", "verbs": ["use:snack"], "x": 3, "y": 2, "uses": [{"id": "snack", "name": "Beliscar", "minutes": 3}]},
	{"kind": "object", "id": 4004, "label": "Sofá", "verbs": ["use:relax"], "x": 8, "y": 6, "uses": [{"id": "relax", "name": "Relaxar", "minutes": 30}]},
]


func test_the_click_on_a_person_finds_the_engine_offer_for_that_person_and_nothing_for_who_is_far() -> void:
	var near := PlayerHud.offer_for_hit(OFFERS, {"kind": "person", "id": 5})
	eq(String(near.get("label", "")), "Rafael", "a pessoa clicada ao alcance: a roda dela")
	eq(PlayerHud.offer_for_hit(OFFERS, {"kind": "person", "id": 9}), {}, "quem não está na oferta (longe, atrás de parede): vazio, então o corpo vai até lá")


func test_the_click_on_furniture_picks_the_offer_in_the_cell_that_was_clicked() -> void:
	eq(String(PlayerHud.offer_for_hit(OFFERS, {"kind": "object", "id": "sofa", "cell": Vector2i(8, 6)}).get("label", "")), "Sofá", "o sofá pela célula")
	eq(String(PlayerHud.offer_for_hit(OFFERS, {"kind": "object", "id": "fridge", "cell": Vector2i(3, 3)}).get("label", "")), "Geladeira", "a geladeira, a uma célula do ponto")
	eq(PlayerHud.offer_for_hit(OFFERS, {"kind": "object", "id": "x", "cell": Vector2i(20, 20)}), {}, "longe de qualquer peça: nada")


func test_going_to_is_the_use_walk_phase_for_the_rest_of_the_client() -> void:
	var busy := BodyBusy.new()
	busy.note_pose({"busy": {"action": "go", "label": "Mercado", "phase": "walk", "use": "go", "totalMs": 8000, "remainingMs": 6000, "slotFx": 9.5, "slotFy": 4.5}}, 0.0, 1.0)
	eq(busy.action, "use", "o ir até é um uso na fase de andar (a barra e o WASD já o entendem)")
	eq(busy.text(0.0), "Indo: Mercado…", "a barra diz para onde vai")
	eq(busy.kind(), "use:walk", "sem gesto: o corpo anda")


func test_the_two_away_scene_names_who_comes_back_first_and_when() -> void:
	eq(PlayerHud.both_away_message(2, "Rafael", "17:40"), "Os dois estão fora — Rafael volta às 17:40", "o aviso do 3º teste")
	eq(PlayerHud.both_away_message(3, "Marina", "14:15"), "Todos estão fora — Marina volta às 14:15", "com mais gente da casa")
	var hud := PlayerHud.new()
	hud.npc_name = "Marina"
	var calls: Array = []
	hud.both_away_changed.connect(func(on: bool, text: String): calls.append([on, text]))
	var job := {"away": true, "returnMs": 800, "returnText": "14:15"}
	hud._note_both_away(true, job, [{"id": 2, "name": "Rafael", "away": true, "returnMs": 1000, "returnText": "17:40"}])
	eq(calls.size(), 1, "os dois fora: a cena")
	eq(calls[0][1], "Os dois estão fora — Marina volta às 14:15", "volta primeiro quem o motor diz (a menor hora)")
	hud._note_both_away(true, job, [{"id": 2, "name": "Rafael", "away": true, "returnMs": 1000, "returnText": "17:40"}])
	eq(calls.size(), 1, "sem repetir")
	hud._note_both_away(true, job, [{"id": 2, "name": "Rafael", "away": false, "returnMs": 0}])
	eq(calls[calls.size() - 1][0], false, "alguém em casa: a cena some")
	hud.free()


func test_the_notice_kinds_the_engine_lists_drop_the_clock_and_others_do_not() -> void:
	var hud := PlayerHud.new()
	hud.time_view = {"speeds": [1, 3, 10, 60, 300], "speed": 300}
	hud._note_career({"stopKinds": ["shift-soon", "bill-due"], "notices": []})
	eq(hud.stop_kinds, ["shift-soon", "bill-due"], "a lista vem do dado do motor")
	check(hud.shift_speed_target() == 1, "a 300× há o que descer")
	hud.free()


func test_the_turn_buttons_show_go_now_in_the_window_and_skip_only_when_alone_and_someone_is_away() -> void:
	var frame := PlayerFrame.new()
	frame.set_career({"job": JOB.merged({"canGo": true}, true), "notices": [], "bills": [], "listings": []})
	check(frame.go_now_button.visible, "janela do turno aberta: Ir agora")
	check(not frame.skip_button.visible, "ninguém fora: nada para pular")
	frame.set_speed({"canSkip": true, "speed": 1})
	frame.set_career({"job": JOB.merged({"away": true, "returnText": "17:40"}, true), "notices": [], "bills": [], "listings": []})
	check(frame.skip_button.visible and not frame.go_now_button.visible, "no trabalho e sozinho no mundo: Pular para a volta")
	frame.set_speed({"canSkip": false, "speed": 1})
	check(not frame.skip_button.visible, "com outro jogador o motor não deixa pular: o botão some")
	frame.set_both_away(true, "Os dois estão fora — Rafael volta às 17:40")
	check(frame.away_panel.visible and frame.away_label.text.contains("Rafael volta às 17:40"), "a cena clara com o aviso")
	frame.free()


func test_the_relation_panel_stacks_above_the_card_and_never_covers_it() -> void:
	var card_h := 480.0
	var top := PlayerFrame.stack_offset(card_h, 0.0)
	check(top <= -(44.0 + card_h), "acima do topo do cartão (%s)" % top)
	var with_bar := PlayerFrame.stack_offset(card_h, 36.0)
	check(with_bar < top, "e acima da barra de ação quando ela aparece")
	var screen := Rect2(0, 0, 1600, 900)
	var card := Rect2(16, screen.size.y - 44.0 - card_h, 260, card_h)
	var relation := Rect2(16, screen.size.y + with_bar - 170.0, 250, 170.0)
	check(not relation.intersects(card), "os retângulos não se tocam")
	check(screen.encloses(relation), "e o painel cabe na tela")


func test_building_labels_that_would_overlap_on_screen_hide_the_farthest_one() -> void:
	var items := [
		{"id": 1, "rect": Rect2(100, 100, 120, 24), "priority": 5.0},
		{"id": 2, "rect": Rect2(140, 104, 120, 24), "priority": 9.0}, # cobre o 1: some (mais longe)
		{"id": 3, "rect": Rect2(400, 100, 80, 24), "priority": 7.0},
	]
	var hidden := BuildingLabels.hidden_ids(items)
	check(hidden.has(2) and not hidden.has(1) and not hidden.has(3), "fica o mais perto; o que o cobriria some; o solto fica")
	items.reverse()
	eq(BuildingLabels.hidden_ids(items), hidden, "a ordem de entrada não muda o resultado")


func test_the_interior_camera_never_leaves_the_house_to_the_side() -> void:
	var box := AABB(Vector3(0, 0, 0), Vector3(10, 3, 9))
	var eye := Vector3(9.5, 1.5, 4.5) # o corpo junto da parede da direita
	var back := Vector3(1, 0.4, 0).normalized() # a câmera iria para fora, de lado
	var arm := InteriorCamera.keep_in_house(eye, back, 12.0, box, 0.5)
	var spot := eye + back * arm
	var limit := box.grow(Grid.units(InteriorCamera.OUTSIDE_MARGIN_M))
	check(spot.x <= limit.end.x + 0.01, "a câmera fica sobre a casa (x=%s, limite %s)" % [spot.x, limit.end.x])
	check(arm >= 0.5, "nunca menos que o braço mínimo")
	var inside := Vector3(-1, 0.4, 0.2).normalized()
	eq(InteriorCamera.keep_in_house(Vector3(5, 1.5, 4.5), inside, 3.0, box, 0.5), 3.0, "uma câmera que já está dentro não muda")


func test_each_person_gets_the_hat_of_the_outfit_the_engine_gave_not_the_one_of_the_trade() -> void:
	var marina := NpcActor.new()
	marina.look = {"skin_tone": 30, "hair_tone": 12, "hair_curl": 60, "build": 34, "outfit": 2}
	var rafael := NpcActor.new()
	rafael.look = {"skin_tone": 64, "hair_tone": 36, "hair_curl": 14, "build": 66, "outfit": 5}
	var hats := [marina.hat_for("straw"), rafael.hat_for("straw")]
	check(hats[0] != hats[1], "dois trajes, dois chapéus (o do ofício era o mesmo para os dois)")
	eq(NpcActor.new().hat_for("straw"), "straw", "sem traje do motor, vale o do ofício")
	marina.free()
	rafael.free()
