extends RefCounted
## #241: a ação em andamento que ocupa o corpo do jogador, como o motor a diz (`busy` da pose: `action`, `untilMs`, `totalMs`,
## `remainingMs`, aditivo e opcional). Só lê: o tempo que falta vem do motor em ms de mundo e aqui vira tempo real pela escala do relógio.
## O motor decide a duração e o que fica feito ao interromper; o cliente mostra a barra, anima o corpo e não manda passo enquanto dura.

## #255: a pose declarada pelo catálogo (já com o fallback do motor) -> o gesto em laço do rig procedural. O rig não muda: pose sem gesto cai em `rest`.
const POSE_GESTURE := {"work": "work", "eat": "eat", "sleep": "rest", "socialize": "talk", "idle": "rest", "buy": "give", "power": "reach"}

## ação do motor -> o que o corpo está fazendo ("Cortando lenha") e o gesto em laço do rig procedural (`npc_actor.GESTURES`)
const ACTIONS := {
	"gather": {"doing": "Cortando lenha", "gesture": "chop", "stopped": "Parou de cortar lenha."},
	"work": {"doing": "Trabalhando", "gesture": "work", "stopped": "Parou de trabalhar."},
	"drink": {"doing": "Bebendo", "gesture": "drink", "stopped": "Parou de beber."},
	"sleep": {"doing": "Dormindo", "gesture": "rest", "stopped": "Parou de dormir."},
	"use": {"doing": "Usando", "gesture": "reach", "stopped": "Parou de usar."},
}

var action := ""
var use := "" ## #255: o uso do catálogo em curso (`busy.use`), o nome que o jogador lê (`label`), a fase (`walk`: o motor leva o corpo até a peça; `do`: a ação corre) e a pose declarada
var label := ""
var phase := ""
var use_pose := ""
var facing := 0.0 ## graus: para onde o corpo olha no lugar de uso
var slot := Vector2.INF ## o lugar de uso em células (fino), dito pelo motor
var total_ms := 0.0 ## duração em ms de mundo
var _end_msec := 0.0 ## instante real (Time.get_ticks_msec) em que acaba
var _real_total_ms := 0.0
var _factor := 1.0 ## ms de mundo por ms real em vigor (1 em tempo real na escala 1; ~28800 com o mundo acelerado a 8 h/s)
var units := 0 ## #243: na coleta, quantas unidades a sessão tira (o motor diz); a unidade k sai em k/units do tempo
var accelerated := false ## #241: o motor acelera o mundo até a ação acabar (jogador sozinho: salto de tempo)
const SPEED_UP := 4.0 ## acima de quantas vezes o tempo real normal o rótulo diz "tempo acelerado"


func active() -> bool:
	return action != ""


## Lê a pose do motor: com `busy` guarda a ação e quando acaba (`scale`: tempo de mundo por tempo real); sem ela, limpa.
## Devolve true se o que se mostra mudou (começou, acabou ou outra ação).
func note_pose(pose: Dictionary, now_msec: float, scale: float) -> bool:
	var before := action
	var before_kind := kind()
	var busy: Variant = pose.get("busy")
	if not busy is Dictionary or String(busy.get("action", "")) == "":
		action = ""
		use = ""
		phase = ""
		units = 0
		accelerated = false
		return before != ""
	action = String(busy.action)
	use = String(busy.get("use", ""))
	label = String(busy.get("label", ""))
	phase = String(busy.get("phase", ""))
	use_pose = String(busy.get("pose", ""))
	facing = float(busy.get("facing", 0.0))
	slot = Vector2(float(busy.slotFx), float(busy.slotFy)) if busy.get("slotFx") != null else Vector2.INF
	units = int(busy.get("units", 0))
	accelerated = bool(busy.get("accelerated", false))
	total_ms = maxf(1.0, float(busy.get("totalMs", 1)))
	var factor := maxf(scale, 0.001)
	_factor = factor
	_real_total_ms = total_ms / factor
	_end_msec = now_msec + float(busy.get("remainingMs", 0)) / factor
	return before != action or before_kind != kind()


