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
