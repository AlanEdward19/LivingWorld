extends Node3D
## Fauna (AD-170): na cidade, cada animal desenhado pela espécie (quadrúpede grande, pequeno ou ave), cor e porte pela
## espécie, um pouco magro quando a energia é baixa; no mapa-múndi, cada bando agregado vira um grupinho de animais no
## centro com o número. Um MultiMesh por forma (malha única feita de caixas), refeito só quando a fauna muda.
## `positions` (animal) e `group_positions` (bando) servem à seleção. O motor não dá idade, porte nem atividade (lacuna).
## AD-188: entre um delta e o outro o animal desliza da célula de antes para a nova em GLIDE segundos (virado para onde
## anda), em vez de saltar; salto longo (mais de JUMP células: entrou no escopo, nasceu) vai direto. Só as instâncias de
## quem se move são reescritas por quadro.

const Grid := preload("res://Scenes/grid.gd")

## espécie -> [forma, cor, escala]. Fora da tabela: forma e cor pelo nome (estável).
const SPECIES := {
	"rabbit": ["small", Color("b8a48a"), 0.7], "coelho": ["small", Color("b8a48a"), 0.7],
	"wolf": ["quad", Color("7a7a80"), 0.85], "lobo": ["quad", Color("7a7a80"), 0.85],
	"fox": ["quad", Color("c86a2a"), 0.6], "raposa": ["quad", Color("c86a2a"), 0.6],
	"deer": ["quad", Color("9a6a3a"), 1.0], "cervo": ["quad", Color("9a6a3a"), 1.0],
	"sheep": ["quad", Color("e8e4da"), 0.8], "ovelha": ["quad", Color("e8e4da"), 0.8],
	"cow": ["quad", Color("6a4a3a"), 1.25], "vaca": ["quad", Color("6a4a3a"), 1.25],
	"pig": ["quad", Color("e0a0a0"), 0.75], "porco": ["quad", Color("e0a0a0"), 0.75],
	"boar": ["quad", Color("4a3a30"), 0.85], "javali": ["quad", Color("4a3a30"), 0.85],
	"horse": ["quad", Color("7a5030"), 1.3], "cavalo": ["quad", Color("7a5030"), 1.3],
	"goat": ["quad", Color("c8c0a8"), 0.7], "cabra": ["quad", Color("c8c0a8"), 0.7],
	"bear": ["quad", Color("4a3424"), 1.4], "urso": ["quad", Color("4a3424"), 1.4],
	"chicken": ["bird", Color("f0e8d8"), 0.8], "galinha": ["bird", Color("f0e8d8"), 0.8],
	"duck": ["bird", Color("c8b070"), 0.8], "pato": ["bird", Color("c8b070"), 0.8],
}
const FORMS := ["quad", "small", "bird"]
const GROUP_SHOWN := 5
const GLIDE := 0.9 ## segundos do deslize entre a célula de antes e a nova
const JUMP := 3.0 ## acima disso (em células) não desliza: aparece no lugar

var store
var bounds_filter := Rect2i()
var positions: Dictionary = {} ## id do animal -> Vector3
var group_positions: Dictionary = {} ## "espécie:região" -> Vector3
var _multis: Dictionary = {} ## forma -> MultiMeshInstance3D
var drawn: Dictionary = {} ## id do animal -> posição desenhada agora (testável)
var moving: Dictionary = {} ## id -> {from, to, t}: quem está deslizando
var _slots: Dictionary = {} ## id -> [forma, índice na MultiMesh, Basis]
var _yaw: Dictionary = {} ## id -> rumo do último deslize (o animal fica virado para onde andou)
var _labels: Node3D


static func look_of(species: String) -> Array:
	var key := species.to_lower()
	if SPECIES.has(key):
		return SPECIES[key]
	var h := absi(hash(key))
	return [FORMS[h % 2], Color.from_hsv(float(h % 100) / 100.0 * 0.15 + 0.05, 0.35, 0.55), 0.8]


static func _box_part(tool: SurfaceTool, size: Vector3, at: Vector3) -> void:
	var box := BoxMesh.new()
	box.size = size
	tool.append_from(box, 0, Transform3D(Basis(), at))


## Malha única por forma (caixas fundidas): corpo, cabeça, patas (quadrúpede), orelhas (pequeno), bico (ave).
static func form_mesh(form: String) -> Mesh:
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	match form:
		"small":
			_box_part(tool, Vector3(0.16, 0.13, 0.24), Vector3(0, 0.1, 0))
			_box_part(tool, Vector3(0.11, 0.1, 0.1), Vector3(0, 0.17, 0.14))
			for side in [-1, 1]:
				_box_part(tool, Vector3(0.03, 0.12, 0.03), Vector3(side * 0.03, 0.27, 0.13))
		"bird":
			_box_part(tool, Vector3(0.14, 0.14, 0.2), Vector3(0, 0.15, 0))
			_box_part(tool, Vector3(0.09, 0.09, 0.09), Vector3(0, 0.26, 0.09))
			_box_part(tool, Vector3(0.04, 0.03, 0.06), Vector3(0, 0.25, 0.16))
			for side in [-1, 1]:
				_box_part(tool, Vector3(0.02, 0.09, 0.02), Vector3(side * 0.04, 0.045, 0))
		_:
			_box_part(tool, Vector3(0.24, 0.2, 0.5), Vector3(0, 0.34, 0))
			_box_part(tool, Vector3(0.16, 0.16, 0.2), Vector3(0, 0.46, 0.3))
			_box_part(tool, Vector3(0.04, 0.04, 0.16), Vector3(0, 0.38, -0.3))
			for x in [-1, 1]:
				for z in [-1, 1]:
					_box_part(tool, Vector3(0.06, 0.26, 0.06), Vector3(x * 0.08, 0.13, z * 0.18))
	tool.generate_normals()
	return tool.commit()


