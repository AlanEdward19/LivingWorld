extends RefCounted
## Texto e links da ficha de cada tipo (ADR-0047 etapa 3), montados do que a store já tem (projeção do observável) e,
## quando veio, da rota de inspeção `GET /entities/{kind}/{id}` (nome, vínculos e história recente). Crime só de registro
## mostra o registro estruturado, nunca o texto narrado. Só leitura.

const Registry := preload("res://Store/entity_registry.gd")
const Parse := preload("res://Store/observable_parse.gd")
const Look := preload("res://Scenes/npc_look.gd")

const FUNCTION_NAMES := {"dwelling": "moradia", "lodging": "hospedagem", "workplace": "local de trabalho", "structure": "estrutura"}
const Style := preload("res://Scenes/render/street_style.gd")


static func _person(label: String, id: int) -> Array:
	return [label, Registry.ref(Registry.PERSON, id)]


static func _people(label: String, ids: Array) -> Array:
	var out: Array = []
	for id in ids:
		out.append(_person(label, int(id)))
	return out


## Prédio: função, tipo de local, dono, moradores, trabalhadores, estoque e despensa (estado do AD-170).
static func building_state(store, registry, id: int) -> Dictionary:
	var state: Dictionary = store.building_states.get(id, {})
	if state.is_empty():
		return {"lines": [], "links": []}
	var functions: Array = Array(String(state.function).split(",", false))
	var lines: Array[String] = ["Função: %s%s" % [", ".join(functions.map(func(f): return FUNCTION_NAMES.get(f, f))),
		(" (%s)" % state.location_type_name) if String(state.location_type_name) != "" else ""]]
	if bool(state.closed):
		lines.append("Fechado")
	var links: Array = []
	if int(state.owner_id) >= 0:
		links.append(_person("Dono(a)", int(state.owner_id)))
	if int(state.workplace_id) >= 0:
		links.append(["Local de trabalho", Registry.ref(Registry.WORKPLACE, int(state.workplace_id))])
	for household in state.households:
		links.append(["Casa", Registry.ref(Registry.HOUSEHOLD, int(household))])
	if not state.residents.is_empty():
		lines.append("Moradores: %d" % state.residents.size())
	links.append_array(_people("Morador(a)", state.residents))
	if not state.workers.is_empty():
		lines.append("Trabalhadores: %d" % state.workers.size())
	links.append_array(_people("Trabalhador(a)", state.workers))
	if not state.stock.is_empty():
		lines.append("Estoque: %s" % ", ".join(state.stock.map(func(line): return "%d %s" % [int(line.quantity),
			String(line.name) if String(line.name) != "" else String(registry.resources.get(int(line.resource_id), "recurso %d" % int(line.resource_id)))])))
	if int(state.pantry) > 0:
		lines.append("Despensa da casa: %d unidades" % int(state.pantry))
	return {"lines": lines, "links": links}


static func crop(store, registry, id: int) -> Dictionary:
	var crop = store.crops.get(id)
	if crop == null:
		return {"text": "Esta lavoura não está no escopo atual.", "links": []}
	var lines: Array[String] = ["%s · %s%s" % [registry.title_of(Registry.ref(Registry.CROP, id)),
		Registry.STAGE_NAMES.get(String(crop.stage), String(crop.stage)),
		(" %d%%" % roundi(float(crop.fraction) * 100.0)) if String(crop.stage) in ["planted", "growing"] else ""]]
	if bool(crop.thirsty):
		lines.append("Com sede (água entregue abaixo do que precisa)")
	lines.append("Semeado %.2f de %.2f ha aráveis" % [float(crop.sown), float(crop.arable)])
	if String(crop.stage) == "mature":
		lines.append("Grão em pé: %d" % int(crop.standing))
	lines.append("Colheita esperada: %d" % int(crop.expected))
	lines.append("Célula %d,%d" % [crop.cell.x, crop.cell.y])
	var links: Array = []
	if int(crop.owner_id) >= 0:
		links.append(_person("Dono(a)", int(crop.owner_id)))
	if int(crop.farm_id) >= 0:
		links.append(["Fazenda", Registry.ref(Registry.WORKPLACE, int(crop.farm_id))])
	return {"text": "\n".join(lines), "links": links}


