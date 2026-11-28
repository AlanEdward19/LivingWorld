extends RefCounted
## #254: malhas do corpo geradas por código (sem asset): torno (lathe) com normais suaves, membros afilados, mãos, sapatos, cabeça em
## ovo com pescoço, tronco com cintura e ombros, cabelos por estilo e barba. Cada forma é gerada uma vez e compartilhada entre todos os NPCs
## (cache por chave): o custo por NPC é só o nó, nunca a malha. Longe usa as mesmas malhas (poucas dezenas de vértices por peça).

static var _cache: Dictionary = {}
const SEGMENTS := 10


## Superfície de revolução em torno de Y. `profile` = pontos (raio, y) de baixo para cima; raio 0 nas pontas fecha a peça. `ellipse`
## achata a seção (x, z); `xf` leva a peça ao lugar (rotaciona o eixo, desloca); `arc` = [de, até] em radianos (0 = frente +z, PI = trás)
## para peças abertas (cortina de cabelo, barba). Normais pela inclinação do perfil: sombra lisa, sem facetas.
static func lathe(profile: Array, segments: int = SEGMENTS, ellipse: Vector2 = Vector2.ONE, xf: Transform3D = Transform3D.IDENTITY,
		arc: Vector2 = Vector2(0.0, TAU)) -> ArrayMesh:
	var rings := profile.size()
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var indices := PackedInt32Array()
	var full := absf(arc.y - arc.x) >= TAU - 0.001
	var columns := segments if full else segments + 1
	for i in rings:
		var p: Vector2 = profile[i]
		var before: Vector2 = profile[maxi(i - 1, 0)]
		var after: Vector2 = profile[mini(i + 1, rings - 1)]
		var tangent := (after - before).normalized() if after != before else Vector2.UP
		var n2 := Vector2(tangent.y, -tangent.x) # normal para fora no plano (raio, y)
		for j in columns:
			var a := arc.x + (arc.y - arc.x) * float(j) / float(segments)
			var dir := Vector3(sin(a), 0.0, cos(a))
			var pos := Vector3(dir.x * p.x * ellipse.x, p.y, dir.z * p.x * ellipse.y)
			var nrm := Vector3(dir.x * n2.x / ellipse.x, n2.y, dir.z * n2.x / ellipse.y).normalized()
			verts.append(xf * pos)
			normals.append((xf.basis * nrm).normalized())
	for i in rings - 1:
		for j in segments:
			var j2 := (j + 1) % columns if full else j + 1
			var a0 := i * columns + j
			var a1 := (i + 1) * columns + j
			var b0 := i * columns + j2
			var b1 := (i + 1) * columns + j2
			indices.append_array([a0, a1, b1, a0, b1, b0])
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


static func _cached(key: String, factory: Callable) -> Mesh:
	if not _cache.has(key):
		_cache[key] = factory.call()
	return _cache[key]


## Junta várias peças (mesma cor) numa malha só: [[malha, Transform3D], ...].
static func merged(parts: Array) -> ArrayMesh:
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	for entry in parts:
		tool.append_from(entry[0], 0, entry[1])
	return tool.commit()


static func _ellipsoid(rx: float, ry: float, rz: float, segments: int = 8) -> ArrayMesh:
	var profile: Array = []
	for k in range(0, 7):
		var t := PI * float(k) / 6.0
		profile.append(Vector2(sin(t) * rx, -cos(t) * ry))
	return lathe(profile, segments, Vector2(1.0, rz / rx))


