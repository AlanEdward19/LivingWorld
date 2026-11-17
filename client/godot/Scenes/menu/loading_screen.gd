extends Control
## Tela de carregamento: diz o que está acontecendo de verdade, em etapas do próprio cliente (a API ainda não manda
## progresso da criação): pedir o mundo, criá-lo, conectar e receber o primeiro retrato. Usada pelo menu (criar ou
## abrir um mundo) e pelo jogo (até o primeiro retrato chegar).

const Kit := preload("res://Scenes/menu/menu_kit.gd")

const STAGES: Array[String] = [
	"Medindo as terras e os rios…",
	"Erguendo as casas e chamando as famílias…",
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
	bar.value = stage + 1


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
