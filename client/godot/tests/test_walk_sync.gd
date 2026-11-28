extends "res://tests/test_base.gd"
## #248: andar sem voltar — o corpo prevê com a regra do chão do motor (máscara de passos) e reconcilia reaplicando o que o motor ainda não viu.

const PlayerBody := preload("res://Scenes/player_body.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const GroundGrid := preload("res://Scenes/ground_grid.gd")

const SPEED := 0.93 ## células/s
const DT := 1.0 / 60.0
const SIDE := 80


## Uma janela de chão como o motor a daria: tudo aberto, menos as células de `walls` (Vector2i -> true); a diagonal não corta canto de parede.
## `doors`: {Vector2i porta: Vector2i soleira}: o passo entre a casa e a rua só vale entre os dois.
func _grid(walls: Dictionary, origin := Vector2i.ZERO) -> GroundGrid:
	var mask: Array = []
	for j in SIDE:
		for i in SIDE:
			var from := origin + Vector2i(i, j)
			var bits := 0
			for dy in range(-1, 2):
				for dx in range(-1, 2):
					if dx == 0 and dy == 0:
						continue
					var to := from + Vector2i(dx, dy)
					if walls.has(to):
						continue
					if dx != 0 and dy != 0 and (walls.has(from + Vector2i(dx, 0)) or walls.has(from + Vector2i(0, dy))):
						continue # canto de parede
					bits |= 1 << ((dy + 1) * 3 + (dx + 1))
			mask.append(bits)
	return GroundGrid.from_body({"x": origin.x, "y": origin.y, "size": SIDE, "floor": 0, "buildingId": null, "crowdLimit": 3, "mask": mask})


func _body(grid: GroundGrid, at: Vector2) -> PlayerBody:
	var body := PlayerBody.new()
	body.ground = grid
	body.place(at)
	return body


func test_the_grid_reads_the_engine_mask_bits_and_refuses_outside_the_window() -> void:
	var grid := _grid({Vector2i(41, 40): true})
	check(not grid.can_step(Vector2i(40, 40), Vector2i(41, 40)), "a parede barra o passo")
	check(grid.can_step(Vector2i(40, 40), Vector2i(40, 41)), "o chão deixa")
	check(not grid.can_step(Vector2i(40, 40), Vector2i(41, 41)), "a diagonal não corta o canto da parede")
	check(not grid.can_step(Vector2i(-5, 0), Vector2i(-4, 0)), "fora da janela não passa (pede outra janela antes)")
	check(grid.covers(Vector2i(40, 40), 3) and not grid.covers(Vector2i(2, 40), 3), "a folga até a borda diz quando pedir outra janela")


func test_the_body_stops_at_the_engine_wall_cell_and_slides_along_it() -> void:
	var walls := {}
	for y in range(30, 50):
		walls[Vector2i(45, y)] = true
	var body := _body(_grid(walls), Vector2(42.5, 40.5))
	for i in 600: # 10 s empurrando na diagonal para a parede
		body.advance(Vector2(1, 1).normalized(), SPEED, DT)
		check(body.pos.x < 45.0, "nunca entra na célula de parede (x=%s)" % body.pos.x)
	check(body.pos.x > 44.7, "encosta na parede (x=%s)" % body.pos.x)
	check(body.pos.y > 45.0, "desliza pelo eixo livre (y=%s)" % body.pos.y)


func test_the_body_speed_is_the_engines_not_a_ramp() -> void:
	var body := _body(_grid({}), Vector2(40.5, 40.5))
	body.advance(Vector2.RIGHT, SPEED, DT)
	check(is_equal_approx(body.pos.x, 40.5 + SPEED * DT), "o primeiro quadro já anda na velocidade do motor (%s)" % body.pos.x)
	body.advance(Vector2.ZERO, SPEED, DT)
	eq(body.velocity, Vector2.ZERO, "soltou: para, como o motor")


func test_a_full_cell_of_people_blocks_the_step_like_the_engine_and_a_lighter_one_does_not() -> void:
	var body := _body(_grid({}), Vector2(40.5, 40.5))
	body.set_neighborhood([], [Vector2(41.5, 40.5), Vector2(41.4, 40.6), Vector2(41.6, 40.4)]) # 3 = o limite
	for i in 120:
		body.advance(Vector2.RIGHT, SPEED, DT)
	check(body.pos.x < 41.0, "célula cheia barra (x=%s)" % body.pos.x)
	body.set_neighborhood([], [Vector2(41.5, 40.5), Vector2(41.4, 40.6)]) # 2: cabe mais um
	for i in 120:
		body.advance(Vector2.RIGHT, SPEED, DT)
	check(body.pos.x > 41.5, "célula com folga deixa passar, sem desvio nem lentidão própria (x=%s)" % body.pos.x)


func test_replay_walks_the_unseen_commands_with_the_same_floor_rule() -> void:
	var walls := {}
	for y in range(30, 50):
		walls[Vector2i(45, y)] = true
	var body := _body(_grid(walls), Vector2(40.5, 40.5))
	var there := body.replay(Vector2(43.0, 40.5), [[Vector2.RIGHT, SPEED, 1.0], [Vector2.DOWN, SPEED, 1.0]])
	check(there.x < 45.0 and there.y > 41.0, "a reaplicação vira com o comando seguinte e respeita a parede (%s)" % there)
	eq(body.replay(Vector2(43.0, 40.5), []), Vector2(43.0, 40.5), "nada novo: a pose do motor")


func test_a_replayed_target_pulls_smoothly_and_a_teleport_snaps() -> void:
	var body := _body(_grid({}), Vector2(40.5, 40.5))
	body.reconcile_to(Vector2(40.6, 40.5)) # 0,15 m: absorvido aos poucos
	var worst := 0.0
	for i in 60:
		var before := body.pos
		body.advance(Vector2.ZERO, SPEED, DT)
		worst = maxf(worst, body.pos.distance_to(before))
	check(body.pos.distance_to(Vector2(40.6, 40.5)) < 0.005, "converge (%s)" % body.pos)
	check(worst < 0.03, "sem salto num quadro (%s)" % worst)
	body.reconcile_to(Vector2(60.5, 40.5))
	eq(body.pos, Vector2(60.5, 40.5), "teleporte do motor salta")


# --- o laço inteiro: um motor simulado (tick de 50 ms, comando por rede com latência) e o HUD de verdade ---

class FakeApi:
	extends RefCounted
	signal delivered(token, response)
	var sent: Array = []
	var next_token := 0

	func player_walk(_player_id, dx, dy, run, wseq) -> Dictionary:
		next_token += 1
		var token := next_token
		sent.append({"token": token, "dx": dx, "dy": dy, "run": run, "seq": wseq})
		while true:
			var got = await delivered
			if int(got[0]) == token:
				return got[1]
		return {}

	func player_ground(_player_id, _radius) -> Dictionary:
		return {"ok": false, "status": 500, "body": null, "errors": []}


## O que o motor faz: ticks de 50 ms, o rumo mantido anda o corpo sozinho (como `PlayerWalk.Pump`), o comando só muda o rumo.
class FakeEngine:
	extends RefCounted
	var body
	var heading := Vector2.ZERO
	var applied := 0
	var speed := 0.93
	var ticked := 0.0 ## s do último tick

	func advance_to(t: float) -> void:
		while ticked + 0.05 <= t + 0.0000001:
			ticked += 0.05
			if heading != Vector2.ZERO:
				body.pos = body.walk_through(body.pos, heading, speed * 0.05)

	func apply(dx: int, dy: int, seq: int) -> void:
		var raw := Vector2(dx, dy)
		heading = raw.normalized() if raw != Vector2.ZERO else Vector2.ZERO
		applied = seq

	func pose() -> Dictionary:
		return {"fx": body.pos.x, "fy": body.pos.y, "speedMps": speed * 1.5, "cellMeters": 1.5, "floor": 0, "seq": applied, "running": false}


## Uma sequência fixa de rumos (segundos, ângulo em graus ou -1 parado), inclusive curvas suaves como a câmera girando.
func _script() -> Array:
	return [[3.0, 0.0], [2.0, 90.0], [1.5, 135.0], [0.6, -1.0], [4.0, 20.0], [3.0, 200.0], [2.0, 330.0], [0.5, -1.0], [5.0, 45.0],
		[2.5, 270.0], [3.0, 180.0], [0.4, -1.0], [4.0, 300.0], [2.0, 0.0], [3.5, 90.0]]


func _direction_at(seconds: float) -> Vector2:
	var t := fmod(seconds, 34.0) # o roteiro dura ~34 s e se repete (3 min = 5 voltas)
	for entry in _script():
		if t < float(entry[0]):
			if float(entry[1]) < 0.0:
				return Vector2.ZERO
			var drift: float = 25.0 * t if int(entry[1]) % 2 == 1 else 0.0 # alguns trechos giram de leve (câmera)
			return Vector2.RIGHT.rotated(deg_to_rad(float(entry[1]) + drift))
		t -= float(entry[0])
	return Vector2.ZERO


## Anda `seconds` com o laço completo (HUD, rede com latência de 5 a 45 ms por perna, motor de 50 ms) e devolve a medida.
func _simulate(seconds: float, walls: Dictionary, jitter: bool) -> Dictionary:
	var grid := _grid(walls)
	var hud := PlayerHud.new()
	hud.set_process(false)
	var api := FakeApi.new()
	hud.api = api
	hud.player_id = "p"
	hud.is_active = true
	var now := [0] # ms simulados
	hud.clock = func(): return now[0]
	var engine := FakeEngine.new()
	engine.body = PlayerBody.new()
	engine.body.ground = grid
	engine.body.place(Vector2(20.5, 20.5))
	hud.fine = Vector2(20.5, 20.5)
	hud.body.ground = grid
	hud.ground = grid
	hud.body.place(hud.fine)
	hud.speed_cells = SPEED
	var arriving: Array = [] # [t chegada ao motor, dx, dy, seq, owd de volta]
	var replying: Array = [] # [t chegada da resposta, token, pose]
	var rng := RandomNumberGenerator.new()
	rng.seed = 248
	var t := 0.0
	var seen := 0
	var gap_worst := 0.0
	var gap_sum := 0.0
	var gap_n := 0
	while t < seconds:
		t += DT
		now[0] = int(round(t * 1000.0))
		# respostas que chegam ao cliente
		for reply in replying.duplicate():
			if float(reply[0]) <= t:
				replying.erase(reply)
				api.delivered.emit(reply[1], {"ok": true, "status": 200, "body": reply[2]})
		# comandos que chegam ao motor (depois dos ticks até o instante deles)
		for command in arriving.duplicate():
			if float(command[0]) <= t:
				arriving.erase(command)
				engine.advance_to(float(command[0]))
				engine.apply(int(command[1]), int(command[2]), int(command[3]))
				replying.append([float(command[0]) + float(command[4]), command[5], engine.pose()])
		engine.advance_to(t)
		hud.glide(_direction_at(t), DT)
		while seen < api.sent.size():
			var cmd: Dictionary = api.sent[seen]
			seen += 1
			var out := rng.randf_range(0.005, 0.045) if jitter else 0.02
			var back := rng.randf_range(0.005, 0.045) if jitter else 0.02
			arriving.append([t + out, cmd.dx, cmd.dy, cmd.seq, back, cmd.token])
		if hud.fine != Vector2.INF:
			var gap := hud.fine.distance_to(engine.body.pos) * 1.5 # m
			gap_worst = maxf(gap_worst, gap)
			gap_sum += gap
			gap_n += 1
	var stats: Dictionary = hud.walk_stats.duplicate()
	stats["gap_worst_m"] = gap_worst
	stats["gap_mean_m"] = gap_sum / maxf(float(gap_n), 1.0)
	stats["sent"] = api.sent.size()
	hud.free()
	return stats


## A cena do dono: andar de verdade com latência de rede e o motor andando sozinho. Alvo: nenhum puxão (> 0,3 m) e o corpo na tela a poucos
## centímetros do corpo do motor, em reta, curva e batendo em parede.
func test_three_minutes_of_walking_never_pulls_the_body_more_than_thirty_centimetres() -> void:
	var walls := {}
	for y in range(5, 35):
		walls[Vector2i(30, y)] = true # uma parede longa na rota
	for x in range(10, 25):
		walls[Vector2i(x, 28)] = true
	var stats := _simulate(180.0, walls, true)
	print("walk-sync: ", stats)
	eq(int(stats.pulls), 0, "nenhum quadro puxa o corpo mais de 0,3 m (pior %s m)" % stats.worst_m)
	eq(int(stats.snaps), 0, "nenhum salto de reconciliação")
	check(float(stats.gap_worst_m) < 0.4, "a tela nunca se afasta do motor mais que 0,4 m (pior %s)" % stats.gap_worst_m)
	check(float(stats.gap_mean_m) < 0.12, "e em média fica a poucos centímetros (%s m)" % stats.gap_mean_m)


func test_the_hud_sends_numbered_commands_and_a_turn_goes_out_at_once() -> void:
	var hud := PlayerHud.new()
	hud.set_process(false)
	var api := FakeApi.new()
	hud.api = api
	hud.player_id = "p"
	hud.is_active = true
	hud.fine = Vector2(5.5, 5.5)
	hud.speed_cells = SPEED
	hud.glide(Vector2.RIGHT, DT)
	check(api.sent.size() == 1 and int(api.sent[0].seq) == 1 and api.sent[0].dx == 1000, "o primeiro rumo sai numerado")
	api.delivered.emit(1, {"ok": true, "status": 200, "body": {"fx": 5.5, "fy": 5.5, "speedMps": 1.4, "cellMeters": 1.5, "seq": 1}})
	hud.glide(Vector2.DOWN, DT) # virou: não espera os 100 ms
	check(api.sent.size() == 2 and int(api.sent[1].seq) == 2 and api.sent[1].dy == 1000, "a curva sai na hora, com o número seguinte")
	api.delivered.emit(2, {"ok": true, "status": 200, "body": {"fx": 5.5, "fy": 5.5, "speedMps": 1.4, "cellMeters": 1.5, "seq": 2}})
	hud.glide(Vector2.DOWN, DT)
	check(api.sent.size() == 2, "o mesmo rumo só é renovado a cada 0,1 s")
	hud.free()
