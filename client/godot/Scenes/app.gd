extends Node
## Raiz do cliente: liga transporte, assinatura, store, cenas e painéis. Só leitura do mundo:
## o único que escreve no servidor são os comandos explícitos do relógio.

const CursorStream := preload("res://Contract/cursor_stream.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const Store := preload("res://Store/world_store.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")
const InteriorStage := preload("res://Scenes/interior_stage.gd")
const Panels := preload("res://Scenes/panels.gd")
const DayCycle := preload("res://Scenes/day_cycle.gd")
const UiTheme := preload("res://Scenes/ui_theme.gd")
const Minimap := preload("res://Scenes/minimap.gd")
const Toasts := preload("res://Scenes/toasts.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")

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
var clock_panel
var list_panel
var npc_panel
var city_panel
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
var _interior_timer: Timer
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
	stream = CursorStream.new(transport, get_tree())
	stream.validate_shapes = validate_shapes
	if sleeper.is_valid():
		stream.sleeper = sleeper
	stream.snapshot_received.connect(_on_snapshot)
	stream.delta_applied.connect(_on_delta)
	stream.connection_changed.connect(_on_connection)
	stream.access_denied.connect(func(): banner.text = "acesso negado a este escopo"; banner.visible = true)
	var catalog: Dictionary = await api.action_catalog()
	if catalog.ok:
		labels.load_from(catalog.body)
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
	day_cycle = DayCycle.new()
	add_child(day_cycle)
	day_cycle.bind(_sun, _environment, world_view.window_material)


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
	clock_panel = Panels.ClockPanel.new()
	clock_panel.api = api
	clock_panel.changed.connect(func(): _resubscribe_after_clock_command())
	clock_panel.status_changed.connect(_on_clock_status)
	bar.add_child(clock_panel)

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
	npc_panel.highlight.connect(func(id: int): _highlight(id))
	npc_panel.follow_requested.connect(func(_id: int): follow_selected())
	side.add_child(npc_panel)
	city_panel = Panels.CityPanel.new()
	side.add_child(city_panel)

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
	help.text = "Botão direito: mover · Shift+botão direito: girar · Roda: zoom · Q/E: girar · L: pessoas · F: seguir · Esc: voltar · PgUp/PgDn: andar · clique: selecionar/entrar"
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

	_interior_timer = Timer.new()
	_interior_timer.wait_time = 1.0
	_interior_timer.timeout.connect(_poll_interior)
	add_child(_interior_timer)
	_names_timer = Timer.new()
	_names_timer.wait_time = 20.0
	_names_timer.autostart = true
	_names_timer.timeout.connect(_load_names)
	add_child(_names_timer)


# --- relógio, eventos, nomes -----------------------------------------------

func _on_clock_status(status: Dictionary) -> void:
	day_cycle.sync(float(status.tick), float(status.ticksPerSecond), bool(status.isPaused))
	_update_time_label(status)


func _update_time_label(status: Dictionary = {}) -> void:
	var s: Dictionary = status if not status.is_empty() else clock_panel.status
	if s.is_empty():
		return
	var day := int(float(s.tick) / 24.0) % 360 + 1
	time_label.text = "Ano %d · Dia %d · %s · %s%s" % [int(s.year), day, day_cycle.clock_text(), day_cycle.phase_name(),
		"  ⏸" if s.isPaused else ""]


func _process(delta: float) -> void:
	if time_label != null and day_cycle != null:
		_update_time_label()
	if minimap != null:
		minimap.set_camera_target(rig.target)
		stage.set_daylight(day_cycle.daylight())
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
	banner.visible = false
	if not _first_snapshot:
		_first_snapshot = true
		loading.visible = false
		minimap.rebuild()
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


func _resubscribe_after_clock_command() -> void:
	# `step` e `advance-year` não publicam delta (o servidor só publica no laço de tempo real):
	# reassina o escopo atual para mostrar o estado novo.
	if stage.active:
		return
	await stream.subscribe(_world_ref[0], _world_ref[1])


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
	stage.enter(store.interior, origin, labels, rig.camera)
	stage.interior_view.apply_names(store.names)
	breadcrumb.text = "%s › Edifício %d" % [breadcrumb.text.get_slice(" › Edifício", 0), _interior_building]
	if marker != null:
		rig.focus(Vector3(origin.x + 1.5, 0, origin.y + 1.5))
		rig.set_zoom(7.0)
	_interior_timer.start()


func exit_building() -> void:
	if not stage.active:
		return
	_interior_timer.stop()
	stage.leave()
	_interior_building = -1
	await stream.subscribe(_previous_scope[0], _previous_scope[1])


func _poll_interior() -> void:
	if not stage.active:
		return
	var response: Dictionary = await api.interior_snapshot(_interior_building)
	if response.ok and stage.active:
		store.apply_snapshot(response.body)
		stage.interior_view.set_occupants(store.interior.occupants)
		stage.interior_view.apply_names(store.names)


# --- seleção e hover -------------------------------------------------------

func select_npc(id: int) -> void:
	await npc_panel.show_npc(id)


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
		return "Edifício %d · %d andar%s\nclique para entrar" % [meta.building_id, floors, "es" if floors > 1 else ""]
	if meta.has("city_id"):
		var city = store.cities.get(meta.city_id)
		if city != null:
			return "%s · %d hab.\nduplo clique para entrar" % [city.name if city.name != "" else "Cidade", city.population]
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
				if stage.active:
					exit_building()
				elif store.kind == "City":
					enter_scope("World", "")
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


func _pick(screen_position: Vector2, double_click: bool) -> void:
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
		city_panel.show_city({"name": payload.name, "indicators": payload.indicators})
