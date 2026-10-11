extends Node
## Raiz do cliente: liga transporte, assinatura, store, cenas e painéis. Só leitura do mundo:
## o único que escreve no servidor são os comandos explícitos do relógio.

const CursorStream := preload("res://Contract/cursor_stream.gd")
const HttpTransport := preload("res://Contract/http_transport.gd")
const Store := preload("res://Store/world_store.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const WorldView := preload("res://Scenes/world_view.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")
const InteriorStage := preload("res://Scenes/interior_stage.gd")
const Panels := preload("res://Scenes/panels.gd")
const DevSteps := preload("res://Scenes/dev_steps.gd")

var transport
var stream
var store
var labels
var world_view
var rig
var stage
var clock_panel
var list_panel
var npc_panel
var city_panel
var banner: Label
var breadcrumb: Label
var selected_npc := -1

var _previous_scope := ["World", ""]
var _world_ref := ["World", ""]
var _interior_building := -1
var _interior_timer: Timer
var _ui: CanvasLayer


func _ready() -> void:
	var real := HttpTransport.new()
	add_child(real)
	await start(real)
	var steps := DevSteps.parse(OS.get_cmdline_user_args())
	if not steps.is_empty():
		await DevSteps.run(self, steps)


## `p_transport`: HttpTransport em produção, FakeTransport nos testes.
func start(p_transport, validate_shapes: bool = true) -> void:
	transport = p_transport
	store = Store.new()
	labels = ActionLabels.new()
	_build_scene()
	_build_ui()
	stream = CursorStream.new(transport, get_tree())
	stream.validate_shapes = validate_shapes
	stream.snapshot_received.connect(_on_snapshot)
	stream.delta_applied.connect(func(envelope): store.apply_delta(envelope))
	stream.connection_changed.connect(_on_connection)
	stream.access_denied.connect(func(): banner.text = "acesso negado a este escopo"; banner.visible = true)
	var catalog: Dictionary = await transport.request("GET", "/visual/catalog/actions")
	if catalog.status == 200 and catalog.body is Array:
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
	var sun := DirectionalLight3D.new()
	sun.rotation = Vector3(deg_to_rad(-52), deg_to_rad(-35), 0)
	sun.shadow_enabled = true
	sun.light_energy = 0.85
	add_child(sun)
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color("a9c7d8")
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color(0.78, 0.82, 0.9)
	environment.environment.ambient_light_energy = 0.5
	environment.environment.tonemap_mode = Environment.TONE_MAPPER_ACES
	environment.environment.tonemap_exposure = 0.85
	add_child(environment)


func _build_ui() -> void:
	_ui = CanvasLayer.new()
	_ui.layer = 5
	add_child(_ui)
	stage = InteriorStage.new()
	_ui.add_child(stage)
	var top := PanelContainer.new()
	top.set_anchors_preset(Control.PRESET_TOP_WIDE)
	_ui.add_child(top)
	var bar := HBoxContainer.new()
	top.add_child(bar)
	breadcrumb = Label.new()
	breadcrumb.text = "Mundo"
	breadcrumb.add_theme_font_size_override("font_size", 18)
	bar.add_child(breadcrumb)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	bar.add_child(spacer)
	clock_panel = Panels.ClockPanel.new()
	clock_panel.transport = transport
	clock_panel.changed.connect(func(): _resubscribe_after_clock_command())
	bar.add_child(clock_panel)
	banner = Label.new()
	banner.visible = false
	banner.add_theme_color_override("font_color", Color("ffcf4a"))
	banner.add_theme_font_size_override("font_size", 20)
	banner.set_anchors_preset(Control.PRESET_CENTER_TOP)
	banner.position = Vector2(-220, 120)
	_ui.add_child(banner)
	var side := VBoxContainer.new()
	side.set_anchors_preset(Control.PRESET_RIGHT_WIDE)
	side.offset_left = -296
	side.offset_right = -8
	side.offset_top = 48
	_ui.add_child(side)
	npc_panel = Panels.NpcDetailPanel.new()
	npc_panel.transport = transport
	npc_panel.highlight.connect(func(id: int): _highlight(id))
	side.add_child(npc_panel)
	city_panel = Panels.CityPanel.new()
	side.add_child(city_panel)
	list_panel = Panels.NpcListPanel.new()
	list_panel.transport = transport
	list_panel.visible = false
	list_panel.set_anchors_preset(Control.PRESET_LEFT_WIDE)
	list_panel.offset_left = 8
	list_panel.offset_right = 296
	list_panel.offset_top = 48
	list_panel.offset_bottom = -40
	list_panel.npc_selected.connect(func(id: int): select_npc(id))
	_ui.add_child(list_panel)
	var help := Label.new()
	help.text = "Arrastar botão direito: mover · Shift+arrastar: girar · Roda: zoom · Q/E: girar · L: lista · F: seguir · Esc: voltar · PgUp/PgDn: andar"
	help.set_anchors_preset(Control.PRESET_BOTTOM_WIDE)
	help.position.y = -26
	help.add_theme_color_override("font_color", Color(1, 1, 1, 0.85))
	help.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.6))
	_ui.add_child(help)
	_interior_timer = Timer.new()
	_interior_timer.wait_time = 1.0
	_interior_timer.timeout.connect(_poll_interior)
	add_child(_interior_timer)


