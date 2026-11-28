extends Node3D
## Um NPC em 3D: corpo articulado procedural (placeholder estilizado) com animação por pose.
## Movimento e pose seguem o que o servidor projeta; nada aqui decide comportamento.

const Grid := preload("res://Scenes/grid.gd")
const Anim := preload("res://Scenes/animation_catalog.gd")
const Look := preload("res://Scenes/npc_look.gd")
const Props := preload("res://Scenes/npc_props.gd")
const RouteWalk := preload("res://Scenes/route_walk.gd")
const Rig := preload("res://Scenes/npc_rig.gd")
const BodyPose := preload("res://Scenes/body_pose.gd")
const NpcMesh := preload("res://Scenes/npc_mesh.gd")

const LEG_X := 0.08
## #237: o corpo deitado fica centrado na origem do ator (a pose antiga ia de z -1,43 a -0,28: com a origem na cama, o corpo cruzava a parede de trás)
const LYING_SHIFT := 0.855
const SKIN_TONES: Array[Color] = [Color("f1c9a5"), Color("e0ac7e"), Color("c68a5c"), Color("a06a44"), Color("7a4b30")]
const HAIR_COLORS: Array[Color] = [Color("2b1b12"), Color("5a3a22"), Color("a0652a"), Color("d6b36a"), Color("8b8b8b"), Color("b5482a")]
## Rampas dos genes de aparência do motor (#46 fatia 2), do mais claro ao mais escuro.
const SKIN_RAMP: Array[Color] = [Color("f6dcc2"), Color("f1c9a5"), Color("e0ac7e"), Color("c68a5c"), Color("a06a44"), Color("7a4b30"), Color("4e2f1f")]
const HAIR_RAMP: Array[Color] = [Color("e9dcae"), Color("d6b36a"), Color("a0652a"), Color("5a3a22"), Color("2b1b12")]
const EYE_RAMP: Array[Color] = [Color("8fb4d8"), Color("6f9f8a"), Color("7d6b3a"), Color("4a2f1c"), Color("1c1410")] ## AD-197t: olhos, do claro ao escuro
const OUTFIT_COLORS: Array[Color] = [Color("8a7a52"), Color("5a6a7a"), Color("8a3a3a"), Color("5a3a8a"), Color("e8e4d8"), Color("4a6a3a")] ## AD-197t: traje, na ordem de `Appearance.OutfitNames` do motor
const GENE_STEP := 5 ## genes em passos de 5: no máximo 21 materiais por rampa, compartilhados entre todos os NPCs

## Simulação pausada: ninguém anda nem anima (um passo manual leva o NPC direto ao destino).
static var frozen := false
## Hora do mundo contínua (RouteClock), posta pelo renderizador a cada quadro: quem tem rota anda por ela.
static var world_tick := 0.0
static var _meshes: Dictionary = {}
static var _materials: Dictionary = {}

var npc_id := 0
var far_lod := false
var bubble := false ## mapa-múndi muito afastado: só uma bolha colorida, sem corpo
var _bubble_node: MeshInstance3D
var _shadow_parts: Array[MeshInstance3D] = []
var _anim_accum := 0.0
var cell := Vector2i.ZERO
var action := -1
var pose := Anim.NEUTRAL_POSE
var look: Dictionary = {} ## genes do motor {skin_tone, hair_tone, hair_curl, build}; vazio = sorteio local de sempre. AD-197t: quem o jogador montou traz também eye_tone, face_shape e outfit
var _eye_color: Variant = null ## cor da íris do criador (eye_tone); sem ela, a cor sai da semente (id)
var _style: Dictionary = {} ## #254: corte de cabelo, barba, roupa do período e cores por semente (Look.style_of)
var _beard: MeshInstance3D
var _blink := 1.0 ## abertura dos olhos (1 aberto, ~0,1 piscando)
var _blend := 1.0 ## #254: mistura da pose anterior com a nova (0 = pose velha, 1 = nova): troca suave
var _blend_from: Array = []
var _last_effective := ""
var _rig = null ## #230: esqueleto articulado (npc_rig.gd), só de perto; longe é null e não custa nada
var full_rig := false: ## prévia do criador: sempre articulado e com rosto
	set(value):
		full_rig = value
		if value:
			set_detail(true)
		_sync_rig()
var ground_probe := Callable() ## (Vector3 do mundo) -> altura do chão ou NAN: o pé acompanha o terreno; sem ele usa as alturas da rota
var _pants_color := Color("5a5a66")
var _torso_base := Vector3.ONE
var _head_base := Vector3.ONE
var _head_mult := Vector3.ONE
var _gesture_target := Vector3.INF
var _outfit_color: Variant = null ## cor do traje escolhido; vence a camisa do ofício
var label_text := ""
var display_name := ""

var _from := Vector3.ZERO
var _to := Vector3.ZERO
var _t := 1.0
var _duration := 1.0
var _clock := 0.0
var _body: Node3D ## tronco + cabeça + braços (gira/deita como um bloco)
var _torso: MeshInstance3D
var _head: Node3D
var _arms: Array[Node3D] = []
var _legs: Array[Node3D] = []
var _tool: Node3D
var _hat: Node3D
var _hair_mesh: MeshInstance3D
var _skin_mesh: MeshInstance3D
var _bed: Node3D
var _label: Label3D
var _bubble: Label3D
var _zzz: Label3D
var _ring: MeshInstance3D
var _aura: MeshInstance3D
var _shirt: StandardMaterial3D
var use_pose := "" ## pose declarada pelo objeto em uso (vazio = pose da ação)
var use_bed := false ## o interior liga: cama só dentro de casa
var _selected := false
var _hovered := false
var spec: Dictionary = {} ## aparência derivada do estado (Look.spec_of); vazio = corpo base
var near_detail := false ## LOD "perto": ferramenta, objeto em uso e rosto
var face_point := Vector3.INF ## alvo da ação (pessoa com quem conversa, oficina): o NPC parado vira para lá
var held_color := Color("9a7a52") ## cor do material do recipiente (vem de /designs/{id} quando conhecido)
var _skin_color := Color("e0ac7e")
var _tool_kind := "hammer"
var site_work := false ## AD-193: no lote de uma obra (alvo `work` do motor): martelo na mão, seja qual for o ofício
var _belly: MeshInstance3D
var _sack: Node3D
var _held: MeshInstance3D
var _face: Node3D
var _face_expression := "-"
var _sickbed: Node3D
var _bubble_dots := 0
var walk = null ## RouteWalk: anda pela rota do motor até a chegada (AD-181); null = movimento de sempre
var spread := Vector3.ZERO ## lugar dentro da célula quando ela é dividida (só apresentação; a célula é a do servidor)
const VEER := 0.16 ## desvio lateral no meio do passo: quem se cruza passa pela direita, não por dentro do outro


func setup(id: int, p_cell: Vector2i, p_action: int, labels, p_look: Dictionary = {}) -> void:
	npc_id = id
	look = p_look
	cell = p_cell
	position = Grid.to_world(cell)
	_from = position
	_to = position
	_build()
	set_action(p_action, labels)


