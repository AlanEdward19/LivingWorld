extends "res://tests/test_base.gd"
## #264: modo comprar e construir. O cliente só mostra o que o motor diz (catálogo, estoque, preço, peças, cômodos) e só pergunta e pede: o fantasma fica verde ou
## vermelho pela resposta do motor, com o motivo; Q/E giram; o verbo do clique sai da célula, do giro e da ferramenta; o painel tem miniaturas desenhadas por código.

const BuyMode := preload("res://Scenes/buy_mode.gd")
const FurnitureThumb := preload("res://Scenes/furniture_thumb.gd")
const Grid := preload("res://Scenes/grid.gd")
const ApiClient := preload("res://Contract/api_client.gd")
const FakeTransport := preload("res://tests/fake_transport.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")
const Modal := preload("res://Scenes/modal.gd")

const SOFA := {"id": "sofa", "category": "furniture", "variationSeed": 0, "usePoints": [], "uses": [],
	"shape": [{"shape": "box", "size": [1.3, 0.45, 0.7], "offset": [0.0, 0.225, 0.0], "color": "5b6f8a", "symmetry": "none"},
		{"shape": "box", "size": [1.3, 0.4, 0.15], "offset": [0.0, 0.65, -0.3], "color": "4d5f78", "symmetry": "none"}]}
const PLANT := {"id": "plant", "category": "decor", "variationSeed": 0, "usePoints": [], "uses": [],
	"shape": [{"shape": "cylinder", "size": [0.17, 0.3], "offset": [0.0, 0.15, 0.0], "color": "9a5b3c", "symmetry": "none"},
		{"shape": "sphere", "size": [0.28], "offset": [0.0, 0.62, 0.0], "color": "3f7d3a", "symmetry": "none"}]}

const HOME := {"building": 4, "wallet": 50, "shop": "Loja de móveis 1", "shopNote": null, "floors": 1, "resalePercent": 60,
	"categories": [{"id": "seat", "name": "Sentar"}, {"id": "decor", "name": "Decoração"}],
	"items": [
		{"object": "sofa", "name": "Sofá", "category": "seat", "price": 30, "orderPrice": 36, "beauty": 3.0, "stock": 1, "orderable": false, "craftHours": 8, "wall": false,
			"rooms": [], "effects": [{"use": "Sentar e relaxar", "need": "fun", "perHour": 40.0, "needName": "Diversão"}], "light": false},
		{"object": "plant", "name": "Planta", "category": "decor", "price": 8, "orderPrice": 9, "beauty": 2.0, "stock": 0, "orderable": true, "craftHours": 2, "wall": false,
			"rooms": [], "effects": [], "light": false}],
	"pieces": [{"piece": 3, "id": "sofa", "name": "Sofá", "floor": 0, "x": 2, "y": 2, "facing": 90, "beauty": 3.0, "resale": 18, "sellable": true, "busy": false}],
	"rooms": [{"floor": 0, "index": 0, "kind": "sala", "cells": 8, "beauty": 7.0, "items": 7.0, "paint": 0.0}, {"floor": 0, "index": 1, "kind": "banheiro", "cells": 4, "beauty": -1.5, "items": 1.5, "paint": -3.0}],
	"paints": [{"id": "wood", "name": "Tábua corrida", "kind": "floor", "color": "b58253", "price": 1, "beauty": 2.0},
		{"id": "cream", "name": "Creme", "kind": "wall", "color": "eadfc2", "price": 1, "beauty": 1.0}],
	"build": {"wall": 4, "door": 3, "demolish": 1, "cell": 6, "extraCells": 0, "maxExtraCells": 12}, "orders": []}


func _house(paint: Variant = null) -> Dictionary:
	var cells: Array = []
	for y in 4:
		for x in 4:
			var material := 3
			if x == 0 or y == 0 or x == 3 or y == 3:
				material = 2 if (x == 2 and y == 3) else 1
			cells.append({"cell": {"x": x, "y": y}, "material": material})
	var info := {"building": 4, "floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": [],
		"objects": {"format": 1, "catalog": [SOFA, PLANT], "furniture": []}}
	if paint != null:
		info["paint"] = paint
	return info


