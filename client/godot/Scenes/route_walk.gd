extends RefCounted
## Caminhada de UM boneco pela rota que o motor mandou (AD-181, `npc.route` + `routeArrivalTick`). O motor só move o NPC na
## chegada; quem anda célula a célula é o cliente: a posição é a fração do tempo da viagem (início da ação → chegada) no
## relógio do mundo (`route_clock.gd`), ao longo do comprimento real da rota (diagonal mais longa que reta). Pausa congela
## porque o relógio para; 200× só chega mais rápido. Só visual (autorizado): o boneco anda pela DIREITA da rua (quem vem
## de frente passa ao lado, não por dentro) e sai do lugar em que estava desenhado sem salto. Sem alocação por quadro.

const Grid := preload("res://Scenes/grid.gd")

const LANE := 0.14 ## afastamento para a direita do rumo, no meio do trecho (some nas pontas)
const BLEND := 0.6 ## em quantas células o boneco sai de onde estava desenhado e entra na rota

var cells: Array = [] ## Vector2i da rota, como veio
var points := PackedVector3Array()
var along := PackedFloat32Array() ## distância acumulada até cada ponto
var total := 0.0
var start_tick := 0.0
var end_tick := 0.0
var cursor := 0 ## trecho atual (só avança: busca O(1) amortizada)
var position := Vector3.ZERO
var heading := Vector3.ZERO
var start_offset := Vector3.ZERO
var done := false


## Monta a caminhada; false se não dá para andar (menos de duas células ou chegada antes da saída).
func setup(route_cells: Array, start: float, arrival: float, from: Vector3) -> bool:
	if route_cells.size() < 2 or arrival <= start:
		return false
	cells = route_cells
	start_tick = start
	end_tick = arrival
	points.resize(route_cells.size())
	along.resize(route_cells.size())
	for i in route_cells.size():
		points[i] = Grid.to_world(route_cells[i])
		along[i] = 0.0 if i == 0 else along[i - 1] + points[i].distance_to(points[i - 1])
	total = along[route_cells.size() - 1]
	var offset: Vector3 = from - points[0]
	offset.y = 0.0
	start_offset = offset if offset.length() <= 1.5 else Vector3.ZERO
	position = from
	return true


func same(route_cells: Array, start: float, arrival: float) -> bool:
	return is_equal_approx(start, start_tick) and is_equal_approx(arrival, end_tick) and route_cells == cells


## Fração da viagem no tick do mundo (0 na saída, 1 na chegada).
func fraction(tick: float) -> float:
	return clampf((tick - start_tick) / (end_tick - start_tick), 0.0, 1.0)


## Põe `position`/`heading` no ponto da rota em que o boneco está neste tick.
func step(tick: float) -> void:
	var d := fraction(tick) * total
	var last := points.size() - 1
	while cursor < last - 1 and along[cursor + 1] < d:
		cursor += 1
	var a := points[cursor]
	var b := points[cursor + 1]
	var segment := along[cursor + 1] - along[cursor]
	var f := clampf((d - along[cursor]) / segment, 0.0, 1.0) if segment > 0.0 else 1.0
	heading = b - a
	position = a.lerp(b, f)
	if total > 0.0 and heading.length_squared() > 0.0001:
		var taper := clampf(minf(d, total - d) / 0.5, 0.0, 1.0)
		position += Vector3(-heading.z, 0, heading.x).normalized() * LANE * taper
	if d < BLEND:
		position += start_offset * (1.0 - d / BLEND)
	done = d >= total


## O que falta andar: o ponto desenhado agora e as células adiante (para a linha da rota no chão).
func remaining(from: Vector3) -> PackedVector3Array:
	var out := PackedVector3Array([from])
	for i in range(cursor + 1, points.size()):
		out.append(points[i])
	return out
