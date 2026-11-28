extends Node
## #232: retorno a cada ação do jogador. Só nasce do que o MOTOR confirmou (verbo aceito, golpe aceito, pulo aceito, ficha
## que mudou depois de uma ação); recusa tem o próprio som discreto e nunca toca o som da ação. Tudo por código: sons
## (feedback_synth.gd), poeira num pool fixo de partículas, números subindo na tela e tremor da câmera (camera_rig.gd).
## Só o corpo do jogador faz passo; nada por NPC distante. Volume pelo barramento "SFX" (Opções) e pela distância.

const Synth := preload("res://Scenes/feedback_synth.gd")
const Grid := preload("res://Scenes/grid.gd")

const VOICES := 6
const PUFFS := 4 ## partículas de poeira/impacto: o pool nunca cresce
const FLOATERS := 6
const DAMAGE_LABELS := 4 ## #234: números de dano sobre o alvo (pool fixo)
const DAMAGE_S := 0.9
const FLOAT_S := 1.3
const ARM_S := 4.0 ## depois de uma ação aceita, as mudanças da ficha viram números por este tempo
const STRONG_AIR_S := 0.55 ## pulo mais longo que isto estremece a câmera no pouso
const FULL_DB_M := 2.0 ## até aqui, volume cheio; depois cai 6 dB a cada dobro da distância
const SILENT_M := 24.0
const SOUND_BY_VERB := {"enter": "door", "exit": "door", "buy": "coin", "sell": "coin", "eat": "eat", "drink": "drink"}
const SHEET_KEYS := [["health", "", "saúde", Color("e8665a")], ["hunger", "", "comida", Color("e0a43a")],
	["thirst", "", "água", Color("4aa3df")], ["sleep", "", "sono", Color("9b7fd1")]]
const MIN_DELTA := 1.0 ## diferenças menores que isto são ruído da ficha, não retorno

var settings ## GameSettings (nulo nos testes): volume dos efeitos
var body_node: Callable = func() -> Node: return null ## o corpo encarnado (npc_actor)
var node_of: Callable = func(_id: int) -> Node: return null ## NPC por id (alvo do golpe)
var surface_at: Callable = func(_pos: Vector3) -> String: return "grass" ## chão sob o ponto
var resource_name_of: Callable = func(id: int) -> String: return "recurso %d" % id ## #243: nome do recurso para "+1 Madeira" (o app liga o catálogo)
var listener_pos: Callable = func() -> Vector3: return Vector3.ZERO
var shake_cb: Callable = func(_amount: float) -> void: pass
var played: Array[String] = [] ## últimos sons pedidos (testes); só entra o que passaria do silêncio
var puff_log := 0 ## poeiras emitidas (testes)
var floaters_shown := 0
var suppressed := false: ## #240: popup aberto: nenhum número flutua (os que subiam somem; os novos não aparecem)
	set(value):
		suppressed = value
		if value:
			for i in _dmg_labels.size():
				_dmg_labels[i].visible = false
				_dmg_ages[i] = DAMAGE_S
			for i in _labels.size():
				_labels[i].visible = false
				_ages[i] = FLOAT_S
var damage_shown: Array = [] ## #234: números de dano mostrados {amount, own} (testes)
var _dmg_labels: Array[Label3D] = []
var _dmg_ages: Array[float] = []
var _dmg_base: Array[float] = []
var _dmg_next := 0
var _streams: Dictionary = {}
var _voices: Array[AudioStreamPlayer] = []
var _next_voice := 0
var _puffs: Array[CPUParticles3D] = []
var _next_puff := 0
var _labels: Array[Label] = []
var _ages: Array[float] = []
var _next_label := 0
var _step_index := -1
var _lands: Array[float] = [] ## tempo que falta para cada pouso confirmado
var _land_air: Array[float] = []
var _armed := 0.0
var _last_sheet: Dictionary = {}


