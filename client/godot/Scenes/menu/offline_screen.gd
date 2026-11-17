extends Control
## "O mundo ainda não está ligado": aparece quando o jogo abre sem servidor. Diz o que fazer em frases curtas e, se o
## projeto já foi compilado, oferece ligar o mundo daqui mesmo (LocalServer), mostrando quanto falta.

signal retry_pressed()
signal start_pressed()
signal quit_pressed()

const Kit := preload("res://Scenes/menu/menu_kit.gd")

var headline: Label
var explanation: Label
var progress: ProgressBar
var start_button: Button
var retry_button: Button
var can_start := false


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var box := Kit.screen(self, 620)
	headline = Kit.title("O mundo ainda não está ligado", 34, box, Kit.GOLD)
	explanation = Kit.text("", box, 18)
	explanation.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	progress = ProgressBar.new()
	progress.max_value = 60
	progress.show_percentage = false
	progress.custom_minimum_size.y = 14
	progress.visible = false
	box.add_child(progress)
	Kit.spacer(box, 6)
	start_button = Kit.big_button("Ligar o mundo", box, func(): start_pressed.emit())
	retry_button = Kit.big_button("Tentar de novo", box, func(): retry_pressed.emit())
	Kit.big_button("Sair", box, func(): quit_pressed.emit())
	set_can_start(can_start)


func set_can_start(value: bool) -> void:
	can_start = value
	if start_button == null:
		return
	start_button.visible = value
	headline.text = "O mundo ainda não está ligado"
	explanation.text = ("Aperte \"Ligar o mundo\" e espere um pouquinho." if value else
		"Feche esta janela e abra o jogo pelo arquivo Jogar.command, na pasta do Living World (dois cliques).\nNo Terminal: bash scripts/play.sh")
	progress.visible = false
	if is_inside_tree():
		focus_default()


## Enquanto liga: barra e segundos, sem botões que atrapalhem.
func show_starting(seconds: int) -> void:
	headline.text = "Ligando o mundo…"
	explanation.text = "Acordando o mundo. Na primeira vez pode levar até um minuto."
	progress.visible = true
	progress.value = mini(seconds, int(progress.max_value))
	start_button.visible = false


func show_failed() -> void:
	set_can_start(false)
	headline.text = "O mundo não ligou"
	explanation.text = "Algo impediu o mundo de ligar. Feche o jogo e abra pelo Jogar.command: ele mostra o motivo."


func focus_default() -> void:
	(start_button if start_button.visible else retry_button).grab_focus()
