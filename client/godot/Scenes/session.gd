extends RefCounted
## Sessão e escopos: qual escopo está assinado (mundo, cidade, interior), o que o servidor acha que a câmera observa e
## a volta do interior para o escopo anterior. Prédio novo já vem com a planta no delta (AD-170) e o nome de cada pessoa
## vem no escopo: nenhuma leitura extra. Não desenha nada:
## manda a cena, a câmera e a interface do App reagirem.

const Store := preload("res://Store/world_store.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Grid := preload("res://Scenes/grid.gd")

var app
var previous_scope := ["World", ""]
var world_ref := ["World", ""]
var interior_building := -1
var gone_buildings := {} ## #237: prédios que o servidor disse não existirem (404)
var first_snapshot := false
var crumb_before := "Mundo" ## trilha antes de entrar no prédio


func _init(p_app) -> void:
	app = p_app


func enter_scope(kind: String, ref_id: String) -> void:
	if app.stage.active or interior_building >= 0:
		leave_focus() # #237: pedir outro escopo (zoom out, outra cidade) nunca deixa a casa em foco para trás
	world_ref = [kind, ref_id]
	await app.stream.subscribe(kind, ref_id)


func on_snapshot(envelope: Dictionary) -> void:
	var kind := Store.scope_name(envelope.scope.kind)
	app.store.apply_snapshot(envelope)
	publish_observation(kind)
	app.banner.visible = false
	if not first_snapshot:
		first_snapshot = true
		app.loading.visible = false
		app.on_world_shown()
		app.minimap.rebuild()
		app.sky_life.build(Vector2(app.store.map_size))
		app.rig.fit_to_map(maxi(app.store.map_size.x, app.store.map_size.y))
		app._load_names()
	if kind == "Interior":
		if interior_building < 0:
			return # #237: snapshot atrasado de uma casa que já não está em foco: não a reabre
		if app.stage.active and app.stage.interior_view.floor_count == int(app.store.interior.floors):
			app.stage.interior_view.set_occupants(app.store.interior.occupants) # reenvio do snapshot pelo canal: só atualiza
			app.stage.interior_view.apply_names(app.store.names)
		else:
			_enter_interior_view()
		return
	if app.stage.active:
		leave_focus() # #237: chegou o snapshot de cidade/mundo com a casa ainda em foco: o foco acabou
	app.rig.set_scope(kind)
	app.list_panel.city_filter = app.store.city.id if kind == "City" else ""
	if app.list_panel.visible:
		app.list_panel.search(app.list_panel.query)
	if kind == "City":
		app.breadcrumb.text = "Mundo › %s" % (app.store.city.name if app.store.city.name != "" else "Cidade")
		if not app.inspector.current.is_empty() and String(app.inspector.current.kind) == Registry.CITY: # #222: a ficha da cidade
			app.inspector.inspect(Registry.ref(Registry.CITY, String(app.store.city.id)))
		var area := built_area()
		if not app.director.active() and not _driving():
			app.rig.focus(center_of(area))
		if not _driving(): # #237: com o corpo do jogador a câmera é dele: a troca de escopo não a recentra nem muda o zoom
			app.rig.set_zoom(clampf(maxf(area.size.x, area.size.y) * 1.15 + 5.0, 7.0, app.rig.city_exit_size - 0.6))
		app._load_names()
	elif kind == "World":
		app.breadcrumb.text = "Mundo"
		if not app.inspector.current.is_empty() and String(app.inspector.current.kind) == Registry.CITY:
			app.inspector.close()
		if app.rig.zoom_size < app.rig.city_exit_size + 1.0 and not _driving():
			app.rig.set_zoom(22.0)
		# #222: no mapa do mundo a câmera abre sobre a primeira cidade (antes abria num campo vazio e a cidade era uma mancha na borda)
		var first := first_city_bounds()
		if first.size != Vector2i.ZERO and not app.director.active() and not _driving():
			app.rig.focus(Grid.to_world(first.position + first.size / 2))


## #237: o jogador tem um corpo e a câmera dele (3ª/1ª pessoa): a câmera segue o corpo, não o enquadramento de observador.
func _driving() -> bool:
	return app.player_hud != null and app.player_hud.is_active and app.rig.mode != app.rig.ISO


func first_city_bounds() -> Rect2i:
	var ids: Array = app.store.cities.keys()
	ids.sort()
	for id in ids:
		var bounds = app.store.cities[id].get("bounds")
		if bounds is Rect2i and bounds.size != Vector2i.ZERO:
			return bounds
	return Rect2i()


## Conta ao servidor o que a câmera observa: só assim o rastro de cognição do NPC é gravado.
func publish_observation(kind: String) -> void:
	match kind:
		"World":
			app.observer.set_scope("World")
		"City":
			app.observer.set_scope("City", String(app.store.city.get("id", "")))
		"Interior":
			var city_id := String(app.store.city.get("id", ""))
			if city_id != "":
				app.observer.set_scope("Building", city_id, str(interior_building))


## Retângulo que cobre os prédios da cidade (ou os limites dela, se ainda não há prédios).
func built_area() -> Rect2i:
	var area := Rect2i()
	var first := true
	for id in app.store.buildings:
		var building: Dictionary = app.store.buildings[id]
		for entry in building.footprint:
			var cell: Vector2i = building.cell + entry.cell
			area = Rect2i(cell, Vector2i.ONE) if first else area.expand(cell).expand(cell + Vector2i.ONE)
			first = false
	return app.store.city.bounds if first else area


static func center_of(bounds: Rect2i) -> Vector3:
	return Vector3(bounds.position.x + bounds.size.x / 2.0, 0, bounds.position.y + bounds.size.y / 2.0)


func on_connection(state: String, retry_in: float) -> void:
	app.banner.visible = state == "offline"
	if state == "offline":
		app.banner.text = "sem conexão — nova tentativa em %d s" % int(retry_in)
		app.loading.visible = not first_snapshot
		if not first_snapshot:
			app.loading.set_message("O mundo não está respondendo. Nova tentativa em %d s…" % int(retry_in))


func enter_building(building_id: int) -> void:
	if app.stage.active or gone_buildings.has(building_id):
		return
	previous_scope = world_ref.duplicate()
	crumb_before = app.breadcrumb.text
	interior_building = building_id
	await app.stream.subscribe("Interior", str(building_id))


## #237: o servidor respondeu 404 ao escopo (cidade/prédio que não existe): sem faixa de contrato; o foco da casa
## acaba e a vista volta ao escopo anterior (ou ao mundo), sem ficar em "Conectando…".
func on_scope_gone(kind: String, ref_id: String) -> void:
	if kind == "Interior":
		if ref_id.is_valid_int():
			gone_buildings[int(ref_id)] = true # a pose do motor não faz o app pedir de novo a mesma casa a cada passo
		leave_focus()
		app._inside_by_body = -1
	if kind == "World":
		return
	var back: Array = previous_scope if kind == "Interior" and previous_scope != [kind, ref_id] else ["World", ""]
	if back[0] == "Interior":
		back = ["World", ""]
	world_ref = back
	await app.stream.subscribe(back[0], back[1])


func _enter_interior_view() -> void:
	var marker = app.store.buildings.get(interior_building, null)
	var origin: Vector2i = marker.cell if marker != null else Vector2i.ZERO
	app.stage.interior_view.catalog = app.catalog
	app.stage.interior_view.figure_context = app.world_view.npcs.figure_context() # #247: a mesma pessoa, a mesma aparência da rua
	var driven: bool = app.player_hud != null and app.player_hud.is_active
	app.stage.interior_view.immersive = driven # #227: com o corpo do jogador na casa, a câmera dele fica dentro (sem corte de maquete)
	var lodging = marker.get("lodging") if marker != null else null
	app.stage.enter(app.store.interior, origin, app.labels, app.rig.camera, int(marker.type) if marker != null else -1,
		int(lodging.units) if lodging is Dictionary else 0)
	app.stage.interior_view.apply_names(app.store.names)
	app.registry.objects = {}
	var objects = app.store.interior.get("objects")
	if objects is Dictionary:
		for descriptor in objects.get("catalog", []):
			app.registry.objects[String(descriptor.id)] = descriptor
	var title: String = app.registry.title_of(Registry.ref(Registry.BUILDING, interior_building))
	app.breadcrumb.text = "%s › %s" % [crumb_before, title]
	if driven and app.player_hud.fine != Vector2.INF and app.player_hud.floor_level == 0 \
			and not app.stage.interior_view.covers(Vector2i(floori(app.player_hud.fine.x), floori(app.player_hud.fine.y)), 0):
		# #237: a pose diz "dentro" mas a célula do corpo não é da planta (posto de trabalho no campo): o corpo está na rua, não há casa
		# para desenhar em volta dele. O motor já não diz isso; é só a rede de segurança (a casa não vira palco sem fim com a casa longe)
		app.player_hud.warning.emit("O corpo está fora da casa: o foco da casa foi desfeito.")
		var shown := interior_building
		leave_focus()
		app._inside_by_body = shown # tratado: a próxima pose igual não pede a casa de novo
		return
	var world_own = app.world_view.npc_nodes.get(app.player_hud.npc_id) if driven else null
	if world_own != null and not app.stage.interior_view.occupant_nodes.has(app.player_hud.npc_id):
		app.stage.interior_view.guide_body(app.player_hud.npc_id, world_own, app.player_hud.floor_level)
		app.stage.set_floor(app.player_hud.floor_level) # o corpo entra no andar em que o motor diz que ele está
		var body_cell := Vector2i(floori(app.player_hud.fine.x), floori(app.player_hud.fine.y))
		if app.player_hud.fine != Vector2.INF and app.stage.interior_view.is_wall_cell(body_cell, app.player_hud.floor_level):
			# #227: o motor pôs o corpo numa célula de parede (o NPC estava "em casa" sem lugar na planta): ele veda todo passo daí
			app.player_hud.warning.emit("O motor pôs este corpo dentro de uma parede da casa e não deixa andar: solte-o (Esc → Sair) e escolha outra pessoa.")
	if driven:
		app._keep_the_body() # #237: a câmera já segue o corpo da casa neste quadro (o do mundo fica parado enquanto a casa está em foco)
	if driven: # #227: o jogador entra no foco com a câmera dele (3ª ou 1ª pessoa), não com a do observador; troca sem corte seco
		app.rig.begin_blend()
	elif marker != null and not app.director.active():
		var middle: Vector3 = app.stage.interior_view.center() # #227: a câmera foca a casa onde ela está, na escala do mundo
		app.rig.begin_blend()
		app.rig.focus(middle)
		app.rig.set_zoom(maxf(7.0, app.stage.interior_view.span_cells() * 1.4 + 3.0))


func exit_building() -> void:
	if not app.stage.active and interior_building < 0:
		return
	leave_focus()
	await app.stream.subscribe(previous_scope[0], previous_scope[1])


## #237: desmonta o foco da casa por inteiro e sem esperar a rede: a casa some do desenho, o mundo volta (visível, processando), o corpo
## do jogador passa para o desenho do mundo no lugar em que estava e a câmera o segue neste mesmo quadro (sem corte seco).
func leave_focus() -> void:
	var driven: bool = app.player_hud != null and app.player_hud.is_active
	var inside = app.stage.interior_view.occupant_nodes.get(app.player_hud.npc_id) if driven else null
	var at := Vector2.INF
	if inside != null and is_instance_valid(inside):
		at = Vector2(inside.position.x, inside.position.z) / Grid.CELL
	app.rig.begin_blend() # #227: sai sem corte seco; a posse e a câmera do jogador ficam
	app.stage.leave()
	interior_building = -1
	app._inside_by_body = -1
	if driven:
		var body = app.world_view.npc_nodes.get(app.player_hud.npc_id)
		if body != null and at != Vector2.INF:
			body.set_fine(at) # o corpo do mundo esteve parado durante a casa: vem para onde o jogador está
		app._keep_the_body()


func on_interior_changed() -> void:
	if app.stage.active:
		app.stage.interior_view.set_occupants(app.store.interior.occupants)
		app.stage.interior_view.apply_names(app.store.names)
