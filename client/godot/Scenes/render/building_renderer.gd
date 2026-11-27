extends Node3D
## Prédios da cidade (paredes, telhado, janelas, porta, poste, chaminé). Um nó por prédio; delta de prédio novo ou
## movido redesenha só aquele (sem recarregar a cidade). Lê a WorldStore; nunca fala com o servidor.

const Grid := preload("res://Scenes/grid.gd")
const Scenery := preload("res://Scenes/scenery.gd")
const WallPanels := preload("res://Scenes/wall_panels.gd")
const Dress := preload("res://Scenes/render/building_dress.gd")
const Works := preload("res://Store/work_book.gd")
const Site := preload("res://Scenes/render/work_site.gd")

const MATERIAL_WALL_STONE := 0
const MATERIAL_WALL_WOOD := 1
const MATERIAL_DOOR := 2
const PLACEHOLDER_FOOTPRINT := [Vector2i(0, 0), Vector2i(1, 0), Vector2i(0, 1), Vector2i(1, 1)]

var nodes: Dictionary = {} ## id -> Node3D
var dressed: Dictionary = {} ## id -> assinatura do estado desenhado (estilo, placa, fechado): só redesenha se mudar
var work_of: Callable = func(_id: int) -> Dictionary: return {} ## AD-193: obra do canteiro (o world_view liga à store)


## #249: aplica a diferença: o prédio que continua igual (mesma planta e mesmo estado desenhado) fica na mesma instância. O que falta
## desenhar entra por fatias de ~`SLICE_MS` por quadro, do mais perto de `center` para o mais longe: trocar de escopo não custa um quadro
## de centenas de ms numa cidade grande (a cidade aparece em poucos quadros, o que importa primeiro).
const SLICE_MS := 8.0
var _pending: Array = []
var _store


func rebuild(store, center: Vector3 = Vector3.INF) -> void:
	_store = store
	_pending.clear()
	if store.kind != "City":
		for node in nodes.values():
			_dispose(node)
		nodes.clear()
		_info_signature.clear()
		return
	for id in nodes.keys():
		if not store.buildings.has(id):
			remove(id)
	for id in store.buildings:
		if _needs_drawing(store, id):
			_pending.append(id)
		else:
			set_lit(int(id), bool(store.building_states.get(id, {}).get("lit", false)))
	if center != Vector3.INF:
		_pending.sort_custom(func(a, b) -> bool:
			return Vector2(store.buildings[a].cell).distance_squared_to(Vector2(center.x, center.z)) < Vector2(store.buildings[b].cell).distance_squared_to(Vector2(center.x, center.z)))
	step(SLICE_MS * 1.5)


func _needs_drawing(store, id: Variant) -> bool:
	var info: Dictionary = store.buildings[id]
	var state: Dictionary = store.building_states.get(id, {})
	return not (nodes.has(id) and _info_signature.get(id, -1) == info.hash() \
		and String(dressed.get(id, "")) == signature(state, work_of.call(int(id))))


## Desenha da fila até `budget_ms`; o resto fica para os próximos quadros.
func step(budget_ms: float = SLICE_MS) -> void:
	if _pending.is_empty() or _store == null:
		return
	var started := Time.get_ticks_usec()
	while not _pending.is_empty():
		var id = _pending.pop_front()
		if _store.buildings.has(id) and _needs_drawing(_store, id):
			upsert(_store.buildings[id], _store.building_states.get(id, {}))
		if (Time.get_ticks_usec() - started) / 1000.0 >= budget_ms:
			break


func _process(_delta: float) -> void:
	step()
	_reap()


func pending_count() -> int:
	return _pending.size()


var _info_signature: Dictionary = {} ## id -> hash da planta desenhada (a diferença do `rebuild`)


## Desenha (ou redesenha) um prédio. Sem planta (veio por delta e o snapshot ainda não chegou), usa um canteiro
## de obra 2x2 de madeira no lugar certo até a planta chegar.
static func signature(state: Dictionary, work: Dictionary = {}) -> String:
	if not work.is_empty(): # AD-193: canteiro — muda de forma com o décimo da obra e com o material no lote
		return "site|%d|%d" % [Works.stage(work), int(round(Works.pile_fraction(work) * 4.0))]
	return "%s|%s|%s" % [Dress.style_of(state), Dress.sign_text(state), bool(state.get("closed", false))]


