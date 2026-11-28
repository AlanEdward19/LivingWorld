extends RefCounted
## #250: o enquadramento da câmera do jogador DENTRO da casa (estilo The Sims): a câmera sobe e olha o cômodo em diagonal, de cima das
## paredes, e o que ficar entre ela e o corpo (parede, móvel) vira translúcido (`interior_view.gd`). Tudo deriva do tamanho real da
## planta que o motor mandou (a caixa do andar); estes são os únicos parâmetros do cliente.

const Grid := preload("res://Scenes/grid.gd")

const PITCH_SMALL := deg_to_rad(-48.0) ## inclinação do olhar em cômodo pequeno: mais a prumo (a vista passa por cima da parede de perto)
const PITCH_LARGE := deg_to_rad(-34.0) ## e em cômodo grande (taverna): mais baixa e mais perto do chão, vê-se o salão inteiro
const PITCH_MIN := deg_to_rad(-64.0) ## o mouse ajusta a inclinação dentro desta faixa
const PITCH_MAX := deg_to_rad(-24.0)
const SMALL_SPAN_M := 4.5 ## lado da planta (m) até o qual vale PITCH_SMALL
const LARGE_SPAN_M := 12.0 ## e a partir do qual vale PITCH_LARGE
const FILL := 0.8 ## fração do campo de visão que o lado maior da planta ocupa (define a distância)
const FOV_DEG := 70.0
const CAP_RATIO := 1.4 ## o braço nunca passa disto vezes o braço da 3ª pessoa de rua
const CLEARANCE_M := 0.25 ## folga da câmera acima do topo das paredes
const MIN_SIN := 0.35 ## seno mínimo da inclinação (evita braço infinito com o olhar quase horizontal)
const OUTSIDE_MARGIN_M := 2.2 ## #267: a câmera pode passar da parede da casa até esta folga (ela está acima das paredes cortadas); mais que isso é "de fora e de lado"


## Lado maior da planta do andar em foco, em unidades da grade (a caixa do andar da entrada).
static func span(entry: Array) -> float:
	var box: AABB = entry[1]
	return maxf(box.size.x, box.size.z)


## Inclinação do olhar ao entrar: cômodo pequeno olha mais para baixo, grande mais de frente.
static func default_pitch(entry: Array) -> float:
	var meters := span(entry) * Grid.cell_meters
	return lerpf(PITCH_SMALL, PITCH_LARGE, smoothstep(SMALL_SPAN_M, LARGE_SPAN_M, meters))


## Distância até a câmera (unidades): a que enquadra a planta (FILL do campo de visão) e passa por cima do topo das paredes com folga.
## `zoom` multiplica (rodinha: 0,4 a 1,8); `third_arm` é o braço de rua, que dá o teto.
static func arm_for(eye: Vector3, pitch: float, entry: Array, zoom: float, third_arm: float) -> float:
	var box: AABB = entry[1]
	var down := maxf(sin(-pitch), MIN_SIN)
	var fit := span(entry) * FILL * 0.5 / tan(deg_to_rad(FOV_DEG) * 0.5)
	var over := (box.end.y + Grid.units(CLEARANCE_M) - eye.y) / down
	return minf(maxf(fit, over) * zoom, third_arm * CAP_RATIO * maxf(zoom, 1.0))


## Plano da câmera para o olho `eye` dentro da casa `entry` ([-1, caixa, caixa, painéis, true]): {arm, lift, shoulder, room, indoor, pitch}.
## `others` são as outras entradas (prédios vizinhos): a câmera não entra na casca deles.
static func plan(eye: Vector3, back: Vector3, pitch: float, entry: Array, zoom: float, third_arm: float, others: Array, min_arm: float) -> Dictionary:
	var arm := arm_for(eye, pitch, entry, zoom, third_arm)
	var best := arm
	var far := eye + back * arm
	for other in others:
		var shell: AABB = other[2] if other.size() > 2 else other[1]
		if shell.has_point(eye):
			continue
		var hit = shell.intersects_segment(eye, far)
		if hit != null:
			best = minf(best, eye.distance_to(hit) - Grid.units(0.25))
	best = keep_in_house(eye, back, best, entry[1], min_arm)
	return {"arm": maxf(best, min_arm), "lift": 0.0, "shoulder": 0.0, "room": true, "indoor": true, "pitch": default_pitch(entry)}


## #267: o braço encurtado até a câmera ficar sobre a casa (a caixa da planta com a folga `OUTSIDE_MARGIN_M`): sem isso, com o olhar de lado, a câmera ia para fora da casa e via
## só a parede ou a fachada. Nunca menor que `min_arm`. Só olha o plano horizontal (a altura já é a do corte das paredes).
static func keep_in_house(eye: Vector3, back: Vector3, arm: float, box: AABB, min_arm: float) -> float:
	var limit := box.grow(Grid.units(OUTSIDE_MARGIN_M))
	var best := arm
	for axis in [0, 2]:
		var direction := back.x if axis == 0 else back.z
		if absf(direction) < 0.001:
			continue
		var start := eye.x if axis == 0 else eye.z
		var low := limit.position.x if axis == 0 else limit.position.z
		var high := low + (limit.size.x if axis == 0 else limit.size.z)
		var bound := high if direction > 0.0 else low
		var reach := (bound - start) / direction
		if reach >= 0.0:
			best = minf(best, reach)
	return maxf(best, min_arm)