func _mat(color: Color) -> StandardMaterial3D:
	var material := StandardMaterial3D.new()
	material.albedo_color = color
	material.roughness = 0.85
	material.rim_enabled = true # #254: contorno de luz suave (fresnel simples): a figura se destaca do fundo sem textura
	material.rim = 0.28
	material.rim_tint = 0.45
	return material


## Cor numa rampa para um gene em [0,100], arredondado ao passo (cor igual = material compartilhado).
static func ramp_color(ramp: Array[Color], gene: int) -> Color:
	var snapped_gene := clampi(roundi(float(gene) / GENE_STEP) * GENE_STEP, 0, 100)
	var position := float(snapped_gene) / 100.0 * (ramp.size() - 1)
	var index := mini(int(position), ramp.size() - 2)
	return ramp[index].lerp(ramp[index + 1], position - index)


## Pele, cabelo, volume do cabelo e porte a partir dos genes; sem genes, não mexe em nada.
func apply_look(p_look: Dictionary) -> void:
	look = p_look
	if look.is_empty() or _skin_mesh == null:
		return
	_skin_color = ramp_color(SKIN_RAMP, int(look.skin_tone))
	_skin_mesh.material_override = _shared_mat(_skin_color)
	_hair_mesh.material_override = _shared_mat(ramp_color(HAIR_RAMP, int(look.hair_tone)))
	_face_expression = "-" # a cor do cabelo pinta as sobrancelhas e a barba: o rosto refaz na próxima vez que estiver perto
	var curl := float(look.hair_curl) / 100.0
	_hair_mesh.scale = Vector3(1.0 + 0.3 * curl, 1.0 + 0.35 * curl, 1.0 + 0.3 * curl)
	var width := 0.85 + 0.4 * float(look.build) / 100.0
	_torso_base = Vector3(width, 1.0, width)
	_torso.scale = _torso_base
	_apply_made_look()
	if _rig != null:
		_rig.refit(self)


## AD-197t: o que só o criador de personagem manda: olhos, rosto (largura da cabeça) e traje. Quem não tem não cria nada.
func _apply_made_look() -> void:
	if look.has("face_shape"):
		var wide := 0.88 + 0.3 * float(look.face_shape) / 100.0
		_head_base = Vector3(wide, 1.0 + 0.08 * (1.0 - wide), 1.0)
		_apply_head_scale()
	if look.has("eye_tone"):
		_eye_color = ramp_color(EYE_RAMP, int(look.eye_tone))
		_face_expression = "-"
	if look.has("outfit"):
		_outfit_color = OUTFIT_COLORS[clampi(int(look.outfit), 0, OUTFIT_COLORS.size() - 1)]
		_shirt.albedo_color = _outfit_color


func _apply_head_scale() -> void:
	_head.scale = _head_base * _head_mult


## Materiais sem emissão variável são compartilhados entre todos os NPCs (menos memória e trocas de estado).
func _shared_mat(color: Color) -> StandardMaterial3D:
	var key := color.to_html()
	if not _materials.has(key):
		_materials[key] = _mat(color)
	return _materials[key]


func _shared_mesh(key: String, factory: Callable) -> Mesh:
	if not _meshes.has(key):
		_meshes[key] = factory.call()
	return _meshes[key]


func _part(parent: Node3D, mesh: Mesh, material: Material, at: Vector3) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.mesh = mesh
	node.material_override = material
	node.position = at
	parent.add_child(node)
	return node


func _limb(parent: Node3D, shoulder: Vector3, mesh: Mesh, material: Material, length: float) -> Node3D:
	var pivot := Node3D.new()
	pivot.position = shoulder
	parent.add_child(pivot)
	_part(pivot, mesh, material, Vector3(0, -length / 2.0, 0))
	return pivot


func _build() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = npc_id * 7919 + 17
	var hue := rng.randf()
	_shirt = _mat(Color.from_hsv(hue, 0.55, 0.8))
	_pants_color = Color.from_hsv(snappedf(fmod(hue + 0.5, 1.0), 0.1), 0.35, 0.4)
	var pants := _shared_mat(_pants_color)
	_skin_color = SKIN_TONES[rng.randi() % SKIN_TONES.size()]
	var skin := _shared_mat(_skin_color)
	var hair := _shared_mat(HAIR_COLORS[rng.randi() % HAIR_COLORS.size()])
	_shirt.emission_enabled = true
	_shirt.emission = Color.BLACK

	_body = Node3D.new()
	add_child(_body)
	_torso = _part(_body, NpcMesh.torso("male"), _shirt, Vector3(0, 0.62, 0))
	_shadow_parts.append(_torso)
	_head = Node3D.new()
	_head.position = Vector3(0, 0.98, 0)
	_body.add_child(_head)
	_skin_mesh = _part(_head, NpcMesh.head(), skin, Vector3.ZERO)
	_shadow_parts.append(_skin_mesh)
	_hair_mesh = _part(_head, NpcMesh.hair("short"), hair, Vector3.ZERO)
	_shadow_parts.append(_hair_mesh)
	for side in [-1, 1]:
		_arms.append(_limb(_body, Vector3(side * 0.22, 0.82, 0), NpcMesh.limb(0.34, 0.05, 0.042), _shirt, 0.34))
		_legs.append(_limb(self, Vector3(side * 0.08, 0.36, 0), NpcMesh.limb(0.36, 0.066, 0.05), pants, 0.36))

	apply_look(look)

	var body := StaticBody3D.new()
	body.set_meta("npc_id", npc_id)
	var shape := CollisionShape3D.new()
	var cylinder := CylinderShape3D.new()
	cylinder.radius = 0.35
	cylinder.height = 1.2
	shape.shape = cylinder
	shape.position.y = 0.6
	body.add_child(shape)
	add_child(body)


## Partes raras (ferramenta, cama, anel, aura, textos) só existem quando precisam: 500 NPCs com tudo
## pronto custam muitos nós; assim a maioria dos NPCs tem só corpo, cabeça, braços e pernas.
func _get_tool() -> Node3D:
	if _tool == null:
		_tool = Props.tool(_arms[1], _tool_kind)
		if _rig != null and _rig.active:
			_rig.attach_new_prop(_tool, false)
	return _tool


## AD-193: o motor diz que a pessoa trabalha no lote da obra: martelo; fora dela volta a ferramenta do ofício.
func set_site_work(on: bool) -> void:
	if on == site_work:
		return
	site_work = on
	_set_tool_kind("hammer" if on else String(spec.get("tool", "hammer")))


## Troca a ferramenta pelo ofício (de perto, na mão durante o trabalho).
func _set_tool_kind(kind: String) -> void:
	if kind == _tool_kind:
		return
	_tool_kind = kind
	if _tool != null:
		var shown := _tool.visible
		_tool.free()
		_tool = null
		_get_tool().visible = shown


func _get_bed() -> Node3D:
	if _bed == null:
		_bed = Node3D.new()
		add_child(_bed)
		var mattress := BoxMesh.new()
		mattress.size = Vector3(0.7, 0.14, 1.15)
		_part(_bed, mattress, _mat(Color("e9e2d0")), Vector3(0, 0.07, 0.0))
		var frame := BoxMesh.new()
		frame.size = Vector3(0.76, 0.06, 1.2)
		_part(_bed, frame, _mat(Color("6b4a2b")), Vector3(0, 0.03, 0.0))
		var pillow := BoxMesh.new()
		pillow.size = Vector3(0.3, 0.07, 0.2)
		_part(_bed, pillow, _mat(Color("ffffff")), Vector3(0, 0.17, -0.38))
	return _bed


