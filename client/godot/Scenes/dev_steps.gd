extends RefCounted
## Roteiro de desenvolvimento para capturas e demonstrações automáticas:
##   godot --path client/godot -- --steps "wait:2;shot:/tmp/a.png;city;wait:2;shot:/tmp/b.png;enter:0;quit"
## Passos (#264, modo comprar: buy · buytab:buy|mine|build · buycat · buyitem:id · buyfind:id[,cômodo] · buyat:X,Y[,giro] · buyface:graus · buyask · buyclick · buycmd:verbo · buyhome · buycut:cômodo · buyedgefind:op · buyextendfind · buytool:nome · buypaint:id · buyroomcell:tipo · buymovefind:N · buyonpiece:N · mousepos:X,Y · mousebuy · golocal:X,Y · partner) · (#258: goto:X,Y · skip:S · delta:MS · speed:N · #260: speedinfo · lamps · walls): wait:s · shot:caminho · city (entra na 1ª cidade) · world · enter:ID · exit · floor:N · zoom:fator ·
##         yaw:graus · list · select:ID · brain:ID · whatif:fome · talk:ID · gametalk:ID (conversa do modo jogador, #237) · pick:N (fala pronta N) · say:texto · timesay:texto (mede quanto a UI espera) · bio:ID · chronicle · reports · player:ID · switch (#252: troca para a outra pessoa da casa) · useverb:Objeto|Uso (#255: usa ou enfileira o uso do móvel) · needs (#255: barras, dica, ação e fila) · waituse:S · cancelq:N · where (#252: onde está o corpo) · use:texto (#252: usa a oferta do motor com esse texto) · act:Nome · creator · creatorstep:N (etapa do criador, #222) · creatorrandom · creatorset:campo=valor,... · creatorcreate (AD-197t) · creatorpick:N (AD-197q) · materialize (aperta Ver em detalhe) · demo:janela (mostra uma janela com dados de exemplo) · size:LxA · treasury · trades:ID · tech:CULTURA · admin:motivo (pausa com o console) · adminsearch:texto · follow · near:zoom · selectfirst · building · next · watch · benchbig:N · third/first (câmera do jogador, AD-197d) · cam:first|third (só o modo, AD-197o) · playstreet (encarna quem está na rua) · unplayer (sai do personagem; medida pareada) · stacks (conta pessoas sobrepostas, AD-197r) · xray · plan · street · lot · ground (AD-177) · roads · walker · walkloop:S (#248: anda S s virando o olhar e mede puxões) · walkstats[:reset] · heat (AD-181) · pause (menu de pausa) · tips:N (dica N) · help · quit. Não é usado em produção (sem --steps nada roda).


const Registry := preload("res://Store/entity_registry.gd")
const Grid := preload("res://Scenes/grid.gd")
const FrameCost := preload("res://Contract/frame_cost.gd")
const PlayerFrame := preload("res://Scenes/player_frame.gd")
const SocialMenu := preload("res://Scenes/social_menu.gd")


static func parse(args: PackedStringArray) -> Array:
	for i in args.size():
		if args[i] == "--steps" and i + 1 < args.size():
			return args[i + 1].split(";", false)
	return []


static func _print_warning(text: String) -> void:
	print("aviso: ", text)


static func _need_line(sheet: Dictionary) -> String:
	var line := PackedStringArray()
	for key in ["health", "hunger", "thirst", "sleep", "social"]:
		line.append("%s=%s" % [key, sheet.get(key)])
	for row in sheet.get("catalogNeeds", []):
		line.append("%s=%d%s" % [row.id, int(row.value), "!" if bool(row.urgent) else ""])
	return " ".join(line)


static func _print_needs(app: Node) -> void:
	app.player_hud.api.invalidate_npc(app.player_hud.npc_id)
	var sheet: Dictionary = (await app.player_hud.api.npc_detail(app.player_hud.npc_id)).body
	print("needs: [", app.player_frame.clock_label.text, "] ", app.player_hud.npc_name, " ", _need_line(sheet), " | dica: ", PlayerFrame.hint_line(sheet.get("needHint")), " | busy: ", app.player_hud.busy.action, "/",
		app.player_hud.busy.phase, "/", app.player_hud.busy.label, " | fila: ", app.player_frame.queue_label.text.replace("\n", " "))


## #262: o humor do corpo e da outra pessoa pela ficha do motor, e o que o cartão do jogador mostra (rosto, linha da emoção, momentos).
static func _print_mood(app: Node) -> void:
	var who: Array = [[app.player_hud.npc_id, app.player_hud.npc_name]]
	for housemate in app.player_hud.housemates:
		who.append([int(housemate.id), String(housemate.name)])
	for entry in who:
		app.player_hud.api.invalidate_npc(int(entry[0]))
		var sheet: Dictionary = (await app.player_hud.api.npc_detail(int(entry[0]))).body
		var mood: Variant = sheet.get("mood")
		if not mood is Dictionary:
			print("mood: ", entry[1], " (sem humor do motor)")
			continue
		var moments: Array = mood.moments.map(func(m): return "%s %+.0f%s" % [m.name, float(m.weight), (" · " + PlayerFrame.remaining_text(m.get("remainingMs"))) if m.get("remainingMs") != null else ""])
		print("mood: ", entry[1], " ", mood.emotionName, " (", mood.emotion, ", rosto ", mood.get("face"), ") nota ", mood.score, " ", mood.get("label"), " | momentos: ", moments, " | aspiração: ", mood.get("aspirationName"),
			" ", mood.points, " pts | desejos: ", mood.wishes.map(func(w): return w.text), " | cumpridos: ", mood.recent.map(func(d): return d.text), " | dominante do observável: ", sheet.get("dominantEmotion"))
	print("mood-card: [", app.player_frame.clock_label.text, "] ", app.player_frame.mood_label.text if app.player_frame.mood_row.visible else "(sem linha de humor)", " | ",
		app.player_frame.moments_box.get_children().map(func(l): return String(l.text)))


## #259: a linha da interação social `interaction` (trecho do nome) da pessoa `person` (trecho do nome), pelo menu "Interagir ▸" da oferta do motor.
static func _social_line(app: Node, person: String, interaction: String) -> Dictionary:
	var entries: Array = await app.player_hud._reach_entries()
	for entry in entries:
		if String(entry.get("verb", "")) != "social_menu" or not person.to_lower() in String(entry.text).to_lower():
			continue
		for cat in SocialMenu.category_entries(entry):
			if String(cat.get("verb", "")) != "social_cat":
				continue
			for line in SocialMenu.interaction_entries(cat):
				if String(line.verb).begins_with("social:") and interaction.to_lower() in String(line.text).to_lower():
					return line
	return {}


