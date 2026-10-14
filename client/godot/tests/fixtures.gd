extends RefCounted
## Envelopes mínimos com a mesma forma que o servidor serializa (camelCase).


static func cursor(seq: int, kind: String = "world", tick: int = 10) -> Dictionary:
	return {"tick": tick, "scopeKey": kind, "sequence": seq}


static func snapshot(seq: int = 0, payload: Variant = null, kind: String = "World") -> Dictionary:
	return {
		"scope": {"kind": kind, "refId": ""}, "mode": "Spectator", "cursor": cursor(seq),
		"activeLayers": [], "payload": payload if payload != null else {},
	}


static func empty_delta(tick: int) -> Dictionary:
	return {
		"tick": tick, "npcUpserts": [], "npcRemoved": [], "cityUpserts": [], "cityRemoved": [],
		"buildingUpserts": [], "buildingRemoved": [], "processUpserts": [], "processRemoved": [],
		"indicators": [], "events": [],
	}


static func delta(from_seq: int, to_seq: int, payload: Dictionary = {}) -> Dictionary:
	var body := empty_delta(10 + to_seq)
	body.merge(payload, true)
	return {
		"scope": {"kind": "World", "refId": ""}, "fromCursor": cursor(from_seq),
		"toCursor": cursor(to_seq), "payload": body,
	}


static func sequences(envelopes: Array) -> Array:
	return envelopes.map(func(e): return int(e.toCursor.sequence))


## Uma decisão do rastro de cognição, na forma de DecisionTrace (enums numéricos, como o servidor serializa).
static func trace_entry(tick: int, winner: int, pressures: int = 2, opportunities: int = 1, alternatives: int = 2) -> Dictionary:
	var pressure_list: Array = []
	for i in pressures:
		pressure_list.append({"kind": "Pressao%d" % i, "intensity": 80.0 - i * 20.0, "factors": ["Fome"]})
	var opportunity_list: Array = []
	for i in opportunities:
		opportunity_list.append({"kind": "Oportunidade%d" % i, "attractiveness": 60.0, "detail": null})
	var alternative_list: Array = []
	for i in alternatives:
		alternative_list.append(i + 2)
	return {"tick": tick, "trace": {
		"wakeReason": 1, "previousIntent": 2, "topPressures": pressure_list, "knownOpportunities": opportunity_list,
		"winner": winner, "winningUtility": 120.5, "topPositiveFactors": ["Fome"], "topNegativeFactors": ["Distancia"],
		"blockingFactors": ["Buy:outscored"], "knownAlternatives": alternative_list}}


## Ficha de NPC (GET /npcs/{id}) com o rastro dado, em ordem cronológica.
static func npc_inspect(trace: Array, lod: int = 0) -> Dictionary:
	return {"status": 200, "body": {
		"name": "Ana", "ageYears": 31, "health": 90, "hunger": 80, "thirst": 70, "sleep": 60, "social": 50,
		"lod": lod, "cognitionTrace": trace}}