func _get_ring() -> MeshInstance3D:
	if _ring == null:
		_ring = MeshInstance3D.new()
		var ring := TorusMesh.new()
		ring.inner_radius = 0.36
		ring.outer_radius = 0.46
		_ring.mesh = ring
		var ring_material := _mat(Color("ffd84a"))
		ring_material.emission_enabled = true
		ring_material.emission = Color("ffd84a")
		ring_material.emission_energy_multiplier = 1.4
		_ring.material_override = ring_material
		_ring.position.y = 0.03
		add_child(_ring)
	return _ring


func _get_aura() -> MeshInstance3D:
	if _aura == null:
		_aura = MeshInstance3D.new()
		var aura := SphereMesh.new()
		aura.radius = 0.55
		aura.height = 1.1
		_aura.mesh = aura
		var aura_material := _mat(Color(0.45, 0.7, 1.0, 0.25))
		aura_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		aura_material.emission_enabled = true
		aura_material.emission = Color(0.4, 0.7, 1.0)
		_aura.material_override = aura_material
		_aura.position.y = 0.6
		add_child(_aura)
	return _aura


func _get_label() -> Label3D:
	if _label == null:
		_label = _text(Vector3(0, 1.45, 0), 26, Color.WHITE)
		_label.visible = false
	return _label


func _get_bubble() -> Label3D:
	if _bubble == null:
		_bubble = _text(Vector3(0.1, 1.3, 0), 30, Color("ffffff"))
		_bubble.no_depth_test = false
	return _bubble


func _get_zzz() -> Label3D:
	if _zzz == null:
		_zzz = _text(Vector3(0, 0.9, 0), 34, Color("bfe3ff"))
		_zzz.text = "Z"
		_zzz.no_depth_test = false
	return _zzz


func _text(at: Vector3, size: int, color: Color) -> Label3D:
	var label := Label3D.new()
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.position = at
	label.pixel_size = 0.007
	label.font_size = size
	label.modulate = color
	label.outline_size = 10
	label.no_depth_test = true
	add_child(label)
	return label


## Modelo por profissão e idade vindo do catálogo: camisa e chapéu da profissão, escala e cabelo da idade.
## Só mexe em materiais próprios do NPC e em um chapéu opcional; o corpo base continua o mesmo.
func apply_model(catalog, profession: int, age_years: float) -> void:
	var by_profession: Dictionary = catalog.profession_model(profession)
	if by_profession.has("shirt"):
		_shirt.albedo_color = _outfit_color if _outfit_color != null else Color(String(by_profession.shirt))
	_set_hat(String(by_profession.get("hat", "none")))
	var by_age: Dictionary = catalog.age_model(age_years)
	scale = Vector3.ONE * float(by_age.get("scale", 1.0))
	if by_age.has("hair") and _hair_mesh != null:
		_hair_mesh.material_override = _shared_mat(Color(String(by_age.hair)))


func _set_hat(kind: String) -> void:
	if _hat != null:
		_hat.free()
		_hat = null
	if kind == "none":
		return
	var mesh := CylinderMesh.new()
	if kind == "straw":
		mesh.top_radius = 0.12
		mesh.bottom_radius = 0.24
		mesh.height = 0.06
	else:
		mesh.top_radius = 0.13
		mesh.bottom_radius = 0.15
		mesh.height = 0.1
	_hat = _part(_head, mesh, _shared_mat(Color("6b4a2b") if kind == "straw" else Color("3b3f46")), Vector3(0, 0.16, 0))


## Corpo pelo estado (ADR-0047 etapa 3): escala por altura e idade, roupa e chapéu do ofício, cabelo pelo sexo, barriga de
## gravidez, palidez e cama de doente, roupa de luto, carga nas costas, recipiente e ferramenta. Só muda o que mudou.
func apply_observable(obs: Dictionary, catalog = null) -> void:
	set_combat_reaction(String(obs.get("combat", ""))) # #234: a briga em curso vale mesmo sem mudar o corpo
	var next := Look.spec_of(obs, catalog)
	if next == spec:
		return
	var before := spec
	spec = next
	if spec.is_empty():
		return
	scale = Vector3.ONE * float(spec.scale)
	_shirt.albedo_color = _outfit_color if _outfit_color != null else spec.cloth
	if before.get("hat", "") != spec.hat:
		if _hat != null:
			_hat.free()
			_hat = null
		_hat = Props.hat(_head, String(spec.hat))
	_apply_style()
	var skin := _skin_color * Look.SICK_SKIN if bool(spec.ill) else _skin_color
	_skin_mesh.material_override = _shared_mat(skin)
	if _rig != null:
		_rig.refit(self)
	_set_tool_kind("hammer" if site_work else String(spec.tool))
	if float(spec.belly) > 0.0:
		if _belly == null:
			_belly = Props.belly(_body, spec.cloth)
		_belly.material_override = Props.material(spec.cloth)
		_belly.scale = Vector3.ONE * (0.55 + 0.75 * float(spec.belly))
		_belly.visible = true
	elif _belly != null:
		_belly.visible = false
	if before.get("sack", {}) != spec.sack:
		if _sack != null:
			_sack.free()
			_sack = null
		if not spec.sack.is_empty():
			_sack = Props.sack(_body, spec.sack)
	_refresh_held()
	if spec.sick_pose == "bed" or _sickbed != null:
		if _sickbed == null:
			_sickbed = Props.sickbed(self)
		_sickbed.visible = spec.sick_pose == "bed"
	_face_expression = "-" # o rosto refaz na próxima vez que estiver perto
	_refresh_face()


## #254: corte de cabelo, barba, roupa do período e proporções pelo estágio e sexo (do motor) e pela semente (id). Malhas compartilhadas:
## trocar de estilo só troca a referência da malha, nunca cria geometria por NPC.
func _apply_style() -> void:
	var stage := String(spec.get("stage", "adult"))
	var female := bool(spec.get("female", false))
	_style = Look.style_of(npc_id, stage, female, String(spec.get("era", "")))
	_hair_mesh.mesh = NpcMesh.hair(String(_style.hair))
	_hair_mesh.position = Vector3.ZERO
	var kind := "child" if (stage == "baby" or stage == "child") else ("elder" if stage == "elder" else ("female" if female else "male"))
	_torso.mesh = NpcMesh.torso(kind)
	_pants_color = _style.pants
	for leg in _legs:
		leg.get_child(0).material_override = _shared_mat(_pants_color)
	var girth := 0.9 if female else (1.12 if (stage == "baby" or stage == "child") else (0.95 if stage == "elder" else 1.0))
	for limb in _arms + _legs:
		var mesh_node: Node3D = limb.get_child(0)
		mesh_node.scale = Vector3(girth, mesh_node.scale.y, girth)