# --- escopos -------------------------------------------------------------

func enter_scope(kind: String, ref_id: String) -> void:
	_world_ref = [kind, ref_id]
	await stream.subscribe(kind, ref_id)


func _on_snapshot(envelope: Dictionary) -> void:
	var kind := Store.scope_name(envelope.scope.kind)
	store.apply_snapshot(envelope)
	banner.visible = false
	if kind == "Interior":
		if stage.active and stage.interior_view.floor_count == int(store.interior.floors):
			stage.interior_view.set_occupants(store.interior.occupants) # reenvio do snapshot pelo canal: só atualiza
		else:
			_enter_interior_view()
		return
	rig.set_scope(kind)
	if kind == "City":
		breadcrumb.text = "Mundo › %s" % (store.city.name if store.city.name != "" else "Cidade")
		city_panel.show_city(store.city)
		rig.focus(_center_of(store.city.bounds))
	elif kind == "World":
		breadcrumb.text = "Mundo"
		city_panel.visible = false


func _center_of(bounds: Rect2i) -> Vector3:
	return Vector3(bounds.position.x + bounds.size.x / 2.0, 0, bounds.position.y + bounds.size.y / 2.0)


func _on_connection(state: String, retry_in: float) -> void:
	banner.visible = state == "offline"
	if state == "offline":
		banner.text = "sem conexão — nova tentativa em %d s" % int(retry_in)


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
	breadcrumb.text = "%s › Edifício %d" % [breadcrumb.text.get_slice(" › Edifício", 0), _interior_building]
	if marker != null:
		rig.focus(Vector3(origin.x + 1.5, 0, origin.y + 1.5))
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
	var path := "/visual/subscribe?scope=Interior&refId=%d&mode=Spectator" % _interior_building
	var response: Dictionary = await transport.request("GET", path)
	if response.status == 200 and response.body is Dictionary and stage.active:
		store.apply_snapshot(response.body)
		stage.interior_view.set_occupants(store.interior.occupants)


# --- seleção -------------------------------------------------------------

func select_npc(id: int) -> void:
	await npc_panel.show_npc(id)


func _highlight(id: int) -> void:
	selected_npc = id
	world_view.highlight_npc(id)


func follow_selected() -> void:
	if world_view.npc_nodes.has(selected_npc):
		rig.follow(world_view.npc_nodes[selected_npc])


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
	var camera: Camera3D = rig.camera
	var origin := camera.project_ray_origin(screen_position)
	var end := origin + camera.project_ray_normal(screen_position) * 400.0
	var query := PhysicsRayQueryParameters3D.create(origin, end)
	var hit := camera.get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return
	var collider: Object = hit.collider
	if collider.has_meta("npc_id"):
		select_npc(int(collider.get_meta("npc_id")))
	elif collider.has_meta("building_id"):
		enter_building(int(collider.get_meta("building_id")))
	elif collider.has_meta("city_id"):
		var id: String = collider.get_meta("city_id")
		if double_click or store.kind == "World":
			if double_click:
				enter_scope("City", id)
			else:
				_show_city_summary(id)


func _show_city_summary(city_id: String) -> void:
	var response: Dictionary = await transport.request("GET", "/visual/subscribe?scope=City&refId=%s&mode=Spectator" % city_id.uri_encode())
	if response.status == 200 and response.body is Dictionary and response.body.get("payload") != null:
		var payload: Dictionary = response.body.payload
		city_panel.show_city({"name": payload.name, "indicators": payload.indicators})
