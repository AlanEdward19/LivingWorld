extends RefCounted
## Atalhos de teclado e clique do App (ver a linha de ajuda em AppUi.HELP). Só traduz a tecla em ação do App.

## Esc pula as dicas ou fecha a ajuda; depois fecha a janela da frente, a conversa e sai do prédio; sem nada aberto,
## abre (ou fecha) o menu de pausa. Sair da cidade: botão "◀ Mapa do mundo" ou afastar a câmera.
static func escape(app) -> void:
	if app.player_mode and app.player_hud.orders_open(): # #222: no modo jogador, Esc fecha as ações antes do menu
		app.player_hud.toggle_orders()
		return
	if app.player_mode and app.conversation.is_open:
		app.conversation.end_conversation()
		return
	if app.rig != null and app.rig.cursor_captured: # AD-197k: Esc solta o cursor antes de qualquer menu
		app.rig.set_cursor_captured(false)
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


## #222: no modo jogador só valem as teclas do jogador (Esc e F1 aqui; o resto é do player_hud e da câmera): os atalhos do
## espectador (B, R, T, M, N, V, L, F, X, P, H, Tab...) não disparam no meio do jogo.
const PLAYER_KEYS := [KEY_ESCAPE, KEY_F1, KEY_F10, KEY_QUESTION, KEY_SLASH, KEY_R] ## #267: R é o golpe do jogador (o clique agora vai até / interage)


static func handle(app, event: InputEvent) -> void:
	if app.player_mode and event is InputEventKey and event.pressed and not event.keycode in PLAYER_KEYS:
		return
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
				if app.player_mode:
					if app.player_hud.is_active and not app.modal.active and not app.conversation.input.has_focus():
						app.strike_at(app.aim_point()) # #267: o golpe na pessoa sob a mira (Shift+clique também)
				else:
					app._toggle_reports()
			KEY_I:
				if app.selected_npc >= 0 and not app.player_hud.is_active:
					app.incarnate(app.selected_npc)
			KEY_T:
				if app.selected_npc >= 0 and not app.conversation.input.has_focus():
					app.talk_to(app.selected_npc)
			KEY_M:
				app.ambience.set_muted(not app.ambience.muted)
			KEY_K: # #222: minimapa sob demanda
				app.minimap_wanted = not app.minimap_wanted
				app.minimap.visible = app.minimap_wanted
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
			KEY_H: # mapa de calor do pisoteio das ruas (AD-181)
				app.reading.toggle_heat()
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
		if app.player_hud != null and app.player_hud.is_active and app.rig.mode != app.rig.ISO:
			if app.modal.active:
				return # #240: com popup aberto o clique é do popup, nunca golpe
			# #267: como o Sims, o clique vai até / interage com o que está sob a mira (o centro da tela com o cursor preso) ou sob o cursor livre: a pessoa, o móvel, o prédio;
			# o golpe é Shift+clique (ou a tecla R): AD-197j, o motor valida o alcance
			var at: Vector2 = app.aim_point() if app.rig.cursor_captured else event.position
			if event.shift_pressed and app.player_hud._held_keys() == Vector2i.ZERO: # Shift segurado correndo não vira golpe: só parado
				app.strike_at(at)
			else:
				app.click_at(at)
		else:
			app._pick(event.position, event.double_click)
