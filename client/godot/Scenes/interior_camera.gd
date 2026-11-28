extends RefCounted
## #250: o enquadramento da câmera do jogador DENTRO da casa (estilo The Sims): a câmera sobe e olha o cômodo em diagonal, de cima das
## paredes, e o que ficar entre ela e o corpo (parede, móvel) vira translúcido (`interior_view.gd`). Tudo deriva do tamanho real da
## planta que o motor mandou (a caixa do andar); estes são os únicos parâmetros do cliente.

const Grid := preload("res://Scenes/grid.gd")

const PITCH_SMALL := deg_to_rad(-56.0) ## inclinação do olhar em cômodo pequeno: mais a prumo (a vista passa por cima da parede de perto)
const PITCH_LARGE := deg_to_rad(-40.0) ## e em cômodo grande (taverna): mais baixa e mais perto do chão, vê-se o salão inteiro
const PITCH_MIN := deg_to_rad(-64.0) ## o mouse ajusta a inclinação dentro desta faixa
const PITCH_MAX := deg_to_rad(-24.0)
const SMALL_SPAN_M := 4.5 ## lado da planta (m) até o qual vale PITCH_SMALL
const LARGE_SPAN_M := 12.0 ## e a partir do qual vale PITCH_LARGE
const FILL := 0.92 ## fração do campo de visão que o lado maior da planta ocupa (define a distância)
const FOV_DEG := 70.0
const ASPECT := 1.6 ## proporção da tela que o enquadramento garante (a de 16:10; as mais largas só ganham sobra)
const CAP_RATIO := 2.2 ## o braço nunca passa disto vezes o braço da 3ª pessoa de rua
const ROOM_PAD_M := 0.5 ## #268: folga em volta do cômodo enquadrado (vê-se um pouco do que há nas portas)
const CLEARANCE_M := 0.25 ## folga da câmera acima do topo das paredes
const MIN_SIN := 0.35 ## seno mínimo da inclinação (evita braço infinito com o olhar quase horizontal)
const OUTSIDE_MARGIN_M := 2.2 ## #267: a câmera pode passar da parede da casa até esta folga (ela está acima das paredes cortadas); mais que isso é "de fora e de lado"


## #268: o cômodo do corpo (planta do motor, `rooms`): a caixa do cômodo que contém o olho (x, z) ou, na porta/soleira, a do mais perto. `entry[5]`
## são as caixas dos cômodos do andar; sem cômodos (mundo sem planta de dentro) vale a caixa do andar inteiro.
static func focus(eye: Vector3, entry: Array) -> AABB:
	var best: AABB = entry[1]
	if eye == Vector3.INF or entry.size() < 6 or not (entry[5] is Array) or (entry[5] as Array).is_empty():
		return best
	var near := INF
	for room: AABB in entry[5]:
		var gap := Vector2(maxf(maxf(room.position.x - eye.x, eye.x - room.end.x), 0.0), maxf(maxf(room.position.z - eye.z, eye.z - room.end.z), 0.0)).length()
		if gap < near:
			near = gap
			best = room
	return best


## Lado do quadrado centrado no corpo que cobre o cômodo inteiro (o corpo no centro da tela: o canto mais longe manda); sem corpo, o lado maior da caixa.
static func span(entry: Array, eye: Vector3 = Vector3.INF) -> float:
	var box := focus(eye, entry)
	if eye == Vector3.INF:
		return maxf(box.size.x, box.size.z)
	var reach := maxf(maxf(absf(box.position.x - eye.x), absf(box.end.x - eye.x)), maxf(absf(box.position.z - eye.z), absf(box.end.z - eye.z)))
	return maxf(reach * 2.0, maxf(box.size.x, box.size.z)) + Grid.units(ROOM_PAD_M) * 2.0


## Inclinação do olhar ao entrar: cômodo pequeno olha mais para baixo, grande mais de frente.
static func default_pitch(entry: Array, eye: Vector3 = Vector3.INF) -> float:
	var box := focus(eye, entry) # #268: pelo tamanho do cômodo do corpo, não pelo alcance até o canto
	var meters := maxf(box.size.x, box.size.z) * Grid.cell_meters
	return lerpf(PITCH_SMALL, PITCH_LARGE, smoothstep(SMALL_SPAN_M, LARGE_SPAN_M, meters))


## Distância até a câmera (unidades): a que enquadra a planta (FILL do campo de visão) e passa por cima do topo das paredes com folga.
## `zoom` multiplica (rodinha: 0,4 a 1,8); `third_arm` é o braço de rua, que dá o teto.
static func arm_for(eye: Vector3, pitch: float, entry: Array, zoom: float, third_arm: float, back: Vector3 = Vector3.ZERO) -> float:
	var box: AABB = entry[1]
	var down := maxf(sin(-pitch), MIN_SIN)
	var fit := span(entry, eye) * FILL * 0.5 / tan(deg_to_rad(FOV_DEG) * 0.5)
	if back != Vector3.ZERO and eye != Vector3.INF:
		fit = fit_arm(eye, back, focus(eye, entry))
	var over := (box.end.y + Grid.units(CLEARANCE_M) - eye.y) / down
	return minf(maxf(fit, over), third_arm * CAP_RATIO) * zoom # #268: o teto vale antes do zoom (a rodinha sempre aproxima)


## #268: o braço que põe os quatro cantos do chão do cômodo (com a folga `ROOM_PAD_M`) dentro do campo de visão (vertical `FOV_DEG`, tela `ASPECT`, com a sobra
## `FILL`), com a câmera atrás do corpo na direção `back` olhando para ele: o canto de perto (embaixo da tela) manda tanto quanto o de longe.
static func fit_arm(eye: Vector3, back: Vector3, room: AABB) -> float:
	var forward := -back.normalized()
	var right := forward.cross(Vector3.UP).normalized()
	var up := right.cross(forward)
	var tan_v := tan(deg_to_rad(FOV_DEG) * 0.5) * FILL
	var tan_h := tan_v * ASPECT
	var pad := Grid.units(ROOM_PAD_M)
	var need := 0.0
	for corner_x in [room.position.x - pad, room.end.x + pad]:
		for corner_z in [room.position.z - pad, room.end.z + pad]:
			var d := Vector3(corner_x, room.position.y, corner_z) - eye
			var depth_y := absf(d.dot(up)) / tan_v
			var depth_x := absf(d.dot(right)) / tan_h
			need = maxf(need, maxf(depth_y, depth_x) - d.dot(forward))
	return need


## Plano da câmera para o olho `eye` dentro da casa `entry` ([-1, caixa, caixa, painéis, true]): {arm, lift, shoulder, room, indoor, pitch}.
## `others` são as outras entradas (prédios vizinhos): a câmera não entra na casca deles.
static func plan(eye: Vector3, back: Vector3, pitch: float, entry: Array, zoom: float, third_arm: float, others: Array, min_arm: float) -> Dictionary:
	var arm := arm_for(eye, pitch, entry, zoom, third_arm, back)
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
	return {"arm": maxf(best, min_arm), "lift": 0.0, "shoulder": 0.0, "room": true, "indoor": true, "pitch": default_pitch(entry, eye)}


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