static func animal(store, id: int) -> Dictionary:
	var animal = store.animals.get(id)
	if animal == null:
		return {"text": "Este animal saiu do escopo (ou morreu).", "links": []}
	var lines: Array[String] = ["%s · energia %d" % [Registry.species_name(String(animal.species)), int(animal.energy)]]
	if bool(animal.disease):
		lines.append("Carrega doença")
	if String(animal.predator_of) != "":
		lines.append("Caça: %s" % Registry.species_name(String(animal.predator_of)))
	lines.append("Célula %d,%d" % [animal.cell.x, animal.cell.y])
	return {"text": "\n".join(lines), "links": []}


static func fauna(store, key: String) -> Dictionary:
	var group = store.fauna_groups.get(key)
	if group == null:
		return {"text": "Este bando não está mais no mapa.", "links": []}
	return {"text": "%d × %s na região %d\nCentro %d,%d" % [int(group.count), Registry.species_name(String(group.species)),
		int(group.region), group.cell.x, group.cell.y], "links": []}


static func leaving(store, registry, id: int) -> Dictionary:
	var item = store.leavings.get(id)
	if item == null:
		return {"text": "Este espólio já foi recolhido.", "links": []}
	var lines: Array[String] = ["%d unidades no chão desde %s" % [int(item.units), "tick %d" % int(item.tick)]]
	var links: Array = []
	if int(item.dead_id) >= 0:
		links.append(_person("De quem era", int(item.dead_id)))
	if int(item.household_id) >= 0:
		links.append(["Casa", Registry.ref(Registry.HOUSEHOLD, int(item.household_id))])
	if int(item.heir_household_id) >= 0:
		links.append(["Casa herdeira", Registry.ref(Registry.HOUSEHOLD, int(item.heir_household_id))])
	if int(item.fetcher_id) >= 0:
		links.append(_person("Foi buscar", int(item.fetcher_id)))
	if int(item.carrier_id) >= 0:
		links.append(_person("Está levando", int(item.carrier_id)))
	if int(item.item_id) >= 0:
		lines.append("Recipiente: peça nº %d (design %d)" % [int(item.item_id), int(item.design_id)])
		links.append(["Objeto", Registry.ref(Registry.ITEM, int(item.item_id))])
	return {"text": "\n".join(lines), "links": links}


## Peça inventada: dono, quem leva, onde, à venda, qualidade e, do design (`/designs/{id}`), partes, materiais e função.
static func item(store, id: int, design: Variant) -> Dictionary:
	var object = store.objects.get(id)
	var lines: Array[String] = []
	var links: Array = []
	if object != null:
		lines.append("Peça nº %d · design %d · qualidade %d%%" % [id, int(object.design_id), roundi(float(object.quality) * 100.0)])
		lines.append("Forma da peça raiz: %s · material %d" % [String(object.shape), int(object.material)])
		if bool(object.for_sale):
			lines.append("À venda")
		if int(object.holder_id) >= 0:
			links.append(_person("Com", int(object.holder_id)))
		if int(object.household_id) >= 0:
			links.append(["Casa dona", Registry.ref(Registry.HOUSEHOLD, int(object.household_id))])
		if int(object.market_id) >= 0:
			links.append(["Mercado", Registry.ref(Registry.WORKPLACE, int(object.market_id))])
	if design is Dictionary:
		lines.append("Design: %d partes · %s · %s" % [design.get("parts", []).size(), String(design.get("function", "")),
			"funciona" if bool(design.get("works", false)) else "não funciona (%s)" % String(design.get("failure", ""))])
		var colors: Array = design.get("materials", []).map(func(m): return String(m.get("color", "")))
		if not colors.is_empty():
			lines.append("Materiais: %s" % ", ".join(colors))
		if int(design.get("inventorId", -1)) >= 0:
			links.append(_person("Inventor(a)", int(design.inventorId)))
	if lines.is_empty():
		lines.append("Esta peça não está no escopo atual.")
	return {"text": "\n".join(lines), "links": links}


## Evento: título (registro só de crime nunca mostra o texto narrado), data, desfecho e envolvidos como links.
static func event(registry, id: int, date_of: Callable) -> Dictionary:
	var event: Dictionary = registry.events.get(id, {})
	if event.is_empty():
		return {"text": "Evento", "links": []}
	var lines: Array[String] = [registry.event_title(event), "%s · tipo %d" % [date_of.call(int(event.tick)), int(event.kind)]]
	var involved: Dictionary = event.get("involved", {})
	if bool(event.record_only):
		lines.append("Só registro: sem cena nem descrição (autor, vítima, caso e desfecho abaixo).")
	elif involved.has("caseId") or involved.has("victimId"):
		lines.append("Crime: o fato, sem cena.")
	if involved.has("outcome") and not bool(event.record_only):
		lines.append("Desfecho: %s" % String(involved.outcome))
	if involved.has("amount"):
		lines.append("Valor: %d" % int(involved.amount))
	if not bool(event.has_cell):
		lines.append("Sem lugar no mapa.")
	return {"text": "\n".join(lines), "links": registry.event_links(event)}


