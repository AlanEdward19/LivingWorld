extends "res://tests/test_base.gd"
## #263: o emprego e as contas no cartão do corpo, o painel da carreira (tecla O), as ofertas de ir ao trabalho, candidatar-se e pagar, e os avisos ("+72 salário",
## "Hora do trabalho em 1 h"). O cliente só mostra o que o motor diz (`career` da ficha e as ofertas `job`/`bills`): nada é calculado nem inventado aqui.

const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const CareerPanel := preload("res://Scenes/career_panel.gd")

const JOB := {"career": "escritorio", "careerName": "Escritório", "title": "Auxiliar administrativo", "levelIndex": 0, "levels": 3, "wagePerHour": 9,
	"startHour": 9, "hours": 8, "days": [0, 1, 2, 3, 4], "score": 78, "shifts": 3, "earned": 216, "good": 2, "promoteAfter": 3, "promoteAt": 65.0, "demoteBelow": 35.0,
	"bad": 0, "demoteAfter": 3, "absences": 0, "absencesToFire": 3, "nextTitle": "Analista", "nextWagePerHour": 14, "away": false, "returnMs": 0, "workEndMs": 0,
	"arriveMs": 0, "nextShiftText": "amanhã 09:00", "canGo": false, "skillName": "Administração", "skill": 21.5}
const BILL := {"id": "agua", "name": "Água", "open": 38, "fee": 0, "dueMs": 1, "cut": false, "late": false, "usage": 1.2, "dueText": "amanhã 08:00"}
const VIEW := {"job": JOB, "bills": [BILL], "notices": [], "listings": [], "dayNames": ["Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom"]}


func _texts(panel: Control) -> String:
	var text := ""
	for child in panel.box.get_children():
		text += (String(child.text) + "\n") if child is Label else ""
	return text


func test_the_card_lines_say_the_job_the_next_shift_and_the_urgent_bill() -> void:
	var lines := CareerPanel.card_lines(VIEW)
	eq(lines.size(), 2, "o cargo e a conta")
	eq(lines[0], "Auxiliar administrativo · próximo turno amanhã 09:00", "o cargo e o turno do motor")
	eq(lines[1], "Água 38 · vence amanhã 08:00", "a conta com o vencimento do motor")
	var away := VIEW.duplicate(true)
	away.job.away = true
	away.job.returnText = "17:40"
	eq(CareerPanel.card_lines(away)[0], "Auxiliar administrativo · no trabalho até 17:40", "no turno: a hora da volta")
	var late := VIEW.duplicate(true)
	late.bills[0].late = true
	late.bills[0].fee = 4
	check(CareerPanel.card_lines(late)[1].begins_with("⚠ Água 42"), "atrasada: aviso e a multa somada")
	eq(CareerPanel.card_lines(null).size(), 0, "sem carreira no mundo, nada no cartão")
	eq(CareerPanel.card_lines({"job": null, "bills": [], "notices": [], "listings": []}).size(), 0, "sem emprego nem conta, nada")


func test_the_schedule_text_comes_from_the_engine_days_and_the_scenario_names() -> void:
	eq(CareerPanel.days_text([0, 1, 2, 3, 4], VIEW.dayNames), "Seg–Sex", "uma faixa")
	eq(CareerPanel.days_text([0, 2, 4], VIEW.dayNames), "Seg, Qua, Sex", "dias soltos")
	eq(CareerPanel.days_text([5, 6], VIEW.dayNames), "Sáb, Dom", "dois dias")
	eq(CareerPanel.days_text([0, 1], null), "dia 1, dia 2", "sem nomes do cenário, o número")
	eq(CareerPanel.hours_text(JOB), "09:00 às 17:00", "o turno")
	eq(CareerPanel.score_text(JOB), "Desempenho do último turno: 78%", "o desempenho do motor")
	eq(CareerPanel.score_text({"score": -1}), "Ainda sem turno cumprido.", "antes do primeiro turno")
	eq(CareerPanel.promotion_text(JOB), "Para Analista (14/h): 2 de 3 turnos com desempenho de 65% ou mais.", "o que falta para subir")
	eq(CareerPanel.promotion_text({"nextTitle": null}), "No topo da carreira.", "sem nível acima")


func test_the_panel_shows_the_job_the_bills_and_the_vacancies() -> void:
	var panel := CareerPanel.new()
	panel.set_career(VIEW)
	var text := _texts(panel)
	check(text.contains("Auxiliar administrativo · Escritório"), "cargo e carreira")
	check(text.contains("Salário: 9 por hora · Nível 1 de 3"), "salário e nível")
	check(text.contains("Horário: 09:00 às 17:00 · Seg–Sex"), "horário")
	check(text.contains("Próximo turno: amanhã 09:00"), "próximo turno")
	check(text.contains("Água: 38 · vence amanhã 08:00"), "a conta")
	check(text.contains("Administração: 22"), "a habilidade")
	var jobless := {"job": null, "bills": [], "notices": [], "listings": [{"career": "ensino", "careerName": "Escola", "employer": 3, "title": "Monitor escolar",
		"wagePerHour": 8, "startHour": 8, "hours": 6, "free": 2, "chance": 0.6}]}
	panel.set_career(jobless)
	text = _texts(panel)
	check(text.contains("Sem emprego"), "sem emprego")
	check(text.contains("Monitor escolar · Escola · 8/h · 08:00, 6h · chance 60%"), "a vaga com a chance do motor")
	check(not panel.is_open(), "nasce fechado")
	panel.toggle()
	check(panel.is_open(), "O abre")
	panel.free()


