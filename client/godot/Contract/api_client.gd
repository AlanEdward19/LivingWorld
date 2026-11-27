extends RefCounted
## Rotas HTTP não-streaming da API (relógio, lista de NPCs, ficha, catálogo, resumos). Toda resposta
## com forma congelada em tests/contracts/shape.*.json é validada aqui; cenas e painéis só recebem
## `{ok, status, body}` e nunca montam rota nem leem código HTTP.
const WorldTime := preload("res://Contract/world_time.gd")

const ShapeValidator := preload("res://Contract/shape_validator.gd")
const JsonRaw := preload("res://Contract/json_raw.gd")

signal contract_error(route: String, errors: Array)

var transport
var validate_shapes := true


func _init(p_transport) -> void:
	transport = p_transport


func _fetch(path: String, shape_name: String = "", extra_headers: Dictionary = {}) -> Dictionary:
	var response: Dictionary = await transport.request("GET", path, null, extra_headers)
	return _checked(path, response, shape_name)


func _checked(route: String, response: Dictionary, shape_name: String) -> Dictionary:
	var result := {"ok": response.status == 200, "status": response.status, "body": response.body, "errors": []}
	if result.ok and validate_shapes and shape_name != "":
		var shape = ShapeValidator.load_shape(shape_name)
		if shape != null:
			var errors := ShapeValidator.validate(response.body, shape)
			if not errors.is_empty():
				result.ok = false
				result.errors = errors
				contract_error.emit(route, errors)
	return result


func clock_status() -> Dictionary:
	return await _fetch("/simulation/status", "simulation-status")


func clock_command(action: String) -> Dictionary:
	return await transport.request("POST", "/simulation/" + action)


func set_speed(ticks_per_second: float) -> Dictionary:
	return await transport.request("POST", "/simulation/speed", {"ticksPerSecond": ticks_per_second})


## Identidade do mundo em andamento (ADR-0027): nome, período de origem ("" se desconhecido) e rótulos dos eixos de tecnologia.
func world_info() -> Dictionary:
	return await _fetch("/world/info", "world-info")


func world_calendar() -> Dictionary:
	return await _fetch("/world/calendar", "world-calendar")


func action_catalog() -> Dictionary:
	return await _fetch("/visual/catalog/actions", "action-catalog")


func npc_page(query: String, skip: int, take: int, city_id: String = "") -> Dictionary:
	var path := "/npcs?skip=%d&take=%d" % [skip, take]
	if query != "":
		path += "&q=" + query.uri_encode()
	if city_id != "":
		path += "&cityId=" + city_id.uri_encode()
	return await _fetch(path, "npc-directory")


func npc_detail(id: int) -> Dictionary:
	return await _fetch("/npcs/%d" % id, "npc-inspect")


## Marca (`on`) ou desmarca o NPC na watchlist: com ela o rastro de cognição é retido por inteiro.
func watch_npc(id: int, on: bool) -> Dictionary:
	var response: Dictionary = await transport.request("POST" if on else "DELETE", "/npcs/%d/watchlist" % id)
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## Publica o que a câmera observa (`scope` = {kind, cityId?, buildingId?}) ou, com `{}`, retira a fonte.
func observation_scope(source_id: String, scope: Dictionary) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/observation/scope", {"sourceId": source_id, "scope": scope if not scope.is_empty() else null})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## Leitura do cérebro (AD-160b): a rede termo → grupo → ação de agora, os ganhos que o NPC aprendeu e o valor que ele dá ao
## próprio estado. Projeção do servidor; nada é gravado.
func npc_brain(id: int) -> Dictionary:
	var response: Dictionary = await transport.request("GET", "/npcs/%d/brain" % id)
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## "E se…?" do painel Cérebro: o servidor roda a decisão do NPC com estímulos sobrepostos; nada é gravado.
func decision_sandbox(id: int, overrides: Dictionary) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/npcs/%d/decision-sandbox" % id, overrides)
	return _checked("/npcs/%d/decision-sandbox" % id, {"status": response.status, "body": response.body}, "decision-sandbox")


