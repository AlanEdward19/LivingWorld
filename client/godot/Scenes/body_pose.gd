extends RefCounted
## #230: matemática pura do corpo articulado (sem nós, testável): IK de dois ossos (perna e braço), trajetória do pé no ciclo
## de passada, mistura de poses, altura do chão sob o pé e proporções por idade. Nada aqui decide comportamento nem lê estado.

const THIGH := 0.19 ## coxa e canela somam 0,34: quadril em 0,36, tornozelo a 0,02 do chão (o pé é a sola de 0,02)
const SHIN := 0.15
const ANKLE_H := 0.02
const HIP_Y := 0.36
const UPPER_ARM := 0.17
const FOREARM := 0.14
const SHOULDER_Y := 0.82
const SHOULDER_X := 0.22
const MAX_STEP := 0.25 ## degrau máximo que o pé acompanha (em unidades do boneco)


## IK de dois ossos no espaço do pai do membro. `to_target` = alvo do punho/tornozelo relativo ao ombro/quadril; `pole` aponta
## para onde o cotovelo/joelho dobra. Devolve `basis` (orientação do pivô do osso de cima: o -Y local aponta ao cotovelo, o eixo X
## local é o eixo da dobra) e `flex` (rotação X do pivô do osso de baixo, 0 = esticado). Alvo fora do alcance estica sem quebrar.
static func solve_limb(to_target: Vector3, l1: float, l2: float, pole: Vector3) -> Dictionary:
	var reach := to_target.length()
	var d := clampf(reach, absf(l1 - l2) + 0.002, l1 + l2 - 0.0002)
	var dir := to_target / reach if reach > 0.00001 else Vector3.DOWN
	var a := acos(clampf((l1 * l1 + d * d - l2 * l2) / (2.0 * l1 * d), -1.0, 1.0))
	var flex := PI - acos(clampf((l1 * l1 + l2 * l2 - d * d) / (2.0 * l1 * l2), -1.0, 1.0))
	var p := pole - dir * pole.dot(dir)
	if p.length_squared() < 0.000001:
		p = dir.cross(Vector3.RIGHT) if absf(dir.x) < 0.9 else dir.cross(Vector3.UP)
	p = p.normalized()
	var upper := dir * cos(a) + p * sin(a)
	var toward := dir * sin(a) - p * cos(a) # perpendicular a `upper`, do lado do alvo: para onde o osso de baixo dobra
	var y_axis := -upper
	var z_axis := -toward
	return {"basis": Basis(y_axis.cross(z_axis), y_axis, z_axis), "flex": flex, "reach": reach}


## Ponta do membro (relativa ao ombro/quadril) que a solução alcança: confere o IK em teste e posiciona a mão no objeto.
static func limb_end(solution: Dictionary, l1: float, l2: float) -> Vector3:
	return solution.basis * (Vector3(0, -l1, 0) + Basis(Vector3.RIGHT, float(solution.flex)) * Vector3(0, -l2, 0))


## Trajetória do tornozelo no ciclo (u em [0,1)): apoio recua linear no chão; balanço sobe e avança suave. (z para a frente, y de levantada).
static func foot_path(u: float, stride: float, lift: float, duty: float) -> Vector2:
	if u < duty:
		return Vector2(stride * (0.5 - u / duty), 0.0)
	var v := (u - duty) / (1.0 - duty)
	return Vector2(stride * (-0.5 + smoothstep(0.0, 1.0, v)), lift * sin(PI * v))


const WALK_EXCURSION := 0.40 ## #256: amplitude do pé no apoio andando (±0,20 na perna de 0,34, o quadril desce o que o alcance pede): passada calma do boneco
const RUN_EXCURSION := 0.44
const WALK_DUTY := 0.55 ## fração do ciclo com o pé no chão ao andar (0,6 era de quem anda apertado: pedia cadência de trote)
const MIN_CYCLES := 0.55 ## ciclos/s mínimos (1,1 passos/s): mais devagar que isto o passo encurta em vez de arrastar