func _refresh_held() -> void:
	var wants: bool = not spec.get("held", {}).is_empty()
	if wants and _held == null:
		_held = Props.held(_arms[0], "cylinder", held_color)
		if _rig != null and _rig.active:
			_rig.attach_new_prop(_held, true)
	if _held != null:
		_held.material_override = Props.material(held_color)
		_held.visible = wants and near_detail


## Cor e forma do recipiente vindas do design (o renderizador lê `/designs/{id}` uma vez e repassa).
func set_held_look(shape: String, color: Color) -> void:
	held_color = color
	if _held != null:
		_held.mesh = Props.shape_mesh(shape, 0.14)
	_refresh_held()


## LOD "perto" (câmera bem próxima): ferramenta e objeto em uso na mão e rosto com expressão simples.
func set_detail(value: bool) -> void:
	if near_detail == value:
		return
	near_detail = value
	_sync_rig()
	_sync_props()
	_refresh_held()
	_refresh_face()


func _refresh_face() -> void:
	var expression := String(spec.get("expression", ""))
	var wants := near_detail and not bubble and not far_lod
	if wants and expression != _face_expression:
		if _face != null:
			_face.free()
		var iris: Color = _eye_color if _eye_color != null else Color(_style.get("eyes", Look.EYE_COLORS[0]))
		_face = Props.face(_head, expression, iris, _hair_mesh.material_override.albedo_color.darkened(0.15), String(spec.get("stage", "adult")), _skin_mesh.material_override)
		_face_expression = expression
		_blink = 1.0
	if _face != null:
		_face.visible = wants
	var beard_kind := int(_style.get("beard", 0))
	if wants and beard_kind > 0:
		if _beard == null:
			_beard = _part(_head, NpcMesh.beard(beard_kind == 2), _hair_mesh.material_override, Vector3.ZERO)
			_beard.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		_beard.mesh = NpcMesh.beard(beard_kind == 2)
		_beard.material_override = _hair_mesh.material_override
		_beard.visible = true
	elif _beard != null:
		_beard.visible = false


func set_action(new_action: int, labels, p_use_pose: String = "") -> void:
	action = new_action
	use_pose = p_use_pose
	pose = Anim.pose_for(new_action, p_use_pose)
	label_text = labels.label_for(new_action) if labels != null else ""
	_refresh_label()
	_sync_props()


## Longe da câmera os NPCs são só tronco e cabeça (sem braços, pernas nem sombra): 500 NPCs fluidos.
func set_far(value: bool) -> void:
	if far_lod == value:
		return
	far_lod = value
	_sync_rig()
	for limb in _arms + _legs:
		limb.visible = not value
	for part in _shadow_parts:
		part.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF if value else GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	if value:
		_reset_pose()


## Bolha de longe: esconde corpo e pernas e mostra uma esfera na cor da camisa (nada de animar nem sombrear).
func set_bubble(value: bool) -> void:
	if bubble == value:
		return
	bubble = value
	_sync_rig()
	_body.visible = not value
	for leg in _legs:
		leg.visible = not value
	if value:
		if _bubble_node == null:
			_bubble_node = MeshInstance3D.new()
			_bubble_node.mesh = _shared_mesh("bubble", func():
				var m := SphereMesh.new()
				m.radius = 0.35
				m.height = 0.7
				m.radial_segments = 8
				m.rings = 4
				return m)
			var material := _shared_mat(_shirt.albedo_color.lightened(0.15))
			_bubble_node.material_override = material
			_bubble_node.position.y = 0.4
			_bubble_node.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
			add_child(_bubble_node)
		_bubble_node.visible = true
		_reset_pose()
	elif _bubble_node != null:
		_bubble_node.visible = false


## #232: fase, peso e corrida da passada do rig ([] sem rig): o tempo dos passos do corpo do jogador.
func step_state() -> Array:
	if _rig == null or not _rig.active:
		return []
	return [_rig.phase, _rig.gait, _rig.run]


## #230: o rig só existe para quem está perto, é o corpo do jogador ou a prévia; ao sair de cena (longe/bolha) os nós são liberados.
func _sync_rig() -> void:
	if _body == null:
		return
	var want := (near_detail or driven or full_rig) and not far_lod and not bubble
	if not want and _rig != null and (far_lod or bubble):
		_rig.deactivate(self, true)
		_rig = null
		return
	var on: bool = _rig != null and _rig.active
	if want == on:
		return
	if want:
		if _rig == null:
			_rig = Rig.new()
		_rig.activate(self)
	else:
		_rig.deactivate(self, false)


## Altura do chão sob o pé que vai `ahead` à frente, relativa ao corpo (unidades do boneco): o pé sobe o degrau e desce a rampa.
func foot_ground(ahead: float) -> float:
	var raw := 0.0
	if ground_probe.is_valid():
		var fwd := Vector3(sin(rotation.y), 0.0, cos(rotation.y))
		var y: Variant = ground_probe.call(position + fwd * ahead * scale.x)
		if y is float and not is_nan(y):
			raw = float(y) - position.y
	elif walk != null and not walk.done:
		var d: float = walk.fraction(world_tick) * walk.total
		raw = BodyPose.route_height(walk.points, walk.along, d + ahead * scale.x) - BodyPose.route_height(walk.points, walk.along, d)
	return clampf(raw / maxf(scale.y, 0.01), -BodyPose.MAX_STEP, BodyPose.MAX_STEP)


func _sync_props() -> void:
	var shows_tool := pose == "work" and near_detail and _tool_kind != ""
	if shows_tool or _tool != null:
		_get_tool().visible = shows_tool
	if (pose == "sleep" and use_bed) or _bed != null:
		_get_bed().visible = pose == "sleep" and use_bed
	if pose == "power" or _aura != null:
		_get_aura().visible = pose == "power"
	if pose == "socialize" or _bubble != null:
		_get_bubble().visible = pose == "socialize"
	if pose == "sleep" or _zzz != null:
		_get_zzz().visible = pose == "sleep"


func enable_bed(enabled: bool) -> void:
	use_bed = enabled
	_sync_props()


func set_display_name(value: String) -> void:
	display_name = value
	_refresh_label()


func _refresh_label() -> void:
	if _label == null and not (_selected or _hovered):
		return
	var label := _get_label()
	label.text = ("%s\n%s" % [display_name, label_text]) if display_name != "" else label_text
	label.visible = (_selected or _hovered) and not is_self


## AD-197p: o corpo do próprio jogador: sem nome, ação nem anel sobre ele (quem joga já sabe quem é).
var is_self := false


## AD-197r: na 1ª pessoa a câmera fica na altura dos olhos, dentro da cabeça: a própria cabeça (e o chapéu) não se desenha.
func set_first_person(on: bool) -> void:
	_head.visible = not on


func set_self(value: bool) -> void:
	is_self = value
	if _ring != null:
		_ring.visible = _ring_shown()
	_refresh_label()


## AD-197s2: o corpo do jogador visto de cima dentro de casa (3ª pessoa sobre o telhado) leva um anel discreto para se achar no meio da gente.
var marked := false


func set_marked(on: bool) -> void:
	if on == marked:
		return
	marked = on
	if _ring != null or on:
		_get_ring().visible = _ring_shown()


func _ring_shown() -> bool:
	return ((_selected or _hovered) and not is_self) or marked


