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


func test_a_person_ahead_is_slipped_past_softly_never_a_wall() -> void:
	var body := _body()
	var other := Vector2(6.5, 5.5)
	body.set_neighborhood([], [other])
	var longest_jump := 0.0
	for i in 240:
		var before := body.pos
		body.advance(Vector2.RIGHT, SPEED, DT)
		longest_jump = maxf(longest_jump, body.pos.distance_to(before))
	check(body.pos.x > other.x + 0.5, "passa pela pessoa em vez de parar nela (x=%s)" % body.pos.x)
	check(longest_jump <= SPEED * DT * 1.01 + 0.05, "o desvio é macio, sem tranco (%s)" % longest_jump)


## #228: ~20 pessoas em volta (3 por célula): segurar W e Shift+W continua avançando; paredes seguem sólidas.
func test_a_crowd_slows_the_body_but_never_blocks_it() -> void:
	for run in [1.0, 1.6]:
		var body := _body(Vector2(10.5, 10.5))
		var crowd: Array = []
		for i in 21: # 7 células com 3 por célula, em volta do caminho
			crowd.append(Vector2(11.0 + float(i / 3), 10.5 + (float(i % 3) - 1.0) * 0.3))
		body.set_neighborhood([], crowd)
		for i in 150: # 2,5 s
			body.advance(Vector2.RIGHT, SPEED * run, DT)
		check(body.pos.x - 10.5 > SPEED * run * 2.5 * 0.4, "avança na multidão (correr %s: %s células)" % [run, body.pos.x - 10.5])
	var walled := _body(Vector2(10.5, 10.5))
	walled.set_neighborhood([[Rect2(11.5, 9.0, 0.3, 3.0), 2.0]], [Vector2(11.0, 10.5), Vector2(11.0, 10.8)])
	for i in 150:
		walled.advance(Vector2.RIGHT, SPEED, DT)
	check(walled.pos.x < 11.5, "a parede segue sólida (x=%s)" % walled.pos.x)


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


## #226: o `solid` do prédio no mundo são painéis finos do contorno (não células inteiras) e a porta continua passável.
func test_a_world_building_solid_is_thin_panels_and_the_door_passes() -> void:
	var WorldView := preload("res://Scenes/world_view.gd")
	var view := WorldView.new()
	var footprint: Array = []
	for y in 4:
		for x in 4:
			var material := 3
			if x == 0 or y == 0 or x == 3 or y == 3:
				material = 1
			if x == 1 and y == 3:
				material = 2
			footprint.append({"cell": Vector2i(x, y), "material": material})
	var node: Node3D = view._make_building({"id": 1, "type": -1, "cell": Vector2i(20, 20), "floors": 1, "orientation": 0, "footprint": footprint})
	var solid: Array = node.get_meta("solid")
	check(not solid.is_empty(), "há paredes sólidas")
	var covered := 0.0
	var obstacles: Array = []
	for wall: AABB in solid:
		check(minf(wall.size.x, wall.size.z) < 0.3, "painel fino (%s)" % wall.size)
		covered += wall.size.x * wall.size.z
		obstacles.append([Rect2(wall.position.x, wall.position.z, wall.size.x, wall.size.z), wall.size.y])
	check(covered < 16.0 * 0.4, "o contorno ocupa menos de 40%% da planta de 4x4 (%s)" % covered)
	var body := _body(Vector2(21.5, 23.5)) # dentro, junto da porta (célula 1,3)
	body.set_neighborhood(obstacles, [])
	for i in 120:
		body.advance(Vector2.DOWN, 2.0, DT)
	check(body.pos.y > 24.5, "sai pela porta, que não é parede (y=%s)" % body.pos.y)
	body.place(Vector2(22.5, 23.5))
	for i in 120:
		body.advance(Vector2.DOWN, 2.0, DT)
	check(body.pos.y < 24.0, "ao lado da porta o painel segura (y=%s)" % body.pos.y)
	view.free()