# --- narrativas (crenças da comunidade, nunca a verdade do motor) ------------------------------

func biography(npc_id: int) -> Dictionary:
	return await _fetch("/narratives/biographies/%d" % npc_id, "narrative-biography")


func chronicle(city_id: String, period_start: int, period_end: int, top_k: int) -> Dictionary:
	return await _fetch("/narratives/chronicles?location=%s&periodStart=%d&periodEnd=%d&topK=%d" % [city_id.uri_encode(), period_start, period_end, top_k], "narrative-chronicle")


func reports() -> Dictionary:
	return await _fetch("/narratives/reports", "narrative-reports")


## Crenças vivas da comunidade (nunca `/history/truth`: é rota de motor).
func beliefs(city_id: String) -> Dictionary:
	return await _fetch("/history/beliefs?communityId=%s" % city_id.uri_encode(), "history-beliefs")


# --- jogador encarnado: sessão por token e canal persistente (ADR-0048, AD-197a) ----------------------------

class _Pending:
	signal done(reply: Dictionary)

var player_token := "" ## sessão do jogador (POST /sessions); vazio = modo local, o playerId vai na rota como antes
var player_channel = null ## canal persistente `/players/ws`; aberto, os comandos vão por ele (sem novo aperto de mão a cada clique)
var _player_seq := 0
var _player_pending: Dictionary = {} ## seq -> _Pending
const CHANNEL_TIMEOUT_S := 5.0


## Pede uma sessão: devolve `{ok, status, body:{sessionToken, playerId}}` e guarda o token.
func create_session() -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/sessions")
	var result := _checked("/sessions", {"status": response.status, "body": response.body}, "")
	if result.ok and result.body is Dictionary and String(result.body.get("sessionToken", "")) != "":
		player_token = String(result.body.sessionToken)
	else:
		result.ok = false
	return result


## Abre o canal persistente do jogador com o token da sessão (sem token, nada).
func open_player_channel() -> void:
	if player_token == "" or player_channel != null or not transport.has_method("open_channel"):
		return
	player_channel = transport.open_channel("/players/ws", {"Authorization": "Bearer " + player_token})
	player_channel.message.connect(_on_player_reply)
	player_channel.closed.connect(func(): _drop_player_channel())


func close_player_channel() -> void:
	if player_channel != null:
		player_channel.close()
	_drop_player_channel()


func _drop_player_channel() -> void:
	player_channel = null
	for seq in _player_pending.keys():
		_player_pending[seq].done.emit({"status": 0, "body": null})
	_player_pending.clear()


func _on_player_reply(data) -> void:
	if not (data is Dictionary) or not _player_pending.has(int(data.get("seq", -1))):
		return
	var seq := int(data.seq)
	var pending = _player_pending[seq]
	_player_pending.erase(seq)
	var body = data.get("body") if bool(data.get("ok", false)) else {"error": data.get("error", "")}
	pending.done.emit({"status": int(data.get("status", 0)), "body": body})


func _player_headers() -> Dictionary:
	return {} if player_token == "" else {"Authorization": "Bearer " + player_token}


## Um comando do jogador: pelo canal persistente se estiver aberto, senão pela rota HTTP. Resposta igual nos dois caminhos.
func _player_command(command: Dictionary, method: String, path: String, body: Variant) -> Dictionary:
	if player_channel != null and player_channel.is_open():
		_player_seq += 1
		var seq := _player_seq
		var pending := _Pending.new()
		_player_pending[seq] = pending
		command["seq"] = seq
		if player_channel.send(command):
			Engine.get_main_loop().create_timer(CHANNEL_TIMEOUT_S).timeout.connect(func():
				if _player_pending.has(seq):
					_player_pending.erase(seq)
					pending.done.emit({"status": 0, "body": null}), CONNECT_ONE_SHOT)
			return await pending.done
		_player_pending.erase(seq)
	return await transport.request(method, path, body, _player_headers())


