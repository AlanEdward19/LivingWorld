extends Node3D
## NPCs do escopo: um NpcActor por pessoa, movido e posado pelo que a store recebe. Anima só o que se vê (LOD):
## dentro do raio da câmera monta a pose completa (alternando metades em multidões); fora só interpola a posição.
## Idade e profissão no corpo: o escopo não traz (lacuna da etapa 2); quem já teve a ficha lida usa `model_of`.

const NpcActor := preload("res://Scenes/npc_actor.gd")

const STEP_SECONDS := 1.0 ## duração visual de um passo; o servidor decide a posição, o cliente só interpola

var store
var labels
var catalog ## AssetCatalog (modelo por profissão e idade); nulo = corpo base
var model_of: Callable = func(_id: int) -> Dictionary: return {} ## {profession, age} conhecidos, vazio se não
var nodes: Dictionary = {} ## id -> NpcActor (o WorldView expõe o mesmo dicionário como npc_nodes)
var selected := -1
var _frame := 0
var _hovered := -1


func rebuild() -> void:
	for node in nodes.values():
		_dispose(node)
	nodes.clear()
	_hovered = -1
	for id in store.npcs:
		spawn(store.npcs[id])


func _dispose(node: Node) -> void:
	if node.is_inside_tree():
		node.queue_free()
	else:
		node.free()


func spawn(info: Dictionary) -> void:
	var actor := NpcActor.new()
	add_child(actor)
	actor.setup(info.id, info.cell, info.action, labels, info.get("look", {}))
	actor.set_selected(info.id == selected)
	if store.names.has(info.id):
		actor.set_display_name(store.names[info.id])
	nodes[info.id] = actor
	apply_known_model(int(info.id))


## Profissão e idade de quem já teve a ficha lida (o corpo deixa de ser o modelo base também depois de trocar de escopo).
func apply_known_model(id: int) -> void:
	var actor = nodes.get(id)
	if actor == null or catalog == null:
		return
	var model: Dictionary = model_of.call(id)
	if not model.is_empty():
		actor.apply_model(catalog, int(model.profession), float(model.age))


func on_changed(id: int, from_cell: Vector2i, to_cell: Vector2i, action: int) -> void:
	var actor = nodes.get(id)
	if actor == null:
		spawn(store.npcs[id])
		return
	if action != actor.action:
		actor.set_action(action, labels)
	if to_cell != from_cell:
		actor.move_to(to_cell, STEP_SECONDS)


func on_removed(id: int) -> void:
	var actor = nodes.get(id)
	if actor != null:
		_dispose(actor)
		nodes.erase(id)


func highlight(id: int) -> void:
	if nodes.has(selected):
		nodes[selected].set_selected(false)
	selected = id
	if nodes.has(id):
		nodes[id].set_selected(true)


## Só o NPC que perdeu e o que ganhou o cursor mudam (antes eram os 500 a cada 70 ms).
func hover(id: int) -> void:
	if id == _hovered:
		return
	if nodes.has(_hovered):
		nodes[_hovered].set_hovered(false)
	_hovered = id
	if nodes.has(id):
		nodes[id].set_hovered(true)


func apply_names() -> void:
	for id in nodes:
		if store.names.has(id):
			nodes[id].set_display_name(store.names[id])


func advance(delta: float, view_center: Vector3, view_radius: float, far_zoom: bool, bubble_zoom: bool) -> void:
	_frame += 1
	var crowded := nodes.size() > 150
	var far_now := far_zoom and crowded
	for id in nodes:
		var actor = nodes[id]
		actor.set_far(far_now)
		actor.set_bubble(bubble_zoom)
		var near: bool = not bubble_zoom and actor.position.distance_to(view_center) <= view_radius
		actor.tick(delta, near, not crowded or (id + _frame) % 2 == 0)
