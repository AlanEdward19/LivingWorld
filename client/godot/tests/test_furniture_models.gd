extends "res://tests/test_base.gd"
## #266: os móveis são modelos procedurais do catálogo (pegada em células, medidas em metros, cores por material): cada modelo cabe na pegada que o motor deu, em qualquer giro,
## com o fundo junto à parede e fora dela; as malhas são compartilhadas; a miniatura do modo comprar usa o mesmo modelo; o corpo deita no meio da pegada.

const Grid := preload("res://Scenes/grid.gd")
const ObjectFactory := preload("res://Scenes/object_factory.gd")
const FurnitureModels := preload("res://Scenes/render/furniture_models.gd")
const FurnitureFootprint := preload("res://Scenes/furniture_footprint.gd")
const FurnitureThumb := preload("res://Scenes/furniture_thumb.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const AssetCatalog := preload("res://Scenes/asset_catalog.gd")
const ActionLabels := preload("res://Store/action_labels.gd")

const WALL_HALF := 0.11 ## meia espessura da parede em unidades (wall_panels.gd)
const EPS := 0.004


## Os descritores reais do cenário (home-life e home-build), com modelo.
func _catalog() -> Array:
	var out: Array = []
	for name in ["home-life", "home-build"]:
		var file := FileAccess.open("res://../../scenarios/catalog/%s.json" % name, FileAccess.READ)
		if file == null:
			failures.append("catálogo %s não encontrado" % name)
			continue
		var parsed: Dictionary = JSON.parse_string(file.get_as_text())
		for descriptor in parsed.Objects:
			if descriptor.has("model"):
				out.append(descriptor)
	return out


func _bounds_in_world(descriptor: Dictionary, origin: Vector2i, facing: int) -> AABB:
	var factory := ObjectFactory.new([descriptor])
	var node: Node3D = factory.build(String(descriptor.id), 0)
	var center := FurnitureFootprint.center_offset(FurnitureFootprint.size_of(descriptor), facing)
	node.position = Grid.to_world(origin) + Vector3(center.x, 0.0, center.y)
	node.rotation.y = deg_to_rad(float(facing))
	tree.root.add_child(node)
	var local := FurnitureModels.node_bounds(node)
	var out := AABB()
	var first := true
	for i in 8:
		var corner := node.transform * (local.position + local.size * Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1))
		out = AABB(corner, Vector3.ZERO) if first else out.expand(corner)
		first = false
	node.queue_free()
	return out


func test_the_catalog_gives_every_piece_a_model_and_a_footprint_the_client_can_draw() -> void:
	var catalog := _catalog()
	check(catalog.size() >= 16, "a mobília inteira tem modelo: %d" % catalog.size())
	for descriptor in catalog:
		check(FurnitureModels.supports(descriptor), "%s: o gerador do tipo existe" % descriptor.id)
	var sizes := {}
	for descriptor in catalog:
		sizes[String(descriptor.id)] = FurnitureFootprint.size_of(descriptor)
	eq(sizes.bed, Vector2i(1, 2), "cama de casal 1×2")
	eq(sizes.sofa, Vector2i(2, 1), "sofá 2×1")
	eq(sizes.fridge, Vector2i(1, 1), "geladeira 1×1")


func test_no_model_leaves_its_footprint_or_enters_the_wall_in_any_rotation() -> void:
	for descriptor in _catalog():
		var size := FurnitureFootprint.size_of(descriptor)
		for facing in [0, 90, 180, 270]:
			var origin := Vector2i(20, 30)
			var box := _bounds_in_world(descriptor, origin, facing)
			var cells := FurnitureFootprint.cells(origin, size, facing)
			var lo := Vector2(INF, INF)
			var hi := Vector2(-INF, -INF)
			for cell in cells:
				lo = Vector2(minf(lo.x, cell.x), minf(lo.y, cell.y))
				hi = Vector2(maxf(hi.x, cell.x + 1.0), maxf(hi.y, cell.y + 1.0))
			var where := "%s giro %d" % [descriptor.id, facing]
			check(box.position.x >= lo.x + WALL_HALF - EPS and box.end.x <= hi.x - WALL_HALF + EPS, "%s: sai da pegada em x (%s contra %s a %s)" % [where, str(box), str(lo), str(hi)])
			check(box.position.z >= lo.y + WALL_HALF - EPS and box.end.z <= hi.y - WALL_HALF + EPS, "%s: sai da pegada em z (%s contra %s a %s)" % [where, str(box), str(lo), str(hi)])
			check(box.position.y >= -EPS and box.end.y <= Grid.units(3.0) * 0.95, "%s: do chão ao teto" % where)


