extends Control
## Primeiros passos dentro do mundo: na primeira vez, 6 dicas curtas, uma por vez, presas perto da parte da tela de que
## falam (Enter: próxima · Esc: pular todas). Ficam lembradas nas preferências locais; o botão "?" reabre. Também
## guarda o painel de Ajuda com todos os atalhos.

signal finished()

const Kit := preload("res://Scenes/menu/menu_kit.gd")

## Cada dica: texto e de qual controle do App ela fala ("" = meio da tela).
const TIPS: Array[Dictionary] = [
	{"text": "Arraste com o botão direito do mouse para andar pelo mundo. A rodinha aproxima e afasta (ou use W A S D e Q/E).", "anchor": ""},
	{"text": "Clique numa pessoa para saber quem ela é: nome, família e o que está fazendo agora.", "anchor": "inspector"},
	{"text": "Na ficha, aperte Seguir (ou a tecla F) e a câmera vai junto com a pessoa.", "anchor": "inspector"},
	{"text": "Aqui fica o tempo: pausar, andar um passo e acelerar.", "anchor": "clock_panel"},
	{"text": "Aperte N para ir ao próximo acontecimento, ou V para só assistir o mundo acontecer.", "anchor": "minimap"},
	{"text": "Esqueceu algo? O botão ? mostra as dicas e os atalhos de novo. Esc abre o menu de pausa.", "anchor": "help_button"},
]
const SHORTCUTS: Array[Array] = [
	["Andar pelo mundo", "botão direito e arrastar · W A S D ou setas"],
	["Aproximar e afastar", "rodinha do mouse"],
	["Girar a câmera", "Shift + botão direito · Q e E"],
	["Ver quem é", "clique numa pessoa, casa ou cidade"],
	["Voltar na ficha", "Backspace"],
	["Entrar numa casa", "duplo clique · PgUp/PgDn troca o andar"],
	["Seguir", "F (ou botão Seguir na ficha)"],
	["Próximo acontecimento", "N"],
	["Só assistir", "V"],
	["Lista de pessoas", "L"],
	["Conversar", "T"],
	["Pensamentos da pessoa", "B"],
	["Jogar com a pessoa", "I"],
	["Histórias que circulam", "R"],
	["Ver por dentro (raio-x)", "X"],
	["Planta da cidade (ruas, obras, acessos, pessoas)", "P"],
	["Mapa de calor do pisoteio das ruas", "H"],
	["Prédio de trás sob o cursor", "Tab · Alt + rodinha"],
	["Som liga/desliga", "M"],
	["Fechar janela · menu de pausa", "Esc"],
	["Console do mundo", "F8"],
	["Esta ajuda", "? ou F1"],
	["Voltar ao menu principal", "F10"],
]

var app
var settings
var index := -1 ## dica aberta (-1: nenhuma)
var bubble: PanelContainer
var bubble_text: Label
var counter: Label
var next_button: Button
var help: Control


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	bubble = PanelContainer.new()
	bubble.visible = false
	bubble.custom_minimum_size.x = 380
	add_child(bubble)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 8)
	bubble.add_child(box)
	counter = Kit.text("", box, 13, Kit.GOLD)
	counter.autowrap_mode = TextServer.AUTOWRAP_OFF
	bubble_text = Kit.text("", box, 17, Color.WHITE)
	bubble_text.custom_minimum_size.x = 360
	var row := Kit.row(box)
	Kit.button("Pular dicas", row, finish)
	next_button = Kit.button("Próxima ▶", row, next_tip)
	help = _build_help()


## Dicas da primeira vez: só se ainda não foram vistas (ou se `force`).
func start(force: bool = false) -> void:
	if not force and settings != null and settings.tips_done:
		return
	index = -1
	help.visible = false
	next_tip()


func active() -> bool:
	return index >= 0


func next_tip() -> void:
	index += 1
	if index >= TIPS.size():
		finish()
		return
	var tip: Dictionary = TIPS[index]
	counter.text = "Dica %d de %d" % [index + 1, TIPS.size()]
	bubble_text.text = tip.text
	next_button.text = "Entendi ✔" if index == TIPS.size() - 1 else "Próxima ▶"
	bubble.visible = true
	bubble_text.size = Vector2(360, 0) # texto quebrado: a altura mínima depende da largura já valer
	bubble.size = Vector2.ZERO
	bubble.reset_size()
	_place(String(tip.anchor))
	next_button.grab_focus()
	_reflow.call_deferred(String(tip.anchor))


func _reflow(anchor: String) -> void:
	if not active():
		return
	bubble.size = Vector2.ZERO
	bubble.reset_size()
	_place(anchor)


## Põe a dica ao lado do controle de que fala, sem sair da tela (vale em qualquer resolução).
func _place(anchor: String) -> void:
	var view := get_viewport_rect().size if is_inside_tree() else Vector2(1600, 900)
	var box_size := bubble.get_combined_minimum_size()
	var at := (view - box_size) / 2.0
	var target: Control = app.get(anchor) if app != null and anchor != "" and anchor in app else null
	if target != null and target.is_visible_in_tree():
		var rect := target.get_global_rect()
		at = Vector2(rect.position.x - box_size.x - 16, rect.position.y)
		if at.x < 8:
			at = Vector2(rect.position.x, rect.end.y + 12)
		if at.y + box_size.y > view.y - 8:
			at.y = rect.position.y - box_size.y - 12
	bubble.position = Vector2(clampf(at.x, 8, maxf(view.x - box_size.x - 8, 8)), clampf(at.y, 8, maxf(view.y - box_size.y - 8, 8)))


func finish() -> void:
	index = -1
	bubble.visible = false
	if settings != null:
		settings.tips_done = true
		settings.save()
	finished.emit()


func _build_help() -> Control:
	var layer := Control.new()
	layer.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layer.visible = false
	add_child(layer)
	var box := Kit.screen(layer, 640)
	Kit.title("Ajuda e atalhos", 30, box, Kit.GOLD)
	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 24)
	grid.add_theme_constant_override("v_separation", 6)
	box.add_child(grid)
	for entry in SHORTCUTS:
		for i in 2:
			var cell := Kit.text(entry[i], grid, 16, Color.WHITE if i == 0 else Kit.SOFT)
			cell.autowrap_mode = TextServer.AUTOWRAP_OFF
	var row := Kit.row(box)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	Kit.button("Ver as dicas de novo", row, func(): start(true))
	Kit.button("Fechar (Esc)", row, close_help)
	return layer


func toggle_help() -> void:
	if help.visible:
		close_help()
	else:
		open_help()


func open_help() -> void:
	bubble.visible = false
	index = -1
	help.visible = true
	help.mouse_filter = Control.MOUSE_FILTER_STOP
	Kit.fade_in(help)
	var first := Kit.first_focusable(help)
	if first != null:
		first.grab_focus()


func close_help() -> void:
	help.visible = false


## Esc pula as dicas ou fecha a ajuda; Enter passa para a próxima dica.
func handle_escape() -> bool:
	if help.visible:
		close_help()
		return true
	if active():
		finish()
		return true
	return false
