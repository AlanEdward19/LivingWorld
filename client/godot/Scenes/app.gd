extends Node
## Raiz do cliente (ADR-0047): liga transporte, leitor, store, registro de entidades, cena e módulos. Só leitura do
## mundo: o único que escreve no servidor são os comandos explícitos (relógio do admin, conversa, jogador).
## Módulos: Session (escopos), Selection (clique e dica), Inspector (ficha), WindowRouter (janelas que convivem),
## FollowDirector (seguir), AppUi (montagem da interface), WorldView (um renderizador por tipo).
const NpcLook := preload("res://Scenes/npc_look.gd")
const WorldTime := preload("res://Contract/world_time.gd")
const BodyBusy := preload("res://Scenes/body_busy.gd")

signal menu_requested() ## F10 ou "Voltar ao menu" na pausa: o Boot volta ao menu principal
signal quit_requested() ## "Sair do jogo" na pausa: o Boot fecha com calma

const CursorStream := preload("res://Contract/cursor_stream.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const WorldReader := preload("res://Contract/world_reader.gd")
const ScopeObserver := preload("res://Contract/scope_observer.gd")
const Store := preload("res://Store/world_store.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Registry := preload("res://Store/entity_registry.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")
const FamilyBuilder := preload("res://Scenes/family_builder.gd")
const AppUi := preload("res://Scenes/app_ui.gd")
const Shortcuts := preload("res://Scenes/shortcuts.gd")
const Session := preload("res://Scenes/session.gd")
const Selection := preload("res://Scenes/selection.gd")
const WindowRouter := preload("res://Scenes/window_router.gd")
const FollowDirector := preload("res://Scenes/follow_director.gd")
const Parse := preload("res://Store/observable_parse.gd")
const Grid := preload("res://Scenes/grid.gd")
const Season := preload("res://Scenes/render/season_palette.gd")
const WeatherLook := preload("res://Scenes/render/weather_look.gd")
const FrameCost := preload("res://Contract/frame_cost.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const MenuSteps := preload("res://Scenes/menu/menu_steps.gd")
const CityReading := preload("res://Scenes/city_reading.gd")
const RoadSync := preload("res://Scenes/road_sync.gd")
const RoadBook := preload("res://Store/road_book.gd")

var transport
var api ## WorldReader: a camada única de leitura (todo painel recebe este objeto)
var stream
var interior_stream ## #249: a casa em foco (ou a mais perto do corpo) tem assinatura própria: a do mundo/cidade segue viva ao entrar
var store
var labels
var registry
var session
var selection
var router
var director
var world_view
var rig
var stage
var day_cycle
var minimap
var toasts
var sky_life
var ambience
var catalog = AssetCatalog.load_default()
var clock_panel
var list_panel
var inspector
var npc_panel
var city_panel
var brain_panel
var conversation
var narrative
var player_hud
var feedback ## #232: sons, poeira e números do retorno de ação
var creator ## AD-197q: criador de personagem pelo motor
var admin_window
var family_builder
var economy
var technology
var timeline ## linha do tempo dos acontecimentos (EventTimeline)
var reading ## CityReading (AD-177): pilha sob o cursor (Tab), raio-x (X) e planta (P)
var road_sync ## RoadSync: relê a camada `Roads` sem recarregar a cena
var period_id := "" ## período de origem do mundo em andamento (GET /world/info); vazio se desconhecido
var admin_enabled := OS.get_environment("LW_ADMIN_TOKEN") != "" ## sem token o console nem aparece e o tempo fica travado
var pause_label: Label
var player_id := ""
var observer
var banner: Label
var breadcrumb: Label
var follow_label: Label
var time_label: Label
var top_bar: Control ## #222: a barra do espectador (some no modo jogador)
var player_frame ## #222: PlayerFrame
var aim_highlight ## #233: AimHighlight, o anel do alvo do E
var reach_ring ## #234: ReachRing, o alcance do golpe em volta do corpo quando mira alguém
var weather_label: Label ## AD-180: indicador do tempo do dia
var loading: Control
var tooltip: PanelContainer
var tooltip_label: Label
var hover_text := ""
var selected_npc := -1
var settings ## preferências locais (GameSettings) vindas do Boot; nulo nos testes e nas medidas
var pause_menu ## PauseMenu (Esc)
var modal ## #240: Modal, o registro único de popup aberto
var popup_hold ## #240: PopupHold, o pedido de pausa de popup ao motor
var first_steps ## FirstSteps: dicas da primeira vez e ajuda
var help_button: Button
var world_button: Button ## "◀ Mapa do mundo", só dentro de uma cidade
var curtain: ColorRect ## cortina que se abre ao chegar o primeiro retrato

var professions: Dictionary:
	get:
		return registry.professions
var skills: Dictionary:
	get:
		return registry.skills
var resource_names: Dictionary:
	get:
		return registry.resources

var _local_event_id := 0 ## evento sem id do servidor (servidor antigo): ids negativos locais
var contract_errors: Array = [] ## última divergência de contrato do stream (faixa do topo)
var _ui: Control
var _hover_clock := 0.0
var _sun: DirectionalLight3D
var _environment: Environment


## #256: veio do botão de início rápido (sandbox): ao abrir, o jogo já está no corpo de uma pessoa da casa, sem passo extra.
var quick_start := false
var quick_entered := false ## a entrada do início rápido terminou (entrou no corpo ou desistiu)
signal quick_entry_done


func _ready() -> void:
	var real := HttpTransport.new()
	add_child(real)
	await start(real)
	if quick_start:
		await enter_first_of_family()
	var steps := DevSteps.parse(OS.get_cmdline_user_args())
	if not steps.is_empty() and not MenuSteps.is_menu(steps): # roteiro do menu roda o resto ele mesmo
		await DevSteps.run(self, steps)
	elif steps.is_empty():
		await start_clock_on_entry()


## #237: entrar num mundo (Continuar ou Mundos salvos) deixa o tempo andando, como no mundo novo: o servidor abre o mundo salvo
## pausado, então quem tem o relógio solta aqui; quem não tem vê por que está parado e como soltar.
func start_clock_on_entry() -> void:
	if clock_panel == null or not bool(clock_panel.status.get("isPaused", false)) or clock_panel.status.get("pausedBy") != null:
		return # rodando, ou parado de propósito por um operador (a faixa do topo diz quem e por quê)
	if clock_panel.can_control:
		await clock_panel.command("resume")
		toasts.push("O tempo está correndo.")
	else:
		toasts.push("O mundo está parado. " + AppUi.NO_ADMIN)


## `p_transport`: HttpTransport em produção, FakeTransport nos testes.
func start(p_transport, validate_shapes: bool = true, sleeper: Callable = Callable()) -> void:
	transport = p_transport
	store = Store.new()
	registry = Registry.new(store)
	api = WorldReader.new(transport)
	api.validate_shapes = validate_shapes
	api.registry = registry
	labels = ActionLabels.new()
	session = Session.new(self)
	selection = Selection.new(self)
	router = WindowRouter.new()
	director = FollowDirector.new(self)
	AppUi.build_scene(self)
	AppUi.build(self)
	world_view.registry = registry
	reading = CityReading.new(self)
	reading.build(_ui)
	size_windows()
	observer = ScopeObserver.new()
	observer.api = api
	add_child(observer)
	stream = CursorStream.new(transport, get_tree())
	stream.validate_shapes = validate_shapes
	stream.delta_check_every = 30 if validate_shapes else 1 # AD-189: no jogo, amostra (os testes conferem tudo)
	if sleeper.is_valid():
		stream.sleeper = sleeper
	stream.snapshot_received.connect(_on_snapshot)
	stream.delta_applied.connect(_on_delta)
	store.interior_changed.connect(session.on_interior_changed)
	store.events_arrived.connect(_on_events)
	stream.connection_changed.connect(session.on_connection)
	stream.access_denied.connect(func(): banner.text = "acesso negado a este escopo"; banner.visible = true)
	stream.contract_error.connect(_on_contract_error)
	stream.scope_gone.connect(session.on_scope_gone)
	interior_stream = CursorStream.new(transport, get_tree())
	interior_stream.validate_shapes = validate_shapes
	interior_stream.delta_check_every = stream.delta_check_every
	if sleeper.is_valid():
		interior_stream.sleeper = sleeper
	interior_stream.snapshot_received.connect(_on_snapshot)
	interior_stream.delta_applied.connect(_on_delta)
	interior_stream.contract_error.connect(_on_contract_error)
	interior_stream.scope_gone.connect(session.on_scope_gone)
	road_sync = RoadSync.new()
	road_sync.setup(self)
	add_child(road_sync)
	var actions: Dictionary = await api.action_catalog()
	if actions.ok:
		labels.load_from(actions.body)
	var calendar: Dictionary = await api.world_calendar()
	if calendar.ok:
		day_cycle.set_calendar(calendar.body)
		player_hud.hours_per_day = int(day_cycle.hours_per_day) # AD-197c: a hora de acordar vai até o fim do dia do mundo
	await clock_panel.refresh()
	await _load_world_info()
	await stream.subscribe("World")


## Identidade do mundo (`GET /world/info`) e catálogo do período: nomes de ofício, habilidade, recurso e tipo de prédio.
func _load_world_info() -> void:
	var identity: Dictionary = await api.world_identity()
	Grid.use_scale(identity.get("scale", {})) # ADR-0052: andar, porta e janela nas medidas do motor
	period_id = identity.period_id
	NpcLook.set_era(NpcLook.era_for(String(identity.catalog.get("wardrobe", "")) if identity.catalog is Dictionary else "")) # #254: roupa do período (moderno x medieval)
	player_hud.world_key = String(identity.get("name", "")) if String(identity.get("name", "")) != "" else identity.period_id # #238
	if identity.period_id != "" or not identity.axes.is_empty():
		technology.axis_names = identity.axes


# --- relógio, eventos, nomes -----------------------------------------------

func _on_clock_status(status: Dictionary) -> void:
	api.set_tick(int(float(status.get("atMs", 0)) / 60000.0)) # #222: o cache da ficha vence por minuto do mundo (em tempo real a hora leva 1 h)
	day_cycle.sync(WorldTime.hours_f(status.get("atMs")), hours_per_second(status), clock_paused(status))
	brain_panel.on_world_tick(WorldTime.hours_of(status, "atMs"))
	player_hud.set_paused(bool(status.isPaused))
	player_hud.set_world_rate(hours_per_second(status)) # #241: a barra da ação em andamento segue o ritmo (acelerado até acabar)
	if player_hud.is_active: # AD-197c: o ritmo e os controles de dormir e acelerar, como o motor os diz
		player_hud.refresh_time()
	if not bool(status.isPaused):
		admin_window.note_running()
	pause_label.text = admin_window.pause_banner(status)
	pause_label.tooltip_text = pause_label.text
	NpcActor.frozen = clock_paused(status)
	world_view.npcs.clock.set_status(WorldTime.hours_of(status, "atMs"), hours_per_second(status), clock_paused(status)) # rota (AD-181); #222: pelo ritmo do motor
	sky_life.paused = clock_paused(status)
	_update_time_label(status)


## #222: quantas horas do mundo passam por segundo real, pelo ritmo que o motor diz (Δ por tick × ticks do laço). Com jogador o mundo
## anda em tempo real: estimar 1 hora por segundo (ticksPerSecond) adiantava o relógio e o motor o puxava para trás.
## #260: o fim do relógio do jogador: "· ⏸ pausado" (admin ou o jogador pausou) ou a velocidade quando passa de 1×.
static func speed_suffix(status: Dictionary) -> String:
	if clock_paused(status):
		return "  ·  ⏸ pausado"
	var speed := int(status.get("playerSpeed", 1)) if status.get("playerSpeed") != null else 1
	return "  ·  %d×" % speed if speed > 1 else ""


## #260: o relógio parado: a pausa do admin ou a do jogador (velocidade 0, ele sozinho no mundo).
static func clock_paused(status: Dictionary) -> bool:
	return bool(status.get("isPaused", false)) or (status.get("playerSpeed") != null and int(status.playerSpeed) == 0)


## #260: as luzes dos objetos da casa (abajur) acendem quando o motor diz que escureceu (o sol e o pôr do sol do motor, `day_cycle`) e há gente em casa: o
## corpo do jogador ou alguém que o motor põe lá dentro. Sem objeto de luz no catálogo não há o que acender.
const LAMP_FULL_BELOW := 0.15 ## luz do dia (0–1) abaixo da qual as luzes estão no máximo
const LAMP_OFF_ABOVE := 0.6 ## e acima dela apagam


static func lamp_level(daylight: float, occupied: bool) -> float:
	if not occupied:
		return 0.0
	return clampf((LAMP_OFF_ABOVE - daylight) / (LAMP_OFF_ABOVE - LAMP_FULL_BELOW), 0.0, 1.0)


func _update_house_lights() -> void:
	if stage == null or stage.interior_view == null or day_cycle == null:
		return
	var view = stage.interior_view
	var occupied: bool = stage.active and (view.immersive or not view.occupant_nodes.is_empty())
	view.set_lamp_level(lamp_level(day_cycle.daylight(), occupied))


## #260: ao entrar numa casa o olhar vai para onde há espaço (a direção mais livre até a parede), não para a parede de nariz: só gira se o olhar de agora
## bate numa parede a menos de 1,6 célula; quem já entra olhando para dentro do cômodo não é mexido.

static func yaw_toward(direction: Vector2) -> float:
	return atan2(-direction.x, -direction.y)


func face_into_room() -> void:
	if stage == null or not stage.active or rig == null or rig.mode == rig.ISO or player_hud == null:
		return
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node == null or not is_instance_valid(node):
		return
	var from := Vector2(node.global_position.x, node.global_position.z)
	var wanted: Vector2 = stage.interior_view.open_direction(from, rig.heading())
	if wanted != Vector2.ZERO:
		rig.yaw = yaw_toward(wanted)


## #260: V troca o modo das paredes da casa em que o corpo está (o aviso diz qual).
func cycle_walls() -> void:
	if stage == null or stage.interior_view == null or not stage.active:
		toasts.push("Sem paredes para cortar aqui: entre numa casa.")
		return
	var mode: int = stage.interior_view.cycle_wall_mode()
	toasts.push(String(stage.interior_view.WALL_MODE_TEXT[mode]))


## #260: o jogador mudou a velocidade do tempo: o relógio da tela relê o ritmo já (não extrapola o de antes).
func on_speed_changed() -> void:
	await clock_panel.refresh()


static func hours_per_second(status: Dictionary) -> float:
	if status.get("playerSpeed") != null and int(status.playerSpeed) == 0:
		return 0.0 # #260: o jogador pausou: o relógio não corre
	if status.get("tickDeltaMs") != null and status.get("loopTicksPerSecond") != null:
		return float(status.tickDeltaMs) / 3600000.0 * float(status.loopTicksPerSecond)
	return float(status.get("ticksPerSecond", 1.0))


func _update_time_label(status: Dictionary = {}) -> void:
	var s: Dictionary = status if not status.is_empty() else clock_panel.status
	if s.is_empty():
		return
	var date: Dictionary = day_cycle.date_parts(WorldTime.hours_f(s.get("atMs")))
	var weather := Season.hud_text(store.climate) if store != null else ""
	time_label.text = "Ano %d · Mês %d · Dia %d · %s · %s%s%s" % [int(s.year), date.month, date.day, day_cycle.clock_text(),
		day_cycle.phase_name(), (" · " + weather) if weather != "" else "", "  ⏸" if clock_paused(s) else ""]
	if player_frame != null: # #222: a hora do modo jogador, curta (dia, hora, fase e tempo)
		player_frame.set_clock("Dia %d · %s · %s%s%s" % [date.day, day_cycle.clock_text(), day_cycle.phase_name(),
			(" · " + weather) if weather != "" else "", speed_suffix(s)])


## Tempo sob a câmera (AD-180): sol pelo nascer e pôr do motor, céu encoberto e o indicador do topo.
func _on_weather(weather: Dictionary) -> void:
	day_cycle.set_weather(weather)
	var text := WeatherLook.hud(weather)
	weather_label.visible = text != ""
	weather_label.text = text
	weather_label.tooltip_text = WeatherLook.tooltip(weather)
	var alert := bool(weather.get("storm", false)) or bool(weather.get("heat_wave", false)) or bool(weather.get("cold_wave", false)) \
		or bool(weather.get("drought", false))
	weather_label.add_theme_color_override("font_color", Color("ffb070") if alert else Color("cfe3f0"))


func _exit_tree() -> void:
	NpcActor.frozen = false # o estado pausado é do servidor, não deve vazar para o próximo mundo


## #243: as árvores cortadas vêm do motor (`GET /world/trees`) a cada TREES_EVERY s (e logo que uma unidade de madeira sai); só reaplica
## nas malhas se a lista mudou. Nada por quadro além de somar o relógio.
const TREES_EVERY := 5.0
var _trees_clock := 0.0
var _trees_pending := false
var _trees_json := ""


func refresh_trees() -> void:
	if _trees_pending or api == null or world_view == null or world_view.terrain == null:
		return
	_trees_pending = true
	_trees_clock = 0.0
	var response: Dictionary = await api.world_trees()
	_trees_pending = false
	if not response.ok or not (response.body is Dictionary):
		return
	var list: Array = response.body.get("trees", [])
	var json := JSON.stringify(list)
	if json == _trees_json:
		return
	_trees_json = json
	world_view.terrain.apply_trees(list)


func _process(delta: float) -> void:
	_step_glide(delta)
	_trees_clock += delta
	if _trees_clock >= TREES_EVERY:
		refresh_trees()
	if player_hud != null and player_hud.is_active:
		_player_view_effects()
	elif not _eye_hidden.is_empty() or _marked != -1:
		_player_view_effects()
	if time_label != null and day_cycle != null:
		_update_time_label()
	_update_house_lights()
	if world_view != null and rig != null:
		world_view.view_center = rig.target
		world_view.view_radius = rig.zoom_size * 1.5
		world_view.zoom_size = rig.zoom_size
		world_view.far_zoom = rig.zoom_size > 16.0
		world_view.bubble_zoom = store.kind == "World" and rig.zoom_size > rig.bubble_size
		world_view.set_lod(WorldView.lod_for(store.kind if not stage.active else "Interior", rig.zoom_size, rig.mode != rig.ISO))
		world_view.followed_npc = int(director.target.id) if director != null and String(director.target.get("kind", "")) == Registry.PERSON else -1
	if world_button != null:
		world_button.visible = store.kind == "City" and not stage.active
	if minimap != null:
		minimap.set_camera_target(rig.target)
		ambience.set_daylight(day_cycle.daylight())
	if director != null:
		director.watch_step(delta)
		director.step(delta)
		follow_label.text = director.label()
	_hover_clock += delta
	if _hover_clock >= 0.07:
		_hover_clock = 0.0
		_update_hover()


func _on_delta(envelope: Dictionary) -> void:
	store.apply_delta(envelope)


## Todo evento entra na linha do tempo e vira aviso; com lugar, também marcador clicável no mapa. Crime só de registro
## (`recordOnly`) aparece como registro estruturado (autor, vítima, caso, desfecho), nunca com o texto narrado.
func _on_events(events: Array) -> void:
	var started := Time.get_ticks_usec()
	var ids: Array = []
	for raw in events:
		_local_event_id -= 1
		# obra de rua (AD-181) vem sem lugar: o marcador vai para as células da obra que o motor mandou nas ruas
		var event := RoadBook.place_event(Parse.event(raw, _local_event_id), store.roads, store.finished_works)
		registry.remember_event(event)
		toasts.record(registry.event_title(event)) # #222: acontecimento não vira aviso no meio da tela; a lista conta os novos
		world_view.events.add(event)
		ids.append(int(event.id))
	if timeline != null:
		timeline.refresh()
		if player_mode:
			timeline.visible = false # AD-197p: a lista de acontecimentos é do espectador
	director.on_events(ids)
	FrameCost.add("events", started) # AD-189: diagnóstico do bench


## Snapshot ou delta fora do contrato congelado (`tests/contracts/shape.*.json`) é descartado pelo stream: em vez de ficar
## em "Conectando…" sem explicação, a faixa diz o primeiro campo divergente e o console guarda a lista.
func _on_contract_error(errors: Array) -> void:
	contract_errors = errors
	banner.text = "resposta do servidor fora do contrato (%d divergência%s): %s" % [errors.size(), "s" if errors.size() != 1 else "",
		String(errors[0]) if not errors.is_empty() else ""]
	banner.visible = true
	push_warning("contrato: " + ", ".join(errors.slice(0, 5)))


## Nomes vêm do escopo (`observable.name`, AD-170): só reaplica nos corpos da cena (sem paginar `GET /npcs`).
func _load_names() -> void:
	world_view.apply_names()
	if stage.active:
		stage.interior_view.apply_names(store.names)


## Linha do tempo e atalho N: câmera no lugar do evento e ficha aberta.
func goto_event(id: int) -> void:
	director.goto_event(id)


func next_event() -> void:
	director.next_event()


func toggle_watch() -> void:
	director.set_watching(not director.watching)
	timeline.set_watching(director.watching)
	timeline.visible = true


## Design de uma invenção (`/designs/{id}`), lido uma vez: o recipiente na mão e a peça no chão ganham forma e cor.
func request_design(id: int) -> void:
	if api.cached_design(id) != null:
		return
	var response: Dictionary = await api.design(id)
	if response.ok:
		world_view.items.rebuild()
		for npc_id in store.npcs:
			if int(store.npcs[npc_id].get("obs", {}).get("held", {}).get("design_id", -1)) == id:
				world_view.npcs.apply_observable(int(npc_id))


## Primeiro retrato na tela: a cortina abre devagar e, num jogo de verdade, as dicas da primeira vez começam.
func on_world_shown() -> void:
	if curtain != null:
		curtain.visible = true
		curtain.modulate.a = 1.0
		var tween := curtain.create_tween()
		tween.tween_property(curtain, "modulate:a", 0.0, 0.6)
		tween.tween_callback(func(): curtain.visible = false)
	if settings != null and first_steps != null and DevSteps.parse(OS.get_cmdline_user_args()).is_empty():
		first_steps.start()


# --- escopos (Session) ------------------------------------------------------

func enter_scope(kind: String, ref_id: String) -> void:
	await session.enter_scope(kind, ref_id)


func _on_snapshot(envelope: Dictionary) -> void:
	session.on_snapshot(envelope)


func release_observation() -> void:
	if player_hud != null and player_hud.is_active: # #222: fechar o jogo ou voltar ao menu sai do personagem (senão o motor segue com
		await player_hud.logout()                    # o jogador presente e o mundo fica preso no tempo real)
	if conversation != null:
		await conversation.end_conversation()
	if observer != null:
		await observer.clear()


func enter_building(building_id: int) -> void:
	await session.enter_building(building_id)


func exit_building() -> void:
	await session.exit_building()


func city_center() -> Vector3:
	return Session.center_of(session.built_area())


# --- seleção, inspetor e seguir ---------------------------------------------

func select_npc(id: int) -> void:
	selected_npc = id
	await inspector.inspect(Registry.ref(Registry.PERSON, id))
	if brain_panel.is_open:
		open_brain(id)


## Abre a ficha de qualquer entidade (pessoa vira também a selecionada: B, T, I e F agem sobre ela).
func inspect(entity: Dictionary) -> void:
	if entity.is_empty():
		return
	if String(entity.kind) == Registry.PERSON:
		await select_npc(int(entity.id))
	else:
		await inspector.inspect(entity)


func follow(entity: Dictionary) -> void:
	if not entity.is_empty():
		director.follow(entity)


func unfollow() -> void:
	director.release()


func follow_selected() -> void:
	if selected_npc >= 0:
		follow(Registry.ref(Registry.PERSON, selected_npc))


## Onde a entidade está na cena agora (para "Ir até lá"); INF se não está desenhada.
func point_of(entity: Dictionary) -> Vector3:
	var id = entity.get("id")
	match String(entity.get("kind", "")):
		Registry.BUILDING:
			return world_view.building_center(int(id))
		Registry.CROP:
			return world_view.crops.positions.get(int(id), Vector3.INF)
		Registry.ANIMAL:
			return world_view.fauna.positions.get(int(id), Vector3.INF)
		Registry.FAUNA:
			return world_view.fauna.group_positions.get(String(id), Vector3.INF)
		Registry.LEAVING:
			return world_view.items.positions.get("leaving:%d" % int(id), Vector3.INF)
		Registry.ITEM:
			return world_view.items.positions.get("item:%d" % int(id), Vector3.INF)
		Registry.EVENT:
			var event: Dictionary = registry.events.get(int(id), {})
			return Grid.to_world(event.cell) + Vector3(0.5, 0, 0.5) if bool(event.get("has_cell", false)) else Vector3.INF
		Registry.STREET:
			var cell := Registry.street_cell(String(id))
			return Grid.to_world(cell) if store.roads.has(cell) else Vector3.INF
		Registry.WORK: # AD-193: o canteiro da obra, senão a célula do lote
			var work = store.works.get(int(id))
			if work == null:
				return Vector3.INF
			var at: Vector3 = world_view.building_center(int(work.building_id)) if int(work.building_id) >= 0 else Vector3.INF
			return at if at != Vector3.INF else Grid.to_world(work.cell)
	return Vector3.INF


func focus_point(point: Vector3) -> void:
	if point != Vector3.INF:
		rig.focus(point)


func _highlight(id: int) -> void:
	selected_npc = id
	world_view.highlight_npc(id)


## Profissão e idade chegaram com a ficha: o corpo do NPC na cena (e no interior aberto) usa o modelo.
func _apply_model(id: int, profession: int, age_years: float) -> void:
	for actor in [world_view.npc_nodes.get(id), stage.interior_view.occupant_nodes.get(id)]:
		if actor != null:
			actor.apply_model(catalog, profession, age_years)


## NPC em foco mudou de ação: toca o som do catálogo (só o selecionado, para não virar barulho de multidão).
func _on_focus_action(id: int, _from: Vector2i, _to: Vector2i, action: int) -> void:
	if id == selected_npc:
		ambience.play_action(action)


func describe_hit(meta: Dictionary) -> String:
	return selection.describe(meta)


func nearest_npc(screen_position: Vector2, max_distance: float = 32.0) -> int:
	return selection.nearest_npc(screen_position, max_distance)


func _update_hover() -> void:
	if player_mode: # AD-197p: a mira diz o que o E alcançaria; a dica grande do espectador não aparece
		tooltip.visible = false
		return
	selection.update_hover()


## Clique simples abre a ficha; duplo clique entra (prédio ou cidade).
func _pick(screen_position: Vector2, double_click: bool) -> void:
	selection.pick(screen_position, double_click)


func _show_city_summary(city_id: String) -> void:
	await inspector.inspect(Registry.ref(Registry.CITY, city_id))


# --- janelas (WindowRouter) -------------------------------------------------

func _info_windows() -> Array:
	return [brain_panel, narrative, economy, technology, admin_window]


## Compatibilidade com quem pedia "só uma": agora as janelas convivem; `keep` só vem para a frente.
func _exclusive(keep) -> void:
	if keep != null:
		router.opened(keep)


func size_windows() -> void:
	for window in _info_windows():
		window.custom_minimum_size.y = 220
	router.layout()


func _show_technologies(culture_id: int) -> void:
	await technology.show_culture(culture_id)
	router.opened(technology)


func open_brain(id: int) -> void:
	await brain_panel.open(id)
	router.opened(brain_panel)


func _toggle_admin() -> void:
	if not admin_enabled:
		return
	if admin_window.is_open:
		admin_window.close()
		router.closed(admin_window)
	else:
		admin_window.open()
		router.opened(admin_window)


## Mostra o botão de tecnologias só se a cultura do NPC selecionado já descobriu algo.
func _probe_technologies(culture_id: int) -> void:
	var response: Dictionary = await api.technologies(culture_id)
	if culture_id == npc_panel.culture_id:
		npc_panel.tech_button.visible = response.ok and not response.body.is_empty() and npc_panel.lod != 2


func open_treasury(city_id: String) -> void:
	if city_id != "":
		await economy.show_treasury(city_id, city_panel.city_name)
		router.opened(economy)


func open_trades(workplace_id: int) -> void:
	if workplace_id >= 0:
		await economy.show_trades(workplace_id, registry.title_of(Registry.ref(Registry.WORKPLACE, workplace_id)))
		router.opened(economy)


func open_biography(id: int) -> void:
	await narrative.show_biography(id, registry.person_name(id))
	router.opened(narrative)


func open_chronicle(city_id: String) -> void:
	if city_id != "":
		await narrative.show_chronicle(city_id, city_panel.city_name)
		router.opened(narrative)


func open_beliefs(city_id: String) -> void:
	if city_id != "":
		await narrative.show_beliefs(city_id, city_panel.city_name)
		router.opened(narrative)


func _toggle_reports() -> void:
	if narrative.is_open and narrative.mode == "reports":
		narrative.close()
		router.closed(narrative)
	else:
		narrative.show_reports()
		router.opened(narrative)


func _toggle_brain() -> void:
	if brain_panel.is_open:
		brain_panel.close()
		router.closed(brain_panel)
	elif selected_npc >= 0:
		open_brain(selected_npc)


# --- jogador, conversa, criação ---------------------------------------------

## Encarna o NPC (atalho I): o servidor decide se pode; a câmera passa a segui-lo.
func incarnate(id: int) -> void:
	await resume_for_play()
	if await player_hud.incarnate(id, registry.person_name(id)):
		await clock_panel.refresh() # #256: com jogador o ritmo muda; o relógio da tela relê já, não extrapola o ritmo velho até o próximo ciclo
		await _drive(id)


## #256: início rápido: entra no corpo da primeira pessoa da família autorada (a de menor id: o motor numera pela ordem em que as
## pessoas da casa são criadas), no modo de posse do jogador (plena com o token de admin, comum sem ele). O G troca para a outra.
func enter_first_of_family() -> bool:
	var page: Dictionary = await api.npc_page("", 0, 50, "")
	var first := -1
	var first_name := ""
	if page.ok and page.body is Dictionary:
		for item in page.body.get("items", []):
			var id := int(item.id.value)
			if first < 0 or id < first:
				first = id
				first_name = String(item.name)
	if first >= 0:
		store.names[first] = first_name
		await select_npc(first)
		await incarnate(first)
	quick_start = false
	quick_entered = true
	quick_entry_done.emit()
	return first >= 0 and player_hud.is_active


## #222: quem vai jogar quer o tempo correndo: com o mundo pausado e o relógio nas mãos (jogo local, admin), o tempo volta a andar
## antes de encarnar ou criar; sem o relógio, o motor recusa e o aviso diz o porquê.
func resume_for_play() -> void:
	if clock_panel == null or not clock_panel.can_control:
		return
	var status: Dictionary = clock_panel.status
	if status.has("isPaused") and bool(status.isPaused):
		await clock_panel.command("resume")
		toasts.push("O tempo voltou a correr para você jogar.")


## AD-197q: abre o criador de personagem (a pessoa nova sai do agregado de uma cidade, pelo motor).
func open_creator() -> void:
	if player_hud.is_active:
		toasts.push("Você já controla alguém: saia do personagem antes de entrar como pessoa nova.")
		return
	await resume_for_play() # #222: o criador cria pelo motor, que recusa com o mundo pausado
	creator.reveal()
	router.opened(creator) # #222: janela central sobre a tela toda (fora do encaixe); Esc a fecha como as demais
	await creator.open()


## AD-197q: entrou como pessoa nova: a cidade dela, o corpo que o motor materializou e a 3ª pessoa, como ao encarnar.
func _on_created(id: int, name: String, city_id: String) -> void:
	store.names[id] = name
	clock_panel.refresh() # #256
	if session.world_ref != ["City", city_id]:
		await enter_scope("City", city_id)
	await _drive(id)


## O corpo encarnado: a câmera o segue em 3ª pessoa e o HUD mostra os poderes que ele porta.
func _drive(id: int) -> void:
	selected_npc = id
	follow(Registry.ref(Registry.PERSON, id))
	# AD-197s: a pessoa recém-materializada só chega ao desenho no próximo delta: até lá a câmera do jogador fica no alto (sem corpo não
	# há enquadramento), e `follow_director` a prende ao corpo, já no enquadramento certo, no quadro em que ele chegar
	var body = world_view.npc_nodes.get(id)
	if body != null:
		body.driven = true
		body.set_self(true) # AD-197p: sem nome, ação nem anel sobre o próprio corpo
		rig.follow(body) # já com o corpo na mão: o enquadramento nasce nele
	rig.set_mode(rig.THIRD) # AD-197d: encarnado, 3ª pessoa (C troca para a 1ª); espectador e admin seguem na isométrica
	var detail: Dictionary = await api.npc_detail(id) # AD-196: os poderes que o NPC porta viram botões do HUD
	if detail.ok and detail.body is Dictionary:
		player_hud.set_powers(detail.body.get("powerIds", []))


## #252 (sandbox do casal): trocou de corpo. O motor soltou a pessoa de antes SEM fazê-la sumir (ela volta à vontade dela e a figura segue
## no mundo, só deixa de ser a conduzida) e agora conduzimos a outra. A câmera desliza da primeira figura até a segunda (um ponto de apoio
## que anda entre as duas) e só então segue o corpo novo: sem corte seco, sem segundo jogador.
const SWITCH_GLIDE_S := 0.8
var _glide_t := -1.0 ## segundos desde o início do deslize (< 0: sem deslize)
var _glide_from := Vector3.ZERO
var _glide_anchor: Node3D = null


func _on_player_switched(old_id: int, new_id: int) -> void:
	clock_panel.refresh() # #256: o ritmo do relógio é relido na hora da troca
	var old_node = world_view.npc_nodes.get(old_id)
	if old_node == null and stage != null and stage.active:
		old_node = stage.interior_view.occupant_nodes.get(old_id)
	if stage != null:
		stage.interior_view.release_guided() # a figura adotada pela casa volta a ser só do mundo
	_glide_from = rig.target
	unfollow() # o diretor de seguir ainda tem a pessoa de antes: sem solta-la, ele puxaria a câmera de volta ao quadro seguinte
	if old_node != null and is_instance_valid(old_node):
		old_node.driven = false
		old_node.set_self(false)
		old_node.set_first_person(false)
	selected_npc = new_id
	if rig.mode == rig.ISO or _glide_from == Vector3.ZERO:
		await _drive(new_id)
		return
	_glide_anchor = Node3D.new()
	_glide_anchor.name = "SwitchGlide"
	_glide_anchor.position = _glide_from
	var old_scale: Vector3 = old_node.scale if old_node != null and is_instance_valid(old_node) else Vector3.ONE
	_glide_anchor.scale = old_scale
	add_child(_glide_anchor)
	rig.follow(_glide_anchor)
	_glide_t = 0.0
	var detail: Dictionary = await api.npc_detail(new_id) # os poderes da pessoa nova viram botões do HUD
	if detail.ok and detail.body is Dictionary:
		player_hud.set_powers(detail.body.get("powerIds", []))


## O deslize a cada quadro: o ponto de apoio sai de onde a câmera estava e chega ao corpo novo (onde ele estiver, mesmo andando).
func _step_glide(delta: float) -> void:
	if _glide_t < 0.0:
		return
	_glide_t += delta
	var actor: Node3D = player_actor() if player_hud != null and player_hud.is_active else null
	var dest: Vector3 = actor.position if actor != null else (Vector3(player_hud.fine.x * Grid.CELL, _glide_from.y, player_hud.fine.y * Grid.CELL)
		if player_hud != null and player_hud.fine != Vector2.INF else _glide_from)
	var k := smoothstep(0.0, 1.0, clampf(_glide_t / SWITCH_GLIDE_S, 0.0, 1.0))
	if _glide_anchor != null and is_instance_valid(_glide_anchor):
		_glide_anchor.position = _glide_from.lerp(dest, k)
		if actor != null:
			_glide_anchor.scale = _glide_anchor.scale.lerp(actor.scale, k)
	if _glide_t >= SWITCH_GLIDE_S and (actor != null or player_hud.fine == Vector2.INF):
		_end_glide()


func _end_glide() -> void:
	_glide_t = -1.0
	if _glide_anchor != null and is_instance_valid(_glide_anchor):
		_glide_anchor.queue_free()
	_glide_anchor = null
	if player_hud != null and player_hud.is_active:
		follow(Registry.ref(Registry.PERSON, player_hud.npc_id)) # o diretor de seguir só entra depois do deslize (ele puxaria a câmera já)
		var actor: Node3D = player_actor()
		if actor != null:
			actor.driven = true
			actor.set_self(true)
			actor.set_first_person(rig.mode == rig.FIRST)
			rig.follow(actor)


## Saiu do personagem: a câmera volta à isométrica do espectador e para de segui-lo.
func player_released() -> void:
	if _glide_t >= 0.0: # #252: saiu no meio do deslize da troca de corpo
		_glide_t = -1.0
		if _glide_anchor != null and is_instance_valid(_glide_anchor):
			_glide_anchor.queue_free()
		_glide_anchor = null
	if stage != null:
		stage.interior_view.release_guided() # #227: o corpo guiado da casa não fica como fantasma
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null:
		node.driven = false
		node.set_self(false)
		node.set_first_person(false)
		if node.has_meta("orphan"):
			world_view.npcs.on_removed(player_hud.npc_id) # #249: o corpo que sobrou de outro escopo some ao soltar a posse
	unfollow()
	rig.set_mode(rig.ISO)
	set_player_mode(false)


## #229: o que o corpo do jogador pode tocar ao redor de `center` (células): paredes de prédios num raio curto e quem está nas
## células vizinhas. Nada de colisor de cidade inteira nem de NPC longe: custo ~ vizinhança, lido a cada 0,25 s ou ao mudar de célula.
const NEAR_REACH := 4.0 ## células ao redor do corpo
func player_neighborhood(center: Vector2) -> Array:
	var obstacles: Array = []
	var people: Array = []
	if stage != null and stage.active: # #226/#227: dentro da casa, os painéis finos da planta do motor e quem está no andar
		return [stage.interior_view.obstacles_near(center, NEAR_REACH), stage.interior_view.people_near(center, 2.0, player_hud.npc_id)]
	if world_view == null or world_view.buildings == null:
		return [obstacles, people]
	for id in world_view.buildings.nodes:
		var node: Node = world_view.buildings.nodes[id]
		var box: AABB = node.get_meta("box", AABB())
		if box.size == Vector3.ZERO or center.x < box.position.x - NEAR_REACH or center.x > box.end.x + NEAR_REACH \
				or center.y < box.position.z - NEAR_REACH or center.y > box.end.z + NEAR_REACH:
			continue
		for wall: AABB in node.get_meta("solid", []):
			if absf(wall.position.x + wall.size.x / 2.0 - center.x) <= NEAR_REACH and absf(wall.position.z + wall.size.z / 2.0 - center.y) <= NEAR_REACH:
				obstacles.append([Rect2(wall.position.x, wall.position.z, wall.size.x, wall.size.z), wall.size.y])
	var base := Vector2i(floori(center.x), floori(center.y))
	for dx in range(-1, 2):
		for dy in range(-1, 2):
			for id in world_view.npcs.cell_members.get(base + Vector2i(dx, dy), []):
				var other = world_view.npc_nodes.get(id)
				if other != null and id != player_hud.npc_id:
					people.append(Vector2(other.position.x, other.position.z) / Grid.CELL)
	return [obstacles, people]


## AD-197k: a posição fina do corpo (prevista ou a do motor) move o boneco do jogador.
func _on_player_fine(at: Vector2) -> void:
	var node = player_actor()
	if node != null:
		if not node.driven:
			node.set_self(true) # AD-197q: a pessoa nova chega ao desenho depois de entrar (corpo materializado agora)
			node.set_first_person(rig.mode == rig.FIRST) # AD-197s: e a própria cabeça some se já está na 1ª pessoa
		node.driven = true
		node.set_fine(at)


## AD-197k: menu de interações ou conversa abertos soltam o cursor; fechados (ainda no modo jogador), prendem de novo.
func _on_player_menu(open: bool) -> void:
	if rig.mode != rig.ISO:
		rig.set_cursor_captured(not open)
	NpcActor.labels_muted = open # #260: a roda aberta esconde o nome sobre a pessoa
	for node in world_view.npc_nodes.values():
		if is_instance_valid(node):
			node._refresh_label()


var _predicted_from := Vector2i.ZERO ## a célula de antes do passo previsto (a recusa volta para ela)


## AD-197d: o passo anda já na tela (previsão) e o motor confirma ou corrige.
func _on_player_predicted(delta: Vector2i) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node == null:
		return
	_predicted_from = node.cell
	node.move_to(node.cell + delta, player_hud.step_seconds)


func _on_player_confirmed(x: int, y: int) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	_predicted_from = Vector2i(x, y)
	if node != null and node.cell != Vector2i(x, y):
		node.move_to(Vector2i(x, y), 0.08) # o motor diz onde o corpo está: a previsão errada é corrigida


func _on_player_rejected() -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null and node.cell != _predicted_from:
		node.move_to(_predicted_from, 0.08)


## #233: o alvo do E sob a mira ganha o anel (um só por vez); sem alvo ou fora do modo jogador, some.
func _on_player_target(kind: String, id: int, cell: Vector2) -> void:
	if aim_highlight == null:
		return
	if modal != null and modal.active: # #240: popup aberto: sem anel nem alcance, sem alvo
		aim_highlight.clear()
		if reach_ring != null:
			reach_ring.clear()
		return
	var own = world_view.npc_nodes.get(player_hud.npc_id) if player_hud != null else null
	if reach_ring != null: # #234: o anel de alcance do golpe só enquanto a mira está numa pessoa
		if kind == "person" and own != null and cell != Vector2.INF:
			reach_ring.show_around(own, player_hud.combat.reach_m)
		else:
			reach_ring.clear()
	if kind == "" or cell == Vector2.INF or own == null:
		aim_highlight.clear()
		return
	var person = world_view.npc_nodes.get(id) if kind == "person" else null
	aim_highlight.show_at(kind, id, aim_highlight.spot(cell, person.position if person != null else null, own.position.y))


## AD-197j: a mira do modo jogador é o centro da tela.
func aim_point() -> Vector2:
	return get_viewport().get_visible_rect().size / 2.0


## AD-197j: o golpe saiu: o braço do personagem desce (a mesma pose para quem olha de fora).
## #224: verbo aceito pelo motor -> gesto no corpo (o mesmo em 1ª e 3ª pessoa).
const GESTURE_BY_VERB := {"take": "reach", "collect": "reach", "gather": "reach", "fill": "reach", "steal": "reach", "deposit": "give", "return": "give",
	"buy": "give", "sell": "give", "abandon": "give", "drink": "drink", "eat": "eat", "enter": "push", "exit": "push", "up": "climb",
	"down": "climb", "talk": "talk", "socialize": "talk", "work": "work", "sleep": "rest", "idle": "rest"}


const DEFENSE_BY_OUTCOME := {"Hit": "hurt", "Dodged": "dodge", "Blocked": "block", "Missed": "dodge"} ## #234: resultado do motor -> reação no corpo


func _on_player_acted(verb: String) -> void:
	var node = player_actor()
	if node != null and GESTURE_BY_VERB.has(verb):
		node.play_gesture(GESTURE_BY_VERB[verb])


## #241: a ação que ocupa o corpo (cortar lenha...): o gesto da ação em laço enquanto o motor diz que dura; "" (acabou ou parou) o solta.
func _on_player_busy(action: String, seconds: float) -> void:
	var node = player_actor()
	if node == null:
		return
	var scene := action != "" and (action.begins_with("use:") or action.begins_with("social:")) # inclui o "ir até" (o outro corpo já entra no quadro)
	rig.set_scene_framing(scene, _actor_by_id(player_hud.busy.partner) if scene and action.begins_with("social:") else null) # #246: enquadra os corpos da cena
	if action == "":
		node.stop_gesture()
	else:
		node.play_loop(BodyBusy.gesture_for(action), seconds)
		if action.begins_with("use:") and action != "use:walk": # #255: no lugar de uso o corpo olha para onde o catálogo manda (sem tocar no rig)
			node.rotation.y = deg_to_rad(player_hud.busy.facing)
		if action.begins_with("social:") and action != "social:walk": # #259: os dois se viram um para o outro e cada um faz o gesto do papel dele
			_face_each_other(node, player_hud.busy.partner, player_hud.busy.partner_pose, seconds)


## #259: a figura do outro corpo da interação (na rua ou dentro da casa em foco).
func _actor_by_id(id: int) -> Node3D:
	for actor in [world_view.npc_nodes.get(id), stage.interior_view.occupant_nodes.get(id) if stage != null else null]:
		if actor != null and is_instance_valid(actor):
			return actor
	return null


## #259: `node` olha para a figura `other_id` (e ela para `node`), e a outra faz o gesto dela em laço; o motor diz quem, o gesto e por quanto tempo.
func _face_each_other(node: Node3D, other_id: int, other_pose: String, seconds: float) -> void:
	var other := _actor_by_id(other_id)
	if other == null or other == node:
		return
	var away: Vector3 = other.position - node.position
	if Vector2(away.x, away.z).length() > 0.01:
		node.rotation.y = atan2(away.x, away.z)
		other.rotation.y = atan2(-away.x, -away.z)
	var gesture := BodyBusy.gesture_for("social:" + other_pose) if other_pose != "" else ""
	if gesture != "":
		other.play_loop(gesture, seconds)


func _on_player_jumped(air_s: float) -> void:
	var node = player_actor()
	if node != null:
		node.play_jump(air_s)


func _on_player_struck(_target: int) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null:
		node.play_strike()


## #234: o que o motor respondeu ao golpe vira reação no alvo (ferida, esquiva ou bloqueio distintos) e, num acerto, o alvo olha para quem bateu.
func _on_player_struck_result(target: int, body: Dictionary) -> void:
	var node = world_view.npc_nodes.get(target)
	if node != null:
		node.play_defense(DEFENSE_BY_OUTCOME.get(String(body.get("outcome", "Hit")), "hurt"))


## #234: a defesa aceita: a esquiva desvia o corpo; o bloqueio levanta a guarda (fica enquanto o motor diz `guard`).
func _on_player_defended(kind: String) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node == null:
		return
	if kind == "Dodge":
		node.play_defense("dodge")
	else:
		node.set_guard(true)


func _on_player_guard_changed(kind: String) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null:
		node.set_guard(kind == "Block")


## #234: golpe recebido pelo corpo (`lastIncoming`): quem bateu golpeia (revide animado), o corpo sente (ferida, esquiva ou bloqueio).
func _on_player_incoming(outcome: String, _damage: int, by: int) -> void:
	var attacker = world_view.npc_nodes.get(by)
	if attacker != null and not attacker.striking():
		attacker.play_strike()
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null and outcome != "Missed":
		node.play_defense(DEFENSE_BY_OUTCOME.get(outcome, "hurt"))


## #240: abriu ou fechou o último popup: a mira, o anel, a dica do E e os números flutuantes saem do centro e voltam ao fechar.
func _on_modal(active: bool) -> void:
	if crosshair != null:
		crosshair.visible = rig.mode != rig.ISO and not active
	if player_frame != null:
		player_frame.blocked = active
	if player_hud != null:
		player_hud.reach_hint.visible = player_hud.reach_hint.visible and not active
		if active:
			if aim_highlight != null:
				aim_highlight.clear()
			if reach_ring != null:
				reach_ring.clear()
		else:
			player_hud.reach_clock = player_hud.REACH_HINT_EVERY # o alvo volta já na próxima leitura
			player_hud._reach_at = Vector2.INF
	if feedback != null:
		feedback.suppressed = active


## AD-197j: a mira (uma cruz no centro) só no modo jogador.
func _on_camera_mode(mode: String) -> void:
	if crosshair != null:
		crosshair.visible = mode != rig.ISO and not (modal != null and modal.active)
	set_player_mode(mode != rig.ISO and player_hud != null and player_hud.is_active)
	if player_hud != null and player_hud.is_active: # AD-197r: o olho fica na altura da cabeça; na 1ª pessoa a própria cabeça não se desenha
		var own = world_view.npc_nodes.get(player_hud.npc_id)
		if own != null:
			own.set_first_person(mode == rig.FIRST)


## AD-197p: no modo jogador some o que é do espectador (inspetor e lista à direita, acontecimentos, minimapa, avisos de
## acontecimento, dica grande sob o cursor, atalhos do espectador); ficam o topo compacto do HUD, a mira com o nome curto do que o E
## alcançaria (da oferta do motor) e as dicas do jogador. Ao sair, tudo volta como estava.
var player_mode := false
var minimap_wanted := false ## #222: o minimapa só aparece quando pedido (K)
var _timeline_was_visible := false


const KEY_HINTS_FADE_AFTER_S := 20.0 ## #246: as duas linhas de teclas ficam cheias no começo e depois recuam (F1 e o guia seguem explicando)
const KEY_HINTS_DIM_ALPHA := 0.28


## #246: a faixa de teclas do rodapé (13 atalhos) poluía a tela para sempre; depois de alguns segundos de jogo ela esmaece e deixa de competir com a cena.
func fade_key_hints(after: float = KEY_HINTS_FADE_AFTER_S, duration: float = 1.5) -> void:
	if player_frame == null or player_frame.hints == null:
		return
	var hints: Label = player_frame.hints
	hints.modulate.a = 1.0
	await get_tree().create_timer(after).timeout
	if player_mode and is_instance_valid(hints) and hints.is_inside_tree():
		hints.create_tween().tween_property(hints, "modulate:a", KEY_HINTS_DIM_ALPHA, duration)


## #246: o nome do bairro como aviso discreto ao entrar na área (como o Sims), no lugar da placa 3D.
func announce_area() -> void:
	if store != null and String(store.kind) == "City" and String(store.city.get("name", "")) != "":
		toasts.push(String(store.city.name))


func set_player_mode(on: bool) -> void:
	if on == player_mode:
		player_hud.set_rpg(on)
		return
	player_mode = on
	player_hud.set_rpg(on)
	if side_panel != null:
		side_panel.visible = not on
	if list_panel != null and on:
		list_panel.visible = false
	if minimap != null:
		minimap.visible = not on and minimap_wanted
	if timeline != null:
		if on:
			_timeline_was_visible = timeline.visible
			timeline.visible = false
		else:
			timeline.visible = _timeline_was_visible
	if tooltip != null and on:
		tooltip.visible = false
	if world_view != null and world_view.events != null:
		world_view.events.visible = not on # AD-197p: os marcadores de acontecimento (alfinetes) são do espectador
	if help_label != null:
		help_label.text = AppUi.PLAYER_HELP if on else AppUi.HELP
		help_label.visible = not on # #222: a moldura mostra as teclas do jogador
	if top_bar != null:
		top_bar.visible = not on # #222: velocidades, passo e ano são do espectador; a moldura mostra a hora
	if world_view != null and world_view.cities != null:
		world_view.cities.set_labels_visible(not on) # #246: a placa 3D da cidade ("1 na cidade · 0 na rua · parado 1") cobria a tela em 3ª pessoa
	if on:
		announce_area()
		fade_key_hints()
	if on and world_view != null:
		world_view.hover_npc(-1) # #222: o nome grande de quem estava sob o cursor não fica preso na tela
	if on:
		_update_time_label()
	if on:
		toasts.clear_events()
	# AD-197p: o HUD do jogador vai para o canto, sem a coluna da lista à esquerda nem o inspetor à direita; os avisos, discretos,
	# centralizados acima do rodapé (não cobrem a mira nem se sobrepõem a painel)
	if on: # #222: as ações (Tab) abrem no meio da tela, por cima da cena
		player_hud.set_anchors_preset(Control.PRESET_CENTER_TOP)
		player_hud.offset_left = -300
		player_hud.offset_right = 300
		player_hud.offset_top = 70
		player_hud.offset_bottom = 70
	else:
		player_hud.set_anchors_preset(Control.PRESET_TOP_WIDE)
		player_hud.offset_left = 310
		player_hud.offset_right = -316
		player_hud.offset_top = 60
	toasts.compact = on
	if on:
		toasts.set_anchors_preset(Control.PRESET_CENTER_BOTTOM)
		toasts.offset_left = -260
		toasts.offset_right = 260
		toasts.offset_top = -150
		toasts.offset_bottom = -62 # #246: acima das duas linhas de teclas (antes -40: o aviso sentava em cima delas)
	else:
		toasts.set_anchors_preset(Control.PRESET_CENTER_TOP)
		toasts.offset_left = -300
		toasts.offset_right = 300
		toasts.offset_top = 64
		toasts.offset_bottom = 240


var crosshair: Label
var _eye_hidden: Dictionary = {} ## AD-197s2: id -> true, quem está colado no olho da 1ª pessoa e não se desenha agora
var _marked := -1 ## e o corpo que leva o anel na vista de cima dentro de casa


## AD-197s2: efeitos só de desenho do modo jogador. 1ª pessoa: some quem está a menos de `near_person()` do olho (só os moradores das células
## vizinhas, pelo índice do renderer). 3ª pessoa sobre o telhado de uma casa: anel discreto no próprio corpo.
## #222: o próprio corpo saiu do escopo desenhado (andou para fora da cidade, entrou noutra): a posse e o modo de câmera ficam; o app
## pergunta ao motor onde o corpo está (`currentScope` da ficha), troca de escopo e volta a segui-lo assim que ele chegar no novo fluxo.
var _body_missing_since := -1
var _seeking_body := false
const BODY_LOST_MS := 600


var _inside_by_body := -1 ## #227: a casa em que o app entrou porque o motor disse que o corpo está nela (-1: nenhuma)
var _inside_busy := false
var _pose_asked_ms := -10000


## #227: o motor diz onde o corpo está (prédio e andar). Dentro de um prédio o app entra na casa em foco com a câmera do jogador;
## o motor diz que saiu e o app volta ao mundo, sem corte seco. A posse e o modo de câmera não mudam em nenhum dos dois.
func _on_player_placed(building: int, floor_level: int) -> void:
	_pose_building = building # #237: a pose mais nova nunca se perde: se chegou no meio de uma troca, a conciliação a aplica ao terminar
	_pose_floor = floor_level
	await _reconcile_placement()


var _pose_building := -1
var _pose_floor := 0


## #237: põe o desenho de acordo com a última pose do motor (entrar, sair, trocar de andar).
func _reconcile_placement() -> void:
	if player_hud == null or not player_hud.is_active or stage == null or _inside_busy:
		return
	var building := _pose_building
	var floor_level := _pose_floor
	if building >= 0 and not stage.active and _inside_by_body != building and store.kind in ["City", "World"]:
		_inside_busy = true
		_inside_by_body = building
		await enter_building(building)
		face_into_room()
		_inside_busy = false
	elif building < 0 and _inside_by_body >= 0:
		_inside_busy = true
		_inside_by_body = -1
		await exit_building()
		_inside_busy = false
	elif building >= 0 and stage.active and _inside_by_body == building and stage.interior_view.current_floor != floor_level \
			and floor_level < stage.interior_view.floor_count:
		stage.set_floor(floor_level) # o corpo subiu a escada: o andar visível o acompanha
		stage.interior_view.place_guided(floor_level)
	if _pose_building != building: # chegou outra pose durante a troca
		await _reconcile_placement()


const BodyScope := preload("res://Scenes/body_scope.gd")
var body_scope := BodyScope.new() ## #237: ponto de apoio da câmera e o escopo que a posição do corpo pede
var _last_body_height := 0.0
var _last_body_scale := Vector3.ONE


func _keep_the_body() -> void:
	if _glide_t >= 0.0:
		return # #252: a câmera desliza até o corpo novo; só ao fim ela o segue
	if player_hud == null or not player_hud.is_active:
		_body_missing_since = -1
		body_scope.release()
		return
	var own = player_actor()
	if own != null:
		_body_missing_since = -1
		_last_body_height = own.position.y
		_last_body_scale = own.scale
		if body_scope.anchor != null:
			# o boneco voltou ao desenho, mas nasce no centro da célula e só depois chega à posição fina: a câmera fica no ponto de apoio
			# até os dois coincidirem e então passa para o corpo no mesmo ponto (sem salto)
			if player_hud.fine != Vector2.INF and Vector2(own.position.x, own.position.z).distance_to(player_hud.fine) > BodyScope.HANDOVER_CELLS:
				var held_on: Node3D = body_scope.hold_at(self, player_hud.fine, _last_body_height, _last_body_scale)
				if rig.following != held_on:
					rig.follow(held_on)
				return
			body_scope.release()
		if rig.following != own:
			own.driven = true
			own.set_self(true)
			own.set_first_person(rig.mode == rig.FIRST)
			rig.follow(own)
		return
	# #237: o boneco do corpo não está no escopo desenhado (andou para fora da cidade, ou voltou): a câmera segue a posição fina do
	# corpo, que o cliente conhece, e não sobe "aguardando corpo" nem congela até a assinatura nova chegar
	if rig.mode != rig.ISO and player_hud.fine != Vector2.INF and not (stage != null and stage.active):
		var held: Node3D = body_scope.hold_at(self, player_hud.fine, _last_body_height, _last_body_scale)
		if rig.following != held:
			rig.follow(held)
	var now := Time.get_ticks_msec()
	if _body_missing_since < 0:
		_body_missing_since = now
	elif now - _body_missing_since > BODY_LOST_MS and not _seeking_body and not _inside_busy and not (stage != null and stage.active):
		_seek_body()


## #227/#249: o corpo do jogador onde ele está desenhado: o boneco do mundo, na rua e dentro da casa em foco (a mesma instância; a
## posse e a câmera o seguem sem trocar de alvo). Só sem boneco no mundo vale o ocupante do interior.
func player_actor() -> Node3D:
	var own = world_view.npc_nodes.get(player_hud.npc_id)
	if own != null and is_instance_valid(own):
		return own # #249: a mesma figura na rua e dentro da casa (a casa em foco a adota; não há segundo boneco)
	if stage != null and stage.active:
		return stage.interior_view.occupant_nodes.get(player_hud.npc_id)
	return null


## #237: o corpo cruzou a borda da cidade (ou entrou nela): pede já o escopo certo, sem esperar o boneco sumir. Não mexe dentro de casa.
func _follow_body_scope() -> void:
	if _seeking_body or _inside_busy or (stage != null and stage.active) or _inside_by_body >= 0 or not store.kind in ["City", "World"]:
		return
	var want: Array = BodyScope.wanted(store.kind, player_hud.fine, store.city, store.cities)
	if want.is_empty() or not body_scope.may_ask(Time.get_ticks_msec()):
		return
	_seeking_body = true
	await enter_scope(String(want[0]), String(want[1]))
	_seeking_body = false


func _seek_body() -> void:
	_seeking_body = true
	api.invalidate_npc(player_hud.npc_id)
	var detail: Dictionary = await api.npc_detail(player_hud.npc_id)
	if detail.ok and detail.body is Dictionary and detail.body.get("currentScope") is Dictionary and player_hud.is_active:
		var scope: Dictionary = detail.body.currentScope
		var city_id := String(scope.get("cityId", {}).get("value", "")) if scope.get("cityId") is Dictionary else ""
		if int(scope.get("kind", 0)) == 1 and city_id != "" and (store.kind != "City" or String(store.city.get("id", "")) != city_id):
			await enter_scope("City", city_id)
		elif int(scope.get("kind", 0)) == 0 and store.kind != "World":
			await enter_scope("World", "")
	_body_missing_since = Time.get_ticks_msec() # dá tempo ao novo fluxo de trazer o corpo antes de perguntar de novo
	_seeking_body = false


func _player_view_effects() -> void:
	_keep_the_body()
	_follow_body_scope()
	session.warm_interior_near_body(get_process_delta_time())
	if player_hud != null and player_hud.is_active and rig.mode != rig.ISO and _inside_by_body < 0 and not stage.active \
			and not rig.opened_ids().is_empty() and Time.get_ticks_msec() - _pose_asked_ms > 1500:
		_pose_asked_ms = Time.get_ticks_msec() # #227: o desenho sugere o corpo num prédio: o motor diz se está (a pose traz o prédio)
		player_hud.sync_pose()
	var own: Node3D = player_actor() if player_hud != null and player_hud.is_active else null
	var inside: bool = stage != null and stage.active # #227: dentro da casa o mundo não é desenhado; ninguém ao redor para esconder
	var want: Dictionary = {}
	if own != null and not inside and rig.mode == rig.FIRST:
		var radius: float = rig.near_person()
		var base := Grid.to_cell(own.position)
		for dx in range(-1, 2):
			for dz in range(-1, 2):
				for id in world_view.npcs.cell_members.get(base + Vector2i(dx, dz), []):
					var other = world_view.npc_nodes.get(id)
					if other != null and id != player_hud.npc_id and Vector2(other.position.x - own.position.x, other.position.z - own.position.z).length() < radius:
						want[id] = true
	if own != null and not inside and rig.mode == rig.THIRD: # #224: em 3ª pessoa, quem está colado no corpo do lado da câmera some (o corpo fica à vista)
		var cam: Vector3 = rig.camera.global_position if rig.camera.is_inside_tree() else own.position
		var toward := Vector2(cam.x - own.position.x, cam.z - own.position.z)
		var reach: float = Grid.units(2.0)
		var base3 := Grid.to_cell(own.position)
		for dx in range(-2, 3):
			for dz in range(-2, 3):
				for id in world_view.npcs.cell_members.get(base3 + Vector2i(dx, dz), []):
					var other = world_view.npc_nodes.get(id)
					if other == null or id == player_hud.npc_id:
						continue
					var rel := Vector2(other.position.x - own.position.x, other.position.z - own.position.z)
					if rel.length() < reach and toward.length() > 0.01 and rel.dot(toward.normalized()) > 0.0 \
							and absf(rel.cross(toward.normalized())) < Grid.units(0.9):
						want[id] = true
	for id in _eye_hidden.keys():
		if not want.has(id):
			var back = world_view.npc_nodes.get(id)
			if back != null:
				back.set_eye_hidden(false)
			_eye_hidden.erase(id)
	for id in want:
		if not _eye_hidden.has(id):
			world_view.npc_nodes[id].set_eye_hidden(true)
			_eye_hidden[id] = true
	if own != null: # #224: em 1ª pessoa o corpo olha para onde a câmera olha (as mãos entram no quadro); correr vem do motor
		if rig.mode == rig.FIRST:
			own.rotation.y = rig.yaw + PI
		own.running = player_hud.running and own.is_moving()
	if own != null and not inside and rig.mode == rig.THIRD and rig.camera.is_inside_tree(): # #227: a translucidez só no que fica entre a câmera e o corpo
		world_view.buildings.set_cover_segment(rig.camera.global_position, own.position + Vector3(0, rig.eye_height() * 0.55, 0))
	else:
		world_view.buildings.clear_cover_segment()
	var mark: int = player_hud.npc_id if own != null and rig.mode == rig.THIRD and not inside and not rig.opened_ids().is_empty() else -1
	if mark != _marked:
		var old = world_view.npc_nodes.get(_marked)
		if old != null:
			old.set_marked(false)
		if mark != -1:
			own.set_marked(true)
		_marked = mark
## AD-197p: as caixas desenhadas dos prédios perto do corpo ([id, AABB]), para o braço de mola da 3ª pessoa.
func camera_blockers() -> Array:
	var out: Array = []
	if stage != null and stage.active and player_hud != null and player_hud.is_active:
		# #227: na casa em foco contam os painéis finos dela; #249: e as cascas dos vizinhos (o mundo segue desenhado em volta)
		out.append([-1, stage.interior_view.floor_box(), stage.interior_view.floor_box(), stage.interior_view.wall_boxes(), true])
	if world_view == null or world_view.buildings == null:
		return out
	var near: Vector3 = rig.target
	for id in world_view.buildings.nodes:
		if stage != null and stage.active and id == stage.covered_building:
			continue # a casa em foco substitui este prédio
		var node: Node = world_view.buildings.nodes[id]
		var box: AABB = node.get_meta("box", AABB())
		if box.size == Vector3.ZERO:
			continue
		var center := box.get_center()
		var reach: float = rig.third_distance() + box.size.length() * 0.5 + 1.0
		if absf(center.x - near.x) > reach or absf(center.z - near.z) > reach:
			continue
		out.append([id, box, node.get_meta("shell", box.grow(Grid.units(0.3))), node.get_meta("walls", [])]) # AD-197s: [id, caixa (onde o corpo está), casca (o que a câmera não toca), paredes (AD-197s2)]
	return out


var side_panel: Control ## AD-197p: inspetor e ficha (lado direito do espectador)
var help_label: Label ## AD-197p: rodapé de atalhos (espectador ou jogador)


## AD-197d: clique encarnado é golpe no NPC sob a mira (o motor valida o alcance).
func strike_at(screen_position: Vector2) -> void:
	var hit: Dictionary = selection.hit_at(screen_position)
	if not hit.is_empty() and String(hit.kind) == Registry.PERSON:
		await player_hud.strike(int(hit.id))


## Pede a conversa com o NPC (atalho T); o servidor aceita ou recusa e a janela explica o motivo.
func talk_to(id: int) -> void:
	await conversation.start(id, registry.person_name(id))


## Criar família com o mundo rodando (ADR-0033): o mesmo criador de pessoas, enviado ao servidor família a família.
func open_family_builder() -> void:
	if family_builder != null:
		return
	family_builder = FamilyBuilder.new()
	family_builder.setup([], professions, [], skills)
	_ui.add_child(family_builder)
	family_builder.cancelled.connect(_close_family_builder)
	family_builder.done.connect(func(families: Array):
		_close_family_builder()
		await admin_window.create_households(families))


func _close_family_builder() -> void:
	family_builder.queue_free()
	family_builder = null


## Data do mundo de um tick (calendário do servidor): "Ano 3 · Mês 2 · Dia 9 · 14:00".
func _date_text(tick: int) -> String:
	var date: Dictionary = day_cycle.date_parts(float(tick))
	var hour := int(fmod(float(tick), day_cycle.hours_per_day) / day_cycle.hours_per_day * 24.0)
	return "Ano %d · Mês %d · Dia %d · %02d:00" % [date.year, date.month, date.day, hour]


# --- teclado e mouse ---------------------------------------------------------


# --- teclado e mouse (Shortcuts) ---------------------------------------------

func _unhandled_input(event: InputEvent) -> void:
	Shortcuts.handle(self, event)
