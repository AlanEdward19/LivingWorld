extends Node
## Ambiente sonoro procedural (sem arquivos de áudio): vento sempre, pássaros de dia, grilos à noite.
## O volume de cada camada segue a luz do dia; ao mudar de período (noite/amanhecer/dia/entardecer) as
## camadas de pássaros e grilos entram com fade. Sons de ação vêm do catálogo (arquivo ou procedural).
## M silencia. Só apresentação.

const AssetCatalog := preload("res://Scenes/asset_catalog.gd")

const MIX_RATE := 22050.0
const FADE_SECONDS := 2.0
const SFX_RATE := 22050

var muted := false
var daylight := 1.0
var catalog = AssetCatalog.new()
var period := "day"
var period_fade := 1.0 ## 0 logo após trocar de período, 1 estável
var _previous_light := 1.0
var _sfx_cache: Dictionary = {}
var _sfx: AudioStreamPlayer
var _player: AudioStreamPlayer
var _playback: AudioStreamGeneratorPlayback
var _noise := 0.0
var _phase := 0.0
var _chirp_timer := 0.0
var _chirp_left := 0.0
var _chirp_pitch := 3000.0
var _rng := RandomNumberGenerator.new()


## Níveis 0–1 de cada camada para uma luz do dia (0 noite, 1 meio-dia).
static func levels(light: float) -> Dictionary:
	return {
		"wind": 0.25 + 0.2 * (1.0 - light),
		"birds": clampf((light - 0.3) / 0.7, 0.0, 1.0),
		"crickets": clampf((0.5 - light) / 0.5, 0.0, 1.0),
	}


func _ready() -> void:
	_rng.randomize()
	_sfx = AudioStreamPlayer.new()
	_sfx.volume_db = -10.0
	add_child(_sfx)
	var stream := AudioStreamGenerator.new()
	stream.mix_rate = MIX_RATE
	stream.buffer_length = 0.25
	_player = AudioStreamPlayer.new()
	_player.stream = stream
	_player.volume_db = -14.0
	add_child(_player)
	_player.play()
	_playback = _player.get_stream_playback()


## Luz do dia vinda do ciclo; troca de período reinicia o fade das camadas.
func set_daylight(light: float) -> void:
	var next := AssetCatalog.period_for(light, light >= _previous_light)
	if absf(light - _previous_light) > 0.0001:
		_previous_light = light
	daylight = light
	if next != period:
		period = next
		period_fade = 0.0


## Toca o som que o catálogo associa à ação; devolve o id tocado ("" se a ação não tem som ou está mudo).
func play_action(action: int) -> String:
	var sound_id: String = catalog.sound_id_for_action(action)
	if sound_id == "" or muted or _sfx == null:
		return ""
	if not _sfx_cache.has(sound_id):
		var stream: AudioStream = catalog.sound_file(sound_id)
		_sfx_cache[sound_id] = stream if stream != null else _synthesize(catalog.sound_spec(sound_id))
	_sfx.stream = _sfx_cache[sound_id]
	_sfx.play()
	return sound_id


## Procedural de fallback: tom com ruído e envelope, 16 bits mono.
static func _synthesize(spec: Dictionary) -> AudioStreamWAV:
	var frames := int(float(spec.get("len", 0.1)) * SFX_RATE)
	var freq := float(spec.get("freq", 440.0))
	var noise := float(spec.get("noise", 0.0))
	var rng := RandomNumberGenerator.new()
	rng.seed = int(freq) # mesmo som a cada execução
	var bytes := PackedByteArray()
	bytes.resize(frames * 2)
	for i in frames:
		var envelope := 1.0 - float(i) / frames
		var value := (sin(TAU * freq * i / SFX_RATE) * (1.0 - noise) + (rng.randf() - 0.5) * 2.0 * noise) * envelope * 0.5
		bytes.encode_s16(i * 2, int(clampf(value, -1.0, 1.0) * 32767.0))
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = SFX_RATE
	wav.data = bytes
	return wav


func set_muted(value: bool) -> void:
	muted = value
	if _player != null:
		_player.volume_db = -80.0 if muted else -14.0


func _process(delta: float) -> void:
	if _playback == null or muted:
		return
	period_fade = minf(period_fade + delta / FADE_SECONDS, 1.0)
	var mix := levels(daylight)
	mix.birds *= period_fade
	mix.crickets *= period_fade
	var frames := _playback.get_frames_available()
	for i in frames:
		_noise = _noise * 0.985 + (_rng.randf() - 0.5) * 0.06 # ruído passa-baixa = vento
		var sample: float = _noise * mix.wind * 3.0
		if _chirp_left > 0.0:
			_chirp_left -= 1.0 / MIX_RATE
			var sweep := 1.0 + (1.0 - _chirp_left / 0.12) * 0.25
			_phase += TAU * _chirp_pitch * sweep / MIX_RATE
			var envelope := clampf(_chirp_left / 0.12, 0.0, 1.0)
			sample += sin(_phase) * 0.12 * envelope * maxf(mix.birds, mix.crickets)
		_playback.push_frame(Vector2(sample, sample))
	_chirp_timer -= delta
	if _chirp_timer <= 0.0:
		var active: float = maxf(mix.birds, mix.crickets)
		_chirp_timer = _rng.randf_range(0.15, 1.4) / maxf(active, 0.05)
		if active > 0.05:
			_chirp_left = 0.12
			_chirp_pitch = (_rng.randf_range(2400.0, 4200.0) if mix.birds >= mix.crickets else 4300.0)
