extends Node
## Raiz do cliente: liga transporte, assinatura, store, cenas e painéis. Só leitura do mundo:
## o único que escreve no servidor são os comandos explícitos do relógio.

signal menu_requested() ## F10 ou o botão Menu: o Boot volta ao menu principal

const CursorStream := preload("res://Contract/cursor_stream.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const Store := preload("res://Store/world_store.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const InteriorStage := preload("res://Scenes/interior_stage.gd")
const Panels := preload("res://Scenes/panels.gd")
const DayCycle := preload("res://Scenes/day_cycle.gd")
const UiTheme := preload("res://Scenes/ui_theme.gd")
const Minimap := preload("res://Scenes/minimap.gd")
const Toasts := preload("res://Scenes/toasts.gd")
const SkyLife := preload("res://Scenes/sky_life.gd")
const Ambience := preload("res://Scenes/ambience.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")
const BrainPanel := preload("res://Scenes/brain_panel.gd")
const ConversationWindow := preload("res://Scenes/conversation_window.gd")
const NarrativeWindow := preload("res://Scenes/narrative_window.gd")
const AdminWindow := preload("res://Scenes/admin_window.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerProfile := preload("res://Contract/player_profile.gd")
const ScopeObserver := preload("res://Contract/scope_observer.gd")

var transport
var api
var stream
var store
var labels
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
var npc_panel
var city_panel
var brain_panel
var conversation
var narrative
var player_hud
var admin_window
var admin_enabled := OS.get_environment("LW_ADMIN_TOKEN") != "" ## sem token o console nem aparece
var pause_label: Label
var player_id := ""
var observer
var banner: Label
var breadcrumb: Label
var time_label: Label
var loading: Control
var tooltip: PanelContainer
var tooltip_label: Label
var hover_text := ""
var selected_npc := -1

var _previous_scope := ["World", ""]
var _world_ref := ["World", ""]
var _interior_building := -1
var _names_timer: Timer
var _ui: Control
var _hover_clock := 0.0
var _sun: DirectionalLight3D
var _environment: Environment
var _first_snapshot := false


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
	api = ApiClient.new(transport)
	api.validate_shapes = validate_shapes
	store = Store.new()
	labels = ActionLabels.new()
	_build_scene()
	_build_ui()
	observer = ScopeObserver.new()
	observer.api = api
	add_child(observer)
	stream = CursorStream.new(transport, get_tree())
	stream.validate_shapes = validate_shapes
	if sleeper.is_valid():
		stream.sleeper = sleeper
	stream.snapshot_received.connect(_on_snapshot)
	stream.delta_applied.connect(_on_delta)
	store.interior_changed.connect(_on_interior_changed)
	stream.connection_changed.connect(_on_connection)
	stream.access_denied.connect(func(): banner.text = "acesso negado a este escopo"; banner.visible = true)
	var catalog: Dictionary = await api.action_catalog()
	if catalog.ok:
		labels.load_from(catalog.body)
	var calendar: Dictionary = await api.world_calendar()
	if calendar.ok:
		day_cycle.set_calendar(calendar.body)
	await clock_panel.refresh()
	await stream.subscribe("World")


func _build_scene() -> void:
	world_view = WorldView.new()
	world_view.bind(store, labels)
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


func _build_ui() -> void:
	_ui = Control.new()
	_ui.set_anchors_preset(Control.PRESET_FULL_RECT)
	_ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_ui.theme = UiTheme.build()
	var layer := CanvasLayer.new()
	layer.layer = 5
	add_child(layer)
	layer.add_child(_ui)
	stage = InteriorStage.new()
	_ui.add_child(stage)

	var top := PanelContainer.new()
	top.set_anchors_preset(Control.PRESET_TOP_WIDE)
	top.offset_left = 8
	top.offset_right = -8
	top.offset_top = 8
	_ui.add_child(top)
	var bar := HBoxContainer.new()
	top.add_child(bar)
	breadcrumb = Label.new()
	breadcrumb.text = "Mundo"
	breadcrumb.add_theme_font_size_override("font_size", 19)
	bar.add_child(breadcrumb)
	var left_spacer := Control.new()
	left_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(left_spacer)
	time_label = Label.new()
	time_label.add_theme_font_size_override("font_size", 19)
	time_label.add_theme_color_override("font_color", Color("ffe9a6"))
	bar.add_child(time_label)
	var right_spacer := Control.new()
	right_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(right_spacer)
	pause_label = Label.new()
	pause_label.clip_text = true
	pause_label.custom_minimum_size.x = 120
	pause_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	pause_label.size_flags_stretch_ratio = 4.0
	pause_label.add_theme_color_override("font_color", Color("ffcf4a"))
	bar.add_child(pause_label)
	clock_panel = Panels.ClockPanel.new()
	clock_panel.api = api
	clock_panel.status_changed.connect(_on_clock_status)
	bar.add_child(clock_panel)
	if admin_enabled:
		var console_button := Button.new()
		console_button.text = "Console (F8)"
		console_button.pressed.connect(func(): _toggle_admin())
		bar.add_child(console_button)
	var menu_button := Button.new()
	menu_button.text = "☰ Menu (F10)"
	menu_button.pressed.connect(func(): menu_requested.emit())
	bar.add_child(menu_button)

	banner = Label.new()
	banner.visible = false
	banner.add_theme_color_override("font_color", Color("ffcf4a"))
	banner.add_theme_font_size_override("font_size", 22)
	banner.set_anchors_preset(Control.PRESET_CENTER_TOP)
	banner.position = Vector2(-220, 80)
	_ui.add_child(banner)

	var side := VBoxContainer.new()
	side.set_anchors_preset(Control.PRESET_RIGHT_WIDE)
	side.offset_left = -306
	side.offset_right = -8
	side.offset_top = 64
	_ui.add_child(side)
	npc_panel = Panels.NpcDetailPanel.new()
	npc_panel.api = api
	npc_panel.labels = labels
	npc_panel.name_of = func(id: int) -> String: return String(store.names.get(id, "NPC %d" % id))
	npc_panel.highlight.connect(func(id: int): _highlight(id))
	npc_panel.model_known.connect(_apply_model)
	store.npc_changed.connect(_on_focus_action)
	npc_panel.follow_requested.connect(func(_id: int): follow_selected())
	side.add_child(npc_panel)
	city_panel = Panels.CityPanel.new()
	side.add_child(city_panel)
	player_hud = PlayerHud.new()
	player_hud.api = api
	player_hud.labels = labels
	player_hud.player_id = player_id if player_id != "" else PlayerProfile.load_or_create()
	player_hud.warning.connect(func(text: String): toasts.push(text))
	player_hud.released.connect(func(): rig.release())
	player_hud.set_anchors_preset(Control.PRESET_TOP_WIDE)
	player_hud.offset_left = 310
	player_hud.offset_right = -316
	player_hud.offset_top = 60
	_ui.add_child(player_hud)
	npc_panel.incarnate_requested.connect(func(id: int): incarnate(id))

	list_panel = Panels.NpcListPanel.new()
	list_panel.api = api
	list_panel.visible = false
	list_panel.set_anchors_preset(Control.PRESET_LEFT_WIDE)
	list_panel.offset_left = 8
	list_panel.offset_right = 296
	list_panel.offset_top = 64
	list_panel.offset_bottom = -272
	list_panel.npc_selected.connect(func(id: int): select_npc(id))
	_ui.add_child(list_panel)

	brain_panel = BrainPanel.new()
	brain_panel.api = api
	brain_panel.labels = labels
	brain_panel.date_of = _date_text
	brain_panel.set_anchors_preset(Control.PRESET_FULL_RECT)
	brain_panel.offset_left = 310
	brain_panel.offset_right = -316
	brain_panel.offset_top = 130
	brain_panel.offset_bottom = -272
	brain_panel.warning.connect(func(text: String): toasts.push(text))
	_ui.add_child(brain_panel)
	npc_panel.brain_requested.connect(func(id: int): open_brain(id))
	conversation = ConversationWindow.new()
	conversation.api = api
	conversation.set_anchors_preset(Control.PRESET_FULL_RECT)
	conversation.anchor_top = 1.0 # faixa fixa na base da tela
	conversation.offset_left = 310
	conversation.offset_right = -430
	conversation.offset_top = -262
	conversation.offset_bottom = -34
	conversation.warning.connect(func(text: String): toasts.push(text))
	_ui.add_child(conversation)
	npc_panel.conversation_requested.connect(func(id: int): talk_to(id))
	narrative = NarrativeWindow.new()
	narrative.api = api
	narrative.calendar_of = func() -> Dictionary: return {"hoursPerDay": day_cycle.hours_per_day, "daysPerMonth": day_cycle.days_per_month, "monthsPerYear": day_cycle.months_per_year}
	narrative.tick_now = func() -> int: return int(clock_panel.status.get("tick", 0))
	narrative.set_anchors_preset(Control.PRESET_FULL_RECT)
	narrative.offset_left = 310
	narrative.offset_right = -316
	narrative.offset_top = 130
	narrative.offset_bottom = -272
	narrative.date_of = _date_text
	narrative.warning.connect(func(text: String): toasts.push(text))
	_ui.add_child(narrative)
	admin_window = AdminWindow.new()
	admin_window.api = api
	admin_window.calendar_of = func() -> Dictionary: return {"hoursPerDay": day_cycle.hours_per_day}
	admin_window.date_of = _date_text
	admin_window.set_anchors_preset(Control.PRESET_FULL_RECT)
	admin_window.offset_left = 310
	admin_window.offset_right = -316
	admin_window.offset_top = 130
	admin_window.offset_bottom = -272
	admin_window.warning.connect(func(text: String): toasts.push(text))
	admin_window.npc_selected.connect(func(id: int): select_npc(id))
	_ui.add_child(admin_window)
	npc_panel.biography_requested.connect(func(id: int): open_biography(id))
	city_panel.chronicle_requested.connect(func(city_id: String): open_chronicle(city_id))
	city_panel.beliefs_requested.connect(func(city_id: String): open_beliefs(city_id))

	minimap = Minimap.new()
	minimap.bind(store)
	minimap.set_anchors_preset(Control.PRESET_BOTTOM_LEFT)
	minimap.offset_left = 8
	minimap.offset_top = -258
	minimap.offset_right = 218
	minimap.offset_bottom = -48
	minimap.pan_requested.connect(func(point: Vector3): rig.focus(point))
	_ui.add_child(minimap)

	toasts = Toasts.new()
	toasts.set_anchors_preset(Control.PRESET_BOTTOM_RIGHT)
	toasts.offset_left = -420
	toasts.offset_right = -12
	toasts.offset_top = -260
	toasts.offset_bottom = -48
	_ui.add_child(toasts)

	var help := Label.new()
	help.text = "Botão direito: mover · Shift+botão direito: girar · Roda: zoom · Q/E: girar · L: pessoas · F: seguir · B: cérebro do NPC · T: conversar · R: relatos · I: encarnar · Esc: voltar · PgUp/PgDn: andar · M: som · clique: selecionar/entrar"
	help.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	help.offset_top = -30
	help.offset_left = 10
	help.add_theme_color_override("font_color", Color(1, 1, 1, 0.8))
	help.add_theme_font_size_override("font_size", 13)
	_ui.add_child(help)

	tooltip = PanelContainer.new()
	tooltip.visible = false
	tooltip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	tooltip_label = Label.new()
	tooltip.add_child(tooltip_label)
	_ui.add_child(tooltip)

	loading = ColorRect.new()
	(loading as ColorRect).color = Color("0d1420")
	loading.set_anchors_preset(Control.PRESET_FULL_RECT)
	var loading_label := Label.new()
	loading_label.text = "Conectando ao mundo…"
	loading_label.add_theme_font_size_override("font_size", 30)
	loading_label.set_anchors_preset(Control.PRESET_CENTER)
	loading_label.position = Vector2(-170, -20)
	loading.add_child(loading_label)
	_ui.add_child(loading)

	_names_timer = Timer.new()
	_names_timer.wait_time = 20.0
	_names_timer.autostart = true
	_names_timer.timeout.connect(_load_names)
	add_child(_names_timer)


# --- relógio, eventos, nomes -----------------------------------------------

func _on_clock_status(status: Dictionary) -> void:
	day_cycle.sync(float(status.tick), float(status.ticksPerSecond), bool(status.isPaused))
	brain_panel.on_world_tick(int(status.tick))
	player_hud.set_paused(bool(status.isPaused))
	if not bool(status.isPaused):
		admin_window.note_running()
	pause_label.text = admin_window.pause_banner(bool(status.isPaused))
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
	_hover_clock += delta
	if _hover_clock >= 0.07:
		_hover_clock = 0.0
		_update_hover()


func _on_delta(envelope: Dictionary) -> void:
	store.apply_delta(envelope)
	for event in envelope.payload.get("events", []):
		toasts.push(String(event.label))


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


# --- escopos -------------------------------------------------------------

func enter_scope(kind: String, ref_id: String) -> void:
	_world_ref = [kind, ref_id]
	await stream.subscribe(kind, ref_id)


func _on_snapshot(envelope: Dictionary) -> void:
	var kind := Store.scope_name(envelope.scope.kind)
	store.apply_snapshot(envelope)
	_publish_observation(kind)
	banner.visible = false
	if not _first_snapshot:
		_first_snapshot = true
		loading.visible = false
		minimap.rebuild()
		sky_life.build(Vector2(store.map_size))
		rig.fit_to_map(maxi(store.map_size.x, store.map_size.y))
		_load_names()
	if kind == "Interior":
		if stage.active and stage.interior_view.floor_count == int(store.interior.floors):
			stage.interior_view.set_occupants(store.interior.occupants) # reenvio do snapshot pelo canal: só atualiza
			stage.interior_view.apply_names(store.names)
		else:
			_enter_interior_view()
		return
	rig.set_scope(kind)
	list_panel.city_filter = store.city.id if kind == "City" else ""
	if list_panel.visible:
		list_panel.search(list_panel.query)
	if kind == "City":
		breadcrumb.text = "Mundo › %s" % (store.city.name if store.city.name != "" else "Cidade")
		city_panel.show_city(store.city)
		var area := _built_area()
		rig.focus(_center_of(area))
		rig.set_zoom(clampf(maxf(area.size.x, area.size.y) * 1.15 + 5.0, 7.0, rig.city_exit_size - 0.6))
		_load_names()
	elif kind == "World":
		breadcrumb.text = "Mundo"
		city_panel.visible = false
		if rig.zoom_size < rig.city_exit_size + 1.0:
			rig.set_zoom(22.0)


## Conta ao servidor o que a câmera observa: só assim o rastro de cognição do NPC é gravado.
func _publish_observation(kind: String) -> void:
	match kind:
		"World":
			observer.set_scope("World")
		"City":
			observer.set_scope("City", String(store.city.get("id", "")))
		"Interior":
			var city_id := String(store.city.get("id", ""))
			if city_id != "":
				observer.set_scope("Building", city_id, str(_interior_building))


## Retira a fonte de observação antes de sair (menu principal); o servidor também a descarta em 30 s sem batimento.
func release_observation() -> void:
	if conversation != null:
		await conversation.end_conversation()
	if observer != null:
		await observer.clear()


## Retângulo que cobre os prédios da cidade (ou os limites dela, se ainda não há prédios).
func _built_area() -> Rect2i:
	var area := Rect2i()
	var first := true
	for id in store.buildings:
		var building: Dictionary = store.buildings[id]
		for entry in building.footprint:
			var cell: Vector2i = building.cell + entry.cell
			area = Rect2i(cell, Vector2i.ONE) if first else area.expand(cell).expand(cell + Vector2i.ONE)
			first = false
	return store.city.bounds if first else area


func _center_of(bounds: Rect2i) -> Vector3:
	return Vector3(bounds.position.x + bounds.size.x / 2.0, 0, bounds.position.y + bounds.size.y / 2.0)


func _on_connection(state: String, retry_in: float) -> void:
	banner.visible = state == "offline"
	if state == "offline":
		banner.text = "sem conexão — nova tentativa em %d s" % int(retry_in)
		loading.visible = not _first_snapshot
		if not _first_snapshot:
			(loading.get_child(0) as Label).text = "Sem conexão com o servidor…\nnova tentativa em %d s" % int(retry_in)


# --- interior ------------------------------------------------------------

func enter_building(building_id: int) -> void:
	if stage.active:
		return
	_previous_scope = _world_ref.duplicate()
	_interior_building = building_id
	await stream.subscribe("Interior", str(building_id))


func _enter_interior_view() -> void:
	var marker = store.buildings.get(_interior_building, null)
	var origin: Vector2i = marker.cell if marker != null else Vector2i.ZERO
	stage.interior_view.catalog = catalog
	var lodging = marker.get("lodging") if marker != null else null
	stage.enter(
		store.interior, origin, labels, rig.camera, int(marker.type) if marker != null else -1,
		int(lodging.units) if lodging is Dictionary else 0)
	stage.interior_view.apply_names(store.names)
	breadcrumb.text = "%s › Edifício %d" % [breadcrumb.text.get_slice(" › Edifício", 0), _interior_building]
	if marker != null:
		rig.focus(Vector3(origin.x + 1.5, 0, origin.y + 1.5))
		rig.set_zoom(7.0)


func exit_building() -> void:
	if not stage.active:
		return
	stage.leave()
	_interior_building = -1
	await stream.subscribe(_previous_scope[0], _previous_scope[1])


func _on_interior_changed() -> void:
	if stage.active:
		stage.interior_view.set_occupants(store.interior.occupants)
		stage.interior_view.apply_names(store.names)


# --- seleção e hover -------------------------------------------------------

func select_npc(id: int) -> void:
	await npc_panel.show_npc(id)
	if brain_panel.is_open:
		open_brain(id)


## Abre o painel "Cérebro" do NPC (atalho B).
func open_brain(id: int) -> void:
	await brain_panel.open(id)


## Encarna o NPC (atalho I): o servidor decide se pode; a câmera passa a segui-lo.
func incarnate(id: int) -> void:
	if await player_hud.incarnate(id, String(store.names.get(id, "NPC %d" % id))):
		selected_npc = id
		if world_view.npc_nodes.has(id):
			rig.follow(world_view.npc_nodes[id])


func _toggle_admin() -> void:
	if not admin_enabled:
		return
	if admin_window.is_open:
		admin_window.close()
	else:
		admin_window.open()


func open_biography(id: int) -> void:
	await narrative.show_biography(id, String(store.names.get(id, "NPC %d" % id)))


func open_chronicle(city_id: String) -> void:
	if city_id != "":
		await narrative.show_chronicle(city_id, city_panel.city_name)


func open_beliefs(city_id: String) -> void:
	if city_id != "":
		await narrative.show_beliefs(city_id, city_panel.city_name)


func _toggle_reports() -> void:
	if narrative.is_open and narrative.mode == "reports":
		narrative.close()
	else:
		narrative.show_reports()


## Pede a conversa com o NPC (atalho T); o servidor aceita ou recusa e a janela explica o motivo.
func talk_to(id: int) -> void:
	await conversation.start(id, String(store.names.get(id, "NPC %d" % id)))


func _toggle_brain() -> void:
	if brain_panel.is_open:
		brain_panel.close()
	elif selected_npc >= 0:
		open_brain(selected_npc)


## Data do mundo de um tick (calendário do servidor): "Ano 3 · Mês 2 · Dia 9 · 14:00".
func _date_text(tick: int) -> String:
	var date: Dictionary = day_cycle.date_parts(float(tick))
	var hour := int(fmod(float(tick), day_cycle.hours_per_day) / day_cycle.hours_per_day * 24.0)
	return "Ano %d · Mês %d · Dia %d · %02d:00" % [date.year, date.month, date.day, hour]


## NPC em foco mudou de ação: toca o som do catálogo (só o selecionado, para não virar barulho de multidão).
func _on_focus_action(id: int, _from: Vector2i, _to: Vector2i, action: int) -> void:
	if id == selected_npc:
		ambience.play_action(action)


## Modelo do NPC (profissão e idade) só chega com o detalhe dele; os demais ficam no corpo base.
func _apply_model(id: int, profession: int, age_years: float) -> void:
	var actor = world_view.npc_nodes.get(id)
	if actor != null:
		actor.apply_model(catalog, profession, age_years)


func _highlight(id: int) -> void:
	selected_npc = id
	world_view.highlight_npc(id)


func follow_selected() -> void:
	if world_view.npc_nodes.has(selected_npc):
		rig.follow(world_view.npc_nodes[selected_npc])


## Texto da dica para o que está sob o cursor (testável sem mouse).
func describe_hit(meta: Dictionary) -> String:
	if meta.has("npc_id"):
		var id: int = meta.npc_id
		var npc = store.npcs.get(id)
		var name: String = store.names.get(id, "NPC %d" % id)
		var action: String = labels.label_for(int(npc.action)) if npc != null else ""
		return name if action == "" else "%s\n%s" % [name, action]
	if meta.has("building_id"):
		var building = store.buildings.get(meta.building_id)
		var floors: int = building.floors if building != null else 1
		var text := "Edifício %d · %d andar%s" % [meta.building_id, floors, "es" if floors > 1 else ""]
		return text + _price_lines(building) + "\nclique para entrar"
	if meta.has("city_id"):
		var city = store.cities.get(meta.city_id)
		if city != null:
			return "%s · %d hab.\nduplo clique para entrar" % [city.name if city.name != "" else "Cidade", city.population]
	return ""


## Preço do imóvel como o servidor calculou hoje (local e demanda já entram): hospedagem mostra vagas, aluguel e diária.
func _price_lines(building) -> String:
	if building == null:
		return ""
	var lodging = building.get("lodging")
	if lodging is Dictionary:
		var line := "\nHospedagem · %d/%d vagas · aluguel %d/mês" % [int(lodging.occupied), int(lodging.units), int(lodging.rentPerMonth)]
		if int(lodging.nightlyRate) > 0:
			line += " · diária %d" % int(lodging.nightlyRate)
		return line + ("\nvalor %d" % int(building.value) if int(building.get("value", 0)) > 0 else "")
	if int(building.get("value", 0)) > 0:
		return "\nvalor estimado %d" % int(building.value)
	return ""


func _update_hover() -> void:
	if stage == null or stage.active or not rig.camera.is_inside_tree():
		tooltip.visible = false
		return
	var mouse := get_viewport().get_mouse_position()
	var hit := _raycast(mouse)
	var meta := {}
	if not hit.is_empty():
		var collider: Object = hit.collider
		for key in ["npc_id", "building_id", "city_id"]:
			if collider.has_meta(key):
				meta[key] = collider.get_meta(key)
	hover_text = describe_hit(meta)
	world_view.hover_npc(int(meta.get("npc_id", -1)))
	tooltip.visible = hover_text != "" and not list_panel.get_global_rect().has_point(mouse)
	if tooltip.visible:
		tooltip_label.text = hover_text
		tooltip.position = mouse + Vector2(16, 18)


func _raycast(screen_position: Vector2) -> Dictionary:
	var camera: Camera3D = rig.camera
	var origin := camera.project_ray_origin(screen_position)
	var end := origin + camera.project_ray_normal(screen_position) * 400.0
	var query := PhysicsRayQueryParameters3D.create(origin, end)
	return camera.get_world_3d().direct_space_state.intersect_ray(query)


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_ESCAPE:
				if admin_window.is_open:
					admin_window.close()
				elif narrative.is_open:
					narrative.close()
				elif conversation.is_open:
					conversation.end_conversation()
				elif brain_panel.is_open:
					brain_panel.close()
				elif stage.active:
					exit_building()
				elif store.kind == "City":
					enter_scope("World", "")
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
				if rig.following != null:
					rig.release()
				else:
					follow_selected()
			KEY_PAGEUP, KEY_BRACKETRIGHT:
				if stage.active:
					stage.set_floor(stage.interior_view.current_floor + 1)
			KEY_PAGEDOWN, KEY_BRACKETLEFT:
				if stage.active:
					stage.set_floor(stage.interior_view.current_floor - 1)
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT and not stage.active:
		_pick(event.position, event.double_click)


## NPC visível mais próximo do clique (em pixels da tela), -1 se nenhum: NPC é pequeno e o raio costuma
## acertar o telhado ou o chão ao lado dele, então o clique perto do NPC vale como clique nele.
func nearest_npc(screen_position: Vector2, max_distance: float = 32.0) -> int:
	var camera: Camera3D = rig.camera
	var best := -1
	var best_distance := max_distance
	for id in world_view.npc_nodes:
		var actor: Node3D = world_view.npc_nodes[id]
		var point := actor.global_position + Vector3(0, 0.6, 0)
		if not actor.visible or camera.is_position_behind(point):
			continue
		var distance := camera.unproject_position(point).distance_to(screen_position)
		if distance < best_distance:
			best_distance = distance
			best = id
	return best


func _pick(screen_position: Vector2, double_click: bool) -> void:
	var near_npc := nearest_npc(screen_position)
	if near_npc >= 0:
		select_npc(near_npc)
		return
	var hit := _raycast(screen_position)
	if hit.is_empty():
		return
	var collider: Object = hit.collider
	if collider.has_meta("npc_id"):
		select_npc(int(collider.get_meta("npc_id")))
	elif collider.has_meta("building_id"):
		enter_building(int(collider.get_meta("building_id")))
	elif collider.has_meta("city_id"):
		var id: String = collider.get_meta("city_id")
		if double_click:
			enter_scope("City", id)
		elif store.kind == "World":
			_show_city_summary(id)


func _show_city_summary(city_id: String) -> void:
	var response: Dictionary = await api.city_snapshot(city_id)
	if response.ok and response.body.get("payload") != null:
		var payload: Dictionary = response.body.payload
		city_panel.show_city({"id": city_id, "name": payload.name, "indicators": payload.indicators})