## AD-193: a obra andou (fração, turma, material): redesenha o canteiro só quando a forma desenhada muda.
func rework(info: Dictionary, state: Dictionary) -> void:
	var work: Dictionary = work_of.call(int(info.id))
	if String(dressed.get(int(info.id), "")) != signature(state, work):
		upsert(info, state)


## Estado novo do prédio (função, placa, fechado) só redesenha quando a aparência muda (dono e estoque não mudam a forma);
## a luz (AD-189, `lit` do motor) só troca o material das janelas.
func restyle(info: Dictionary, state: Dictionary) -> void:
	if not work_of.call(int(info.id)).is_empty():
		return # canteiro: o estado de prédio (função, luz) não muda a obra
	if String(dressed.get(int(info.id), "")) != signature(state):
		upsert(info, state)
	else:
		set_lit(int(info.id), bool(state.get("lit", false)))


## AD-189: acende ou apaga as janelas do prédio pelo dado do motor, sem redesenhar.
func set_lit(id: int, lit: bool) -> void:
	var node: Node3D = nodes.get(id)
	if node == null or bool(node.get_meta("lit", false)) == lit:
		return
	node.set_meta("lit", lit)
	for window in node.get_meta("windows", []):
		window.material_override = window_material if lit else dark_window_material
		window.name = "Window" if lit else "WindowDark"


## Quantas janelas acesas o prédio tem agora (testável).
func lit_count(id: int) -> int:
	var node: Node3D = nodes.get(id)
	return 0 if node == null else (node.get_meta("windows", []) as Array).filter(func(w): return w.material_override == window_material).size()


func upsert(info: Dictionary, state: Dictionary = {}) -> Node3D:
	var was_faded := faded.has(int(info.id)) # AD-197s: redesenhar (a pessoa nova mudou o estado do prédio) não pode apagar a translucidez
	remove(int(info.id))
	if was_faded:
		faded[int(info.id)] = true
	var node := _draw(info, state)
	if xray or was_faded or opened.has(int(info.id)):
		_fade(int(info.id), FADE if was_faded else 0.0)
	return node


func _draw(info: Dictionary, state: Dictionary) -> Node3D:
	var work: Dictionary = work_of.call(int(info.id))
	dressed[int(info.id)] = signature(state, work)
	_info_signature[int(info.id)] = info.hash()
	if not work.is_empty() and not info.footprint.is_empty():
		var site := Site.make(info, work, Grid.floor_height())
		add_child(site)
		nodes[int(info.id)] = site
		return site
	var drawn := info
	if info.footprint.is_empty():
		drawn = info.duplicate()
		drawn.footprint = []
		for cell in PLACEHOLDER_FOOTPRINT:
			drawn.footprint.append({"cell": cell, "material": MATERIAL_DOOR if cell == Vector2i.ZERO else MATERIAL_WALL_WOOD})
	var node := make_building(drawn, state)
	add_child(node)
	nodes[int(info.id)] = node
	return node


# --- prédio que cobre fica translúcido (pedido do dono, AD-171 item 14) ---------------------------------------

const FADE := 0.72 ## `GeometryInstance3D.transparency` por instância: sem material novo nem malha duplicada
const XRAY := 0.88 ## raio-x (AD-177): paredes de todos os prédios translúcidas
const XRAY_ROOF := 0.96 ## e o telhado quase some
const ROOF_EAVE_M := 0.3 ## beiral do telhado de cada lado (metros)
const ROOF_OPEN := 0.97 ## AD-197s: o telhado do prédio em que o jogador está (3ª pessoa de cima) quase some
var faded: Dictionary = {} ## id -> true (translúcidos agora)
var opened: Dictionary = {} ## AD-197s: id -> true (o corpo do jogador está dentro: telhado aberto)
var xray := false


## AD-197s: prédios em que o corpo do jogador está: o telhado some para a câmera olhar de cima.
func set_opened(ids: Dictionary) -> void:
	var changed := {}
	for id in opened:
		if not ids.has(id):
			changed[id] = true
	for id in ids:
		if not opened.has(id):
			changed[id] = true
	opened = ids.duplicate()
	for id in changed:
		_fade(id, FADE if faded.has(id) else 0.0)


