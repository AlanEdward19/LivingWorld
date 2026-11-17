extends RefCounted
## PONTO ÚNICO do desenho e do nome de uma célula de rua (AD-177). A superfície vem do motor (camada `Roads`); o cliente
## só escolhe a aparência. Superfície desconhecida cai em NEUTRAL. A AD-176 (rua pelo uso, obra de calçamento, tipo de
## acesso: viela, escada, laje/telhado) entra AQUI: acrescente a chave em ACCESS_STYLES/WORK_STYLE e leia o campo novo em
## `access_of`/`works_of` (o parse guarda todo campo desconhecido em `road.extra`). Mapa 3D, planta e ficha usam isto.

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
## Tipo de acesso (AD-176, ainda não vem): "viela", "escada", "laje"... -> ajustes por cima da superfície.
const ACCESS_STYLES := {}
## Obra de calçamento em andamento (AD-176, ainda não vem): ajuste por cima da superfície.
const WORK_STYLE := {}
const MAIN_WIDTH_BONUS := 0.12 ## rua principal: mais larga e com meio-fio
const USERS_FOR_FULL := 24 ## quantas pessoas alargam a rua até o teto da superfície
const PATH_COLOR := Color("9b8460") ## caminho da porta até a rua (lote)
const CROFT_COLOR := Color("5f7a3c") ## chão do quintal: horta e terreiro
const SQUARE_TINT := Color(1.12, 1.08, 1.0)
const PLAN_SQUARE := Color("c8b48a")


static func access_of(_road: Dictionary) -> String:
	return "" # AD-176: ler o campo do tipo de acesso de road.extra quando o contrato trouxer


static func works_of(_road: Dictionary) -> bool:
	return false # AD-176: ler o campo da obra de rua de road.extra quando o contrato trouxer


## Aparência resolvida de uma rua: {name, long, color, plan, width, detail, jitter, main, square}.
static func style_of(road: Dictionary) -> Dictionary:
	var base: Dictionary = SURFACES.get(int(road.get("surface", -1)), NEUTRAL)
	var style := base.duplicate()
	var users := int(road.get("users", 0))
	style.width = clampf(float(base.width) * (0.8 + 0.4 * minf(float(users) / USERS_FOR_FULL, 1.0)), 0.18, 1.0)
	style.main = bool(road.get("main", false))
	style.square = bool(road.get("square", false))
	if style.main:
		style.width = minf(float(style.width) + MAIN_WIDTH_BONUS, 1.0)
	if style.square:
		style.width = 1.0
		style.color = Color(base.color) * SQUARE_TINT
		style.plan = PLAN_SQUARE
	style.merge(ACCESS_STYLES.get(access_of(road), {}), true)
	if works_of(road):
		style.merge(WORK_STYLE, true)
	return style


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
	lines.append("Quem passa por aqui: %d pessoa%s (moradores e quem trabalha nos prédios servidos)" % [users, "" if users == 1 else "s"])
	if style.square:
		lines.append("É o largo da cidade (em volta do ponto de fundação)")
	elif style.main:
		lines.append("É a rua principal")
	lines.append("Célula %d,%d" % [road.cell.x, road.cell.y])
	return lines