static func run(app: Node, steps: Array) -> void:
	for step in steps:
		var name: String = step.get_slice(":", 0)
		var arg: String = step.get_slice(":", 1) if ":" in step else ""
		match name:
			"wait":
				await app.get_tree().create_timer(float(arg)).timeout
			"shot":
				await RenderingServer.frame_post_draw
				app.get_viewport().get_texture().get_image().save_png(arg)
				print("shot ", arg)
				if app.get_node_or_null("FrameMeter") != null:
					app.get_node("FrameMeter").ignore(3) # o PNG custa ~130 ms: não é quadro do jogo
			"city": # `city:<id>` entra nessa cidade (AD-193: a fumaça escolhe a que tem obra); sem id, a primeira
				var ids: Array = app.store.cities.keys()
				if arg != "":
					await app.enter_scope("City", arg)
				elif ids.size() > 0:
					await app.enter_scope("City", ids[0])
			"world":
				await app.enter_scope("World", "")
			"enter":
				await app.enter_building(int(arg))
			"exit":
				await app.exit_building()
			"placed": # #237: `placed:-1` faz de conta que o motor disse "saiu do prédio" (`placed:<id>` entra), como a pose faz
				await app._on_player_placed(int(arg), 0)
			"floor":
				app.stage.set_floor(int(arg))
			"zoom":
				app.rig.zoom_by(float(arg))
			"yaw":
				app.rig.orbit(deg_to_rad(float(arg)))
			"list":
				app.list_panel.visible = true
				await app.list_panel.load_page()
			"select":
				await app.select_npc(int(arg))
			"brain":
				await app.select_npc(int(arg))
				await app.open_brain(int(arg))
			"whatif":
				app.brain_panel.set_sandbox_mode(true)
				app.brain_panel.need_sliders["hunger"].value = float(arg)
				await app.brain_panel.simulate()
			"bio":
				await app.select_npc(int(arg))
				await app.open_biography(int(arg))
			"chronicle":
				var cities: Array = app.store.cities.keys()
				if cities.size() > 0:
					await app.enter_scope("City", cities[0])
					await app.open_chronicle(String(cities[0]))
			"reports":
				await app.narrative.show_reports()
			"player":
				await app.select_npc(int(arg))
				await app.incarnate(int(arg))
			"unplayer": # AD-197r: sai do personagem (o servidor o libera; encarnar o mesmo corpo na rodada seguinte não dá 409)
				await app.player_hud.logout()
				print("unplayer: ativo=", app.player_hud.is_active)
			"switch": # #252 (sandbox do casal): troca de corpo para a outra pessoa da casa (o motor solta a de antes sem sumir)
				if app.player_hud.housemates.is_empty():
					await app.player_hud.refresh_housemates()
				var before: int = app.player_hud.npc_id
				var switched: bool = await app.player_hud.switch_body()
				print("switch: ", before, " -> ", app.player_hud.npc_id, " ok=", switched, " ativo=", app.player_hud.is_active)
			"social": # #259: `social:Marina|Elogiar` pede a interação social do catálogo (pelo menu "Interagir ▸" da oferta do motor); a recusa diz o motivo
				var sparts := arg.split("|")
				var picked_social := await _social_line(app, sparts[0], sparts[1] if sparts.size() > 1 else "")
				print("social: ", arg, " -> ", picked_social.get("text", "(nada)"), " ", "(esmaecida: " + String(picked_social.get("reason", "")) + ")" if bool(picked_social.get("disabled", false)) else "")
				if not picked_social.is_empty():
					await app.player_hud.interact(picked_social)
			"socialmenu": # #259: `socialmenu:Marina` abre a roda de baixo ("Interagir ▸" aberto nas categorias); `socialmenu:Marina|Amigável` abre as interações da categoria
				var mparts := arg.split("|")
				var found_social: Array = await app.player_hud._reach_entries()
				for entry in found_social:
					if String(entry.get("verb", "")) == "social_menu" and mparts[0].to_lower() in String(entry.text).to_lower():
						var shown: Array = SocialMenu.category_entries(entry)
						if mparts.size() > 1:
							for cat in shown:
								if String(cat.get("verb", "")) == "social_cat" and mparts[1].to_lower() in String(cat.text).to_lower():
									shown = SocialMenu.interaction_entries(cat)
									break
						app.player_hud.frame.show_menu(shown)
						app.player_hud._show_relation_of([entry])
						print("socialmenu: ", shown.map(func(e): return String(e.text) + (" [esmaecida: " + String(e.reason) + "]" if bool(e.get("disabled", false)) else "")))
						break
			"warnlog": # #259: imprime os avisos do HUD (os que o jogador lê: recusas, "Marina gostou do elogio (+5)"...)
				if not app.player_hud.warning.is_connected(_print_warning):
					app.player_hud.warning.connect(_print_warning)
			"socialnote": # #259: imprime o último fato social e se o corpo está numa interação, pela ficha do motor
				app.player_hud.api.invalidate_npc(app.player_hud.npc_id)
				var note_sheet: Dictionary = (await app.player_hud.api.npc_detail(app.player_hud.npc_id)).body
				print("socialnote: [", app.player_frame.clock_label.text, "] ", app.player_hud.npc_name, " fato=", note_sheet.get("socialNote"), " ocupado=", note_sheet.get("socialBusy"), " tarefa=", app.player_hud.busy.kind(), " | ", app.player_frame.busy_label.text)
			"accel": # #259: o tempo acelerado com o corpo parado (só quem joga sozinho): o motor corre até algo chamar
				await app.player_hud.accelerate()
			"watchsocial": # #259: `watchsocial:S` imprime por S s reais a ficha social do corpo e a da outra pessoa (ação e ocupação), a cada 2 s
				var watched := 0.0
				while watched < float(arg):
					app.player_hud.api.invalidate_npc(app.player_hud.npc_id)
					var mine_sheet: Dictionary = (await app.player_hud.api.npc_detail(app.player_hud.npc_id)).body
					var other_line := ""
					for housemate in app.player_hud.housemates:
						app.player_hud.api.invalidate_npc(int(housemate.id))
						var other_sheet: Dictionary = (await app.player_hud.api.npc_detail(int(housemate.id))).body
						other_line += " | %s ação=%s ocupado=%s social=%s" % [other_sheet.name, other_sheet.get("currentAction"), other_sheet.get("socialBusy"), other_sheet.get("social")]
					print("watchsocial: [", app.player_frame.clock_label.text, "] fato=", mine_sheet.get("socialNote"), " ocupado=", mine_sheet.get("socialBusy"), " tarefa=", app.player_hud.busy.kind(), other_line)
					await app.get_tree().create_timer(2.0).timeout
					watched += 2.0
			"relation": # #259: imprime a relação com a pessoa ao alcance, como o motor a diz (a que o painel mostra)
				await app.player_hud._reach_entries()
				for offer in app.player_hud.offers:
					if String(offer.get("kind", "")) == "person":
						print("relation: ", offer.get("label"), " ", offer.get("relation"))
			"use": # #252: `use:Geladeira` usa a oferta do motor cujo texto tem esse trecho (o que o E faria): imprime as ofertas e o que foi feito
				var entries: Array = await app.player_hud._reach_entries()
				var chosen: Dictionary = {}
				for entry in entries:
					if arg.to_lower() in String(entry.text).to_lower():
						chosen = entry
						break
				print("use: ", arg, " -> ", chosen.get("text", "(nada)"), " | ofertas: ", entries.map(func(e): return String(e.text)))
				if not chosen.is_empty():
					await app.player_hud.interact(chosen)
			"useverb": # #255: `useverb:Chuveiro|Tomar banho` usa (ou, com o corpo ocupado, enfileira) o uso do objeto pelo nome, pela oferta do motor
				var parts := arg.split("|")
				var found: Array = await app.player_hud._reach_entries()
				var object_entry: Dictionary = {}
				for entry in found:
					if String(entry.get("verb", "")) == "menu" and parts[0].to_lower() in String(entry.text).to_lower():
						object_entry = entry
						break
				var picked: Dictionary = {}
				for line in app.player_hud.use_entries(object_entry, app.player_hud.busy.active()) if not object_entry.is_empty() else []:
					if parts.size() < 2 or parts[1].to_lower() in String(line.text).to_lower():
						picked = line
						break
				print("useverb: ", arg, " -> ", picked.get("text", "(nada)"), " | objetos: ", found.map(func(e): return String(e.text)))
				if not picked.is_empty():
					await app.player_hud.interact(picked)
			"career": # #263: imprime o cartão do emprego (a linha do HUD), o cargo, o desempenho, as contas abertas, os avisos e as vagas, como o motor os diz na ficha
				app.player_hud.api.invalidate_npc(app.player_hud.npc_id)
				var career_sheet: Dictionary = (await app.player_hud.api.npc_detail(app.player_hud.npc_id)).body
				var career = career_sheet.get("career")
				print("career: [", app.player_frame.clock_label.text, "] ", app.player_hud.npc_name, " dinheiro=", career_sheet.get("money"), " | cartão: ", app.player_frame.career_label.text.replace("\n", " / "),
					" | job: ", (career.get("job") if career is Dictionary else null), " | contas: ", (career.get("bills") if career is Dictionary else null),
					" | vagas: ", (career.get("listings") if career is Dictionary else null))
				if career is Dictionary:
					for notice in career.get("notices", []):
						print("career-notice: ", notice.get("id"), " ", notice.get("kind"), " ", notice.get("text"))
			"careerpanel": # #263: abre (ou fecha) o painel da carreira, como a tecla O
				app.player_frame.toggle_career()
				await app.get_tree().create_timer(0.3).timeout
			"needs": # #255: imprime as barras do corpo (as de sempre e as do catálogo), a dica, a ação em curso e a fila
				await _print_needs(app)
			"buy": # #264: abre (ou fecha) o modo comprar e construir (a tecla B)
				await app.toggle_buy_mode()
				app.buy_mode.mouse_follow = false # a captura manda a célula; `mousebuy` devolve o mouse
				print("buy: aberto=", app.buy_mode.is_open, " dinheiro=", app.buy_mode.home.get("wallet"), " itens=", app.buy_mode.home.get("items", []).size(), " peças=", app.buy_mode.home.get("pieces", []).size(), " loja=", app.buy_mode.home.get("shop"))
			"mousepos": # #264: `mousepos:X,Y` põe o mouse da janela ali (o fantasma segue; o ponto vira célula pela câmera de cima) e imprime a célula e a volta ao pixel
				var mp: PackedStringArray = arg.split(",")
				var where := Vector2(float(mp[0]), float(mp[1]))
				app.get_viewport().warp_mouse(where)
				await app.get_tree().process_frame
				await app.get_tree().process_frame
				var cell_point: Vector3 = app.buy_mode.floor_point(where)
				app.buy_mode.point_hovered(cell_point)
				var back: Vector2 = app.rig.camera.unproject_position(Grid.to_world(app.stage.interior_view.origin + app.buy_mode.hover, 0.0))
				print("mousepos: ", where, " -> célula local ", app.buy_mode.hover, " aresta ", app.buy_mode.hover_edge, " | volta ao pixel (centro da célula) ", back, " | mouse da janela ", app.get_viewport().get_mouse_position())
			"mousebuy": # #264: o fantasma volta a seguir o mouse (use `hover:X,Y` para movê-lo)
				app.buy_mode.mouse_follow = true
			"buylook": # #266: `buylook:X,Y,tamanho[,giro[,inclinação]][,sem-painel]` aponta a câmera do modo comprar para o ponto X,Y da planta (célula + fração; tamanho = largura vista em unidades; giro em graus); "sem-painel" esconde os painéis
				var look := arg.split(",")
				var look_origin: Vector2i = app.stage.interior_view.origin
				var look_at := Vector3((float(look_origin.x) + float(look[0]) + 0.5) * Grid.CELL, float(app.buy_mode.floor_level) * Grid.floor_height(), (float(look_origin.y) + float(look[1]) + 0.5) * Grid.CELL)
				app.rig.build_pitch = -deg_to_rad(float(look[4])) if look.size() > 4 and look[4].is_valid_float() else app.rig.BUILD_PITCH
				app.rig.set_build_view(true, look_at, float(look[2]), deg_to_rad(float(look[3])) if look.size() > 3 and look[3].is_valid_float() else 0.0)
				for panel in [app.buy_mode._top, app.buy_mode._side, app.buy_mode._rooms_panel, app.buy_mode._foot]:
					if panel != null:
						panel.visible = not ("sem-painel" in look)
			"buytab": # #264: `buytab:buy|mine|build`
				app.buy_mode.set_tab(arg)
			"buycat": # #264: filtra a categoria (`buycat:decor`; vazio = tudo)
				app.buy_mode.set_category(arg)
			"buyitem": # #264: escolhe o móvel (`buyitem:sofa`)
				app.buy_mode.select_item(arg)
			"buytool": # #264: `buytool:wall|open|door|shut|extend|paint_floor|paint_wall` (+ `buypaint:id`)
				app.buy_mode.use_tool(arg)
			"buypaint":
				app.buy_mode.paint_id = arg
				app.buy_mode._rebuild()
			"buyface": # #264: giro em graus
				app.buy_mode.facing = int(arg)
				app.buy_mode._update_ghost()
			"buyat": # #264: `buyat:X,Y[,giro]` põe o fantasma na célula local e pergunta ao motor (imprime o veredito)
				var at: PackedStringArray = arg.split(",")
				app.buy_mode.hover = Vector2i(int(at[0]), int(at[1]))
				if at.size() > 2:
					app.buy_mode.facing = int(at[2])
				await app.buy_mode.ask(app.buy_mode.current_verb())
				app.buy_mode._update_ghost()
				app.buy_mode._update_marks()
				print("buyat: ", app.buy_mode.current_verb(), " -> ", app.buy_mode.verdict, " verde=", app.buy_mode.ghost_is_green(), " vermelho=", app.buy_mode.ghost_is_red())
			"buyedge": # #264: `buyedge:ax,ay,bx,by` a aresta alvo das ferramentas de obra
				var edge: PackedStringArray = arg.split(",")
				app.buy_mode.hover_edge = [int(edge[0]), int(edge[1]), int(edge[2]), int(edge[3])]
				app.buy_mode.hover = Vector2i(int(edge[0]), int(edge[1]))
				await app.buy_mode.ask(app.buy_mode.current_verb())
				app.buy_mode._update_marks()
				print("buyedge: ", app.buy_mode.current_verb(), " -> ", app.buy_mode.verdict)
			"buyfind": # #264: `buyfind:sofa[,sala]` acha pelo motor a primeira célula e giro onde o móvel cabe (opcionalmente no cômodo do tipo) e põe o fantasma ali
				var want: PackedStringArray = arg.split(",")
				app.buy_mode.select_item(want[0])
				var rooms: Array = app.store.interior.get("rooms", [])
				var allowed := {}
				if want.size() > 1 and rooms.size() > 0:
					for room in rooms[0].get("rooms", []):
						if String(room.get("kind", "")) == want[1]:
							for room_cell in room.cells:
								allowed[Vector2i(int(room_cell.x), int(room_cell.y))] = true
				var found := false
				for entry in app.store.interior.layout[0].cells:
					var cell := Vector2i(int(entry.cell.x), int(entry.cell.y))
					if found or (not allowed.is_empty() and not allowed.has(cell)):
						continue
					for turn in [0, 90, 180, 270]:
						app.buy_mode.hover = cell
						app.buy_mode.facing = turn
						await app.buy_mode.ask(app.buy_mode.current_verb())
						if bool(app.buy_mode.verdict.get("ok", false)):
							found = true
							break
				app.buy_mode._update_ghost()
				app.buy_mode._update_marks()
				print("buyfind: cômodo ", want[1] if want.size() > 1 else "-", " células=", allowed.size(), " cômodos no retrato=", rooms.size(), " | ", want[0], " -> ", app.buy_mode.current_verb(), " ", app.buy_mode.verdict.get("note", ""), " verde=", app.buy_mode.ghost_is_green())
			"buyonpiece": # #264: `buyonpiece:N` põe o fantasma sobre a peça N da casa (o motor deve dizer que já tem móvel ali)
				var onto: Dictionary = app.buy_mode.piece_by_index(int(arg))
				app.buy_mode.hover = Vector2i(int(onto.x), int(onto.y))
				await app.buy_mode.ask(app.buy_mode.current_verb())
				app.buy_mode._update_ghost()
				app.buy_mode._update_marks()
				print("buyonpiece: ", app.buy_mode.current_verb(), " -> ", app.buy_mode.verdict.get("reason"), " vermelho=", app.buy_mode.ghost_is_red())
			"buyask": # #264: pergunta ao motor o verbo de agora e imprime o veredito
				await app.buy_mode.ask(app.buy_mode.current_verb())
				app.buy_mode._update_ghost()
				app.buy_mode._update_marks()
				print("buyask: ", app.buy_mode.current_verb(), " -> ", app.buy_mode.verdict, " verde=", app.buy_mode.ghost_is_green(), " vermelho=", app.buy_mode.ghost_is_red())
			"buymovefind": # #264: `buymovefind:N` começa a mover a peça N e acha pelo motor outra célula e giro onde ela cabe
				var piece := int(arg)
				var from: Dictionary = app.buy_mode.piece_by_index(piece)
				app.buy_mode.start_move(piece)
				var moved := false
				for entry in app.store.interior.layout[0].cells:
					var cell := Vector2i(int(entry.cell.x), int(entry.cell.y))
					if moved or (cell == Vector2i(int(from.x), int(from.y))):
						continue
					for turn in [0, 90, 180, 270]:
						app.buy_mode.hover = cell
						app.buy_mode.facing = turn
						await app.buy_mode.ask(app.buy_mode.current_verb())
						if bool(app.buy_mode.verdict.get("ok", false)):
							moved = true
							break
				app.buy_mode._update_ghost()
				print("buymovefind: ", app.buy_mode.current_verb(), " ", app.buy_mode.verdict)
			"buyedgefind": # #264: `buyedgefind:wall|open|door|shut` acha pelo motor a primeira aresta onde a obra vale (e a marca)
				var op := arg
				app.buy_mode.use_tool(op)
				var cells := {}
				for entry in app.store.interior.layout[0].cells:
					cells[Vector2i(int(entry.cell.x), int(entry.cell.y))] = true
				var picked := false
				for cell in cells:
					for step_dir in [Vector2i(1, 0), Vector2i(0, 1)]:
						var other: Vector2i = cell + step_dir
						if picked or not cells.has(other):
							continue
						app.buy_mode.hover = cell
						app.buy_mode.hover_edge = [cell.x, cell.y, other.x, other.y]
						await app.buy_mode.ask(app.buy_mode.current_verb())
						if bool(app.buy_mode.verdict.get("ok", false)):
							picked = true
				app.buy_mode._update_marks()
				print("buyedgefind: ", app.buy_mode.current_verb(), " ", app.buy_mode.verdict)
			"buycut": # #264: `buycut:sala` divide o cômodo por uma linha reta de paredes (a primeira com porta, para ninguém ficar preso); cada trecho é um comando ao motor
				var rooms3: Array = app.store.interior.get("rooms", [])
				var room_cells := {}
				for room in rooms3[0].get("rooms", []) if rooms3.size() > 0 else []:
					if String(room.get("kind", "")) == arg and room_cells.is_empty():
						for room_cell in room.cells:
							room_cells[Vector2i(int(room_cell.x), int(room_cell.y))] = true
				var line: Array = []
				var xs: Array = room_cells.keys().map(func(c): return c.x)
				var ys: Array = room_cells.keys().map(func(c): return c.y)
				var cuts: Array = range(xs.min(), xs.max())
				cuts.sort_custom(func(a, b): return absf(a - (xs.min() + xs.max() - 1) / 2.0) < absf(b - (xs.min() + xs.max() - 1) / 2.0)) # do meio para as pontas
				for cut_x in cuts:
					var edges: Array = []
					for y in range(ys.min(), ys.max() + 1):
						if room_cells.has(Vector2i(cut_x, y)) and room_cells.has(Vector2i(cut_x + 1, y)):
							edges.append([cut_x, y, cut_x + 1, y])
					if line.is_empty() and edges.size() == ys.max() - ys.min() + 1:
						line = edges
				app.buy_mode.use_tool("wall")
				var made := 0
				for index in line.size():
					var e: Array = line[index]
					var wall_verb := "wall:0:%d:%d:%d:%d" % [e[0], e[1], e[2], e[3]]
					await app.buy_mode.ask(wall_verb)
					var ok_wall: bool = await app.buy_mode.command(wall_verb)
					print("buycut: ", wall_verb, " ", app.buy_mode.verdict.get("reason", "ok") if not ok_wall else "ok")
					made += 1 if ok_wall else 0
					if index == 0 and ok_wall:
						var door_verb := "door:0:%d:%d:%d:%d" % [e[0], e[1], e[2], e[3]]
						await app.buy_mode.ask(door_verb)
						var ok_door: bool = await app.buy_mode.command(door_verb)
						print("buycut: ", door_verb, " ", app.buy_mode.verdict.get("reason", "ok") if not ok_door else "ok")
				print("buycut: ", arg, " linha=", line, " trechos=", made, " dinheiro=", app.buy_mode.home.get("wallet"), " cômodos=", app.buy_mode.home.get("rooms", []).map(func(r): return "%s:%s" % [r.kind, r.cells]))
			"buyextendfind": # #264: acha pelo motor a primeira célula onde a casa pode ampliar e a marca
				app.buy_mode.use_tool("extend")
				var known := {}
				for entry in app.store.interior.layout[0].cells:
					known[Vector2i(int(entry.cell.x), int(entry.cell.y))] = true
				var chosen := false
				for cell in known:
					for dir in [Vector2i(-1, 0), Vector2i(1, 0), Vector2i(0, -1), Vector2i(0, 1)]:
						var out: Vector2i = cell + dir
						if chosen or known.has(out):
							continue
						app.buy_mode.hover = out
						await app.buy_mode.ask(app.buy_mode.current_verb())
						if bool(app.buy_mode.verdict.get("ok", false)):
							chosen = true
				app.buy_mode._update_marks()
				print("buyextendfind: ", app.buy_mode.current_verb(), " ", app.buy_mode.verdict)
			"buyroomcell": # #264: `buyroomcell:sala` põe o mouse numa célula do cômodo (para pintar)
				var rooms2: Array = app.store.interior.get("rooms", [])
				for room in rooms2[0].get("rooms", []) if rooms2.size() > 0 else []:
					if String(room.get("kind", "")) == arg:
						app.buy_mode.hover = Vector2i(int(room.cells[0].x), int(room.cells[0].y))
						break
				await app.buy_mode.ask(app.buy_mode.current_verb())
				app.buy_mode._update_marks()
				print("buyroomcell: ", app.buy_mode.current_verb(), " ", app.buy_mode.verdict)
			"buyclick": # #264: o clique do mouse (manda o verbo se o motor disse que pode)
				var before_money := int(app.buy_mode.home.get("wallet", 0))
				await app.buy_mode.click()
				print("buyclick: dinheiro ", before_money, " -> ", app.buy_mode.home.get("wallet"), " | ", app.buy_mode.last_note, " | peças=", app.buy_mode.home.get("pieces", []).size())
			"buycmd": # #264: manda um verbo de casa direto (`buycmd:sell:3`)
				var full_verb: String = step.substr(name.length() + 1)
				var sent: bool = await app.buy_mode.command(full_verb)
				print("buycmd: ", full_verb, " aceito=", sent, " dinheiro=", app.buy_mode.home.get("wallet"))
			"buyhome": # #264: o que o motor diz da casa: dinheiro, peças, cômodos com a beleza e as encomendas
				await app.buy_mode.reload()
				var home: Dictionary = app.buy_mode.home
				print("buyhome: dinheiro=", home.get("wallet"), " loja=", home.get("shop"), " peças=", home.get("pieces", []).map(func(p): return "%d:%s@%d,%d/%d" % [int(p.piece), p.id, int(p.x), int(p.y), int(p.facing)]),
					" cômodos=", home.get("rooms", []).map(func(r): return "%s %s cél beleza %s" % [r.kind, r.cells, r.beauty]), " encomendas=", home.get("orders", []), " estoque=", home.get("items", []).map(func(i): return "%s:%d" % [i.object, int(i.stock)]))
			"partner": # #264: o que as outras pessoas da casa fazem agora (o uso do catálogo e a peça): o parceiro usando o móvel novo
				var doing: Array = []
				for occupant in app.store.interior.get("occupants", []):
					var who := int(occupant.npc.value) if occupant.npc is Dictionary else int(occupant.npc)
					doing.append("%s:%s/%s@%d,%d" % [app.store.names.get(who, who), occupant.get("usingObject"), occupant.get("usingUse"), int(occupant.localCell.x), int(occupant.localCell.y)])
				print("partner: ", doing, " peças do motor=", app.store.interior.objects.furniture.size())
			"mood": # #262: imprime o humor do corpo e da outra pessoa como o motor diz (emoção, nota, momentos com causa e tempo, aspiração, desejos) e o que o cartão mostra
				await app.player_hud.refresh_body_now()
				await _print_mood(app)
			"dowish": # #262: cumpre o primeiro desejo social do motor pelo menu "Interagir ▸" ("Abraçar Rafael" -> Abraçar); a pessoa precisa estar ao alcance (`walkperson` antes)
				var wish_sheet: Dictionary = (await app.player_hud.api.npc_detail(app.player_hud.npc_id)).body
				var wished: Array = wish_sheet.get("mood", {}).get("wishes", []) if wish_sheet.get("mood") is Dictionary else []
				var verbs := {"Abraçar": "Abraçar", "Elogiar": "Elogiar", "Beijar": "Beijar", "Fazer": "piada", "Conversar": "papo"}
				var done_wish := false
				for wish in wished:
					var first_word := String(wish.text).get_slice(" ", 0)
					if int(wish.other) < 0 or not verbs.has(first_word):
						continue
					var person_name := ""
					for housemate in app.player_hud.housemates:
						if int(housemate.id) == int(wish.other):
							person_name = String(housemate.name)
					var line := await _social_line(app, person_name, String(verbs[first_word]))
					print("dowish: ", wish.text, " -> ", line.get("text", "(nada)"))
					if not line.is_empty():
						await app.player_hud.interact(line)
						done_wish = true
					break
				if not done_wish:
					print("dowish: nenhum desejo social ao alcance: ", wished.map(func(w): return w.text))
			"needslog": # #255: `needslog:S` imprime as barras a cada 3 s reais por S s (o log do dia)
				var logged := 0.0
				while logged < float(arg):
					await _print_needs(app)
					await app.get_tree().create_timer(3.0).timeout
					logged += 3.0
			"bothlog": # #255: `bothlog:S` imprime as barras do corpo e da outra pessoa da casa a cada 3 s reais por S s (o log do dia dos dois)
				var both := 0.0
				while both < float(arg):
					await _print_needs(app)
					for housemate in app.player_hud.housemates:
						app.player_hud.api.invalidate_npc(int(housemate.id))
						var mate2: Dictionary = (await app.player_hud.api.npc_detail(int(housemate.id))).body
						print("theother: [", app.player_frame.clock_label.text, "] ", mate2.name, " ação=", mate2.get("currentAction"), " ", _need_line(mate2))
					await app.get_tree().create_timer(3.0).timeout
					both += 3.0
			"sleepfast": # #255: dormir acelerando (só quem joga sozinho): o motor corre o tempo até descansar
				await app.player_hud.sleep(-1)
				app.player_hud.api.invalidate_npc(app.player_hud.npc_id)
				var slept: Dictionary = (await app.player_hud.api.npc_detail(app.player_hud.npc_id)).body
				print("sleepfast: ação=", slept.get("currentAction"), " (1 = dormir) sono=", slept.get("sleep"), " tempo=", app.player_hud.time_view)
			"theotherlog": # #255: `theotherlog:S` imprime as barras da outra pessoa da casa (a que ninguém conduz) a cada 6 s reais por S s
				var seen := 0.0
				while seen < float(arg):
					for housemate in app.player_hud.housemates:
						app.player_hud.api.invalidate_npc(int(housemate.id))
						var mate: Dictionary = (await app.player_hud.api.npc_detail(int(housemate.id))).body
						print("theother: [", app.player_frame.clock_label.text, "] ", mate.name, " ação=", mate.get("currentAction"), " ", _need_line(mate))
					await app.get_tree().create_timer(6.0).timeout
					seen += 6.0
			"interrupt": # #255: Espaço: para o que o corpo faz (o que já subiu fica)
				await app.player_hud.interrupt()
			"theother": # #255: o NPC que ninguém conduz: imprime as barras dele pela ficha do motor
				for housemate in app.player_hud.housemates:
					app.player_hud.api.invalidate_npc(int(housemate.id))
					var other: Dictionary = (await app.player_hud.api.npc_detail(int(housemate.id))).body
					print("theother: [", app.player_frame.clock_label.text, "] ", other.name, " ação=", other.get("currentAction"), " em=", other.get("currentLocation"), " ", _need_line(other), " | dica: ", PlayerFrame.hint_line(other.get("needHint")))
			"waituse": # #255: espera (até S s reais) o uso acabar; imprime as barras uma vez por segundo
				var waited := 0.0
				while app.player_hud.busy.active() and waited < float(arg):
					await app.get_tree().create_timer(1.0).timeout
					waited += 1.0
				print("waituse: ", waited, " s; ocupado=", app.player_hud.busy.active())
			"cancelq": # #255: tira a ação N da fila (0 = a próxima)
				await app.player_hud.cancel_queued(int(arg))
			"where": # #252: onde está o corpo conduzido (id, nome, posição fina, prédio e andar do motor, câmera)
				print("where: chão=", app.player_frame._ground, " npc=", app.player_hud.npc_id, " ", app.player_hud.npc_name, " fine=", app.player_hud.fine, " prédio=", app.player_hud.building_id,
					" andar=", app.player_hud.floor_level, " câmera=", app.rig.target, " seguindo=", app.rig.following)
			"speedinfo": # #260: a velocidade do tempo como o HUD e o motor a mostram (botões, relógio, ritmo)
				var st: Dictionary = (await app.api.clock_status()).body
				print("speedinfo: botões=", app.player_frame.speed_box.visible, " marcado=", app.player_frame.speed_now, " hud=", app.player_hud.speed_now(), " motor=", st.get("playerSpeed"),
					" Δ=", st.get("tickDeltaMs"), " modo=", st.get("rhythm"), " relógio=", app.player_frame.clock_label.text)
			"clockrate": # #261: `clockrate:S` mede por S s reais quanto o relógio do MOTOR andou (segundos de mundo por segundo real) e imprime com a velocidade e o relógio da tela
				var rate_a: Dictionary = (await app.api.clock_status()).body
				var rate_t0 := Time.get_ticks_msec()
				await app.get_tree().create_timer(float(arg if arg != "" else "3")).timeout
				var rate_b: Dictionary = (await app.api.clock_status()).body
				var real_s := (Time.get_ticks_msec() - rate_t0) / 1000.0
				print("clockrate: velocidade=", rate_b.get("playerSpeed"), " Δ=", rate_b.get("tickDeltaMs"), " ticks/s do laço=", rate_b.get("loopTicksPerSecond"), " mundo andou ",
					snappedf((float(rate_b.atMs) - float(rate_a.atMs)) / 1000.0, 0.1), " s em ", snappedf(real_s, 0.1), " s reais = ", snappedf((float(rate_b.atMs) - float(rate_a.atMs)) / 1000.0 / real_s, 0.1),
					"× | relógio da tela=", app.player_frame.clock_label.text)
			"lamps": # #260: as luzes dos objetos da casa (quantas, em que nível) e a luz do dia que o cliente usa
				var view = app.stage.interior_view
				print("lamps: casa=", app.stage.active, " luzes=", view.lamp_count(), " ligadas pelo motor=", view.lit_count(), " nível=", view._lamp_level, " luz do dia=", app.day_cycle.daylight(), " hora=", app.day_cycle.hour, " | luzes do motor (delta)=", app.store.interior.get("lights"),
					" | peças das luzes=", view._lamps.map(func(l): return [l.get_meta("piece", -1), l.get_meta("switch", false), l.visible]))
			"walls": # #260: troca o modo das paredes (a tecla V)
				app.cycle_walls()
			"hudclock": # #256: `hudclock:N` imprime a cada 1 s, por N s, o relógio que a TELA mostra (texto e horas do mundo) e o ritmo que o cliente usa
				var clock_start := Time.get_ticks_msec()
				var clock_first := -1.0
				for _tick in int(float(arg if arg != "" else "1")) + 1:
					var hours: float = app.day_cycle._tick
					if clock_first < 0.0:
						clock_first = hours
					print("hudclock: t=%.2f s relogio=%s mundo=%.5f h (+%.1f s de mundo) ritmo=%.1f s/s" % [(Time.get_ticks_msec() - clock_start) / 1000.0,
						app.day_cycle.clock_text(), hours, (hours - clock_first) * 3600.0, app.day_cycle._ticks_per_second * 3600.0])
					await app.get_tree().create_timer(1.0).timeout
			"gait": # #256: `gait:W=5` anda 5 s e mede o que está desenhado: m/s do corpo, passos/s e quanto o pé de apoio anda no mundo (deslize)
				var gkey := OS.find_keycode_from_string(arg.get_slice("=", 0))
				var gpress := InputEventKey.new()
				gpress.keycode = gkey
				gpress.physical_keycode = gkey
				gpress.pressed = true
				Input.parse_input_event(gpress)
				var secs := float(arg.get_slice("=", 1)) if "=" in arg else 5.0
				var samples: Array = []
				var elapsed := 0.0
				while elapsed < secs:
					await app.get_tree().process_frame
					var dt := app.get_process_delta_time()
					elapsed += dt
					var actor = app.player_actor()
					if actor == null or actor._rig == null or elapsed < 1.5: # a 1ª parte é a arrancada
						continue
					var ankle: Vector3 = actor.transform * (actor._legs[0].transform * actor._rig._knees[0].transform * actor._rig._ankles[0].transform).origin
					samples.append({"t": elapsed, "pos": actor.position, "phase": actor._rig.phase, "ankle": ankle, "stance": fposmod(actor._rig.phase / TAU, 1.0) < actor._rig._duty})
				var grelease: InputEventKey = gpress.duplicate()
				grelease.pressed = false
				Input.parse_input_event(grelease)
				if samples.size() > 2:
					var first: Dictionary = samples[0]
					var last: Dictionary = samples[samples.size() - 1]
					var span: float = last.t - first.t
					var cell_m: float = load("res://Scenes/grid.gd").cell_meters
					var mps: float = (last.pos - first.pos).length() * cell_m / span
					var cycles: float = (last.phase - first.phase) / TAU / span
					var drift := 0.0
					var stance_travel := 0.0
					var path_len := 0.0
					var worst_step := 0.0
					var rig_speed := 0.0
					for i in range(1, samples.size()):
						var d: float = (samples[i].pos - samples[i - 1].pos).length()
						path_len += d
						worst_step = maxf(worst_step, d)
					print("gait: caminho percorrido %.2f m x deslocamento liquido %.2f m (puxoes aumentam o primeiro), maior passo por quadro %.3f m em %d quadros" % [
						path_len * cell_m, (last.pos - first.pos).length() * cell_m, worst_step * cell_m, samples.size()])
					for i in range(1, samples.size()):
						if samples[i].stance and samples[i - 1].stance:
							drift += (samples[i].ankle - samples[i - 1].ankle).length()
							stance_travel += (samples[i].pos - samples[i - 1].pos).length()
					print("gait: %.2f m/s | %.2f passos/s | pé de apoio andou %.3f unidades para %.3f do corpo no apoio (deslize %.0f%%) | amostras=%d" % [mps, cycles * 2.0, drift, stance_travel,
						100.0 * drift / maxf(stance_travel, 0.0001), samples.size()])
				else:
					print("gait: sem amostras (ator=", app.player_actor() != null, ")")
			"act":
				await app.player_hud.act(arg)
			"creator": # AD-197q: abre o criador de personagem (cidades e primeira amostra do motor)
				await app.open_creator()
			"creatorset": # AD-197t: `creatorset:name=Ana,age=29,role=child,family=0,money=90,height=1.65,look.skinTone=30,outfit=3` preenche o criador
				await _creator_set(app, arg)
			"creatorstep": # #222: `creatorstep:N` vai à etapa N do criador (1 Origem … 5 Revisar), com a prévia do motor
				await app.creator.designer.go_to(int(arg) - 1)
			"creatorrandom": # #222: sorteia a aparência no cliente (o motor valida)
				app.creator.designer._randomize_look(app.creator.designer._all_look_keys())
				await app.creator.designer.refresh()
			"creatorcreate": # AD-197t: cria a pessoa montada e entra
				await app.creator.designer.refresh()
				await app.creator.designer.create()
			"creatorpick": # AD-197q: `creatorpick:N` entra como o N-ésimo candidato que a vontade deixa (0 = o primeiro)
				if app.creator.mode != "sample":
					await app.creator.set_mode("sample")
				var open_cards: Array = app.creator.cards.filter(func(c): return not c.button.disabled)
				if int(arg) < open_cards.size():
					var card: Dictionary = open_cards[int(arg)]
					var who: String = ""
					for c in app.creator.sample.get("candidates", []):
						if int(c.npcId) == int(card.npc_id):
							who = String(c.name)
					await app.creator.choose(int(card.npc_id), who)
			"admin":
				app.admin_window.open()
				app.admin_window.reason_input.text = arg
				await app.admin_window.pause()
			"adminsearch":
				app.admin_window.open()
				app.admin_window.tabs.current_tab = 1
				app.admin_window.search_input.text = arg
				await app.admin_window.search()
			"treasury":
				var cities: Array = app.store.cities.keys()
				if cities.size() > 0:
					await app.enter_scope("City", cities[0])
					await app.economy.show_treasury(String(cities[0]), "Cidade")
			"trades":
				await app.open_trades(int(arg))
			"tech":
				await app.technology.show_culture(int(arg))
			"size":
				var wh: PackedStringArray = arg.split("x")
				DisplayServer.window_set_size(Vector2i(int(wh[0]), int(wh[1])))
				await app.get_tree().create_timer(0.5).timeout
			"demo":
				_demo(app, arg)
			"materialize":
				await app.npc_panel.materialize()
			"talk":
				await app.select_npc(int(arg))
				await app.talk_to(int(arg))
			"timesay":
				var started := Time.get_ticks_msec()
				app.conversation.send(arg)
				while app.conversation.waiting and Time.get_ticks_msec() - started < 30000:
					await app.get_tree().process_frame
				print("timesay: esperou ", Time.get_ticks_msec() - started, " ms; avisos=", app.toasts.history, " linhas=", app.conversation.lines.size())
			"gametalk": # #237: a conversa do modo jogador (caixa de jogo com escolhas) com o NPC `arg`, sem depender de quem está ao lado
				app._on_player_menu(true)
				app.conversation.game_style = app.player_mode
				await app.conversation.start(int(arg), app.registry.person_name(int(arg)))
			"toasts":
				print("toasts: ", app.toasts.history)
			"pick":
				await app.conversation.pick(int(arg) - 1)
			"say":
				await app.conversation.send(arg)
			"follow":
				app.follow_selected()
			"pause":
				await app.pause_menu.open()
			"tips":
				app.first_steps.start(true)
				for _i in int(arg):
					app.first_steps.next_tip()
			"help":
				app.first_steps.open_help()
				if arg == "actions": # #238: a página "O que dá para fazer" da ajuda do jogador
					app.first_steps.toggle_actions()
			"guide": # #238: o guia da primeira posse (`guide:N` vai ao passo N, de 1; `guide:close` fecha)
				if arg == "close":
					app.player_hud.frame.guide.close()
					continue
				await app.player_hud.show_guide()
				var guide = app.player_hud.frame.guide
				for _i in maxi(int(arg) - 1, 0):
					guide.go(1)
				print("guide: ", guide.steps.size(), " passos; passo ", guide.index + 1, ": ", guide.steps[guide.index].text if guide.visible else "(fechado)")
			"xray": # AD-177: raio-x, planta, ficha da rua mais usada e do lote com quintal, contagem do chão
				app.reading.toggle_xray()
			"focuscell": # #243: só captura: `focuscell:x,y` põe a câmera na célula (sem corpo), para fotografar a árvore cortada e o toco
				var cell_xy := arg.split(",")
				app.focus_point(Vector3((float(cell_xy[0]) + 0.5) * Grid.CELL, 0.0, (float(cell_xy[1]) + 0.5) * Grid.CELL))
			"focuswork": # AD-193: câmera e ficha na primeira obra do escopo (captura da fumaça)
				for work_id in app.store.works:
					app.focus_point(app.point_of({"kind": "work", "id": int(work_id)}))
					await app.inspect({"kind": "work", "id": int(work_id)})
					break
			"plan":
				app.reading.toggle_plan()
			"street":
				var best := Vector2i(-1, -1)
				for cell in app.world_view.ground.drawn_roads:
					if best == Vector2i(-1, -1) or int(app.store.roads[cell].users) > int(app.store.roads[best].users):
						best = cell
				if best != Vector2i(-1, -1):
					await app.inspect(Registry.ref(Registry.STREET, "%d,%d" % [best.x, best.y]))
					app.rig.focus(Grid.to_world(best))
			"lot":
				for id in app.store.buildings:
					if not app.store.buildings[id].get("lot", {}).get("croft", []).is_empty():
						await app.inspect(Registry.ref(Registry.BUILDING, int(id)))
						app.rig.focus(app.world_view.building_center(int(id)))
						break
			"ground":
				var crofts: int = app.store.buildings.values().filter(func(b): return not b.get("lot", {}).get("croft", []).is_empty()).size()
				print("ground: scope=%s roads=%d drawn=%d buildings=%d with_croft=%d road_pieces=%d lot_pieces=%d syncs=%d" % [app.store.kind,
					app.store.roads.size(), app.world_view.ground.drawn_roads.size(), app.store.buildings.size(), crofts,
					app.world_view.ground.road_pieces, app.world_view.ground.lot_pieces, app.road_sync.refreshes])
			"roads": # AD-181: rua por delta, obras e quem anda pela rota agora
				var surfaces := {}
				for cell in app.store.roads:
					surfaces[app.store.roads[cell].surface] = surfaces.get(app.store.roads[cell].surface, 0) + 1
				var walking: int = app.world_view.npc_nodes.values().filter(func(a): return a.walk != null).size()
				print("roads: scope=%s live=%s cells=%d drawn=%d reserved=%d surfaces=%s works=%d walking=%d routes=%d syncs=%d" % [app.store.kind,
					app.store.roads_live, app.store.roads.size(), app.world_view.ground.drawn_roads.size(), app.world_view.ground.reserved.size(),
					surfaces, preload("res://Store/road_book.gd").work_cells(app.store.roads).size(), walking,
					app.store.npcs.values().filter(func(n): return not n.get("route", []).is_empty()).size(), app.road_sync.refreshes])
			"walker": # segue alguém andando pela rota (rota no chão)
				for id in app.world_view.npc_nodes:
					if app.world_view.npc_nodes[id].walk != null and not app.world_view.npc_nodes[id].walk.done:
						await app.select_npc(int(id))
						app._highlight(int(id))
						app.follow_selected()
						break
			"heat":
				app.reading.toggle_heat()
			"log":
				var moving := 0
				var actions := {}
				for id in app.store.npcs:
					var a: int = app.store.npcs[id].action
					actions[a] = actions.get(a, 0) + 1
				print("log: scope=", app.store.kind, " cursor=", app.stream.cursor, " state=", app.stream.state, " npcs=", app.store.npcs.size(), " actions=", actions)
				# AD-193: obras que o cliente recebeu e quantas desenhou como canteiro (e quem está no lote com a obra como alvo)
				var sites := 0
				for node in app.world_view.buildings.nodes.values():
					if node.has_meta("work_id"):
						sites += 1
				var on_site := 0
				for id in app.store.npcs:
					if String(app.store.npcs[id].get("target", {}).get("kind", "")) == "work":
						on_site += 1
				var works_text: Array = []
				for work in app.store.works.values():
					var mat := ""
					for entry in work.materials: # AD-194: material no lote / posto / total
						mat += ":mat=%d/%d/%d" % [int(entry.get("on_site", -1)), int(entry.placed), int(entry.needed)]
					works_text.append("%d:%s:%d%%:%s:turma=%d+%d:lote=%d%s" % [int(work.id), String(work.kind), int(round(float(work.progress) * 100.0)),
						String(work.halt) if String(work.halt) != "" else "anda", work.crew.size(), work.helpers.size(), work.on_site.size(), mat])
				var arrivals := 0
				for event in app.registry.events.values():
					if String(event.get("label", "")).begins_with("Chegou ao lote"):
						arrivals += 1
				var carrying := 0 # AD-195: quem leva material nas mãos (observável `carried`)
				for id in app.store.npcs:
					for load in app.store.npcs[id].get("obs", {}).get("carried", []):
						if int(load.get("resource_id", load.get("resourceId", -1))) == 3:
							carrying += 1
				print("log-works: carregando=", carrying, " cargas_no_registro=", arrivals, " works=", app.store.works.size(), " sites=", sites, " npcs_on_site=", on_site, " ", " ".join(works_text))
			"walls":
				var view = app.stage.interior_view
				var heights := {}
				for wall in view._walls:
					heights[snappedf(wall.mesh.size.y, 0.01)] = heights.get(snappedf(wall.mesh.size.y, 0.01), 0) + 1
				print("walls: n=", view._walls.size(), " heights=", heights, " dir=", view._view_dir, " center=", view._center)
			"click", "dblclick", "hover":
				var xy: PackedStringArray = arg.split(",")
				var position := Vector2(float(xy[0]), float(xy[1]))
				var move := InputEventMouseMotion.new()
				move.position = position
				move.global_position = position
				Input.parse_input_event(move)
				await app.get_tree().process_frame
				if name != "hover":
					var press := InputEventMouseButton.new()
					press.position = position
					press.global_position = position
					press.button_index = MOUSE_BUTTON_LEFT
					press.pressed = true
					press.double_click = name == "dblclick"
					Input.parse_input_event(press)
					await app.get_tree().process_frame
				await app.get_tree().create_timer(0.3).timeout
				print("hover: ", app.hover_text.replace("\n", " | "))
			"stress", "stress_look":
				if app.banner != null:
					app.banner.text = "MEDIDA DE DESEMPENHO: pessoas de teste. Isto não é o mundo do jogo."
					app.banner.visible = true
				# Carga sintética: N NPCs andando pelo mapa para medir quadros por segundo. `stress_look` dá a cada um
				# genes de aparência (como um mundo com o bloco Appearance); `stress` mantém o sorteio local de sempre.
				var count := int(arg)
				var rng := RandomNumberGenerator.new()
				rng.seed = 7
				for i in count:
					var cell := Vector2i(rng.randi_range(0, 29), rng.randi_range(0, 29))
					var look := {}
					if name == "stress_look":
						look = {"skin_tone": rng.randi_range(0, 100), "hair_tone": rng.randi_range(0, 100),
							"hair_curl": rng.randi_range(0, 100), "build": rng.randi_range(0, 100)}
					app.store.npcs[100000 + i] = {"id": 100000 + i, "cell": cell, "action": rng.randi_range(0, 7), "city": "", "look": look}
				app.world_view._rebuild_npcs()
				var frames := 0
				var elapsed := 0.0
				while elapsed < 4.0:
					await app.get_tree().process_frame
					elapsed += app.get_process_delta_time()
					frames += 1
					if frames % 30 == 0:
						for i in count:
							var id := 100000 + i
							var from_cell: Vector2i = app.store.npcs[id].cell
							var to_cell := Vector2i(clampi(from_cell.x + rng.randi_range(-1, 1), 0, 29), clampi(from_cell.y + rng.randi_range(-1, 1), 0, 29))
							app.store.npcs[id].cell = to_cell
							app.store.npc_changed.emit(id, from_cell, to_cell, app.store.npcs[id].action)
				print("stress: npcs=", count, " fps=", snappedf(frames / elapsed, 0.1), " (monitor ", Performance.get_monitor(Performance.TIME_FPS), ")")
			"fps": # #266: `fps:S` mede os quadros por segundo da cena de agora durante S segundos (a casa do sandbox com a mobília inteira); imprime a média e os nós
				var fps_frames := 0
				var fps_elapsed := 0.0
				var fps_worst := 0.0
				while fps_elapsed < float(arg if arg != "" else "4"):
					await app.get_tree().process_frame
					var fps_dt: float = app.get_process_delta_time()
					fps_elapsed += fps_dt
					fps_worst = maxf(fps_worst, fps_dt)
					fps_frames += 1
				print("fps: ", snappedf(fps_frames / fps_elapsed, 0.1), " pior_quadro_ms=", snappedf(fps_worst * 1000.0, 0.1), " nos=", Performance.get_monitor(Performance.OBJECT_NODE_COUNT), " objetos_desenhados=", Performance.get_monitor(Performance.RENDER_TOTAL_OBJECTS_IN_FRAME))
			"bench", "benchbig":
				# quem olha a janela durante a medida precisa saber que isto não é o mundo: é carga sintética local
				if app.banner != null:
					app.banner.text = "MEDIDA DE DESEMPENHO: pessoas e prédios de teste, colocados em grade. Isto não é o mundo do jogo."
					app.banner.visible = true
				await _bench(app, int(arg) if arg != "" else 500, name == "benchbig")
			"playstreet": # só captura (AD-197o): encarna um morador que está na rua (o mesmo do passo "near")
				var id := _street_npc(app)
				if id >= 0:
					await app.select_npc(id)
					await app.incarnate(id)
			"playopen": # só captura (AD-197s2): encarna o morador mais longe de qualquer prédio (rua aberta de verdade)
				var open_id := _open_npc(app)
				if open_id >= 0:
					await app.select_npc(open_id)
					await app.incarnate(open_id)
			"playwall": # só captura (AD-197s4): encarna um morador da rua e o põe a 1 m da quina de um prédio, com a câmera virada para a quina atrás
				var wall_id := _street_npc(app)
				if wall_id >= 0:
					await app.select_npc(wall_id)
					await app.incarnate(wall_id)
					var wall_body: Node3D = app.world_view.npc_nodes.get(wall_id)
					if wall_body != null:
						var spot := _corner_spot(app, wall_body.position)
						if spot.size() == 2:
							wall_body.position = spot[0]
							app.rig.yaw = spot[1]
							app.rig.follow(wall_body)
			"playhome": # só captura (AD-197s): encarna um morador e o põe no meio do prédio mais próximo (só o desenho; o motor não muda)
				var home_id := _street_npc(app)
				if home_id >= 0:
					await app.select_npc(home_id)
					await app.incarnate(home_id)
					var body: Node3D = app.world_view.npc_nodes.get(home_id)
					if body != null:
						var inside := _nearest_box_center(app, body.position, arg == "big")
						if inside != Vector3.INF:
							body.position = inside
							app.rig.follow(body)
			"crowd": # só captura (AD-197r): câmera perto do ponto com mais gente a menos de 0,6 unidade umas das outras
				var best_id := -1
				var best_count := 0
				for id in app.world_view.npc_nodes:
					var at: Vector3 = app.world_view.npc_nodes[id].position
					var count := 0
					for other in app.world_view.npc_nodes.values():
						if Vector2(at.x, at.z).distance_to(Vector2(other.position.x, other.position.z)) < 0.6:
							count += 1
					if count > best_count:
						best_count = count
						best_id = id
				if best_id >= 0:
					print("crowd: ", best_count, " pessoas perto de ", app.world_view.npc_nodes[best_id].position)
					app.rig.focus(app.world_view.npc_nodes[best_id].position)
					app.rig.set_zoom(float(arg) if arg != "" else 4.0)
			"camdump": # só diagnóstico (AD-197s): onde estão corpo e câmera, e em que caixas de prédio cada um está
				print(camera_report(app))
			"stacks": # só medida (AD-197r): quantas pessoas ficam a menos de 0,25 unidade de outra e o maior bolo (com onde e o que fazem)
				print(stacks_report(app.world_view.npc_nodes))
			"cam": # só captura (AD-197o): troca o modo da câmera (`cam:first|third|iso`) sem trocar quem ela segue
				app.rig.set_mode({"first": app.rig.FIRST, "third": app.rig.THIRD}.get(arg, app.rig.ISO))
			"third", "first": # só medida (AD-197d): câmera do modo jogador (3ª/1ª pessoa) colada num morador, sem encarnar
				var ids: Array = app.world_view.npc_nodes.keys()
				ids.sort()
				if not ids.is_empty():
					app.rig.follow(app.world_view.npc_nodes[ids[0]])
				app.rig.set_mode(app.rig.THIRD if name == "third" else app.rig.FIRST)
			"unsub": # só medida (AD-188): para de receber deltas (separa o custo do cliente do da máquina dividida com o servidor)
				app.stream.close()
			"rain": # só medida/captura (AD-188): tempo sintético local (`rain:heavy|moderate|light`, `rain:snow`); o motor não muda
				var snow := arg == "snow"
				var fake := {"precipitation": "snow" if snow else "rain", "intensity": "heavy" if snow else (arg if arg != "" else "heavy"),
					"storm": not snow, "wind": 9.0, "ground": "snow" if snow else "mud", "snow_cm": 12.0 if snow else 0.0, "now_c": -3.0 if snow else 9.0,
					"feels_c": -7.0 if snow else 6.0, "sunrise": 7.0, "sunset": 17.0}
				app.store.climate = app.store.climate.duplicate()
				app.store.climate.weather = fake
				for cell in app.store.roads:
					app.store.roads[cell].ground = fake.ground if int(app.store.roads[cell].surface) < 2 else ("snow" if snow else "wet")
					app.store.roads[cell].snow_cm = fake.snow_cm
				app.store.climate_changed.emit()
				app.world_view._mark_ground()
			"near": # câmera bem perto de um morador na rua (LOD "perto": ferramenta, objeto e rosto)
				var id := _street_npc(app)
				if id >= 0:
					app.rig.focus(app.world_view.npc_nodes[id].position)
					app.rig.set_zoom(float(arg) if arg != "" else 5.0)
			"selectfirst": # ficha de um morador na rua (o mesmo do passo "near")
				var id := _street_npc(app)
				if id >= 0:
					await app.select_npc(id)
			"building": # ficha do primeiro prédio
				var ids: Array = app.store.buildings.keys()
				ids.sort()
				if not ids.is_empty():
					await app.inspect({"kind": "building", "id": int(ids[0])})
			"next":
				app.next_event()
			"watch":
				app.toggle_watch()
			"hold": # #222: `hold:W=2` segura a tecla (W, A, S, D, SHIFT, SPACE, E) por N s, como o teclado de verdade
				var key_name := arg.get_slice("=", 0)
				var code := OS.find_keycode_from_string(key_name)
				var press := InputEventKey.new()
				press.keycode = code
				press.physical_keycode = code
				press.pressed = true
				Input.parse_input_event(press)
				await app.get_tree().create_timer(float(arg.get_slice("=", 1)) if "=" in arg else 0.1).timeout
				var release: InputEventKey = press.duplicate()
				release.pressed = false
				Input.parse_input_event(release)
			"walkshot": # #238: `walkshot:W=2,caminho.png` anda 2 s e fotografa o cartão com o corpo em movimento (e imprime o rótulo do chão)
				var spec := arg.get_slice(",", 0)
				var walk_key := OS.find_keycode_from_string(spec.get_slice("=", 0))
				var walk_press := InputEventKey.new()
				walk_press.keycode = walk_key
				walk_press.physical_keycode = walk_key
				walk_press.pressed = true
				Input.parse_input_event(walk_press)
				await app.get_tree().create_timer(float(spec.get_slice("=", 1)) * 0.6).timeout
				print("ground: ", app.player_frame.action_label.text, " | ", app.player_frame._ground)
				await RenderingServer.frame_post_draw
				app.get_viewport().get_texture().get_image().save_png(arg.get_slice(",", 1))
				await app.get_tree().create_timer(float(spec.get_slice("=", 1)) * 0.4).timeout
				var walk_release: InputEventKey = walk_press.duplicate()
				walk_release.pressed = false
				Input.parse_input_event(walk_release)
			"walkraw": # #237: um passo cru ao motor (`walkraw:-707,-707`) e a resposta inteira
				var raw: Dictionary = await app.api.player_walk(app.player_hud.player_id, int(arg.get_slice(",", 0)), int(arg.get_slice(",", 1)), false)
				print("walkraw: status=", raw.get("status"), " body=", raw.get("body"))
			"resume": # #237: retoma o mundo (admin) para medir com o tempo correndo, como o dono joga
				var resumed: Dictionary = await app.api.admin_resume("dev")
				print("resume: ok=", resumed.get("ok"), " status=", resumed.get("status"))
			"walkwatch": # #237: `walkwatch:W=4` anda 4 s e, a cada 0,25 s, imprime o corpo, a dica na tela e o escopo (prova de oferta velha / troca de escopo)
				var wspec := arg.get_slice(",", 0)
				if not app.player_hud.warning.is_connected(_print_warning):
					app.player_hud.warning.connect(_print_warning)
				var wkey := OS.find_keycode_from_string(wspec.get_slice("=", 0))
				var wpress := InputEventKey.new()
				wpress.keycode = wkey
				wpress.physical_keycode = wkey
				wpress.pressed = true
				Input.parse_input_event(wpress)
				var left := float(wspec.get_slice("=", 1))
				while left > 0.0:
					await app.get_tree().create_timer(0.25).timeout
					left -= 0.25
					var hud2 = app.player_hud
					var shown: String = app.player_frame.prompt.text if app.player_frame != null and app.player_frame.prompt.visible else ""
					var live_water := ""
					var live: Dictionary = await app.api.player_reach(hud2.player_id) # #237: o que o motor oferece neste instante (verdade)
					if live.ok and live.body is Dictionary:
						for offer in live.body.get("offers", []):
							if String(offer.get("kind", "")) == "water":
								var gap := Vector2(maxf(maxf(float(offer.x) - hud2.fine.x, hud2.fine.x - float(offer.x) - 1.0), 0.0), maxf(maxf(float(offer.y) - hud2.fine.y, hud2.fine.y - float(offer.y) - 1.0), 0.0)).length()
								live_water = "agua@%d,%d folga=%.2f" % [offer.x, offer.y, gap]
					var node = app.world_view.npc_nodes.get(hud2.npc_id)
					print("walkwatch: t=", snappedf(float(wspec.get_slice("=", 1)) - left, 0.01), " escopo=", app.store.kind, " fine=", hud2.fine, " motor_fine=", hud2._last_server, " vel=", snappedf(hud2.speed_cells, 0.001), " corpo=", node.position if node != null and is_instance_valid(node) else "sem corpo",
						" alvo_cam=", app.rig.target, " cam=", app.rig.camera.global_position, " zoom=", snappedf(app.rig.zoom_size, 0.01), " segue=", app.rig.following != null and is_instance_valid(app.rig.following), " apoio=", app.body_scope.anchor != null and is_instance_valid(app.body_scope.anchor), " ator=", app.player_actor() != null, " prompt='", shown, "' motor_agua=", live_water)
					if arg.contains(",") and int(roundf(left * 4.0)) % 2 == 0: # um quadro a cada 0,5 s: `walkwatch:W=8,/caminho/prefixo`
						await RenderingServer.frame_post_draw
						app.get_viewport().get_texture().get_image().save_png("%s-%04d.png" % [arg.get_slice(",", 1), int(roundf((float(wspec.get_slice("=", 1)) - left) * 100.0))])
				var wrelease: InputEventKey = wpress.duplicate()
				wrelease.pressed = false
				Input.parse_input_event(wrelease)
			"walkto": # #241: só captura: `walkto:x,y[,s]` vira o olhar para o ponto (células) e anda de W até ficar a menos de 1,2 m, ou s segundos (padrão 40)
				var goal := Vector2(float(arg.get_slice(",", 0)), float(arg.get_slice(",", 1)))
				var limit := float(arg.get_slice(",", 2)) if arg.count(",") >= 2 else 40.0
				var first = app.world_view.npc_nodes.get(app.player_hud.npc_id)
				if first != null: # o olhar já vai ao ponto antes de apertar W (o motor ainda não deu a posição fina)
					var from_start := goal - Vector2(first.position.x, first.position.z)
					app.rig.yaw = atan2(-from_start.x, -from_start.y)
					app.rig.view_yaw = app.rig.yaw
				var w_press := InputEventKey.new()
				w_press.keycode = KEY_W
				w_press.physical_keycode = KEY_W
				w_press.pressed = true
				Input.parse_input_event(w_press)
				var spent := 0.0
				while spent < limit:
					var here: Vector2 = app.player_hud.fine
					var gap := goal - here
					if here != Vector2.INF and gap.length() * preload("res://Scenes/grid.gd").cell_meters < 1.2:
						break
					if here != Vector2.INF:
						app.rig.yaw = atan2(-gap.x, -gap.y)
						app.rig.view_yaw = app.rig.yaw
					await app.get_tree().create_timer(0.1).timeout
					spent += 0.1
				var w_release: InputEventKey = w_press.duplicate()
				w_release.pressed = false
				Input.parse_input_event(w_release)
				print("walkto: fine=", app.player_hud.fine, " em ", snappedf(spent, 0.1), " s")
			"offers": # #246: imprime as ofertas completas do motor (verbo, usos) para o texto `arg`
				for entry in await app.player_hud._reach_entries():
					if arg == "" or arg.to_lower() in String(entry.text).to_lower():
						print("offer: ", entry)
			"walkperson": # #246: `walkperson:Rafael` anda de W até a pessoa da casa (olhar virado a ela) e para a menos de 1 m; imprime onde ela estava
				if app.player_hud.housemates.is_empty():
					await app.player_hud.refresh_housemates()
				var other_id := -1
				for mate in app.player_hud.housemates:
					if arg.to_lower() in String(mate.get("name", "")).to_lower():
						other_id = int(mate.id)
				var other_node = app._actor_by_id(other_id)
				print("walkperson: ", arg, " id=", other_id, " nó=", other_node != null, " em ", other_node.position if other_node != null else Vector3.ZERO)
				if other_node != null:
					var wp := InputEventKey.new()
					wp.keycode = KEY_W
					wp.physical_keycode = KEY_W
					wp.pressed = true
					Input.parse_input_event(wp)
					var spent_wp := 0.0
					while spent_wp < 30.0:
						var here_wp: Vector2 = app.player_hud.fine
						var gap_wp := Vector2(other_node.position.x, other_node.position.z) - here_wp
						if here_wp != Vector2.INF:
							if gap_wp.length() * preload("res://Scenes/grid.gd").cell_meters < 1.0:
								break
							app.rig.yaw = atan2(-gap_wp.x, -gap_wp.y)
							app.rig.view_yaw = app.rig.yaw
						await app.get_tree().create_timer(0.1).timeout
						spent_wp += 0.1
					var wp_up: InputEventKey = wp.duplicate()
					wp_up.pressed = false
					Input.parse_input_event(wp_up)
					print("walkperson: chegou em ", snappedf(spent_wp, 0.1), " s")
			"speed": # #246: `speed:N` (0 a 3) aperta o botão de velocidade do HUD (o motor valida)
				await app.player_hud.set_speed(int(arg))
			"key": # #246: `key:G` ou `key:E=1.5` aperta a tecla (segura por s segundos, padrão 0,1), como o jogador
				var kcode := OS.find_keycode_from_string(arg.get_slice("=", 0))
				var kdown := InputEventKey.new()
				kdown.keycode = kcode
				kdown.physical_keycode = kcode
				kdown.pressed = true
				Input.parse_input_event(kdown)
				await app.get_tree().create_timer(float(arg.get_slice("=", 1)) if "=" in arg else 0.1).timeout
				var kup: InputEventKey = kdown.duplicate()
				kup.pressed = false
				Input.parse_input_event(kup)
			"walkstats": # #248: imprime a medida do passo (puxões > 0,3 m, saltos, pior deslocamento extra); `walkstats:reset` zera
				if arg == "reset":
					app.player_hud.walk_stats = {"frames": 0, "pulls": 0, "pull_m": 0.0, "replies": 0, "snaps": 0, "worst_m": 0.0, "back_m": 0.0, "back_events": 0, "gap_mean_m": 0.0, "gap_worst_m": 0.0}
				print("walkstats: ", app.player_hud.walk_stats, " fine=", app.player_hud.fine)
			"speed": # #258: `speed:N` a velocidade do tempo do jogador (0 pausa, 1, 2, 3), pelo mesmo botão do HUD
				await app.player_hud.set_speed(int(arg))
			"delta": # #258: `delta:MS` o admin troca o Δ do tick (ms de mundo por tick; divide a hora): 1000 = 20 s de mundo por s real; `delta:50` volta ao tempo real
				var reply: Dictionary = await app.api.transport.request("POST", "/simulation/tick-delta", {"deltaMs": int(arg)})
				print("delta: ", arg, " ms -> ", reply.get("status"))
			"skip": # #258: `skip:S` o admin acelera o mundo por S s reais (uma hora de mundo por tick) e imprime a hora; o jogador fica onde está (a prova do parceiro fora de casa)
				await app.api.transport.request("POST", "/simulation/accelerate", {"on": true})
				await app.get_tree().create_timer(float(arg if arg != "" else "1")).timeout
				await app.api.transport.request("POST", "/simulation/accelerate", {"on": false})
				print("skip: relógio=", app.player_frame.clock_label.text if app.player_frame != null else app.day_cycle.clock_text())
			"golocal": # #264: `golocal:X,Y` anda até a célula X,Y da planta da casa em foco (converte para a do mundo e usa `goto`)
				var origin: Vector2i = app.stage.interior_view.origin
				await run(app, ["goto:%d,%d" % [origin.x + int(arg.get_slice(",", 0)), origin.y + int(arg.get_slice(",", 1))]])
			"goto": # #258: `goto:X,Y` anda com W até a célula X,Y (até 120 s): o olhar vira para ela a cada 0,1 s e o passo para a ~0,3 célula; imprime onde parou
				var goal := Vector2(float(arg.get_slice(",", 0)) + 0.5, float(arg.get_slice(",", 1)) + 0.5)
				var go_press := InputEventKey.new()
				go_press.keycode = KEY_W
				go_press.physical_keycode = KEY_W
				go_press.pressed = true
				Input.parse_input_event(go_press)
				var walked := 0.0
				while walked < 120.0:
					var here: Vector2 = app.player_hud.fine
					if here != Vector2.INF:
						var delta_cells := goal - here
						if delta_cells.length() < 0.3:
							break
						app.rig.yaw = atan2(-delta_cells.x, -delta_cells.y)
						app.rig.view_yaw = app.rig.yaw
					await app.get_tree().create_timer(0.1).timeout
					walked += 0.1
				var go_release: InputEventKey = go_press.duplicate()
				go_release.pressed = false
				Input.parse_input_event(go_release)
				print("goto: ", arg, " em ", snappedf(walked, 0.1), " s, parou em ", app.player_hud.fine, " prédio=", app.player_hud.building_id)
			"walkloop": # #248: `walkloop:S` anda S s com W segurado, virando o olhar a cada ~2,5 s (reta, curva, parada) e imprime a medida
				var span := float(arg) if arg != "" else 30.0
				app.player_hud.walk_stats = {"frames": 0, "pulls": 0, "pull_m": 0.0, "replies": 0, "snaps": 0, "worst_m": 0.0, "back_m": 0.0, "back_events": 0, "gap_mean_m": 0.0, "gap_worst_m": 0.0}
				var loop_press := InputEventKey.new()
				loop_press.keycode = KEY_W
				loop_press.physical_keycode = KEY_W
				loop_press.pressed = true
				var turns := RandomNumberGenerator.new()
				turns.seed = 248
				var spent2 := 0.0
				var next_turn := 0.0
				var held := false
				while spent2 < span:
					if spent2 >= next_turn:
						next_turn = spent2 + turns.randf_range(1.5, 3.5)
						if turns.randf() < 0.18 and held: # pára um instante
							var up: InputEventKey = loop_press.duplicate()
							up.pressed = false
							Input.parse_input_event(up)
							held = false
							next_turn = spent2 + 0.6
						else:
							app.rig.yaw += deg_to_rad(turns.randf_range(-80.0, 80.0))
							app.rig.view_yaw = app.rig.yaw
							if not held:
								Input.parse_input_event(loop_press)
								held = true
					await app.get_tree().create_timer(0.1).timeout
					spent2 += 0.1
				var loop_release: InputEventKey = loop_press.duplicate()
				loop_release.pressed = false
				Input.parse_input_event(loop_release)
				print("walkloop: ", span, " s ", app.player_hud.walk_stats, " fine=", app.player_hud.fine)
			"busy": # #241: só captura: `busy:gather,60` finge a pose do motor com a ação em andamento (60 s, 40% feitos), para ver a barra, o gesto e o bloqueio do WASD
				var seconds := float(arg.get_slice(",", 1)) if "," in arg else 60.0
				var scale: float = float(app.player_hud.time_view.get("timeScale", 1.0)) if app.player_hud.time_view.get("timeScale") != null else 1.0
				app.player_hud.apply_pose({"busy": {"action": arg.get_slice(",", 0), "untilMs": 0, "totalMs": int(seconds * 1000.0 * scale), "remainingMs": int(seconds * 600.0 * scale)}})
			"gesture": # #224: `gesture:reach` toca um gesto no corpo do jogador (captura das animações)
				var own = app.world_view.npc_nodes.get(app.player_hud.npc_id)
				if own != null:
					own.play_gesture(arg, 1.6)
			"pitch": # #224: `pitch:-40` inclina o olhar do jogador (graus)
				app.rig.pitch = deg_to_rad(float(arg))
				app.rig._apply()
			"nochannel": # #222: depuração: comandos do jogador pela rota HTTP em vez do canal
				app.api.close_player_channel()
			"money": # #222: o que a ficha do motor diz agora (dinheiro), lida na hora
				var detail: Dictionary = await app.api.npc_detail(app.player_hud.npc_id)
				print("money: ok=", detail.ok, " status=", detail.status, " money=", detail.body.get("money") if detail.body is Dictionary else null, " erros=", detail.get("errors", []))
			"sizes": # #222: tamanhos da ficha (depuração de layout)
				var ins = app.inspector
				print("sizes: inspector ", ins.size, " min ", ins.get_combined_minimum_size(), " side ", app.side_panel.size)
				for child in ins.get_child(0).get_children():
					print("sizes:   ", child.get_class(), " ", child.size, " min ", child.get_combined_minimum_size(), " vis ", child.visible)
			"reach": # #237: a dica do E que o cliente mostra agora × o que o motor oferece agora (prova da água de longe)
				var hud = app.player_hud
				var live: Dictionary = await app.api.player_reach(hud.player_id)
				var kinds: Array = []
				if live.ok and live.body is Dictionary:
					for offer in live.body.get("offers", []):
						kinds.append("%s:%s@%s,%s" % [offer.get("kind"), ",".join(offer.get("verbs", [])), offer.get("x"), offer.get("y")])
				print("reach: fine=", hud.fine, " dica='", hud.reach_hint.text, "' prompt='", app.player_frame.prompt.text if app.player_frame != null else "", "' vis=", app.player_frame.prompt.visible if app.player_frame != null else false, " motor=", kinds)
			"pos": # #222: onde o corpo do jogador está (motor e desenho)
				var own = app.world_view.npc_nodes.get(app.player_hud.npc_id)
				print("pos: escopo=", app.store.kind, " segue=", app.rig.following != null and is_instance_valid(app.rig.following), " apoio=", app.body_scope.anchor != null and is_instance_valid(app.body_scope.anchor), " ator=", app.player_actor() != null, " ativo=", app.player_hud.is_active, " fine=", app.player_hud.fine, " speed=", app.player_hud.speed_cells,
					" corpo=", own.position if own != null else "sem corpo", " modo=", app.rig.mode, " braço=", app.rig.arm, " subida=", app.rig.lift,
					" ombro=", app.rig.shoulder, " dentro=", app.rig.opened_ids().keys(), " cartão=", app.player_frame.purse_label.text if app.player_frame != null else "", " leitura=", app.player_hud._body_pending, "/", snappedf(app.player_hud._body_clock, 0.1), " rpg=", app.player_hud.rpg, " npc=", app.player_hud.npc_id, " menu1=", app.player_frame.entries[0].text if app.player_frame != null and not app.player_frame.entries.is_empty() else "")
				var actor = app.player_actor()
				print("pos: ator=", actor.position if actor != null else "sem ator", " alvo=", app.rig.target, " camera=", app.rig.camera.global_position, " yaw=", snappedf(app.rig.view_yaw, 0.01), " dentro_da_casa=", app.stage.active, " predio_do_motor=", app.player_hud.building_id, " visivel=", actor.visible if actor != null else "-", " tela=", app.rig.camera.unproject_position(actor.position + Vector3(0, 0.7, 0)) if actor != null else "-", " atras=", app.rig.camera.is_position_behind(actor.position) if actor != null else "-", " rot=", app.rig.camera.global_rotation_degrees, " cams=", app.get_viewport().get_camera_3d() == app.rig.camera, " ocupantes=", app.stage.interior_view.occupant_nodes.keys(), " npcs_store=", app.store.interior.get("occupants", []).size(), " ator_pai=", actor.get_parent().name if actor != null else "-")
				if own != null:
					var eye: Vector3 = app.rig.target + Vector3(0, app.rig.eye_height() + app.rig.shoulder_height(), 0)
					for entry in app.camera_blockers():
						if (entry[1] as AABB).has_point(eye) or (entry[2] as AABB).has_point(eye):
							print("pos: olho na caixa ", entry[0], " ", entry[1], " paredes=", (entry[3] as Array).size())
			"wv": # #227: estado do mundo desenhado (prédios visíveis, modo de processo, nós de transparência)
				var shown := 0
				var faded := 0
				for bid in app.world_view.buildings.nodes:
					var bnode: Node3D = app.world_view.buildings.nodes[bid]
					if bnode.is_visible_in_tree():
						shown += 1
					if app.world_view.buildings.faded.has(bid):
						faded += 1
				print("wv: prédios=", app.world_view.buildings.nodes.size(), " visíveis=", shown, " translúcidos=", faded, " mundo_visivel=", app.world_view.visible, " processo=", app.world_view.process_mode, " opened=", app.rig.opened_ids().keys(), " escopo=", app.store.kind, " casa=", app.stage.active, " cena=", app.store.cities.size())
			"meter": # #249: `meter:rótulo` imprime a medida do trecho (quadros, pior quadro, > 100 ms, salto da câmera, quadros sem figura) e começa outro
				var meter = app.get_node_or_null("FrameMeter")
				if meter == null:
					meter = preload("res://Scenes/frame_meter.gd").new()
					meter.name = "FrameMeter"
					meter.app = app
					app.add_child(meter)
				else:
					print(meter.report(arg))
					meter.reset()
			"cost": # #249: custo somado desde o último `cost` (snapshot por escopo e cada camada do redesenho) em ms/vezes
				print("cost[", arg, "]: ", FrameCost.report())
				FrameCost.reset()
			"scopeinfo": # #249: onde está cada coisa agora (escopo desenhado, casa, assinatura do interior, figura do jogador)
				var own2 = app.world_view.npc_nodes.get(app.player_hud.npc_id) if app.player_hud.is_active else null
				print("scopeinfo: escopo=", app.store.kind, " casa=", app.stage.active, " mundo_visivel=", app.world_view.visible, " interior_assinado=", app.get("interior_stream").ref_id if app.get("interior_stream") != null else "-",
					"/", app.get("interior_stream").state if app.get("interior_stream") != null else "-", " figura=", own2.get_instance_id() if own2 != null else 0, " ator=", app.player_actor().get_instance_id() if app.player_actor() != null else 0,
					" npcs=", app.world_view.npc_nodes.size(), " predio_do_motor=", app.player_hud.building_id if app.player_hud.is_active else -2, " ocupantes_casa=", app.stage.interior_view.occupant_nodes.keys())
			"quit":
				await app.release_observation() # como fechar a janela: retira a fonte e encerra a conversa
				app.get_tree().quit()



