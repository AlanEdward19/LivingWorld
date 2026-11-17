extends Control
## Meus mundos: um cartão por mundo salvo no servidor, tudo vindo de `GET /worlds` (AD-175): nome, ano, pessoas, lugar
## de origem, quando foi jogado, se é o aberto agora e se é o mundo inicial. Jogar abre (já rodando, se o menu pode);
## Renomear pede o nome e mostra o erro do servidor; Apagar pede confirmação com o nome. Nenhum dado de mundo é
## produzido pelo cliente.

signal loaded()
signal back()

const Kit := preload("res://Scenes/menu/menu_kit.gd")
const UiSounds := preload("res://Scenes/menu/ui_sounds.gd")

var api
var rows: VBoxContainer
var message: Label
var confirmation: ConfirmationDialog
var pending_delete := ""
var pending_name := ""
var busy := false
var start_running := false ## abrir já rodando (o menu só liga com o token de admin da sessão)
var rename_dialog: ConfirmationDialog
var rename_edit: LineEdit
var rename_error: Label
var pending_rename := ""
var _back_button: Button


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var column := Kit.screen(self, 760)
	Kit.title("Meus mundos", 36, column, Kit.GOLD)
	message = Kit.text("", column, 16)
	message.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size.y = 380
	scroll.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	column.add_child(scroll)
	rows = VBoxContainer.new()
	rows.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	rows.add_theme_constant_override("separation", 10)
	scroll.add_child(rows)
	_back_button = Kit.big_button("◀ Voltar", column, func(): back.emit())
	confirmation = ConfirmationDialog.new()
	confirmation.title = "Apagar mundo"
	confirmation.cancel_button_text = "Manter"
	confirmation.confirmed.connect(_delete_confirmed)
	add_child(confirmation)
	rename_dialog = ConfirmationDialog.new()
	rename_dialog.title = "Renomear mundo"
	rename_dialog.ok_button_text = "Renomear"
	rename_dialog.cancel_button_text = "Cancelar"
	rename_dialog.dialog_hide_on_ok = false # com erro, fica aberto mostrando o motivo
	var rename_box := VBoxContainer.new()
	rename_edit = LineEdit.new()
	rename_edit.max_length = 60 # o servidor é quem diz o limite (40); passar dele mostra a frase dele
	rename_edit.custom_minimum_size.x = 360
	rename_edit.text_submitted.connect(func(_t: String): _rename_confirmed())
	rename_box.add_child(rename_edit)
	rename_error = Kit.text("", rename_box, 15, Kit.ERROR)
	rename_dialog.add_child(rename_box)
	rename_dialog.confirmed.connect(_rename_confirmed)
	add_child(rename_dialog)


func refresh_worlds() -> void:
	if busy:
		return
	busy = true
	_clear_rows()
	message.text = "Carregando mundos…"
	var response: Dictionary = await api.saved_worlds()
	busy = false
	if not response.ok:
		message.text = "Não deu para ver a lista de mundos. Tente de novo daqui a pouco."
		return
	message.text = "Nenhum mundo salvo ainda. Crie um em Novo mundo!" if response.body.is_empty() else "Escolha um mundo para jogar."
	for world in response.body:
		_card(world, bool(world.get("isCurrent", false)))
	focus_default()


## Um cartão por mundo, do jogado mais recentemente para o mais antigo; o aberto agora vem marcado pelo servidor.
func _card(world: Dictionary, is_current: bool) -> void:
	var panel := PanelContainer.new()
	rows.add_child(panel)
	var row := Kit.row(panel, 14)
	var info := VBoxContainer.new()
	info.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(info)
	var name_label := Kit.title(display_name(world), 22, info, Color.WHITE)
	name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_LEFT
	Kit.text(describe(world, is_current), info, 15)
	var play := Kit.button("Jogar", row, func(): load_world(String(world.worldId)))
	play.custom_minimum_size.x = 120
	Kit.button("Renomear", row, func(): request_rename(String(world.worldId), display_name(world)))
	var remove := Kit.button("Apagar", row, func(): request_delete(String(world.worldId), display_name(world)))
	if is_current:
		remove.disabled = true
		remove.tooltip_text = "Este mundo está aberto agora. Abra outro antes de apagar este."