## #240: pausa de popup (só pelo canal persistente): o motor honra se este for o único jogador conectado; vence em 30 s sem renovar.
func player_hold(on: bool) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "hold", "on": on}, "POST", "/players/hold", {"on": on})
	return {"ok": response.status == 200, "held": response.body is Dictionary and bool(response.body.get("held", false))}


func incarnate(npc_id: int, player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "incarnate", "npcId": npc_id}, "POST", "/players/%d/incarnate" % npc_id, {"playerId": player_id})
	return _checked("/players/incarnate", {"status": response.status, "body": response.body}, "player-incarnate")


## #222 (AD-197e): posse plena, do admin (o transporte manda o X-Admin-Token) com a sessão do jogador: a pessoa não recusa ordens.
func player_possess(npc_id: int) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/players/%d/possess" % npc_id, {}, _player_headers())
	return _checked("/players/possess", {"status": response.status, "body": response.body}, "player-incarnate")


## AD-197q: de onde dá para sair como pessoa nova (`{cities:[{cityId,name,aggregateCount,detailedCount}]}`), do motor.
func player_origins() -> Dictionary:
	var response: Dictionary = await transport.request("GET", "/players/origins", null, _player_headers())
	return _checked("/players/origins", {"status": response.status, "body": response.body}, "player-origins")


## AD-197t: o que o criador oferece numa cidade (faixas, teto de dinheiro, ofícios, trajes, origens e as famílias com os papéis possíveis).
func player_city_options(city_id: String) -> Dictionary:
	var response: Dictionary = await transport.request("GET", "/players/origins/%s" % city_id.uri_encode(), null, _player_headers())
	return _checked("/players/origins/city", {"status": response.status, "body": response.body}, "player-city-options")


## AD-197t: a prévia que o motor valida da escolha (`spec`); só lê, nada vai ao registro. 409 com o motivo se for incoerente.
func player_character_preview(player_id: String, spec: Dictionary) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/players/%s/character/preview" % player_id.uri_encode(), spec, _player_headers())
	return _checked("/players/character/preview", {"status": response.status, "body": response.body}, "player-character-preview")


## AD-197t: cria a pessoa que o jogador montou e a encarna (nome, idade, traços, cidade, família ou chegada, dinheiro, aparência).
func player_character(player_id: String, spec: Dictionary) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "character", "spec": spec}, "POST", "/players/%s/character" % player_id.uri_encode(), spec)
	return _checked("/players/character", {"status": response.status, "body": response.body}, "player-character")


## AD-197q: uma amostra nova do agregado da cidade (`filter`: sex, minAge, maxAge, professionId, count). Cada pedido é registrado
## pelo motor; 409 com o motivo quando o agregado está vazio ou ninguém passa no filtro.
func player_candidates(player_id: String, city_id: String, filter: Dictionary = {}) -> Dictionary:
	var body := filter.duplicate()
	body["cityId"] = city_id
	var command := body.duplicate()
	command["command"] = "candidates"
	var response: Dictionary = await _player_command(command, "POST", "/players/%s/candidates" % player_id.uri_encode(), body)
	return _checked("/players/candidates", {"status": response.status, "body": response.body}, "player-candidates")


## AD-197q: entrar como o candidato `npc_id` da amostra `sample`: o motor materializa a pessoa e a encarna.
func player_create(player_id: String, city_id: String, npc_id: int, sample: int) -> Dictionary:
	var body := {"cityId": city_id, "npcId": npc_id, "sample": sample}
	var command := body.duplicate()
	command["command"] = "create"
	var response: Dictionary = await _player_command(command, "POST", "/players/%s/create" % player_id.uri_encode(), body)
	return _checked("/players/create", {"status": response.status, "body": response.body}, "player-create")