## #256: a passada pela velocidade REAL desenhada: `speed` em unidades de mundo/s (a célula tem `cell_meters` m) e `body_scale` a escala
## do corpo (altura). Sem deslize: o pé de apoio recua `stride` (unidades do boneco) em `duty` do ciclo na velocidade do corpo, então
## ciclos/s = v·duty/stride. A amplitude é a do tamanho do corpo (teto de andar ou correr, menor em criança e idoso); só se encurta
## quando andar tão devagar pediria menos de MIN_CYCLES. Devolve {stride, duty, cycles (ciclos/s), steps (passos/s), mps (m/s)}.
static func gait_params(speed: float, body_scale: float, stage: String, run: float, cell_meters: float) -> Dictionary:
	var local_speed := speed / maxf(body_scale, 0.01) # unidades do boneco por segundo (o boneco é desenhado ×escala no mundo)
	var duty := lerpf(WALK_DUTY, 0.4, run)
	var stride := lerpf(WALK_EXCURSION, RUN_EXCURSION, run) * stride_factor(stage)
	stride = minf(stride, local_speed * duty / MIN_CYCLES) if local_speed > 0.0001 else stride
	var cycles := local_speed * duty / maxf(stride, 0.0001)
	return {"stride": stride, "duty": duty, "cycles": cycles, "steps": cycles * 2.0, "mps": speed * cell_meters}


## #256: quanto o quadril tem de descer para o pé (a `reach_z` à frente ou atrás do quadril, `lift` acima do chão) ser alcançado sem
## esticar a perna além do osso: a perna mede THIGH+SHIN e o quadril está a HIP_Y-ANKLE_H do tornozelo; sem isto o IK esticava e o pé de
## apoio escorregava. Dá o balanço natural do quadril (mais baixo com as pernas abertas, mais alto com elas sob o corpo).
static func drop_for_reach(reach_z: float, lift: float) -> float:
	var leg := THIGH + SHIN - 0.004
	var up := HIP_Y - ANKLE_H - lift
	var vertical := sqrt(maxf(leg * leg - reach_z * reach_z, 0.0))
	return maxf(up - vertical, 0.0)


## Aproximação exponencial independente do quadro: `rate` em 1/s. Sem estalo ao mudar de alvo.
static func approach(current: float, target: float, rate: float, dt: float) -> float:
	return lerpf(current, target, 1.0 - exp(-rate * dt))


## Mistura de duas orientações de membro (FK x IK) sem salto.
static func blend_basis(a: Basis, b: Basis, weight: float) -> Basis:
	if weight <= 0.0:
		return a
	if weight >= 1.0:
		return b
	return Basis(a.get_rotation_quaternion().slerp(b.get_rotation_quaternion(), weight))


## Envelope de um gesto (0..1..0) com platô: sobe e desce depressa e SEGURA a pose no meio, para o gesto ler (o seno antigo nunca parava).
static func plateau(t: float, edge: float = 0.22) -> float:
	var x := clampf(t, 0.0, 1.0)
	return smoothstep(0.0, 1.0, clampf(minf(x / edge, (1.0 - x) / edge), 0.0, 1.0))


const CHOP_PERIOD := 1.15 ## s por golpe de machado (levantar, descer, recuperar)


## #241: o golpe de machado em laço (u em [0,1) do ciclo): 0 = machado embaixo, 1 = no alto. Sobe devagar (0 a 55%), desce num golpe
## seco (55 a 68%) e recupera embaixo até o ciclo fechar.
static func chop_swing(u: float) -> float:
	var x := fposmod(u, 1.0)
	if x < 0.55:
		return smoothstep(0.0, 1.0, x / 0.55)
	if x < 0.68:
		return 1.0 - smoothstep(0.0, 1.0, (x - 0.55) / 0.13)
	return 0.0


## #241: peso de um gesto em laço (o corpo entra e sai da pose em `edge` s, e fica nela o resto do tempo): não escala com a duração.
static func loop_weight(elapsed: float, length: float, edge: float = 0.25) -> float:
	return smoothstep(0.0, 1.0, clampf(minf(elapsed, length - elapsed) / maxf(edge, 0.001), 0.0, 1.0))


## Altura da rota (pontos com `along` acumulado) na distância `d`: o chão sob o pé que vai à frente. Rota sem altura = 0.
static func route_height(points: PackedVector3Array, along: PackedFloat32Array, d: float) -> float:
	var n := points.size()
	if n < 2:
		return 0.0
	d = clampf(d, 0.0, along[n - 1])
	var i := clampi(along.bsearch(d) - 1, 0, n - 2)
	var span := along[i + 1] - along[i]
	var f := clampf((d - along[i]) / span, 0.0, 1.0) if span > 0.0 else 1.0
	return lerpf(points[i].y, points[i + 1].y, f)


## Proporções pelo estágio de vida: cabeça maior no bebê e na criança (o corpo todo já encolhe pela altura do motor).
static func head_factor(stage: String) -> float:
	match stage:
		"baby":
			return 1.5
		"child":
			return 1.25
	return 1.0


## Passada pelo estágio: idoso passa curto, inclinado e devagar; criança tem o passo mais curto.
static func stride_factor(stage: String) -> float:
	match stage:
		"elder":
			return 0.8
		"child":
			return 0.85
		"baby":
			return 0.7
	return 1.0
