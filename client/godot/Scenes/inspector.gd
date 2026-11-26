extends PanelContainer
## Inspetor genérico (ADR-0047): uma ficha para qualquer entidade observável — pessoa, prédio, cidade, processo, lavoura,
## animal, bando, espólio, objeto inventado, mobília do interior, local de trabalho, casa e evento. Atualiza sozinho
## enquanto aberto, tem links clicáveis e histórico (Voltar). Etapa 3: todo tipo com rota usa `GET /entities/{kind}/{id}`
## (nome real, vínculos e história recente) por cima do que a store já tem. Só lê (WorldReader e store).

const Panels := preload("res://Scenes/panels.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Describe := preload("res://Scenes/inspector_describe.gd")
const Look := preload("res://Scenes/npc_look.gd")
const Works := preload("res://Store/work_book.gd")
const WorkDescribe := preload("res://Scenes/work_describe.gd")

signal follow_requested(entity: Dictionary)
signal enter_requested(building_id: int)
signal trades_requested(workplace_id: int)
signal focus_requested(point: Vector3)
signal inspected(entity: Dictionary)

const REFRESH_SECONDS := 1.0
const FOLLOWABLE := [Registry.PERSON, Registry.HOUSEHOLD, Registry.CITY]

var reader
var registry
var store
var labels
var hours_per_day: Callable = func() -> float: return 24.0
var date_of: Callable = func(tick: int) -> String: return "tick %d" % tick
var event_point: Callable = func(_id: int) -> Vector3: return Vector3.INF
var point_of: Callable = func(_entity: Dictionary) -> Vector3: return Vector3.INF ## onde a entidade está na cena ("Ir até lá")
var current: Dictionary = {} ## {kind, id}; vazio = fechado
var history: Array = [] ## referências anteriores (Voltar)
var text := "" ## corpo da ficha genérica (o que não é pessoa nem cidade), testável
var link_refs: Array = [] ## referências dos links mostrados agora, na ordem
var npc_panel
var city_panel
var back_button: Button
var follow_button: Button
var enter_button: Button
var trades_button: Button
var focus_button: Button
var _title: Label
var _kind: Label
var _generic: Label
var _extra: Label ## pessoa e cidade: corpo visível e história recente (o resto vai em `_generic`)
var extra_text := "" ## testável
var server_body: Variant = null ## última resposta de /entities para a entidade em foco (null = sem rota ou falhou)
var _links: VBoxContainer
var _timer: Timer
var _request := 0
var _link_keys: Array = []


func _init() -> void:
	custom_minimum_size = Vector2(300, 0)
	var card := StyleBoxFlat.new() # #222: um cartão só, do título aos vínculos (o fim da ficha não fica solto sobre a cena)
	card.bg_color = Color(0.07, 0.08, 0.12, 0.9)
	card.set_corner_radius_all(10)
	card.content_margin_left = 8
	card.content_margin_right = 8
	card.content_margin_top = 8
	card.content_margin_bottom = 8
	add_theme_stylebox_override("panel", card)
	var box := VBoxContainer.new()
	add_child(box)
	var header := HBoxContainer.new()
	box.add_child(header)
	back_button = _button(header, "◀ Voltar", back)
	_kind = Label.new()
	_kind.modulate = Color(1, 1, 1, 0.6)
	_kind.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	header.add_child(_kind)
	follow_button = _button(header, "Seguir", func(): follow_requested.emit(current))
	_button(header, "✕", close)
	_title = Label.new()
	_title.add_theme_font_size_override("font_size", 20)
	box.add_child(_title)
	npc_panel = Panels.NpcDetailPanel.new()
	box.add_child(npc_panel)
	city_panel = Panels.CityPanel.new()
	box.add_child(city_panel)
	_generic = Label.new()
	_generic.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_generic.custom_minimum_size.x = 280
	box.add_child(_generic)
	_extra = Label.new()
	_extra.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_extra.custom_minimum_size.x = 280
	_extra.modulate = Color(1, 1, 1, 0.85)
	box.add_child(_extra)
	var actions := HBoxContainer.new()
	box.add_child(actions)
	enter_button = _button(actions, "Entrar", func(): enter_requested.emit(int(current.id)))
	trades_button = _button(actions, "Vendas", func(): trades_requested.emit(int(current.id)))
	focus_button = _button(actions, "Ir até lá", func(): focus_requested.emit(_point(current)))
	_links = VBoxContainer.new()
	box.add_child(_links)
	_timer = Timer.new()
	_timer.wait_time = REFRESH_SECONDS
	_timer.autostart = true
	_timer.timeout.connect(_on_timer)
	add_child(_timer)
	visible = false


func _button(parent: Control, label: String, action: Callable) -> Button:
	var button := Button.new()
	button.text = label
	button.pressed.connect(action)
	parent.add_child(button)
	return button


func _on_timer() -> void:
	if is_open():
		refresh(true)


func is_open() -> bool:
	return visible and not current.is_empty()


## Abre a ficha da entidade; a anterior vai para o histórico (Voltar).
func inspect(entity: Dictionary, remember: bool = true) -> void:
	if remember and not current.is_empty() and Registry.key(current) != Registry.key(entity):
		history.append(current)
		if history.size() > 30:
			history.pop_front()
	current = entity
	visible = true
	inspected.emit(entity)
	await refresh()


func _point(entity: Dictionary) -> Vector3:
	if String(entity.get("kind", "")) == Registry.EVENT:
		var at: Vector3 = event_point.call(int(entity.id))
		if at != Vector3.INF:
			return at
	return point_of.call(entity)


## Rota de inspeção do tipo (pessoa vira "npc", objeto inventado "object"...); null se o tipo não tem rota ou falhou.
func _server(entity: Dictionary) -> Variant:
	var kind := String(Registry.SERVER_KINDS.get(String(entity.kind), ""))
	if kind == "" or reader == null or not reader.has_method("entity"):
		return null
	var response: Dictionary = await reader.entity(kind, entity.id)
	return response.body if response.ok and response.body is Dictionary else null


func back() -> void:
	if history.is_empty():
		return
	await inspect(history.pop_back(), false)


func close() -> void:
	current = {}
	history.clear()
	visible = false


## Relê a entidade em foco: pessoa pelo reader (cache por hora do mundo), o resto pela store (custo zero de rede).
func refresh(auto: bool = false) -> void:
	if current.is_empty():
		return
	_request += 1
	var ticket := _request
	var kind := String(current.kind)
	_kind.text = {"person": "Pessoa", "building": "Prédio", "city": "Cidade", "process": "Processo", "object": "Objeto",
		"workplace": "Local de trabalho", "household": "Casa", "event": "Evento", "crop": "Lavoura", "animal": "Animal",
		"fauna": "Bando", "leaving": "Espólio", "item": "Objeto inventado", "street": "Rua", "work": "Obra"}.get(kind, kind)
	back_button.disabled = history.is_empty()
	follow_button.visible = kind in FOLLOWABLE
	enter_button.visible = kind == Registry.BUILDING
	trades_button.visible = kind == Registry.WORKPLACE
	focus_button.visible = kind == Registry.EVENT or point_of.call(current) != Vector3.INF
	var links: Array = []
	var body = await _server(current)
	if ticket != _request:
		return
	server_body = body
	var extra: Array[String] = []
	match kind:
		Registry.PERSON:
			var response: Dictionary
			if body is Dictionary and body.get("state") is Dictionary and body.state.get("npc") is Dictionary:
				response = {"ok": true, "status": 200, "body": body.state.npc}
			else:
				response = await reader.npc_detail(int(current.id))
			if ticket != _request:
				return
			if response.ok and response.body is Dictionary:
				npc_panel._present(int(current.id), response.body, auto)
				links = person_links(int(current.id), response.body)
			text = "" if response.ok else "Não foi possível ler esta pessoa (erro %d)." % int(response.status)
			var npc = store.npcs.get(int(current.id))
			var hint := Look.hint_of(npc.get("obs", {})) if npc != null else ""
			if hint != "":
				extra.append(hint)
		Registry.CITY:
			await _show_city(String(current.id))
			if ticket != _request:
				return
			text = ""
			var founder := Describe.founder(body) # AD-183: quem liderou a saída e fundou a cidade-filha
			if not founder.is_empty():
				extra.append(String(founder.line))
				links.append(founder.link)
		_:
			var built: Dictionary = await describe(current)
			if ticket != _request:
				return
			text = built.text
			links = built.links
	links = Describe.merge_links(links, Describe.server_links(body))
	var recent := Describe.recent_lines(registry, body, date_of)
	if not recent.is_empty():
		extra.append("História recente:")
		extra.append_array(recent)
	if kind == Registry.PERSON or kind == Registry.CITY:
		extra_text = "\n".join(extra)
	else:
		extra_text = ""
		if not recent.is_empty():
			text += "\n" + "\n".join(extra)
	_extra.text = extra_text
	_extra.visible = extra_text != ""
	var server_name := String(body.get("name", "")) if body is Dictionary else ""
	_title.text = (server_name if server_name != "" and kind not in [Registry.EVENT] else registry.title_of(current)) if kind != Registry.PERSON else ""
	_title.visible = kind != Registry.PERSON and kind != Registry.CITY
	npc_panel.visible = kind == Registry.PERSON and text == ""
	city_panel.visible = kind == Registry.CITY
	_generic.text = text
	_generic.visible = text != ""
	_set_links(links)


func _show_city(city_id: String) -> void:
	if store.kind == "City" and String(store.city.get("id", "")) == city_id:
		city_panel.show_city(store.city)
		return
	var response: Dictionary = await reader.city_snapshot(city_id)
	if response.ok and response.body is Dictionary and response.body.get("payload") is Dictionary:
		var payload: Dictionary = response.body.payload
		city_panel.show_city({"id": city_id, "name": payload.name, "indicators": payload.indicators,
			"pendingResidentIds": payload.get("pendingResidentIds", [])})


## Links da pessoa: mãe, pai, cônjuge, filhos conhecidos, casa, trabalho e a cidade onde está.
func person_links(id: int, detail: Dictionary) -> Array:
	var links: Array = []
	for spec in [["Mãe", "motherId"], ["Pai", "fatherId"], ["Cônjuge", "spouse"]]:
		var value = detail.get(spec[1])
		if value is Dictionary:
			links.append([spec[0], Registry.ref(Registry.PERSON, int(value.value))])
	for child in registry.known_children(id):
		links.append(["Filho(a)", Registry.ref(Registry.PERSON, int(child))])
	if detail.get("household") is Dictionary:
		links.append(["Casa", Registry.ref(Registry.HOUSEHOLD, int(detail.household.value))])
	if detail.get("employer") is Dictionary:
		links.append(["Trabalho", Registry.ref(Registry.WORKPLACE, int(detail.employer.value))])
	var employment = detail.get("employment")
	if employment is Dictionary and employment.get("buildingId") is Dictionary:
		links.append(["Prédio do trabalho", Registry.ref(Registry.BUILDING, int(employment.buildingId.value))])
	if detail.get("city") is Dictionary:
		links.append(["Cidade", Registry.ref(Registry.CITY, String(detail.city.value))])
	return links


## Ficha das entidades que a store já descreve inteiras: {text, links}.
func describe(entity: Dictionary) -> Dictionary:
	if String(entity.kind) == Registry.ITEM:
		return await _describe_item(entity)
	return _describe_sync(entity)


func _describe_item(entity: Dictionary) -> Dictionary:
	var object = store.objects.get(int(entity.id))
	var design = null
	if object != null and reader != null and reader.has_method("design"):
		var response: Dictionary = await reader.design(int(object.design_id))
		design = response.body if response.ok else null
	return Describe.item(store, int(entity.id), design)


func _describe_sync(entity: Dictionary) -> Dictionary:
	var id = entity.id
	match String(entity.kind):
		Registry.BUILDING:
			return _describe_building(int(id))
		Registry.PROCESS:
			return _describe_process(int(id))
		Registry.OBJECT:
			return _describe_object(String(id))
		Registry.WORKPLACE:
			return Describe.workplace(store, registry, int(id))
		Registry.HOUSEHOLD:
			return Describe.household(store, registry, int(id), server_body)
		Registry.EVENT:
			return Describe.event(registry, int(id), date_of)
		Registry.CROP:
			return Describe.crop(store, registry, int(id))
		Registry.ANIMAL:
			return Describe.animal(store, int(id))
		Registry.FAUNA:
			return Describe.fauna(store, String(id))
		Registry.LEAVING:
			return Describe.leaving(store, registry, int(id))
		Registry.STREET:
			return Describe.street(store, String(id))
		Registry.WORK: # AD-193
			return WorkDescribe.describe(store, registry, WorkDescribe.work_of(store, int(id), server_body), hours_per_day.call(), date_of)
	return {"text": "", "links": []}


func _describe_building(id: int) -> Dictionary:
	var building = store.buildings.get(id)
	if building == null:
		return {"text": "Este prédio não está no escopo atual (ou foi demolido).", "links": []}
	var lines: Array[String] = ["Edifício %d · %d andar%s · célula %d,%d" % [id, int(building.floors),
		"es" if int(building.floors) > 1 else "", building.cell.x, building.cell.y]]
	var site := Works.of_building(store, id)
	if not site.is_empty(): # AD-193: canteiro — o prédio ainda não existe; a ficha é a da obra
		var view := WorkDescribe.describe(store, registry, site, hours_per_day.call(), date_of)
		var site_links: Array = [["Obra", Registry.ref(Registry.WORK, int(site.id))]]
		site_links.append_array(view.links)
		return {"text": "Canteiro de obra (ninguém mora nem trabalha aqui ainda)\n" + String(view.text) + "\n" + "\n".join(lines), "links": site_links}
	var lodging = building.get("lodging")
	if lodging is Dictionary:
		lines.append("Hospedagem: %d/%d vagas · aluguel %d/mês%s" % [int(lodging.occupied), int(lodging.units), int(lodging.rentPerMonth),
			(" · diária %d" % int(lodging.nightlyRate)) if int(lodging.nightlyRate) > 0 else ""])
	if int(building.get("value", 0)) > 0:
		lines.append("Valor estimado: %d" % int(building.value))
	if store.pending_footprints.has(id):
		lines.append("Recém-construído: sem planta ainda (servidor antigo).")
	var state_view := Describe.building_state(store, registry, id)
	lines.append_array(state_view.lines)
	var lot_view := Describe.lot(store, id)
	lines.append_array(lot_view.lines)
	var links: Array = lot_view.links + state_view.links
	if int(store.interior.get("building", -1)) == id:
		lines.append("Quem está dentro agora:")
		for occupant in store.interior.get("occupants", []):
			links.append(["Dentro", Registry.ref(Registry.PERSON, int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc))])
	else:
		lines.append("Entre (duplo clique) para ver quem está dentro.")
	return {"text": "\n".join(lines), "links": links}