func test_a_model_aligned_to_the_back_sits_against_the_wall_with_its_back_turned_to_it() -> void:
	for descriptor in _catalog():
		if String(descriptor.model.get("align", "back")) != "back":
			continue
		var size := FurnitureFootprint.size_of(descriptor)
		for facing in [0, 90, 180, 270]:
			var origin := Vector2i(5, 7)
			var box := _bounds_in_world(descriptor, origin, facing)
			# o fundo do móvel é a face voltada para a parede: a aresta de trás da pegada, recuada a meia parede
			var back := FurnitureFootprint.cells(origin, Vector2i(size.x, 1), facing)
			var gap := INF
			for cell in back:
				var toward := FurnitureFootprint.turn(0.0, -1.0, facing)
				var wall_line := (Vector2(cell) + Vector2(0.5, 0.5) + toward * 0.5)
				var inner := wall_line - toward * WALL_HALF # a face de dentro da parede
				var edge := Vector2.ZERO
				match facing:
					0: edge.y = box.position.z
					90: edge.x = box.position.x
					180: edge.y = box.end.z
					270: edge.x = box.end.x
				gap = minf(gap, absf((edge.y - inner.y) if facing in [0, 180] else (edge.x - inner.x)))
			check(gap <= 0.04, "%s giro %d: o fundo fica a %.3f da parede" % [descriptor.id, facing, gap])


func test_models_are_rich_parts_and_share_meshes_and_materials_between_instances() -> void:
	var by_id := {}
	for descriptor in _catalog():
		by_id[String(descriptor.id)] = descriptor
	var factory := ObjectFactory.new(by_id.values())
	var minimum := {"bed": 14, "sofa": 12, "fridge": 8, "stove": 14, "shower": 12, "toilet": 6, "tv": 8, "plant": 10, "bookshelf": 30, "chair": 8, "table": 6}
	for id in minimum:
		var node: Node3D = factory.build(id, 0)
		var count := node.find_children("*", "MeshInstance3D", true, false).size()
		check(count >= int(minimum[id]), "%s tem %d partes (esperado ao menos %d)" % [id, count, int(minimum[id])])
		node.free()
	var built := FurnitureModels.meshes_built
	var materials: Dictionary = FurnitureModels._materials
	var materials_before := materials.size()
	for i in 5:
		for id in by_id:
			factory.build(id, 0).free()
	eq(FurnitureModels.meshes_built, built, "construir de novo não cria malha nenhuma: o cache é por forma")
	eq(materials.size(), materials_before, "nem material: o cache é por tipo e cor")
	var a: Node3D = factory.build("bed", 0)
	var b: Node3D = factory.build("bed", 0)
	var first: MeshInstance3D = a.find_children("*", "MeshInstance3D", true, false)[0]
	var second: MeshInstance3D = b.find_children("*", "MeshInstance3D", true, false)[0]
	check(first.mesh == second.mesh and first.material_override == second.material_override, "duas camas dividem malha e material")
	a.free()
	b.free()


func test_the_far_view_drops_the_small_details_and_keeps_the_body() -> void:
	var factory := ObjectFactory.new(_catalog())
	var shelf: Node3D = factory.build("bookshelf", 0)
	var far := 0
	var always := 0
	for found in shelf.find_children("*", "MeshInstance3D", true, false):
		if (found as MeshInstance3D).visibility_range_end > 0.0:
			far += 1
		else:
			always += 1
	check(far > 20 and always >= 6, "livros só de perto (%d), a estante sempre (%d)" % [far, always])
	shelf.free()