## AD-197s2: na 1ª pessoa quem está colado no olho não se desenha (só o desenho; o motor não muda).
func set_eye_hidden(on: bool) -> void:
	visible = not on


func label_visible() -> bool:
	return _label != null and _label.visible


func label_text_shown() -> String:
	return _label.text if _label != null else ""


## Novo lugar dentro da mesma célula (outro chegou ou saiu): desliza até lá se está parado.
func set_spread(offset: Vector3) -> void:
	if offset.is_equal_approx(spread):
		return
	spread = offset
	if walk != null: # andando pela rota: o lugar na célula vale na chegada
		return
	if _t >= 1.0:
		_from = position
		_to = Grid.to_world(cell) + spread
		_duration = 0.35
		_t = 0.0 if not frozen else 1.0
		if frozen:
			position = _to
	else:
		_to = Grid.to_world(cell) + spread


var avoid := Vector3.ZERO ## AD-197r: desvio (só desenho) de quem anda ao redor de quem está parado; some nas pontas do trecho
var avoid_goal := Vector3.ZERO


## Rumo de quem anda agora (para onde desviar do lado): o da rota, ou o do passo; ZERO parado.
func heading_now() -> Vector3:
	if walk != null and not walk.done:
		return walk.heading
	return _to - _from if _t < 1.0 else Vector3.ZERO


## O ponto do trecho sem o desvio (para onde a rota/o passo o levaria): base do desvio de AD-197r.
func base_position() -> Vector3:
	if walk != null and not walk.done:
		return walk.position
	return _from.lerp(_to, _t)


var driven := false: ## AD-197k: o corpo do jogador anda solto pela posição fina (o delta da grade não o puxa ao centro da célula)
	set(value):
		driven = value
		_sync_rig() # #230: o corpo do jogador é sempre articulado


## AD-197k: a posição fina (células, absoluta) do corpo do jogador; vira para onde anda.
func set_fine(at: Vector2) -> void:
	var to := Vector3(at.x * Grid.CELL, position.y, at.y * Grid.CELL)
	var heading := to - position
	walk = null # #222: o corpo do jogador anda pela posição fina, nunca pela rota do motor desenhada
	if heading.length() > 0.001:
		rotation.y = lerp_angle(rotation.y, atan2(heading.x, heading.z), 0.35)
		# #222: só marca "andando" (a pose de passo) sem interpolar de volta da posição anterior: antes o _from velho puxava o
		# corpo para trás a cada quadro e o passo saía em trancos
		_from = to
		_to = to
		_duration = 0.15
		_t = 0.0
	position = to
	cell = Vector2i(floori(at.x), floori(at.y))


func move_to(new_cell: Vector2i, duration: float) -> void:
	if driven:
		return
	cell = new_cell
	_from = position
	_to = Grid.to_world(new_cell) + spread
	_duration = maxf(duration, 0.001)
	_t = 0.0
	if frozen: # pausado (passo manual): sem caminhada, o NPC aparece no destino
		position = _to
		_t = 1.0


func is_moving() -> bool:
	return _t < 1.0 or (walk != null and not walk.done)


## Rota do motor (células, início da viagem, chegada em ticks). A mesma rota já em curso não recomeça. False se não dá.
func set_route(cells: Array, start: float, arrival: float, heights: Dictionary = {}) -> bool:
	if walk != null and walk.same(cells, start, arrival):
		return true
	var next = RouteWalk.new()
	if not next.setup(cells, start, arrival, position, heights):
		walk = null
		return false
	walk = next
	_t = 1.0
	return true


func clear_route() -> void:
	walk = null


func set_selected(selected: bool) -> void:
	_selected = selected
	if _ring != null or selected or _hovered:
		_get_ring().visible = _ring_shown()
	_refresh_label()


func set_hovered(hovered: bool) -> void:
	_hovered = hovered
	if _ring != null or hovered or _selected:
		_get_ring().visible = _ring_shown()
	_refresh_label()


## Movimento (posição e rumo): roda todo quadro para todo NPC, assim a caminhada dura exatamente o passo do servidor.
func advance_motion(dt: float) -> void:
	if frozen:
		return
	if avoid != avoid_goal:
		avoid = avoid.lerp(avoid_goal, minf(1.0, dt * 10.0))
	if walk != null:
		walk.step(world_tick)
		position = walk.position + avoid * walk.taper
		if not far_lod and walk.heading.length_squared() > 0.0001:
			rotation.y = lerp_angle(rotation.y, atan2(walk.heading.x, walk.heading.z), minf(1.0, dt * 10.0))
		return
	if _t < 1.0:
		_t = minf(1.0, _t + dt / _duration)
		var heading := _to - _from
		position = _from.lerp(_to, _t) + avoid * sin(PI * _t)
		if heading.length() > 0.5: # passo de verdade: desvia para a direita no meio e volta ao lugar no fim
			position += Vector3(-heading.z, 0, heading.x).normalized() * VEER * sin(PI * _t)
		if heading.length() > 0.01 and not far_lod:
			rotation.y = lerp_angle(rotation.y, atan2(heading.x, heading.z), minf(1.0, dt * 10.0))


## Pose: só quando o NPC está à vista; `dt` pode ser o tempo acumulado de quadros pulados.
func animate(dt: float) -> void:
	if frozen:
		return
	_clock += dt
	if not far_lod and not bubble:
		_animate(dt)


func advance(dt: float) -> void:
	advance_motion(dt)
	animate(dt)


## Chamado todo quadro pelo dono da cena: movimento sempre; pose só quando `due` (o tempo pulado se acumula).
func tick(dt: float, near: bool, due: bool) -> void:
	advance_motion(dt)
	if not near:
		_anim_accum = 0.0
		return
	_anim_accum += dt
	if due:
		animate(_anim_accum)
		_anim_accum = 0.0


## Postura pela emoção e pela doença, por cima da pose da ação: cabeça baixa, encolhido, duro ou leve.
func _apply_posture(effective: String) -> void:
	if effective == "sleep":
		return
	match String(spec.get("posture", "")):
		"down":
			_head.rotation.x += 0.35
			_body.rotation.x += 0.08
		"slump":
			_head.rotation.x += 0.25
			_body.rotation.x += 0.18
			_body.position.y -= 0.03
		"hunch":
			_body.position.y -= 0.04
			_body.rotation.x += 0.12
			_head.rotation.y = sin(_clock * 3.0) * 0.5
		"stiff":
			_arms[0].rotation.z = 0.15
			_arms[1].rotation.z = -0.15
		"light":
			_body.position.y += absf(sin(_clock * 4.0)) * 0.025


func _reset_pose() -> void:
	_body.position = Vector3.ZERO
	_body.rotation = Vector3.ZERO
	_head.rotation = Vector3.ZERO
	for limb in _arms + _legs:
		limb.rotation = Vector3.ZERO
	for i in _legs.size():
		_legs[i].position = Vector3(LEG_X * (-1 if i == 0 else 1), 0.36, 0)
	_torso.scale = _torso_base
	if _rig != null and _rig.active:
		_rig.reset_joints()
	if _shirt.emission != Color.BLACK: # só o poder acende a roupa: não reenvia o material de 500 NPCs a cada quadro
		_shirt.emission = Color.BLACK