func _describe_process(id: int) -> Dictionary:
	var process = store.processes.get(id)
	if process == null:
		return {"text": "Este processo terminou.", "links": []}
	var lines: Array[String] = ["%s · %d%%" % [registry.process_name(process), int(round(float(process.progress) * 100.0))]]
	if process.remaining != null:
		lines.append("Faltam %s" % _hours_text(float(process.remaining)))
	if process.quality != null:
		lines.append("Qualidade %d%%" % int(round(float(process.quality) * 100.0)))
	if process.has_cell:
		lines.append("Célula %d,%d" % [process.cell.x, process.cell.y])
	var links: Array = []
	match String(process.kind):
		"rest", "food", "cook", "water", "process":
			links.append(["Quem faz", Registry.ref(Registry.PERSON, int(process.target))])
		"extraordinary-construct":
			links.append(["Criador(a)", Registry.ref(Registry.PERSON, int(process.target))])
		"construction":
			lines.append("Obra: %s" % registry.building_type_name(int(process.target)))
		"crop":
			links.append(["Lavoura", Registry.ref(Registry.CROP, int(process.target))])
	return {"text": "\n".join(lines), "links": links}


func _hours_text(hours: float) -> String:
	var per_day: float = maxf(hours_per_day.call(), 1.0)
	return "%d h" % int(hours) if hours < per_day else "%.1f dias" % (hours / per_day)


