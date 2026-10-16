extends Control
## Catálogo do servidor: nenhum snapshot ou identidade é produzido pelo cliente.

signal loaded()
signal back()

var api
var rows: VBoxContainer
var message: Label
var confirmation: ConfirmationDialog
var pending_delete := ""
var busy := false


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	add_child(center)
	var column := VBoxContainer.new()
	column.custom_minimum_size = Vector2(700, 420)
	column.add_theme_constant_override("separation", 12)
	center.add_child(column)
	var title := Label.new()
	title.text = "Carregar mundo"
	title.add_theme_font_size_override("font_size", 32)
	column.add_child(title)
	message = Label.new()
	message.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	column.add_child(message)
	var scroll := ScrollContainer.new()
	scroll.custom_minimum_size.y = 280
	column.add_child(scroll)
	rows = VBoxContainer.new()
	rows.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	scroll.add_child(rows)
	var refresh := Button.new()
	refresh.text = "Atualizar lista"
	refresh.pressed.connect(refresh_worlds)
	column.add_child(refresh)
	var return_button := Button.new()
	return_button.text = "Voltar"
	return_button.pressed.connect(func(): back.emit())
	column.add_child(return_button)
	confirmation = ConfirmationDialog.new()
	confirmation.title = "Apagar mundo"
	confirmation.ok_button_text = "Apagar definitivamente"
	confirmation.confirmed.connect(_delete_confirmed)
	add_child(confirmation)


func refresh_worlds() -> void:
	if busy:
		return
	busy = true
	_clear_rows()
	message.text = "Carregando mundos…"
	var response: Dictionary = await api.saved_worlds()
	busy = false
	if not response.ok:
		message.text = "Não foi possível listar os mundos. Tente novamente."
		return
	message.text = "Nenhum mundo salvo." if response.body.is_empty() else "Escolha um mundo para continuar."
	for world in response.body:
		var row := HBoxContainer.new()
		rows.add_child(row)
		var name_label := Label.new()
		name_label.text = "%s · seed %s · hora %s · %s habitantes" % [world.name, str(world.seed), str(world.currentDate), str(world.population)]
		name_label.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(name_label)
		var load_button := Button.new()
		load_button.text = "Carregar"
		load_button.pressed.connect(func(): load_world(String(world.worldId)))
		row.add_child(load_button)
		var delete_button := Button.new()
		delete_button.text = "Apagar"
		delete_button.pressed.connect(func(): request_delete(String(world.worldId), String(world.name)))
		row.add_child(delete_button)


func _clear_rows() -> void:
	for child in rows.get_children():
		rows.remove_child(child)
		child.queue_free()


func load_world(id: String) -> void:
	if busy:
		return
	busy = true
	message.text = "Carregando mundo…"
	var response: Dictionary = await api.load_saved_world(id)
	busy = false
	if response.ok:
		loaded.emit()
	else:
		message.text = "Não foi possível carregar o mundo. Atualize a lista e tente novamente."


func request_delete(id: String, world_name: String) -> void:
	if busy:
		return
	pending_delete = id
	confirmation.dialog_text = "Apagar ‘%s’? Esta ação remove o mundo e sua história." % world_name
	confirmation.popup_centered()


func _delete_confirmed() -> void:
	if busy or pending_delete == "":
		return
	busy = true
	var response: Dictionary = await api.delete_saved_world(pending_delete)
	pending_delete = ""
	busy = false
	if response.ok:
		await refresh_worlds()
	else:
		message.text = "O mundo atual não pode ser apagado." if response.status == 409 else "Não foi possível apagar o mundo. Tente novamente."
