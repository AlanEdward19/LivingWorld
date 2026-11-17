extends RefCounted
## Seleção e dica: o que está sob o cursor, no mundo e dentro do interior. Pessoas, marcadores de evento,
## processos e objetos são pequenos: valem pela distância na tela (pixels); prédio e cidade, pelo raio físico.
## Devolve sempre um "acerto" {kind, id} que o inspetor sabe abrir; nunca fala com o servidor.

const Registry := preload("res://Store/entity_registry.gd")

const NEAR_PIXELS := 32.0

var app ## o App (store, world_view, stage, rig, labels, registry)


func _init(p_app) -> void:
	app = p_app


## Ponto da lista `points` (id -> Vector3) mais perto de `screen` em pixels, ou null.
static func nearest(points: Dictionary, camera: Camera3D, screen: Vector2, max_distance: float = NEAR_PIXELS) -> Variant:
	var best = null
	var best_distance := max_distance
	for id in points:
		var point: Vector3 = points[id]
		if camera.is_position_behind(point):
			continue
		var distance := camera.unproject_position(point).distance_to(screen)
		if distance < best_distance:
			best_distance = distance
			best = id
	return best


## NPC visível mais próximo do clique, -1 se nenhum: o raio costuma acertar o telhado ao lado de um NPC pequeno.
func nearest_npc(screen: Vector2, max_distance: float = NEAR_PIXELS) -> int:
	var points := {}
	for id in app.world_view.npc_nodes:
		var actor: Node3D = app.world_view.npc_nodes[id]
		if actor.visible:
			points[id] = actor.global_position + Vector3(0, 0.6, 0)
	var found = nearest(points, app.rig.camera, screen, max_distance)
	return int(found) if found != null else -1


## O que está sob o ponto da tela: {kind, id} ou vazio.
func hit_at(screen: Vector2) -> Dictionary:
	if app.stage.active:
		return _interior_hit(screen)
	var camera: Camera3D = app.rig.camera
	var npc := nearest_npc(screen)
	if npc >= 0:
		return Registry.ref(Registry.PERSON, npc)
	var event = nearest(app.world_view.events.positions, camera, screen)
	if event != null:
		return Registry.ref(Registry.EVENT, int(event))
	var process = nearest(app.world_view.processes.positions, camera, screen, 18.0)
	if process != null:
		return Registry.ref(Registry.PROCESS, int(process))
	var hit := raycast(screen)
	if hit.is_empty():
		return {}
	var collider: Object = hit.collider
	if collider.has_meta("npc_id"):
		return Registry.ref(Registry.PERSON, int(collider.get_meta("npc_id")))
	if collider.has_meta("building_id"):
		return Registry.ref(Registry.BUILDING, int(collider.get_meta("building_id")))
	if collider.has_meta("city_id"):
		return Registry.ref(Registry.CITY, String(collider.get_meta("city_id")))
	return {}


func _interior_hit(screen: Vector2) -> Dictionary:
	var view = app.stage.interior_view
	var camera: Camera3D = app.stage.camera()
	if camera == null or not camera.is_inside_tree():
		return {}
	var people := {}
	for id in view.occupant_nodes:
		var actor: Node3D = view.occupant_nodes[id]
		if actor.visible:
			people[id] = actor.global_position + Vector3(0, 0.6, 0)
	var person = nearest(people, camera, screen)
	if person != null:
		return Registry.ref(Registry.PERSON, int(person))
	var objects := {}
	var points: Array = view.visible_objects()
	for index in points.size():
		objects[index] = points[index].position
	var object = nearest(objects, camera, screen, 26.0)
	if object != null:
		return Registry.ref(Registry.OBJECT, String(points[int(object)].id))
	return {}


func raycast(screen: Vector2) -> Dictionary:
	var camera: Camera3D = app.rig.camera
	if not camera.is_inside_tree():
		return {}
	var origin := camera.project_ray_origin(screen)
	var end := origin + camera.project_ray_normal(screen) * 400.0
	var query := PhysicsRayQueryParameters3D.create(origin, end)
	return camera.get_world_3d().direct_space_state.intersect_ray(query)


