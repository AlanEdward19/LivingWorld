extends "res://tests/test_base.gd"
## #232: retorno a cada ação — síntese determinística, passos na passada, recusa muda, pool fixo, tremor que decai.

const Feedback := preload("res://Scenes/feedback.gd")
const Synth := preload("res://Scenes/feedback_synth.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")


func _feedback() -> Feedback:
	var fb := Feedback.new()
	tree.root.add_child(fb) # _ready monta vozes, poeira e rótulos
	return fb


func test_synth_makes_nonempty_deterministic_buffers() -> void:
	for kind in Synth.KINDS:
		var a := Synth.samples(kind)
		check(a.size() > 500, "%s não é vazio" % kind)
		eq(hash(a), hash(Synth.samples(kind)),"%s dá sempre o mesmo buffer" % kind)
		var peak := 0.0
		for v in a:
			peak = maxf(peak, absf(v))
		check(peak > 0.05 and peak <= 1.0, "%s audível e sem estourar (%s)" % [kind, peak])
		eq(Synth.stream(kind).data.size(), a.size() * 2, "%s vira WAV de 16 bits" % kind)
	check(hash(Synth.samples("step_stone")) != hash(Synth.samples("step_grass")),"cada chão soa diferente")
	eq(Synth.step_kind("gravel"), "step_gravel", "chão conhecido")
	eq(Synth.step_kind("lava"), "step_grass", "chão desconhecido cai na grama")


func test_steps_follow_the_stride_phase() -> void:
	var fb := _feedback()
	var steps := 0
	var phase := 0.3
	fb.step_tick(phase, 1.0, 0.0, Vector3.ZERO) # primeira leitura só marca onde a passada está
	for i in 400: # ~ 2 ciclos e meio da passada, em passos pequenos de fase
		phase += 0.03
		if fb.step_tick(phase, 1.0, 0.0, Vector3.ZERO):
			steps += 1
	var expected := Feedback.step_of(phase) - Feedback.step_of(0.3)
	eq(steps, expected, "um som por pé que toca o chão (a cada PI rad)")
	check(fb.played.all(func(k): return k == "step_grass"), "o chão padrão é grama")
	var before := fb.played.size()
	fb.step_tick(phase + 0.5, 0.1, 0.0, Vector3.ZERO) # parado (peso da passada baixo): sem som
	fb.step_tick(phase + 0.5 + PI, 0.1, 0.0, Vector3.ZERO)
	eq(fb.played.size(), before, "sem passada não há passo")
	fb.surface_at = func(_p: Vector3) -> String: return "stone"
	fb.step_tick(0.0, 1.0, 0.0, Vector3.ZERO)
	fb.step_tick(PI + 0.01, 1.0, 0.0, Vector3.ZERO)
	eq(fb.played[fb.played.size() - 1], "step_stone", "o som segue o tipo de chão")
	fb.queue_free()


func test_nothing_plays_for_a_refusal_except_the_quiet_deny() -> void:
	var fb := _feedback()
	fb.on_refused()
	eq(fb.played, ["deny"], "recusa: só o som discreto")
	eq(fb.puff_log, 0, "recusa não levanta poeira")
	eq(fb.floaters_shown, 0, "recusa não mostra número")
	var shakes := []
	fb.shake_cb = func(a): shakes.append(a)
	fb.on_refused()
	eq(shakes, [], "recusa não treme a câmera")
	fb.on_acted("walk") # verbo sem som: nada toca
	eq(fb.played.size(), 2, "verbo sem retorno sonoro não toca nada")
	fb.on_acted("buy")
	eq(fb.played[fb.played.size() - 1], "coin", "compra confirmada tilinta")
	fb.queue_free()


