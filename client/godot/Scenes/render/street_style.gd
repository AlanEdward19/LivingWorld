extends RefCounted
## PONTO ÚNICO do desenho e do nome de uma célula de rua (AD-177, AD-181). Tudo vem do motor (ruas do escopo): superfície,
## faixa de uso (`useBand`) e pisoteio (`wear`), condição do calçamento, obra; e `lot.access` do prédio. O cliente só escolhe
## a aparência. Superfície desconhecida cai em NEUTRAL; acesso desconhecido, em "passagem"; campo novo fica em `extra`.
## Mapa 3D, planta e fichas usam isto.

## surface (0 trilha, 1 terra batida, 2 cascalho, 3 pedra) -> aparência. width = largura base na célula (0..1);
## detail: "" (liso), "pebbles" (pedrinhas soltas) ou "slabs" (lajes com junta); jitter = irregularidade da trilha.
const SURFACES := {
	0: {"name": "trilha", "long": "trilha de capim pisado", "color": Color("8d8a55"), "plan": Color("b7b276"),
		"width": 0.30, "detail": "", "jitter": 0.10},
	1: {"name": "terra batida", "long": "rua de terra batida", "color": Color("8a6f4e"), "plan": Color("a8875e"),
		"width": 0.62, "detail": "", "jitter": 0.03},
	2: {"name": "cascalho", "long": "rua de cascalho", "color": Color("93897a"), "plan": Color("a59e93"),
		"width": 0.72, "detail": "pebbles", "jitter": 0.0},
	3: {"name": "pedra", "long": "rua calçada de pedra", "color": Color("7f7d7a"), "plan": Color("6d6c6e"),
		"width": 0.82, "detail": "slabs", "jitter": 0.0},
}
const NEUTRAL := {"name": "passagem", "long": "passagem (superfície que este cliente não conhece)", "color": Color("8b8577"),
	"plan": Color("9a958a"), "width": 0.55, "detail": "", "jitter": 0.0}
## Como se chega à porta (`lot.access`, AD-176): nome na ficha, cor na planta e se é alerta.
const ACCESS_STYLES := {
	"street": {"name": "rua", "line": "pela rua, porta direto nela", "plan": Color("e8dcc0")},
	"path": {"name": "caminho", "line": "por um caminho da porta até a rua", "plan": Color("9b8460")},
	"alley": {"name": "viela", "line": "por uma viela entre dois prédios", "plan": Color("5c4b3b")},
	"stair": {"name": "escada", "line": "por uma escada encostada no sobrado vizinho", "plan": Color("c98b3c")},
	"roof": {"name": "passagem por cima", "line": "por cima da laje/telhado do vizinho (passarela)", "plan": Color("b0623a")},
	"none": {"name": "sem acesso", "line": "SEM ACESSO: a porta dá para fora do mapa (o motor diz que não deveria existir)",
		"plan": Color("e23b3b"), "alert": true},
}
## Obra de calçamento (`work`): pilha do material, estacas e a parte já assentada.
const WORK_STYLE := {"pile": Color("a39d92"), "stone_pile": Color("8c8a86"), "stake": Color("d8b25a"), "plan": Color("e0a030")}
const PAYERS := {"treasury": "o tesouro da cidade", "frontage": "as casas de frente (cada uma a sua parte)"}
const USE_BANDS := ["quase ninguém (menos de 10 passagens no mês)", "pouco uso (10 a 29)", "uso médio (30 a 99)",
	"muito uso (100 a 299)", "uso intenso (300 ou mais)"]
const MAIN_WIDTH_BONUS := 0.12 ## rua principal: mais larga e com meio-fio
const USERS_FOR_FULL := 24 ## servidor sem `useBand`: quantas pessoas alargam a rua até o teto da superfície
const WEAR_FULL := 1500.0 ## pisoteio em que o chão fica todo à mostra (limiar de terra batida do motor, ordem de grandeza)
const BARE := Color("7a5f42") ## chão gasto, sem capim
const GRASS := Color("6f8a45") ## capim que volta onde pouco se pisa
const BROKEN := Color("6e5a45") ## terra que aparece no calçamento estragado
const BROKEN_BELOW := 600 ## condição (0–1000) abaixo da qual o calçamento aparece estragado
const PATH_COLOR := Color("9b8460") ## caminho da porta até a rua (lote)
const CROFT_COLOR := Color("5f7a3c") ## chão do quintal: horta e terreiro
const SQUARE_TINT := Color(1.12, 1.08, 1.0)
const PLAN_SQUARE := Color("c8b48a")


## Tipo de acesso do lote ("" = o motor não disse).
static func access_of(lot: Dictionary) -> String:
	return String(lot.get("access", ""))


static func access_style(access: String) -> Dictionary:
	return ACCESS_STYLES.get(access, {"name": "passagem", "line": "por uma passagem (%s)" % access, "plan": Color("9a958a")})


static func works_of(road: Dictionary) -> bool:
	return not work_of(road).is_empty()


## Obra da célula ({surface, progress, repair, payer, extra}) ou {}.
static func work_of(road: Dictionary) -> Dictionary:
	return road.get("work", {}) if road.get("work") is Dictionary else {}


