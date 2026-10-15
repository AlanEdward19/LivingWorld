extends RefCounted
## Instancia objetos do catálogo do cenário (ADR-0034, #47 fatia 1): partes primitivas (box|cylinder|sphere|capsule) com
## simetria e variação. Mesh e material são compartilhados por (id, variação) — nunca um mesh por instância.
## Só apresentação: a forma vem do servidor; a variação (índice) também.

const SHADE_PER_VARIATION := 0.05 ## variação 0 = cor declarada, sem alteração

var _descriptors: Dictionary = {} ## id -> descritor
var _meshes: Dictionary = {} ## "id|variação|parte" -> Mesh
var _materials: Dictionary = {} ## "id|variação|parte" -> Material
var meshes_built := 0 ## quantos meshes distintos foram criados (teste de compartilhamento)


func _init(catalog: Array = []) -> void:
	for descriptor in catalog:
		_descriptors[String(descriptor.id)] = descriptor


func has(id: String) -> bool:
	return _descriptors.has(id)


## Nó do objeto com a base no chão da célula; null se o id não existe no catálogo.
## Uma parte só sem simetria vira o próprio MeshInstance3D (igual à caixa de antes).
func build(id: String, variation: int = 0) -> Node3D:
	if not _descriptors.has(id):
		return null
	var shape: Array = _descriptors[id].shape
	if shape.size() == 1 and String(shape[0].get("symmetry", "none")) == "none":
		return _instance(id, variation, 0, shape[0], _offset(shape[0], 1.0, 1.0))
	var root := Node3D.new()
	for index in shape.size():
		var part: Dictionary = shape[index]
		var symmetry := String(part.get("symmetry", "none"))
		for sx in ([1.0, -1.0] if "x" in symmetry else [1.0]):
			for sz in ([1.0, -1.0] if "z" in symmetry else [1.0]):
				root.add_child(_instance(id, variation, index, part, _offset(part, sx, sz)))
	return root


func _offset(part: Dictionary, sx: float, sz: float) -> Vector3:
	var offset: Array = part.offset
	return Vector3(float(offset[0]) * sx, float(offset[1]), float(offset[2]) * sz)


func _instance(id: String, variation: int, index: int, part: Dictionary, position: Vector3) -> MeshInstance3D:
	var key := "%s|%d|%d" % [id, variation, index]
	if not _meshes.has(key):
		_meshes[key] = _mesh_for(part)
		_materials[key] = _material_for(part, variation)
		meshes_built += 1
	var node := MeshInstance3D.new()
	node.name = id
	node.mesh = _meshes[key]
	node.material_override = _materials[key]
	node.position = position
	return node


static func _mesh_for(part: Dictionary) -> Mesh:
	var size: Array = part.size
	match String(part.shape):
		"cylinder":
			var cylinder := CylinderMesh.new()
			cylinder.top_radius = float(size[0])
			cylinder.bottom_radius = float(size[0])
			cylinder.height = float(size[1])
			return cylinder
		"sphere":
			var sphere := SphereMesh.new()
			sphere.radius = float(size[0])
			sphere.height = float(size[0]) * 2.0
			return sphere
		"capsule":
			var capsule := CapsuleMesh.new()
			capsule.radius = float(size[0])
			capsule.height = float(size[1])
			return capsule
	var box := BoxMesh.new()
	box.size = Vector3(float(size[0]), float(size[1]), float(size[2]))
	return box


static func _material_for(part: Dictionary, variation: int) -> Material:
	var color := Color(String(part.color))
	if variation > 0:
		color = color.lightened(SHADE_PER_VARIATION * variation)
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.9
	return material
