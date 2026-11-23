extends RefCounted
## Ficha da obra (AD-193): quem ergue, para quem, o que falta e por que parou, tudo do que o motor projeta (`works` do escopo ou
## `GET /entities/work/{id}` → `state.work`). A mesma ficha aparece no canteiro (o prédio que ainda é obra). Só leitura.

const Registry := preload("res://Store/entity_registry.gd")
const Works := preload("res://Store/work_book.gd")


## A obra da store, ou a que a rota de inspeção trouxe (obra de outra cidade); {} se nenhuma.
static func work_of(store, id: int, server_body: Variant = null) -> Dictionary:
	var known = store.works.get(id)
	if known != null:
		return known
	var state = server_body.get("state") if server_body is Dictionary else null
	return Works.parse(state.work) if state is Dictionary and state.get("work") is Dictionary else {}


## {text, links} da obra. `hours_per_day` converte horas em dias para "o que falta".
static func describe(store, registry, work: Dictionary, hours_per_day: float, date_of: Callable) -> Dictionary:
	if work.is_empty():
		return {"text": "Esta obra acabou ou foi abandonada.", "links": []}
	var lines: Array[String] = []
	var links: Array = []
	lines.append("Obra nº %d: %s · %d%% feita" % [int(work.id), Works.what(work), int(round(float(work.progress) * 100.0))])
	match String(work.payer):
		"casa":
			lines.append("Para a família que encomendou (%s)" % Works.PAYER_TEXT.casa)
		"fundador":
			lines.append("Para a oficina de quem empreende (%s)" % Works.PAYER_TEXT.fundador)
		"cidade":
			lines.append("Para a cidade (%s)" % Works.PAYER_TEXT.cidade)
	if int(work.owner_id) >= 0:
		links.append(["Encomendou", Registry.ref(Registry.PERSON, int(work.owner_id))])
	if int(work.household_id) >= 0:
		links.append(["Casa que encomendou", Registry.ref(Registry.HOUSEHOLD, int(work.household_id))])
	var left := "Faltam %s de obra" % _hours(int(work.hours_left), hours_per_day)
	if int(work.haul_left) > 0:
		left += " e %s de carregar o material até o lote" % _hours(int(work.haul_left), hours_per_day)
	lines.append(left)
	var halt := String(work.get("halt", ""))
	if halt != "":
		lines.append("Parada: %s" % String(Works.HALT_TEXT.get(halt, halt)))
	lines.append("Quem ergue: %d diarista(s) pago(s) (jornal a %d%% do dia), %d ajudante(s) sem jornal (família, parentes, vizinhos)" % [
		work.crew.size(), int(round(int(work.offer_pm) / 10.0)), work.helpers.size()])
	if work.crew.is_empty() and work.helpers.is_empty():
		lines.append("Ninguém na turma agora.")
	var here: Array = work.on_site
	lines.append("No lote agora: %s" % (", ".join(here.map(func(n): return registry.person_name(int(n)))) if not here.is_empty() else "ninguém"))
	for n in here:
		links.append(["No lote agora", Registry.ref(Registry.PERSON, int(n))])
	for n in work.crew:
		links.append(["Diarista", Registry.ref(Registry.PERSON, int(n))])
	for n in work.helpers:
		links.append(["Ajudante", Registry.ref(Registry.PERSON, int(n))])
	lines.append("Horas feitas: %d pela turma paga, %d pelos ajudantes · pago aos diaristas: %d" % [int(work.hours_crew),
		int(work.hours_helpers), int(work.paid)])
	if int(work.spent) > 0:
		lines.append("Gasto ao abrir (material comprado, foro, equipamento): %d" % int(work.spent))
	for entry in work.materials:
		lines.append("Material: %s %d de %d já posto" % [String(entry.name) if String(entry.name) != "" else "recurso %d" % int(entry.resource),
			int(entry.placed), int(entry.needed)])
	if int(work.started) > 0:
		lines.append("Começou: %s · última hora de trabalho: %s" % [date_of.call(int(work.started)), date_of.call(int(work.last_worked))])
	if int(work.building_id) >= 0:
		links.append(["Canteiro", Registry.ref(Registry.BUILDING, int(work.building_id))])
	return {"text": "\n".join(lines), "links": links}


## Horas-homem de obra (a turma soma as horas de cada um): ditas em horas, não em dias do calendário.
static func _hours(hours: int, _per_day: float) -> String:
	return "%d h" % hours