const STRIKE_TIME := 0.42 ## s do golpe (AD-197j, #234): antecipação (braço sobe), impacto (desce rápido) e recuo (volta)
const STRIKE_WINDUP := 0.4 ## fração do golpe na antecipação
const STRIKE_IMPACT := 0.6 ## fração em que o impacto termina e o recuo começa


## AD-197j: o golpe que o motor aceitou (ou tentou): o braço da frente desce no alvo.
func play_strike() -> void:
	_strike_until = _clock + STRIKE_TIME


## #234: a fase do golpe (k de 0 a 1): "windup" (antecipação), "impact" (impacto) ou "recover" (recuo).
static func strike_phase(k: float) -> String:
	return "windup" if k < STRIKE_WINDUP else ("impact" if k < STRIKE_IMPACT else "recover")


## #234: o braço da frente no golpe (rotação em x): sobe devagar, desce rápido, volta ao repouso.
static func strike_arm(k: float) -> float:
	if k < STRIKE_WINDUP:
		return lerpf(0.0, -2.6, k / STRIKE_WINDUP)
	if k < STRIKE_IMPACT:
		return lerpf(-2.6, 0.5, (k - STRIKE_WINDUP) / (STRIKE_IMPACT - STRIKE_WINDUP))
	return lerpf(0.5, 0.0, (k - STRIKE_IMPACT) / (1.0 - STRIKE_IMPACT))


func striking() -> bool:
	return _clock < _strike_until


const DEFENSE_TIME := {"dodge": 0.38, "block": 0.32, "hurt": 0.4, "recoil": 0.3} ## s de cada reação no corpo
var _defense := ""
var _defense_from := -1.0
var guarding := false ## #234: a guarda do motor vale agora (braços cruzados à frente)
var combat_reaction := "" ## #234: a reação de briga em curso que o motor projetou (retaliate, flee, callHelp, recoil)


## #234: reação do corpo ao golpe recebido: esquiva (desvia e agacha), bloqueio (braços à frente), ferida (tranco para trás)
## ou recuo (passo atrás).
func play_defense(kind: String) -> void:
	if not DEFENSE_TIME.has(kind):
		return
	_defense = kind
	_defense_from = _clock


func defense_playing() -> String:
	return _defense if _defense_from >= 0.0 and _clock < _defense_from + float(DEFENSE_TIME.get(_defense, 0.0)) else ""


func set_guard(on: bool) -> void:
	guarding = on


## #234: o que o motor projetou da briga deste NPC (campo opcional `combat` do observável). Fugir é correr; revidar é golpear;
## pedir ajuda é chamar (e correr); recuar é o passo atrás. Só reage quando a reação muda, e nunca no corpo do jogador.
func set_combat_reaction(kind: String) -> void:
	if kind == combat_reaction:
		return
	combat_reaction = kind
	if is_self:
		return
	running = kind == "flee" or kind == "callHelp"
	match kind:
		"retaliate":
			play_strike()
		"callHelp":
			play_gesture("call", 1.4)
		"recoil":
			play_defense("recoil")


## #222: o pulo que o motor deixou: o corpo sobe e desce em `air_s` segundos (parábola), pernas encolhidas no alto.
func play_jump(air_s: float) -> void:
	_jump_from = _clock
	_jump_air = maxf(0.15, air_s)


## #224: gesto curto por cima da pose (o verbo que o motor aceitou: pegar, dar, beber, comer, empurrar a porta, subir, conversar,
## trabalhar). Toca igual em 1ª e 3ª pessoa; em 1ª as mãos sobem para dentro do quadro.
const GESTURES := ["reach", "give", "drink", "eat", "push", "climb", "talk", "work", "rest", "call", "chop", "hug", "kiss", "laugh", "argue", "plead"]
var _gesture := ""
var _gesture_from := -1.0
var _gesture_len := 0.0
var running := false ## #224: o motor disse que o corpo corre (só o do jogador): passada mais larga e rápida, corpo inclinado


## `target` (ponto do mundo, opcional): onde a mão vai (o objeto pego/empurrado); sem ele usa o ponto típico do gesto à frente do corpo.
func play_gesture(kind: String, seconds: float = 0.9, target: Vector3 = Vector3.INF) -> void:
	if not kind in GESTURES:
		return
	_gesture_target = Vector3.INF if target == Vector3.INF else Basis(Vector3.UP, rotation.y).inverse() * (target - position) / maxf(scale.x, 0.01)
	_gesture = kind
	_gesture_from = _clock
	_gesture_len = maxf(0.2, seconds)
	_gesture_loop = false


var _gesture_loop := false ## #241: gesto em laço enquanto a ação do motor dura (cortar lenha...): peso constante, ciclo pelo relógio


## #241: o gesto da ação que ocupa o corpo, em laço por `seconds` reais (o fim vem do motor; `stop_gesture` corta antes).
func play_loop(kind: String, seconds: float) -> void:
	if not kind in GESTURES:
		return
	play_gesture(kind, seconds)
	_gesture_loop = true


func stop_gesture() -> void:
	_gesture_from = -1.0
	_gesture_loop = false


func gesture_playing() -> String:
	return _gesture if _gesture_from >= 0.0 and _clock < _gesture_from + _gesture_len else ""


