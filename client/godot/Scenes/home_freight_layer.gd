extends Node3D
## #265: o transporte do móvel comprado, desenhado só do que o motor diz (`GET /players/{id}/home`, bloco `haul`, e `home` da ficha do corpo): o móvel nas mãos de quem o leva,
## o carro da casa (parado em frente à porta, na loja, ou andando com o corpo que o dirige) e o entregador a caminho pela rota que o motor calculou. Nada é decidido aqui: a posição
## do entregador é só a interpolação dos tempos do motor (`startsInMs`, `arrivesInMs`, `totalMs`) sobre a rota (`path`), e o carro estacionado fica onde o motor o pôs.

signal notice(text: String) ## uma entrega chegou ao depósito da casa

const Grid := preload("res://Scenes/grid.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")

const POLL_S := 1.5
const IDLE_POLL_S := 20.0 ## mundo sem modo comprar (404): relê raramente
const CARRY_HEIGHT := 0.95
const CARRY_FRONT := 0.42

var api
var id_of: Callable ## () -> String: o jogador
var active: Callable ## () -> bool: há corpo conduzido?
var actor_of: Callable ## () -> Node3D: a figura do corpo conduzido (o móvel nas mãos vai nela)
var catalog_of: Callable ## () -> Array: descritores do catálogo (o modelo do móvel)
var rate_of: Callable ## () -> float: ms de mundo por ms real (o ritmo do relógio), para andar o entregador entre duas leituras
var view: Dictionary = {} ## o retrato do motor (`home`), a última leitura
var enabled := false ## o motor respondeu com o bloco de transporte
var car: Node3D
var couriers: Dictionary = {} ## seq -> {node, path, starts, arrives, total, read_at}
var carry_node: Node3D
var carry_item := ""

var _seen := 0 ## o último móvel entregue já avisado (o motor numera); a primeira leitura só aprende
var _primed := false
var _clock := 0.0
var _factory
var _descriptors: Dictionary = {}
var _busy := false
var _driver_actor: Callable ## () -> Node3D: o corpo que dirige (o carro segue a figura dele)


func _process(delta: float) -> void:
	if api == null or not id_of.is_valid() or String(id_of.call()) == "" or not (active.is_valid() and active.call()):
		return
	_clock += delta
	if _clock >= (POLL_S if enabled or view.is_empty() else IDLE_POLL_S) and not _busy:
		_clock = 0.0
		_poll()
	_step_couriers()
	_step_car()


func _poll() -> void:
	_busy = true
	var response: Dictionary = await api.player_home(String(id_of.call()))
	_busy = false
	if not response.ok or not response.body is Dictionary:
		enabled = false
		view = {"none": true}
		_clear()
		return
	apply(response.body)


## O retrato do motor chegou: desenha o carro e os entregadores do que ele diz.
func apply(home: Dictionary) -> void:
	view = home
	var haul: Variant = home.get("haul")
	enabled = haul is Dictionary
	if not enabled:
		_clear()
		return
	_ensure_factory()
	_apply_car(haul.get("car"))
	_apply_couriers(haul.get("couriers", []))


func _clear() -> void:
	if car != null and is_instance_valid(car):
		car.queue_free()
	car = null
	for seq in couriers:
		if is_instance_valid(couriers[seq].node):
			couriers[seq].node.queue_free()
	couriers = {}
	set_carry("")


func _ensure_factory() -> void:
	var list: Array = catalog_of.call() if catalog_of.is_valid() else []
	if list.is_empty() or (_factory != null and _descriptors.size() == list.size()):
		return
	_descriptors = {}
	for descriptor in list:
		_descriptors[String(descriptor.id)] = descriptor
	_factory = ObjectFactory.new(list)


# --- o carro ----------------------------------------------------------------------------------------------------------------------------------------

func _apply_car(data: Variant) -> void:
	if not data is Dictionary:
		if car != null and is_instance_valid(car):
			car.queue_free()
		car = null
		return
	if car == null or not is_instance_valid(car):
		car = _build_car(Color(String(data.get("color", "5b7fa6"))))
		add_child(car)
		car.set_meta("target", Vector2(float(data.x), float(data.y)))
		car.position = Vector3(float(data.x) * Grid.CELL, 0.0, float(data.y) * Grid.CELL)
	car.set_meta("target", Vector2(float(data.x), float(data.y)))
	car.set_meta("driving", bool(data.get("driving", false)))
	car.set_meta("driver", int(data.get("driver", 0)))