## História recente de `GET /entities` (cada evento passa pela regra do registro: crime só de registro sem texto).
static func recent_lines(registry, body: Variant, date_of: Callable, limit: int = 6) -> Array[String]:
	var lines: Array[String] = []
	if not body is Dictionary:
		return lines
	var recent: Array = body.get("recentEvents", []) if body.get("recentEvents") is Array else []
	for i in range(recent.size() - 1, maxi(recent.size() - 1 - limit, -1), -1):
		var event := Parse.event(recent[i], 0)
		lines.append("• %s · %s" % [date_of.call(int(event.tick)), registry.event_title(event)])
	return lines


const RELATIONS := {"mother": "Mãe", "father": "Pai", "spouse": "Cônjuge", "child": "Filho(a)", "household": "Casa",
	"dwelling": "Moradia", "workplace": "Local de trabalho", "employer": "Trabalho", "employee": "Trabalhador(a)",
	"worker": "Trabalhador(a)", "owner": "Dono(a)", "owns": "É dono(a) de", "resident": "Morador(a)", "member": "Morador(a)",
	"head": "Chefe", "building": "Prédio", "city": "Cidade", "farm": "Fazenda", "market": "Mercado", "object": "Objeto",
	"design": "Design", "holder": "Com", "dead": "De quem era", "fetcher": "Foi buscar", "carrier": "Está levando"}
const MAX_SERVER_LINKS := 12 ## a ficha da cidade lista todos os prédios: os primeiros bastam (o resto é clicável no mapa)


## Links da rota de inspeção (relação, tipo, id, nome) no formato do inspetor; tipos que o cliente não abre ficam fora.
static func server_links(body: Variant) -> Array:
	var out: Array = []
	if not body is Dictionary:
		return out
	for link in body.get("links", []) if body.get("links") is Array else []:
		var kind := String(Registry.LOCAL_KINDS.get(String(link.get("kind", "")), ""))
		if kind == "" or link.get("id") == null:
			continue
		var relation := String(link.get("relation", ""))
		out.append([String(RELATIONS.get(relation, relation.capitalize())), Registry.ref(kind, Registry.id_for(kind, link.id))])
		if out.size() >= MAX_SERVER_LINKS:
			break
	return out


## Junta links sem repetir a mesma entidade (a primeira relação vence).
static func merge_links(first: Array, second: Array) -> Array:
	var seen := {}
	var out: Array = []
	for link in first + second:
		var k := Registry.key(link[1])
		if not seen.has(k):
			seen[k] = true
			out.append(link)
	return out


## Local de trabalho: o servidor dá nome, dono e trabalhadores (links); sem a rota, quem já teve a ficha aberta.
static func workplace(store, registry, id: int) -> Dictionary:
	var lines: Array[String] = []
	var links: Array = _people("Trabalhador(a)", registry.known_workers(id))
	for state in store.building_states.values():
		if int(state.workplace_id) == id:
			links.push_front(["Prédio", Registry.ref(Registry.BUILDING, int(state.building_id))])
			var built := building_state(store, registry, int(state.building_id))
			lines.append_array(built.lines)
			links = merge_links(built.links, links)
	if lines.is_empty():
		lines.append("Quem trabalha aqui (das fichas já abertas):%s" % ("" if not links.is_empty() else " ninguém conhecido"))
	return {"text": "\n".join(lines), "links": links}


