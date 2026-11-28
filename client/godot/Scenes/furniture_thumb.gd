extends RefCounted
## #264: a miniatura de um objeto do catálogo, desenhada por código: o próprio objeto (as partes do descritor, pelo mesmo ObjectFactory que a casa usa) renderizado
## pequeno num SubViewport com câmera isométrica e luz. Nenhuma imagem guardada: o que o cenário descreve é o que se vê no painel de compra.

const ObjectFactory := preload("res://Scenes/object_factory.gd")
const FurnitureModels := preload("res://Scenes/render/furniture_models.gd")

const LOOK_FROM := Vector3(1.0, 0.9, 1.0) ## de onde a câmera olha (a diagonal de cima, como a vista de comprar)


## A caixa que as partes do descritor ocupam (a mesma conta do desenho): serve para enquadrar a câmera.
static func bounds_of(descriptor: Dictionary) -> AABB:
	var lo := Vector3(INF, INF, INF)
	var hi := Vector3(-INF, -INF, -INF)
	for part in descriptor.get("shape", []):
		var size: Array = part.size
		var offset: Array = part.offset
		var half := Vector3.ZERO
		match String(part.shape):
			"cylinder":
				half = Vector3(float(size[0]), float(size[1]) / 2.0, float(size[0]))
			"sphere":
				half = Vector3(float(size[0]), float(size[0]), float(size[0]))
			"capsule":
				half = Vector3(float(size[0]), float(size[1]) / 2.0, float(size[0]))
			_:
				half = Vector3(float(size[0]), float(size[1]), float(size[2])) / 2.0
		var center := Vector3(float(offset[0]), float(offset[1]), float(offset[2]))
		lo = Vector3(minf(lo.x, center.x - half.x), minf(lo.y, center.y - half.y), minf(lo.z, center.z - half.z))
		hi = Vector3(maxf(hi.x, center.x + half.x), maxf(hi.y, center.y + half.y), maxf(hi.z, center.z + half.z))
	if lo.x == INF:
		return AABB(Vector3.ZERO, Vector3.ONE * 0.1)
	return AABB(lo, hi - lo)


## A miniatura: um TextureRect de `px` por `px` com o SubViewport dentro. `factory` é um ObjectFactory com o catálogo da casa.
static func make(factory, descriptor: Dictionary, px: int = 72, variation: int = 0) -> TextureRect:
	var rect := TextureRect.new()
	rect.custom_minimum_size = Vector2(px, px)
	rect.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	rect.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	rect.name = "thumb_" + String(descriptor.get("id", ""))
	var viewport := SubViewport.new()
	viewport.size = Vector2i(px * 2, px * 2) # o dobro: fica nítida na tela de alta densidade
	viewport.transparent_bg = true
	viewport.own_world_3d = true
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	viewport.msaa_3d = Viewport.MSAA_4X
	var node: Node3D = factory.build(String(descriptor.id), variation)
	if node == null:
		return rect
	viewport.add_child(node)
	var box := FurnitureModels.node_bounds(node) if FurnitureModels.supports(descriptor) else bounds_of(descriptor) # #266: o modelo novo enquadra pelas próprias partes
	var center := box.get_center()
	var extent := maxf(box.size.length(), 0.3)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = extent * 1.05
	viewport.add_child(camera)
	camera.position = center + LOOK_FROM.normalized() * (extent + 2.0)
	camera.look_at_from_position(camera.position, center, Vector3.UP)
	var light := DirectionalLight3D.new()
	light.rotation = Vector3(deg_to_rad(-48.0), deg_to_rad(35.0), 0.0)
	light.light_energy = 1.1
	viewport.add_child(light)
	var environment := Environment.new()
	environment.background_mode = Environment.BG_CLEAR_COLOR
	environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.ambient_light_color = Color(0.82, 0.84, 0.9)
	environment.ambient_light_energy = 0.75
	var world_environment := WorldEnvironment.new()
	world_environment.environment = environment
	viewport.add_child(world_environment)
	rect.add_child(viewport)
	rect.texture = viewport.get_texture()
	return rect