## O mundo que a API cria sozinha no primeiro uso não tem nome.
static func display_name(world: Dictionary) -> String:
	var name := String(world.get("name", "")).strip_edges()
	return name if name != "" else "Mundo sem nome"


func describe(world: Dictionary, is_current: bool) -> String:
	var parts: PackedStringArray = []
	parts.append("ano %d" % int(world.get("year", 0))) # como o relógio do jogo: o mundo nasce no ano 0
	var people := int(world.get("population", 0))
	parts.append("%d %s" % [people, "pessoa" if people == 1 else "pessoas"])
	if String(world.get("periodName", "")) != "":
		parts.append(String(world.periodName))
	if String(world.get("origin", "")) == "automatic":
		parts.append("mundo inicial")
	parts.append(when_played(String(world.get("lastPlayedAtUtc", world.get("lastSavedAtUtc", "")))))
	return ("Aberto agora · " if is_current else "") + " · ".join(parts)


## "jogado hoje", "ontem", "há 3 dias" a partir da última gravação do servidor.
static func when_played(iso: String, now_unix: float = -1.0) -> String:
	if iso == "":
		return "nunca jogado"
	var when := Time.get_unix_time_from_datetime_string(iso.get_slice(".", 0).trim_suffix("Z"))
	var now := now_unix if now_unix >= 0 else Time.get_unix_time_from_system()
	var days := int(floor((now - when) / 86400.0))
	if days <= 0:
		return "jogado hoje"
	if days == 1:
		return "jogado ontem"
	return "jogado há %d dias" % days


func _clear_rows() -> void:
	for child in rows.get_children():
		rows.remove_child(child)
		child.queue_free()


func load_world(id: String) -> void:
	if busy:
		return
	busy = true
	message.text = "Abrindo o mundo…"
	var response: Dictionary = await api.load_saved_world(id, start_running)
	busy = false
	if response.ok:
		loaded.emit()
	else:
		message.text = "Não deu para abrir este mundo. Atualize a lista e tente de novo."


## Apagar é para sempre: o diálogo diz o nome do mundo e o botão também; "Manter" é o foco inicial.
func request_delete(id: String, world_name: String) -> void:
	if busy:
		return
	pending_delete = id
	pending_name = world_name
	confirmation.dialog_text = "Apagar “%s” para sempre?\nToda a história deste mundo some e não volta." % world_name
	confirmation.ok_button_text = "Apagar “%s”" % world_name
	confirmation.popup_centered()
	confirmation.get_cancel_button().grab_focus()


func _delete_confirmed() -> void:
	if busy or pending_delete == "":
		return
	busy = true
	var response: Dictionary = await api.delete_saved_world(pending_delete)
	pending_delete = ""
	busy = false
	if response.ok:
		await refresh_worlds()
		message.text = "“%s” foi apagado." % pending_name if rows.get_child_count() > 0 else "Nenhum mundo salvo ainda. Crie um em Novo mundo!"
	else:
		message.text = "Este mundo está aberto agora e não pode ser apagado." if response.status == 409 else "Não deu para apagar o mundo. Tente de novo."


## Renomear: o diálogo já vem com o nome atual selecionado; o servidor valida (vazio, longo, repetido).
func request_rename(id: String, world_name: String) -> void:
	if busy:
		return
	pending_rename = id
	rename_edit.text = world_name
	rename_error.text = ""
	rename_dialog.popup_centered()
	rename_edit.grab_focus()
	rename_edit.select_all()


func _rename_confirmed() -> void:
	if busy or pending_rename == "":
		return
	busy = true
	var chosen := rename_edit.text.strip_edges()
	var response: Dictionary = await api.rename_world(pending_rename, chosen)
	busy = false
	if not response.ok:
		rename_error.text = String(response.error) if String(response.error) != "" else "Não deu para renomear agora. Tente de novo."
		UiSounds.cue(self, "error")
		return
	pending_rename = ""
	rename_dialog.hide()
	UiSounds.cue(self, "confirm")
	await refresh_worlds()
	message.text = "Agora o mundo se chama “%s”." % chosen


func focus_default() -> void:
	if not is_inside_tree():
		return
	var first := Kit.first_focusable(rows)
	(first if first != null else _back_button).grab_focus()


func _unhandled_input(event: InputEvent) -> void:
	if is_visible_in_tree() and not confirmation.visible and not rename_dialog.visible and event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		back.emit()
