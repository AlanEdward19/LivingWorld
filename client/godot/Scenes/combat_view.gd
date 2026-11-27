extends RefCounted
## #234: o combate do corpo conduzido, lido do motor (`pose.combat` e a resposta do golpe). Só guarda o que o motor disse:
## recarga do golpe, guarda e recarga dela, alcance e o último golpe recebido. O relógio é o do cliente (ms), então os
## testes passam `now_ms`. Nenhuma regra de combate mora aqui: quem decide acerto, esquiva e dano é o motor.

const FRESH_MS := 1500 ## um golpe recebido mais velho que isto, visto pela primeira vez, não é animado (é história)
const SAME_MS := 400 ## o mesmo golpe visto em duas leituras difere só pela latência

var reach_m := 0.0 ## alcance do golpe em metros (`reachMeters`); 0 = o motor ainda não disse
var _strike_at := 0 ## ms do cliente em que o golpe volta a estar pronto
var guard := "" ## "Dodge" | "Block" enquanto a janela vale
var _guard_until := 0
var _guard_ready_at := 0
var _last_key := ""
var _last_at := -1_000_000


func reset() -> void:
	reach_m = 0.0
	_strike_at = 0
	guard = ""
	_guard_until = 0
	_guard_ready_at = 0
	_last_key = ""
	_last_at = -1_000_000


## Lê um `pose` (de `walk`, `strike`, `dodge` ou `block`). Devolve os golpes recebidos novos: [{outcome, damage, by}].
func note_pose(pose: Dictionary, now_ms: int) -> Array:
	var events: Array = []
	var c = pose.get("combat")
	if not c is Dictionary:
		return events
	reach_m = float(c.get("reachMeters", reach_m))
	_strike_at = now_ms + int(c.get("strikeReadyInMs", 0))
	guard = String(c.get("guard", "")) if c.get("guard") != null else ""
	_guard_until = now_ms + int(c.get("guardMs", 0)) if guard != "" else 0
	_guard_ready_at = now_ms + int(c.get("guardReadyInMs", 0))
	var incoming = c.get("lastIncoming")
	if incoming is Dictionary:
		var ago := int(incoming.get("agoMs", 0))
		var at := now_ms - ago
		var key := "%s:%d:%d" % [incoming.get("outcome", ""), int(incoming.get("damage", 0)), int(incoming.get("by", -1))]
		var same := key == _last_key and absi(at - _last_at) <= SAME_MS
		var first_look := _last_key == "" and ago > FRESH_MS
		if not same and not first_look:
			events.append({"outcome": String(incoming.get("outcome", "")), "damage": int(incoming.get("damage", 0)),
				"by": int(incoming.get("by", -1))})
		_last_key = key
		_last_at = at
	return events


## O golpe aceito: o motor diz em quanto tempo o corpo golpeia de novo (`readyInMs`).
func note_strike(ready_in_ms: int, now_ms: int) -> void:
	_strike_at = now_ms + maxi(0, ready_in_ms)


func strike_ready(now_ms: int) -> bool:
	return now_ms >= _strike_at


## 0 = pronto, 1 = acabou de golpear: a barra discreta de recarga. `total_ms` é a recarga conhecida (a última dita pelo motor).
func strike_wait_ms(now_ms: int) -> int:
	return maxi(0, _strike_at - now_ms)


func guarding(now_ms: int) -> bool:
	return guard != "" and now_ms <= _guard_until


func guard_wait_ms(now_ms: int) -> int:
	return maxi(0, _guard_ready_at - now_ms)


func guard_ready(now_ms: int) -> bool:
	return now_ms >= _guard_ready_at


## O texto curto do que o motor recusou (fora de alcance, recarga): o próprio erro, sem o prefixo técnico.
static func refusal_text(status: int, body: Variant, fallback: String) -> String:
	var error := String(body.get("error", "")) if body is Dictionary else ""
	if error == "":
		return "%s (erro %d)." % [fallback, status] if status > 0 else fallback
	for prefix in ["conflict: recusa: ", "conflict: ", "bad request: ", "strike: ", "dodge: ", "block: "]:
		error = error.trim_prefix(prefix)
	return error
