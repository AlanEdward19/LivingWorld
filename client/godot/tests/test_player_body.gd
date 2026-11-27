extends "res://tests/test_base.gd"
## #229: o controlador local do corpo do jogador — aceleração e freio, parede, degrau, gente, reconciliação com o motor.

const PlayerBody := preload("res://Scenes/player_body.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")

const SPEED := 0.93 ## células/s (1,4 m/s com 1,5 m por célula): o que o motor diz, não um número do controlador
const DT := 1.0 / 60.0


func _body(at := Vector2(5.5, 5.5)) -> PlayerBody:
	var body := PlayerBody.new()
	body.place(at)
	return body


func test_accelerates_smoothly_and_brakes_without_stopping_dead() -> void:
	var body := _body()
	body.advance(Vector2.RIGHT, SPEED, DT)
	check(body.velocity.x > 0.0 and body.velocity.x < SPEED * 0.3, "o primeiro quadro já anda, mas sem pular para a velocidade cheia (%s)" % body.velocity.x)
	var last := body.velocity.x
	for i in 30:
		body.advance(Vector2.RIGHT, SPEED, DT)
		check(body.velocity.x >= last - 0.0001, "a velocidade só sobe até o teto")
		last = body.velocity.x
	check(is_equal_approx(body.velocity.x, SPEED), "chega à velocidade do motor (%s)" % body.velocity.x)
	body.advance(Vector2.ZERO, SPEED, DT)
	check(body.velocity.x > 0.0 and body.velocity.x < SPEED, "soltou a tecla: freia, não para seco")
	for i in 10:
		body.advance(Vector2.ZERO, SPEED, DT)
	eq(body.velocity, Vector2.ZERO, "para em menos de 0,2 s")
	check(not body.settling(), "parado e sem erro pendente")


func test_slides_along_a_wall_and_never_enters_it() -> void:
	var body := _body(Vector2(5.5, 5.5))
	body.set_neighborhood([[Rect2(7, 0, 1, 20), 2.0]], [])
	for i in 240: # 4 s empurrando na diagonal contra a parede
		body.advance(Vector2(1, 1).normalized(), SPEED, DT)
		check(body.pos.x <= 7.0 - body.radius + 0.001, "não atravessa a parede (x=%s)" % body.pos.x)
	check(body.pos.y > 7.0, "desliza ao longo da parede em vez de grudar (y=%s)" % body.pos.y)
	check(body.pos.x > 7.0 - body.radius - 0.05, "encostou nela (x=%s)" % body.pos.x)


func test_a_fast_frame_cannot_tunnel_through_a_thin_wall() -> void:
	var body := _body(Vector2(5.5, 5.5))
	body.set_neighborhood([[Rect2(6.0, 0, 0.1, 20), 2.0]], [])
	body.velocity = Vector2(20, 0)
	body.advance(Vector2.RIGHT, 20.0, 0.25) # quadro longo (travada): 5 células de uma vez
	check(body.pos.x <= 6.0 - body.radius + 0.001, "sub-passos seguram a parede fina (x=%s)" % body.pos.x)


func test_low_obstacles_are_steps_not_walls() -> void:
	var body := _body()
	body.set_neighborhood([[Rect2(6.5, 0, 1, 20), 0.1]], [])
	for i in 120:
		body.advance(Vector2.RIGHT, SPEED, DT)
	check(body.pos.x > 7.2, "degrau baixo é pisado por cima (x=%s)" % body.pos.x)


func test_people_push_softly_and_are_never_crossed() -> void:
	var body := _body()
	var other := Vector2(6.5, 5.5)
	body.set_neighborhood([], [other])
	var closest := 99.0
	var longest_jump := 0.0
	for i in 240:
		var before := body.pos
		body.advance(Vector2.RIGHT, SPEED, DT)
		closest = minf(closest, body.pos.distance_to(other))
		longest_jump = maxf(longest_jump, body.pos.distance_to(before))
	check(body.pos.x < other.x, "não atravessa a pessoa (x=%s)" % body.pos.x)
	check(closest > 0.3, "fica a uma distância de corpo (%s)" % closest)
	check(longest_jump <= SPEED * DT * 1.01 + 0.05, "o empurrão é macio, sem tranco (%s)" % longest_jump)


func test_a_pose_in_front_of_the_prediction_or_slightly_behind_does_not_pull_the_body_back() -> void:
	var body := _body()
	for i in 30:
		body.advance(Vector2.RIGHT, SPEED, DT)
	var x := body.pos.x
	body.reconcile(Vector2(x - SPEED * 0.2, 5.5)) # o motor atrasado pela latência normal
	for i in 6:
		body.advance(Vector2.RIGHT, SPEED, DT)
		check(body.pos.x >= x, "ao segurar W o corpo nunca volta para trás (x=%s)" % body.pos.x)
		x = body.pos.x


func test_reconciliation_converges_without_a_visible_jump() -> void:
	var body := _body()
	var server := Vector2(5.5, 6.1) # o motor corrigiu de lado (0,6 célula): abaixo do salto
	body.reconcile(server)
	var last := body.pos
	var biggest := 0.0
	for i in 60:
		body.advance(Vector2.ZERO, SPEED, DT)
		biggest = maxf(biggest, body.pos.distance_to(last))
		last = body.pos
	check(body.pos.distance_to(server) < 0.01, "converge ao motor em ~1 s (%s)" % body.pos.distance_to(server))
	check(biggest < 0.15, "nenhum quadro salta mais que uma fração do erro (%s)" % biggest)


func test_the_engine_refusing_pulls_the_body_back_smoothly() -> void:
	var body := _body()
	for i in 30:
		body.advance(Vector2.RIGHT, SPEED, DT)
	var stuck := Vector2(5.5, 5.5) # o motor não deixou andar: ficou onde estava
	body.stop()
	body.reconcile(stuck)
	for i in 90:
		body.advance(Vector2.ZERO, SPEED, DT)
	check(body.pos.distance_to(stuck) < 0.01, "volta à pose do motor (%s)" % body.pos.distance_to(stuck))


func test_a_big_error_snaps_to_the_engine() -> void:
	var body := _body()
	body.reconcile(Vector2(20.5, 5.5))
	eq(body.pos, Vector2(20.5, 5.5), "teleporte do motor: salta")
	eq(body.velocity, Vector2.ZERO)


func test_the_hud_predicts_with_the_body_and_stops_at_nearby_walls() -> void:
	var hud := PlayerHud.new()
	hud.set_process(false)
	hud._walk_busy = true # só a previsão: nada vai ao motor
	hud.fine = Vector2(5.5, 5.5)
	hud.speed_cells = SPEED
	var asked: Array = []
	hud.neighborhood = func(center: Vector2) -> Array:
		asked.append(center)
		return [[[Rect2(7, 0, 1, 20), 2.0]], []]
	var steps: Array = []
	hud.glided.connect(func(p: Vector2): steps.append(p))
	for i in 240:
		hud.glide(Vector2.RIGHT, DT)
	check(steps.size() > 0 and hud.fine.x <= 7.0 - PlayerBody.RADIUS + 0.001, "o corpo prevê e a parede do app o segura (x=%s)" % hud.fine.x)
	check(asked.size() >= 1 and asked.size() < 40, "a vizinhança é lida de vez em quando, não a cada quadro (%d)" % asked.size())
	hud.apply_pose({"fx": 6.0, "fy": 5.5, "speedMps": 1.4, "cellMeters": 1.5})
	check(hud.fine.distance_to(Vector2(6.0, 5.5)) < 1.0, "a pose do motor converge, sem teleporte")
	hud.free()