func test_the_chamfered_box_has_outward_faces_and_cut_corners() -> void:
	var mesh := FurnitureModels.chamfer_mesh(Vector3(1.0, 0.6, 0.4), 0.05)
	var arrays := mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	eq(vertices.size(), 3 * (12 + 24 + 8), "6 faces, 12 chanfros e 8 cantos")
	var outward := 0
	for i in vertices.size():
		if normals[i].dot(vertices[i]) > 0.0:
			outward += 1
	eq(outward, vertices.size(), "toda normal aponta para fora")
	var box := mesh.get_aabb()
	check(absf(box.size.x - 1.0) < 0.001 and absf(box.size.y - 0.6) < 0.001, "mantém a medida: %s" % str(box.size))
	var corner_cut := false
	for vertex in vertices:
		if absf(vertex.x) > 0.499 and absf(vertex.y) > 0.299 and absf(vertex.z) > 0.199:
			corner_cut = true
	check(not corner_cut, "nenhum vértice fica no canto agudo da caixa")


func test_the_buy_mode_thumbnail_is_the_new_model_framed_by_its_own_parts() -> void:
	var by_id := {}
	for descriptor in _catalog():
		by_id[String(descriptor.id)] = descriptor
	var factory := ObjectFactory.new(by_id.values())
	var thumb := FurnitureThumb.make(factory, by_id.sofa, 64)
	tree.root.add_child(thumb)
	var viewport: SubViewport = thumb.get_child(0)
	var body := viewport.find_child("Body", true, false)
	check(body != null, "a miniatura carrega o modelo procedural (Body), não a caixa de antes")
	check(viewport.find_children("*", "MeshInstance3D", true, false).size() >= 12, "com as partes do sofá")
	thumb.queue_free()


func _view(furniture: Array, occupants: Array, catalog: Array) -> Node3D:
	var cells: Array = []
	for y in 5:
		for x in 5:
			var material := 3
			if x == 0 or y == 0 or x == 4 or y == 4:
				material = 2 if (x == 2 and y == 4) else 1
			cells.append({"cell": {"x": x, "y": y}, "material": material})
	var info := {"building": 1, "floors": 1, "layout": [{"level": 0, "cells": cells}], "occupants": occupants, "objects": {"format": 1, "catalog": catalog, "furniture": furniture}}
	var view := InteriorView.new()
	view.catalog = AssetCatalog.load_default()
	tree.root.add_child(view)
	view.show_interior(info, Vector2i(10, 10), ActionLabels.new(), 3)
	return view


func test_the_house_draws_the_two_cell_bed_centered_on_its_footprint_and_the_sleeper_lies_in_the_middle() -> void:
	var by_id := {}
	for descriptor in _catalog():
		by_id[String(descriptor.id)] = descriptor
	var bed: Dictionary = by_id.bed.duplicate(true)
	bed["usePoints"] = [{"name": "lie", "offset": [0.0, 0.05, 0.0], "facing": 0.0, "action": 1, "pose": "sleep"}]
	var placed := {"id": "bed", "variation": 0, "floor": 0, "cell": {"x": 1, "y": 1}, "facing": 90}
	var sleeper := {"npc": {"value": 7}, "floor": {"value": 0}, "localCell": {"x": 1, "y": 1}, "action": 1, "usingObject": "bed"}
	var view := _view([placed], [sleeper], [bed])
	var node: Node3D = view.furniture.get_child(0)
	# origem (1,1) no giro 90: a pegada 1×2 vai para o leste, então o centro fica meia célula a leste do centro da origem
	check(absf(node.position.x - 12.0) < 0.01 and absf(node.position.z - 11.5) < 0.01, "o modelo fica centrado na pegada: %s" % str(node.position))
	var actor: Node3D = view.occupant_nodes[7]
	check(absf(actor.position.x - 12.0) < 0.01 and absf(actor.position.z - 11.5) < 0.01, "o corpo deita no meio da cama: %s" % str(actor.position))
	var point: Dictionary = view.object_points[0]
	check(absf(point.position.x - 12.0) < 0.01, "o ponto de clique também fica no meio da pegada")
	view.free()
