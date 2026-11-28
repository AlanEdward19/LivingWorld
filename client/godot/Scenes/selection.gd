extends RefCounted
## Seleção e dica: o que está sob o cursor, no mundo e dentro do interior. Pessoas, marcadores de evento,
## processos e objetos são pequenos: valem pela distância na tela (pixels); prédio e cidade, pelo raio físico.
## Devolve sempre um "acerto" {kind, id} que o inspetor sabe abrir; nunca fala com o servidor.

const Registry := preload("res://Store/entity_registry.gd")
const Look := preload("res://Scenes/npc_look.gd")
const Grid := preload("res://Scenes/grid.gd")
const Style := preload("res://Scenes/render/street_style.gd")
const Works := preload("res://Store/work_book.gd")

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
	var animal = nearest(app.world_view.fauna.positions, camera, screen, 20.0)
	if animal != null:
		return Registry.ref(Registry.ANIMAL, int(animal))
	var group = nearest(app.world_view.fauna.group_positions, camera, screen, 26.0)
	if group != null:
		return Registry.ref(Registry.FAUNA, String(group))
	var thing = nearest(app.world_view.items.positions, camera, screen, 18.0)
	if thing != null:
		var parts := String(thing).split(":")
		return Registry.ref(Registry.LEAVING if parts[0] == "leaving" else Registry.ITEM, int(parts[1]))
	var crop = nearest(app.world_view.crops.positions, camera, screen, 22.0)
	if crop != null:
		return Registry.ref(Registry.CROP, int(crop))
	return from_hit(raycast(screen))


## O acerto do raio (colisor + ponto) como entidade: corpo, prédio, cidade ou o chão (rua/lote pela célula).
func from_hit(hit: Dictionary) -> Dictionary:
	if hit.is_empty():
		return {}
	var collider: Object = hit.collider
	if collider.has_meta("npc_id"):
		return Registry.ref(Registry.PERSON, int(collider.get_meta("npc_id")))
	if collider.has_meta("building_id"):
		return Registry.ref(Registry.BUILDING, int(collider.get_meta("building_id")))
	if collider.has_meta("city_id"):
		return Registry.ref(Registry.CITY, String(collider.get_meta("city_id")))
	if collider.has_meta("ground"): # chão: rua da camada `Roads` ou lote de um prédio (AD-177); terreno comum = nada
		return app.world_view.ground.entity_at(Grid.to_cell(hit.position))
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
		var ref := Registry.ref(Registry.OBJECT, String(points[int(object)].id))
		var spot: Vector3 = points[int(object)].position
		ref["cell"] = Vector2i(Grid.to_cell(spot)) # #267: a célula do móvel clicado (o motor oferece cada peça na sua célula)
		return ref
	return {}


