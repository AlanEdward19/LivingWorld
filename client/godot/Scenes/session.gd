extends RefCounted
## Sessão e escopos: qual escopo está assinado (mundo, cidade, interior), o que o servidor acha que a câmera observa e
## a volta do interior para o escopo anterior. Prédio novo já vem com a planta no delta (AD-170) e o nome de cada pessoa
## vem no escopo: nenhuma leitura extra. Não desenha nada:
## manda a cena, a câmera e a interface do App reagirem.

const Store := preload("res://Store/world_store.gd")
const Registry := preload("res://Store/entity_registry.gd")
const Grid := preload("res://Scenes/grid.gd")
const FrameCost := preload("res://Contract/frame_cost.gd")

var app
var previous_scope := ["World", ""]
var world_ref := ["World", ""]
var interior_building := -1
var gone_buildings := {} ## #237: prédios que o servidor disse não existirem (404)
var first_snapshot := false
var crumb_before := "Mundo" ## trilha antes de entrar no prédio
var _view_before: Dictionary = {} ## enquadramento do observador antes de entrar na casa (volta ao sair)


func _init(p_app) -> void:
	app = p_app


func enter_scope(kind: String, ref_id: String) -> void:
	if app.stage.active or interior_building >= 0:
		leave_focus() # #237: pedir outro escopo (zoom out, outra cidade) nunca deixa a casa em foco para trás
	drop_interior() # #249: a casa assinada era da cidade de antes
	world_ref = [kind, ref_id]
	await app.stream.subscribe(kind, ref_id)


func on_snapshot(envelope: Dictionary) -> void:
	var kind := Store.scope_name(envelope.scope.kind)
	var drawn_before: String = app.store.kind
	var city_before := String(app.store.city.get("id", ""))
	var started := Time.get_ticks_usec()
	app.store.apply_snapshot(envelope) # (inclui o redesenho do mundo: `reloaded` → world_view)
	FrameCost.add("snapshot." + kind, started)
	if kind != "Interior":
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
			return # #237/#249: casa assinada com antecedência (o corpo chegou à porta) ou snapshot atrasado: guardada, não aberta
		if app.stage.active and app.stage.interior_view.floor_count == int(app.store.interior.floors):
			app.stage.sync_occupants(app.store.interior.occupants, app.store.names) # reenvio do snapshot pelo canal: só atualiza
		else:
			_enter_interior_view()
		return
	var after_started := Time.get_ticks_usec()
	_after_snapshot(kind, drawn_before, city_before)
	FrameCost.add("snapshot.after", after_started)


func _after_snapshot(kind: String, drawn_before: String, city_before: String) -> void:
	var same_scope: bool = drawn_before == kind and (kind == "World" or city_before == String(app.store.city.get("id", "")))
	if same_scope:
		# #249: o canal reenviou o snapshot do mesmo escopo (o servidor manda um ao abrir o canal, logo depois do da assinatura): a
		# diferença já foi aplicada no desenho; a câmera, a trilha e a lista ficam como estão e a casa em foco continua em foco
		app._load_names()
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
		if _driving():
			app.announce_area() # #246: voltou à cidade com o corpo: o nome do bairro, discreto
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
	if _warm_building == building_id:
		if int(app.store.interior.get("building", -1)) == building_id:
			_enter_interior_view() # #249: a casa já estava assinada e lida: aparece neste mesmo quadro
		return # (ainda a caminho: o snapshot a abre quando chegar)
	await _subscribe_interior(building_id)


## #249: a casa tem assinatura própria (`interior_stream`); a do mundo/cidade continua aberta, viva e desenhada.
func _subscribe_interior(building_id: int) -> void:
	_warm_building = building_id
	app.store.interior = {}
	await app.interior_stream.subscribe("Interior", str(building_id))


## Larga a assinatura da casa (o corpo se afastou, trocou de escopo ou o foco acabou sem corpo para perto dela).
func drop_interior() -> void:
	_warm_building = -1
	if app.interior_stream != null:
		app.interior_stream.close()
	app.store.interior = {}


const WARM_REACH := 3.0 ## células da planta do prédio em que o corpo já pede a casa (ela chega antes de a porta abrir)
const WARM_KEEP := 6.0 ## e só solta a casa quando o corpo passa disto (histerese: andar na porta não assina e desassina)
var _warm_building := -1 ## prédio cuja casa está assinada no `interior_stream` (aberta ou só pré-carregada)
var _warm_clock := 0.0


## #249: com o corpo do jogador na rua, assina antes a casa mais perto dele (a assinatura do interior chega enquanto ele anda até a porta,
## não depois de a pose dizer "dentro"). Roda do `_process` do app; 4 vezes por segundo.
func warm_interior_near_body(delta: float) -> void:
	_warm_clock += delta
	if _warm_clock < 0.25:
		return
	_warm_clock = 0.0
	var hud = app.player_hud
	if hud == null or not hud.is_active or hud.fine == Vector2.INF or interior_building >= 0 or app.stage.active \
			or app.store.kind != "City" or app._inside_by_body >= 0:
		return
	var near := nearest_building(hud.fine, WARM_REACH)
	if not near.is_empty() and int(near.id) != _warm_building and not gone_buildings.has(int(near.id)):
		_subscribe_interior(int(near.id)) # sem esperar: o snapshot entra na loja quando chegar
	elif _warm_building >= 0 and near.is_empty() and app.store.buildings.has(_warm_building):
		if building_distance(app.store.buildings[_warm_building], hud.fine) > WARM_KEEP:
			drop_interior()