## Medida recalibrada (ADR-0047): sem vsync, na cidade aberta (prédios e NPCs reais do servidor), com a ficha do
## primeiro morador aberta e a multidão completada até `total` NPCs andando dentro da cidade. Imprime FPS médio e o
## tempo de quadro p95 de 6 s, depois de 1 s de aquecimento. Uso: `--steps "wait:3;city;wait:3;bench:500;quit"`.
## Morador na rua (fora da planta dos prédios), de preferência trabalhando ou conversando; -1 se não há.
static func stacks_report(nodes: Dictionary, limit: float = 0.25) -> String:
	var cells := {}
	for id in nodes:
		var at: Vector3 = nodes[id].position
		cells.get_or_add(Vector2i(floori(at.x / limit), floori(at.z / limit)), []).append(id)
	var close := {}
	var best: Array = []
	for id in nodes:
		var at: Vector3 = nodes[id].position
		var group: Array = []
		for dx in [-1, 0, 1]:
			for dz in [-1, 0, 1]:
				for other in cells.get(Vector2i(floori(at.x / limit) + dx, floori(at.z / limit) + dz), []):
					if other != id and Vector2(at.x, at.z).distance_to(Vector2(nodes[other].position.x, nodes[other].position.z)) < limit:
						group.append(other)
		if not group.is_empty():
			close[id] = true
			if group.size() + 1 > best.size():
				best = group + [id]
	var detail := ""
	if not best.is_empty():
		var first = nodes[best[0]]
		detail = " maior=%d em %s ações=%s andando=%s" % [best.size(), first.position, best.map(func(i): return nodes[i].action),
			best.map(func(i): return nodes[i].walk != null)]
	return "stacks: npcs=%d sobrepostos=%d%s" % [nodes.size(), close.size(), detail]