## Raio-x: todos os prédios translúcidos (telhado quase sumido) para ver ruas, lotes e quem anda entre as casas.
func set_xray(on: bool) -> void:
	if on == xray:
		return
	xray = on
	for id in nodes:
		_fade(id, FADE if faded.has(id) else 0.0)


## Prédios entre a câmera (direção `view_dir`, ortográfica) e algum dos pontos. `points` = [[ponto, id de prédio a ignorar]]
## (o próprio prédio em foco não cobre a si mesmo).
func occluders(points: Array, view_dir: Vector3) -> Dictionary:
	var out := {}
	for id in nodes:
		var box: AABB = nodes[id].get_meta("box", AABB())
		if box.size == Vector3.ZERO:
			continue
		box = box.grow(-0.04)
		for entry in points:
			var point: Vector3 = entry[0]
			if int(entry[1]) == int(id) or box.has_point(point):
				continue
			if box.intersects_segment(point - view_dir * 200.0, point - view_dir * 0.05):
				out[id] = true
				break
	return out


## Aplica a translucidez só no que mudou (volta ao normal ao sair).
func set_faded(ids: Dictionary) -> void:
	for id in faded.keys():
		if not ids.has(id):
			_fade(id, 0.0)
			faded.erase(id)
	for id in ids:
		if not faded.has(id) and nodes.has(id):
			_fade(id, FADE)
			faded[id] = true


## #227: com a câmera da 3ª pessoa e o corpo conhecidos, só as peças que cortam a linha entre eles ficam translúcidas (e mais, para
## quem olha através); o resto do prédio fica opaco, em vez de lajes pálidas inteiras. Sem segmento, o prédio inteiro (iso e raio-x).
const COVER_FADE := 0.9
const COVER_GROW := 0.12
var _cover: Array = [] ## [de, até] ou vazio


func set_cover_segment(from: Vector3, to: Vector3) -> void:
	if not _cover.is_empty() and (_cover[0] as Vector3).distance_to(from) < 0.02 and (_cover[1] as Vector3).distance_to(to) < 0.02:
		return
	_cover = [from, to]
	for id in faded:
		_fade(id, FADE)


func clear_cover_segment() -> void:
	if _cover.is_empty():
		return
	_cover = []
	for id in faded:
		_fade(id, FADE)


## A peça cobre a linha da câmera ao corpo (caixa dela, com folga, cruzada pelo segmento).
func covers(child: GeometryInstance3D) -> bool:
	if _cover.is_empty():
		return true
	var box: AABB = child.global_transform * child.get_aabb()
	return box.grow(COVER_GROW).intersects_segment(_cover[0], _cover[1]) != null or box.grow(COVER_GROW).has_point(_cover[0])


func _fade(id: int, amount: float) -> void:
	if not nodes.has(id):
		return
	for child in nodes[id].find_children("*", "GeometryInstance3D", true, false):
		var value := amount
		if amount > 0.0 and not _cover.is_empty():
			value = COVER_FADE if covers(child) else 0.0
		if xray:
			value = maxf(amount, XRAY_ROOF if child.name.begins_with("Roof") else XRAY)
		if opened.has(id) and child.name.begins_with("Roof"):
			value = maxf(value, ROOF_OPEN)
		if is_equal_approx((child as GeometryInstance3D).transparency, value):
			continue
		(child as GeometryInstance3D).transparency = value
		(child as GeometryInstance3D).cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF if value > 0.0 \
			else GeometryInstance3D.SHADOW_CASTING_SETTING_ON # aberto não faz sombra sobre a rua


## Translúcido agora: coberto pelo foco, empurrado pela alternância (Tab) ou raio-x ligado.
func is_faded(id: int) -> bool:
	return xray or faded.has(id)


## O mouse atravessa só o que cobre o foco ou foi empurrado (no raio-x o prédio continua clicável).
func passes_mouse(id: int) -> bool:
	return faded.has(id)


func remove(id: int) -> void:
	dressed.erase(id)
	_info_signature.erase(id)
	faded.erase(id)
	if nodes.has(id):
		_dispose(nodes[id])
		nodes.erase(id)


## #249: o prédio que sai some do desenho na hora, mas o nó só é liberado aos poucos (uma cidade inteira de uma vez custava um quadro).
const REAP_PER_FRAME := 6
var _dying: Array = []


