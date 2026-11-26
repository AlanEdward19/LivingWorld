extends RefCounted
## Monta a interface do App (barra do topo, inspetor, lista, janelas, minimapa, avisos, ajuda, dica e carregamento)
## e liga os sinais de cada painel às ações do App. Só construção: comportamento mora no App e nos módulos.
const WorldTime := preload("res://Contract/world_time.gd")

const Panels := preload("res://Scenes/panels.gd")
const UiTheme := preload("res://Scenes/ui_theme.gd")
const Minimap := preload("res://Scenes/minimap.gd")
const Toasts := preload("res://Scenes/toasts.gd")
const InteriorStage := preload("res://Scenes/interior_stage.gd")
const Inspector := preload("res://Scenes/inspector.gd")
const BrainPanel := preload("res://Scenes/brain_panel.gd")
const ConversationWindow := preload("res://Scenes/conversation_window.gd")
const NarrativeWindow := preload("res://Scenes/narrative_window.gd")
const AdminWindow := preload("res://Scenes/admin_window.gd")
const EconomyWindow := preload("res://Scenes/economy_window.gd")
const TechnologyWindow := preload("res://Scenes/technology_window.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const CharacterCreator := preload("res://Scenes/character_creator.gd")
const PlayerProfile := preload("res://Contract/player_profile.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")
const SkyLife := preload("res://Scenes/sky_life.gd")
const Ambience := preload("res://Scenes/ambience.gd")
const DayCycle := preload("res://Scenes/day_cycle.gd")
const EventTimeline := preload("res://Scenes/event_timeline.gd")
const PauseMenu := preload("res://Scenes/pause_menu.gd")
const FirstSteps := preload("res://Scenes/first_steps.gd")
const LoadingScreen := preload("res://Scenes/menu/loading_screen.gd")

const HELP := "Clique: ver quem é · Botão direito: andar · Roda: zoom · F: seguir · N: próximo acontecimento · V: assistir · X: raio-x · P: planta · ? ou F1: todos os atalhos · Esc: pausa"
## AD-197p: atalhos do modo jogador (3ª/1ª pessoa), no lugar dos do espectador.
const PLAYER_HELP := "WASD: andar · Mouse: olhar · E: interagir · Clique: bater · Tab: ordens · C: 1ª/3ª pessoa · Z: soltar o cursor · Esc: pausa"
const NO_ADMIN := "Só quem ligou o mundo (o admin) controla o tempo: abra o jogo pelo Jogar.command, que cuida disso sozinho."


static func build(app) -> void:
	var ui := Control.new()
	ui.set_anchors_preset(Control.PRESET_FULL_RECT)
	ui.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.theme = UiTheme.build()
	app._ui = ui
	var layer := CanvasLayer.new()
	layer.layer = 5
	app.add_child(layer)
	layer.add_child(ui)
	app.stage = InteriorStage.new()
	ui.add_child(app.stage)
	_top_bar(app, ui)
	_side(app, ui)
	_windows(app, ui)
	_bottom(app, ui)
	_overlays(app, ui)


static func _top_bar(app, ui: Control) -> void:
	var top := PanelContainer.new()
	app.top_bar = top # #222: some no modo jogador (a moldura mostra a hora)
	top.set_anchors_preset(Control.PRESET_TOP_WIDE)
	top.offset_left = 8
	top.offset_right = -8
	top.offset_top = 8
	ui.add_child(top)
	var bar := HBoxContainer.new()
	top.add_child(bar)
	app.breadcrumb = Label.new()
	app.breadcrumb.text = "Mundo"
	app.breadcrumb.add_theme_font_size_override("font_size", 19)
	bar.add_child(app.breadcrumb)
	app.world_button = Button.new()
	app.world_button.text = "◀ Mapa do mundo"
	app.world_button.tooltip_text = "Sai da cidade e mostra o mundo todo"
	app.world_button.visible = false
	app.world_button.pressed.connect(func(): app.enter_scope("World", ""))
	bar.add_child(app.world_button)
	app.follow_label = Label.new()
	app.follow_label.add_theme_color_override("font_color", Color("9fe0a8"))
	bar.add_child(app.follow_label)
	var left_spacer := Control.new()
	left_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(left_spacer)
	app.time_label = Label.new()
	app.time_label.add_theme_font_size_override("font_size", 19)
	app.time_label.add_theme_color_override("font_color", Color("ffe9a6"))
	bar.add_child(app.time_label)
	app.weather_label = Label.new() # AD-180: tempo do dia (estado, sensação, vento, chão, seca/ondas); detalhes na dica
	app.weather_label.add_theme_font_size_override("font_size", 15)
	app.weather_label.add_theme_color_override("font_color", Color("cfe3f0"))
	app.weather_label.mouse_filter = Control.MOUSE_FILTER_STOP
	app.weather_label.visible = false
	bar.add_child(app.weather_label)
	var right_spacer := Control.new()
	right_spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(right_spacer)
	app.pause_label = Label.new()
	app.pause_label.clip_text = true
	app.pause_label.custom_minimum_size.x = 120
	app.pause_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	app.pause_label.size_flags_stretch_ratio = 4.0
	app.pause_label.add_theme_color_override("font_color", Color("ffcf4a"))
	bar.add_child(app.pause_label)
	app.clock_panel = Panels.ClockPanel.new()
	app.clock_panel.api = app.api
	app.clock_panel.status_changed.connect(app._on_clock_status)
	app.clock_panel.show_status_text = false # o relógio do topo já diz ano e pausa; a linha técnica vira dica (cabe em 1280)
	bar.add_child(app.clock_panel)
	if not app.admin_enabled:
		app.clock_panel.set_control_enabled(false, NO_ADMIN)
	if app.admin_enabled:
		var console_button := Button.new()
		console_button.text = "Console (F8)"
		console_button.pressed.connect(func(): app._toggle_admin())
		bar.add_child(console_button)
	var creator_button := Button.new() # AD-197q: entrar como pessoa nova, tirada do agregado de uma cidade pelo motor
	creator_button.text = "Pessoa nova"
	creator_button.tooltip_text = "Entrar no mundo como alguém da população de uma cidade"
	creator_button.pressed.connect(func(): app.open_creator())
	bar.add_child(creator_button)
	app.help_button = Button.new()
	app.help_button.text = " ? "
	app.help_button.tooltip_text = "Dicas e atalhos"
	app.help_button.pressed.connect(func(): app.first_steps.toggle_help())
	bar.add_child(app.help_button)
	var menu_button := Button.new()
	menu_button.text = "☰"
	menu_button.tooltip_text = "Pausa, opções, voltar ao menu principal (F10) ou sair"
	menu_button.pressed.connect(func(): app.pause_menu.toggle())
	bar.add_child(menu_button)
	app.banner = Label.new()
	app.banner.visible = false
	app.banner.add_theme_color_override("font_color", Color("ffcf4a"))
	app.banner.add_theme_font_size_override("font_size", 22)
	app.banner.set_anchors_preset(Control.PRESET_CENTER_TOP)
	app.banner.position = Vector2(-220, 80)
	ui.add_child(app.banner)


static func _side(app, ui: Control) -> void:
	var scroll := ScrollContainer.new() # #222: a ficha longa rola dentro da tela em vez de sair por baixo
	scroll.set_anchors_preset(Control.PRESET_RIGHT_WIDE)
	scroll.offset_left = -314
	scroll.offset_right = -8
	scroll.offset_top = 64
	scroll.offset_bottom = -40
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	scroll.mouse_filter = Control.MOUSE_FILTER_PASS
	ui.add_child(scroll)
	var side := VBoxContainer.new()
	side.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(side)
	app.side_panel = scroll
	var inspector := Inspector.new()
	inspector.reader = app.api
	inspector.registry = app.registry
	inspector.store = app.store
	inspector.labels = app.labels
	inspector.date_of = app._date_text
	inspector.hours_per_day = func() -> float: return app.day_cycle.hours_per_day
	inspector.event_point = func(id: int) -> Vector3: return app.world_view.events.positions.get(id, Vector3.INF)
	inspector.point_of = func(entity: Dictionary) -> Vector3: return app.point_of(entity)
	inspector.follow_requested.connect(func(entity: Dictionary): app.follow(entity))
	inspector.enter_requested.connect(func(id: int): app.enter_building(id))
	inspector.trades_requested.connect(func(id: int): app.open_trades(id))
	inspector.focus_requested.connect(app.focus_point)
	side.add_child(inspector)
	app.inspector = inspector
	var npc_panel = inspector.npc_panel
	app.npc_panel = npc_panel
	app.city_panel = inspector.city_panel
	npc_panel.api = app.api
	npc_panel.labels = app.labels
	npc_panel.name_of = func(id: int) -> String: return app.registry.person_name(id)
	npc_panel.profession_name_of = func(id: int) -> String: return app.registry.profession_name(id)
	npc_panel.skill_name_of = func(id: int) -> String: return String(app.registry.skills.get(id, "habilidade %d" % id))
	npc_panel.highlight.connect(func(id: int): app._highlight(id))
	npc_panel.model_known.connect(app._apply_model)
	npc_panel.follow_requested.connect(func(_id: int): app.follow_selected())
	npc_panel.incarnate_requested.connect(func(id: int): app.incarnate(id))
	npc_panel.brain_requested.connect(func(id: int): app.open_brain(id))
	npc_panel.conversation_requested.connect(func(id: int): app.talk_to(id))
	npc_panel.culture_shown.connect(func(id: int): app._probe_technologies(id))
	npc_panel.technologies_requested.connect(func(id: int): app._show_technologies(id))
	npc_panel.trades_requested.connect(func(workplace_id: int): app.open_trades(workplace_id))
	npc_panel.warning.connect(func(text: String): app.toasts.push(text))
	npc_panel.biography_requested.connect(func(id: int): app.open_biography(id))
	app.city_panel.treasury_requested.connect(func(city_id: String): app.open_treasury(city_id))
	app.city_panel.anonymous_requested.connect(func(id: int): app.select_npc(id))
	app.city_panel.chronicle_requested.connect(func(city_id: String): app.open_chronicle(city_id))
	app.city_panel.beliefs_requested.connect(func(city_id: String): app.open_beliefs(city_id))
	app.store.npc_changed.connect(app._on_focus_action)

	app.player_hud = PlayerHud.new()
	app.player_hud.api = app.api
	app.player_hud.labels = app.labels
	app.player_hud.player_id = app.player_id if app.player_id != "" else PlayerProfile.load_or_create()
	app.player_hud.session_enabled = true # AD-197a: sessão por token e canal persistente; o id local fica só como reserva
	app.player_hud.warning.connect(func(text: String): app.toasts.push(text))
	app.player_hud.released.connect(func(): app.player_released())
	app.player_hud.set_anchors_preset(Control.PRESET_TOP_WIDE)
	app.player_hud.offset_left = 310
	app.player_hud.offset_right = -316
	app.player_hud.offset_top = 60
	ui.add_child(app.player_hud)
	# AD-197d/197j: WASD relativo ao olhar da câmera; o passo previsto e corrigido pelo motor; golpe, nomes e mira
	app.player_hud.heading = app.rig.heading
	app.player_hud.predicted.connect(app._on_player_predicted)
	app.player_hud.confirmed.connect(app._on_player_confirmed)
	app.player_hud.rejected.connect(app._on_player_rejected)
	app.player_hud.conversation_opened.connect(func(id: int, name: String, session: int):
		app._on_player_menu(true)
		app.conversation.attach(id, name, session))
	app.player_hud.struck.connect(app._on_player_struck)
	app.player_hud.glided.connect(app._on_player_fine)
	app.player_hud.settled.connect(app._on_player_fine)
	app.player_hud.menu_toggled.connect(app._on_player_menu)
	app.player_hud.resource_name_of = func(id: int) -> String: return String(app.registry.resources.get(id, "recurso %d" % id))
	app.player_frame = PlayerFrame.new() # #222: a moldura de jogo do modo jogador (barras, dica do E, teclas, menu do E)
	ui.add_child(app.player_frame)
	app.player_hud.frame = app.player_frame
	app.player_frame.chosen.connect(func(entry: Dictionary): app.player_hud.interact(entry))
	app.player_hud.jumped.connect(app._on_player_jumped)
	app.crosshair = Label.new() # AD-197j: a mira do modo jogador
	app.crosshair.text = "+"
	app.crosshair.add_theme_font_size_override("font_size", 28)
	app.crosshair.set_anchors_preset(Control.PRESET_CENTER)
	app.crosshair.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	app.crosshair.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	app.crosshair.mouse_filter = Control.MOUSE_FILTER_IGNORE
	app.crosshair.visible = false
	ui.add_child(app.crosshair)
	var hint: Label = app.player_hud.reach_hint # AD-197p: o nome curto do que o E alcançaria, logo abaixo da mira
	hint.set_anchors_preset(Control.PRESET_CENTER)
	hint.offset_left = -200
	hint.offset_right = 200
	hint.offset_top = 22
	hint.offset_bottom = 44
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	ui.add_child(hint)
	app.rig.mode_changed.connect(app._on_camera_mode)
	app.rig.blockers = app.camera_blockers # AD-197p: braço de mola contra os prédios desenhados
	app.rig.see_through.connect(func(ids: Dictionary): app.world_view.buildings.set_faded(ids))
	app.rig.opened.connect(func(ids: Dictionary): app.world_view.buildings.set_opened(ids)) # AD-197s

	app.list_panel = Panels.NpcListPanel.new()
	app.list_panel.api = app.api
	app.list_panel.visible = false
	app.list_panel.set_anchors_preset(Control.PRESET_LEFT_WIDE)
	app.list_panel.offset_left = 8
	app.list_panel.offset_right = 296
	app.list_panel.offset_top = 64
	app.list_panel.offset_bottom = -272
	app.list_panel.npc_selected.connect(func(id: int): app.select_npc(id))
	ui.add_child(app.list_panel)


static func _calendar(app) -> Dictionary:
	return {"hoursPerDay": app.day_cycle.hours_per_day, "daysPerMonth": app.day_cycle.days_per_month, "monthsPerYear": app.day_cycle.months_per_year}


static func _windows(app, ui: Control) -> void:
	app.brain_panel = BrainPanel.new()
	app.brain_panel.api = app.api
	app.brain_panel.labels = app.labels
	app.brain_panel.date_of = app._date_text
	app.brain_panel.name_of = func(id: int) -> String: return String(app.store.names.get(id, "pessoa nº %d" % id))
	app.narrative = NarrativeWindow.new()
	app.narrative.api = app.api
	app.narrative.calendar_of = func() -> Dictionary: return _calendar(app)
	app.narrative.tick_now = func() -> int: return WorldTime.hours_of(app.clock_panel.status, "atMs")
	app.narrative.date_of = app._date_text
	app.admin_window = AdminWindow.new()
	app.admin_window.api = app.api
	app.admin_window.calendar_of = func() -> Dictionary: return {"hoursPerDay": app.day_cycle.hours_per_day}
	app.admin_window.date_of = app._date_text
	app.admin_window.npc_selected.connect(func(id: int): app.select_npc(id))
	app.admin_window.family_requested.connect(app.open_family_builder)
	app.economy = EconomyWindow.new()
	app.economy.api = app.api
	app.economy.calendar_of = func() -> Dictionary: return _calendar(app)
	app.economy.date_of = app._date_text
	app.economy.resource_name_of = func(id: int) -> String: return String(app.registry.resources.get(id, "recurso %d" % id))
	app.technology = TechnologyWindow.new()
	app.technology.api = app.api
	app.technology.date_of = app._date_text
	app.creator = CharacterCreator.new()
	app.creator.api = app.api
	app.creator.hud = app.player_hud
	app.creator.entered.connect(app._on_created)
	app.router.host = ui
	for window in [app.brain_panel, app.narrative, app.admin_window, app.economy, app.technology, app.creator]:
		window.warning.connect(func(text: String): app.toasts.push(text))
		ui.add_child(window)
		app.router.register(window)
	app.conversation = ConversationWindow.new()
	app.conversation.api = app.api
	app.conversation.closed.connect(func(): app._on_player_menu(false)) # AD-197k: fechada a conversa, o cursor volta a ser o olhar
	app.conversation.set_anchors_preset(Control.PRESET_FULL_RECT)
	app.conversation.anchor_top = 1.0 # faixa fixa na base da tela
	app.conversation.offset_left = 310
	app.conversation.offset_right = -430
	app.conversation.offset_top = -262
	app.conversation.offset_bottom = -34
	app.conversation.warning.connect(func(text: String): app.toasts.push(text))
	ui.add_child(app.conversation)


static func _bottom(app, ui: Control) -> void:
	app.minimap = Minimap.new()
	app.minimap.bind(app.store)
	app.minimap.set_anchors_preset(Control.PRESET_BOTTOM_LEFT)
	app.minimap.offset_left = 8
	app.minimap.offset_top = -258
	app.minimap.offset_right = 218
	app.minimap.offset_bottom = -48
	app.minimap.pan_requested.connect(func(point: Vector3): app.rig.focus(point))
	ui.add_child(app.minimap)
	app.timeline = EventTimeline.new()
	app.timeline.registry = app.registry
	app.timeline.date_of = app._date_text
	app.timeline.set_anchors_preset(Control.PRESET_BOTTOM_LEFT)
	app.timeline.offset_left = 226
	app.timeline.offset_right = 600
	app.timeline.offset_top = -80
	app.timeline.offset_bottom = -40
	app.timeline.grow_vertical = Control.GROW_DIRECTION_BEGIN
	app.timeline.visible = false
	app.timeline.event_chosen.connect(func(id: int): app.goto_event(id))
	app.timeline.next_requested.connect(func(): app.next_event())
	app.timeline.watch_toggled.connect(func(_on: bool): app.toggle_watch())
	ui.add_child(app.timeline)
	app.toasts = Toasts.new()
	app.toasts.set_anchors_preset(Control.PRESET_CENTER_TOP) # #222: avisos no alto, no meio: nem sobre a ficha nem sobre a linha do tempo
	app.toasts.offset_left = -300
	app.toasts.offset_right = 300
	app.toasts.offset_top = 64
	app.toasts.offset_bottom = 240
	ui.add_child(app.toasts)
	var help := Label.new()
	help.text = HELP
	help.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	help.offset_top = -30
	help.offset_left = 10
	help.add_theme_color_override("font_color", Color(1, 1, 1, 0.8))
	help.add_theme_font_size_override("font_size", 13)
	help.add_theme_constant_override("outline_size", 5) # #222: legível sobre chão claro
	help.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.75))
	ui.add_child(help)
	app.help_label = help
	app.tooltip = PanelContainer.new()
	app.tooltip.visible = false
	app.tooltip.mouse_filter = Control.MOUSE_FILTER_IGNORE
	app.tooltip_label = Label.new()
	app.tooltip.add_child(app.tooltip_label)
	ui.add_child(app.tooltip)
	var loading := LoadingScreen.new()
	ui.add_child(loading)
	loading.set_heading("Entrando no mundo")
	loading.set_stage(3)
	app.loading = loading