## Casa (família): moradores, prédio e estoque pela rota de inspeção; sem ela, os moradores já conhecidos.
static func household(store, registry, id: int, server_body: Variant) -> Dictionary:
	var links: Array = []
	if registry.household_buildings.has(id):
		links.append(["Prédio da casa", Registry.ref(Registry.BUILDING, int(registry.household_buildings[id]))])
	var lines: Array[String] = []
	var state = server_body.get("state") if server_body is Dictionary else null
	if state is Dictionary:
		var members: Array = state.get("memberIds", [])
		lines.append("%d morador%s%s" % [members.size(), "es" if members.size() != 1 else "", " · sem teto" if bool(state.get("homeless", false)) else ""])
		var stock: Array = state.get("stock", []) if state.get("stock") is Array else []
		if not stock.is_empty():
			lines.append("Estoque: %s" % ", ".join(stock.map(func(line): return "%d %s" % [int(line.get("quantity", 0)),
				String(registry.resources.get(int(line.get("resourceId", -1)), "recurso %d" % int(line.get("resourceId", -1))))])))
		if state.get("dwellingId") != null and int(state.dwellingId) >= 0:
			links.append(["Prédio da casa", Registry.ref(Registry.BUILDING, int(state.dwellingId))])
		if state.get("headId") != null:
			links.append(["Chefe", Registry.ref(Registry.PERSON, int(state.headId))])
		links.append_array(_people("Morador(a)", members))
	else:
		lines.append("Moradores conhecidos (das fichas já abertas):%s" % ("" if not registry.known_household(id).is_empty() else " ninguém conhecido"))
		links.append_array(_people("Morador(a)", registry.known_household(id)))
	return {"text": "\n".join(lines), "links": merge_links(links, [])}


## Rua (camada `Roads`, AD-177): superfície, quantas pessoas usam, principal/largo, e os prédios cuja porta dá nela.
static func street(store, id: String) -> Dictionary:
	var cell := Registry.street_cell(id)
	var road = store.roads.get(cell)
	if road == null:
		return {"text": "Esta rua não está na camada de ruas do motor agora.", "links": []}
	var links: Array = []
	if String(road.city) != "":
		links.append(["Cidade", Registry.ref(Registry.CITY, String(road.city))])
	for building_id in store.buildings:
		var parcel: Dictionary = store.buildings[building_id].get("lot", {})
		if parcel.get("street") == cell:
			links.append(["Porta dá aqui", Registry.ref(Registry.BUILDING, int(building_id))])
	return {"text": "\n".join(Style.lines_of(road)), "links": links}


## Lote do prédio como o motor o mandou: quintal (tamanho), caminho da porta, rua da frente e parede-meia.
static func lot(store, id: int) -> Dictionary:
	var building: Dictionary = store.buildings.get(id, {})
	var parcel: Dictionary = building.get("lot", {})
	var lines: Array[String] = []
	var links: Array = []
	if parcel.is_empty():
		lines.append("Lote: o motor não mandou lote deste prédio")
	else:
		var croft := int(parcel.croft.size())
		lines.append("Quintal: %d célula%s, cercado" % [croft, "" if croft == 1 else "s"] if croft > 0 else "Sem quintal")
		var street = parcel.street
		if street == null:
			lines.append("Não dá para nenhuma rua")
		else:
			var road = store.roads.get(street)
			lines.append("Dá para a rua em %d,%d%s%s" % [street.x, street.y,
				(" (%s)" % Style.title_of(road).to_lower()) if road != null else "",
				(" por um caminho de %d célula%s" % [parcel.front_path.size(), "" if parcel.front_path.size() == 1 else "s"])
					if not parcel.front_path.is_empty() else ", porta direto na rua"])
			if road != null:
				links.append(["Rua da frente", Registry.ref(Registry.STREET, "%d,%d" % [street.x, street.y])])
	var shared := party_walls(store, id)
	if not shared.is_empty():
		lines.append("Parede-meia com %d prédio%s" % [shared.size(), "" if shared.size() == 1 else "s"])
		for other in shared:
			links.append(["Parede-meia", Registry.ref(Registry.BUILDING, int(other))])
	return {"lines": lines, "links": links}


## Prédios cuja planta encosta na deste (lado a lado, sem vão): parede-meia, pelo que o motor pôs em cada célula.
static func party_walls(store, id: int) -> Array:
	var building: Dictionary = store.buildings.get(id, {})
	var mine := {}
	for entry in building.get("footprint", []):
		mine[building.cell + entry.cell] = true
	var out: Array = []
	for other_id in store.buildings:
		if int(other_id) == id:
			continue
		var other: Dictionary = store.buildings[other_id]
		for entry in other.footprint:
			var cell: Vector2i = other.cell + entry.cell
			if mine.has(cell + Vector2i(1, 0)) or mine.has(cell + Vector2i(-1, 0)) or mine.has(cell + Vector2i(0, 1)) or mine.has(cell + Vector2i(0, -1)):
				out.append(int(other_id))
				break
	out.sort()
	return out
