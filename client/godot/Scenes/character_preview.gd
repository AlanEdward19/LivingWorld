extends SubViewportContainer
## AD-197t: a prévia da pessoa no criador. Desenha o MESMO corpo do mundo (NpcActor) a partir do estado que o motor devolveu e
## validou (`character.look`), nunca da escolha local: o que a tela mostra é o que o motor aceitou. Mundo 3D próprio (não toca na cena).

const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const ADULT_HEIGHT_M := 1.70

var actor: Node3D
var last_look: Dictionary = {} ## o que foi entregue ao ator (para os testes e a captura)
var last_height_m := 0.0
var _viewport: SubViewport
const SPIN_RAD_S := 0.5 ## giro lento do boneco; só gira visível e com alguém desenhado (o criador fechado não custa nada)


func _init() -> void:
	custom_minimum_size = Vector2(300, 400)
	stretch = true
	_viewport = SubViewport.new()
	_viewport.size = Vector2i(300, 400)
	_viewport.own_world_3d = true
	_viewport.transparent_bg = true
	add_child(_viewport)
	var camera := Camera3D.new()
	camera.fov = 28.0
	camera.position = Vector3(0.0, 1.05, 4.7)
	camera.basis = Basis.looking_at(Vector3(0.0, 0.88, 0.0) - camera.position, Vector3.UP)
	_viewport.add_child(camera)
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-35, -25, 0)
	_viewport.add_child(light)
	var floor := MeshInstance3D.new() # um disco no chão para o boneco não flutuar
	var disc := CylinderMesh.new()
	disc.top_radius = 0.6
	disc.bottom_radius = 0.6
	disc.height = 0.03
	floor.mesh = disc
	floor.position = Vector3(0.0, -0.016, 0.0)
	var floor_material := StandardMaterial3D.new()
	floor_material.albedo_color = Color(0.2, 0.24, 0.33)
	floor.material_override = floor_material
	_viewport.add_child(floor)
	var fill := OmniLight3D.new()
	fill.position = Vector3(1.5, 1.6, 2.5)
	fill.light_energy = 0.6
	_viewport.add_child(fill)
	set_process(false)


## Entrega ao corpo o estado que o motor validou. Sem `look` (resposta vazia) apaga o desenho.
func show_character(character: Dictionary) -> void:
	clear()
	if not character.has("look") or not character.look is Dictionary:
		return
	var look: Dictionary = character.look
	last_look = {"skin_tone": int(look.skinTone), "hair_tone": int(look.hairTone), "hair_curl": int(look.hairCurl),
		"build": int(look.build), "eye_tone": int(look.eyeTone), "face_shape": int(look.faceShape), "outfit": int(look.outfit)}
	last_height_m = float(look.heightM)
	actor = NpcActor.new()
	actor.setup(0, Vector2i.ZERO, -1, ActionLabels.new(), last_look)
	actor.position = Vector3.ZERO
	actor.scale = Vector3.ONE * clampf(last_height_m / ADULT_HEIGHT_M, 0.7, 1.25)
	_viewport.add_child(actor)
	set_process(true)


func _process(delta: float) -> void:
	if actor != null and is_visible_in_tree():
		actor.rotation.y = wrapf(actor.rotation.y + delta * SPIN_RAD_S, -PI, PI)


func clear() -> void:
	last_look = {}
	last_height_m = 0.0
	if actor != null:
		actor.queue_free()
		actor = null
	set_process(false)
