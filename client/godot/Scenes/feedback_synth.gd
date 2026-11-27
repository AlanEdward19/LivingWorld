extends RefCounted
## #232: sons do jogo gerados por código (sem arquivo de som). Ruído de um LCG fixo, então o mesmo tipo dá sempre o mesmo
## buffer. Mono, 16 bits, MIX_RATE Hz. `samples` é a lógica (testável); `stream` empacota num AudioStreamWAV.

const MIX_RATE := 22050
const KINDS: Array[String] = ["step_dirt", "step_gravel", "step_stone", "step_grass", "step_wood", "land", "hit", "door",
	"coin", "eat", "drink", "deny"]
const SURFACE_STEP := {"dirt": "step_dirt", "gravel": "step_gravel", "stone": "step_stone", "grass": "step_grass", "wood": "step_wood"}


## Som do passo para a superfície (desconhecida = grama).
static func step_kind(surface: String) -> String:
	return String(SURFACE_STEP.get(surface, "step_grass"))


static func samples(kind: String) -> PackedFloat32Array:
	match kind:
		"step_dirt": return _step(0.09, 0.30, 0.0, 0.0, 1)
		"step_gravel": return _step(0.13, 0.55, 0.0, 38.0, 2)
		"step_stone": return _step(0.07, 0.75, 0.0, 0.0, 3, 2100.0)
		"step_grass": return _step(0.10, 0.12, 0.0, 0.0, 4)
		"step_wood": return _step(0.10, 0.22, 120.0, 0.0, 5)
		"land": return _step(0.20, 0.16, 70.0, 0.0, 6)
		"hit": return _hit()
		"door": return _door()
		"coin": return _coin()
		"eat": return _crunches()
		"drink": return _gulps()
		"deny": return _deny()
	return PackedFloat32Array()


static func stream(kind: String) -> AudioStreamWAV:
	var data := samples(kind)
	var bytes := PackedByteArray()
	bytes.resize(data.size() * 2)
	for i in data.size():
		bytes.encode_s16(i * 2, int(clampf(data[i], -1.0, 1.0) * 32000.0))
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = MIX_RATE
	wav.stereo = false
	wav.data = bytes
	return wav


## Ruído branco determinístico (LCG), -1..1.
static func _noise(state: Array) -> float:
	state[0] = (int(state[0]) * 1103515245 + 12345) & 0x7fffffff
	return float((int(state[0]) >> 8) & 0xffff) / 32768.0 - 1.0


static func _count(seconds: float) -> int:
	return int(seconds * MIX_RATE)


## Passo: ruído passa-baixa que morre rápido; `thump` Hz = batida grave; `crunch` Hz = amplitude picotada (cascalho).
static func _step(seconds: float, brightness: float, thump: float, crunch: float, seed_value: int, tick_hz: float = 0.0) -> PackedFloat32Array:
	var n := _count(seconds)
	var out := PackedFloat32Array()
	out.resize(n)
	var state := [seed_value * 7919]
	var low := 0.0
	for i in n:
		var t := float(i) / MIX_RATE
		var env := exp(-t / (seconds * 0.28)) * minf(1.0, t * 900.0)
		low += (_noise(state) - low) * brightness
		var v := low * 2.2
		if crunch > 0.0:
			v *= 0.5 + 0.5 * sin(TAU * crunch * t) * sin(TAU * crunch * 1.7 * t)
		if thump > 0.0:
			v += sin(TAU * thump * t * (1.0 - 0.3 * t / seconds)) * 1.3
		if tick_hz > 0.0:
			v += sin(TAU * tick_hz * t) * exp(-t * 260.0) * 0.8
		out[i] = v * env * 0.4
	return out


static func _hit() -> PackedFloat32Array:
	var n := _count(0.16)
	var out := PackedFloat32Array()
	out.resize(n)
	var state := [4242]
	var low := 0.0
	for i in n:
		var t := float(i) / MIX_RATE
		low += (_noise(state) - low) * 0.5
		var body := sin(TAU * (170.0 - 90.0 * t / 0.16) * t)
		out[i] = (body * 0.8 + low * 1.1) * exp(-t / 0.045) * minf(1.0, t * 1500.0) * 0.55
	return out


## Rangido: dente-de-serra grave que desliza, com tremor, e um estalo do trinco no fim.
static func _door() -> PackedFloat32Array:
	var n := _count(0.5)
	var out := PackedFloat32Array()
	out.resize(n)
	var low := 0.0
	var phase := 0.0
	for i in n:
		var t := float(i) / MIX_RATE
		var f := (150.0 - 55.0 * t / 0.5) * (1.0 + 0.04 * sin(TAU * 17.0 * t))
		phase = fmod(phase + f / MIX_RATE, 1.0)
		low += ((phase * 2.0 - 1.0) - low) * 0.12
		var env := minf(1.0, t * 14.0) * (1.0 - smoothstep(0.38, 0.45, t))
		var latch := exp(-maxf(t - 0.44, 0.0) * 300.0) * (1.0 if t >= 0.44 else 0.0) * sin(TAU * 900.0 * t)
		out[i] = low * env * 0.5 + latch * 0.5
	return out


## Tilintar: dois parciais agudos com decaimento, o segundo entra um pouco depois.
static func _coin() -> PackedFloat32Array:
	var n := _count(0.38)
	var out := PackedFloat32Array()
	out.resize(n)
	for i in n:
		var t := float(i) / MIX_RATE
		var a := sin(TAU * 1760.0 * t) * exp(-t / 0.09)
		var b := sin(TAU * 2349.0 * t) * exp(-maxf(t - 0.07, 0.0) / 0.12) * (1.0 if t >= 0.07 else 0.0)
		out[i] = (a * 0.5 + b * 0.5 + sin(TAU * 5300.0 * t) * exp(-t * 60.0) * 0.15) * 0.45 * minf(1.0, t * 2000.0)
	return out


static func _crunches() -> PackedFloat32Array:
	var out := PackedFloat32Array()
	out.resize(_count(0.42))
	var state := [99]
	var low := 0.0
	for i in out.size():
		var t := float(i) / MIX_RATE
		var local := fmod(t, 0.14)
		low += (_noise(state) - low) * 0.45
		out[i] = low * exp(-local / 0.02) * minf(1.0, local * 1500.0) * 0.9
	return out


static func _gulps() -> PackedFloat32Array:
	var out := PackedFloat32Array()
	out.resize(_count(0.5))
	for i in out.size():
		var t := float(i) / MIX_RATE
		var local := fmod(t, 0.2)
		var env := sin(PI * clampf(local / 0.16, 0.0, 1.0)) * (1.0 if t < 0.4 else 0.0)
		out[i] = sin(TAU * (260.0 + 380.0 * local / 0.16) * local) * env * 0.45
	return out


## Recusa: batida grave e seca, discreta (não imita ação nenhuma).
static func _deny() -> PackedFloat32Array:
	var out := PackedFloat32Array()
	out.resize(_count(0.14))
	for i in out.size():
		var t := float(i) / MIX_RATE
		out[i] = (sin(TAU * 196.0 * t) + 0.5 * sin(TAU * 185.0 * t)) * exp(-t / 0.05) * minf(1.0, t * 1200.0) * 0.3
	return out