func _mode(replies: Dictionary = {}, with_stage: bool = true) -> Array:
	var transport := FakeTransport.new()
	transport.on("GET /players/p-1/home/check", replies.get("check", {"status": 200, "body": {"ok": true, "reason": "", "price": 30, "refund": 0, "note": "Sofá"}}))
	transport.on("GET /players/p-1/home", replies.get("home", {"status": 200, "body": HOME}))
	transport.on("POST /players/p-1/interact", replies.get("interact", {"status": 200, "body": {"kind": "home", "id": 4, "verb": "x", "quantity": 1, "value": 30, "note": "Comprou Sofá",
		"pose": {"npcId": 7, "playerId": "p-1", "x": 3, "y": 3}}}))
	var mode := BuyMode.new()
	mode.api = ApiClient.new(transport)
	mode.player_id = "p-1"
	var info := _house()
	mode.catalog_of = func() -> Array: return info.objects.catalog
	mode.info_of = func() -> Dictionary: return info
	var holder := Control.new()
	tree.root.add_child(holder)
	holder.add_child(mode)
	var view: Node3D = null
	var rig = null
	if with_stage:
		view = InteriorView.new()
		view.catalog = AssetCatalog.load_default()
		holder.add_child(view)
		view.show_interior(info, Vector2i(10, 10), ActionLabels.new(), 3)
		var stage := _FakeStage.new()
		stage.interior_view = view
		holder.add_child(stage)
		mode.stage = stage
		rig = CameraRig.new()
		holder.add_child(rig)
		mode.rig = rig
	return [mode, transport, holder, view, rig]


class _FakeStage extends Control:
	var interior_view


func _free(made: Array) -> void:
	made[2].queue_free()


func _key(code: int) -> InputEventKey:
	var event := InputEventKey.new()
	event.keycode = code
	event.pressed = true
	return event


func _find(node: Node, node_name: String) -> Node:
	return node.find_child(node_name, true, false)


func test_the_panel_lists_the_engine_catalog_with_price_beauty_effect_stock_and_a_thumbnail_by_category() -> void:
	var made := await _mode()
	var mode = made[0]
	check(await mode.open(), "abriu: há casa própria e loja")
	eq(mode.cards.keys().size(), 2, "um card por móvel que o motor diz")
	var sofa = mode.cards.sofa
	eq(String(_find(sofa, "title").text), "Sofá · 30", "nome e preço do motor")
	eq(String(_find(sofa, "stock").text), "Em estoque: 1", "o estoque da loja")
	eq(String(_find(mode.cards.plant, "stock").text), "Esgotado: sob encomenda por 9, pronto em 2 h", "esgotado, o motor diz se faz por encomenda e o prazo")
	check(_find(sofa, "thumb_sofa") is TextureRect, "a miniatura do sofá é desenhada por código (o objeto do catálogo renderizado)")
	check(_find(mode.cards.plant, "thumb_plant") is TextureRect, "e a da planta")
	eq(String(_find(made[2], "money").text), "Dinheiro: 50", "o dinheiro atual do motor")
	eq(BuyMode.beauty_text(3.0), "Beleza +3", "a beleza")
	eq(BuyMode.effect_text(HOME.items[0]), "Diversão +40/h", "o efeito vem do uso do catálogo (nome da necessidade do motor)")
	mode.set_category("decor")
	eq(mode.cards.keys(), ["plant"], "a categoria filtra")
	mode.close()
	_free(made)