## Texto da dica para um acerto (testável sem mouse). Aceita também o formato antigo {npc_id|building_id|city_id}.
func describe(hit: Dictionary) -> String:
	var entity := hit
	if hit.has("npc_id"):
		entity = Registry.ref(Registry.PERSON, int(hit.npc_id))
	elif hit.has("building_id"):
		entity = Registry.ref(Registry.BUILDING, int(hit.building_id))
	elif hit.has("city_id"):
		entity = Registry.ref(Registry.CITY, String(hit.city_id))
	if entity.is_empty():
		return ""
	var store = app.store
	var id = entity.id
	match String(entity.kind):
		Registry.PERSON:
			var npc = store.npcs.get(int(id))
			var action := -1 if npc == null else int(npc.action)
			if npc == null and app.stage.active and app.stage.interior_view.occupant_nodes.has(int(id)):
				action = int(app.stage.interior_view.occupant_nodes[int(id)].action)
			var name: String = store.names.get(int(id), "NPC %d" % int(id))
			var label: String = app.labels.label_for(action) if action >= 0 else ""
			return name if label == "" else "%s\n%s" % [name, label]
		Registry.BUILDING:
			var building = store.buildings.get(int(id))
			var floors: int = building.floors if building != null else 1
			var kind_name: String = app.registry.building_type_name(int(building.type)) if building != null else "Edifício"
			var text := "%s · Edifício %d · %d andar%s" % [kind_name, int(id), floors, "es" if floors > 1 else ""]
			return text + _price_lines(building) + "\nclique: ficha · duplo clique: entrar"
		Registry.CITY:
			var city = store.cities.get(String(id))
			if city != null:
				return "%s · %d hab.\nclique: ficha · duplo clique: entrar" % [city.name if city.name != "" else "Cidade", city.population]
		Registry.PROCESS:
			var process = store.processes.get(int(id))
			if process != null:
				return "%s · %d%%" % [app.registry.process_name(process), int(round(float(process.progress) * 100.0))]
		Registry.EVENT:
			return String(app.registry.events.get(int(id), {}).get("label", "Evento")) + "\nclique: ficha do evento"
		Registry.OBJECT:
			return "%s\nclique: ficha do objeto" % app.registry.title_of(entity)
	return ""


## Preço do imóvel como o servidor calculou hoje: hospedagem mostra vagas, aluguel e diária.
static func _price_lines(building) -> String:
	if building == null:
		return ""
	var lodging = building.get("lodging")
	if lodging is Dictionary:
		var line := "\nHospedagem · %d/%d vagas · aluguel %d/mês" % [int(lodging.occupied), int(lodging.units), int(lodging.rentPerMonth)]
		if int(lodging.nightlyRate) > 0:
			line += " · diária %d" % int(lodging.nightlyRate)
		return line + ("\nvalor %d" % int(building.value) if int(building.get("value", 0)) > 0 else "")
	if int(building.get("value", 0)) > 0:
		return "\nvalor estimado %d" % int(building.value)
	return ""


func update_hover() -> void:
	if app.stage == null or not app.rig.camera.is_inside_tree():
		app.tooltip.visible = false
		return
	var mouse: Vector2 = app.get_viewport().get_mouse_position()
	var hit: Dictionary = hit_at(mouse)
	app.hover_text = describe(hit)
	app.world_view.hover_npc(int(hit.id) if not hit.is_empty() and hit.kind == Registry.PERSON and not app.stage.active else -1)
	app.tooltip.visible = app.hover_text != "" and not app.list_panel.get_global_rect().has_point(mouse)
	if app.tooltip.visible:
		app.tooltip_label.text = app.hover_text
		app.tooltip.position = mouse + Vector2(16, 18)


## Clique simples abre a ficha; duplo clique entra (prédio ou cidade).
func pick(screen_position: Vector2, double_click: bool) -> void:
	var hit: Dictionary = hit_at(screen_position)
	if hit.is_empty():
		return
	match String(hit.kind):
		Registry.BUILDING:
			if double_click:
				app.enter_building(int(hit.id))
			else:
				app.inspect(hit)
		Registry.CITY:
			if double_click:
				app.enter_scope("City", String(hit.id))
			elif app.store.kind == "World":
				app.inspect(hit)
		_:
			app.inspect(hit)
