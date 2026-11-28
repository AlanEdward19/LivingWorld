extends "res://tests/test_base.gd"
## #268 (4º teste de jogo): as vagas na roda do jornal, a faixa de tempo pausado, o livre-arbítrio no HUD, a entrega com hora, o "Chamar" de quem está num móvel, o realce do corpo conduzido
## visto de cima e a câmera de rua. O cliente só desenha o que o motor diz: texto, hora e decisão vêm dele.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const SocialMenu := preload("res://Scenes/social_menu.gd")
const BuyMode := preload("res://Scenes/buy_mode.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")

const LISTINGS := [
	{"career": "escritorio", "careerName": "Escritório", "employer": 7, "title": "Auxiliar administrativo", "wagePerHour": 9, "startHour": 9, "hours": 8, "free": 2, "chance": 0.62, "commuteMin": 40},
	{"career": "ensino", "careerName": "Escola", "employer": 9, "title": "Monitor escolar", "wagePerHour": 8, "startHour": 8, "hours": 6, "free": 1, "chance": 0.6, "commuteMin": 15},
]


func test_reading_the_paper_lists_the_vacancies_in_the_wheel_with_apply_and_the_way_to_the_employer() -> void:
	var entries := PlayerHud.vacancy_entries(LISTINGS, 3)
	eq(entries.size(), 2, "uma linha por vaga do motor")
	eq(String(entries[0].text), "Candidatar-se: Auxiliar administrativo · Escritório, 9/h, 40 min de ida · chance 62%", "o cargo, o empregador, o salário, a ida e a chance")
	eq(String(entries[0].verb), "apply:escritorio@7", "o verbo do motor: a carreira e o empregador")
	eq([String(entries[0].kind), int(entries[0].id)], ["job", 3], "é a oferta do emprego do corpo")
	eq(PlayerHud.vacancy_entries([], 3), [], "sem vaga, sem roda")


func test_the_apply_answers_are_told_with_the_engine_sentence() -> void:
	var entry := {"text": "Candidatar-se: Auxiliar", "kind": "job", "id": 3, "verb": "apply:escritorio@7"}
	eq(PlayerHud.done_text(entry, {"outcome": "going", "note": "Indo ao Escritório para se candidatar a Auxiliar administrativo."}), "Indo ao Escritório para se candidatar a Auxiliar administrativo.", "o corpo vai até o empregador")
	eq(PlayerHud.done_text(entry, {"outcome": "later", "note": "Escritório está fechado: entrevista marcada amanhã às 08:00."}), "Escritório está fechado: entrevista marcada amanhã às 08:00.", "entrevista com hora")
	eq(PlayerHud.done_text(entry, {"outcome": "done", "note": "Contratado: Auxiliar administrativo."}), "✓ Contratado: Auxiliar administrativo.", "contratado")


func test_the_search_notice_opens_the_vacancies_once_and_only_after_the_first_reading() -> void:
	var hud := PlayerHud.new()
	var shown: Array = []
	hud._show_vacancies_hook = func(list: Array): shown.append(list.size())
	var view := {"stopKinds": [], "notices": [{"id": 1, "kind": "search", "text": "2 vaga(s)"}], "listings": LISTINGS, "job": null}
	hud._note_career(view)
	eq(shown, [], "a primeira leitura aprende sem abrir o passado")
	view = {"stopKinds": [], "notices": [{"id": 1, "kind": "search", "text": "x"}, {"id": 2, "kind": "search", "text": "2 vaga(s)"}], "listings": LISTINGS, "job": null}
	hud._note_career(view)
	eq(shown, [2], "o aviso novo do jornal abre as vagas")
	hud._note_career(view)
	eq(shown, [2], "sem repetir")
	hud.free()


func test_the_pause_band_says_the_world_is_paused_while_the_wheel_or_a_window_is_open() -> void:
	var frame := PlayerFrame.new()
	check(not frame.pause_band.visible, "sem popup, sem faixa")
	frame.menu_panel.visible = true
	frame.blocked = true
	check(frame.pause_band.visible, "a roda aberta pausa o mundo: a faixa aparece")
	eq(frame.pause_label.text, "⏸ Tempo pausado (menu aberto)", "diz o porquê")
	frame.menu_panel.visible = false
	frame.blocked = true
	eq(frame.pause_label.text, "⏸ Tempo pausado (janela aberta)", "outro popup")
	frame.blocked = false
	check(not frame.pause_band.visible, "ao fechar, a faixa some")
	frame.free()


func test_free_will_is_a_toggle_in_the_card_that_the_engine_confirms() -> void:
	var frame := PlayerFrame.new()
	check(not frame.will_label.visible, "sem necessidade crítica no cenário, a linha não existe")
	frame.set_free_will(true)
	check(frame.will_label.visible and frame.will_label.text.begins_with("U · Livre-arbítrio: ligado"), "ligado: U desliga")
	frame.set_free_will(false)
	check(frame.will_label.text.contains("desligado"), "desligado")
	frame.set_free_will(null)
	check(not frame.will_label.visible, "nulo esconde")
	frame.free()