func _dispose(node: Node) -> void:
	if node.is_inside_tree():
		if node is Node3D:
			(node as Node3D).visible = false
		_dying.append(node)
	else:
		node.free()


func _reap() -> void:
	var count := 0
	while not _dying.is_empty() and count < REAP_PER_FRAME:
		var node = _dying.pop_front()
		if is_instance_valid(node):
			node.queue_free()
		count += 1


const WALL_COLORS := {
	0: Color("a9a399"), # pedra
	1: Color("b88f62"), # madeira
}
const ROOF_COLORS: Array[Color] = [Color("a14f3d"), Color("6b5b73"), Color("7d6a45"), Color("8c4a32")]
const DIRS: Array[Vector2i] = [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]

var window_material: StandardMaterial3D = _make_window_material()
var dark_window_material: StandardMaterial3D = _flat_material(Color("2b3a4a")) ## janela sem ninguém dentro (AD-188)
var lamp_material: StandardMaterial3D = _make_lamp_material()


func _make_lamp_material() -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = Color("fff1c4")
	material.emission_enabled = true
	material.emission = Color("ffc860")
	material.emission_energy_multiplier = 0.0
	return material


func _make_window_material() -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = Color("2b3a4a")
	material.emission_enabled = true
	material.emission = Color("ffd27a")
	material.emission_energy_multiplier = 0.0
	material.roughness = 0.2
	return material



