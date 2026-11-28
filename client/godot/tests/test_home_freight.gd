extends "res://tests/test_base.gd"
## #265: o transporte do móvel desenhado só do que o motor diz: o entregador anda pelos tempos e pela rota do motor, o carro fica onde o motor o pôs, o móvel vai nas mãos
## de quem o motor diz levá-lo e o aviso "Seu pedido chegou" sai uma vez por entrega (a primeira leitura só aprende o passado).

const Layer := preload("res://Scenes/home_freight_layer.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")

const PATH := [[10.5, 4.5], [11.5, 4.5], [12.5, 4.5], [13.5, 4.5], [14.5, 4.5]]
const HAUL := {"couriers": [{"seq": 3, "id": "sofa", "item": "Sofá", "name": "Seu Tomás", "npc": 5, "startsInMs": 0, "arrivesInMs": 60000, "totalMs": 120000, "path": PATH}],
	"car": {"id": "car", "name": "Carro", "color": "5b7fa6", "x": 7.5, "y": 6.5, "driving": false, "driver": 0, "trunkKg": 250.0, "trunkLiters": 1400.0, "loadKg": 0.0, "loadLiters": 0.0}, "parcels": []}


func _layer() -> Array:
	var layer := Layer.new()
	layer.api = ApiClient.new(FakeTransport.new())
	var holder := Node3D.new()
	tree.root.add_child(holder)
	holder.add_child(layer)
	return [layer, holder]


func test_the_courier_is_at_the_start_before_leaving_and_moves_along_the_engine_path_by_the_engine_times() -> void:
	eq(Layer.courier_at(PATH, 5000.0, 125000.0, 120000.0), Vector2(10.5, 4.5), "ainda não saiu da loja: fica na porta dela")
	eq(Layer.courier_at(PATH, 0.0, 120000.0, 120000.0), Vector2(10.5, 4.5), "saiu agora")
	eq(Layer.courier_at(PATH, 0.0, 60000.0, 120000.0), Vector2(12.5, 4.5), "na metade do tempo, na metade da rota")
	eq(Layer.courier_at(PATH, 0.0, 30000.0, 120000.0), Vector2(13.5, 4.5), "três quartos")
	eq(Layer.courier_at(PATH, 0.0, 0.0, 120000.0), Vector2(14.5, 4.5), "chegou: na porta de casa")
	eq(Layer.courier_at(PATH, 0.0, -9000.0, 120000.0), Vector2(14.5, 4.5), "passou do prazo: não sai da porta")
	eq(Layer.courier_at([], 0.0, 1.0, 1.0), Vector2.INF, "sem rota, nada")


func test_the_car_and_the_courier_are_drawn_from_the_engine_view_and_removed_when_the_engine_stops_saying_them() -> void:
	var made := _layer()
	var layer = made[0]
	layer.apply({"haul": HAUL})
	check(layer.enabled, "o motor trouxe o bloco de transporte")
	check(layer.car != null and layer.car.name == "HomeCar", "o carro da casa é desenhado")
	check(absf(layer.car.position.x - 7.5) < 0.01 and absf(layer.car.position.z - 6.5) < 0.01, "onde o motor o pôs: %s" % str(layer.car.position))
	check(layer.couriers.has(3), "o entregador do motor")
	layer._step_couriers()
	var at: Vector3 = layer.couriers[3].node.position
	check(at.x >= 10.5 and at.x <= 14.5 and absf(at.z - 4.5) < 0.01, "na rota: %s" % str(at))
	layer.apply({"haul": {"couriers": [], "car": null, "parcels": []}})
	check(layer.car == null and layer.couriers.is_empty(), "sem carro nem entregador no motor: nada desenhado")
	layer.apply({"haul": null})
	check(not layer.enabled, "mundo sem transporte: a camada se cala")
	made[1].queue_free()


func test_the_arrival_notice_comes_once_per_delivery_and_the_first_read_only_learns_the_past() -> void:
	var made := _layer()
	var layer = made[0]
	var notices: Array = []
	layer.notice.connect(func(text: String): notices.append(text))
	var one := {"home": {"arrived": [{"seq": 4, "id": "sofa", "item": "Sofá", "by": "Seu Tomás"}], "coming": 0}}
	layer.note_sheet(one)
	eq(notices.size(), 0, "a primeira leitura aprende o que já chegou, sem repetir o passado")
	layer.note_sheet(one)
	eq(notices.size(), 0, "a mesma entrega não avisa de novo")
	layer.note_sheet({"home": {"arrived": [{"seq": 4, "id": "sofa", "item": "Sofá", "by": "Seu Tomás"}, {"seq": 6, "id": "plant", "item": "Planta", "by": "Seu Tomás"}], "coming": 0}})
	eq(notices.size(), 1, "uma entrega nova, um aviso")
	check("Planta" in String(notices[0]) and "Seu Tomás" in String(notices[0]) and "depósito" in String(notices[0]), "o aviso diz o que chegou, quem trouxe e onde ficou: %s" % notices[0])
	layer.note_sheet({"money": 3})
	eq(notices.size(), 1, "ficha sem o bloco: nada")
	made[1].queue_free()


func test_the_carried_furniture_goes_to_the_body_the_engine_names_and_leaves_when_the_engine_stops_saying_it() -> void:
	var made := _layer()
	var layer = made[0]
	var actor := Node3D.new()
	made[1].add_child(actor)
	layer.actor_of = func() -> Node3D: return actor
	layer.note_sheet({"home": {"carry": {"seq": 1, "id": "plant", "item": "Planta", "kg": 3.0, "with": null}, "arrived": [], "coming": 0}})
	check(layer.carry_node != null and layer.carry_node.get_parent() == actor, "o móvel nas mãos vai na figura do corpo conduzido")
	check(layer.carry_node.position.y > 0.6 and layer.carry_node.position.z > 0.2, "à frente do peito, não no chão: %s" % str(layer.carry_node.position))
	layer.note_sheet({"home": {"carry": {"seq": 1, "id": "plant", "item": "Planta", "kg": 3.0, "with": null}, "arrived": [], "coming": 0}})
	eq(actor.get_child_count(), 1, "a mesma carga não duplica")
	layer.note_sheet({"money": 3})
	await tree.process_frame
	check(layer.carry_item == "" and actor.get_child_count() == 0, "largou: o motor já não diz a carga")
	made[1].queue_free()
