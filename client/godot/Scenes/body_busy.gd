extends RefCounted
## #241: a ação em andamento que ocupa o corpo do jogador, como o motor a diz (`busy` da pose: `action`, `untilMs`, `totalMs`,
## `remainingMs`, aditivo e opcional). Só lê: o tempo que falta vem do motor em ms de mundo e aqui vira tempo real pela escala do relógio.
## O motor decide a duração e o que fica feito ao interromper; o cliente mostra a barra, anima o corpo e não manda passo enquanto dura.

## ação do motor -> o que o corpo está fazendo ("Cortando lenha") e o gesto em laço do rig procedural (`npc_actor.GESTURES`)
const ACTIONS := {
	"gather": {"doing": "Cortando lenha", "gesture": "chop", "stopped": "Parou de cortar lenha."},
	"work": {"doing": "Trabalhando", "gesture": "work", "stopped": "Parou de trabalhar."},
	"drink": {"doing": "Bebendo", "gesture": "drink", "stopped": "Parou de beber."},
	"sleep": {"doing": "Dormindo", "gesture": "rest", "stopped": "Parou de dormir."},
	"use": {"doing": "Usando", "gesture": "reach", "stopped": "Parou de usar."},
}

var action := ""
var total_ms := 0.0 ## duração em ms de mundo
var _end_msec := 0.0 ## instante real (Time.get_ticks_msec) em que acaba
var _real_total_ms := 0.0


func active() -> bool:
	return action != ""


## Lê a pose do motor: com `busy` guarda a ação e quando acaba (`scale`: tempo de mundo por tempo real); sem ela, limpa.
## Devolve true se o que se mostra mudou (começou, acabou ou outra ação).
func note_pose(pose: Dictionary, now_msec: float, scale: float) -> bool:
	var before := action
	var busy: Variant = pose.get("busy")
	if not busy is Dictionary or String(busy.get("action", "")) == "":
		action = ""
		return before != ""
	action = String(busy.action)
	total_ms = maxf(1.0, float(busy.get("totalMs", 1)))
	var factor := maxf(scale, 0.001)
	_real_total_ms = total_ms / factor
	_end_msec = now_msec + float(busy.get("remainingMs", 0)) / factor
	return before != action


func clear() -> void:
	action = ""


## Segundos reais que faltam (0 vencida).
func seconds_left(now_msec: float) -> float:
	return maxf(0.0, _end_msec - now_msec) / 1000.0 if active() else 0.0


func expired(now_msec: float) -> bool:
	return active() and now_msec >= _end_msec


## 0..1 do que já passou.
func fraction(now_msec: float) -> float:
	if not active():
		return 0.0
	return clampf(1.0 - (_end_msec - now_msec) / maxf(_real_total_ms, 1.0), 0.0, 1.0)


static func doing(kind: String) -> String:
	return String(ACTIONS.get(kind, {}).get("doing", "Ocupado"))


static func gesture_for(kind: String) -> String:
	return String(ACTIONS.get(kind, {}).get("gesture", "work"))


static func stopped(kind: String) -> String:
	return String(ACTIONS.get(kind, {}).get("stopped", "Parou."))


## "Cortando lenha — 40%", a linha da barra.
func text(now_msec: float) -> String:
	return "%s — %d%%" % [doing(action), roundi(fraction(now_msec) * 100.0)]


## O que se diz quando o jogador tenta andar ocupado: nunca o erro cru do servidor.
func blocked_note() -> String:
	return "Ocupado: %s (Espaço para parar)" % doing(action).to_lower()


## O erro do servidor em frase humana: sem prefixo técnico (`walk:`, `conflict:`, `move:`...), sem número de ms, com inicial maiúscula
## e ponto final. O jogador nunca vê o texto cru.
static func human_error(raw: String) -> String:
	var text := raw.strip_edges()
	var cut := true
	while cut:
		cut = false
		for prefix in ["conflict:", "recusa:", "walk:", "move:", "jump:", "gather:", "interact:", "interrupt:", "not_found:", "bad_request:", "error:"]:
			if text.begins_with(prefix):
				text = text.substr(prefix.length()).strip_edges()
				cut = true
	var open := text.find(" (faltam")
	if open >= 0:
		var close := text.find(")", open)
		text = text.substr(0, open) + (text.substr(close + 1) if close >= 0 else "")
	text = text.strip_edges()
	if text == "":
		return "O motor recusou."
	text = text.substr(0, 1).to_upper() + text.substr(1)
	return text if text.ends_with(".") or text.ends_with("!") else text + "."