func test_the_thumbnail_frames_the_object_parts_and_is_a_viewport_texture() -> void:
	var box := FurnitureThumb.bounds_of(SOFA)
	check(absf(box.size.x - 1.3) < 0.001 and box.size.y > 0.8, "a caixa cobre as duas partes do sofá: %s" % str(box))
	var factory := preload("res://Scenes/object_factory.gd").new([SOFA])
	var thumb := FurnitureThumb.make(factory, SOFA, 64)
	tree.root.add_child(thumb)
	check(thumb.texture is ViewportTexture, "a miniatura é a textura de um SubViewport")
	var viewport: SubViewport = thumb.get_child(0)
	eq(viewport.size, Vector2i(128, 128), "desenhada no dobro para ficar nítida")
	check(viewport.own_world_3d and viewport.transparent_bg, "mundo próprio e fundo transparente: nada da casa entra")
	thumb.queue_free()


func test_the_ghost_follows_the_cell_rotates_with_q_and_e_and_the_verb_carries_cell_and_facing() -> void:
	var made := await _mode()
	var mode = made[0]
	await mode.open()
	mode.select_item("sofa")
	mode.hover = Vector2i(2, 1)
	eq(mode.current_verb(), "buy:sofa:0:2:1:0", "o verbo de compra: objeto, andar, célula, giro")
	mode._input(_key(KEY_E))
	eq(mode.facing, 90, "E gira para a direita")
	mode._input(_key(KEY_R))
	eq(mode.facing, 180, "R também")
	mode._input(_key(KEY_Q))
	mode._input(_key(KEY_Q))
	mode._input(_key(KEY_Q))
	eq(mode.facing, 270, "Q gira para a esquerda, dando a volta")
	eq(mode.current_verb(), "buy:sofa:0:2:1:270", "o giro entra no verbo")
	mode._update_ghost()
	check(mode.ghost != null and mode.ghost.visible, "o fantasma aparece sob o mouse")
	check(absf(mode.ghost.position.x - 12.5) < 0.01 and absf(mode.ghost.position.z - 11.5) < 0.01, "na célula (2,1) do prédio em (10,10): %s" % str(mode.ghost.position))
	check(absf(mode.ghost.rotation.y - deg_to_rad(270.0)) < 0.001, "girado como o verbo")
	mode.close()
	_free(made)


func test_the_ghost_is_green_or_red_by_what_the_engine_answers_and_the_reason_shows() -> void:
	var made := await _mode()
	var mode = made[0]
	var transport = made[1]
	await mode.open()
	mode.select_item("sofa")
	mode.hover = Vector2i(1, 1)
	await mode.ask(mode.current_verb())
	check(mode.ghost_is_green() and not mode.ghost_is_red(), "o motor disse que pode: verde")
	check(String(mode._reason.text).begins_with("Sofá"), "a frase do motor e o custo: %s" % mode._reason.text)
	check("custa 30" in String(mode._reason.text), "custa 30")
	check(transport.calls.any(func(call): return call.begins_with("GET /players/p-1/home/check?verb=buy%3Asofa%3A0%3A1%3A1%3A0")), "perguntou ao motor com o verbo")
	transport.on("GET /players/p-1/home/check", {"status": 200, "body": {"ok": false, "reason": "bloquearia a passagem da porta", "price": 0, "refund": 0, "note": ""}})
	mode.hover = Vector2i(2, 2)
	await mode.ask(mode.current_verb())
	check(mode.ghost_is_red() and not mode.ghost_is_green(), "o motor disse que não: vermelho")
	eq(String(mode._reason.text), "bloquearia a passagem da porta", "o motivo do motor aparece")
	mode.rotate_by(1)
	check(not mode.ghost_is_red() and not mode.ghost_is_green(), "mudou o giro: espera a nova resposta (neutro)")
	mode.close()
	_free(made)


