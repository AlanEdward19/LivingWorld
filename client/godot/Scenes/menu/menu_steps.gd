extends RefCounted
## Roteiro de capturas do MENU (o `--steps` comum pula o menu e vai ao mundo). Começa com `menu`:
##   godot --path client/godot -- --steps "menu;wait:2;shot:/tmp/titulo.png;new;wait:2;step:3;wait:2;shot:/tmp/mapa.png;quit"
## Passos: menu · wait:s · shot:caminho · size:LxA · new (Novo mundo) · step:N (passo do Novo mundo) · surprise ·
##         custom (Personalizar) · worlds (Meus mundos) · options · offline (tela sem mundo ligado) · loading ·
##         play (entra no mundo; os passos seguintes são do jogo, ver dev_steps.gd) · quit.

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
			"quit":
				boot.get_tree().quit()
