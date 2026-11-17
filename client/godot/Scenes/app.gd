extends Node
## Raiz do cliente (ADR-0047): liga transporte, leitor, store, registro de entidades, cena e módulos. Só leitura do
## mundo: o único que escreve no servidor são os comandos explícitos (relógio do admin, conversa, jogador).
## Módulos: Session (escopos), Selection (clique e dica), Inspector (ficha), WindowRouter (janelas que convivem),
## FollowDirector (seguir), AppUi (montagem da interface), WorldView (um renderizador por tipo).

signal menu_requested() ## F10 ou o botão Menu: o Boot volta ao menu principal

const CursorStream := preload("res://Contract/cursor_stream.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const WorldReader := preload("res://Contract/world_reader.gd")
const ScopeObserver := preload("res://Contract/scope_observer.gd")
const Store := preload("res://Store/world_store.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Registry := preload("res://Store/entity_registry.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const DayCycle := preload("res://Scenes/day_cycle.gd")
const SkyLife := preload("res://Scenes/sky_life.gd")
const Ambience := preload("res://Scenes/ambience.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")
const FamilyBuilder := preload("res://Scenes/family_builder.gd")
const AppUi := preload("res://Scenes/app_ui.gd")
const Session := preload("res://Scenes/session.gd")
const Selection := preload("res://Scenes/selection.gd")
const WindowRouter := preload("res://Scenes/window_router.gd")
const FollowDirector := preload("res://Scenes/follow_director.gd")

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
var admin_window
var family_builder
var economy
var technology
var period_id := "" ## período de origem do mundo em andamento (GET /world/info); vazio se desconhecido
var admin_enabled := OS.get_environment("LW_ADMIN_TOKEN") != "" ## sem token o console nem aparece e o tempo fica travado
var pause_label: Label
var player_id := ""
var observer
var banner: Label
var breadcrumb: Label
var follow_label: Label
var time_label: Label
var loading: Control
var tooltip: PanelContainer
var tooltip_label: Label
var hover_text := ""
var selected_npc := -1

var professions: Dictionary:
	get:
		return registry.professions
var skills: Dictionary:
	get:
		return registry.skills
var resource_names: Dictionary:
	get:
		return registry.resources

var _names_timer: Timer
var _ui: Control
var _hover_clock := 0.0
var _sun: DirectionalLight3D
var _environment: Environment


func _ready() -> void:
	var real := HttpTransport.new()
	add_child(real)
	await start(real)
	var steps := DevSteps.parse(OS.get_cmdline_user_args())
	if not steps.is_empty():
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
	_build_scene()
	AppUi.build(self)
	size_windows()
	observer = ScopeObserver.new()
	observer.api = api
	add_child(observer)
	stream = CursorStream.new(transport, get_tree())
	stream.validate_shapes = validate_shapes
	if sleeper.is_valid():
		stream.sleeper = sleeper
	stream.snapshot_received.connect(_on_snapshot)
	stream.delta_applied.connect(_on_delta)
	store.interior_changed.connect(session.on_interior_changed)
	store.events_arrived.connect(_on_events)
	store.buildings_changed.connect(func(_ids): session.fill_pending_footprints())
	stream.connection_changed.connect(session.on_connection)
	stream.access_denied.connect(func(): banner.text = "acesso negado a este escopo"; banner.visible = true)
	_names_timer = Timer.new()
	_names_timer.wait_time = 20.0
	_names_timer.autostart = true
	_names_timer.timeout.connect(_load_names)
	add_child(_names_timer)
	var actions: Dictionary = await api.action_catalog()
	if actions.ok:
		labels.load_from(actions.body)
	var calendar: Dictionary = await api.world_calendar()
	if calendar.ok:
		day_cycle.set_calendar(calendar.body)
	await clock_panel.refresh()
	await _load_world_info()
	await stream.subscribe("World")


## Identidade do mundo (`GET /world/info`) e catálogo do período: nomes de ofício, habilidade, recurso e tipo de prédio.
func _load_world_info() -> void:
	var identity: Dictionary = await api.world_identity()
	period_id = identity.period_id
	if identity.period_id != "" or not identity.axes.is_empty():
		technology.axis_names = identity.axes


func _build_scene() -> void:
	world_view = WorldView.new()
	world_view.bind(store, labels)
	world_view.npcs.catalog = catalog
	world_view.npcs.model_of = registry.model_of
	add_child(world_view)
	rig = CameraRig.new()
	rig.city_at = func(point: Vector3) -> String: return world_view.city_at(point)
	rig.scope_requested.connect(func(kind: String, ref_id: String): enter_scope(kind, ref_id))
	add_child(rig)
	rig.camera.current = true
	_sun = DirectionalLight3D.new()
	_sun.shadow_enabled = true
	_sun.directional_shadow_max_distance = 120.0
	add_child(_sun)
	var world_environment := WorldEnvironment.new()
	_environment = Environment.new()
	_environment.background_mode = Environment.BG_COLOR
	_environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	_environment.tonemap_mode = Environment.TONE_MAPPER_ACES
	_environment.tonemap_exposure = 0.9
	_environment.ssao_enabled = true
	_environment.ssao_intensity = 1.6
	world_environment.environment = _environment
	add_child(world_environment)
	sky_life = SkyLife.new()
	add_child(sky_life)
	sky_life.build(Vector2(30, 30))
	ambience = Ambience.new()
	ambience.catalog = catalog
	add_child(ambience)
	day_cycle = DayCycle.new()
	add_child(day_cycle)
	day_cycle.bind(_sun, _environment, world_view.window_material, world_view.lamp_material)


# --- relógio, eventos, nomes -----------------------------------------------

func _on_clock_status(status: Dictionary) -> void:
	api.set_tick(int(status.tick))
	day_cycle.sync(float(status.tick), float(status.ticksPerSecond), bool(status.isPaused))
	brain_panel.on_world_tick(int(status.tick))
	player_hud.set_paused(bool(status.isPaused))
	if not bool(status.isPaused):
		admin_window.note_running()
	pause_label.text = admin_window.pause_banner(status)
	pause_label.tooltip_text = pause_label.text
	NpcActor.frozen = bool(status.isPaused)
	sky_life.paused = bool(status.isPaused)
	_update_time_label(status)


func _update_time_label(status: Dictionary = {}) -> void:
	var s: Dictionary = status if not status.is_empty() else clock_panel.status
	if s.is_empty():
		return
	var date: Dictionary = day_cycle.date_parts(float(s.tick))
	time_label.text = "Ano %d · Mês %d · Dia %d · %s · %s%s" % [int(s.year), date.month, date.day, day_cycle.clock_text(),
		day_cycle.phase_name(), "  ⏸" if s.isPaused else ""]


func _exit_tree() -> void:
	NpcActor.frozen = false # o estado pausado é do servidor, não deve vazar para o próximo mundo


func _process(delta: float) -> void:
	if time_label != null and day_cycle != null:
		_update_time_label()
	if world_view != null and rig != null:
		world_view.view_center = rig.target
		world_view.view_radius = rig.zoom_size * 1.5
		world_view.far_zoom = rig.zoom_size > 16.0
		world_view.bubble_zoom = store.kind == "World" and rig.zoom_size > rig.bubble_size
	if minimap != null:
		minimap.set_camera_target(rig.target)
		stage.set_daylight(day_cycle.daylight())
		ambience.set_daylight(day_cycle.daylight())
	if director != null:
		director.step(delta)
		follow_label.text = director.label()
	_hover_clock += delta
	if _hover_clock >= 0.07:
		_hover_clock = 0.0
		_update_hover()


func _on_delta(envelope: Dictionary) -> void:
	store.apply_delta(envelope)
	for event in envelope.payload.get("events", []):
		toasts.push(String(event.label))


## Evento com lugar vira marcador clicável no mapa (o aviso de texto continua vindo de _on_delta).
func _on_events(events: Array) -> void:
	for event in events:
		var id: int = world_view.events.add(event)
		if id > 0:
			var location: Dictionary = event.location
			registry.events[id] = {"label": String(event.get("label", "")), "kind": int(event.get("kind", 0)),
				"tick": int(event.get("tick", 0)), "cell": Vector2i(int(location.x), int(location.y))}


func _load_names() -> void:
	if api == null or store == null or stage.active:
		return
	var city_id: String = store.city.id if store.kind == "City" else ""
	var skip := 0
	while skip < 1000:
		var page: Dictionary = await api.npc_page("", skip, 200, city_id)
		if not page.ok:
			return
		for item in page.body.items:
			store.names[int(item.id.value)] = String(item.name)
		skip += 200
		if skip >= int(page.body.total):
			break
	world_view.apply_names()
	if stage.active:
		stage.interior_view.apply_names(store.names)


# --- escopos (Session) ------------------------------------------------------

func enter_scope(kind: String, ref_id: String) -> void:
	await session.enter_scope(kind, ref_id)


func _on_snapshot(envelope: Dictionary) -> void:
	session.on_snapshot(envelope)


func release_observation() -> void:
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
	if stage == null or not rig.camera.is_inside_tree():
		tooltip.visible = false
		return
	var mouse := get_viewport().get_mouse_position()
	var hit: Dictionary = selection.hit_at(mouse)
	hover_text = selection.describe(hit)
	world_view.hover_npc(int(hit.id) if not hit.is_empty() and hit.kind == Registry.PERSON and not stage.active else -1)
	tooltip.visible = hover_text != "" and not list_panel.get_global_rect().has_point(mouse)
	if tooltip.visible:
		tooltip_label.text = hover_text
		tooltip.position = mouse + Vector2(16, 18)


## Clique simples abre a ficha; duplo clique entra (prédio ou cidade).
func _pick(screen_position: Vector2, double_click: bool) -> void:
	var hit: Dictionary = selection.hit_at(screen_position)
	if hit.is_empty():
		return
	match String(hit.kind):
		Registry.BUILDING:
			if double_click:
				enter_building(int(hit.id))
			else:
				inspect(hit)
		Registry.CITY:
			if double_click:
				enter_scope("City", String(hit.id))
			elif store.kind == "World":
				inspect(hit)
		_:
			inspect(hit)


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
	if await player_hud.incarnate(id, registry.person_name(id)):
		selected_npc = id
		follow(Registry.ref(Registry.PERSON, id))


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

## Esc fecha a janela da frente; sem janela, a conversa; depois sai do prédio e, por fim, da cidade.
func _on_escape() -> void:
	if router.close_top():
		return
	if conversation.is_open:
		conversation.end_conversation()
	elif stage.active:
		exit_building()
	elif store.kind == "City":
		enter_scope("World", "")


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_ESCAPE:
				_on_escape()
			KEY_BACKSPACE:
				if inspector.is_open():
					inspector.back()
			KEY_F10:
				menu_requested.emit()
			KEY_B:
				_toggle_brain()
			KEY_F8:
				_toggle_admin()
			KEY_R:
				_toggle_reports()
			KEY_I:
				if selected_npc >= 0 and not player_hud.is_active:
					incarnate(selected_npc)
			KEY_T:
				if selected_npc >= 0 and not conversation.input.has_focus():
					talk_to(selected_npc)
			KEY_M:
				ambience.set_muted(not ambience.muted)
			KEY_L:
				list_panel.visible = not list_panel.visible
				if list_panel.visible and list_panel.rows.is_empty():
					list_panel.load_page()
			KEY_F:
				if director.active():
					unfollow()
				else:
					follow_selected()
			KEY_PAGEUP, KEY_BRACKETRIGHT:
				if stage.active:
					stage.set_floor(stage.interior_view.current_floor + 1)
			KEY_PAGEDOWN, KEY_BRACKETLEFT:
				if stage.active:
					stage.set_floor(stage.interior_view.current_floor - 1)
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		_pick(event.position, event.double_click)
