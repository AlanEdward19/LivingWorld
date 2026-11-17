extends RefCounted
## Atalhos de teclado e clique do App (ver a linha de ajuda em AppUi.HELP). Só traduz a tecla em ação do App.

## Esc fecha a janela da frente; sem janela, a conversa; depois sai do prédio e, por fim, da cidade.
static func escape(app) -> void:
	if app.router.close_top():
		return
	if app.conversation.is_open:
		app.conversation.end_conversation()
	elif app.stage.active:
		app.exit_building()
	elif app.store.kind == "City":
		app.enter_scope("World", "")


static func handle(app, event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_ESCAPE:
				escape(app)
			KEY_BACKSPACE:
				if app.inspector.is_open():
					app.inspector.back()
			KEY_F10:
				app.menu_requested.emit()
			KEY_B:
				app._toggle_brain()
			KEY_F8:
				app._toggle_admin()
			KEY_R:
				app._toggle_reports()
			KEY_I:
				if app.selected_npc >= 0 and not app.player_hud.is_active:
					app.incarnate(app.selected_npc)
			KEY_T:
				if app.selected_npc >= 0 and not app.conversation.input.has_focus():
					app.talk_to(app.selected_npc)
			KEY_M:
				app.ambience.set_muted(not app.ambience.muted)
			KEY_L:
				app.list_panel.visible = not app.list_panel.visible
				if app.list_panel.visible and app.list_panel.rows.is_empty():
					app.list_panel.load_page()
			KEY_F:
				if app.director.active():
					app.unfollow()
				else:
					app.follow_selected()
			KEY_PAGEUP, KEY_BRACKETRIGHT:
				if app.stage.active:
					app.stage.set_floor(app.stage.interior_view.current_floor + 1)
			KEY_PAGEDOWN, KEY_BRACKETLEFT:
				if app.stage.active:
					app.stage.set_floor(app.stage.interior_view.current_floor - 1)
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		app._pick(event.position, event.double_click)