func make_building(info: Dictionary, state: Dictionary = {}) -> Node3D:
	var root := Node3D.new()
	root.name = "Building_%d" % info.id
	root.set_meta("building_id", info.id)
	var floors: int = info.floors
	var height: float = floors * Grid.floor_height()
	var cells := {}
	for entry in info.footprint:
		cells[info.cell + entry.cell] = entry.material
	var form := Dress.body_of(state) # AD-188: corpo pela função; AD-189: a luz das janelas é o `lit` do motor
	var roof_color: Color = ROOF_COLORS[info.type % ROOF_COLORS.size()] if info.type >= 0 else ROOF_COLORS[0]
	if form.roof != null:
		roof_color = Color(form.roof)
	var trim := _flat_material(Color("5d4a3a"))
	var door := _flat_material(Color("3b2a20"))
	var step := _flat_material(Color("8d8579"))
	var wall_materials := {}
	for key in WALL_COLORS:
		wall_materials[key] = _flat_material(WALL_COLORS[key] if form.wall == null else Color(WALL_COLORS[key]).lerp(Color(form.wall), 0.55))
	var windows: Array = []
	var min_cell := Vector2i(1 << 20, 1 << 20)
	var max_cell := Vector2i(-(1 << 20), -(1 << 20))
	var door_at := Vector3.INF
	var door_out := Vector3(0, 0, 1)
	var solid: Array = [] # #229/#226: painéis finos de parede (sem a porta): o que o corpo do jogador não atravessa
	var walls: Array = [] # AD-197s2: as caixas sólidas das células de parede (a câmera do jogador fica no cômodo, fora delas)
	for cell in cells:
		min_cell = Vector2i(mini(min_cell.x, cell.x), mini(min_cell.y, cell.y))
		max_cell = Vector2i(maxi(max_cell.x, cell.x), maxi(max_cell.y, cell.y))
		var material: int = cells[cell]
		match material:
			MATERIAL_WALL_STONE, MATERIAL_WALL_WOOD:
				walls.append(AABB(Vector3(cell.x, 0, cell.y), Vector3(1, height, 1)))
				_box(root, Vector3(1.0, height, 1.0), Grid.to_world(cell, height / 2.0), wall_materials[material])
				_box(root, Vector3(1.04, 0.12, 1.04), Grid.to_world(cell, 0.06), trim) # rodapé
				for level in range(1, floors):
					_box(root, Vector3(1.03, 0.07, 1.03), Grid.to_world(cell, level * Grid.floor_height()), trim) # friso entre andares
				for dir in DIRS:
					if cells.has(cell + dir):
						continue
					for level in floors:
						if Scenery.cell_hash(cell, level * 4 + DIRS.find(dir) + 11) < 0.5:
							var outward := Vector3(dir.x, 0, dir.y) * 0.505
							var pane := Grid.units(Grid.window_meters) # ADR-0052: janela e peitoril em metros do motor
							var window := _box(root, Vector3(pane if dir.x == 0 else 0.04, pane, pane if dir.y == 0 else 0.04),
								Grid.to_world(cell, level * Grid.floor_height() + Grid.units(Grid.window_sill_meters) + pane / 2.0) + outward, window_material)
							window.name = "Window"
							windows.append(window)
			MATERIAL_DOOR:
				walls.append(AABB(Vector3(cell.x, 0, cell.y), Vector3(1, height, 1)))
				_box(root, Vector3(1.0, height, 1.0), Grid.to_world(cell, height / 2.0), wall_materials[0])
				for dir in DIRS:
					if not cells.has(cell + dir):
						var outward := Vector3(dir.x, 0, dir.y)
						if door_at == Vector3.INF:
							door_at = Grid.to_world(cell) + outward * 0.5
							door_out = outward
						var door_w := Grid.units(Grid.door_width_meters) # ADR-0052: porta em metros do motor
						var door_h := Grid.units(Grid.door_meters)
						var size := Vector3(door_w if dir.x == 0 else 0.1, door_h, door_w if dir.y == 0 else 0.1)
						_box(root, size, Grid.to_world(cell, door_h / 2.0) + outward * 0.5, door).name = "Door"
						_box(root, Vector3(0.9 if dir.x == 0 else 0.3, 0.06, 0.9 if dir.y == 0 else 0.3),
							Grid.to_world(cell, 0.03) + outward * 0.78, step)
						_lamp_post(root, Grid.to_world(cell) + outward * 1.15 + Vector3(outward.z, 0, outward.x) * 0.5)
			_:
				pass
	for panel in WallPanels.panels(cells): # #226: o corpo colide com painel fino (contorno rente à borda de fora), não com a célula inteira
		var rect: Rect2 = panel.rect
		solid.append(AABB(Vector3(rect.position.x, 0, rect.position.y), Vector3(rect.size.x, height, rect.size.y)))
	var lit := Dress.lit_windows(state, windows.size())
	for i in range(lit, windows.size()):
		windows[i].material_override = dark_window_material
		windows[i].name = "WindowDark"
	for i in range(0, lit):
		windows[i].material_override = window_material
	root.set_meta("windows", windows)
	root.set_meta("lit", lit > 0)
	var span := max_cell - min_cell + Vector2i.ONE
	var rectangular: bool = cells.size() == span.x * span.y
	var roof := _flat_material(roof_color)
	if rectangular:
		var prism := PrismMesh.new()
		var along_x := span.x >= span.y
		var eave := Grid.units(ROOF_EAVE_M) # AD-197s3: beiral em metros do motor (a câmera do jogador usa a mesma casca)
		prism.size = Vector3((span.x if along_x else span.y) + eave * 2.0, float(form.pitch), (span.y if along_x else span.x) + eave * 2.0)
		var ridge := MeshInstance3D.new()
		ridge.mesh = prism
		ridge.material_override = roof
		ridge.name = "Roof"
		ridge.position = Vector3(min_cell.x + span.x / 2.0, height + float(form.pitch) / 2.0, min_cell.y + span.y / 2.0)
		if not along_x:
			ridge.rotation.y = PI / 2.0
		root.add_child(ridge)
	else:
		for cell in cells:
			var pyramid := CylinderMesh.new()
			pyramid.top_radius = 0.0
			pyramid.bottom_radius = 0.78
			pyramid.height = 0.62
			pyramid.radial_segments = 4
			var node := MeshInstance3D.new()
			node.mesh = pyramid
			node.material_override = roof
			node.rotation.y = PI / 4.0
			node.position = Grid.to_world(cell, height + 0.31)
			node.name = "Roof"
			root.add_child(node)
	if info.type == -1:
		var chimney_cell: Vector2i = min_cell
		_box(root, Vector3(Dress.u(0.22), Dress.u(0.7), Dress.u(0.22)), Grid.to_world(chimney_cell, height + Dress.u(0.55)) + Vector3(Dress.u(0.1), 0, Dress.u(0.1)), _flat_material(Color("7a6a5c")))
		root.add_child(_smoke(Grid.to_world(chimney_cell, height + Dress.u(0.95)) + Vector3(Dress.u(0.1), 0, Dress.u(0.1))))
	if not state.is_empty():
		var center := Vector3(min_cell.x + span.x / 2.0, height, min_cell.y + span.y / 2.0)
		if door_at == Vector3.INF:
			door_at = Vector3(center.x, 0, max_cell.y + 1.0)
		var dress_state := state
		if info.type == -1 and Dress.style_of(state) == "dwelling":
			dress_state = state.duplicate()
			dress_state.function = "" # a casa padrão já tem chaminé com fumaça
		Dress.dress(root, dress_state, Vector3(min_cell.x + 0.5, height, min_cell.y + 0.5), door_at, door_out, height)
	var body := StaticBody3D.new()
	body.set_meta("building_id", info.id)
	var shape := CollisionShape3D.new()
	var collider := BoxShape3D.new()
	collider.size = Vector3(span.x, height + 0.6, span.y)
	shape.shape = collider
	shape.position = Vector3(min_cell.x + span.x / 2.0, (height + 0.6) / 2.0, min_cell.y + span.y / 2.0)
	body.add_child(shape)
	root.add_child(body)
	root.set_meta("box", AABB(Vector3(min_cell.x, 0, min_cell.y), Vector3(span.x, height + Dress.u(0.9), span.y)))
	root.set_meta("walls", walls)
	root.set_meta("solid", solid)
	# AD-197s: a casca que a câmera não toca: o beiral do telhado (0,2 de cada lado) e a cumeeira (acima da caixa nos telhados altos)
	var roof_top: float = height + (float(form.pitch) if rectangular else 0.62)
	var eave_shell := Grid.units(ROOF_EAVE_M)
	root.set_meta("shell", AABB(Vector3(min_cell.x - eave_shell, 0, min_cell.y - eave_shell), Vector3(span.x + eave_shell * 2.0, maxf(height + Dress.u(0.9), roof_top + Dress.u(0.1)), span.y + eave_shell * 2.0)))
	return root