## `amplitude` < 0: envelope em seno (corpo sem rig). O rig passa 1 e mistura por conta própria (envelope de platô).
func _apply_gesture(amplitude: float = -1.0) -> void:
	var t := (_clock - _gesture_from) / _gesture_len
	var k := sin(clampf(t, 0.0, 1.0) * PI) if amplitude < 0.0 else amplitude # sobe e volta
	match _gesture:
		"reach": # agacha e estende as duas mãos ao chão/à prateleira (pegar, recolher, encher o cântaro)
			_body.rotation.x = 0.55 * k
			_body.position.y = -0.08 * k
			_arms[0].rotation.x = -1.1 * k
			_arms[1].rotation.x = -1.3 * k
			_legs[0].rotation.x = 0.4 * k
			_legs[1].rotation.x = 0.4 * k
		"give": # estende a mão para a frente (guardar, devolver, comprar, vender)
			_arms[1].rotation.x = -1.55 * k
			_arms[0].rotation.x = -0.4 * k
			_body.rotation.x = 0.12 * k
		"drink": # leva a mão à boca e inclina a cabeça para trás
			_arms[1].rotation.x = -2.5 * k
			_arms[1].rotation.z = 0.35 * k
			_head.rotation.x = -0.3 * k
		"eat": # mão à boca, mastigando
			_arms[1].rotation.x = -2.2 * k + sin(_clock * 12.0) * 0.15 * k
			_arms[1].rotation.z = 0.3 * k
			_head.rotation.x = 0.1 * k
		"push": # empurra a porta com as duas mãos e dá um passo
			_arms[0].rotation.x = -1.6 * k
			_arms[1].rotation.x = -1.6 * k
			_legs[1].rotation.x = -0.4 * k
			_body.rotation.x = 0.15 * k
		"climb": # passos altos alternados (escada)
			_legs[0].rotation.x = -0.9 * maxf(0.0, sin(t * TAU * 2.0))
			_legs[1].rotation.x = -0.9 * maxf(0.0, -sin(t * TAU * 2.0))
			_arms[0].rotation.x = 0.5 * sin(t * TAU * 2.0)
			_arms[1].rotation.x = -0.5 * sin(t * TAU * 2.0)
			_body.position.y = 0.05 * absf(sin(t * TAU * 2.0))
		"talk": # gesticula com as mãos à frente
			_arms[0].rotation.x = -1.0 * k + sin(_clock * 6.0) * 0.25 * k
			_arms[1].rotation.x = -1.2 * k + cos(_clock * 5.0) * 0.3 * k
			_head.rotation.y = sin(_clock * 3.0) * 0.25 * k
		"work": # golpe de ferramenta para a frente
			_arms[1].rotation.x = -2.4 * k + sin(t * TAU * 2.0) * 0.8 * k
			_arms[0].rotation.x = -1.2 * k
			_body.rotation.x = 0.2 * k
		"rest": # deixa o corpo cair (deitar, sentar)
			_body.position.y = -0.1 * k
			_head.rotation.x = 0.3 * k
		"chop": # #241: golpe de machado em laço: as duas mãos sobem e descem juntas, o corpo acompanha
			var swing := BodyPose.chop_swing(_clock / BodyPose.CHOP_PERIOD)
			_arms[0].rotation.x = lerpf(-1.1, -2.9, swing) * k
			_arms[1].rotation.x = lerpf(-1.2, -3.0, swing) * k
			_body.rotation.x = lerpf(0.3, -0.12, swing) * k
			_legs[0].rotation.x = 0.12 * k
			_legs[1].rotation.x = -0.12 * k
		"hug": # #259: abraço: os dois braços à frente e para dentro, o corpo se inclina para o outro
			_arms[0].rotation.x = -1.35 * k
			_arms[1].rotation.x = -1.35 * k
			_arms[0].rotation.z = 0.55 * k
			_arms[1].rotation.z = -0.55 * k
			_body.rotation.x = 0.16 * k
			_head.rotation.z = 0.18 * k
		"kiss": # #259: beijo: o corpo e a cabeça se inclinam, uma das mãos sobe ao rosto do outro
			_body.rotation.x = 0.2 * k
			_head.rotation.x = 0.22 * k
			_head.rotation.z = -0.2 * k
			_arms[1].rotation.x = -1.2 * k
			_arms[0].rotation.x = -0.5 * k
		"laugh": # #259: risada: a cabeça vai para trás e o tronco sacode
			_head.rotation.x = -0.35 * k
			_body.rotation.x = -0.08 * k + sin(_clock * 15.0) * 0.05 * k
			_arms[0].rotation.x = -0.5 * k + sin(_clock * 15.0) * 0.18 * k
			_arms[1].rotation.x = -0.35 * k - sin(_clock * 15.0) * 0.18 * k
		"argue": # #259: discussão: braços agitados e a cabeça balançando
			_arms[0].rotation.x = -1.6 * k + sin(_clock * 9.0) * 0.6 * k
			_arms[1].rotation.x = -1.4 * k + cos(_clock * 8.0) * 0.6 * k
			_head.rotation.y = sin(_clock * 7.0) * 0.4 * k
			_body.rotation.x = 0.1 * k
		"plead": # #259: pedido de desculpa: as mãos juntas à frente, a cabeça baixa
			_arms[0].rotation.x = -1.15 * k
			_arms[1].rotation.x = -1.15 * k
			_arms[0].rotation.z = 0.35 * k
			_arms[1].rotation.z = -0.35 * k
			_head.rotation.x = 0.38 * k
			_body.rotation.x = 0.1 * k
		"call": # #234: pedir ajuda: os dois braços para o alto, acenando
			_arms[0].rotation.x = -2.8 * k
			_arms[1].rotation.x = -2.9 * k
			_arms[0].rotation.z = -0.3 * k + sin(_clock * 11.0) * 0.35 * k
			_arms[1].rotation.z = 0.3 * k + sin(_clock * 11.0 + 1.0) * 0.35 * k
			_head.rotation.x = -0.25 * k


## #254: pisca a cada ~3,6 s (fase pela semente, para ninguém piscar junto); dormindo, de olhos fechados.
func _blink_eyes(asleep: bool) -> void:
	if _face == null or not _face.visible:
		return
	var cycle := fposmod(_clock + float(npc_id % 7) * 0.9, 3.6)
	var open := 0.12 if asleep else (1.0 if cycle > 0.16 else 1.0 - sin(cycle / 0.16 * PI) * 0.88)
	if absf(open - _blink) < 0.01:
		return
	_blink = open
	for eye in _face.get_meta("eyes", []):
		eye.scale.y = float(eye.get_meta("open", 1.0)) * open


func _apply_defense() -> void:
	var t := (_clock - _defense_from) / float(DEFENSE_TIME[_defense])
	var k := sin(clampf(t, 0.0, 1.0) * PI)
	match _defense:
		"dodge": # desvia para o lado e agacha
			_body.rotation.z = 0.35 * k
			_body.position.x = 0.14 * k
			_body.position.y = -0.1 * k
			_legs[0].rotation.x = 0.35 * k
			_legs[1].rotation.x = 0.35 * k
		"block": # braços à frente, ombros encolhidos
			_arms[0].rotation.x = -1.7 * k
			_arms[1].rotation.x = -1.7 * k
			_body.rotation.x = 0.18 * k
		"hurt": # o tranco do golpe: corpo e cabeça para trás, braços soltos
			_body.rotation.x = -0.32 * k
			_head.rotation.x = -0.35 * k
			_arms[0].rotation.x = 0.7 * k
			_arms[1].rotation.x = 0.7 * k
		"recoil": # um passo atrás
			_body.rotation.x = -0.14 * k
			_legs[0].rotation.x = 0.35 * k
			_legs[1].rotation.x = -0.3 * k


var _jump_from := -1.0
var _jump_air := 0.0
const JUMP_HEIGHT := 0.22 ## altura do salto em unidades do boneco (~0,4 m num adulto)


var _strike_until := -1.0


## #254: nós que a pose mexe; a troca de pose mistura o último desenho com a pose nova (sem estalo).
func _pose_nodes() -> Array[Node3D]:
	return [_body, _head, _arms[0], _arms[1], _legs[0], _legs[1]]


const POSE_BLEND_S := 0.28


