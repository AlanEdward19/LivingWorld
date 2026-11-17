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
	return "" if target.is_empty() else "◎ seguindo %s" % app.registry.title_of(target)


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


func _refresh_detail(id: int) -> void:
	var response: Dictionary = await app.api.npc_detail(id)
	if response.ok and response.body is Dictionary and response.body.get("currentLocation") is Dictionary:
		var location: Dictionary = response.body.currentLocation
		_last_cells[id] = Grid.to_world(Vector2i(int(location.x), int(location.y)))
