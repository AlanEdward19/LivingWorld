extends Node3D
## NPCs do escopo: um NpcActor por pessoa, movido e posado pelo que a store recebe. Anima só o que se vê (LOD):
## dentro do raio da câmera monta a pose completa (alternando metades em multidões); fora só interpola a posição.
## Corpo pelo estado (ADR-0047 etapa 3): o `observable` do escopo (idade, altura, ofício, gravidez, doença, luto, carga)
## vai para TODOS os NPCs, não só para quem teve a ficha lida. Ação com alvo: quem conversa vira para o outro e uma linha
## discreta liga os dois (um MultiMesh, refeito 4x por segundo); quem trabalha vira para a oficina.
## Rota (AD-181): quem viaja anda célula a célula pela `npc.route`, chegando no `routeArrivalTick` pelo relógio do mundo
## (`clock`); enquanto anda, sai do espalhamento da célula de partida e entra no da chegada quando o motor o move.

const NpcActor := preload("res://Scenes/npc_actor.gd")
const NpcFigure := preload("res://Scenes/npc_figure.gd")
const Grid := preload("res://Scenes/grid.gd")
const RouteClock := preload("res://Scenes/route_clock.gd")
const FrameCost := preload("res://Contract/frame_cost.gd")

const OVER_FLOOR := 1.0 ## altura de um andar (a mesma do `building_renderer.gd`): quem passa por cima anda no telhado
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
var clock = RouteClock.new() ## hora do mundo contínua (o app sincroniza pelo relógio; a store, pelo tick do delta)
var nodes: Dictionary = {} ## id -> NpcActor (o WorldView expõe o mesmo dicionário como npc_nodes)
var selected := -1
var detail_zoom := false ## LOD "perto": o app liga quando a câmera está bem próxima
var _frame := 0
var _hovered := -1
var _links: MultiMeshInstance3D
var _link_clock := 0.0
var link_pairs: Array = [] ## [[a, b]] desenhados agora (testável)
var cell_members: Dictionary = {} ## célula -> [ids] (quem divide a célula se espalha dentro dela)
const PERSONAL_M := 0.9 ## AD-197r: distância mínima entre dois corpos parados desenhados (a largura do boneco com os braços); AD-197s4: em metros do motor
const AVOID_RADIUS_M := 1.05 ## quem anda contorna o parado que passa a menos disto (metros)
const AVOID_MAX_M := 1.2
static func personal() -> float: return Grid.units(PERSONAL_M)
static func avoid_radius() -> float: return Grid.units(AVOID_RADIUS_M)
static func avoid_max() -> float: return Grid.units(AVOID_MAX_M)
const STILL_EVERY := 16 ## quadros entre refazer o índice de quem está parado
const STEER_EVERY := 8 ## cada pessoa andando reavalia o desvio a cada tantos quadros (escalonado por id)
var _ids: Array = [] ## ids na hora de refazer o índice (a fatia de cada quadro)
var _still: Dictionary = {} ## (célula de 0,5) -> [posições de quem está parado e desenhado]


## Lugar de cada um numa célula dividida, SÓ no desenho (a célula é a do motor): sozinho no centro; em grupo, até 6 num anel que deixa
## `PERSONAL` entre vizinhos; acima disso, o centro e anéis hexagonais (6, 12, 18...) também a `PERSONAL` ou mais. A vaga preferida é
## `id % vagas`; conflito vai para a próxima livre, na ordem dos ids: estável enquanto o grupo é o mesmo. Nada aleatório.
static func spread_offsets(ids: Array) -> Dictionary:
	var out := {}
	if ids.size() <= 1:
		for id in ids:
			out[id] = Vector3.ZERO
		return out
	var sorted := ids.duplicate()
	sorted.sort()
	var count := sorted.size()
	var taken := {}
	for id in sorted:
		var slot := posmod(int(id), count)
		while taken.has(slot):
			slot = (slot + 1) % count
		taken[slot] = true
		out[id] = slot_point(count, slot)
	return out


## Posição da vaga `slot` de um grupo de `count`: anel único até 6; depois centro + anéis de 6, 12, 18... (hexagonal).
static func slot_point(count: int, slot: int) -> Vector3:
	if count <= 6:
		var radius := personal() * 0.5 / sin(PI / count)
		var angle := TAU * float(slot) / count + PI / 6.0 * (count % 2)
		return Vector3(cos(angle), 0, sin(angle)) * radius
	if slot == 0:
		return Vector3.ZERO
	var ring := 1
	var first := 1
	while slot >= first + 6 * ring:
		first += 6 * ring
		ring += 1
	var in_ring := 6 * ring
	var theta := TAU * float(slot - first) / in_ring
	return Vector3(cos(theta), 0, sin(theta)) * (personal() * ring)