func _animate(dt: float) -> void:
	var rig_on: bool = _rig != null and _rig.active
	var phase := _clock * 7.0 + float(npc_id)
	var lying: bool = String(spec.get("sick_pose", "")) == "bed"
	var moving := is_moving() and pose != "sleep" and not lying
	var effective := "walk" if moving else ("sleep" if lying else pose)
	if not is_moving() and face_point != Vector3.INF and effective != "sleep":
		var to := face_point - position
		if to.length() > 0.2:
			rotation.y = lerp_angle(rotation.y, atan2(to.x, to.z), 0.25)
	var key := "idle" if (rig_on and effective == "walk") else effective
	if key != _last_effective:
		if _last_effective != "": # guarda o desenho do quadro anterior (ainda não zerado) para misturar com a pose nova
			_blend_from.clear()
			for node in _pose_nodes():
				_blend_from.append([node.rotation, node.position])
			_blend = 0.0
		_last_effective = key
	_reset_pose()
	match key: # com rig a caminhada é uma camada (abaixo), não uma pose
		"walk":
			var swing := sin(phase * 1.3)
			_legs[0].rotation.x = swing * 0.7
			_legs[1].rotation.x = -swing * 0.7
			_arms[0].rotation.x = -swing * 0.6
			_arms[1].rotation.x = swing * 0.6
			_body.position.y = absf(sin(phase * 1.3)) * 0.04
			_body.rotation.x = 0.08
		"sleep":
			_body.rotation.x = -PI / 2.0
			_body.position = Vector3(0, 0.27, LYING_SHIFT - 0.28)
			_head.rotation.x = 0.2
			for i in _legs.size(): # deitadas junto ao quadril do corpo, em vez de em pé no chão
				_legs[i].position = Vector3(LEG_X * (-1 if i == 0 else 1), 0.27, LYING_SHIFT - 0.64)
				_legs[i].rotation.x = -PI / 2.0
			var zzz := _get_zzz()
			zzz.position = Vector3(0.1 + sin(_clock) * 0.05, 0.9 + fmod(_clock * 0.3, 0.6), 0)
			zzz.modulate.a = 1.0 - fmod(_clock * 0.3, 0.6) / 0.6
			_torso.scale.y = 1.0 + sin(_clock * 1.6) * 0.03
		"work":
			_arms[1].rotation.x = -1.2 + sin(phase * 1.1) * 0.9
			_arms[0].rotation.x = -0.5
			_body.rotation.x = 0.15 + sin(phase * 1.1) * 0.05
			_legs[0].rotation.x = 0.1
		"eat":
			_arms[1].rotation.x = -1.8 + sin(phase * 0.8) * 0.5
			_head.rotation.x = 0.12 + maxf(0.0, sin(phase * 0.8)) * 0.15
		"socialize":
			_head.rotation.y = sin(phase * 0.45) * 0.6
			_arms[0].rotation.x = -0.9 + sin(phase * 0.9) * 0.5
			_arms[1].rotation.x = -0.7 + cos(phase * 0.8) * 0.5
			_body.position.y = absf(sin(phase * 0.9)) * 0.02
			var dots := 1 + int(_clock * 2.0) % 3
			if _bubble_dots != dots: # AD-189: o texto só muda quando o número de pontos muda (refazer o texto custa)
				_bubble_dots = dots
				_get_bubble().text = ".".repeat(dots)
		"buy":
			_arms[1].rotation.x = -1.3
			_arms[0].rotation.x = -1.3
			_head.rotation.x = 0.1
		"steal": # furtivo: agachado, olha para os lados, um braço estendido e rápido para a frente
			_body.position.y = -0.06
			_body.rotation.x = 0.35
			_head.rotation.y = sin(phase * 0.9) * 0.9
			_arms[1].rotation.x = -1.4 + sin(phase * 2.2) * 0.25
			_arms[0].rotation.x = 0.3
			_legs[0].rotation.x = 0.45
			_legs[1].rotation.x = -0.2
		"cry": # cabeça baixa, mãos no rosto, ombros sacudindo
			_head.rotation.x = 0.45
			_arms[0].rotation.x = -2.3
			_arms[1].rotation.x = -2.3
			_arms[0].rotation.z = -0.35
			_arms[1].rotation.z = 0.35
			_body.position.y = absf(sin(phase * 2.6)) * 0.025
			_body.rotation.x = 0.12
		"power":
			_body.position.y = 0.12 + sin(phase * 0.6) * 0.06
			_arms[0].rotation.z = 0.9
			_arms[1].rotation.z = -0.9
			_shirt.emission = Color(0.4, 0.7, 1.0) * (0.5 + 0.5 * sin(phase * 0.7))
			_get_aura().scale = Vector3.ONE * (1.0 + sin(phase * 0.7) * 0.08)
		_:
			_torso.scale.y = 1.0 + sin(phase * 0.4) * 0.015
			_head.rotation.y = sin(phase * 0.15) * 0.4
			_arms[0].rotation.x = sin(phase * 0.4) * 0.05
			_arms[1].rotation.x = -sin(phase * 0.4) * 0.05
	_apply_posture(effective)
	var breath := sin(_clock * 1.7 + float(npc_id))
	_torso.scale *= Vector3(1.0 + 0.008 * breath, 1.0 + 0.014 * breath, 1.0 + 0.012 * breath) # respiração
	if rig_on:
		_rig.locomotion(self, dt, moving)
	if effective == "walk" and running and not rig_on: # #224: correr: passada mais rápida e larga, corpo inclinado
		var stride := sin(_clock * 15.0 + float(npc_id))
		_legs[0].rotation.x = stride * 1.0
		_legs[1].rotation.x = -stride * 1.0
		_arms[0].rotation.x = -stride * 0.9
		_arms[1].rotation.x = stride * 0.9
		_body.rotation.x = 0.25
		_body.position.y = absf(stride) * 0.06
	if gesture_playing() != "": # por cima da pose: o gesto do verbo aceito
		if rig_on:
			_rig.gesture(self)
		else:
			_apply_gesture()
	if _jump_from >= 0.0 and _clock < _jump_from + _jump_air: # por cima da pose: o pulo
		var t := (_clock - _jump_from) / _jump_air
		var lift := 4.0 * JUMP_HEIGHT * t * (1.0 - t)
		_body.position.y += lift
		if rig_on:
			_rig.air(lift, sin(t * PI))
		else:
			for i in _legs.size():
				_legs[i].position.y += lift
				_legs[i].rotation.x = -0.5 * sin(t * PI) * (1 if i == 0 else 0.6)
	if guarding: # #234: a guarda do motor: braços cruzados à frente, corpo firme
		_arms[0].rotation.x = -1.7
		_arms[1].rotation.x = -1.7
		_arms[0].rotation.z = 0.35
		_arms[1].rotation.z = -0.35
		_body.rotation.x = 0.12
	if defense_playing() != "":
		_apply_defense()
	if _clock < _strike_until: # por cima da pose: o golpe (antecipação, impacto, recuo)
		var k := 1.0 - (_strike_until - _clock) / STRIKE_TIME
		_arms[1].rotation.x = strike_arm(k)
		_body.rotation.x = lerpf(-0.1, 0.25, clampf(k / STRIKE_IMPACT, 0.0, 1.0)) * (1.0 - maxf(0.0, (k - STRIKE_IMPACT) / (1.0 - STRIKE_IMPACT)))
	if _blend < 1.0 and not _blend_from.is_empty():
		_blend = minf(1.0, _blend + dt / POSE_BLEND_S)
		var w := smoothstep(0.0, 1.0, _blend)
		var nodes := _pose_nodes()
		for i in nodes.size():
			nodes[i].rotation = Vector3(_blend_from[i][0]).lerp(nodes[i].rotation, w)
			nodes[i].position = Vector3(_blend_from[i][1]).lerp(nodes[i].position, w)
	_blink_eyes(effective == "sleep")
	if rig_on:
		_rig.finalize(self, effective == "sleep")
	if _ring != null and (_selected or _hovered):
		_ring.rotation.y = _clock * 1.5