func test_the_frame_shows_the_card_line_and_toggles_the_panel() -> void:
	var frame := PlayerFrame.new()
	tree.root.add_child(frame)
	check(not frame.career_label.visible, "sem carreira, sem linha")
	frame.set_career(VIEW)
	check(frame.career_label.visible, "a linha do emprego aparece")
	check(frame.career_label.text.contains("próximo turno amanhã 09:00"), "o turno do motor")
	check(PlayerFrame.hint_text().contains("O carreira"), "a tecla O nas dicas")
	check(not frame.career_open(), "o painel nasce fechado")
	frame.toggle_career()
	check(frame.career_open(), "O abre a carreira")
	frame.set_career(null)
	check(not frame.career_label.visible, "sem dado, a linha some")
	frame.queue_free()


func test_the_menu_lists_go_to_work_apply_and_pay_with_the_text_the_engine_sends() -> void:
	var offers := [
		{"kind": "job", "id": 7, "label": "Auxiliar administrativo", "verbs": ["go"], "x": 5, "y": 5, "resources": [],
			"uses": [{"id": "go", "name": "Ir ao trabalho · Auxiliar administrativo", "minutes": 0, "need": null, "busy": false}]},
		{"kind": "bills", "id": 2, "label": "Contas", "verbs": ["pay:agua"], "x": 5, "y": 5, "resources": [],
			"uses": [{"id": "pay:agua", "name": "Pagar Água: 38", "minutes": 0, "need": null, "busy": false}]},
		{"kind": "job", "id": 7, "label": "Vagas", "verbs": ["apply:ensino@3"], "x": 5, "y": 5, "resources": [],
			"uses": [{"id": "apply:ensino@3", "name": "Candidatar-se: Monitor escolar (Escola, 8/h)", "minutes": 0, "need": null, "busy": false}]}]
	var entries := PlayerHud.menu_entries(offers)
	eq(entries.size(), 3, "uma linha por verbo do motor")
	eq(entries[0].text, "Ir ao trabalho · Auxiliar administrativo", "ir ao trabalho")
	eq(entries[0].verb, "go", "o verbo do motor")
	eq(entries[1].text, "Pagar Água: 38", "pagar a conta")
	eq(entries[1].verb, "pay:agua", "o verbo da conta")
	eq(entries[2].verb, "apply:ensino@3", "candidatar-se à vaga")
	eq(entries[2].kind, "job", "o tipo da oferta")


func test_notices_warn_once_and_the_first_read_only_learns() -> void:
	var hud := PlayerHud.new()
	var said: Array = []
	hud.warning.connect(func(text): said.append(text))
	var first := {"job": JOB, "notices": [{"id": 3, "kind": "paid", "text": "+72 salário", "amount": 72}]}
	hud._note_career(first)
	eq(said.size(), 0, "a primeira leitura não repete o passado")
	var second := {"job": JOB, "notices": [{"id": 3, "kind": "paid", "text": "+72 salário", "amount": 72},
		{"id": 4, "kind": "shift-soon", "text": "Hora do trabalho em 1 h: Auxiliar administrativo, às 09:00.", "verb": "go"},
		{"id": 5, "kind": "bill-issued", "text": "Conta de Água: 38, vence amanhã 08:00.", "verb": "pay"}]}
	hud._note_career(second)
	eq(said.size(), 2, "só os avisos novos")
	check(String(said[0]).begins_with("Hora do trabalho em 1 h") and String(said[0]).ends_with("E: Ir ao trabalho"), "o aviso de turno oferece ir")
	check(String(said[1]).ends_with("E em casa: Contas"), "o aviso de conta aponta onde pagar")
	hud._note_career(second)
	eq(said.size(), 2, "nada se repete")
	hud.free()


func test_the_body_leaving_for_the_shift_asks_the_app_to_hide_the_figure_and_back() -> void:
	var hud := PlayerHud.new()
	hud.npc_id = 7
	var calls: Array = []
	hud.body_away.connect(func(away: bool, id: int): calls.append([away, id]))
	hud._note_career({"job": JOB, "notices": []})
	eq(calls.size(), 0, "em casa, nada muda")
	var away_job := JOB.duplicate(true)
	away_job.away = true
	hud._note_career({"job": away_job, "notices": []})
	eq(calls, [[true, 7]], "saiu: a figura some")
	hud._note_career({"job": away_job, "notices": []})
	eq(calls.size(), 1, "só na mudança")
	hud._note_career({"job": JOB, "notices": []})
	eq(calls, [[true, 7], [false, 7]], "voltou: a figura volta")
	hud.free()