func test_clicking_sends_the_home_command_only_when_the_engine_said_yes_then_reloads_the_money() -> void:
	var made := await _mode()
	var mode = made[0]
	var transport = made[1]
	await mode.open()
	var acted: Array = []
	mode.acted.connect(func(note: String): acted.append(note))
	mode.select_item("sofa")
	mode.hover = Vector2i(1, 2)
	transport.on("GET /players/p-1/home", {"status": 200, "body": HOME.merged({"wallet": 20}, true)})
	await mode.click()
	eq(acted, ["Comprou Sofá"], "o motor aceitou: o app é avisado do que foi")
	var sent: Array = transport.bodies.get("POST /players/p-1/interact", [])
	eq(sent.size(), 1, "um comando")
	eq(String(sent[0].kind), "home", "kind home")
	eq(int(sent[0].id), 4, "a casa do motor")
	eq(String(sent[0].verb), "buy:sofa:0:1:2:0", "o verbo")
	eq(String(_find(made[2], "money").text), "Dinheiro: 20", "o dinheiro caiu: relido do motor")
	mode.close()
	_free(made)


func test_a_no_from_the_engine_sends_nothing_and_a_409_warns_with_the_engine_reason() -> void:
	var made := await _mode({"check": {"status": 200, "body": {"ok": false, "reason": "falta dinheiro: custa 30, você tem 2", "price": 0, "refund": 0, "note": ""}}})
	var mode = made[0]
	var transport = made[1]
	await mode.open()
	var warnings: Array = []
	mode.warning.connect(func(text: String): warnings.append(text))
	mode.select_item("sofa")
	mode.hover = Vector2i(1, 2)
	await mode.click()
	eq(warnings, ["falta dinheiro: custa 30, você tem 2"], "o motivo do motor vira aviso")
	check(not transport.bodies.has("POST /players/p-1/interact"), "o motor disse não: nada é enviado")
	transport.on("POST /players/p-1/interact", {"status": 409, "body": {"error": "conflict: a loja não tem caixa para comprar essa peça agora"}})
	var ok: bool = await mode.command("sell:3")
	check(not ok, "o 409 volta como falha")
	eq(warnings[1], "a loja não tem caixa para comprar essa peça agora", "e o aviso é o texto do motor sem o prefixo")
	mode.close()
	_free(made)


func test_my_house_lists_the_pieces_with_resale_and_move_sell_discard_send_the_engine_verbs() -> void:
	var made := await _mode()
	var mode = made[0]
	var transport = made[1]
	await mode.open()
	mode.set_tab("mine")
	check(mode.piece_rows.has(3), "a peça 3 do motor está na lista")
	var row: Node = mode.piece_rows[3]
	var buttons: Array = row.find_children("*", "Button", true, false)
	eq(buttons.map(func(b): return String(b.text)), ["Mover", "Vender +18", "Descartar"], "mover, vender (o motor diz quanto volta) e descartar")
	await mode.command("sell:3")
	await mode.command("discard:3")
	var sent: Array = transport.bodies.get("POST /players/p-1/interact", [])
	eq(sent.map(func(b): return String(b.verb)), ["sell:3", "discard:3"], "os verbos de venda e descarte")
	mode.start_move(3)
	eq(mode.facing, 90, "mover começa no giro que a peça tem")
	mode.hover = Vector2i(1, 1)
	eq(mode.current_verb(), "move:3:0:1:1:90", "o verbo de mover")
	mode.close()
	_free(made)


func test_the_build_tab_has_the_tools_with_prices_and_the_edge_and_paint_verbs() -> void:
	var made := await _mode()
	var mode = made[0]
	await mode.open()
	mode.set_tab("build")
	eq(String(_find(made[2], "tool_wall").text), "Levantar parede · 4", "o preço da parede é do motor")
	eq(String(_find(made[2], "tool_extend").text), "Ampliar a casa · 6", "e o da célula")
	mode.use_tool("wall")
	mode.hover = Vector2i(2, 1)
	mode.hover_edge = [2, 1, 3, 1]
	eq(mode.current_verb(), "wall:0:2:1:3:1", "a aresta entre duas células, na ordem do motor")
	mode.use_tool("door")
	eq(mode.current_verb(), "door:0:2:1:3:1", "porta")
	mode.use_tool("extend")
	mode.hover = Vector2i(4, 1)
	eq(mode.current_verb(), "extend:4:1", "ampliar a célula fora da casa")
	mode.use_tool("paint_floor")
	eq(mode.paint_id, "wood", "a primeira tinta de piso que custa algo")
	check(_find(made[2], "paint_wood") != null, "as tintas do motor viram botões")
	eq(mode.current_verb(), "paint:floor:0:4:1:wood", "pintar o cômodo da célula")
	mode.use_tool("paint_wall")
	eq(mode.paint_id, "cream", "a tinta de parede")
	eq(mode.current_verb(), "paint:wall:0:4:1:cream", "pintar as paredes")
	mode.close()
	_free(made)