func test_volume_falls_with_distance() -> void:
	check(is_equal_approx(Feedback.gain_db(1.0), 0.0), "perto: cheio")
	check(Feedback.gain_db(8.0) < Feedback.gain_db(4.0), "mais longe, mais baixo")
	check(is_equal_approx(Feedback.gain_db(8.0), -12.0), "-6 dB por dobro (2 m -> 8 m = 2 dobros)")
	eq(Feedback.gain_db(30.0), -80.0, "longe demais: mudo")
	var fb := _feedback()
	fb.listener_pos = func() -> Vector3: return Vector3.ZERO
	fb.play("hit", Vector3(100, 0, 100))
	eq(fb.played.size(), 0, "som distante nem é pedido")
	fb.queue_free()


func test_particle_and_label_pools_do_not_grow() -> void:
	var fb := _feedback()
	var sizes := fb.pool_sizes()
	for i in 200:
		fb.puff(Vector3(i, 0, 0), 1.0)
		fb.on_struck_hit(-1)
	eq(fb.pool_sizes(), sizes, "vozes, poeira e números têm tamanho fixo")
	var particles := 0
	for child in fb.get_children():
		if child is CPUParticles3D:
			particles += 1
	eq(particles, Feedback.PUFFS, "o pool de poeira continua do mesmo tamanho")
	fb.queue_free()


func test_sheet_changes_show_only_after_a_confirmed_action() -> void:
	var fb := _feedback()
	fb.note_sheet({"money": 50, "hunger": 40.0, "health": 90.0})
	eq(fb.note_sheet({"money": 50, "hunger": 30.0, "health": 90.0}), 0, "fome que passa sozinha não vira número")
	fb.on_acted("eat")
	var shown := fb.note_sheet({"money": 38, "hunger": 35.0, "health": 90.0})
	eq(shown, 2, "−12 dinheiro e +5 comida")
	var lines := Feedback.sheet_changes({"money": 50, "hunger": 30.0}, {"money": 38, "hunger": 35.0})
	eq(lines[0].text, "−12 dinheiro", "perda de dinheiro")
	eq(lines[1].text, "+5 comida", "ganho de comida")
	eq(Feedback.sheet_changes({"health": 90.0}, {"health": 90.4}).size(), 0, "ruído pequeno não aparece")
	fb.queue_free()


func test_jump_landing_waits_for_the_air_time_and_shakes_only_when_high() -> void:
	var fb := _feedback()
	var shakes := []
	fb.shake_cb = func(a): shakes.append(a)
	fb.on_jumped(0.3)
	fb.on_jumped(0.8)
	fb._process(0.1)
	eq(fb.played.count("land"), 0, "ainda no ar")
	fb._process(0.3)
	eq(fb.played.count("land"), 1, "o pulo curto pousou")
	eq(shakes.size(), 0, "pulo curto não treme")
	fb._process(0.6)
	eq(fb.played.count("land"), 2, "o alto pousou")
	eq(shakes.size(), 1, "pouso forte treme")
	fb.queue_free()


func test_camera_shake_decays_without_fps_dependence() -> void:
	var rig := CameraRig.new()
	rig.set_mode(CameraRig.THIRD)
	rig.add_shake(0.8)
	check(rig.shake_amount > 0.0, "tremor entrou")
	var a := CameraRig.new()
	a.set_mode(CameraRig.THIRD)
	a.add_shake(0.8)
	for i in 30: # meio segundo a 60 FPS
		rig.step_shake(1.0 / 60.0)
	for i in 15: # o mesmo meio segundo a 30 FPS
		a.step_shake(1.0 / 30.0)
	check(is_equal_approx(rig.shake_amount, a.shake_amount), "mesmo tempo, mesmo resíduo em qualquer FPS")
	eq(rig.shake_amount, 0.0, "decai rápido (< 0,5 s)")
	eq(rig.shake_offset, Vector3.ZERO, "sem resíduo de deslocamento")
	rig.shake_enabled = false
	rig.add_shake(1.0)
	eq(rig.shake_amount, 0.0, "opção desligada: nenhum tremor")
	var iso := CameraRig.new()
	iso.add_shake(1.0)
	eq(iso.shake_amount, 0.0, "isométrica não treme")
	rig.free()
	a.free()
	iso.free()