## `action` = nome da ação (Eat, Sleep, Work, Socialize, Idle, Buy, UsePower); "" devolve a escolha ao motor. `power_id` (AD-196)
## vai só com UsePower: o poder que o NPC porta (o servidor recusa com 400 o que ele não porta).
func player_action(player_id: String, action: String, power_id: String = "") -> Dictionary:
	var body := {"action": null if action == "" else action}
	if power_id != "":
		body["powerId"] = power_id
	var command := {"command": "action", "action": body.action}
	if power_id != "":
		command["powerId"] = power_id
	var response: Dictionary = await _player_command(command, "POST", "/players/%s/action" % player_id.uri_encode(), body)
	return _checked("/players/action", {"status": response.status, "body": response.body}, "player-action")


## AD-197e: um passo do personagem na direção (−1..1 em cada eixo): intenção, o motor decide onde ele chega (resposta com x, y).
func player_move(player_id: String, dx: int, dy: int) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "move", "dx": dx, "dy": dy}, "POST", "/players/%s/move" % player_id.uri_encode(), {"dx": dx, "dy": dy})
	return _checked("/players/move", {"status": response.status, "body": response.body}, "")


## AD-197k: o rumo do passo solto (milésimos por eixo; 0, 0 para); a resposta é a pose com a posição fina e a velocidade.
func player_walk(player_id: String, dx: int, dy: int, run: bool = false) -> Dictionary:
	var body := {"dx": dx, "dy": dy}
	if run:
		body["run"] = true # #222: Shift corre; o motor decide se o corpo aguenta (cansado, anda)
	var command := body.duplicate()
	command["command"] = "walk"
	var response: Dictionary = await _player_command(command, "POST", "/players/%s/walk" % player_id.uri_encode(), body)
	return _checked("/players/walk", {"status": response.status, "body": response.body}, "")


## #222: pular; o motor diz se o corpo consegue e quanto tempo fica no ar (`airMs`), ou 409 com o motivo.
func player_jump(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "jump"}, "POST", "/players/%s/jump" % player_id.uri_encode(), {})
	return _checked("/players/jump", {"status": response.status, "body": response.body}, "")


## #241: interrompe a ação que ocupa o corpo (cortar lenha...); o motor deixa a parte proporcional e devolve a pose, ou 409 sem ação.
func player_interrupt(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "interrupt"}, "POST", "/players/%s/interrupt" % player_id.uri_encode(), {})
	return _checked("/players/interrupt", {"status": response.status, "body": response.body}, "")


## AD-197d: golpe no NPC sob a mira; o motor valida o alcance e a vontade da pessoa possuída.
func player_strike(player_id: String, npc_id: int) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "strike", "npcId": npc_id}, "POST", "/players/%s/strike" % player_id.uri_encode(), {"npcId": npc_id})
	return _checked("/players/strike", {"status": response.status, "body": response.body}, "")


## #234: esquiva (janela curta de evitar o golpe); o motor responde a `pose` com `combat`, ou 409 na recarga da guarda.
func player_dodge(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "dodge"}, "POST", "/players/%s/dodge" % player_id.uri_encode(), {})
	return _checked("/players/dodge", {"status": response.status, "body": response.body}, "")


## #234: bloqueio (segurar a guarda): deixa passar só parte do golpe; 409 na recarga.
func player_block(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "block"}, "POST", "/players/%s/block" % player_id.uri_encode(), {})
	return _checked("/players/block", {"status": response.status, "body": response.body}, "")


## AD-197d: o que o corpo alcança agora e o que dá para fazer ali (`{offers:[{kind,id,label,verbs,x,y,resources}]}`), do motor.
func player_reach(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "reach"}, "GET", "/players/%s/reach" % player_id.uri_encode(), null)
	return _checked("/players/reach", {"status": response.status, "body": response.body}, "player-reach")


