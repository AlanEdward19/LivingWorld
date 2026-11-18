extends RefCounted
## Ruas do escopo por snapshot e delta (AD-181). O motor manda `livingState.roads` no retrato dos escopos Cidade e Mundo e,
## a cada tick, `roadUpserts` (registro inteiro, chave = `cell`) e `roadRemoved` (células). Aqui só se aplica isso à
## `WorldStore.roads`; servidor antigo (sem `livingState.roads`) cai na camada `Roads` e na releitura de `road_sync.gd`.
## Também guarda onde acabou a última obra de cada cidade: o evento `StreetWorkDone` não traz lugar (lacuna do contrato).

const Ground := preload("res://Store/city_ground.gd")

## Tipos de evento da obra de rua (`WorldEventKind`: o contrato manda o número). Sem lugar no evento: o marcador vai para as
## células da obra que o próprio motor mandou nas ruas.
const STREET_WORK_STARTED := 101
const STREET_WORK_DONE := 102


## Retrato: `livingState.roads` quando veio (lista, mesmo vazia) -> {roads, live: true}; senão a camada `Roads` (live: false).
static func from_snapshot(state: Variant, layer: Variant = null) -> Dictionary:
	if state is Dictionary and state.get("roads") is Array:
		return {"roads": Ground.road_list(state.roads), "live": true}
	return {"roads": Ground.roads(layer), "live": false}


## Delta: troca as células de `roadUpserts` e apaga as de `roadRemoved`. Devolve {changed, finished: {cidade: [células]}}:
## `finished` = células que tinham obra e deixaram de ter neste delta (onde a obra acabou).
static func apply_delta(roads: Dictionary, upserts: Array, removed: Array) -> Dictionary:
	var changed := false
	var finished := {}
	for raw in removed:
		var cell = Ground._cell(raw)
		if cell != null and roads.has(cell):
			_note_finished(finished, roads[cell], {})
			roads.erase(cell)
			changed = true
	for raw in upserts:
		var parsed = Ground.road(raw)
		if parsed == null:
			continue
		var before = roads.get(parsed.cell)
		if before is Dictionary:
			_note_finished(finished, before, parsed)
		if before != parsed:
			roads[parsed.cell] = parsed
			changed = changed or not _looks_same(before, parsed)
	return {"changed": changed, "finished": finished}


## Só a umidade (`wetPm`) mudou: o desenho é o mesmo (o chão é o `ground`), então a malha de ruas não é refeita. Com o tempo
## ligado o motor reenvia a célula a cada tick enquanto o chão seca ou molha (AD-188, medida com o mundo rodando).
static func _looks_same(before: Variant, after: Dictionary) -> bool:
	if not before is Dictionary:
		return false
	var a: Dictionary = before.duplicate()
	a.wet_pm = after.get("wet_pm", 0)
	return a == after


static func _note_finished(finished: Dictionary, before: Dictionary, after: Dictionary) -> void:
	if not before.get("work", {}).is_empty() and after.get("work", {}).is_empty():
		(finished.get_or_add(String(before.city), []) as Array).append(before.cell)


## Reserva do traçado sem uso: célula planejada sem pisoteio, sem gente, sem obra e sem calçamento. Não é rua: só um traço
## discreto na planta (P).
static func is_reserve(road: Dictionary) -> bool:
	return bool(road.get("planned", false)) and int(road.get("wear", 0)) <= 0 and int(road.get("users", 0)) <= 0 \
		and road.get("work", {}).is_empty() and int(road.get("condition", -1)) < 0


## Células com obra de calçamento (de uma cidade, ou de todas com `city_id` vazio).
static func work_cells(roads: Dictionary, city_id := "") -> Array:
	var out: Array = []
	for cell in roads:
		var road: Dictionary = roads[cell]
		if not road.get("work", {}).is_empty() and (city_id == "" or String(road.city) == city_id):
			out.append(cell)
	return out


## Célula do meio de um conjunto (a mais perto do centro, para cair numa célula da obra de verdade); null se vazio.
static func middle_of(cells: Array) -> Variant:
	if cells.is_empty():
		return null
	var sum := Vector2.ZERO
	for cell in cells:
		sum += Vector2(cell)
	var center := sum / cells.size()
	var best: Vector2i = cells[0]
	for cell in cells:
		if Vector2(cell).distance_squared_to(center) < Vector2(best).distance_squared_to(center):
			best = cell
	return best


## Evento de obra de rua sem lugar: põe o lugar nas células da obra (começo: as que têm obra agora; fim: onde acabou).
## Devolve o evento (lido por `Parse.event`) com `cell`/`has_cell` preenchidos quando dá.
static func place_event(event: Dictionary, roads: Dictionary, finished: Dictionary) -> Dictionary:
	var kind := int(event.get("kind", -1))
	if bool(event.get("has_cell", false)) or not kind in [STREET_WORK_STARTED, STREET_WORK_DONE]:
		return event
	var city := String(event.get("involved", {}).get("cityId", ""))
	var cells: Array = work_cells(roads, city) if kind == STREET_WORK_STARTED else finished.get(city, [])
	if cells.is_empty() and kind == STREET_WORK_DONE:
		for list in finished.values():
			cells.append_array(list)
	var cell = middle_of(cells)
	if cell != null:
		event.cell = cell
		event.has_cell = true
	return event