static func _key(x: float, z: float) -> Vector2i:
	var radius := avoid_radius()
	return Vector2i(floori(x / radius), floori(z / radius))


## AD-197r: quem anda contorna quem está parado (só no desenho). Índice de parados refeito a cada STILL_EVERY quadros; cada pessoa
## andando reavalia a cada STEER_EVERY quadros (uma fatia por quadro): o desvio é lateral, do lado oposto ao do obstáculo, e some nas pontas
## do trecho (chega no lugar exato). O estado do motor não é tocado.
func _separate() -> void:
	if _frame % STILL_EVERY == 0:
		_still.clear()
		_ids = nodes.keys()
		for id in _ids:
			var actor = nodes[id]
			if actor.driven or not (actor._t < 1.0 or (actor.walk != null and not actor.walk.done)):
				_still.get_or_add(_key(actor.position.x, actor.position.z), []).append(Vector4(actor.position.x, 0.0, actor.position.z, float(id)))
	if _still.is_empty():
		return
	var i := _frame % STEER_EVERY # uma fatia dos moradores por quadro (sem varrer todos todo quadro)
	var count := _ids.size()
	while i < count:
		var id: int = _ids[i]
		var actor = nodes.get(id)
		i += STEER_EVERY
		if actor == null:
			continue
		if actor.driven or not (actor._t < 1.0 or (actor.walk != null and not actor.walk.done)):
			if actor.avoid_goal != Vector3.ZERO:
				actor.avoid_goal = Vector3.ZERO
			continue
		actor.avoid_goal = steer_away(actor.base_position(), actor.heading_now(), _still, id)


## Desvio lateral de quem anda: `base` é o ponto do trecho SEM o desvio, `heading` o rumo. Cada parado perto do caminho (um alcance à frente,
## `AVOID_RADIUS` atrás e dos lados) pede para passar com `AVOID_RADIUS` de folga, do lado oposto ao dele (no meio da linha, pela direita).
## Alimentação à frente, sem realimentar a posição desenhada: o mesmo caminho dá o mesmo desvio. ZERO se ninguém está perto.
static func steer_away(base: Vector3, heading: Vector3, still: Dictionary, self_id: int = -1) -> Vector3:
	if heading.length_squared() < 0.0001:
		return Vector3.ZERO
	var ahead := Vector3(heading.x, 0, heading.z).normalized()
	var right := Vector3(-ahead.z, 0, ahead.x)
	var key := _key(base.x, base.z)
	var radius := avoid_radius() # uma conversão por chamada, não uma por vizinho (caminho quente)
	var best := 0.0
	for dx in [-1, 0, 1]:
		for dz in [-1, 0, 1]:
			for other in still.get(key + Vector2i(dx, dz), []):
				if int(other.w) == self_id: # a própria posição de antes de sair não é obstáculo
					continue
				var rel := Vector3(other.x - base.x, 0, other.z - base.z)
				var along := rel.dot(ahead)
				var lat := rel.dot(right)
				if along < -radius or along > radius or absf(lat) >= radius:
					continue
				var need := (1.0 if lat <= 0.0 else -1.0) * (radius - absf(lat))
				if absf(need) > absf(best):
					best = need
	return right * clampf(best, -avoid_max(), avoid_max())


func _join(id: int, cell: Vector2i) -> void:
	var members: Array = cell_members.get_or_add(cell, [])
	if not members.has(id):
		members.append(id)
	_respread(cell)


func _leave(id: int, cell: Vector2i) -> void:
	var members: Array = cell_members.get(cell, [])
	members.erase(id)
	if members.is_empty():
		cell_members.erase(cell)
	else:
		_respread(cell)


func _respread(cell: Vector2i) -> void:
	var offsets := spread_offsets(cell_members.get(cell, []))
	for id in offsets:
		if nodes.has(id):
			nodes[id].set_spread(offsets[id])


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
	cell_members.clear()
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
	if not _apply_route(actor, info):
		_join(int(info.id), info.cell)
	else: # entrou no escopo no meio da viagem: já no ponto da rota pelo tempo decorrido, sem passar pela célula de partida
		actor.walk.step(clock.tick)
		actor.position = actor.walk.position
	apply_known_model(int(info.id))
	apply_observable(int(info.id))


## Profissão e idade de quem já teve a ficha lida (o corpo deixa de ser o modelo base também depois de trocar de escopo).
## Com o `observable` do escopo (AD-170) o corpo vem do estado, para todos.
func apply_known_model(id: int) -> void:
	var actor = nodes.get(id)
	var npc = store.npcs.get(id)
	if actor == null or catalog == null or (npc != null and not npc.get("obs", {}).is_empty()):
		return
	NpcFigure.dress(actor, id, figure_context())