## Tronco (centro em y 0,62; quadril em -0,26, ombro em +0,25): `kind` = male, female, child, elder. Cintura e ombros diferentes por sexo e
## idade; seção achatada na frente-trás. O pescoço entra pela cabeça.
static func torso(kind: String) -> Mesh:
	return _cached("torso-" + kind, func():
		var p: Array
		match kind:
			"female":
				p = [Vector2(0, -0.27), Vector2(0.10, -0.265), Vector2(0.158, -0.2), Vector2(0.162, -0.13), Vector2(0.125, -0.02),
					Vector2(0.12, 0.05), Vector2(0.146, 0.15), Vector2(0.158, 0.215), Vector2(0.12, 0.255), Vector2(0.06, 0.275), Vector2(0, 0.28)]
			"child":
				p = [Vector2(0, -0.27), Vector2(0.10, -0.265), Vector2(0.145, -0.2), Vector2(0.15, -0.08), Vector2(0.142, 0.05),
					Vector2(0.15, 0.17), Vector2(0.15, 0.22), Vector2(0.11, 0.255), Vector2(0.06, 0.275), Vector2(0, 0.28)]
			"elder":
				p = [Vector2(0, -0.27), Vector2(0.11, -0.265), Vector2(0.162, -0.2), Vector2(0.168, -0.1), Vector2(0.158, 0.0),
					Vector2(0.158, 0.1), Vector2(0.168, 0.19), Vector2(0.165, 0.225), Vector2(0.115, 0.255), Vector2(0.06, 0.275), Vector2(0, 0.28)]
			_:
				p = [Vector2(0, -0.27), Vector2(0.11, -0.265), Vector2(0.15, -0.2), Vector2(0.15, -0.12), Vector2(0.14, -0.03),
					Vector2(0.158, 0.08), Vector2(0.185, 0.18), Vector2(0.19, 0.225), Vector2(0.13, 0.258), Vector2(0.06, 0.275), Vector2(0, 0.28)]
		return lathe(p, 12, Vector2(1.0, 0.76)))


## Cabeça em ovo (testa larga, queixo mais estreito) já com o pescoço descendo; centro em 0, olhos em y +0,02.
static func head() -> Mesh:
	return _cached("head", func():
		var p: Array = [Vector2(0, -0.205), Vector2(0.044, -0.205), Vector2(0.046, -0.15), Vector2(0.05, -0.115), Vector2(0.075, -0.1),
			Vector2(0.105, -0.07), Vector2(0.123, -0.02), Vector2(0.13, 0.03), Vector2(0.124, 0.08), Vector2(0.1, 0.12), Vector2(0.06, 0.142),
			Vector2(0, 0.15)]
		return lathe(p, 14, Vector2(1.0, 0.96)))


## Membro afilado de `length` (centro em 0, topo em +length/2): ombro/coxa grossos, punho/tornozelo finos, pontas arredondadas.
static func limb(length: float, r_top: float, r_bottom: float) -> Mesh:
	return _cached("limb-%s-%s-%s" % [length, r_top, r_bottom], func():
		var h := length * 0.5
		var cap := minf(r_top, r_bottom) * 0.8
		var p: Array = [Vector2(0, -h), Vector2(r_bottom * 0.7, -h + cap * 0.2), Vector2(r_bottom, -h + cap),
			Vector2(lerpf(r_bottom, r_top, 0.5) * 1.04, 0.0), Vector2(r_top, h - cap), Vector2(r_top * 0.75, h - cap * 0.2), Vector2(0, h)]
		return lathe(p, 8))


## Mão em luva (mitene) com o polegar, pendurada a partir do punho (origem no punho, desce em -Y).
static func hand(skin_scale: float = 1.0) -> Mesh:
	return _cached("hand-%s" % skin_scale, func():
		var palm := _ellipsoid(0.034 * skin_scale, 0.05 * skin_scale, 0.02 * skin_scale)
		var thumb := _ellipsoid(0.011 * skin_scale, 0.024 * skin_scale, 0.011 * skin_scale, 6)
		return merged([[palm, Transform3D(Basis(), Vector3(0, -0.04, 0))],
			[thumb, Transform3D(Basis(Vector3.BACK, 0.5), Vector3(0.03 * skin_scale, -0.03, 0.006))]]))


## Sapato visto de lado: calcanhar redondo, bico afilado; origem no tornozelo, bico em +Z. Tênis (modern) e bota (medieval) diferem na altura.
static func shoe(boot: bool) -> Mesh:
	return _cached("shoe-%s" % boot, func():
		var p: Array = [Vector2(0, -0.062), Vector2(0.03, -0.056), Vector2(0.042, -0.03), Vector2(0.042, 0.03), Vector2(0.038, 0.06),
			Vector2(0.024, 0.086), Vector2(0, 0.096)]
		var sole := lathe(p, 8, Vector2(1.0, 0.55), Transform3D(Basis(Vector3.RIGHT, PI * 0.5), Vector3(0, 0, 0.03)))
		if not boot:
			return sole
		var shaft := lathe([Vector2(0.036, 0.0), Vector2(0.04, 0.03), Vector2(0.04, 0.07), Vector2(0.044, 0.09)], 8, Vector2.ONE,
			Transform3D(Basis(), Vector3(0, 0.0, -0.006)))
		return merged([[sole, Transform3D()], [shaft, Transform3D()]]))