## [posição a 1 m da quina mais próxima, giro para a quina ficar atrás da câmera] ou [].
static func _corner_spot(app: Node, from: Vector3) -> Array:
	var best: Array = []
	var best_distance := INF
	for node in app.world_view.buildings.nodes.values():
		var box: AABB = node.get_meta("box", AABB())
		if box.size == Vector3.ZERO:
			continue
		var center := Vector3(box.get_center().x, 0.0, box.get_center().z)
		for corner: Vector3 in [Vector3(box.position.x, 0, box.position.z), Vector3(box.end.x, 0, box.position.z), Vector3(box.position.x, 0, box.end.z), Vector3(box.end.x, 0, box.end.z)]:
			var outward: Vector3 = (corner - center).normalized()
			if from.distance_to(corner) < best_distance:
				best_distance = from.distance_to(corner)
				best = [corner + outward * Grid.units(1.5), atan2(-outward.x, -outward.z)]
	return best


static func _open_npc(app: Node) -> int:
	var boxes: Array = []
	for node in app.world_view.buildings.nodes.values():
		var box: AABB = node.get_meta("box", AABB())
		if box.size != Vector3.ZERO:
			boxes.append(box.grow(0.0))
	var best := -1
	var best_gap := -1.0
	var ids: Array = app.world_view.npc_nodes.keys()
	ids.sort()
	for id in ids:
		var at: Vector3 = app.world_view.npc_nodes[id].position
		var gap := INF
		for box in boxes:
			var b: AABB = box
			var dx := maxf(maxf(b.position.x - at.x, at.x - b.end.x), 0.0)
			var dz := maxf(maxf(b.position.z - at.z, at.z - b.end.z), 0.0)
			gap = minf(gap, sqrt(dx * dx + dz * dz))
		if gap > best_gap:
			best_gap = gap
			best = int(id)
	return best


