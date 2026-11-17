extends Node3D
## NPCs do escopo: um NpcActor por pessoa, movido e posado pelo que a store recebe. Anima só o que se vê (LOD):
## dentro do raio da câmera monta a pose completa (alternando metades em multidões); fora só interpola a posição.
## Corpo pelo estado (ADR-0047 etapa 3): o `observable` do escopo (idade, altura, ofício, gravidez, doença, luto, carga)
## vai para TODOS os NPCs, não só para quem teve a ficha lida. Ação com alvo: quem conversa vira para o outro e uma linha
## discreta liga os dois (um MultiMesh, refeito 4x por segundo); quem trabalha vira para a oficina.

const NpcActor := preload("res://Scenes/npc_actor.gd")
const Grid := preload("res://Scenes/grid.gd")

const STEP_SECONDS := 1.0 ## duração visual de um passo; o servidor decide a posição, o cliente só interpola
const LINK_SECONDS := 0.25
const SOCIALIZE := 3

var store
var labels
var catalog ## AssetCatalog (modelo por profissão e idade); nulo = corpo base
var model_of: Callable = func(_id: int) -> Dictionary: return {} ## {profession, age} conhecidos, vazio se não
var design_of: Callable = func(_id: int) -> Variant: return null ## corpo de /designs/{id} já lido (cor do recipiente)
var request_design: Callable = func(_id: int) -> void: pass ## pede o design uma vez (o reader guarda para sempre)
var building_point: Callable = func(_id: int) -> Vector3: return Vector3.INF ## centro do prédio (alvo "oficina")
var nodes: Dictionary = {} ## id -> NpcActor (o WorldView expõe o mesmo dicionário como npc_nodes)
var selected := -1
var detail_zoom := false ## LOD "perto": o app liga quando a câmera está bem próxima
var _frame := 0
var _hovered := -1
var _links: MultiMeshInstance3D
var _link_clock := 0.0
var link_pairs: Array = [] ## [[a, b]] desenhados agora (testável)


func _init() -> void:
	var multi := MultiMesh.new()
	multi.transform_format = MultiMesh.TRANSFORM_3D
	var mesh := BoxMesh.new()
	mesh.size = Vector3(1, 1, 1)
	multi.mesh = mesh
	_links = MultiMeshInstance3D.new()
	_links.name = "TalkLines"
	_links.multimesh = multi
	_links.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	var material := StandardMaterial3D.new()
	material.albedo_color = Color(1.0, 0.95, 0.7, 0.55)
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_links.material_override = material
	add_child(_links)


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
	apply_observable(int(info.id))


## Profissão e idade de quem já teve a ficha lida (o corpo deixa de ser o modelo base também depois de trocar de escopo).
## Com o `observable` do escopo (AD-170) o corpo vem do estado, para todos.
func apply_known_model(id: int) -> void:
	var actor = nodes.get(id)
	if actor == null or catalog == null:
		return
	var npc = store.npcs.get(id)
	if npc != null and not npc.get("obs", {}).is_empty():
		return
	var model: Dictionary = model_of.call(id)
	if not model.is_empty():
		actor.apply_model(catalog, int(model.profession), float(model.age))


## Sinais visíveis chegaram (snapshot ou `npc_observed`): corpo, roupa, carga, doença, emoção e alvo da ação.
func apply_observable(id: int) -> void:
	var actor = nodes.get(id)
	var npc = store.npcs.get(id)
	if actor == null or npc == null:
		return
	var obs: Dictionary = npc.get("obs", {})
	if not obs.is_empty():
		actor.apply_observable(obs, catalog)
		if store.names.has(id):
			actor.set_display_name(store.names[id])
		var held: Dictionary = obs.get("held", {})
		if not held.is_empty():
			_apply_held(actor, int(held.design_id))
	actor.face_point = target_point(npc)


func _apply_held(actor, design_id: int) -> void:
	var body = design_of.call(design_id)
	if body is Dictionary:
		var parts: Array = body.get("parts", [])
		var materials: Array = body.get("materials", [])
		var shape := String(parts[0].get("shape", "cylinder")) if not parts.is_empty() else "cylinder"
		var color: Color = Color(String(materials[0].get("color", "#9a7a52"))) if not materials.is_empty() else actor.held_color
		actor.set_held_look(shape, color)
	elif design_id > 0:
		request_design.call(design_id)


## Ponto para onde o NPC parado olha: a pessoa com quem conversa ou a oficina onde trabalha; INF se a ação não tem alvo.
func target_point(npc: Dictionary) -> Vector3:
	var target: Dictionary = npc.get("target", {})
	if target.is_empty():
		return Vector3.INF
	match String(target.kind):
		"npc":
			var other = nodes.get(int(target.id))
			return other.position if other != null else Vector3.INF
		"workplace":
			for state in store.building_states.values():
				if int(state.workplace_id) == int(target.id):
					return building_point.call(int(state.building_id))
	return Vector3.INF


func on_changed(id: int, from_cell: Vector2i, to_cell: Vector2i, action: int) -> void:
	var actor = nodes.get(id)
	if actor == null:
		spawn(store.npcs[id])
		return
	if action != actor.action:
		actor.set_action(action, labels)
		actor.face_point = target_point(store.npcs[id])
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


## Linhas entre quem conversa com alvo conhecido (cônjuge, pelo motor). Só pares com os dois na cena.
func refresh_links() -> void:
	link_pairs.clear()
	var transforms: Array = []
	for id in store.npcs:
		var npc: Dictionary = store.npcs[id]
		if int(npc.action) != SOCIALIZE or npc.get("target", {}).get("kind", "") != "npc":
			continue
		var other := int(npc.target.id)
		if other < int(id) and store.npcs.has(other) and int(store.npcs[other].action) == SOCIALIZE:
			continue # o par já entra pelo de menor id
		var a = nodes.get(id)
		var b = nodes.get(other)
		if a == null or b == null or not a.visible or not b.visible:
			continue
		var from: Vector3 = a.position + Vector3(0, 1.05 * a.scale.y, 0)
		var to: Vector3 = b.position + Vector3(0, 1.05 * b.scale.y, 0)
		var length := from.distance_to(to)
		if length < 0.05:
			continue
		var basis := Basis.looking_at((to - from).normalized(), Vector3.UP if absf((to - from).normalized().y) < 0.99 else Vector3.RIGHT)
		transforms.append(Transform3D(basis.scaled(Vector3(0.025, 0.025, length)), (from + to) / 2.0))
		link_pairs.append([int(id), other])
	var multi: MultiMesh = _links.multimesh
	multi.instance_count = transforms.size()
	for i in transforms.size():
		multi.set_instance_transform(i, transforms[i])
	_links.visible = not transforms.is_empty()


func advance(delta: float, view_center: Vector3, view_radius: float, far_zoom: bool, bubble_zoom: bool) -> void:
	_frame += 1
	var crowded := nodes.size() > 150
	var far_now := far_zoom and crowded
	var detail_radius := 6.0
	for id in nodes:
		var actor = nodes[id]
		actor.set_far(far_now)
		actor.set_bubble(bubble_zoom)
		var distance: float = actor.position.distance_to(view_center)
		var near: bool = not bubble_zoom and distance <= view_radius
		actor.set_detail(detail_zoom and distance <= detail_radius)
		actor.tick(delta, near, not crowded or (id + _frame) % 2 == 0)
	_link_clock += delta
	if _link_clock >= LINK_SECONDS:
		_link_clock = 0.0
		if bubble_zoom:
			_links.visible = false
		else:
			refresh_links()