## #235: as metas do corpo (nascem do estado do motor, cada uma com motivo, urgência e próximo passo) e o progresso das habilidades.
func player_goals(player_id: String, with_start: bool = false) -> Dictionary:
	## #238: `with_start` pede também o ponto de partida (casa, família, emprego, mata): só o guia da primeira posse usa
	var path := "/players/%s/goals" % player_id.uri_encode() + ("?start=true" if with_start else "")
	var response: Dictionary = await transport.request("GET", path, null, _player_headers())
	return _checked("/players/goals", {"status": response.status, "body": response.body}, "player-goals")


## #235: o diário do corpo: o que o mundo registrou sobre ele e o que o jogador fez, os marcos de vida marcados.
func player_diary(player_id: String) -> Dictionary:
	var response: Dictionary = await transport.request("GET", "/players/%s/diary" % player_id.uri_encode(), null, _player_headers())
	return _checked("/players/diary", {"status": response.status, "body": response.body}, "player-diary")


## AD-197d: interagir com uma oferta (porta, estoque, banca, cama, pessoa, casa); a recusa da vontade vem 409 com o motivo.
func player_interact(player_id: String, kind: String, id: int, verb: String, resource_id: int = -1, quantity: int = -1) -> Dictionary:
	var body := {"kind": kind, "id": id, "verb": verb}
	if resource_id >= 0:
		body["resourceId"] = resource_id
	if quantity > 0:
		body["quantity"] = quantity
	var command := body.duplicate()
	command["command"] = "interact"
	var response: Dictionary = await _player_command(command, "POST", "/players/%s/interact" % player_id.uri_encode(), body)
	return _checked("/players/interact", {"status": response.status, "body": response.body}, "player-interact")


## AD-197c: o tempo como o jogador o vê (`GET /players/{id}/time`): o ritmo (Fast, RealTime, Accelerated, Manual), se ele pode
## pular o tempo agora (só quem joga sozinho), o pulo em curso e o que o acordou. O cliente só mostra o que o motor diz.
func player_time(player_id: String) -> Dictionary:
	var response: Dictionary = await transport.request("GET", "/players/%s/time" % player_id.uri_encode(), null, _player_headers())
	return _checked("/players/time", {"status": response.status, "body": response.body}, "player-time")


## AD-197c: dormir até a hora do dia `until_hour` (0..horas do dia − 1) ou, com −1, acelerando até descansar.
func player_sleep(player_id: String, until_hour: int = -1) -> Dictionary:
	var command := {"command": "sleep"}
	var body := {}
	if until_hour >= 0:
		command["untilHour"] = until_hour
		body["untilHour"] = until_hour
	var response: Dictionary = await _player_command(command, "POST", "/players/%s/sleep" % player_id.uri_encode(), body)
	return _checked("/players/sleep", {"status": response.status, "body": response.body}, "player-time")


## AD-197c: acelerar parado (o corpo fica à toa, o mundo corre e as necessidades caem).
func player_accelerate(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "accelerate"}, "POST", "/players/%s/accelerate" % player_id.uri_encode(), null)
	return _checked("/players/accelerate", {"status": response.status, "body": response.body}, "player-time")


## AD-197c: encerrar o pulo de tempo (acordar).
func player_wake(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "wake"}, "POST", "/players/%s/wake" % player_id.uri_encode(), null)
	return _checked("/players/wake", {"status": response.status, "body": response.body}, "player-time")


func player_logout(player_id: String) -> Dictionary:
	var response: Dictionary = await _player_command({"command": "logout"}, "POST", "/players/%s/logout" % player_id.uri_encode(), null)
	return _checked("/players/logout", {"status": response.status, "body": response.body}, "player-logout")


# --- console de admin (ADR-0015): o token vem de LW_ADMIN_TOKEN no transporte; o ator vai no cabeçalho -------