func _lamp_post(parent: Node3D, at: Vector3) -> void:
	var post := MeshInstance3D.new()
	var pole := CylinderMesh.new()
	pole.top_radius = 0.025
	pole.bottom_radius = 0.035
	pole.height = 0.85
	post.mesh = pole
	post.material_override = _flat_material(Color("3d3a36"))
	post.position = at + Vector3(0, 0.425, 0)
	post.name = "LampPost"
	parent.add_child(post)
	var bulb := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.1
	sphere.height = 0.2
	bulb.mesh = sphere
	bulb.material_override = lamp_material
	bulb.position = at + Vector3(0, 0.9, 0)
	bulb.name = "Lamp"
	parent.add_child(bulb)


func _box(parent: Node3D, size: Vector3, at: Vector3, material: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = size
	node.mesh = mesh
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node


func _smoke(at: Vector3) -> GPUParticles3D:
	var particles := GPUParticles3D.new()
	particles.name = "Smoke"
	particles.position = at
	particles.amount = 12
	particles.lifetime = 3.5
	particles.local_coords = false
	var process := ParticleProcessMaterial.new()
	process.direction = Vector3(0.3, 1, 0.1)
	process.spread = 12.0
	process.initial_velocity_min = 0.25
	process.initial_velocity_max = 0.45
	process.gravity = Vector3(0.12, 0.05, 0)
	process.scale_min = 0.5
	process.scale_max = 1.1
	var curve := Curve.new()
	curve.add_point(Vector2(0, 0.3))
	curve.add_point(Vector2(1, 1.6))
	var scale_texture := CurveTexture.new()
	scale_texture.curve = curve
	process.scale_curve = scale_texture
	var fade := Gradient.new()
	fade.set_color(0, Color(0.85, 0.85, 0.85, 0.55))
	fade.set_color(1, Color(0.85, 0.85, 0.85, 0.0))
	var ramp := GradientTexture1D.new()
	ramp.gradient = fade
	process.color_ramp = ramp
	particles.process_material = process
	var puff := SphereMesh.new()
	puff.radius = 0.12
	puff.height = 0.24
	puff.radial_segments = 8
	puff.rings = 4
	var puff_material := StandardMaterial3D.new()
	puff_material.vertex_color_use_as_albedo = true
	puff_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	puff_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	puff.material = puff_material
	particles.draw_pass_1 = puff
	return particles


func _flat_material(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.9
	return material