## Cabelo por estilo, no espaço da cabeça (cabeça em 0, raio 0,13): "short", "buzz", "curly", "fringe" (calvície com franja), "long",
## "bob", "ponytail", "bun", "tuft" (bebê) e "none". Calota inclinada para trás (a testa fica livre).
static func hair(style: String) -> Mesh:
	return _cached("hair-" + style, func():
		var tilt := Transform3D(Basis(Vector3.RIGHT, -0.42), Vector3(0, 0.012, -0.008))
		match style:
			"buzz":
				return _dome(0.136, 0.154, tilt)
			"curly":
				return merged([[_dome(0.152, 0.172, tilt), Transform3D()],
					[_ellipsoid(0.055, 0.05, 0.055, 6), Transform3D(Basis(), Vector3(-0.1, 0.1, -0.02))],
					[_ellipsoid(0.055, 0.05, 0.055, 6), Transform3D(Basis(), Vector3(0.1, 0.1, -0.02))],
					[_ellipsoid(0.06, 0.05, 0.05, 6), Transform3D(Basis(), Vector3(0, 0.15, -0.04))]])
			"fringe":
				return lathe([Vector2(0.131, -0.06), Vector2(0.138, -0.02), Vector2(0.141, 0.04), Vector2(0.136, 0.075)], 10, Vector2(1.0, 0.98),
					Transform3D(Basis(Vector3.RIGHT, -0.2), Vector3(0, 0.0, -0.01)), Vector2(0.55 * PI, 1.45 * PI))
			"long":
				return merged([[_dome(0.146, 0.158, tilt), Transform3D()], [_curtain(-0.27), Transform3D()]])
			"bob":
				return merged([[_dome(0.146, 0.158, tilt), Transform3D()], [_curtain(-0.1), Transform3D()]])
			"ponytail":
				return merged([[_dome(0.145, 0.158, tilt), Transform3D()],
					[limb(0.17, 0.035, 0.012), Transform3D(Basis(Vector3.RIGHT, 0.55), Vector3(0, 0.0, -0.17))]])
			"bun":
				return merged([[_dome(0.145, 0.158, tilt), Transform3D()], [_ellipsoid(0.058, 0.052, 0.058, 8), Transform3D(Basis(), Vector3(0, 0.12, -0.09))]])
			"tuft":
				return _dome(0.1, 0.08, Transform3D(Basis(Vector3.RIGHT, -0.3), Vector3(0, 0.07, 0.0)))
		return _dome(0.145, 0.158, tilt))


static func _dome(radius: float, top: float, xf: Transform3D) -> ArrayMesh:
	var lower := 0.03 # a linha do cabelo na frente fica acima dos olhos e das sobrancelhas depois da inclinação
	var p: Array = [Vector2(radius * 0.97, lower - 0.035)]
	for k in range(0, 7):
		var t := (PI * 0.5) * float(k) / 6.0 # equador -> topo
		p.append(Vector2(cos(t) * radius, lower + sin(t) * (top - lower)))
	p.append(Vector2(0, top))
	return lathe(p, 12, Vector2(1.0, 1.02), xf)


## Cortina de cabelo atrás da cabeça, descendo até `bottom` (y da cabeça): só o semicírculo de trás.
static func _curtain(bottom: float) -> ArrayMesh:
	var p: Array = [Vector2(0.128, 0.05), Vector2(0.15, -0.02), Vector2(0.152, bottom * 0.5), Vector2(0.146, bottom * 0.85), Vector2(0.125, bottom)]
	return lathe(p, 10, Vector2(1.0, 1.0), Transform3D(Basis(), Vector3(0, 0, -0.012)), Vector2(0.52 * PI, 1.48 * PI))


## Barba cheia (queixo e queixadas), no espaço da cabeça; só o arco da frente, sob a boca.
static func beard(full: bool) -> Mesh:
	return _cached("beard-%s" % full, func():
		var bottom := -0.19 if full else -0.15
		var p: Array = [Vector2(0.118, -0.06), Vector2(0.126, -0.1), Vector2(0.12, bottom * 0.8), Vector2(0.09, bottom), Vector2(0.04, bottom - 0.01)]
		return lathe(p, 10, Vector2(1.0, 1.0), Transform3D(Basis(), Vector3(0, 0, 0.004)), Vector2(-0.62 * PI, 0.62 * PI)))