func _ready() -> void:
	if AudioServer.get_bus_index("SFX") < 0:
		AudioServer.add_bus()
		var index := AudioServer.bus_count - 1
		AudioServer.set_bus_name(index, "SFX")
		AudioServer.set_bus_send(index, "Master")
	if settings != null:
		settings.apply_sfx()
	for kind in Synth.KINDS:
		_streams[kind] = Synth.stream(kind)
	for i in VOICES:
		var voice := AudioStreamPlayer.new()
		voice.bus = "SFX"
		add_child(voice)
		_voices.append(voice)
	var puff_mesh := QuadMesh.new()
	puff_mesh.size = Vector2(0.18, 0.18)
	var material := StandardMaterial3D.new()
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	material.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.vertex_color_use_as_albedo = true
	puff_mesh.material = material
	for i in PUFFS:
		var puff := CPUParticles3D.new()
		puff.emitting = false
		puff.one_shot = true
		puff.amount = 8
		puff.lifetime = 0.55
		puff.explosiveness = 1.0
		puff.mesh = puff_mesh
		puff.direction = Vector3.UP
		puff.spread = 70.0
		puff.initial_velocity_min = 0.3
		puff.initial_velocity_max = 0.9
		puff.gravity = Vector3(0, -0.6, 0)
		puff.color = Color(0.72, 0.66, 0.55, 0.55)
		puff.scale_amount_min = 0.5
		puff.scale_amount_max = 1.1
		add_child(puff)
		_puffs.append(puff)
	for i in DAMAGE_LABELS:
		var label := Label3D.new()
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		label.pixel_size = 0.008
		label.font_size = 56
		label.outline_size = 14
		label.no_depth_test = true
		label.visible = false
		add_child(label)
		_dmg_labels.append(label)
		_dmg_ages.append(DAMAGE_S)
		_dmg_base.append(0.0)
	var layer := CanvasLayer.new()
	layer.layer = 5
	add_child(layer)
	for i in FLOATERS:
		var label := Label.new()
		label.visible = false
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		label.add_theme_font_size_override("font_size", 24)
		label.add_theme_constant_override("outline_size", 6)
		label.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.85))
		layer.add_child(label)
		_labels.append(label)
		_ages.append(FLOAT_S)


func _process(delta: float) -> void:
	_armed = maxf(_armed - delta, 0.0)
	var body = body_node.call()
	if body != null and is_instance_valid(body) and body.has_method("step_state"):
		var state: Array = body.step_state()
		if not state.is_empty():
			step_tick(float(state[0]), float(state[1]), float(state[2]), body.position)
	for i in range(_lands.size() - 1, -1, -1):
		_lands[i] -= delta
		if _lands[i] <= 0.0:
			var air := _land_air[i]
			_lands.remove_at(i)
			_land_air.remove_at(i)
			_land(air)
	for i in _dmg_labels.size():
		if _dmg_ages[i] >= DAMAGE_S:
			continue
		_dmg_ages[i] += delta
		var d := _dmg_ages[i] / DAMAGE_S
		_dmg_labels[i].position.y = _dmg_base[i] + 0.7 * d
		_dmg_labels[i].modulate.a = 1.0 - smoothstep(0.5, 1.0, d)
		if _dmg_ages[i] >= DAMAGE_S:
			_dmg_labels[i].visible = false
	var size := get_viewport().get_visible_rect().size if is_inside_tree() else Vector2(1280, 720)
	for i in FLOATERS:
		if _ages[i] >= FLOAT_S:
			continue
		_ages[i] += delta
		var k := _ages[i] / FLOAT_S
		_labels[i].position = Vector2(size.x * 0.5 + (i - 2.5) * 70.0 - _labels[i].size.x * 0.5, size.y * 0.62 - 90.0 * k)
		_labels[i].modulate.a = 1.0 - smoothstep(0.6, 1.0, k)
		if _ages[i] >= FLOAT_S:
			_labels[i].visible = false


## Superfície da rua do motor (0 trilha, 1 terra batida, 2 cascalho, 3 pedra); sem rua, grama.
static func surface_name(surface: int) -> String:
	return ["grass", "dirt", "gravel", "stone"][surface] if surface >= 0 and surface <= 3 else "grass"


## ---- lógica pura (testável) ---------------------------------------------------------------------------------------

## Volume (dB) pela distância em metros: cheio de perto, -6 dB por dobro, mudo longe.
static func gain_db(distance_m: float) -> float:
	if distance_m >= SILENT_M:
		return -80.0
	return -6.0 * log(maxf(distance_m, FULL_DB_M) / FULL_DB_M) / log(2.0)


## Índice do passo na fase da passada (um por pé, a cada PI rad).
static func step_of(phase: float) -> int:
	return int(floor(phase / PI))


