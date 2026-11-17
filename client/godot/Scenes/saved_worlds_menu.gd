extends Control
## Meus mundos: um cartão por mundo salvo no servidor (nome, ano, pessoas, última vez jogado; época só do mundo
## aberto, a lista da API não traz). Jogar abre; Apagar pede confirmação com o nome do mundo. Nenhum dado de mundo é
## produzido pelo cliente. Renomear não existe na API (lacuna no backlog 09).

signal loaded()
signal back()

const Kit := preload("res://Scenes/menu/menu_kit.gd")

var api
var rows: VBoxContainer
var message: Label
var confirmation: ConfirmationDialog
var pending_delete := ""
var pending_name := ""
var busy := false
var current_world := {} ## {name, period} do mundo aberto agora (GET /world/info), para marcar o cartão
var hours_per_year := 0.0 ## do calendário do mundo aberto; 0 = mostra o tempo em dias
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


func refresh_worlds() -> void:
	if busy:
		return
	busy = true
	_clear_rows()
	message.text = "Carregando mundos…"
	var response: Dictionary = await api.saved_worlds()
	if response.ok and not response.body.is_empty():
		await _load_context()
	busy = false
	if not response.ok:
		message.text = "Não deu para ver a lista de mundos. Tente de novo daqui a pouco."
		return
	message.text = "Nenhum mundo salvo ainda. Crie um em Novo mundo!" if response.body.is_empty() else "Escolha um mundo para jogar."
	for i in response.body.size():
		_card(response.body[i], i == 0)
	focus_default()


## Nome e época do mundo aberto e o calendário (para mostrar o ano). Se falhar, o cartão só não mostra isso.
func _load_context() -> void:
	if not api.has_method("world_info"):
		return
	var info: Dictionary = await api.world_info()
	if info.ok and info.body is Dictionary:
		current_world = {"name": String(info.body.get("name", "")), "period": String(info.body.get("periodId", ""))}
	var calendar: Dictionary = await api.world_calendar()
	if calendar.ok and calendar.body is Dictionary:
		hours_per_year = float(calendar.body.get("hoursPerYear", 0))


## A lista vem do mais recente para o mais antigo: o primeiro é o mundo aberto agora.
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
	var remove := Kit.button("Apagar", row, func(): request_delete(String(world.worldId), display_name(world)))
	if is_current:
		remove.disabled = true
		remove.tooltip_text = "Este mundo está aberto agora. Abra outro antes de apagar este."


## O mundo que a API cria sozinha no primeiro uso não tem nome.
static func display_name(world: Dictionary) -> String:
	var name := String(world.get("name", "")).strip_edges()
	return name if name != "" else "Mundo sem nome"


func describe(world: Dictionary, is_current: bool) -> String:
	var hours := float(world.get("currentDate", 0))
	var parts: PackedStringArray = []
	if hours_per_year > 0:
		parts.append("ano %d" % int(hours / hours_per_year)) # como o relógio do jogo: o mundo nasce no ano 0
	else:
		parts.append("dia %d" % (int(hours / 24.0) + 1))
	var people := int(world.get("population", 0))
	parts.append("%d %s" % [people, "pessoa" if people == 1 else "pessoas"])
	if is_current and current_world.get("period", "") != "":
		parts.append(String(current_world.period).replace("-", " ").capitalize())
	parts.append(when_played(String(world.get("lastSavedAtUtc", ""))))
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
	var response: Dictionary = await api.load_saved_world(id)
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


func focus_default() -> void:
	if not is_inside_tree():
		return
	var first := Kit.first_focusable(rows)
	(first if first != null else _back_button).grab_focus()


func _unhandled_input(event: InputEvent) -> void:
	if is_visible_in_tree() and not confirmation.visible and event.is_action_pressed("ui_cancel"):
		get_viewport().set_input_as_handled()
		back.emit()