func test_edge_near_picks_the_closest_side_of_the_cell_in_the_engine_order() -> void:
	eq(BuyMode.edge_near(Vector2i(2, 2), Vector2(0.05, 0.5)), [1, 2, 2, 2], "perto da esquerda: a aresta com a vizinha de trás")
	eq(BuyMode.edge_near(Vector2i(2, 2), Vector2(0.95, 0.5)), [2, 2, 3, 2], "da direita")
	eq(BuyMode.edge_near(Vector2i(2, 2), Vector2(0.5, 0.04)), [2, 1, 2, 2], "de cima")
	eq(BuyMode.edge_near(Vector2i(2, 2), Vector2(0.5, 0.96)), [2, 2, 2, 3], "de baixo")


func test_opening_takes_the_camera_from_above_and_pauses_with_the_modal_and_closing_gives_it_back() -> void:
	var made := await _mode()
	var mode = made[0]
	var rig = made[4]
	var modal := Modal.new()
	mode.modal = modal
	modal.watch(mode)
	await mode.open()
	check(rig.build_view, "a câmera sobe sobre a casa")
	check(modal.active, "aberto é popup: o motor pausa o tempo (hold) enquanto se compra")
	eq(made[3].wall_mode, made[3].WALLS_DOWN, "de cima as paredes ficam baixas")
	mode.close()
	check(not rig.build_view, "fechou: a câmera do jogador volta")
	check(not modal.active, "e o tempo volta")
	eq(made[3].wall_mode, made[3].WALLS_CUT, "as paredes voltam ao corte de antes")
	_free(made)


func test_no_home_or_no_shop_means_no_buy_mode_and_the_player_is_told() -> void:
	var made := await _mode({"home": {"status": 404, "body": {"error": "sem casa própria"}}})
	var mode = made[0]
	var warnings: Array = []
	mode.warning.connect(func(text: String): warnings.append(text))
	check(not await mode.open(), "sem casa própria o modo não abre")
	eq(warnings.size(), 1, "o jogador é avisado")
	check(not mode.visible and not mode.is_open, "nada fica aberto")
	_free(made)


func test_the_house_paints_the_floor_and_the_walls_with_the_colors_the_engine_gives() -> void:
	var info := _house({"marks": [{"kind": "floor", "floor": 0, "x": 1, "y": 1, "bx": 0, "by": 0, "paint": "wood"},
		{"kind": "wall", "floor": 0, "x": 0, "y": 1, "bx": 1, "by": 1, "paint": "cream"}], "colors": {"wood": "b58253", "cream": "eadfc2"}})
	var view := InteriorView.new()
	view.catalog = AssetCatalog.load_default()
	tree.root.add_child(view)
	view.show_interior(info, Vector2i(0, 0), ActionLabels.new(), 3)
	eq(view._floor_color(Vector2i(1, 1), true, 0), Color("b58253"), "o piso pintado tem a cor da tinta do motor")
	check(view._floor_color(Vector2i(2, 2), true, 0) != Color("b58253"), "o que não foi pintado fica como era")
	check(view._paint_wall.has("0|0,1,1,1"), "a parede pintada é achada pela aresta do motor")
	view.queue_free()