func _admin(method: String, path: String, actor: String, body: Variant, shape_name: String) -> Dictionary:
	var response: Dictionary = await transport.request(method, path, body, {"X-Admin-Actor": actor})
	return _checked(path, {"status": response.status, "body": response.body}, shape_name)


## Criação ao vivo (ADR-0033): corpo = item do cenário; exige o token de admin e o mundo fora de pausa de admin.
func admin_create_household(actor: String, body: Dictionary) -> Dictionary:
	return await _admin("POST", "/authoring/households", actor, body, "authoring-household")


func admin_create_workplace(actor: String, body: Dictionary) -> Dictionary:
	return await _admin("POST", "/authoring/workplaces", actor, body, "authoring-workplace")


func admin_pause(actor: String, reason: String, max_ticks: int) -> Dictionary:
	return await _admin("POST", "/admin/world/pause", actor, {"reason": reason, "maxMs": WorldTime.ms(max_ticks)}, "admin-pause")


func admin_resume(actor: String) -> Dictionary:
	return await _admin("POST", "/admin/world/resume", actor, null, "admin-pause")


func admin_speed(actor: String, ticks_per_second: float) -> Dictionary:
	return await _admin("POST", "/admin/world/speed", actor, {"ticksPerSecond": ticks_per_second}, "admin-pause")


func admin_search(actor: String, query: String, kind: String) -> Dictionary:
	return await _fetch("/admin/search?q=%s&kind=%s" % [query.uri_encode(), kind.uri_encode()], "admin-search", {"X-Admin-Actor": actor})


func admin_audit(actor: String, from_tick: int) -> Dictionary:
	return await _fetch("/admin/audit?fromMs=%d" % WorldTime.ms(from_tick), "admin-audit", {"X-Admin-Actor": actor})


# --- economia (só leitura) ------------------------------------------------------------------

func treasury(city_id: String) -> Dictionary:
	return await _fetch("/cities/%s/treasury" % city_id.uri_encode(), "city-treasury")


func workplace_trades(workplace_id: int) -> Dictionary:
	return await _fetch("/workplaces/%d/trades" % workplace_id, "workplace-trades")


## Tecnologias emergidas da cultura (sem nome, só o vetor: ADR-0013).
func technologies(culture_id: int) -> Dictionary:
	return await _fetch("/cultures/%d/technologies" % culture_id, "culture-technologies")


# --- conversa com NPC (só estas três rotas escrevem; o cliente não aplica nada ao mundo) -----

func conversation_start(npc_id: int) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/conversations/start", {"npcId": npc_id})
	return _checked("/conversations/start", {"status": response.status, "body": response.body}, "conversation-start")


