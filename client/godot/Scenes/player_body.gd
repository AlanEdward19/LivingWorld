extends RefCounted
## #229: controlador cinemático local do corpo do jogador (células, plano x/z). Só prevê: o motor valida cada passo.
## Aceleração e freio suaves, deslize em paredes, desvio macio de pessoas (desacelera e desliza, nunca bloqueia), degraus baixos pisados por cima e
## reconciliação com a posição do motor sem trancos. Só conhece a vizinhança (set_neighborhood); custo por quadro
## ~ obstáculos ao redor, nunca por NPC da cidade.

const ACCEL_TIME := 0.12 ## s para chegar à velocidade do motor (parado → andando)
const BRAKE_TIME := 0.08 ## s para parar ao soltar a tecla
const RADIUS := 0.2 ## células (0,3 m com 1,5 m por célula)
const PERSON_RADIUS := 0.2
const PERSON_SOFT := 0.35 ## fração da sobreposição com uma pessoa desviada (de lado) por sub-passo
const CROWD_DRAG := 0.1 ## #228: perda de velocidade por pessoa encostada; gente nunca bloqueia: o piso abaixo vale em qualquer multidão
const CROWD_FLOOR := 0.5 ## fração mínima da velocidade que a multidão deixa
const STEP_HEIGHT := 0.3 ## altura (mundo) até a qual um obstáculo é degrau e não parede
const SNAP_DISTANCE := 2.0 ## células: erro maior que isto vira salto (teleporte do motor, erro grande)
const LEAD_ALLOWANCE := 0.35 ## s de dianteira sobre o motor que são latência normal, não erro
const CONVERGE_RATE := 10.0 ## 1/s: quão rápido o erro fora da tolerância é absorvido

var pos := Vector2.INF
var velocity := Vector2.ZERO
var radius := RADIUS
var step_height := STEP_HEIGHT
var walls: Array[Rect2] = [] ## paredes sólidas (células, absolutas) da vizinhança
var people: Array[Vector2] = [] ## quem está ao redor (posição fina), sem o próprio corpo
var _correction := Vector2.ZERO ## erro pendente a absorver aos poucos


## Troca a vizinhança: `obstacles` = [[Rect2, topo]]; o que não passa de step_height é degrau e não bloqueia.
func set_neighborhood(obstacles: Array, around: Array) -> void:
	walls.clear()
	for entry in obstacles:
		if float(entry[1]) > step_height:
			walls.append(entry[0])
	people.clear()
	for p in around:
		people.append(p)


func place(at: Vector2) -> void:
	pos = at
	velocity = Vector2.ZERO
	_correction = Vector2.ZERO


## Ainda se move (freando ou absorvendo um erro do motor)?
func settling() -> bool:
	return velocity.length_squared() > 0.000001 or _correction.length_squared() > 0.000001


func stop() -> void:
	velocity = Vector2.ZERO


## Um quadro: `direction` (unitária ou zero) na `speed` (células/s do motor). Devolve a posição.
func advance(direction: Vector2, speed: float, delta: float) -> Vector2:
	if pos == Vector2.INF or delta <= 0.0:
		return pos
	var rate: float
	if direction == Vector2.ZERO:
		rate = maxf(velocity.length(), 0.5) / BRAKE_TIME
	else:
		rate = maxf(speed, 0.5) / ACCEL_TIME
	velocity = velocity.move_toward(direction * speed, rate * delta)
	var nudge := _correction * (1.0 - exp(-CONVERGE_RATE * delta))
	_correction -= nudge
	var move := velocity * delta * _crowd_factor() + nudge
	var pieces := maxi(1, ceili(move.length() / (radius * 0.5)))
	for i in pieces:
		pos += move / float(pieces)
		_resolve()
	return pos


func _resolve() -> void:
	for pass_index in 2: # duas passadas: o canto entre duas paredes
		for wall in walls:
			var nearest := Vector2(clampf(pos.x, wall.position.x, wall.end.x), clampf(pos.y, wall.position.y, wall.end.y))
			var away := pos - nearest
			var distance := away.length()
			if distance >= radius:
				continue
			var normal: Vector2
			if distance > 0.0001:
				normal = away / distance
				pos = nearest + normal * radius
			else: # o centro entrou na parede: sai pelo lado mais raso
				var to_left := pos.x - wall.position.x
				var to_right := wall.end.x - pos.x
				var to_top := pos.y - wall.position.y
				var to_bottom := wall.end.y - pos.y
				var shallow := minf(minf(to_left, to_right), minf(to_top, to_bottom))
				if shallow == to_left:
					normal = Vector2.LEFT
				elif shallow == to_right:
					normal = Vector2.RIGHT
				elif shallow == to_top:
					normal = Vector2.UP
				else:
					normal = Vector2.DOWN
				pos = nearest + normal * (shallow + radius)
			var into := velocity.dot(normal)
			if into < 0.0:
				velocity -= normal * into # desliza ao longo da parede
	var reach := radius + PERSON_RADIUS
	var heading := velocity.normalized() if velocity.length_squared() > 0.0001 else Vector2.ZERO
	for other in people:
		var apart := pos - other
		var gap := apart.length()
		if gap >= reach:
			continue
		var direction := apart / gap if gap > 0.0001 else Vector2.RIGHT
		if heading != Vector2.ZERO:
			# #228: as pessoas abrem passagem: o jogador desvia de lado (deslizando entre elas), nunca é empurrado para trás
			var side := direction - heading * direction.dot(heading)
			if side.length_squared() < 0.01: # a pessoa está de frente: escolhe o lado por uma regra fixa (determinística)
				side = Vector2(-heading.y, heading.x)
			direction = side.normalized()
		pos += direction * (reach - gap) * PERSON_SOFT


## #228: quanto da velocidade a gente encostada deixa (desacelera, nunca bloqueia): CROWD_FLOOR a 1.
func _crowd_factor() -> float:
	var touching := 0
	var reach := radius + PERSON_RADIUS
	for other in people:
		if pos.distance_squared_to(other) < reach * reach:
			touching += 1
	return maxf(CROWD_FLOOR, 1.0 - CROWD_DRAG * float(touching))


## A pose do motor chegou. Erro pequeno ao longo do rumo é latência (o corpo está à frente) e fica; o resto converge aos poucos.
func reconcile(server: Vector2) -> void:
	if pos == Vector2.INF or pos.distance_to(server) > SNAP_DISTANCE:
		place(server)
		return
	var error := server - pos
	var speed := velocity.length()
	if speed > 0.05:
		var forward := velocity / speed
		var along := error.dot(forward)
		var lead := speed * LEAD_ALLOWANCE
		error -= forward * clampf(along, -lead, 0.0)
	_correction = error