## Tempo dos passos: chamada a cada quadro com a fase do rig do corpo. Soa quando o índice avança com a passada de pé;
## a fase que volta (a volta do ciclo do rig) só reajusta o contador. Devolve se houve passo.
func step_tick(phase: float, gait: float, run: float, pos: Vector3) -> bool:
	var index := step_of(phase)
	var advanced := _step_index >= 0 and index == _step_index + 1
	_step_index = index
	if not advanced or gait < 0.35:
		return false
	var kind := Synth.step_kind(String(surface_at.call(pos)))
	play(kind, pos, 0.0 if run < 0.5 else 2.5)
	if run >= 0.6:
		puff(pos, 0.5)
	return true


## ---- o que o motor confirmou --------------------------------------------------------------------------------------

func on_acted(verb: String) -> void:
	_armed = ARM_S
	if SOUND_BY_VERB.has(verb):
		play(String(SOUND_BY_VERB[verb]), _body_pos())


## O golpe foi aceito pelo motor: impacto no alvo, poeira e um tranco curto (sem número: o dano só vem com a resposta).
func on_struck_hit(target: int) -> void:
	on_struck_result(target, {"outcome": "Hit"})


## #234: o resultado que o motor devolveu ao golpe (`outcome`, `damage`, `lethal`). O número de dano sai SOBRE O ALVO só com
## acerto confirmado e dano real; esquiva e bloqueio têm som e efeito próprios e nenhum número.
func on_struck_result(target: int, body: Dictionary) -> void:
	_armed = ARM_S
	var node = node_of.call(target)
	var at: Vector3 = node.position if node != null and is_instance_valid(node) else _body_pos()
	var damage := int(body.get("damage", 0)) if body.get("damage") != null else 0
	match String(body.get("outcome", "Hit")):
		"Hit":
			play("hit", at)
			puff(at + Vector3(0, 0.6, 0), 1.0)
			shake_cb.call(0.5 if bool(body.get("lethal", false)) else 0.3)
			if damage > 0:
				show_damage(at, damage, Color("ff5a4a") if bool(body.get("lethal", false)) else Color("f2c14e"))
		"Blocked":
			play("guard", at)
			puff(at + Vector3(0, 0.6, 0), 0.4)
		_: # Dodged, Missed
			play("swish", at)


## #234: a defesa que o motor aceitou: esquiva sopra, bloqueio estala (e a guarda aparece no corpo).
func on_defended(kind: String) -> void:
	play("swish" if kind == "Dodge" else "guard", _body_pos())


## #234: o golpe que o corpo do jogador recebeu (`lastIncoming`): acerto machuca, esquiva e bloqueio têm efeito próprio.
func on_incoming(outcome: String, damage: int, _by: int) -> void:
	var at := _body_pos()
	match outcome:
		"Hit":
			play("hit", at)
			shake_cb.call(0.35)
			if damage > 0:
				show_damage(at, damage, Color("e8665a"), true)
		"Blocked":
			play("guard", at)
			shake_cb.call(0.1)
		"Dodged", "Missed":
			play("swish", at)


## Número de dano que sobe sobre `at` (o alvo) e some: um pool fixo de rótulos 3D.
func show_damage(at: Vector3, amount: int, color: Color, own: bool = false) -> void:
	damage_shown.append({"amount": amount, "own": own})
	if damage_shown.size() > 32:
		damage_shown.remove_at(0)
	if _dmg_labels.is_empty() or suppressed:
		return
	var i := _dmg_next
	_dmg_next = (_dmg_next + 1) % _dmg_labels.size()
	var label := _dmg_labels[i]
	label.text = "−%d" % amount
	label.modulate = color
	label.position = at + Vector3(0, 1.5, 0)
	_dmg_base[i] = label.position.y
	label.visible = true
	_dmg_ages[i] = 0.0


## O motor recusou: só um som baixo e neutro. Nenhum som, poeira ou número da ação.
func on_refused() -> void:
	play("deny", _body_pos(), -6.0)


## O motor deixou o corpo pular por `air_s`: o pouso soa quando o salto acaba (e estremece se foi alto).
func on_jumped(air_s: float) -> void:
	_lands.append(maxf(air_s, 0.05))
	_land_air.append(air_s)


func _land(air_s: float) -> void:
	var at := _body_pos()
	play("land", at)
	puff(at, 0.8)
	if air_s >= STRONG_AIR_S:
		shake_cb.call(minf(0.25 + air_s * 0.3, 0.6))


