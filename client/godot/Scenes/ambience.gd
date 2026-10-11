extends Node
## Ambiente sonoro procedural (sem arquivos de áudio): vento sempre, pássaros de dia, grilos à noite.
## O volume de cada camada segue a luz do dia; M silencia. Só apresentação.

const MIX_RATE := 22050.0

var muted := false
var daylight := 1.0
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
	var stream := AudioStreamGenerator.new()
	stream.mix_rate = MIX_RATE
	stream.buffer_length = 0.25
	_player = AudioStreamPlayer.new()
	_player.stream = stream
	_player.volume_db = -14.0
	add_child(_player)
	_player.play()
	_playback = _player.get_stream_playback()


func set_muted(value: bool) -> void:
	muted = value
	if _player != null:
		_player.volume_db = -80.0 if muted else -14.0


func _process(delta: float) -> void:
	if _playback == null or muted:
		return
	var mix := levels(daylight)
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
