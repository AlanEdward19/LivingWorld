extends RefCounted
## Atalhos de teclado e clique do App (ver a linha de ajuda em AppUi.HELP). Só traduz a tecla em ação do App.

## Esc pula as dicas ou fecha a ajuda; depois fecha a janela da frente, a conversa e sai do prédio; sem nada aberto,
## abre (ou fecha) o menu de pausa. Sair da cidade: botão "◀ Mapa do mundo" ou afastar a câmera.
static func escape(app) -> void:
	if app.first_steps != null and app.first_steps.handle_escape():
		return
	if app.pause_menu != null and app.pause_menu.is_open():
		app.pause_menu.close()
		return
	if app.reading != null and app.reading.close_overlays():
		return
	if app.router.close_top():
		return
	if app.conversation.is_open:
		app.conversation.end_conversation()
	elif app.stage.active:
		app.exit_building()
	elif app.pause_menu != null:
		app.pause_menu.open()


static func handle(app, event: InputEvent) -> void:
	if event is InputEventKey and event.pressed:
		match event.keycode:
			KEY_ESCAPE:
				escape(app)
			KEY_F1:
				app.first_steps.toggle_help()
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
			KEY_N:
				app.next_event()
			KEY_V:
				app.toggle_watch()
			KEY_L:
				app.list_panel.visible = not app.list_panel.visible
				if app.list_panel.visible and app.list_panel.rows.is_empty():
					app.list_panel.load_page()
			KEY_F:
				if app.director.active():
					app.unfollow()
				else:
					app.follow_selected()
			KEY_X: # raio-x: todos os prédios translúcidos (AD-177)
				app.reading.toggle_xray()
			KEY_P: # planta baixa da cidade
				app.reading.toggle_plan()
			KEY_TAB: # o prédio de trás sob o cursor
				app.reading.cycle(app.get_viewport().get_mouse_position(), -1 if event.shift_pressed else 1)
			KEY_PAGEUP, KEY_BRACKETRIGHT:
				if app.stage.active:
					app.stage.set_floor(app.stage.interior_view.current_floor + 1)
			KEY_PAGEDOWN, KEY_BRACKETLEFT:
				if app.stage.active:
					app.stage.set_floor(app.stage.interior_view.current_floor - 1)
	elif event is InputEventJoypadButton and event.pressed and (event.button_index == JOY_BUTTON_START or event.is_action_pressed("ui_cancel")):
		escape(app) # controle: Start ou B fazem o mesmo que Esc
	elif event is InputEventMouseButton and event.pressed and event.alt_pressed and event.button_index in [MOUSE_BUTTON_WHEEL_UP, MOUSE_BUTTON_WHEEL_DOWN]:
		app.reading.cycle(event.position, 1 if event.button_index == MOUSE_BUTTON_WHEEL_DOWN else -1) # Alt+roda: igual ao Tab
	elif event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		app._pick(event.position, event.double_click)