## Tubo fino que segue uma curva no plano XY (sobrancelha, boca): afina nas pontas.
static func curve_tube(points: Array, radius: float, sides: int = 5) -> ArrayMesh:
	var verts := PackedVector3Array()
	var normals := PackedVector3Array()
	var indices := PackedInt32Array()
	var n := points.size()
	for i in n:
		var p: Vector2 = points[i]
		var t: Vector2 = (points[mini(i + 1, n - 1)] - points[maxi(i - 1, 0)]).normalized()
		var nrm := Vector2(-t.y, t.x)
		var r := radius * (0.55 + 0.45 * sin(PI * float(i) / float(n - 1)))
		for k in sides:
			var a := TAU * float(k) / float(sides)
			verts.append(Vector3(p.x + nrm.x * cos(a) * r, p.y + nrm.y * cos(a) * r, sin(a) * r))
			normals.append(Vector3(nrm.x * cos(a), nrm.y * cos(a), sin(a)).normalized())
	for i in n - 1:
		for k in sides:
			var k2 := (k + 1) % sides
			var a0 := i * sides + k
			var a1 := (i + 1) * sides + k
			var b0 := i * sides + k2
			var b1 := (i + 1) * sides + k2
			indices.append_array([a0, a1, b1, a0, b1, b0])
	var arrays := []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = verts
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_INDEX] = indices
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
	return mesh


## Arco de `width` (metade) com `bend` (>0 as pontas sobem: sorriso/sobrancelha caída; <0 desce): a boca e a sobrancelha.
static func arc(width: float, bend: float, radius: float) -> Mesh:
	return _cached("arc-%s-%s-%s" % [width, bend, radius], func():
		var pts: Array = []
		for k in 7:
			var x := lerpf(-width, width, float(k) / 6.0)
			pts.append(Vector2(x, bend * (x / width) * (x / width)))
		return curve_tube(pts, radius))


static func eye_white() -> Mesh:
	return _cached("eye-white", func(): return _ellipsoid(0.024, 0.02, 0.012, 8))


static func iris() -> Mesh:
	return _cached("eye-iris", func(): return _ellipsoid(0.0125, 0.0125, 0.005, 8))


static func pupil() -> Mesh:
	return _cached("eye-pupil", func(): return _ellipsoid(0.0065, 0.0065, 0.003, 6))


static func nose() -> Mesh:
	return _cached("nose", func(): return merged([[_ellipsoid(0.011, 0.02, 0.017, 6), Transform3D(Basis(), Vector3(0, 0.0, 0))],
		[_ellipsoid(0.017, 0.011, 0.012, 6), Transform3D(Basis(), Vector3(0, -0.014, 0.004))]]))


static func ear() -> Mesh:
	return _cached("ear", func(): return _ellipsoid(0.011, 0.026, 0.02, 6))


static func open_mouth(wide: float) -> Mesh:
	return _cached("open-mouth-%s" % wide, func(): return _ellipsoid(wide, wide * 1.3, 0.008, 8))


static func pelvis() -> Mesh:
	return _cached("pelvis", func(): return lathe([Vector2(0, -0.075), Vector2(0.1, -0.065), Vector2(0.14, -0.01), Vector2(0.138, 0.05),
		Vector2(0.1, 0.075), Vector2(0, 0.08)], 10, Vector2(1.0, 0.72)))


## Saia/túnica: tronco de cone aberto embaixo, achatado como o tronco; origem no cós, desce `length` (em -Y).
static func skirt(length: float, flare: float) -> Mesh:
	return _cached("skirt-%s-%s" % [length, flare], func():
		return lathe([Vector2(0.15 + flare, -length), Vector2(0.15 + flare * 0.5, -length * 0.55), Vector2(0.147, 0.0), Vector2(0.1, 0.01)], 12,
			Vector2(1.0, 0.8)))


static func shoulder() -> Mesh:
	return _cached("shoulder", func(): return _ellipsoid(0.05, 0.048, 0.048, 8))