## Por cima de tudo: dicas dos primeiros passos, ajuda, menu de pausa e a cortina que abre suave ao entrar no mundo.
static func _overlays(app, ui: Control) -> void:
	app.first_steps = FirstSteps.new()
	app.first_steps.app = app
	app.first_steps.settings = app.settings
	ui.add_child(app.first_steps)
	app.pause_menu = PauseMenu.new()
	app.pause_menu.app = app
	app.pause_menu.menu_pressed.connect(func(): app.menu_requested.emit())
	app.pause_menu.quit_pressed.connect(func(): app.quit_requested.emit())
	app.pause_menu.help_pressed.connect(func(): app.first_steps.open_help())
	ui.add_child(app.pause_menu)
	app.curtain = ColorRect.new()
	app.curtain.color = Color("0d1420")
	app.curtain.set_anchors_preset(Control.PRESET_FULL_RECT)
	app.curtain.mouse_filter = Control.MOUSE_FILTER_IGNORE
	app.curtain.visible = false
	ui.add_child(app.curtain)


## Cena 3D: mundo, câmera, sol, ambiente, céu, som e ciclo do dia.
static func build_scene(app) -> void:
	app.world_view = WorldView.new()
	app.world_view.bind(app.store, app.labels)
	app.world_view.npcs.catalog = app.catalog
	app.world_view.npcs.model_of = app.registry.model_of
	for renderer in [app.world_view.npcs, app.world_view.items]:
		renderer.design_of = func(id: int) -> Variant: return app.api.cached_design(id)
		renderer.request_design = func(id: int) -> void: app.request_design(id)
	app.add_child(app.world_view)
	app.rig = CameraRig.new()
	app.rig.city_at = func(point: Vector3) -> String: return app.world_view.city_at(point)
	app.rig.walking = func() -> bool: return app.player_hud != null and app.player_hud.is_active # AD-197e: WASD/setas andam com o personagem
	app.rig.scope_requested.connect(func(kind: String, ref_id: String): app.enter_scope(kind, ref_id))
	app.add_child(app.rig)
	app.rig.camera.current = true
	app._sun = DirectionalLight3D.new()
	app._sun.shadow_enabled = true
	app._sun.directional_shadow_max_distance = 120.0
	app.add_child(app._sun)
	var world_environment := WorldEnvironment.new()
	app._environment = Environment.new()
	app._environment.background_mode = Environment.BG_COLOR
	app._environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	app._environment.tonemap_mode = Environment.TONE_MAPPER_ACES
	app._environment.tonemap_exposure = 0.9
	app._environment.ssao_enabled = true
	app._environment.ssao_intensity = 1.6
	world_environment.environment = app._environment
	app.add_child(world_environment)
	app.sky_life = SkyLife.new()
	app.add_child(app.sky_life)
	app.sky_life.build(Vector2(30, 30))
	app.ambience = Ambience.new()
	app.ambience.catalog = app.catalog
	app.add_child(app.ambience)
	app.day_cycle = DayCycle.new()
	app.add_child(app.day_cycle)
	app.day_cycle.bind(app._sun, app._environment, app.world_view.window_material, app.world_view.lamp_material)
	app.world_view.weather_changed.connect(app._on_weather) # AD-180: sol do motor e indicador do tempo