## Um carro de caixas (corpo, cabine, quatro rodas): cabe nos 1,5 m da célula e tem cor do dado.
static func _build_car(color: Color) -> Node3D:
	var root := Node3D.new()
	root.name = "HomeCar"
	var body := _box(Vector3(0.95, 0.34, 0.46), color, Vector3(0, 0.32, 0))
	root.add_child(body)
	root.add_child(_box(Vector3(0.5, 0.26, 0.4), color.lightened(0.18), Vector3(-0.04, 0.6, 0)))
	for x in [-0.3, 0.3]:
		for z in [-0.24, 0.24]:
			root.add_child(_box(Vector3(0.17, 0.17, 0.08), Color(0.08, 0.08, 0.09), Vector3(x, 0.1, z)))
	return root


static func _box(size: Vector3, color: Color, at: Vector3) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	node.mesh = mesh
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.8
	node.material_override = material
	node.position = at
	return node


func _step_car() -> void:
	if car == null or not is_instance_valid(car):
		return
	var target: Vector2 = car.get_meta("target", Vector2.ZERO)
	var driver_node: Node3D = _driver_actor.call() if _driver_actor.is_valid() else null
	var at := Vector3(target.x * Grid.CELL, 0.0, target.y * Grid.CELL)
	if bool(car.get_meta("driving", false)) and driver_node != null and is_instance_valid(driver_node):
		at = Vector3(driver_node.global_position.x, 0.0, driver_node.global_position.z) # dirigindo, o carro anda com a figura do corpo
	var move := at - car.position
	car.position = at if move.length() > 6.0 else car.position.lerp(at, clampf(get_process_delta_time() * 8.0, 0.0, 1.0))
	if Vector2(move.x, move.z).length() > 0.02:
		car.rotation.y = lerp_angle(car.rotation.y, atan2(-move.z, move.x), clampf(get_process_delta_time() * 6.0, 0.0, 1.0))


func set_driver_of(callable: Callable) -> void:
	_driver_actor = callable


# --- o entregador -------------------------------------------------------------------------------------------------------------------------------------

func _apply_couriers(list: Array) -> void:
	var now := Time.get_ticks_msec()
	var seen := {}
	for entry in list:
		var seq := int(entry.seq)
		seen[seq] = true
		if not couriers.has(seq):
			var node := _build_courier(String(entry.get("item", "")))
			add_child(node)
			couriers[seq] = {"node": node}
		var c: Dictionary = couriers[seq]
		c.path = entry.get("path", [])
		c.starts = int(entry.get("startsInMs", 0))
		c.arrives = int(entry.get("arrivesInMs", 0))
		c.total = maxi(1, int(entry.get("totalMs", 1)))
		c.read_at = now
	for seq in couriers.keys():
		if not seen.has(seq):
			if is_instance_valid(couriers[seq].node):
				couriers[seq].node.queue_free()
			couriers.erase(seq)


func _build_courier(item: String) -> Node3D:
	var root := Node3D.new()
	root.name = "Courier"
	var shirt := _box(Vector3(0.3, 0.5, 0.2), Color("c98b3a"), Vector3(0, 0.95, 0))
	root.add_child(shirt)
	root.add_child(_box(Vector3(0.2, 0.2, 0.2), Color("e0b794"), Vector3(0, 1.38, 0)))
	root.add_child(_box(Vector3(0.12, 0.62, 0.14), Color("3a4a6a"), Vector3(-0.08, 0.31, 0)))
	root.add_child(_box(Vector3(0.12, 0.62, 0.14), Color("3a4a6a"), Vector3(0.08, 0.31, 0)))
	var load_node: Node3D = _model_of(item)
	load_node.position = Vector3(0, 1.0, 0.34)
	load_node.name = "Load"
	root.add_child(load_node)
	return root