func test_the_critical_need_notice_drops_the_clock_like_the_shift_one() -> void:
	var hud := PlayerHud.new()
	hud.time_view = {"speeds": [1, 3, 10, 60, 300], "speed": 300}
	hud._note_career({"stopKinds": ["shift-soon", "need-critical"], "notices": [], "freeWill": true})
	eq(hud.free_will, true, "o motor diz se o livre-arbítrio está ligado")
	check(hud.shift_speed_target() == 1, "a 300× o aviso crítico tem para onde descer")
	hud.free()


func test_the_courier_arrival_is_the_engine_clock_hour_not_minutes() -> void:
	eq(BuyMode.parcel_status({"where": "courier", "courier": "Seu Tomás", "dueInMs": 47280000, "dueText": "amanhã às 08:12"}), "Seu Tomás está trazendo: chega amanhã às 08:12", "a hora do jogo")
	eq(BuyMode.parcel_status({"where": "courier", "courier": "Seu Tomás", "dueInMs": 120000}), "Seu Tomás está trazendo (2 min)", "sem a hora do motor, o de antes")
	eq(BuyMode.parcel_status({"where": "shop", "readyInMs": 3600000, "readyText": "hoje às 15:00"}), "a loja está fazendo: pronto hoje às 15:00", "o preparo também diz a hora")


func test_a_person_busy_with_a_piece_can_be_called_or_waited_for() -> void:
	var options := [
		{"id": "hug", "name": "Abraçar", "category": "friendly", "categoryName": "Amigável", "available": false, "reason": "Rafael está ocupado com um móvel", "seconds": 8, "canCall": true, "freeAtText": "hoje às 08:12"},
		{"id": "chat", "name": "Bater papo", "category": "friendly", "categoryName": "Amigável", "available": true, "seconds": 40},
	]
	var list := SocialMenu.interaction_entries({"id": 5, "cat": "friendly", "social": options, "title": "Rafael"})
	var verbs := list.map(func(e): return String(e.verb))
	check("call" in verbs and "wait_use" in verbs, "Chamar e Esperar terminar entram na roda (%s)" % [verbs])
	eq(String(list[verbs.find("wait_use")].text), "Esperar terminar (hoje às 08:12)", "a hora que o outro termina é do motor")
	eq(String(list[verbs.find("call")].text), "Chamar Rafael (pedir que levante)", "chamar")
	var free := SocialMenu.interaction_entries({"id": 5, "cat": "friendly", "social": [options[1]], "title": "Rafael"}).map(func(e): return String(e.verb))
	check("call" not in free and "wait_use" not in free, "quem pode conversar não precisa chamar")


func test_the_body_the_player_drives_gets_a_soft_halo_from_above_that_grows_with_the_camera_distance() -> void:
	var actor := NpcActor.new()
	actor.set_self(true)
	actor.set_legible(true, 2.0)
	check(actor.legible and actor._halo != null and actor._halo.visible, "de cima, o corpo conduzido leva o realce")
	eq(actor._halo.scale.x, 1.0, "perto: o tamanho de base")
	actor.set_legible(true, 6.0)
	check(actor._halo.scale.x > 1.5, "longe cresce para manter o tamanho aparente (%s)" % actor._halo.scale.x)
	actor.set_legible(true, 60.0)
	check(actor._halo.scale.x <= NpcActor.HALO_MAX + 0.001, "com teto")
	actor.set_legible(false, 6.0)
	check(not actor._halo.visible, "de lado o realce some")
	var other := NpcActor.new()
	other.set_legible(true, 6.0)
	check(not other.legible and other._halo == null, "só o corpo do jogador")
	other.free()
	actor.free()


func test_the_card_says_the_body_is_going_to_the_workplace_not_idle() -> void:
	var frame := PlayerFrame.new()
	frame.set_body("Marina", "Parado", {"health": 100, "hunger": 100, "thirst": 100, "sleep": 100})
	frame.set_busy("Indo: Candidatar-se: Escritório…", 0.2)
	eq(frame.action_label.text, "Indo: Candidatar-se: Escritório", "o cartão não diz Parado com o corpo a caminho do trabalho")
	frame.free()


func test_a_room_is_ugly_only_below_what_its_kind_is_expected_to_be() -> void:
	eq(BuyMode.room_color(-1.5, -3.0), Color(0.85, 0.88, 0.94), "o banheiro de vaso e chuveiro (−1,5 contra −3 esperado) não fica vermelho")
	eq(BuyMode.room_color(-1.5, 0.0), Color("e8a090"), "sem esperado, a mesma medida é feia")
	eq(BuyMode.room_color(-5.0, -3.0), Color("e8a090"), "abaixo do esperado é feio")
	eq(BuyMode.room_color(3.0, -3.0), Color("9fd6b0"), "bem acima do esperado é bonito")
