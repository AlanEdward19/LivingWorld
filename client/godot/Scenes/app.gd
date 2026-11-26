extends Node
## Raiz do cliente (ADR-0047): liga transporte, leitor, store, registro de entidades, cena e módulos. Só leitura do
## mundo: o único que escreve no servidor são os comandos explícitos (relógio do admin, conversa, jogador).
## Módulos: Session (escopos), Selection (clique e dica), Inspector (ficha), WindowRouter (janelas que convivem),
## FollowDirector (seguir), AppUi (montagem da interface), WorldView (um renderizador por tipo).
const WorldTime := preload("res://Contract/world_time.gd")

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
var weather_label: Label ## AD-180: indicador do tempo do dia
var loading: Control
var tooltip: PanelContainer
var tooltip_label: Label
var hover_text := ""
var selected_npc := -1
var settings ## preferências locais (GameSettings) vindas do Boot; nulo nos testes e nas medidas
var pause_menu ## PauseMenu (Esc)
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


func _ready() -> void:
	var real := HttpTransport.new()
	add_child(real)
	await start(real)
	var steps := DevSteps.parse(OS.get_cmdline_user_args())
	if not steps.is_empty() and not MenuSteps.is_menu(steps): # roteiro do menu roda o resto ele mesmo
		await DevSteps.run(self, steps)


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
	if identity.period_id != "" or not identity.axes.is_empty():
		technology.axis_names = identity.axes


# --- relógio, eventos, nomes -----------------------------------------------

func _on_clock_status(status: Dictionary) -> void:
	api.set_tick(int(float(status.get("atMs", 0)) / 60000.0)) # #222: o cache da ficha vence por minuto do mundo (em tempo real a hora leva 1 h)
	day_cycle.sync(WorldTime.hours_f(status.get("atMs")), hours_per_second(status), bool(status.isPaused))
	brain_panel.on_world_tick(WorldTime.hours_of(status, "atMs"))
	player_hud.set_paused(bool(status.isPaused))
	if player_hud.is_active: # AD-197c: o ritmo e os controles de dormir e acelerar, como o motor os diz
		player_hud.refresh_time()
	if not bool(status.isPaused):
		admin_window.note_running()
	pause_label.text = admin_window.pause_banner(status)
	pause_label.tooltip_text = pause_label.text
	NpcActor.frozen = bool(status.isPaused)
	world_view.npcs.clock.set_status(WorldTime.hours_of(status, "atMs"), hours_per_second(status), bool(status.isPaused)) # rota (AD-181); #222: pelo ritmo do motor
	sky_life.paused = bool(status.isPaused)
	_update_time_label(status)


## #222: quantas horas do mundo passam por segundo real, pelo ritmo que o motor diz (Δ por tick × ticks do laço). Com jogador o mundo
## anda em tempo real: estimar 1 hora por segundo (ticksPerSecond) adiantava o relógio e o motor o puxava para trás.
static func hours_per_second(status: Dictionary) -> float:
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
		day_cycle.phase_name(), (" · " + weather) if weather != "" else "", "  ⏸" if s.isPaused else ""]
	if player_frame != null: # #222: a hora do modo jogador, curta (dia, hora, fase e tempo)
		player_frame.set_clock("Dia %d · %s · %s%s%s" % [date.day, day_cycle.clock_text(), day_cycle.phase_name(),
			(" · " + weather) if weather != "" else "", "  ·  ⏸ pausado" if s.isPaused else ""])


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


func _process(delta: float) -> void:
	if player_hud != null and player_hud.is_active:
		_player_view_effects()
	elif not _eye_hidden.is_empty() or _marked != -1:
		_player_view_effects()
	if time_label != null and day_cycle != null:
		_update_time_label()
	if world_view != null and rig != null:
		world_view.view_center = rig.target
		world_view.view_radius = rig.zoom_size * 1.5
		world_view.zoom_size = rig.zoom_size
		world_view.far_zoom = rig.zoom_size > 16.0
		world_view.bubble_zoom = store.kind == "World" and rig.zoom_size > rig.bubble_size
		world_view.set_lod(WorldView.lod_for(store.kind if not stage.active else "Interior", rig.zoom_size))
		world_view.followed_npc = int(director.target.id) if director != null and String(director.target.get("kind", "")) == Registry.PERSON else -1
	if world_button != null:
		world_button.visible = store.kind == "City" and not stage.active
	if minimap != null:
		minimap.set_camera_target(rig.target)
		stage.set_daylight(day_cycle.daylight())
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
		await _drive(id)


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


## Saiu do personagem: a câmera volta à isométrica do espectador e para de segui-lo.
func player_released() -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null:
		node.driven = false
		node.set_self(false)
		node.set_first_person(false)
	unfollow()
	rig.set_mode(rig.ISO)
	set_player_mode(false)


## AD-197k: a posição fina do corpo (prevista ou a do motor) move o boneco do jogador.
func _on_player_fine(at: Vector2) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
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


## AD-197j: a mira do modo jogador é o centro da tela.
func aim_point() -> Vector2:
	return get_viewport().get_visible_rect().size / 2.0


## AD-197j: o golpe saiu: o braço do personagem desce (a mesma pose para quem olha de fora).
## #224: verbo aceito pelo motor -> gesto no corpo (o mesmo em 1ª e 3ª pessoa).
const GESTURE_BY_VERB := {"take": "reach", "collect": "reach", "fill": "reach", "steal": "reach", "deposit": "give", "return": "give",
	"buy": "give", "sell": "give", "abandon": "give", "drink": "drink", "eat": "eat", "enter": "push", "exit": "push", "up": "climb",
	"down": "climb", "talk": "talk", "socialize": "talk", "work": "work", "sleep": "rest", "idle": "rest"}


func _on_player_acted(verb: String) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null and GESTURE_BY_VERB.has(verb):
		node.play_gesture(GESTURE_BY_VERB[verb])


func _on_player_jumped(air_s: float) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null:
		node.play_jump(air_s)


func _on_player_struck(_target: int) -> void:
	var node = world_view.npc_nodes.get(player_hud.npc_id)
	if node != null:
		node.play_strike()


## AD-197j: a mira (uma cruz no centro) só no modo jogador.
func _on_camera_mode(mode: String) -> void:
	if crosshair != null:
		crosshair.visible = mode != rig.ISO
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
		toasts.offset_top = -130
		toasts.offset_bottom = -40
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
func _player_view_effects() -> void:
	var own: Node3D = world_view.npc_nodes.get(player_hud.npc_id) if player_hud != null and player_hud.is_active else null
	var want: Dictionary = {}
	if own != null and rig.mode == rig.FIRST:
		var radius: float = rig.near_person()
		var base := Grid.to_cell(own.position)
		for dx in range(-1, 2):
			for dz in range(-1, 2):
				for id in world_view.npcs.cell_members.get(base + Vector2i(dx, dz), []):
					var other = world_view.npc_nodes.get(id)
					if other != null and id != player_hud.npc_id and Vector2(other.position.x - own.position.x, other.position.z - own.position.z).length() < radius:
						want[id] = true
	if own != null and rig.mode == rig.THIRD: # #224: em 3ª pessoa, quem está colado no corpo do lado da câmera some (o corpo fica à vista)
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
	var mark: int = player_hud.npc_id if own != null and rig.mode == rig.THIRD and not rig.opened_ids().is_empty() else -1
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
	if world_view == null or world_view.buildings == null or (stage != null and stage.active):
		return out
	var near: Vector3 = rig.target
	for id in world_view.buildings.nodes:
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