## O dado que a fábrica de figura (`npc_figure.gd`) usa para vestir o boneco; o interior recebe o mesmo.
func figure_context() -> Dictionary:
	return {"store": store, "catalog": catalog, "model_of": model_of}


## Sinais visíveis chegaram (snapshot ou `npc_observed`): corpo, roupa, carga, doença, emoção e alvo da ação.
func apply_observable(id: int) -> void:
	var actor = nodes.get(id)
	var npc = store.npcs.get(id)
	if actor == null or npc == null:
		return
	var obs: Dictionary = npc.get("obs", {})
	if not obs.is_empty():
		NpcFigure.dress(actor, id, figure_context())
		if store.names.has(id):
			actor.set_display_name(store.names[id])
		var held: Dictionary = obs.get("held", {})
		if not held.is_empty():
			_apply_held(actor, int(held.design_id))
	_aim(actor, npc)


## Para onde o boneco olha e, no lote de uma obra (alvo `work`, AD-193), o martelo na mão em vez da ferramenta do ofício.
func _aim(actor, npc: Dictionary) -> void:
	actor.face_point = target_point(npc)
	if actor.has_method("set_site_work"):
		actor.set_site_work(String(npc.get("target", {}).get("kind", "")) == "work")


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


## Alvo do NPC para o tracejado da seleção (AD-188): a intenção com alvo (`intentTarget` "tipo:id") vence; senão o alvo da
## ação. INF sem alvo do motor ou alvo que não está na cena.
func goal_point(id: int) -> Vector3:
	var npc = store.npcs.get(id)
	if npc == null:
		return Vector3.INF
	var intent := String(npc.get("intent_target", ""))
	if ":" in intent:
		var point := target_point({"target": {"kind": intent.get_slice(":", 0), "id": intent.get_slice(":", 1)}})
		if point != Vector3.INF:
			return point
	return target_point(npc)


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
		"building":
			return building_point.call(int(target.id))
		"household":
			for state in store.building_states.values():
				if int(target.id) in state.households:
					return building_point.call(int(state.building_id))
		"leaving": # AD-189: o espólio que foi buscar
			var leaving = store.leavings.get(int(target.id))
			return Grid.to_world(leaving.cell) if leaving != null else Vector3.INF
		"city": # AD-189: o largo (a cidade da mudança ou a praça da visita)
			var city: Dictionary = store.city if String(store.city.get("id", "")) == String(target.id) else store.cities.get(String(target.id), {})
			return Grid.to_world(city.location) if city.get("location") is Vector2i else Vector3.INF
		"work": # AD-193: a obra em que trabalha agora (o canteiro dela, senão a célula do lote)
			var work = store.works.get(int(target.id))
			if work == null:
				return Vector3.INF
			var at: Vector3 = building_point.call(int(work.building_id)) if int(work.building_id) >= 0 else Vector3.INF
			return at if at != Vector3.INF else Grid.to_world(work.cell)
		"water", "site": # AD-189: a fonte e a obra, por célula "x,y"
			var parts := String(target.id).split(",")
			return Grid.to_world(Vector2i(int(parts[0]), int(parts[1]))) if parts.size() == 2 else Vector3.INF
	return Vector3.INF


func on_changed(id: int, from_cell: Vector2i, to_cell: Vector2i, action: int) -> void:
	var actor = nodes.get(id)
	if actor == null:
		spawn(store.npcs[id])
		return
	if action != actor.action:
		actor.set_action(action, labels)
		_aim(actor, store.npcs[id])
	var walking: bool = actor.walk != null
	if _apply_route(actor, store.npcs[id]):
		if not walking:
			_leave(id, actor.cell) # na rua: fora do espalhamento da célula de partida
		actor.cell = to_cell
		return
	if walking: # chegou (ou a viagem acabou): entra na célula que o motor diz, saindo de onde está desenhado
		_join(id, to_cell)
		actor.move_to(to_cell, STEP_SECONDS * 0.5)
		return
	if to_cell != actor.cell:
		var old_cell: Vector2i = actor.cell
		_leave(id, old_cell)
		_join(id, to_cell) # define o lugar na célula nova antes do passo
		actor.move_to(to_cell, STEP_SECONDS)


## Rota do motor no boneco: começa (ou continua) a caminhada; sem rota, para. Devolve se está andando pela rota.
## Início = `routeDepartedTick` (AD-185); servidor antigo: `actionStartedAtTick`; sem os dois (ou inválidos), a hora em que a
## rota chegou. Células de `routeOver` vão na altura do prédio por cima do qual se passa.
func _apply_route(actor, info: Dictionary) -> bool:
	var cells: Array = info.get("route", [])
	var arrival := int(info.get("arrival", -1))
	if cells.size() < 2 or arrival < 0:
		actor.clear_route()
		return false
	var start := float(info.get("departed", -1))
	if start < 0.0 or start >= float(arrival):
		start = float(info.get("started", -1))
	if start < 0.0 or start >= float(arrival):
		start = actor.walk.start_tick if actor.walk != null and actor.walk.end_tick == float(arrival) else minf(clock.tick, float(arrival) - 1.0)
	if actor.walk != null and actor.walk.same(cells, start, float(arrival)):
		return true # AD-189: a mesma rota em curso: nem procura as alturas de novo (varria todos os prédios)
	return actor.set_route(cells, start, float(arrival), over_heights(cells, info.get("route_over", [])))


