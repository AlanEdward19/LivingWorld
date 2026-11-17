extends RefCounted
## Leitura da cidade (AD-177, "tá tudo muito grudado, não dá para analisar nada"): alternar entre os prédios empilhados
## sob o cursor (Tab ou Alt+roda: os da frente ficam translúcidos e o clique pega o escolhido), raio-x (X: todos os
## prédios translúcidos, telhado quase some) e a planta baixa (P: lotes, ruas por superfície, prédios pela função,
## com legenda) e o mapa de calor do pisoteio (H, AD-181). Só leitura; nada aqui fala com o servidor.

const CityPlan := preload("res://Scenes/city_plan.gd")

var app
var plan ## CityPlan (Control por baixo dos painéis)
var stack: Array = [] ## prédios cortados pelo raio do cursor, do mais perto ao mais longe
var stack_index := 0
var pushed: Dictionary = {} ## id -> true: os da frente do escolhido (translúcidos, o mouse atravessa)


func _init(p_app) -> void:
	app = p_app


func build(ui: Control) -> void:
	plan = CityPlan.new()
	plan.app = app
	ui.add_child(plan)
	ui.move_child(plan, 0)
	app.store.roads_changed.connect(_on_store_changed)
	for one_arg in [app.store.buildings_changed, app.store.building_removed, app.store.building_states_changed, app.store.reloaded]:
		(one_arg as Signal).connect(_on_store_changed.unbind(1))


func _on_store_changed() -> void:
	if plan_open():
		plan.refresh()


## Prédios cortados pelo raio que sai do ponto da tela, do mais perto para o mais longe (só na cidade).
func stack_at(screen: Vector2) -> Array:
	var camera: Camera3D = app.rig.camera
	if not camera.is_inside_tree() or app.store.kind != "City" or app.stage.active:
		return []
	var origin := camera.project_ray_origin(screen)
	var direction := camera.project_ray_normal(screen)
	var hits: Array = []
	var nodes: Dictionary = app.world_view.buildings.nodes
	for id in nodes:
		var box: AABB = nodes[id].get_meta("box", AABB())
		if box.size == Vector3.ZERO:
			continue
		var at = box.intersects_ray(origin, direction)
		if at != null:
			hits.append([origin.distance_to(at), int(id)])
	hits.sort_custom(func(a, b): return a[0] < b[0])
	return hits.map(func(h): return h[1])


## Tab / Alt+roda: o próximo prédio da pilha sob o cursor (dá a volta). Devolve o escolhido, ou -1 sem pilha.
func cycle(screen: Vector2, step := 1) -> int:
	var now := stack_at(screen)
	if now.is_empty():
		reset()
		return -1
	if now != stack:
		stack = now
		stack_index = 0
	stack_index = posmod(stack_index + step, stack.size())
	pushed = {}
	for i in stack_index:
		pushed[stack[i]] = true
	app.selection.update_fade({})
	return int(stack[stack_index])


func picked() -> int:
	return int(stack[stack_index]) if not stack.is_empty() else -1


## O cursor saiu da pilha: a alternância acaba e os prédios voltam.
func on_hover(screen: Vector2) -> void:
	if not stack.is_empty() and stack_at(screen) != stack:
		reset()


func reset() -> void:
	stack = []
	stack_index = 0
	pushed = {}


func toggle_xray() -> bool:
	var on: bool = not app.world_view.buildings.xray
	app.world_view.buildings.set_xray(on)
	app.toasts.push("Ver por dentro (raio-x): %s · X alterna" % ("ligado" if on else "desligado"))
	return on


## H (AD-181): mapa de calor do pisoteio (`wear` de cada rua) no chão 3D e na planta.
func toggle_heat() -> bool:
	var ground = app.world_view.ground
	ground.set_heat(not ground.heat)
	if plan_open():
		plan.queue_redraw()
	app.toasts.push("Mapa de calor do pisoteio: %s · H alterna" % ("ligado" if ground.heat else "desligado"))
	return ground.heat


func plan_open() -> bool:
	return plan != null and plan.visible


func toggle_plan() -> bool:
	plan.visible = not plan.visible
	if plan.visible:
		plan.refresh()
	else:
		app.tooltip.visible = false
	return plan.visible


## Esc fecha a planta antes de qualquer outra coisa.
func close_overlays() -> bool:
	if plan_open():
		toggle_plan()
		return true
	return false