func _describe_object(object_id: String) -> Dictionary:
	var descriptor: Dictionary = registry.objects.get(object_id, {})
	var lines: Array[String] = ["%s · %s" % [object_id, String(descriptor.get("category", "objeto"))]]
	var uses: Array[String] = []
	for point in descriptor.get("usePoints", []):
		if point.get("action") != null:
			uses.append(labels.label_for(int(point.action)) if labels != null else str(point.action))
	lines.append("Serve para: %s" % (", ".join(uses) if not uses.is_empty() else "enfeite (sem ponto de uso)"))
	lines.append("Partes: %d" % descriptor.get("shape", []).size())
	var links: Array = []
	for occupant in store.interior.get("occupants", []):
		if String(occupant.get("usingObject", "")) == object_id:
			links.append(["Usando agora", Registry.ref(Registry.PERSON, int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc))])
	return {"text": "\n".join(lines), "links": links}


func _set_links(links: Array) -> void:
	var wanted: Array = links.map(func(link): return "%s|%s|%s" % [link[0], Registry.key(link[1]), registry.title_of(link[1])])
	if wanted == _link_keys:
		return # atualização sozinha sem mudança: não refaz os botões a cada segundo
	_link_keys = wanted
	link_refs = []
	for child in _links.get_children():
		child.queue_free()
	for link in links:
		var entity: Dictionary = link[1]
		link_refs.append(entity)
		var button := Button.new()
		button.text = "%s: %s" % [link[0], registry.title_of(entity)]
		button.alignment = HORIZONTAL_ALIGNMENT_LEFT
		button.pressed.connect(func(): inspect(entity))
		_links.add_child(button)


## Para testes e atalhos: aperta o link N como o usuário faria.
func open_link(index: int) -> void:
	if index >= 0 and index < link_refs.size():
		await inspect(link_refs[index])
