extends Control
## Tela de carregamento: diz o que está acontecendo de verdade. Criando um mundo, mostra as etapas que o servidor manda
## (AD-175: terreno, vilas, famílias, ofícios, guardar, pronto; `set_progress`); entrando no mundo, as do próprio cliente
## (conectar e receber o primeiro retrato; `set_stage`). Usada pelo menu e pelo jogo (até o primeiro retrato chegar).

const Kit := preload("res://Scenes/menu/menu_kit.gd")

const STAGES: Array[String] = [ ## etapas do cliente ao entrar num mundo pronto (as da criação vêm do servidor)
	"Pedindo o mundo…",
	"Esperando o mundo responder…",
	"Acordando os moradores…",
	"Abrindo os olhos no mundo…",
]

var heading: Label
var stage_label: Label
var bar: ProgressBar
var stage := 0
var _dots := 0.0


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	var shade := ColorRect.new()
	shade.color = Color("0d1420")
	shade.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	shade.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(shade)
	var box := Kit.screen(self, 560, false)
	heading = Kit.title("Preparando o mundo", 34, box, Kit.GOLD)
	stage_label = Kit.title(STAGES[0], 20, box, Color.WHITE)
	bar = ProgressBar.new()
	bar.max_value = STAGES.size()
	bar.show_percentage = false
	bar.custom_minimum_size = Vector2(0, 14)
	box.add_child(bar)
	set_stage(stage)


## Etapa real em que o cliente está (0..STAGES.size()-1); `text` troca a frase quando há algo mais exato a dizer.
func set_stage(index: int, text: String = "") -> void:
	stage = clampi(index, 0, STAGES.size() - 1)
	if stage_label == null:
		return
	stage_label.text = text if text != "" else STAGES[stage]
	bar.max_value = STAGES.size()
	bar.value = stage + 1


## Etapa real do servidor: `index` de `count` (0..count-1) com a frase que ele mandou.
func set_progress(index: int, count: int, text: String) -> void:
	if stage_label == null:
		return
	bar.max_value = maxi(count, 1)
	bar.value = clampi(index + 1, 1, maxi(count, 1))
	stage_label.text = text + ("" if text.ends_with("!") else "…")


func set_heading(text: String) -> void:
	if heading != null:
		heading.text = text


## Mensagem fora das etapas (ex.: sem conexão, nova tentativa).
func set_message(text: String) -> void:
	if stage_label != null:
		stage_label.text = text


func _process(delta: float) -> void:
	_dots += delta
	if bar != null:
		bar.modulate.a = 0.75 + 0.25 * sin(_dots * 3.0)