func _init() -> void:
	for form in FORMS:
		var multi := MultiMesh.new()
		multi.transform_format = MultiMesh.TRANSFORM_3D
		multi.use_colors = true
		multi.mesh = form_mesh(form)
		var instance := MultiMeshInstance3D.new()
		instance.name = "Fauna_%s" % form
		instance.multimesh = multi
		var material := StandardMaterial3D.new()
		material.vertex_color_use_as_albedo = true
		material.roughness = 0.9
		instance.material_override = material
		add_child(instance)
		_multis[form] = instance
	_labels = Node3D.new()
	_labels.name = "FaunaLabels"
	add_child(_labels)


## Animais e bandos visíveis no escopo: [{form, transform, color}].
func _instances() -> Array:
	var out: Array = []
	positions.clear()
	group_positions.clear()
	for id in store.animals:
		var animal: Dictionary = store.animals[id]
		if bounds_filter.size != Vector2i.ZERO and not bounds_filter.has_point(animal.cell):
			continue
		var look := look_of(String(animal.species))
		var jitter := Vector3(fmod(float(id) * 0.37, 1.0) - 0.5, 0, fmod(float(id) * 0.61, 1.0) - 0.5) * 0.5
		var at := Grid.to_world(animal.cell) + jitter
		var lean: float = 0.85 if int(animal.energy) < 30 else 1.0
		var yaw: float = _yaw.get(int(id), float(id % 8) * PI / 4.0)
		var from: Vector3 = drawn.get(int(id), at)
		if from.distance_to(at) > 0.01 and from.distance_to(at) <= JUMP: # andou uma ou poucas células: desliza até lá
			moving[int(id)] = {"from": from, "to": at, "t": 0.0}
			yaw = atan2(at.x - from.x, at.z - from.z)
			_yaw[int(id)] = yaw
		else:
			moving.erase(int(id))
			drawn[int(id)] = at
		var basis := Basis(Vector3.UP, yaw) * Basis.from_scale(Vector3(lean, 1, 1) * float(look[2]))
		var color: Color = look[1].darkened(0.25) if bool(animal.disease) else look[1]
		out.append({"form": look[0], "transform": Transform3D(basis, drawn[int(id)]), "color": color, "id": int(id)})
		positions[int(id)] = drawn[int(id)] + Vector3(0, 0.3, 0)
	for key in store.fauna_groups:
		var group: Dictionary = store.fauna_groups[key]
		var look := look_of(String(group.species))
		var center := Grid.to_world(group.cell)
		for n in mini(int(group.count), GROUP_SHOWN):
			var angle := float(n) * TAU / GROUP_SHOWN + float(absi(hash(key)) % 10)
			var at := center + Vector3(cos(angle), 0, sin(angle)) * (0.0 if n == 0 else 0.55)
			out.append({"form": look[0], "transform": Transform3D(Basis(Vector3.UP, angle).scaled(Vector3.ONE * float(look[2]) * 1.4), at),
				"color": look[1]})
		group_positions[key] = center + Vector3(0, 0.5, 0)
	return out


func rebuild() -> void:
	_slots.clear()
	for id in drawn.keys():
		if not store.animals.has(id):
			drawn.erase(id)
			moving.erase(id)
			_yaw.erase(id)
	var instances := _instances()
	var by_form: Dictionary = {}
	for form in FORMS:
		by_form[form] = []
	for entry in instances:
		by_form[entry.form].append(entry)
	for form in FORMS:
		var list: Array = by_form[form]
		var multi: MultiMesh = _multis[form].multimesh
		multi.instance_count = list.size()
		for i in list.size():
			multi.set_instance_transform(i, list[i].transform)
			multi.set_instance_color(i, list[i].color)
			if list[i].has("id"):
				_slots[list[i].id] = [form, i, list[i].transform.basis]
		_multis[form].visible = not list.is_empty()
	for child in _labels.get_children():
		if child.is_inside_tree():
			_labels.remove_child(child)
		child.free()
	for key in group_positions:
		var group: Dictionary = store.fauna_groups[key]
		var label := Label3D.new()
		label.text = "%d" % int(group.count)
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		label.pixel_size = 0.01
		label.font_size = 28
		label.outline_size = 8
		label.position = group_positions[key] + Vector3(0, 0.6, 0)
		_labels.add_child(label)


func instance_count() -> int:
	var total := 0
	for form in FORMS:
		total += _multis[form].multimesh.instance_count
	return total


## Um quadro do deslize: só quem está andando entre duas células.
func step(delta: float) -> void:
	if moving.is_empty():
		return
	for id in moving.keys():
		var glide: Dictionary = moving[id]
		glide.t = minf(1.0, float(glide.t) + delta / GLIDE)
		var at: Vector3 = Vector3(glide.from).lerp(glide.to, smoothstep(0.0, 1.0, float(glide.t)))
		drawn[id] = at
		positions[id] = at + Vector3(0, 0.3, 0)
		var slot = _slots.get(id)
		if slot != null:
			_multis[slot[0]].multimesh.set_instance_transform(slot[1], Transform3D(slot[2], at))
		if float(glide.t) >= 1.0:
			moving.erase(id)
