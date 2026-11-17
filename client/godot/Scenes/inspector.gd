extends PanelContainer
## Inspetor genérico (ADR-0047, etapa 1): uma ficha para qualquer entidade observável — pessoa, prédio, cidade,
## processo/lavoura, objeto do interior, local de trabalho, casa (família) e evento no mapa. Atualiza sozinho enquanto
## aberto, tem links clicáveis (parentesco, casa, trabalho, quem está dentro, quem usa) e histórico (Voltar).
## Lê só pelo WorldReader (`reader`) e pela store; nunca escreve no mundo.

const Panels := preload("res://Scenes/panels.gd")
const Registry := preload("res://Store/entity_registry.gd")

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
var _links: VBoxContainer
var _timer: Timer
var _request := 0
var _link_keys: Array = []


func _init() -> void:
	custom_minimum_size = Vector2(300, 0)
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
	var actions := HBoxContainer.new()
	box.add_child(actions)
	enter_button = _button(actions, "Entrar", func(): enter_requested.emit(int(current.id)))
	trades_button = _button(actions, "Vendas", func(): trades_requested.emit(int(current.id)))
	focus_button = _button(actions, "Ir até lá", func(): focus_requested.emit(event_point.call(int(current.id))))
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
		"workplace": "Local de trabalho", "household": "Casa", "event": "Evento"}.get(kind, kind)
	back_button.disabled = history.is_empty()
	follow_button.visible = kind in FOLLOWABLE
	enter_button.visible = kind == Registry.BUILDING
	trades_button.visible = kind == Registry.WORKPLACE
	focus_button.visible = kind == Registry.EVENT
	var links: Array = []
	match kind:
		Registry.PERSON:
			var response: Dictionary = await reader.npc_detail(int(current.id))
			if ticket != _request:
				return
			if response.ok and response.body is Dictionary:
				npc_panel._present(int(current.id), response.body, auto)
				links = person_links(int(current.id), response.body)
			text = "" if response.ok else "Não foi possível ler esta pessoa (erro %d)." % int(response.status)
		Registry.CITY:
			await _show_city(String(current.id))
			if ticket != _request:
				return
			text = ""
		_:
			var built := describe(current)
			text = built.text
			links = built.links
	_title.text = registry.title_of(current) if kind != Registry.PERSON else ""
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
	if detail.get("city") is Dictionary:
		links.append(["Cidade", Registry.ref(Registry.CITY, String(detail.city.value))])
	return links


## Ficha das entidades que a store já descreve inteiras: {text, links}.
func describe(entity: Dictionary) -> Dictionary:
	var id = entity.id
	match String(entity.kind):
		Registry.BUILDING:
			return _describe_building(int(id))
		Registry.PROCESS:
			return _describe_process(int(id))
		Registry.OBJECT:
			return _describe_object(String(id))
		Registry.WORKPLACE:
			return {"text": "Quem trabalha aqui (das fichas já abertas):%s\nO nome do local ainda não vem do servidor." % (
				"" if not registry.known_workers(int(id)).is_empty() else " ninguém conhecido"),
				"links": _people("Trabalhador(a)", registry.known_workers(int(id)))}
		Registry.HOUSEHOLD:
			return {"text": "Moradores conhecidos (das fichas já abertas):%s\nO prédio desta casa ainda não vem do servidor." % (
				"" if not registry.known_household(int(id)).is_empty() else " ninguém conhecido"),
				"links": _people("Morador(a)", registry.known_household(int(id)))}
		Registry.EVENT:
			var event: Dictionary = registry.events.get(int(id), {})
			return {"text": "%s\n%s · tipo %d" % [String(event.get("label", "Evento")), date_of.call(int(event.get("tick", 0))),
				int(event.get("kind", 0))], "links": []}
	return {"text": "", "links": []}


func _people(label: String, ids: Array) -> Array:
	var out: Array = []
	for npc_id in ids:
		out.append([label, Registry.ref(Registry.PERSON, int(npc_id))])
	return out


func _describe_building(id: int) -> Dictionary:
	var building = store.buildings.get(id)
	if building == null:
		return {"text": "Este prédio não está no escopo atual (ou foi demolido).", "links": []}
	var lines: Array[String] = ["Edifício %d · %d andar%s · célula %d,%d" % [id, int(building.floors),
		"es" if int(building.floors) > 1 else "", building.cell.x, building.cell.y]]
	var lodging = building.get("lodging")
	if lodging is Dictionary:
		lines.append("Hospedagem: %d/%d vagas · aluguel %d/mês%s" % [int(lodging.occupied), int(lodging.units), int(lodging.rentPerMonth),
			(" · diária %d" % int(lodging.nightlyRate)) if int(lodging.nightlyRate) > 0 else ""])
	if int(building.get("value", 0)) > 0:
		lines.append("Valor estimado: %d" % int(building.value))
	if store.pending_footprints.has(id):
		lines.append("Recém-construído: a planta chega com o próximo retrato da cidade.")
	var links: Array = []
	if int(store.interior.get("building", -1)) == id:
		lines.append("Quem está dentro agora:")
		for occupant in store.interior.get("occupants", []):
			links.append(["Dentro", Registry.ref(Registry.PERSON, int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc))])
	else:
		lines.append("Dono e moradores ainda não vêm do servidor. Entre (duplo clique) para ver quem está dentro.")
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
			lines.append("Lavoura nº %d (estágio e cultura ainda não vêm do servidor)" % int(process.target))
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