## Ficha lida do motor. As mudanças viram "+5 comida" / "−12 dinheiro" só logo depois de uma ação confirmada; fora disso
## (fome que passa com o tempo) só atualizam a referência.
func note_sheet(body: Dictionary) -> int:
	var shown := 0
	if _armed > 0.0 and not _last_sheet.is_empty():
		for line in sheet_changes(_last_sheet, body):
			_float(String(line.text), line.color)
			shown += 1
	if not _last_sheet.is_empty():
		# #243: o que entra nas mãos aparece na hora ("+1 Madeira"), unidade a unidade: só nasce de ação do motor, então vale sem armar
		for line in carry_gains(_last_sheet, body, resource_name_of):
			_float(String(line.text), line.color)
			shown += 1
	_last_sheet = body.duplicate()
	return shown


func reset() -> void:
	_last_sheet = {}
	_armed = 0.0
	_step_index = -1


## Diferenças (dinheiro, barras) entre duas fichas: [{text, color}].
static func sheet_changes(before: Dictionary, after: Dictionary) -> Array:
	var out: Array = []
	if before.get("money") != null and after.get("money") != null:
		var d := int(after.money) - int(before.money)
		if d != 0:
			out.append({"text": "%s dinheiro" % _signed(d, ""), "color": Color("f2c14e") if d > 0 else Color("e8665a")})
	for spec in SHEET_KEYS:
		var key: String = spec[0]
		if before.get(key) == null or after.get(key) == null:
			continue
		var delta := float(after[key]) - float(before[key])
		if absf(delta) >= MIN_DELTA:
			out.append({"text": "%s %s" % [_signed(int(round(delta)), String(spec[1])), spec[2]], "color": spec[3] if delta > 0 else Color("e8665a")})
	return out


## #243: o que passou a levar nas mãos entre duas fichas (mesmo recurso, mais unidades; ou outro recurso em mãos vazias): [{text, color}].
static func carry_gains(before: Dictionary, after: Dictionary, name_of: Callable) -> Array:
	var now: Variant = after.get("carrying")
	if not now is Dictionary or int(now.get("quantity", 0)) <= 0:
		return []
	var was: Variant = before.get("carrying")
	var had := 0
	if was is Dictionary and int(was.get("resourceId", -1)) == int(now.get("resourceId", -2)):
		had = int(was.get("quantity", 0))
	var gained := int(now.quantity) - had
	if gained <= 0:
		return []
	return [{"text": "+%d %s" % [gained, String(name_of.call(int(now.resourceId))) if name_of.is_valid() else ""], "color": Color("c9a36b")}]


static func _signed(value: int, icon: String) -> String:
	return "%s%s%d" % [icon + " " if icon != "" else "", "+" if value > 0 else "−", absi(value)] # #228: sem emoji (a fonte do jogo não tem o glifo)


## ---- saídas -------------------------------------------------------------------------------------------------------

func _body_pos() -> Vector3:
	var body = body_node.call()
	return body.position if body != null and is_instance_valid(body) else listener_pos.call()


func play(kind: String, at: Vector3, extra_db: float = 0.0) -> void:
	var db := gain_db(Vector2(at.x - listener_pos.call().x, at.z - listener_pos.call().z).length() * _meters_per_unit()) + extra_db
	if db <= -60.0 or not _streams.has(kind):
		return
	played.append(kind)
	if played.size() > 32:
		played.remove_at(0)
	if _voices.is_empty() or not is_inside_tree():
		return
	var voice := _voices[_next_voice]
	_next_voice = (_next_voice + 1) % _voices.size()
	voice.stream = _streams[kind]
	voice.volume_db = db - 12.0
	voice.pitch_scale = 0.94 + 0.12 * float((played.size() * 7) % 5) / 4.0
	voice.play()


func _meters_per_unit() -> float:
	return float(Grid.cell_meters)


func puff(at: Vector3, strength: float) -> void:
	puff_log += 1
	if _puffs.is_empty():
		return
	var p := _puffs[_next_puff]
	_next_puff = (_next_puff + 1) % _puffs.size()
	p.position = at + Vector3(0, 0.05, 0)
	p.initial_velocity_max = 0.5 + 0.6 * strength
	p.restart()
	p.emitting = true


func _float(text: String, color: Color) -> void:
	floaters_shown += 1
	if _labels.is_empty() or suppressed:
		return
	var i := _next_label
	_next_label = (_next_label + 1) % _labels.size()
	_labels[i].text = text
	_labels[i].add_theme_color_override("font_color", color)
	_labels[i].visible = true
	_labels[i].modulate.a = 1.0
	_ages[i] = 0.0


func pool_sizes() -> Array[int]:
	return [_voices.size(), _puffs.size(), _labels.size(), _dmg_labels.size()]
