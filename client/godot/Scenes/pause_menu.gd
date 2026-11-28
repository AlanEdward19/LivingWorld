extends Control
## Menu de pausa do jogo (Esc ou ☰): Continuar, estado do mundo, Salvar agora, Opções, Ajuda, Voltar ao menu e Sair.
## Ao abrir, pausa o tempo se o jogador puder (token de admin da sessão) e, ao continuar, devolve como estava. O mundo
## se salva sozinho a cada dia do jogo; "Salvar agora" (AD-175, `POST /world/save`) grava na hora e diz quando.

signal resume_pressed()
signal menu_pressed()
signal quit_pressed()
signal help_pressed()

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const OptionsScreen := preload("res://Scenes/menu/options_screen.gd")
const UiSounds := preload("res://Scenes/menu/ui_sounds.gd")
const AUTO_SAVE := "Seu mundo é salvo sozinho, a cada dia do jogo. Pode sair quando quiser."

var app ## App: relógio (clock_panel) e preferências (settings)
var paused_by_us := false ## o tempo corria e este menu pausou: Continuar e Voltar ao menu retomam
var world_line: Label
var save_line: Label
var continue_button: Button
var save_button: Button
var saving := false
var last_saved_iso := "" ## última gravação conhecida (da lista de mundos ou do Salvar agora)
var options: Control
var switch_button: Button ## #252: "Trocar para Rafael" (o outro da casa; só com alguém encarnado e outro na casa)
var leave_button: Button ## #222: "Sair do personagem" (só com alguém encarnado)
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
	save_line = Kit.title(AUTO_SAVE, 15, _box, Kit.SOFT)
	Kit.spacer(_box, 6)
	continue_button = Kit.big_button("Continuar", _box, close)
	switch_button = Kit.big_button("Trocar de pessoa", _box, func():
		await close()
		if app != null and app.player_hud != null:
			await app.player_hud.switch_body())
	leave_button = Kit.big_button("Sair do personagem", _box, func():
		await close()
		if app != null and app.player_hud != null:
			await app.player_hud.logout())
	save_button = Kit.big_button("Salvar agora", _box, save_now)
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
	if app != null and app.first_steps != null:
		app.first_steps.close_help() # #222: a pausa não abre por cima da ajuda
	visible = true
	leave_button.visible = app != null and app.player_hud != null and app.player_hud.is_active
	var next: Dictionary = app.player_hud.next_housemate() if leave_button.visible else {}
	switch_button.visible = not next.is_empty()
	switch_button.text = "Trocar para %s" % String(next.get("name", ""))
	Kit.fade_in(self)
	continue_button.grab_focus()
	var status: Dictionary = app.clock_panel.status if app != null else {}
	world_line.text = ("Ano %d" % int(status.year)) if status.has("year") else ""
	if app != null and app.clock_panel.can_control and status.has("isPaused") and not bool(status.isPaused):
		paused_by_us = true
		await app.clock_panel.command("pause")
	await _read_last_save()
	_show_save_line()


## Quando o mundo aberto foi gravado pela última vez, pela lista de mundos (o item com `isCurrent`).
func _read_last_save() -> void:
	if app == null or app.api == null or not app.api.has_method("saved_worlds"):
		return
	var list: Dictionary = await app.api.saved_worlds()
	if list.ok and list.body is Array:
		for world in list.body:
			if bool(world.get("isCurrent", false)):
				last_saved_iso = String(world.get("lastSavedAtUtc", ""))


func _show_save_line(extra: String = "") -> void:
	var when := saved_ago(last_saved_iso)
	save_line.text = (extra if extra != "" else AUTO_SAVE) + ("\nSalvo pela última vez %s." % when if when != "" else "") + \
		("\nO tempo está parado enquanto este menu está aberto." if paused_by_us else "")


## Grava agora e confirma na tela; erro vira frase, e o salvamento automático continua valendo.
func save_now() -> void:
	if saving or app == null or app.api == null:
		return
	saving = true
	save_button.disabled = true
	save_button.text = "Salvando…"
	var response: Dictionary = await app.api.save_world()
	saving = false
	save_button.disabled = false
	if response.ok and response.body is Dictionary:
		last_saved_iso = String(response.body.get("savedAtUtc", ""))
		save_button.text = "Salvo ✔"
		UiSounds.cue(self, "confirm")
		_show_save_line("Pronto, o mundo está salvo.")
	else:
		save_button.text = "Salvar agora"
		UiSounds.cue(self, "error")
		_show_save_line("Não deu para salvar agora. O mundo continua se salvando sozinho a cada dia do jogo.")


## "agora mesmo", "há 5 minutos", "há 2 horas", "há 3 dias" desde a gravação do servidor; "" sem data.
static func saved_ago(iso: String, now_unix: float = -1.0) -> String:
	if iso == "":
		return ""
	var when := Time.get_unix_time_from_datetime_string(iso.get_slice(".", 0).trim_suffix("Z").get_slice("+", 0))
	var now := now_unix if now_unix >= 0 else Time.get_unix_time_from_system()
	var minutes := int(floor((now - when) / 60.0))
	if minutes < 1:
		return "agora mesmo"
	if minutes < 60:
		return "há %d %s" % [minutes, "minuto" if minutes == 1 else "minutos"]
	if minutes < 60 * 24:
		return "há %d %s" % [minutes / 60, "hora" if minutes / 60 == 1 else "horas"]
	return "há %d %s" % [minutes / 1440, "dia" if minutes / 1440 == 1 else "dias"]


func close() -> void:
	if options != null:
		options.queue_free()
		options = null
		_column_root.visible = true
	visible = false
	save_button.text = "Salvar agora"
	await _restore_clock()
	resume_pressed.emit()
	if app != null and app.player_mode and app.rig != null: # #222: de volta ao jogo, o mouse volta a olhar
		app.rig.set_cursor_captured(true)


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