func raycast(screen: Vector2) -> Dictionary:
	var camera: Camera3D = app.rig.camera
	if not camera.is_inside_tree():
		return {}
	var origin := camera.project_ray_origin(screen)
	var end := origin + camera.project_ray_normal(screen) * 400.0
	var query := PhysicsRayQueryParameters3D.create(origin, end)
	var space := camera.get_world_3d().direct_space_state
	var hit: Dictionary = space.intersect_ray(query)
	for _i in 4: # prédio translúcido não segura o mouse: o raio passa para o que está atrás
		if hit.is_empty() or not hit.collider.has_meta("building_id") or not app.world_view.buildings.passes_mouse(int(hit.collider.get_meta("building_id"))):
			break
		query.exclude = query.exclude + [hit.rid]
		hit = space.intersect_ray(query)
	return hit


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
			if label != "" and npc != null and npc.get("target", {}).get("kind", "") == "npc":
				label += " com %s" % app.registry.person_name(int(npc.target.id))
			elif label != "" and npc != null and npc.get("target", {}).get("kind", "") == "work": # AD-193: no lote de uma obra
				label += " na obra (%s)" % app.registry.title_of(Registry.ref(Registry.WORK, int(npc.target.id))).trim_prefix("Obra: ")
			var text := name if label == "" else "%s\n%s" % [name, label]
			var hint := Look.hint_of(npc.get("obs", {})) if npc != null else ""
			return text if hint == "" else "%s\n%s" % [text, hint]
		Registry.BUILDING:
			var building = store.buildings.get(int(id))
			var floors: int = building.floors if building != null else 1
			var kind_name: String = app.registry.building_type_name(int(building.type)) if building != null else "Edifício"
			var state: Dictionary = store.building_states.get(int(id), {})
			if String(state.get("location_type_name", "")) != "":
				kind_name = "%s (%s)" % [state.location_type_name, kind_name]
			var text := "%s · Edifício %d · %d andar%s" % [kind_name, int(id), floors, "es" if floors > 1 else ""]
			var site: Dictionary = Works.of_building(store, int(id))
			if not site.is_empty(): # AD-193: canteiro — a dica diz que é obra, quanto falta e se parou
				text = "Obra: %s · %d%% feita · %d no lote agora%s" % [Works.what(site), int(round(float(site.progress) * 100.0)),
					site.get("on_site", []).size(), (" · parada (%s)" % String(site.halt)) if String(site.get("halt", "")) != "" else ""]
			if int(state.get("owner_id", -1)) >= 0:
				text += "\ndono: %s" % app.registry.person_name(int(state.owner_id))
			if not state.get("residents", []).is_empty():
				text += " · %d morador(es)" % state.residents.size()
			return text + _price_lines(building) + _lot_hint(building) + "\nclique: ficha · duplo clique: entrar · Tab: o de trás"
		Registry.CITY:
			var city = store.cities.get(String(id))
			if city != null:
				return "%s · %d hab.\nclique: ficha · duplo clique: entrar" % [city.name if city.name != "" else "Cidade", city.population]
		Registry.PROCESS:
			var process = store.processes.get(int(id))
			if process != null:
				return "%s · %d%%" % [app.registry.process_name(process), int(round(float(process.progress) * 100.0))]
		Registry.EVENT:
			return app.registry.event_title(app.registry.events.get(int(id), {})) + "\nclique: ficha do evento"
		Registry.CROP:
			var crop = store.crops.get(int(id))
			if crop != null:
				return "%s · %s\ncolheita esperada %d · clique: ficha" % [app.registry.title_of(entity),
					Registry.STAGE_NAMES.get(String(crop.stage), String(crop.stage)), int(crop.expected)]
		Registry.ANIMAL, Registry.FAUNA, Registry.LEAVING, Registry.ITEM:
			return "%s\nclique: ficha" % app.registry.title_of(entity)
		Registry.OBJECT:
			return "%s\nclique: ficha do objeto" % app.registry.title_of(entity)
		Registry.STREET:
			var road = store.roads.get(Registry.street_cell(String(id)))
			if road != null:
				var users := int(road.users)
				var work := Style.work_of(road)
				var doing := "\nem obra: %s · %d%%" % [Style.surface_name(int(work.surface)), int(work.progress)] if not work.is_empty() else ""
				if int(road.get("use_band", -1)) >= 0: # AD-176: `users` = passagens do último mês
					return "%s · %d passage%s no último mês%s\nclique: ficha da rua" % [Style.title_of(road), users, "m" if users == 1 else "ns", doing]
				return "%s · %d pessoa%s passa%s por aqui%s\nclique: ficha da rua" % [Style.title_of(road), users,
					"" if users == 1 else "s", "" if users == 1 else "m", doing]
	return ""


## Uma linha curta do lote na dica do prédio (AD-177): quintal, caminho e rua, como o motor disse.
static func _lot_hint(building) -> String:
	var parcel: Dictionary = building.get("lot", {}) if building != null else {}
	if parcel.is_empty():
		return ""
	var parts: Array[String] = ["quintal de %d célula%s" % [parcel.croft.size(), "" if parcel.croft.size() == 1 else "s"]
		if not parcel.croft.is_empty() else "sem quintal"]
	if not parcel.front_path.is_empty():
		parts.append("caminho de %d até a rua" % parcel.front_path.size())
	elif parcel.street != null:
		parts.append("porta na rua")
	var access := Style.access_of(parcel)
	if access not in ["", "street", "path"]:
		parts.append(("⚠ " if access == "none" else "") + "acesso: %s" % Style.access_style(access).name)
	return "\nlote: " + " · ".join(parts)


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


