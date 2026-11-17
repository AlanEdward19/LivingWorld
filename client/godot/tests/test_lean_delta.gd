extends "res://tests/test_base.gd"
## Delta enxuto do servidor (AD-172, seção "Delta enxuto" de docs/domain/observable-projection.md): o que a store mantém
## sozinha num NPC já conhecido e os processos `rest`/`food` que avançam pelo relógio sem reenvio.

const O := preload("res://tests/observable_fixtures.gd")
const Store := preload("res://Store/world_store.gd")


func _known_store() -> Store:
	var store := Store.new()
	var env := O.city()
	env.payload.livingState.npcs[0]["appearance"] = {"skinTone": 20, "hairTone": 40, "hairCurl": 10, "build": 50}
	env.payload.livingState.npcs[0]["actionTarget"] = {"kind": "workplace", "id": "4"}
	env.payload.livingState.npcs[0]["intent"] = 2
	env.payload.livingState.npcs[0]["intentTarget"] = "w4"
	store.apply_snapshot(env)
	return store


func test_city_null_appearance_absent_and_observable_absent_keep_the_previous() -> void:
	var store := _known_store()
	eq(store.npcs[7].city, "city-a")
	store.apply_delta(O.delta({"npcUpserts": [{"id": {"value": 7}, "city": null, "location": {"x": 4, "y": 4}, "currentAction": 2}]}))
	eq(store.npcs[7].city, "city-a", "regra 1: city null = mesma cidade")
	eq(store.npcs[7].look.skin_tone, 20, "regra 2: appearance ausente = a anterior")
	eq(store.npcs[7].obs.name, "Ana", "regra 4: observable ausente = não mudou")
	eq(store.npcs[7].obs.pregnancy, 0.6)
	store.apply_delta(O.delta({"npcUpserts": [{"id": {"value": 7}, "city": null, "location": {"x": 4, "y": 4}, "currentAction": 2,
		"appearance": {"skinTone": 80, "hairTone": 40, "hairCurl": 10, "build": 50}}]}))
	eq(store.npcs[7].look.skin_tone, 80, "appearance presente troca")


func test_action_group_is_kept_without_started_tick_and_replaced_whole_with_it() -> void:
	var store := _known_store()
	store.apply_delta(O.delta({"npcUpserts": [{"id": {"value": 7}, "city": null, "location": {"x": 4, "y": 4}, "currentAction": 2}]}))
	eq(store.npcs[7].started, 3, "regra 3: actionStartedAtTick ausente = grupo anterior")
	eq(store.npcs[7].target, {"kind": "workplace", "id": "4"})
	eq(store.npcs[7].intent, 2)
	eq(store.npcs[7].intent_target, "w4")
	store.apply_delta(O.delta({"npcUpserts": [{"id": {"value": 7}, "city": null, "location": {"x": 4, "y": 4}, "currentAction": 5,
		"actionStartedAtTick": 20}]}))
	eq(store.npcs[7].started, 20, "presente = grupo inteiro novo")
	eq(store.npcs[7].target, {}, "alvo ausente com grupo novo = nulo")
	eq(store.npcs[7].intent, -1)
	eq(store.npcs[7].intent_target, "")


func test_removed_and_upserted_in_the_same_delta_restarts_the_npc_from_the_upsert() -> void:
	var store := _known_store()
	eq(store.npcs[7].obs.pregnancy, 0.6)
	# AD-172: campo que precisa voltar a nulo vem com o NPC em npcRemoved e em npcUpserts; o que se sabia é descartado
	store.apply_delta(O.delta({"npcRemoved": [{"value": 7}],
		"npcUpserts": [{"id": {"value": 7}, "location": {"x": 4, "y": 4}, "currentAction": 2}]}))
	check(store.npcs.has(7), "o NPC continua no escopo")
	eq(store.npcs[7].city, "", "cidade ausente no recomeço = nula, não a anterior")
	eq(store.npcs[7].look, {}, "aparência ausente no recomeço = nula")
	eq(store.npcs[7].target, {}, "alvo ausente no recomeço = nulo")


func test_rest_and_food_advance_by_the_delta_clock_when_not_resent() -> void:
	var store := Store.new()
	var env := O.city()
	env.cursor.tick = 10
	env.payload.livingState.processes = [
		{"id": 60, "kind": "rest", "targetId": 7, "progress": 0.5, "descriptorKey": "rest", "location": {"x": 3, "y": 3},
			"remainingHours": 4, "quality": null},
		{"id": 61, "kind": "food", "targetId": 8, "progress": 0.0, "descriptorKey": "food", "location": {"x": 4, "y": 3},
			"remainingHours": 10, "quality": null},
		{"id": 62, "kind": "construction", "targetId": 3, "progress": 0.2, "descriptorKey": "construction",
			"location": {"x": 1, "y": 1}, "remainingHours": 40, "quality": null}]
	store.apply_snapshot(env)
	eq(store.last_tick, 10, "tick do snapshot guardado")
	var changed: Array = []
	store.processes_changed.connect(func(): changed.append(1))
	var delta := O.delta({"processUpserts": [{"id": 61, "kind": "food", "targetId": 8, "progress": 0.5, "descriptorKey": "food",
		"location": {"x": 4, "y": 3}, "remainingHours": 5, "quality": null}]})
	delta.payload.tick = 12
	store.apply_delta(delta)
	eq(float(store.processes[60].remaining), 2.0, "regra 5: duração 8 h, passaram 2: faltam 2")
	eq(float(store.processes[60].progress), 0.75)
	eq(float(store.processes[61].remaining), 5.0, "o que veio no upsert vale como veio")
	eq(float(store.processes[62].progress), 0.2, "outros tipos ficam como estão")
	eq(store.last_tick, 12, "tick do delta guardado")
	eq(changed.size(), 1)
	var later := O.delta({})
	later.payload.tick = 20
	store.apply_delta(later)
	eq(float(store.processes[60].remaining), 0.0, "nunca negativo")
	eq(float(store.processes[60].progress), 1.0)
