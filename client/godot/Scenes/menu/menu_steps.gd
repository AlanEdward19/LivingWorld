extends RefCounted
## Roteiro de capturas do MENU (o `--steps` comum pula o menu e vai ao mundo). Começa com `menu`:
##   godot --path client/godot -- --steps "menu;wait:2;shot:/tmp/titulo.png;new;wait:2;step:3;wait:2;shot:/tmp/mapa.png;quit"
## Passos: menu · wait:s · shot:caminho · size:LxA · new (Novo mundo) · step:N (passo do Novo mundo) · surprise ·
##         custom (Personalizar) · worlds (Meus mundos) · options · offline (tela sem mundo ligado) · loading ·
##         play (entra no mundo; os passos seguintes são do jogo, ver dev_steps.gd) · quit.
## AD-175 (teste de verdade pelo play.sh): create (cria o mundo do assistente sem esperar, para fotografar o carregamento) ·
##         rename:Nome (renomeia o mundo aberto em Meus mundos) · title (volta ao título e relê o servidor) ·
##         sandbox[:periodo] (#252: o botão de início rápido do título; segue com passos do jogo) · continue (Continuar) · resume (#246: Continuar o mundo atual, sem recriar, e segue com passos do jogo; a API fica ligada entre rodadas) · savenow (no jogo: abre a pausa e aperta Salvar agora).

const DevSteps := preload("res://Scenes/dev_steps.gd")


static func is_menu(steps: Array) -> bool:
	return not steps.is_empty() and String(steps[0]) == "menu"


static func run(boot: Node, steps: Array) -> void:
	for i in steps.size():
		var step: String = steps[i]
		var name: String = step.get_slice(":", 0)
		var arg: String = step.get_slice(":", 1) if ":" in step else ""
		var menu = boot.menu
		match name:
			"wait":
				await boot.get_tree().create_timer(float(arg)).timeout
			"shot":
				await RenderingServer.frame_post_draw
				boot.get_viewport().get_texture().get_image().save_png(arg)
				print("shot ", arg)
			"size":
				var wh: PackedStringArray = arg.split("x")
				DisplayServer.window_set_size(Vector2i(int(wh[0]), int(wh[1])))
				await boot.get_tree().create_timer(0.5).timeout
			"new":
				menu.show_new_world()
			"step":
				await menu.wizard.go_to(int(arg))
			"surprise":
				await menu.wizard.surprise()
			"custom":
				await menu.wizard.go_to(menu.wizard.CUSTOM_STEP)
			"worlds":
				menu.show_saved_worlds()
			"options":
				menu.show_options()
			"offline":
				menu.offline.set_can_start(false)
				menu._switch(menu.offline)
			"loading":
				menu._show_loading("Criando Valdor", 1)
			"play":
				boot._play()
				await boot.get_tree().create_timer(0.2).timeout
				await DevSteps.run(boot.app, steps.slice(i + 1))
				return
			"sandbox": # #252: o botão "Sandbox: casal" do título: cria o mundo do período de início rápido e entra nele; o resto são passos do jogo
				await menu.quick_start(arg if arg != "" else "sandbox-casal")
				await boot.get_tree().create_timer(0.5).timeout
				for _i in 400: # #256: o início rápido entra sozinho no corpo da primeira pessoa da casa; espera isso terminar (até ~40 s)
					if boot.app == null or boot.app.quick_entered:
						break
					await boot.get_tree().create_timer(0.1).timeout
				await DevSteps.run(boot.app, steps.slice(i + 1))
				return
			"create":
				menu.wizard.creation_progress.connect(func(index: int, count: int, text: String): print("etapa %d/%d: %s" % [index + 1, count, text]))
				menu.wizard.created.connect(func(_t: bool): print("criado: ", menu.wizard.world_name))
				menu.wizard.create()
			"rename":
				var screen = menu.saved_worlds_screen
				if screen == null:
					menu.show_saved_worlds()
					await boot.get_tree().create_timer(1.0).timeout
					screen = menu.saved_worlds_screen
				var list: Dictionary = await menu.api.saved_worlds()
				for world in list.body:
					if bool(world.get("isCurrent", false)):
						screen.request_rename(String(world.worldId), String(world.name))
				screen.rename_edit.text = arg
				await screen._rename_confirmed()
				print("rename ", arg, ": ", screen.message.text)
			"title":
				menu.show_title()
				await menu.refresh_server()
				print("continue: ", menu.title.continue_button.visible, " ", menu.title.continue_button.text.replace("\n", " | "))
			"continue":
				menu._on_continue()
			"resume": # #246 (5º teste): Continuar o mundo que a API já tem (sem recriar) e seguir com passos do jogo: dois dias no mesmo mundo
				menu._on_continue()
				for _i in 300:
					if boot.app != null:
						break
					await boot.get_tree().create_timer(0.1).timeout
				await boot.get_tree().create_timer(1.5).timeout
				await DevSteps.run(boot.app, steps.slice(i + 1))
				return
			"savenow":
				await boot.app.pause_menu.open()
				await boot.app.pause_menu.save_now()
				print("save: ", boot.app.pause_menu.save_button.text, " ", boot.app.pause_menu.save_line.text.replace("\n", " | "))
			"quit":
				boot.get_tree().quit()
