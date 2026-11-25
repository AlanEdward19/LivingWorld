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


func _init() -> void:
	custom_minimum_size = Vector2(220, 300)
	stretch = true
	_viewport = SubViewport.new()
	_viewport.size = Vector2i(220, 300)
	_viewport.own_world_3d = true
	_viewport.transparent_bg = true
	add_child(_viewport)
	var camera := Camera3D.new()
	camera.fov = 28.0
	camera.position = Vector3(0.0, 0.95, 3.0)
	camera.basis = Basis.looking_at(Vector3(0.0, 0.85, 0.0) - camera.position, Vector3.UP)
	_viewport.add_child(camera)
	var light := DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-35, -25, 0)
	_viewport.add_child(light)
	var fill := OmniLight3D.new()
	fill.position = Vector3(1.5, 1.6, 2.5)
	fill.light_energy = 0.6
	_viewport.add_child(fill)


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


func clear() -> void:
	last_look = {}
	last_height_m = 0.0
	if actor != null:
		actor.queue_free()
		actor = null