func conversation_send(session_id: int, message: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/conversations/send", {"sessionId": session_id, "message": message})
	return _checked("/conversations/send", {"status": response.status, "body": response.body}, "conversation-send")


func conversation_end(session_id: int) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/conversations/end", {"sessionId": session_id})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## Comando explícito: transforma um morador anônimo do pool em NPC detalhado. O GET nunca faz isso.
func materialize_npc(id: int) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/npcs/%d/materialize" % id)
	return _checked("/npcs/%d/materialize" % id, {"status": response.status, "body": response.body}, "npc-inspect")


func interior_snapshot(building_id: int) -> Dictionary:
	return await _fetch("/visual/subscribe?scope=Interior&refId=%d&mode=Spectator" % building_id, "interior-subscribe")


## Retrato do mundo pela rota sem estado (só leitura): a camada `Roads` é relida daqui sem recarregar a cena (AD-177).
func world_snapshot() -> Dictionary:
	return await _fetch("/visual/subscribe?scope=World&mode=Spectator", "world-subscribe")


func city_snapshot(city_id: String) -> Dictionary:
	return await _fetch("/visual/subscribe?scope=City&refId=%s&mode=Spectator" % city_id.uri_encode(), "city-subscribe")


# --- mundos e períodos (menu principal) ---------------------------------------

## Cada período com o cartão do menu (AD-175): nome, época, descrição, pessoas, vilas, ofícios e tecnologia de partida.
func periods() -> Dictionary:
	return await _fetch("/periods", "periods")


func period_catalog(period_id: String) -> Dictionary:
	return await _fetch("/periods/%s/catalog" % period_id.uri_encode())


## Cenário do período como TEXTO cru (ver JsonRaw), com a seed trocada; "" se o período não carregou.
func period_scenario(period_id: String, seed_value: int) -> String:
	var response: Dictionary = await transport.request("GET", "/periods/%s" % period_id.uri_encode())
	if response.status != 200:
		return ""
	var scenario: String = JsonRaw.member(String(response.get("text", "")), "periodDefinition")
	return JsonRaw.replace_member(scenario, "Seed", str(seed_value)) if scenario != "" else ""


func world_preview(scenario_json: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/worlds/preview", {"scenarioJson": scenario_json})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## `start_running` (AD-175) pede o mundo já rodando; o servidor exige o token de admin da sessão (403 sem ele).
func world_create(scenario_json: String, world_name: String, period_id: String = "", start_running := false) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/worlds/create" + _running_query(start_running), {"scenarioJson": scenario_json, "name": world_name, "periodId": period_id})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


## Criação em segundo plano (AD-175): 202 com `jobId`, `statusUrl` e as etapas; o andamento vem de `world_create_job`.
func world_create_async(scenario_json: String, world_name: String, period_id: String = "", start_running := false) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/worlds/create/async" + _running_query(start_running), {"scenarioJson": scenario_json, "name": world_name, "periodId": period_id})
	return {"ok": response.status == 202, "status": response.status, "body": response.body}


## Etapa real em que a criação está (`stage`, `stageLabel`, `stageIndex` de `stageCount`), e no fim `result` ou `error`.
func world_create_job(status_url: String) -> Dictionary:
	return await _fetch(status_url, "world-create-job")


func _running_query(start_running: bool) -> String:
	return "?startRunning=true" if start_running else ""


# --- retoques do mundo recém-criado (autoria) ---------------------------------

func power_catalog() -> Dictionary:
	return await _fetch("/authoring/extraordinary/catalog")


func grant_power(npc_id: int, power_id: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/authoring/npcs/%d/extraordinary/grant" % npc_id, {"powerId": power_id})
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


func rewrite_personality(npc_id: int, values: Dictionary) -> Dictionary:
	var response: Dictionary = await transport.request("PUT", "/authoring/npcs/%d/personality" % npc_id, values)
	return {"ok": response.status == 200, "status": response.status, "body": response.body}


func saved_worlds() -> Dictionary:
	return await _fetch("/worlds", "saved-worlds")


func load_saved_world(id: String, start_running := false) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/worlds/%s/load" % id.uri_encode() + _running_query(start_running))
	return _checked("/worlds/load", response, "")


## Novo nome (AD-175). Erro do servidor (vazio, longo demais, repetido) vem em `error`, já em frase para o jogador.
func rename_world(id: String, new_name: String) -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/worlds/%s/rename" % id.uri_encode(), {"name": new_name})
	var body = response.body
	var error := String(body.get("error", "")) if body is Dictionary else ""
	return {"ok": response.status == 200, "status": response.status, "body": body, "error": error}


## Grava o mundo aberto agora (AD-175); `body.savedAtUtc` diz quando.
func save_world() -> Dictionary:
	var response: Dictionary = await transport.request("POST", "/world/save")
	return _checked("/world/save", {"status": response.status, "body": response.body}, "world-save")


func delete_saved_world(id: String) -> Dictionary:
	var response: Dictionary = await transport.request("DELETE", "/worlds/%s" % id.uri_encode())
	return {"ok": response.status == 204, "status": response.status, "body": response.body}