## A chave do que o corpo faz para o app animar: a ação, ou (#255) o uso com a pose dele; andando até a peça, `use:walk` (sem gesto).
func kind() -> String:
	if action != "use":
		return action
	return "use:walk" if phase == "walk" else "use:%s" % use_pose


func clear() -> void:
	action = ""
	use = ""
	phase = ""
	units = 0
	accelerated = false


## O ritmo do mundo mudou (acelerou, voltou ao normal): o que falta em ms de mundo é o mesmo, o tempo real que ele leva é que muda.
## `base_scale` é a escala do tempo real normal (para dizer se está acelerado).
func retime(now_msec: float, factor: float, base_scale: float) -> void:
	factor = maxf(factor, 0.001)
	if not active():
		return
	accelerated = factor > maxf(base_scale, 0.001) * SPEED_UP
	if is_equal_approx(factor, _factor):
		return
	var left_world := maxf(0.0, _end_msec - now_msec) * _factor
	_factor = factor
	_end_msec = now_msec + left_world / factor
	_real_total_ms = total_ms / factor


## O que falta, em ms de mundo.
func world_left_ms(now_msec: float) -> float:
	return maxf(0.0, _end_msec - now_msec) * _factor if active() else 0.0


## Segundos reais que faltam (0 vencida).
func seconds_left(now_msec: float) -> float:
	return maxf(0.0, _end_msec - now_msec) / 1000.0 if active() else 0.0


func expired(now_msec: float) -> bool:
	return active() and now_msec >= _end_msec


## 0..1 do que já passou (do tempo de mundo, então segue o ritmo mesmo quando ele muda no meio).
func fraction(now_msec: float) -> float:
	if not active():
		return 0.0
	return clampf(1.0 - world_left_ms(now_msec) / maxf(total_ms, 1.0), 0.0, 1.0)


static func doing(kind: String) -> String:
	return String(ACTIONS.get(kind, {}).get("doing", "Ocupado"))


static func gesture_for(kind: String) -> String:
	if kind == "use:walk":
		return ""
	if kind.begins_with("use:"):
		return String(POSE_GESTURE.get(kind.substr(4), "rest"))
	return String(ACTIONS.get(kind, {}).get("gesture", "work"))


static func stopped(kind: String) -> String:
	return String(ACTIONS.get(kind, {}).get("stopped", "Parou."))


## "Cortando lenha — 40% · tempo acelerado": a linha da barra. Sem acelerar (mais de um jogador, ou ação curta), diz o que falta em
## horas e minutos de mundo ("· faltam 4 h 12 min"), para o jogador saber quanto vai demorar.
func name_now() -> String:
	if action == "use" and label != "":
		return label
	return doing(action)


func text(now_msec: float) -> String:
	if action == "use" and phase == "walk":
		return "Indo: %s…" % label
	var line := "%s — %d%%" % [name_now(), roundi(fraction(now_msec) * 100.0)]
	if units > 1:
		line += " · %d de %d" % [mini(units, int(floor(fraction(now_msec) * units + 0.0001))), units] # #243: quantas já saíram
	if accelerated:
		return line + " · tempo acelerado"
	var left := world_left_ms(now_msec)
	return line + (" · faltam " + left_text(left) if left >= 60000.0 else "")


## "4 h 12 min", "35 min": horas e minutos de mundo.
static func left_text(world_ms: float) -> String:
	var minutes := int(ceil(world_ms / 60000.0))
	var hours := minutes / 60
	return ("%d h %02d min" % [hours, minutes % 60]) if hours > 0 else "%d min" % minutes


## O que se diz quando o jogador tenta andar ocupado: nunca o erro cru do servidor.
func blocked_note() -> String:
	return "Ocupado: %s (Espaço para parar)" % name_now().to_lower()


## #255: a frase de quando o jogador para o uso (o nome vem do catálogo do motor).
func stopped_text() -> String:
	return "Parou: %s." % label if action == "use" and label != "" else stopped(action)


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
