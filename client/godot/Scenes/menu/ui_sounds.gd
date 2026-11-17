extends Node
## Sons de interface (AD-175), todos gerados por código (sem arquivo de som): clique, foco, confirmar, voltar e erro.
## Um nó só, posto pelo Boot: todo botão que entra na árvore ganha o clique (o "◀ Voltar" ganha o som de voltar) e o
## foco pelo teclado/controle ganha um toque baixo. Telas pedem confirmar/erro por `UiSounds.cue(nó, tipo)`.
## Toca no barramento Master, então o volume das Opções vale também aqui.

const GROUP := "ui_sounds"
const MIX_RATE := 22050
const KINDS: Array[String] = ["click", "focus", "confirm", "back", "error"]
const VOLUME_DB := -10.0
const VOICES := 4 ## sons que podem soar ao mesmo tempo
const MIN_GAP := 0.04 ## segundos: o mesmo som não repete mais rápido que isto (foco correndo pela lista)

var streams: Dictionary = {} ## tipo -> AudioStreamWAV
var last_cue := "" ## último som pedido (testes)
var _voices: Array[AudioStreamPlayer] = []
var _next_voice := 0
var _last_at: Dictionary = {}


func _ready() -> void:
	add_to_group(GROUP)
	for kind in KINDS:
		streams[kind] = make_stream(kind)
	for i in VOICES:
		var voice := AudioStreamPlayer.new()
		voice.volume_db = VOLUME_DB
		add_child(voice)
		_voices.append(voice)
	get_tree().node_added.connect(_hook)
	_hook_tree(get_tree().root)


## Toca `kind` pelo nó de sons da árvore de `from`, se houver (cenas e testes sem o Boot simplesmente ficam mudos).
static func cue(from: Node, kind: String) -> void:
	if from == null or not from.is_inside_tree():
		return
	var sounds := from.get_tree().get_first_node_in_group(GROUP)
	if sounds != null:
		sounds.play(kind)


func play(kind: String) -> void:
	if not streams.has(kind):
		return
	var now := Time.get_ticks_msec() / 1000.0
	if now - float(_last_at.get(kind, -1.0)) < MIN_GAP:
		return
	_last_at[kind] = now
	last_cue = kind
	var voice := _voices[_next_voice]
	_next_voice = (_next_voice + 1) % _voices.size()
	voice.stream = streams[kind]
	voice.play()


func _hook_tree(node: Node) -> void:
	_hook(node)
	for child in node.get_children():
		_hook_tree(child)


func _hook(node: Node) -> void:
	if not (node is BaseButton) or node.has_meta("ui_sound"):
		return
	node.set_meta("ui_sound", true)
	var button := node as BaseButton
	button.pressed.connect(func(): play("back" if _is_back(button) else "click"))
	button.focus_entered.connect(func():
		if not button.is_hovered(): # foco pelo teclado/controle; o clique do mouse já tem o seu som
			play("focus"))


static func _is_back(button: BaseButton) -> bool:
	return button is Button and ((button as Button).text.begins_with("◀") or (button as Button).text in ["Manter", "Cancelar", "Cancel"])


## Som curto em PCM 16 bits mono: tom com envelope (ataque rápido, queda exponencial), por tipo.
static func make_stream(kind: String) -> AudioStreamWAV:
	var notes: Array = { # [frequência inicial, final, duração em s, forma: 0 seno, 1 quadrada suave, volume]
		"click": [[1100.0, 800.0, 0.035, 0, 0.55]],
		"focus": [[720.0, 720.0, 0.022, 0, 0.25]],
		"confirm": [[660.0, 660.0, 0.07, 0, 0.5], [990.0, 990.0, 0.11, 0, 0.5]],
		"back": [[620.0, 380.0, 0.07, 0, 0.45]],
		"error": [[220.0, 200.0, 0.08, 1, 0.4], [0.0, 0.0, 0.04, 0, 0.0], [200.0, 180.0, 0.1, 1, 0.4]],
	}.get(kind, [])
	var data := PackedByteArray()
	for note in notes:
		var count := int(float(note[2]) * MIX_RATE)
		var phase := 0.0
		for i in count:
			var t := float(i) / maxf(count - 1, 1)
			var frequency := lerpf(float(note[0]), float(note[1]), t)
			phase += TAU * frequency / MIX_RATE
			var wave := sin(phase)
			if int(note[3]) == 1:
				wave = clampf(wave * 3.0, -1.0, 1.0) * 0.6
			var envelope := minf(1.0, float(i) / (0.004 * MIX_RATE)) * exp(-4.0 * t)
			var sample := int(clampf(wave * envelope * float(note[4]), -1.0, 1.0) * 32767.0)
			data.append(sample & 0xFF)
			data.append((sample >> 8) & 0xFF)
	var stream := AudioStreamWAV.new()
	stream.format = AudioStreamWAV.FORMAT_16_BITS
	stream.mix_rate = MIX_RATE
	stream.stereo = false
	stream.data = data
	return stream
