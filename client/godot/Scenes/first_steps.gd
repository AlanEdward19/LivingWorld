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
## #222: com um personagem, a ajuda mostra os comandos do jogo (o resto é do observador).
const PLAYER_SHORTCUTS: Array[Array] = [
	["Andar", "W A S D (para onde a câmera olha)"],
	["Olhar em volta", "mexer o mouse"],
	["Correr", "Shift + andar (o corpo cansa)"],
	["Pular", "Espaço"],
	["Interagir (comprar, beber, conversar, porta, cama…)", "E, depois o número da opção"],
	["Bater em quem está na mira", "clique"],
	["1ª ou 3ª pessoa", "C"],
	["Aproximar e afastar a câmera", "+ e − (ou a rodinha)"],
	["Soltar o mouse", "Z"],
	["Menu (continuar, sair do personagem)", "Esc"],
]
## #238: "O que dá para fazer": os tipos de oferta que o motor tem (`/reach`), onde cada um acontece e a tecla. A oferta só aparece
## com o corpo ao lado do alvo; esta lista diz o que procurar e onde.
const PLAYER_ACTIONS: Array[Array] = [
	["Conversar", "perto de alguém: E, ou segure E e escolha Conversar"],
	["Comer e guardar", "na despensa de casa: E (pegar), E de novo com a carga (guardar)"],
	["Dormir", "na cama de casa: E e escolha Dormir"],
	["Comprar e vender", "junto à banca de uma oficina: E e o número da opção"],
	["Trabalhar", "no posto do seu emprego (oficina ou lavoura): segure E e escolha Trabalhar"],
	["Beber e encher o cântaro", "colado na água (rio, lago, fonte): E"],
	["Coletar madeira", "na mata, fora da cidade: segure E e escolha Coletar"],
	["Usar a mobília", "dentro da sua casa ou do seu posto, junto ao objeto: E"],
	["Entrar, sair e subir", "na porta ou na escada do prédio: E"],
	["Recolher espólio", "onde alguém deixou coisas no chão: E"],
	["Furtar", "banca ou casa alheia: segure E e escolha Furtar (tem consequência)"],
	["Bater e se defender", "clique bate; botão direito ou F bloqueia; Ctrl ou Alt esquiva"],
]
var spectator_grid: GridContainer
var player_grid: GridContainer
var actions_grid: GridContainer
var _actions_button: Button
var _showing_actions := false
var _guide_button: Button
var _title_label: Label
const SHORTCUTS: Array[Array] = [
	["Andar pelo mundo", "botão direito e arrastar · W A S D ou setas"],
	["Aproximar e afastar", "rodinha do mouse · + e −"],
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
	["Minimapa", "K"],
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
	var dim := ColorRect.new() # #222: fundo escuro: nada da cena vaza por trás do texto
	dim.color = Color(0.02, 0.03, 0.06, 0.82)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	layer.add_child(dim)
	var box := Kit.screen(layer, 640)
	_title_label = Kit.title("Ajuda e atalhos", 30, box, Kit.GOLD)
	spectator_grid = _shortcut_grid(box, SHORTCUTS)
	player_grid = _shortcut_grid(box, PLAYER_SHORTCUTS)
	actions_grid = _shortcut_grid(box, PLAYER_ACTIONS)
	actions_grid.visible = false
	var row := Kit.row(box)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	_actions_button = Kit.button("O que dá para fazer", row, toggle_actions)
	_actions_button.visible = false
	Kit.button("Rever o guia", row, review_guide).visible = false
	_guide_button = row.get_child(row.get_child_count() - 1)
	Kit.button("Ver as dicas de novo", row, func(): start(true))
	Kit.button("Fechar (Esc)", row, close_help)
	return layer


func _shortcut_grid(box: Control, rows: Array) -> GridContainer:
	var grid := GridContainer.new()
	grid.columns = 2
	grid.add_theme_constant_override("h_separation", 24)
	grid.add_theme_constant_override("v_separation", 6)
	box.add_child(grid)
	for entry in rows:
		for i in 2:
			var cell := Kit.text(entry[i], grid, 16, Color.WHITE if i == 0 else Kit.SOFT)
			cell.autowrap_mode = TextServer.AUTOWRAP_OFF
	return grid


func toggle_help() -> void:
	if help.visible:
		close_help()
	else:
		open_help()


func open_help() -> void:
	bubble.visible = false
	index = -1
	help.visible = true
	var playing: bool = app != null and bool(app.get("player_mode"))
	_showing_actions = false
	_show_player_page()
	spectator_grid.visible = not playing
	_actions_button.visible = playing
	_guide_button.visible = playing
	help.mouse_filter = Control.MOUSE_FILTER_STOP
	Kit.fade_in(help)
	var first := Kit.first_focusable(help)
	if first != null:
		first.grab_focus()


## #238: alterna entre os atalhos e a lista "O que dá para fazer" (só no modo jogador).
func toggle_actions() -> void:
	_showing_actions = not _showing_actions
	_show_player_page()


func _show_player_page() -> void:
	var playing: bool = app != null and bool(app.get("player_mode"))
	player_grid.visible = playing and not _showing_actions
	actions_grid.visible = playing and _showing_actions
	_title_label.text = ("O que dá para fazer" if _showing_actions else "Como jogar") if playing else "Ajuda e atalhos"
	if _actions_button != null:
		_actions_button.text = "Ver os atalhos" if _showing_actions else "O que dá para fazer"


## #238: reabre o guia da primeira posse (a ajuda fecha para ele aparecer).
func review_guide() -> void:
	close_help()
	if app != null and app.get("player_hud") != null:
		app.player_hud.show_guide()


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