static func _nearest_box_center(app: Node, from: Vector3, largest: bool = false) -> Vector3:
	var best := Vector3.INF
	var best_distance := INF
	for node in app.world_view.buildings.nodes.values():
		var box: AABB = node.get_meta("box", AABB())
		if box.size == Vector3.ZERO:
			continue
		var center := Vector3(box.get_center().x, 0.0, box.get_center().z)
		var score := from.distance_to(center) if not largest else -box.size.x * box.size.z
		if score < best_distance:
			best_distance = score
			best = center
	return best


static func camera_report(app: Node) -> String:
	var rig = app.rig
	var cam: Vector3 = rig.camera.global_position if rig.camera.is_inside_tree() else Vector3.INF
	var body: Vector3 = rig.following.position if rig.following != null and is_instance_valid(rig.following) else Vector3.INF
	var around: Array = []
	for entry in app.camera_blockers():
		var box: AABB = entry[1]
		if box.has_point(body + Vector3(0, 0.5, 0)):
			around.append("caixa=%s paredes=%d na_parede=%s" % [box, (entry[3] as Array).size(), (entry[3] as Array).any(func(w): return (w as AABB).has_point(body + Vector3(0, 1.3, 0)))])
		around.append("%s:%s%s%s" % [entry[0], "C" if box.has_point(cam) else "-", "B" if box.has_point(body + Vector3(0, 0.5, 0)) else "-", "F" if app.world_view.buildings.faded.has(entry[0]) else "-"])
	var roofs: Array = []
	for id in rig.seen_ids():
		var node = app.world_view.buildings.nodes.get(id)
		if node != null:
			for child in node.find_children("Roof*", "GeometryInstance3D", true, false):
				roofs.append(snappedf((child as GeometryInstance3D).transparency, 0.01))
	around.append("vistos=%s abertos=%s faded=%s kind=%s stage=%s teto=%s" % [rig.seen_ids().keys(), app.world_view.buildings.opened.keys(), app.world_view.buildings.faded.keys(), app.store.kind, app.stage.active, roofs])
	var own = app.world_view.npc_nodes.get(app.player_hud.npc_id)
	var near_ids: Array = []
	if own != null:
		near_ids = app.world_view.npcs.cell_members.get(Grid.to_cell(own.position), [])
	around.append("jogador=%s ativo=%s escondidos=%s mesma_celula=%s marcado=%s" % [app.player_hud.npc_id, app.player_hud.is_active, app._eye_hidden.keys(), near_ids, app._marked])
	return "camdump: mode=%s following=%s body=%s cam=%s arm=%s lift=%s shoulder=%s caixas=%s" % [rig.mode, rig.following != null, body, cam, snappedf(rig.arm, 0.01), snappedf(rig.lift, 0.01), snappedf(rig.shoulder, 0.01), around]


