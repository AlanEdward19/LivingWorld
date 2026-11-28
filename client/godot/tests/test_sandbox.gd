extends "res://tests/test_base.gd"
## #252 (S1 do sandbox): o botão de início rápido do título ("Sandbox: casal", declarado pelo servidor em `card.quickStart`) cria e abre o
## mundo direto; e trocar de corpo solta uma pessoa sem sumir com ela e passa à outra da casa, sem segundo jogador.

const MainMenu := preload("res://Scenes/main_menu.gd")
const GameSettings := preload("res://Scenes/menu/game_settings.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const WorldReader := preload("res://Contract/world_reader.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const MenuTest := preload("res://tests/test_main_menu.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const App := preload("res://Scenes/app.gd")
const Fx := preload("res://tests/fixtures.gd")

const SETTINGS_PATH := "user://test-sandbox-settings.cfg"
const DEFINITION := '{"periodId":"sandbox-casal","periodDefinition":{"Width":24,"Height":24,"Seed": 252,"InitialPopulation":0},"version":1}'
const SANDBOX_CARD := {"name": "Sandbox: casal", "era": "Subúrbio de hoje", "description": "Um casal numa casa de subúrbio.", "startingTechnology": "",
	"initialPopulation": 2, "settlementCount": 1, "professionCount": 1, "professions": ["Morador"], "inventionEnabled": true, "quickStart": true}
const MEDIEVAL_CARD := {"name": "Vila do vale", "era": "Aldeias", "description": "Poucas famílias.", "startingTechnology": "", "initialPopulation": 40,
	"settlementCount": 1, "professionCount": 1, "professions": ["Fazendeiro"], "inventionEnabled": true}


func _transport(status := 200) -> RefCounted:
	var transport := FakeTransport.new()
	transport.on("POST /worlds/create/async", {"status": 202, "body": {"jobId": "j", "statusUrl": "/worlds/create/jobs/j", "stages": []}})
	transport.on("GET /worlds/create/jobs/", MenuTest.job_route(status != 200, status, "Já existe um mundo salvo com esta seed." if status != 200 else ""))
	transport.on("GET /simulation/status", {"status": 200, "body": {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 0, "tickDeltaMs": 3600000, "year": 0}})
	transport.on("GET /worlds", {"status": 200, "body": []})
	transport.on("GET /world/info", {"status": 200, "body": {"name": "Aldeia", "periodId": "", "technologyAxes": []}})
	transport.on("GET /periods/sandbox-casal/catalog", {"status": 200, "body": {"professionIds": [1], "professionNames": {"1": "Morador"}}})
	transport.on("GET /periods/sandbox-casal", {"status": 200, "body": {}, "text": DEFINITION})
	transport.on("GET /periods/medieval/catalog", {"status": 200, "body": {"professionIds": [1], "professionNames": {"1": "Fazendeiro"}}})
	transport.on("GET /periods/medieval", {"status": 200, "body": {}, "text": DEFINITION.replace("sandbox-casal", "medieval")})
	transport.on("GET /periods", {"status": 200, "body": [
		{"periodId": "medieval", "source": "Vila", "width": 2, "height": 2, "card": MEDIEVAL_CARD},
		{"periodId": "sandbox-casal", "source": "Sandbox: casal", "width": 24, "height": 24, "card": SANDBOX_CARD}]})
	return transport


func _menu(transport: RefCounted) -> Control:
	DirAccess.remove_absolute(ProjectSettings.globalize_path(SETTINGS_PATH))
	var menu := MainMenu.new()
	menu.api = ApiClient.new(transport)
	menu.api.validate_shapes = false
	menu.settings = GameSettings.load_from(SETTINGS_PATH)
	tree.root.add_child(menu)
	return menu


func _done(menu: Control) -> void:
	menu.free()
	DirAccess.remove_absolute(ProjectSettings.globalize_path(SETTINGS_PATH))


func test_the_title_offers_a_button_for_the_period_the_server_marks_quick_start() -> void:
	var menu := _menu(_transport())
	await menu.refresh_server()

	eq(menu.title.quick_buttons.size(), 1, "um botão para o único período de início rápido")
	eq(menu.title.quick_buttons[0].text, "Sandbox: casal", "o nome vem do cartão do servidor")
	check(menu.title.quick_buttons[0].get_index() == menu.title.new_world_button.get_index() + 1, "logo abaixo de Novo mundo")
	_done(menu)


func test_the_wizard_does_not_list_the_quick_start_period_as_a_card() -> void:
	var menu := _menu(_transport())
	await menu.refresh_server()
	await menu.show_new_world()

	eq(menu.wizard.period_ids, ["medieval"] as Array[String], "o início rápido não vira cartão do assistente")
	_done(menu)


func test_the_button_creates_the_world_with_a_fresh_seed_and_enters_it_directly() -> void:
	var transport := _transport()
	var menu := _menu(transport)
	await menu.refresh_server()
	var fired := [false]
	menu.world_ready.connect(func(): fired[0] = true)

	menu.title.quick_buttons[0].pressed.emit()
	while not fired[0]:
		await tree.process_frame

	check("POST /worlds/create/async" in transport.calls, "criou pelo servidor")
	var body: Dictionary = transport.bodies["POST /worlds/create/async"][0]
	eq(String(body.periodId), "sandbox-casal", "o período do botão")
	eq(String(body.name), "Sandbox: casal", "o nome do cartão")
	check(not ('"Seed":252' in String(body.scenarioJson).replace(" ", "")), "semente nova a cada criação (a do arquivo já pode estar em outro mundo salvo)")
	eq(fired[0], true, "entra no mundo sem passar pelo assistente")
	_done(menu)


func test_a_server_refusal_returns_to_the_title_and_says_why() -> void:
	var menu := _menu(_transport(409))
	await menu.refresh_server()

	await menu.quick_start("sandbox-casal")

	eq(menu.current, menu.title, "volta ao título, não ao assistente")
	check("Já existe um mundo" in menu.title.status_label.text, "diz o motivo: " + menu.title.status_label.text)
	_done(menu)


func _hud_transport() -> RefCounted:
	var transport := FakeTransport.new()
	transport.on("POST /players/7/incarnate", {"status": 200, "body": {"npcId": 7, "playerId": "p-1"}})
	transport.on("POST /players/p-1/switch", {"status": 200, "body": {"npcId": 8, "playerId": "p-1"}})
	transport.on("POST /players/p-1/logout", {"status": 200, "body": {"npcId": 7, "playerId": "p-1", "disappearedAtMs": 0}})
	transport.on("GET /entities/npc/7", {"status": 200, "body": {"kind": "npc", "id": "7", "name": "Marina", "links": [], "recentEvents": [],
		"state": {"npc": {"household": {"value": 3}}}}})
	transport.on("GET /entities/npc/8", {"status": 200, "body": {"kind": "npc", "id": "8", "name": "Rafael", "links": [], "recentEvents": [],
		"state": {"npc": {"household": {"value": 3}}}}})
	transport.on("GET /entities/household/3", {"status": 200, "body": {"kind": "household", "id": "3", "name": "Casa", "links": [], "recentEvents": [],
		"state": {"memberIds": [8, 7]}}})
	return transport


func _hud(transport: RefCounted) -> Object:
	var hud := PlayerHud.new()
	hud.api = WorldReader.new(transport)
	hud.api.validate_shapes = false
	hud.labels = ActionLabels.new()
	hud.player_id = "p-1"
	return hud


func test_switching_bodies_releases_the_old_one_without_logging_out_and_takes_the_other() -> void:
	var transport := _hud_transport()
	var hud := _hud(transport)
	var seen: Array = []
	hud.switched.connect(func(old_id: int, new_id: int): seen.append([old_id, new_id]))
	await hud.incarnate(7, "Marina")
	await hud.refresh_housemates()

	eq(hud.housemates, [{"id": 8, "name": "Rafael"}] as Array, "a outra pessoa da casa, lida do motor")
	eq(hud.switch_button.visible, true, "o botão aparece com alguém para trocar")
	eq(hud.switch_button.text, "Trocar para Rafael (G)", "diz para quem")

	var switched: bool = await hud.switch_body()

	eq(switched, true, "o motor aceitou a troca")
	eq(hud.npc_id, 8, "agora conduz o outro")
	eq(hud.npc_name, "Rafael", "com o nome dele")
	eq(hud.is_active, true, "um jogador só, sempre com corpo")
	eq(seen, [[7, 8]] as Array, "o app foi avisado da troca (para soltar a figura e deslizar a câmera)")
	check("POST /players/p-1/switch" in transport.calls, "trocou pela rota de troca")
	check(not ("POST /players/p-1/logout" in transport.calls), "NÃO saiu do personagem (isso faria o corpo de antes sumir do mundo)")
	hud.free()


func test_a_refused_switch_keeps_the_current_body() -> void:
	var transport := _hud_transport()
	transport.on("POST /players/p-1/switch", {"status": 409, "body": {"error": "conflict: NPC já tem controlador"}})
	var hud := _hud(transport)
	var warnings: Array = []
	hud.warning.connect(func(text: String): warnings.append(text))
	await hud.incarnate(7, "Marina")
	await hud.refresh_housemates()

	var switched: bool = await hud.switch_body()

	eq(switched, false, "recusada")
	eq(hud.npc_id, 7, "continua no mesmo corpo")
	eq(hud.is_active, true, "e ativo")
	eq(warnings.size(), 1, "o motivo vira aviso")
	hud.free()


func test_alone_in_the_house_there_is_nobody_to_switch_to() -> void:
	var transport := _hud_transport()
	transport.on("GET /entities/household/3", {"status": 200, "body": {"kind": "household", "id": "3", "name": "Casa", "links": [], "recentEvents": [],
		"state": {"memberIds": [7]}}})
	var hud := _hud(transport)
	await hud.incarnate(7, "Marina")
	await hud.refresh_housemates()

	eq(hud.housemates.size(), 0, "ninguém na casa além de quem se conduz")
	eq(hud.switch_button.visible, false, "sem botão")
	eq(await hud.switch_body(), false, "e a tecla não faz nada")
	check(not ("POST /players/p-1/switch" in transport.calls), "nenhuma chamada ao motor")
	hud.free()


## #256: o relógio dos dois ritmos: sem jogador o sandbox corre uma hora por tick; com jogador, tempo real 1:1 (50 ms por tick, 20 por segundo).
func _app_transport(rhythm: Dictionary) -> RefCounted:
	var transport := FakeTransport.new()
	transport.on("GET /visual/catalog/actions", {"status": 200, "body": []})
	var status := {"isPaused": false, "ticksPerSecond": 1.0, "atMs": 5 * 3600000, "year": 0}
	status.merge(rhythm)
	transport.on("GET /simulation/status", {"status": 200, "body": status})
	transport.on("GET /npcs?", {"status": 200, "body": {"total": 2, "skip": 0, "take": 50, "items": [
		{"id": {"value": 12}, "name": "Rafael"}, {"id": {"value": 11}, "name": "Marina"}]}})
	transport.on("POST /players/11/incarnate", {"status": 200, "body": {"npcId": 11, "playerId": "p-1"}})
	transport.on("GET /npcs/11", Fx.npc_inspect([]))
	transport.on("GET /visual/subscribe?scope=World", {"status": 200, "body": Fx.snapshot(0, {
		"width": 2, "height": 2, "cities": [], "externalNpcs": [], "activeEvents": [], "portals": [],
		"layers": {"Terrain": {"isModeled": false, "payload": null}}, "livingState": {"npcs": []}}, "World")})
	return transport


func test_a_quick_start_world_opens_already_inside_the_first_person_of_the_family() -> void:
	var transport := _app_transport({"tickDeltaMs": 50, "loopTicksPerSecond": 20.0, "rhythm": "RealTime", "playersPresent": 1})
	var app := App.new()
	app.player_id = "p-1"
	app.quick_start = true
	tree.root.add_child(app)
	await app.start(transport, false)
	var entered: bool = await app.enter_first_of_family()

	eq(entered, true, "entrou no corpo")
	eq(app.player_hud.npc_id, 11, "o primeiro da família (menor id), não o segundo")
	eq(app.player_hud.npc_name, "Marina", "com o nome que o motor deu")
	eq(app.quick_entered, true, "a entrada do início rápido terminou (os passos de captura esperam por isto)")
	check("POST /players/11/incarnate" in transport.calls or "POST /players/possess" in transport.calls, "possuiu pelo motor")
	app.queue_free()


func test_the_clock_is_read_again_right_after_possessing_and_runs_one_to_one() -> void:
	var transport := _app_transport({"tickDeltaMs": 50, "loopTicksPerSecond": 20.0, "rhythm": "RealTime", "playersPresent": 1})
	var app := App.new()
	app.player_id = "p-1"
	tree.root.add_child(app)
	await app.start(transport, false)
	var before: int = transport.calls.count("GET /simulation/status")

	await app.incarnate(11)

	check(transport.calls.count("GET /simulation/status") > before, "releu o status na hora de possuir, sem esperar o próximo ciclo")
	var rate := App.hours_per_second(app.clock_panel.status)
	check(absf(rate * 3600.0 - 1.0) < 1e-6, "o relógio anda 1 s de mundo por segundo real (%s)" % (rate * 3600.0))
	app.queue_free()