## Pontos que não podem ficar escondidos atrás de um prédio: o que está sob o mouse, a pessoa selecionada e o alvo seguido.
## Cada um é [ponto, id do prédio a ignorar] (o próprio prédio em foco não cobre a si mesmo).
func focus_points(hovered: Dictionary) -> Array:
	var points: Array = []
	var entities: Array = [hovered]
	if app.selected_npc >= 0:
		entities.append(Registry.ref(Registry.PERSON, app.selected_npc))
	if app.director.active():
		entities.append(app.director.target)
	for entity in entities:
		if entity.is_empty():
			continue
		match String(entity.kind):
			Registry.PERSON:
				var node = app.world_view.npc_nodes.get(int(entity.id))
				if node != null:
					points.append([node.position + Vector3(0, 0.6 * node.scale.y, 0), -1])
			Registry.BUILDING:
				var center: Vector3 = app.world_view.building_center(int(entity.id))
				if center != Vector3.INF:
					points.append([center + Vector3(0, 0.5, 0), int(entity.id)])
			_:
				var point: Vector3 = app.point_of(entity)
				if point != Vector3.INF:
					points.append([point, -1])
	if app.director.active() and String(app.director.target.get("kind", "")) != Registry.PERSON and app.director.last_point != Vector3.INF:
		points.append([app.director.last_point + Vector3(0, 0.5, 0), -1])
	return points


## Prédio que cobre o que importa fica translúcido enquanto cobrir (só a cidade). #249: com a casa em foco a cidade continua desenhada
## em volta: os vizinhos que ficam entre a câmera e a casa (ou o corpo dentro dela) ficam translúcidos; a própria casa não é um deles.
func update_fade(hovered: Dictionary) -> void:
	if app.store.kind != "City":
		app.world_view.buildings.set_faded({})
		return
	if app.stage.active:
		var neighbors: Dictionary = {}
		if app.rig.mode != app.rig.ISO:
			neighbors = app.rig.seen_ids().duplicate()
		elif app.rig.camera.is_inside_tree():
			var looking: Vector3 = -app.rig.camera.global_transform.basis.z
			neighbors = app.world_view.buildings.occluders([[app.stage.interior_view.center() + Vector3(0, 0.5, 0), app.stage.covered_building]], looking)
		neighbors.erase(-1)
		neighbors.erase(app.stage.covered_building)
		app.world_view.buildings.set_faded(neighbors)
		return
	if app.rig.mode != app.rig.ISO: # AD-197s: encarnado, quem cobre o corpo é a conta do braço de mola (não a das ortográficas, que apagava a dela)
		var seen: Dictionary = app.rig.seen_ids()
		if app.reading != null:
			seen = seen.duplicate()
			seen.merge(app.reading.pushed)
		app.world_view.buildings.set_faded(seen)
		return
	var camera: Camera3D = app.rig.camera
	var view_dir := -camera.global_transform.basis.z if camera.is_inside_tree() else Vector3(-0.5, -0.7, -0.5).normalized()
	var covering: Dictionary = app.world_view.buildings.occluders(focus_points(hovered), view_dir)
	if app.reading != null:
		covering.merge(app.reading.pushed) # Tab/Alt+roda: os da frente da pilha sob o cursor abrem
	app.world_view.buildings.set_faded(covering)


func update_hover() -> void:
	if app.stage == null or not app.rig.camera.is_inside_tree():
		app.tooltip.visible = false
		return
	var mouse: Vector2 = app.get_viewport().get_mouse_position()
	if app.reading != null and app.reading.plan_open():
		app.reading.plan.update_tooltip(mouse)
		return
	if app.reading != null:
		app.reading.on_hover(mouse)
	var hit: Dictionary = hit_at(mouse)
	update_fade(hit)
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