static func _street_npc(app: Node) -> int:
	var covered := {}
	for building in app.store.buildings.values():
		for entry in building.footprint:
			covered[building.cell + entry.cell] = true
	var ids: Array = app.store.npcs.keys()
	ids.sort()
	var best := -1
	for id in ids:
		if not app.world_view.npc_nodes.has(id) or covered.has(app.store.npcs[id].cell):
			continue
		if best < 0 or int(app.store.npcs[id].action) in [2, 3]:
			best = int(id)
			if int(app.store.npcs[id].action) in [2, 3]:
				break
	return best


## Cidade maior para o bench (`benchbig`): 40 prédios 2x2 a mais em grade dentro dos limites; no cliente com a projeção do
## observável (AD-171) também estado de prédio, 30 lavouras e 20 animais. Só carga sintética local, nada vai ao servidor.
static func _enlarge_city(app: Node, bounds: Rect2i, rng: RandomNumberGenerator) -> void:
	var observable: bool = "crops" in app.store
	for i in 40:
		var id := 900000 + i
		var cell := bounds.position + Vector2i((i % 8) * 3, (i / 8) * 3)
		var footprint: Array = []
		for c in [Vector2i(0, 0), Vector2i(1, 0), Vector2i(0, 1), Vector2i(1, 1)]:
			footprint.append({"cell": c, "material": 2 if c == Vector2i.ZERO else i % 2})
		app.store.buildings[id] = {"id": id, "type": i % 6, "cell": cell, "orientation": 0, "floors": 1 + i % 3,
			"footprint": footprint, "value": 100, "lodging": null}
		if observable:
			app.store.building_states[id] = {"building_id": id, "type": i % 6, "type_name": "", "function": ["dwelling", "workplace", "lodging"][i % 3],
				"workplace_id": id, "location_type_id": i % 5, "location_type_name": ["Fazenda", "Ferraria", "Padaria", "Mercado", "Taverna"][i % 5],
				"owner_id": -1, "households": [], "residents": [], "workers": [], "stock": [], "pantry": 0, "closed": i % 7 == 0}
	if observable:
		var stages := ["unsown", "planted", "growing", "mature", "harvested", "fallow"]
		for i in 30:
			app.store.crops[800000 + i] = {"id": 800000 + i, "cell": bounds.position + Vector2i(rng.randi_range(0, bounds.size.x - 1), rng.randi_range(0, bounds.size.y - 1)),
				"resource_id": i % 4, "resource_name": ["Trigo", "Milho", "Feijão", "Batata"][i % 4], "status": "", "stage": stages[i % 6],
				"fraction": rng.randf(), "thirsty": i % 5 == 0, "sown": 1.0, "arable": 2.0, "standing": 0, "farm_id": -1, "owner_id": -1, "expected": 10}
		for i in 20:
			app.store.animals[700000 + i] = {"id": 700000 + i, "species": ["rabbit", "wolf", "sheep", "chicken"][i % 4],
				"cell": bounds.position + Vector2i(rng.randi_range(0, bounds.size.x - 1), rng.randi_range(0, bounds.size.y - 1)),
				"energy": 50, "disease": false, "predator_of": ""}
		app.store.crops_changed.emit()
		app.store.fauna_changed.emit()
	app.world_view.buildings.rebuild(app.store)


