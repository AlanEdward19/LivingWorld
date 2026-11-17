extends Node3D
## Regiões das cidades no mapa-múndi: chão translúcido, nome e população, e o corpo clicável (meta `city_id`).

var nodes: Dictionary = {} ## id -> Node3D


func rebuild(store) -> void:
	for node in nodes.values():
		if node.is_inside_tree():
			node.queue_free()
		else:
			node.free()
	nodes.clear()
	if store.kind != "World":
		return
	for id in store.cities:
		var info: Dictionary = store.cities[id]
		var root := Node3D.new()
		root.name = "City_%s" % str(id).left(8)
		root.set_meta("city_id", id)
		var bounds: Rect2i = info.bounds
		var plane := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(bounds.size.x, 0.06, bounds.size.y)
		plane.mesh = box
		var material := StandardMaterial3D.new()
		material.albedo_color = Color(1.0, 0.8, 0.3, 0.35)
		material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		plane.material_override = material
		plane.position = Vector3(bounds.position.x + bounds.size.x / 2.0, 0.03, bounds.position.y + bounds.size.y / 2.0)
		root.add_child(plane)
		var label := Label3D.new()
		label.text = "%s\n%d hab." % [info.name if info.name != "" else "Cidade", info.population]
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		label.pixel_size = 0.012
		label.font_size = 32
		label.no_depth_test = true
		label.position = Vector3(info.location.x + 0.5, 1.6, info.location.y + 0.5)
		root.add_child(label)
		var body := StaticBody3D.new()
		body.set_meta("city_id", id)
		var shape := CollisionShape3D.new()
		var collider := BoxShape3D.new()
		collider.size = Vector3(bounds.size.x, 1.0, bounds.size.y)
		shape.shape = collider
		shape.position = plane.position + Vector3(0, 0.5, 0)
		body.add_child(shape)
		root.add_child(body)
		root.position = Vector3.ZERO
		add_child(root)
		nodes[id] = root