## O ponto da rota do entregador agora (células absolutas, fração) e o rumo: só interpolação dos tempos que o motor deu.
static func courier_at(path: Array, starts_ms: float, arrives_ms: float, total_ms: float) -> Vector2:
	if path.is_empty():
		return Vector2.INF
	if starts_ms > 0.0:
		return Vector2(float(path[0][0]), float(path[0][1])) # ainda não saiu da loja
	var fraction := clampf(1.0 - maxf(arrives_ms, 0.0) / maxf(total_ms, 1.0), 0.0, 1.0)
	var steps := path.size() - 1
	if steps <= 0:
		return Vector2(float(path[0][0]), float(path[0][1]))
	var at := fraction * float(steps)
	var i := mini(int(floor(at)), steps - 1)
	var a := Vector2(float(path[i][0]), float(path[i][1]))
	var b := Vector2(float(path[i + 1][0]), float(path[i + 1][1]))
	return a.lerp(b, at - float(i))


func _step_couriers() -> void:
	var rate := float(rate_of.call()) if rate_of.is_valid() else 1.0
	var now := Time.get_ticks_msec()
	for seq in couriers:
		var c: Dictionary = couriers[seq]
		if not is_instance_valid(c.node) or c.path.is_empty():
			continue
		var elapsed := float(now - int(c.read_at)) * rate
		var point := courier_at(c.path, float(c.starts) - elapsed, float(c.arrives) - elapsed, float(c.total))
		var before: Vector3 = c.node.position
		var target := Vector3(point.x * Grid.CELL, 0.0, point.y * Grid.CELL)
		c.node.position = target
		var move := target - before
		if Vector2(move.x, move.z).length() > 0.001:
			c.node.rotation.y = atan2(-move.z, move.x) + PI / 2.0


# --- a ficha do corpo ----------------------------------------------------------------------------------------------------------------------------------

## A ficha do corpo (`home` do motor): o móvel nas mãos e as entregas que chegaram. O aviso sai uma vez por número; a primeira leitura só aprende o passado.
func note_sheet(body: Dictionary) -> void:
	var data: Variant = body.get("home")
	var carry: Variant = data.get("carry") if data is Dictionary else null
	set_carry(String(carry.get("id", "")) if carry is Dictionary else "")
	var highest := _seen
	if data is Dictionary:
		for entry in data.get("arrived", []):
			var seq := int(entry.get("seq", 0))
			if seq + 1 > _seen and _primed:
				notice.emit(notice_text(entry))
			highest = maxi(highest, seq + 1)
	_seen = highest
	_primed = true


static func notice_text(entry: Dictionary) -> String:
	return "Seu pedido chegou: %s (%s o deixou no depósito da casa; tecla B)" % [String(entry.get("item", "")), String(entry.get("by", "o entregador"))]


# --- o móvel nas mãos ---------------------------------------------------------------------------------------------------------------------------------

## Põe (ou tira, com "") o móvel nas mãos da figura do corpo conduzido; o motor diz qual (`home.carry.item` na ficha) e a figura só o segura.
func set_carry(object_id: String) -> void:
	if object_id == carry_item and (object_id == "" or (carry_node != null and is_instance_valid(carry_node))):
		return
	if carry_node != null and is_instance_valid(carry_node):
		carry_node.queue_free()
	carry_node = null
	carry_item = object_id
	if object_id == "":
		return
	var actor: Node3D = actor_of.call() if actor_of.is_valid() else null
	if actor == null or not is_instance_valid(actor):
		carry_item = ""
		return
	carry_node = _model_of(object_id)
	carry_node.name = "CarriedFurniture"
	carry_node.position = Vector3(0, CARRY_HEIGHT, CARRY_FRONT)
	actor.add_child(carry_node)


func _model_of(object_id: String) -> Node3D:
	_ensure_factory()
	var model: Node3D = _factory.build(object_id, 0) if _factory != null and _descriptors.has(object_id) else null
	if model != null:
		var holder := Node3D.new()
		holder.add_child(model)
		holder.scale = Vector3.ONE * 0.8
		return holder
	var crate := Node3D.new()
	crate.add_child(_box(Vector3(0.45, 0.35, 0.4), Color("8a6a44"), Vector3.ZERO))
	return crate