## Corpo sintético pelo estado (cliente AD-171): ofício, idade, gravidez, carga e luto variados, como o escopo manda.
static func _synthetic_observable(i: int) -> Dictionary:
	var ages := [1, 8, 15, 30, 45, 70]
	var age: int = ages[i % ages.size()]
	return {"name": "Sintético %d" % i, "sex": "Female" if i % 2 == 0 else "Male", "age": age,
		"stage": "Child" if age < 13 else ("Elder" if age >= 60 else "Adult"), "profession_id": i % 6,
		"profession_name": ["Lavrador", "Ferreiro", "Padeiro", "Mercador", "Lenhador", "Guarda"][i % 6],
		"height_cm": [75, 125, 160, 170, 168, 162][i % ages.size()], "growth": 1.0, "pregnancy": 0.6 if i % 17 == 0 else 0.0,
		"babies": 1, "ill": i % 13 == 0, "symptomatic": i % 13 == 0, "bedridden": false, "disease_id": -1, "emotions": {},
		"dominant": ["", "joy", "grief", "fear", "anger"][i % 5], "mourning": i % 11 == 0,
		"carried": [{"resource_id": 2, "quantity": 4, "name": "Trigo"}] if i % 4 == 0 else [], "held": {}}


static func _bench(app: Node, total: int, big: bool = false) -> void:
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	Engine.max_fps = 0
	var real: Array = app.store.npcs.keys()
	real.sort()
	if not real.is_empty():
		await app.select_npc(int(real[0]))
	var bounds: Rect2i = app.store.city.get("bounds", Rect2i(0, 0, 30, 30)) if app.store.kind == "City" else Rect2i(0, 0, 30, 30)
	var rng := RandomNumberGenerator.new()
	rng.seed = 11
	if big and app.store.kind == "City":
		_enlarge_city(app, bounds, rng)
	var observable: bool = "crops" in app.store
	var synthetic: Array = []
	for i in maxi(total - real.size(), 0):
		var id := 200000 + i
		var cell := Vector2i(bounds.position.x + rng.randi_range(0, bounds.size.x - 1), bounds.position.y + rng.randi_range(0, bounds.size.y - 1))
		app.store.npcs[id] = {"id": id, "cell": cell, "action": rng.randi_range(0, 9), "city": "", "look": {}}
		if observable:
			app.store.npcs[id]["obs"] = _synthetic_observable(i)
		app.store.npc_changed.emit(id, cell, cell, app.store.npcs[id].action)
		if observable:
			app.store.npc_observed.emit(id)
		synthetic.append(id)
	var frame_ms: Array = []
	var clock := 0.0
	var next_step := 0.5
	var costed := false
	var last := Time.get_ticks_usec()
	while clock < 7.0:
		await app.get_tree().process_frame
		var now := Time.get_ticks_usec()
		var dt := (now - last) / 1000.0
		last = now
		clock += dt / 1000.0
		if clock >= 1.0:
			frame_ms.append(dt)
			if not costed:
				costed = true
				FrameCost.reset()
			FrameCost.usec["~process"] = int(FrameCost.usec.get("~process", 0)) + int(Performance.get_monitor(Performance.TIME_PROCESS) * 1000000.0)
			FrameCost.count["~process"] = int(FrameCost.count.get("~process", 0)) + 1
			FrameCost.usec["~draws"] = int(FrameCost.usec.get("~draws", 0)) + int(Performance.get_monitor(Performance.RENDER_TOTAL_DRAW_CALLS_IN_FRAME)) * 1000
			FrameCost.count["~draws"] = int(FrameCost.count.get("~draws", 0)) + 1
		if clock >= next_step:
			next_step += 0.5
			for id in synthetic:
				if not app.store.npcs.has(id):
					continue
				var from_cell: Vector2i = app.store.npcs[id].cell
				var to_cell := Vector2i(clampi(from_cell.x + rng.randi_range(-1, 1), bounds.position.x, bounds.end.x - 1),
					clampi(from_cell.y + rng.randi_range(-1, 1), bounds.position.y, bounds.end.y - 1))
				app.store.npcs[id].cell = to_cell
				app.store.npc_changed.emit(id, from_cell, to_cell, app.store.npcs[id].action)
	var sorted := frame_ms.duplicate()
	sorted.sort()
	var total_ms := 0.0
	for value in frame_ms:
		total_ms += value
	var p95: float = sorted[mini(int(sorted.size() * 0.95), sorted.size() - 1)] if not sorted.is_empty() else 0.0
	print("%s: scope=%s npcs=%d buildings=%d fps_avg=%.1f p95_ms=%.2f frames=%d vsync=%d" % ["benchbig" if big else "bench", app.store.kind, app.store.npcs.size(),
		app.store.buildings.size(), frame_ms.size() / (total_ms / 1000.0) if total_ms > 0 else 0.0, p95, frame_ms.size(),
		DisplayServer.window_get_vsync_mode()])
	print("bench-cost: ", FrameCost.report(), " walkers=", app.world_view.npc_nodes.values().filter(func(a): return a.walk != null).size(),
		" nodes=", Performance.get_monitor(Performance.OBJECT_NODE_COUNT))


