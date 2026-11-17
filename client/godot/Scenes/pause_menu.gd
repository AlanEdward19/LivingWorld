extends Control
## Menu de pausa do jogo (Esc ou ☰): Continuar, estado do mundo (salvo sozinho), Opções, Ajuda, Voltar ao menu e
## Sair. Ao abrir, pausa o tempo se o jogador puder (token de admin da sessão) e, ao continuar, devolve como estava.

signal resume_pressed()
signal menu_pressed()
signal quit_pressed()
signal help_pressed()

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const OptionsScreen := preload("res://Scenes/menu/options_screen.gd")

var app ## App: relógio (clock_panel) e preferências (settings)
var paused_by_us := false ## o tempo corria e este menu pausou: Continuar e Voltar ao menu retomam
var world_line: Label
var save_line: Label
var continue_button: Button
var options: Control
var _box: VBoxContainer
var _column_root: Control


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_STOP
	visible = false
	var dim := ColorRect.new()
	dim.color = Color(0.02, 0.03, 0.06, 0.6)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(dim)
	_box = Kit.screen(self, 420)
	_column_root = get_child(get_child_count() - 1)
	Kit.title("Pausa", 38, _box, Kit.GOLD)
	world_line = Kit.title("", 18, _box, Color.WHITE)
	save_line = Kit.title("Seu mundo é salvo sozinho, a cada dia do jogo. Pode sair quando quiser.", 15, _box, Kit.SOFT)
	Kit.spacer(_box, 6)
	continue_button = Kit.big_button("Continuar", _box, close)
	Kit.big_button("Opções", _box, show_options)
	Kit.big_button("Ajuda e atalhos", _box, func(): close(); help_pressed.emit())
	Kit.big_button("Voltar ao menu", _box, func(): await _restore_clock(); visible = false; menu_pressed.emit())
	Kit.big_button("Sair do jogo", _box, func(): visible = false; quit_pressed.emit())


func is_open() -> bool:
	return visible


func toggle() -> void:
	if visible:
		close()
	else:
		await open()


func open() -> void:
	visible = true
	Kit.fade_in(self)
	continue_button.grab_focus()
	var status: Dictionary = app.clock_panel.status if app != null else {}
	world_line.text = ("Ano %d" % int(status.year)) if status.has("year") else ""
	if app != null and app.clock_panel.can_control and status.has("isPaused") and not bool(status.isPaused):
		paused_by_us = true
		await app.clock_panel.command("pause")
	save_line.text = "Seu mundo é salvo sozinho, a cada dia do jogo. Pode sair quando quiser." + \
		("\nO tempo está parado enquanto este menu está aberto." if paused_by_us else "")


func close() -> void:
	if options != null:
		options.queue_free()
		options = null
		_column_root.visible = true
	visible = false
	await _restore_clock()
	resume_pressed.emit()


func _restore_clock() -> void:
	if paused_by_us and app != null:
		paused_by_us = false
		await app.clock_panel.command("resume")


func show_options() -> void:
	options = OptionsScreen.new()
	options.settings = app.settings if app != null else null
	_column_root.visible = false # a coluna da pausa some enquanto as opções estão abertas
	add_child(options)
	options.focus_default()
	options.closed.connect(func():
		options.queue_free()
		options = null
		_column_root.visible = true
		continue_button.grab_focus())


func _unhandled_input(event: InputEvent) -> void:
	if visible and options == null and event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		close()