## {id, distance} do prédio da cidade mais perto de `fine` (células; 0 = sobre a planta) até `limit`, ou {}.
func nearest_building(fine: Vector2, limit: float) -> Dictionary:
	var best := {}
	var best_distance := limit
	for id in app.store.buildings:
		var info: Dictionary = app.store.buildings[id]
		var distance := building_distance(info, fine, best_distance)
		if distance < best_distance:
			best_distance = distance
			best = {"id": int(id), "distance": distance}
	return best


## Distância (células) de `fine` à planta do prédio; `cutoff` abandona cedo os prédios distantes (devolve INF).
func building_distance(info: Dictionary, fine: Vector2, cutoff: float = INF) -> float:
	var at: Vector2i = info.cell
	if cutoff != INF and (absf(fine.x - at.x) > cutoff + 24.0 or absf(fine.y - at.y) > cutoff + 24.0):
		return INF
	var best := INF
	for entry in info.footprint:
		var cell: Vector2i = at + entry.cell
		var dx := maxf(maxf(cell.x - fine.x, fine.x - (cell.x + 1.0)), 0.0)
		var dy := maxf(maxf(cell.y - fine.y, fine.y - (cell.y + 1.0)), 0.0)
		best = minf(best, Vector2(dx, dy).length())
	return best


## #237: o servidor respondeu 404 ao escopo (cidade/prédio que não existe): sem faixa de contrato; o foco da casa
## acaba e a vista volta ao escopo anterior (ou ao mundo), sem ficar em "Conectando…".
func on_scope_gone(kind: String, ref_id: String) -> void:
	if kind == "Interior":
		if ref_id.is_valid_int():
			gone_buildings[int(ref_id)] = true # a pose do motor não faz o app pedir de novo a mesma casa a cada passo
		leave_focus()
		drop_interior()
		app._inside_by_body = -1
		return # #249: o mundo/cidade seguem assinados; só a casa some
	if kind == "World":
		return
	var back: Array = ["World", ""]
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
	var own = app.world_view.npc_nodes.get(app.player_hud.npc_id) if driven else null
	if own != null:
		# #249: o corpo é a MESMA figura da rua; a casa a adota (nenhum boneco novo, nenhuma troca) antes de montar o resto
		app.stage.interior_view.guide_body(app.player_hud.npc_id, own, app.player_hud.floor_level)
	app.stage.enter(app.store.interior, origin, app.labels, app.rig.camera, int(marker.type) if marker != null else -1,
		int(lodging.units) if lodging is Dictionary else 0, interior_building)
	app.stage.sync_occupants(app.store.interior.occupants, app.store.names)
	publish_observation("Interior")
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
	if own != null:
		app.stage.set_floor(app.player_hud.floor_level) # o corpo entra no andar em que o motor diz que ele está
		var body_cell := Vector2i(floori(app.player_hud.fine.x), floori(app.player_hud.fine.y))
		if app.player_hud.fine != Vector2.INF and app.stage.interior_view.is_wall_cell(body_cell, app.player_hud.floor_level):
			# #227: o motor pôs o corpo numa célula de parede (o NPC estava "em casa" sem lugar na planta): ele veda todo passo daí
			app.player_hud.warning.emit("O motor pôs este corpo dentro de uma parede da casa e não deixa andar: solte-o (Esc → Sair) e escolha outra pessoa.")
	if driven: # #227: o jogador entra no foco com a câmera dele (3ª ou 1ª pessoa), não com a do observador; segue a mesma figura, sem corte seco
		app.rig.begin_blend()
		app._keep_the_body()
	elif marker != null and not app.director.active():
		var middle: Vector3 = app.stage.interior_view.center() # #227: a câmera foca a casa onde ela está, na escala do mundo
		_view_before = {"target": app.rig.target, "zoom": app.rig.zoom_size} # #249: sair da casa devolve o enquadramento (a cidade não é mais recarregada para isso)
		app.rig.begin_blend()
		app.rig.focus(middle)
		app.rig.set_zoom(maxf(7.0, app.stage.interior_view.span_cells() * 1.4 + 3.0))


func exit_building() -> void:
	if not app.stage.active and interior_building < 0:
		return
	leave_focus()
	if app.player_hud == null or not app.player_hud.is_active:
		drop_interior() # observador: sem corpo perto da porta, a casa não fica assinada
		if not _view_before.is_empty() and not app.director.active():
			app.rig.begin_blend()
			app.rig.focus(_view_before.target) # o observador volta ao enquadramento de antes de entrar
			app.rig.set_zoom(float(_view_before.zoom))
	_view_before = {}


## #237/#249: desmonta o foco da casa por inteiro e sem esperar a rede: a casa some do desenho e o prédio volta. A cidade nunca saiu
## (continuou carregada e viva) e o corpo do jogador é a mesma figura que sempre esteve nela: não há o que reconstruir nem transferir.
func leave_focus() -> void:
	var driven: bool = app.player_hud != null and app.player_hud.is_active
	app.rig.begin_blend() # #227: sai sem corte seco; a posse e a câmera do jogador ficam
	var was_in_focus: bool = app.stage.active
	app.stage.leave()
	interior_building = -1
	app._inside_by_body = -1
	if was_in_focus:
		app.breadcrumb.text = crumb_before # a trilha volta: a cidade não é reaberta, ninguém mais a reescreve
	if driven:
		app._keep_the_body()


func on_interior_changed() -> void:
	if app.stage.active:
		app.stage.sync_occupants(app.store.interior.occupants, app.store.names)
