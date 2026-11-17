extends RefCounted
## Seguir uma pessoa, uma família (casa) ou uma cidade sem perder o alvo ao trocar de escopo, entrar num prédio ou
## sair da cidade. Guarda a REFERÊNCIA {kind, id}, nunca o nó: a cada passo resolve de novo onde o alvo está agora
## (nó na cena, ocupante do interior, membros conhecidos da casa, cidade no mapa ou a última posição da ficha).

const Registry := preload("res://Store/entity_registry.gd")
const Grid := preload("res://Scenes/grid.gd")

const DETAIL_SECONDS := 2.0 ## sem nó na cena, a ficha (cache por hora do mundo) diz onde a pessoa está

var app
var target: Dictionary = {} ## {kind, id}; vazio = não segue
var last_point := Vector3.INF
var _clock := 0.0
var _last_cells: Dictionary = {} ## npc id -> Vector3 da última ficha lida


func _init(p_app) -> void:
	app = p_app


func active() -> bool:
	return not target.is_empty()


func follow(entity: Dictionary) -> void:
	target = entity
	_clock = DETAIL_SECONDS # já busca a posição na primeira passada, se precisar
	step(0.0)


func release() -> void:
	target = {}
	last_point = Vector3.INF
	app.rig.release()


func label() -> String:
	var prefix := "▶ assistindo · " if watching else ""
	if target.is_empty():
		return prefix.trim_suffix(" · ")
	return "%s◎ seguindo %s" % [prefix, app.registry.title_of(target)]


## Nó 3D da pessoa no que está desenhado agora (cidade/mundo ou interior aberto); null se não está na cena.
func person_node(id: int) -> Node3D:
	if app.stage.active and app.stage.interior_view.occupant_nodes.has(id):
		return app.stage.interior_view.occupant_nodes[id]
	return app.world_view.npc_nodes.get(id)


## Ponto a seguir agora; Vector3.INF se ainda não se sabe.
func resolve() -> Vector3:
	match String(target.get("kind", "")):
		Registry.PERSON:
			return _person_point(int(target.id))
		Registry.HOUSEHOLD:
			var sum := Vector3.ZERO
			var seen := 0
			for member in app.registry.known_household(int(target.id)):
				var point := _person_point(int(member))
				if point != Vector3.INF:
					sum += point
					seen += 1
			return sum / seen if seen > 0 else Vector3.INF
		Registry.CITY:
			var id := String(target.id)
			if app.store.kind == "City" and String(app.store.city.get("id", "")) == id:
				return app.city_center()
			var city = app.store.cities.get(id)
			return Grid.to_world(city.location) if city != null else Vector3.INF
	return Vector3.INF


func _person_point(id: int) -> Vector3:
	var node := person_node(id)
	if node != null and is_instance_valid(node):
		return node.global_position if node.is_inside_tree() else node.position
	if app.store.npcs.has(id):
		return Grid.to_world(app.store.npcs[id].cell)
	if _last_cells.has(id):
		return _last_cells[id]
	return Vector3.INF


## Uma passada (o App chama todo quadro): a câmera vai até o alvo; sem nó na cena, relê a ficha de tempos em tempos.
func step(delta: float) -> void:
	if target.is_empty():
		return
	_clock += delta
	if String(target.kind) == Registry.PERSON:
		var node := person_node(int(target.id))
		if node != null and is_instance_valid(node):
			if app.rig.following != node:
				app.rig.follow(node)
			last_point = node.position
			return
		app.rig.release()
		if _clock >= DETAIL_SECONDS:
			_clock = 0.0
			_refresh_detail(int(target.id))
	var point := resolve()
	if point != Vector3.INF and point.distance_to(last_point) > 0.05:
		last_point = point
		app.rig.focus(point)


# --- direção (ADR-0047 etapa 3): pular para o próximo evento e modo assistir ---------------------------------

const WATCH_DWELL := 8.0 ## quanto o modo assistir fica num acontecimento antes de procurar outro
const WATCH_MIN := 3.0 ## evento novo só interrompe depois disto
const INTERESTING := [3, 2, 8, 9, 0, 6, 7] ## conversar, trabalhar, furtar, chorar, comer, comprar, poder (ordem de interesse)

var watching := false
var jump_cursor := -1 ## id do último evento visitado por "próximo"
var current_event := -1
var _watch_clock := 0.0
var _watched: Dictionary = {} ## eventos já mostrados pelo modo assistir
var _fresh := false
var _person_cursor := 0


## Leva a câmera ao evento e abre a ficha dele; segue a primeira pessoa envolvida que está na cena (ou a do evento).
func goto_event(id: int) -> void:
	var event: Dictionary = app.registry.events.get(id, {})
	if event.is_empty():
		return
	current_event = id
	jump_cursor = id
	_watched[id] = true
	var people: Array = app.registry.event_people(event)
	var followed := false
	for person in people:
		if person_node(int(person)) != null:
			target = Registry.ref(Registry.PERSON, int(person))
			followed = true
			break
	if not followed:
		target = {}
		app.rig.release()
		if bool(event.get("has_cell", false)):
			app.rig.focus(Grid.to_world(event.cell) + Vector3(0.5, 0, 0.5))
	app.inspect(Registry.ref(Registry.EVENT, id))


## Próximo evento depois do último visitado (na ordem de chegada); sem mais novos, o mais recente.
func next_event() -> int:
	var order: Array = app.registry.event_order
	if order.is_empty():
		return -1
	var index := order.find(jump_cursor)
	var next_id: int = order[index + 1] if index >= 0 and index + 1 < order.size() else order[order.size() - 1]
	goto_event(next_id)
	return next_id


func set_watching(value: bool) -> void:
	watching = value
	_watch_clock = WATCH_DWELL # já escolhe na primeira passada
	if not value:
		release()


## Evento novo chegou: o modo assistir corre para ele assim que o atual teve o seu tempo.
func on_events(_ids: Array) -> void:
	_fresh = true


## Passo do modo assistir: evento ainda não mostrado (o mais novo primeiro); sem evento, alguém fazendo algo.
func watch_step(delta: float) -> void:
	if not watching:
		return
	_watch_clock += delta
	if _watch_clock < WATCH_MIN or (_watch_clock < WATCH_DWELL and not _fresh):
		return
	_watch_clock = 0.0
	_fresh = false
	var order: Array = app.registry.event_order
	for i in range(order.size() - 1, -1, -1):
		if not _watched.has(order[i]):
			goto_event(int(order[i]))
			return
	var person := busy_person()
	if person >= 0:
		current_event = -1
		follow(Registry.ref(Registry.PERSON, person))
		app.inspect(Registry.ref(Registry.PERSON, person))


## Alguém na cena fazendo algo que vale ver (conversa, trabalho, furto, choro...), girando entre os candidatos.
func busy_person() -> int:
	var ids: Array = app.world_view.npc_nodes.keys()
	ids.sort()
	if ids.is_empty():
		return -1
	for action in INTERESTING:
		var found: Array = ids.filter(func(id): return app.store.npcs.has(id) and int(app.store.npcs[id].action) == action)
		if not found.is_empty():
			_person_cursor += 1
			return int(found[_person_cursor % found.size()])
	_person_cursor += 1
	return int(ids[_person_cursor % ids.size()])


func _refresh_detail(id: int) -> void:
	var response: Dictionary = await app.api.npc_detail(id)
	if response.ok and response.body is Dictionary and response.body.get("currentLocation") is Dictionary:
		var location: Dictionary = response.body.currentLocation
		_last_cells[id] = Grid.to_world(Vector2i(int(location.x), int(location.y)))