## Altura de cada índice "por cima" da rota: o telhado do prédio que cobre a célula (andares × altura do andar); sem prédio
## conhecido ali, um andar. {} quando a rota é toda no chão (o caso comum: nada a procurar).
func over_heights(cells: Array, over: Array) -> Dictionary:
	var out := {}
	if over.is_empty():
		return out
	var wanted := {}
	for index in over:
		wanted[cells[int(index)]] = int(index)
	for building in store.buildings.values():
		for entry in building.footprint:
			var cell: Vector2i = building.cell + entry.cell
			if wanted.has(cell):
				out[wanted[cell]] = float(maxi(int(building.get("floors", 1)), 1)) * OVER_FLOOR
	for index in over:
		if not out.has(int(index)):
			out[int(index)] = OVER_FLOOR
	return out


func on_removed(id: int) -> void:
	var actor = nodes.get(id)
	if actor != null:
		if actor.walk == null:
			_leave(id, actor.cell)
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
		transforms.append(Transform3D(basis * Basis.from_scale(Vector3(0.025, 0.025, length)), (from + to) / 2.0))
		link_pairs.append([int(id), other])
	var multi: MultiMesh = _links.multimesh
	multi.instance_count = transforms.size()
	for i in transforms.size():
		multi.set_instance_transform(i, transforms[i])
	_links.visible = not transforms.is_empty()


func advance(delta: float, view_center: Vector3, view_radius: float, far_zoom: bool, bubble_zoom: bool) -> void:
	_frame += 1
	if not bubble_zoom and not (far_zoom and nodes.size() > 150):
		var steered := Time.get_ticks_usec()
		_separate()
		FrameCost.add("npc_avoid", steered)
	if store != null:
		clock.observe(int(store.last_tick))
	clock.advance(delta)
	NpcActor.world_tick = clock.tick
	var crowded := nodes.size() > 150
	var far_now := far_zoom and crowded
	var detail_radius := 6.0
	var radius_sq := view_radius * view_radius
	var half_sq := radius_sq * 0.25
	var detail_sq := detail_radius * detail_radius
	for id in nodes:
		var actor = nodes[id]
		# AD-189: os setters só quando o estado muda (500 chamadas a menos de cada por quadro)
		if actor.far_lod != far_now:
			actor.set_far(far_now)
		if actor.bubble != bubble_zoom:
			actor.set_bubble(bubble_zoom)
		var distance_sq: float = actor.position.distance_squared_to(view_center)
		var near: bool = not bubble_zoom and distance_sq <= radius_sq
		var detail: bool = detail_zoom and distance_sq <= detail_sq
		if actor.near_detail != detail:
			actor.set_detail(detail)
		# multidão: pose a cada 2 quadros perto do centro da vista e a cada 4 na borda (o tempo pulado se acumula: o passo não
		# muda, só a frequência com que a pose é refeita); a posição continua todo quadro (AD-188, medida com o mundo rodando).
		# AD-189: parado (sem passo nem rota em curso; rota que já chegou não conta), a pose é a respiração lenta: a cada 8 quadros
		var stride := 1
		if crowded and not actor.driven: # #222: o corpo do jogador se anima todo quadro (na multidão a pose era a cada 3)
			var moving: bool = actor._t < 1.0 or (actor.walk != null and not actor.walk.done)
			# com o mundo rodando a 40 ticks/s quase todo mundo está no passo (cada tick muda a célula): o passo perto do centro
			# a cada 3 quadros, na borda a cada 6 (o selecionado a cada 2)
			stride = 2 if id == selected else (3 if moving and distance_sq <= half_sq else (6 if moving else 8))
		var due: bool = (id + _frame) % stride == 0
		if due and near:
			var posed := Time.get_ticks_usec()
			actor.tick(delta, near, due)
			FrameCost.add("npc_pose", posed) # AD-189: diagnóstico do bench (pose refeita)
		else:
			actor.tick(delta, near, due)
	_link_clock += delta
	if _link_clock >= LINK_SECONDS:
		_link_clock = 0.0
		if bubble_zoom:
			_links.visible = false
		else:
			var linked := Time.get_ticks_usec()
			refresh_links()
			FrameCost.add("npc_links", linked)
