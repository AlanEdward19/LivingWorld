extends Node
## #249: medida de continuidade da troca de escopo (só diagnóstico, ligada pelo passo `meter:`). Por trecho: quadros, tempo médio,
## pior quadro, quantos passaram de 100 ms, o maior salto da câmera entre quadros (metros) e quantos quadros o corpo do jogador
## ficou sem figura visível.

const Grid := preload("res://Scenes/grid.gd")

var app
var frames := 0
var total_ms := 0.0
var worst_ms := 0.0
var over_100 := 0
var over_50 := 0
var jump_worst := 0.0
var jump_at := 0.0
var rel_worst := 0.0 ## maior salto da câmera RELATIVO ao corpo seguido e ao giro (descontado o andar e o olhar do jogador)
var rel_at := 0.0
var _last_rel := Vector3.INF
var body_missing := 0
var slow: Array = [] ## [{ms, t}] de cada quadro > 100 ms no trecho
var _last_us := 0
var _last_cam := Vector3.INF
var _clock := 0.0
var _ignore := 0


func _ready() -> void:
	_last_us = Time.get_ticks_usec()
	reset()


func reset() -> void:
	frames = 0
	total_ms = 0.0
	worst_ms = 0.0
	over_100 = 0
	over_50 = 0
	jump_worst = 0.0
	rel_worst = 0.0
	_last_rel = Vector3.INF
	body_missing = 0
	slow = []
	_clock = 0.0
	_last_cam = Vector3.INF
	_last_us = Time.get_ticks_usec()


## Os próximos `count` quadros não entram na medida (captura de tela).
func ignore(count: int) -> void:
	_ignore = count


func _process(_delta: float) -> void:
	var now := Time.get_ticks_usec()
	var ms := (now - _last_us) / 1000.0
	_last_us = now
	if _ignore > 0:
		_ignore -= 1
		return
	frames += 1
	total_ms += ms
	_clock += ms / 1000.0
	worst_ms = maxf(worst_ms, ms)
	if ms > 100.0:
		over_100 += 1
		slow.append({"ms": snappedf(ms, 0.1), "t": snappedf(_clock, 0.01)})
	if ms > 50.0:
		over_50 += 1
	if app == null:
		return
	var cam: Camera3D = app.rig.camera
	if cam != null and cam.is_inside_tree():
		if _last_cam != Vector3.INF:
			var jump := cam.global_position.distance_to(_last_cam) * Grid.cell_meters
			if jump > jump_worst:
				jump_worst = jump
				jump_at = _clock
		_last_cam = cam.global_position
		var followed = app.rig.following
		if followed != null and is_instance_valid(followed) and followed.is_inside_tree():
			var rel: Vector3 = Basis(Vector3.UP, -app.rig.yaw) * (cam.global_position - followed.global_position)
			if _last_rel != Vector3.INF and rel.distance_to(_last_rel) * Grid.cell_meters > rel_worst:
				rel_worst = rel.distance_to(_last_rel) * Grid.cell_meters
				rel_at = _clock
			_last_rel = rel
		else:
			_last_rel = Vector3.INF
	if app.player_hud != null and app.player_hud.is_active:
		var own = app.player_actor()
		if own == null or not is_instance_valid(own) or not own.is_visible_in_tree():
			body_missing += 1


func report(label: String) -> String:
	return "meter[%s]: quadros=%d medio=%.1fms pior=%.1fms >100ms=%d >50ms=%d salto_camera_max=%.2fm(t=%.2f) salto_relativo_max=%.2fm(t=%.2f) sem_figura=%d lentos=%s" % [
		label, frames, total_ms / maxf(float(frames), 1.0), worst_ms, over_100, over_50, jump_worst, jump_at, rel_worst, rel_at, body_missing, slow]