## Janelas com dados de exemplo (só para capturas de tela): `demo:biografia|cronica|crencas|relatos|tecnologias|tesouro|vendas`.
static func _demo(app: Node, which: String) -> void:
	app._exclusive(null)
	var ok := func(body: Variant) -> Dictionary: return {"ok": true, "status": 200, "body": body}
	var claims := [{"text": "Birth (evento 3): nasceu na vila", "eventIds": [3]}, {"text": "Marriage (evento 9): casou com Ana", "eventIds": [9, 11]},
		{"text": "Death (evento 14): perdeu o irmão numa praga", "eventIds": [14]}]
	match which:
		"biografia":
			app.narrative.mode = "biography"; app.narrative.subject_name = "Beto"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative._title.text = "Biografia de Beto"
			app.narrative._render(ok.call({"npcId": 7, "prose": "Beto cresceu na vila, casou cedo e viu a praga levar a família.", "claims": claims}))
		"cronica":
			app.narrative.mode = "chronicle"; app.narrative.subject_name = "Alfa"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative.period_option.visible = true; app.narrative._title.text = "Crônica de Alfa"
			app.narrative._render(ok.call({"id": 1, "prose": "O ano foi marcado pela praga e pela recuperação da colheita.", "claims": claims}))
		"crencas":
			app.narrative.mode = "beliefs"; app.narrative.subject_name = "Alfa"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative._title.text = "O que Alfa acredita"
			app.narrative._render(ok.call([{"reportId": 1, "distortedMagnitude": 0.83, "distortedAtMs": 300 * 3600000, "seed": "A praga foi castigo dos vizinhos do norte", "distanceFromFact": 0.62},
				{"reportId": 2, "distortedMagnitude": 0.41, "distortedAtMs": 900 * 3600000, "seed": "O rei morreu de fome", "distanceFromFact": 0.15}]))
		"relatos":
			app.narrative.mode = "reports"; app.narrative.is_open = true; app.narrative.visible = true
			app.narrative._title.text = "Relatos que circulam"
			app.narrative._render(ok.call([{"reportId": 1, "originFactId": 5, "medium": "Song", "confidence": 0.82}, {"reportId": 2, "originFactId": 8, "medium": "Book", "confidence": 0.25}]))
		"tecnologias":
			app.technology.is_open = true; app.technology.visible = true
			app.technology._render(ok.call([{"category": "mobilidade", "vector": [2, 4, 6], "key": "2.4.6", "discoveredAtMs": 500 * 3600000, "isVariation": false, "variationOfKey": null, "adoptedByCities": 1},
				{"category": "mobilidade", "vector": [3, 4, 6], "key": "3.4.6", "discoveredAtMs": 800 * 3600000, "isVariation": true, "variationOfKey": "2.4.6", "adoptedByCities": 3}]))
		"tesouro":
			app.economy.mode = "treasury"; app.economy.is_open = true; app.economy.visible = true; app.economy._title.text = "Tesouro de Alfa"
			app.economy._render(ok.call({"cityId": "x", "balance": 1902, "fromDay": 0, "toDay": 359, "months": [{"month": 0, "revenue": 46, "spending": 0}, {"month": 1, "revenue": 52, "spending": 30}, {"month": 2, "revenue": 40, "spending": 80}]}))
		"vendas":
			app.economy.mode = "trades"; app.economy.is_open = true; app.economy.visible = true; app.economy._title.text = "Vendas de local 0"
			app.economy._render(ok.call({"workplaceId": 0, "fromDay": 0, "toDay": 29, "days": [{"day": 3, "resourceId": 1, "quantity": 30, "value": 30, "purchases": 2}, {"day": 4, "resourceId": 2, "quantity": 20, "value": 20, "purchases": 1}]}))


## AD-197t: preenche o criador (`campo=valor` separados por vírgula): name, sex (0 sortear, 1 mulher, 2 homem), age, profession (índice),
## role (newcomer, child, sibling, spouse, relative), family (índice da lista), origin (índice), money, health, height, outfit (índice),
## look.<gene> e trait.<traço>. Espera a prévia do motor ao fim.
static func _creator_set(app: Node, text: String) -> void:
	var designer = app.creator.designer
	for pair in text.split(",", false):
		var key: String = pair.get_slice("=", 0)
		var value: String = pair.get_slice("=", 1)
		match key:
			"name":
				designer.name_edit.text = value
			"sex":
				designer.sex_select.select(int(value))
			"profession":
				designer.profession_select.select(int(value))
			"role":
				for i in designer.ROLES.size():
					if designer.ROLES[i][0] == value:
						designer.role_select.select(i)
				designer._refill_families()
			"family":
				designer.family_select.select(int(value))
				designer._refill_vias()
			"via":
				designer.via_select.select(int(value))
			"origin":
				designer.origin_select.select(int(value))
			"outfit":
				designer.outfit_select.select(int(value))
			"age", "money", "health":
				designer.set_field(key, float(value))
			"height":
				designer.set_field("heightM", float(value))
			_:
				designer.set_field(key, float(value))
	await designer.refresh()