## Aparência resolvida de uma rua: {name, long, color, plan, width, detail, jitter, main, square, wear, tufts, broken, work}.
static func style_of(road: Dictionary) -> Dictionary:
	var base: Dictionary = SURFACES.get(int(road.get("surface", -1)), NEUTRAL)
	var style := base.duplicate()
	var band := int(road.get("use_band", -1))
	var use := float(band) / 4.0 if band >= 0 else minf(float(road.get("users", 0)) / USERS_FOR_FULL, 1.0)
	style.width = clampf(float(base.width) * (0.8 + 0.4 * use), 0.18, 1.0)
	var wear := clampf(float(road.get("wear", 0)) / WEAR_FULL, 0.0, 1.0)
	style.wear = wear
	style.tufts = 0
	if int(road.get("surface", -1)) in [0, 1] and band >= 0: # chão de terra (motor da AD-176): o pisoteio tira o capim
		style.color = Color(base.color).lerp(GRASS, (1.0 - wear) * 0.25).lerp(BARE, wear * 0.3)
		style.tufts = int(round((1.0 - wear) * 3.0))
	var condition := int(road.get("condition", -1))
	style.broken = clampf(1.0 - float(condition) / BROKEN_BELOW, 0.0, 1.0) if condition >= 0 else 0.0
	if style.broken > 0.0:
		style.color = Color(style.color).lerp(BROKEN, style.broken * 0.6)
		style.plan = Color(style.plan).lerp(BROKEN, style.broken * 0.5)
	style.main = bool(road.get("main", false))
	style.square = bool(road.get("square", false))
	if style.main:
		style.width = minf(float(style.width) + MAIN_WIDTH_BONUS, 1.0)
	if style.square:
		style.width = 1.0
		style.color = Color(base.color) * SQUARE_TINT
		style.plan = PLAN_SQUARE
	style.work = work_of(road)
	return style


## Cor do mapa de calor do pisoteio: frio (pouco) a quente (muito), relativo ao mais pisado do escopo.
static func heat_color(wear: int, top: int) -> Color:
	var t := clampf(float(wear) / maxf(float(top), 1.0), 0.0, 1.0)
	return Color("2f5d8a").lerp(Color("f0d040"), minf(t * 2.0, 1.0)) if t < 0.5 else Color("f0d040").lerp(Color("d6342a"), (t - 0.5) * 2.0)


static func surface_name(surface: int) -> String:
	return String(SURFACES.get(surface, NEUTRAL).name)


## Nome curto ("Largo", "Rua principal de terra batida", "Trilha").
static func title_of(road: Dictionary) -> String:
	var style := style_of(road)
	if style.square:
		return "Largo (%s)" % style.name
	if style.main:
		return "Rua principal de %s" % style.name
	var text := String(style.long)
	return text.substr(0, 1).to_upper() + text.substr(1) if int(road.get("surface", -1)) in SURFACES else "Passagem"


## Linhas da ficha da rua.
static func lines_of(road: Dictionary) -> Array[String]:
	var style := style_of(road)
	var lines: Array[String] = ["Superfície: %s" % style.long]
	var users := int(road.get("users", 0))
	var band := int(road.get("use_band", -1))
	if band >= 0: # AD-176: `users` = passagens do último mês
		lines.append("Passagens no último mês: %d · %s" % [users, USE_BANDS[band]])
		lines.append("Pisoteio acumulado: %d (%s)" % [int(road.get("wear", 0)), "chão todo à mostra" if style.wear >= 0.66
			else ("capim ralo" if style.wear >= 0.25 else "capim ainda volta")])
	else:
		lines.append("Quem passa por aqui: %d pessoa%s (moradores e quem trabalha nos prédios servidos)" % [users, "" if users == 1 else "s"])
	var condition := int(road.get("condition", -1))
	if condition >= 0:
		lines.append("Calçamento: %d%% de condição%s" % [condition / 10, " · estragado, pede conserto" if condition < BROKEN_BELOW else ""])
	if style.square:
		lines.append("É o largo da cidade (em volta do ponto de fundação)")
	elif style.main:
		lines.append("É a rua principal")
	if bool(road.get("planned", false)):
		lines.append("Faz parte do traçado reservado da cidade")
	var work: Dictionary = style.work
	if not work.is_empty():
		lines.append("Em obra: %s para %s · %d%% feito" % ["conserto" if bool(work.repair) else "obra nova",
			surface_name(int(work.surface)), int(work.progress)])
		lines.append("Quem paga: %s" % PAYERS.get(String(work.payer), String(work.payer) if String(work.payer) != "" else "não informado"))
		lines.append("Quem trabalha nela: %s" % ("%d pessoa(s)" % workers_of(road).size() if not workers_of(road).is_empty()
			else "ninguém no momento (obra parada)"))
	lines.append("Célula %d,%d" % [road.cell.x, road.cell.y])
	return lines


## Quem está na obra agora (`work.workerIds`, AD-179); vazio = obra parada por falta de braço.
static func workers_of(road: Dictionary) -> Array:
	var extra: Dictionary = work_of(road).get("extra", {})
	for key in ["workers", "workerIds", "crew"]:
		if extra.get(key) is Array:
			return extra[key].map(func(v): return int(v.value) if v is Dictionary and v.has("value") else int(v))
	return []
