extends "res://tests/test_base.gd"
## #250: a câmera do jogador DENTRO da casa (estilo The Sims): sobe e olha o cômodo em diagonal, de cima das paredes; parede e móvel entre
## ela e o corpo ficam translúcidos; entrar e sair não têm salto; o mouse gira em volta do corpo e WASD segue a câmera.

const CameraRig := preload("res://Scenes/camera_rig.gd")
const InteriorCamera := preload("res://Scenes/interior_camera.gd")
const InteriorView := preload("res://Scenes/interior_view.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const Grid := preload("res://Scenes/grid.gd")


## A casa em foco como o app a entrega ao rig: [-1, caixa do andar, caixa, painéis finos, true]; `size` em unidades da grade.
func _entry(size: float, origin: Vector3 = Vector3.ZERO) -> Array:
	var h := Grid.floor_height()
	var thick := 0.05
	var walls: Array = [
		AABB(origin, Vector3(size, h, thick)), AABB(origin + Vector3(0, 0, size - thick), Vector3(size, h, thick)),
		AABB(origin, Vector3(thick, h, size)), AABB(origin + Vector3(size - thick, 0, 0), Vector3(thick, h, size))]
	var box := AABB(origin, Vector3(size, h, size))
	return [-1, box, box, walls, true]


## Um andar de 3 m (2 unidades), como o motor manda; os testes restauram o valor de antes.
func _real_floor() -> float:
	var kept := Grid.floor_meters
	Grid.floor_meters = 3.0
	return kept


func _camera_world(rig) -> Transform3D:
	return rig.transform * rig._pivot.transform * rig.camera.transform


func _rig(entries: Array, body: Node3D, yaw: float = 0.0, pitch_deg: float = -15.0) -> CameraRig:
	var rig := CameraRig.new()
	rig.add_child(body)
	rig.blockers = func() -> Array: return entries
	rig.set_mode(rig.THIRD)
	rig.yaw = yaw
	rig.pitch = deg_to_rad(pitch_deg)
	rig.follow(body)
	return rig


func _settle(rig: CameraRig, frames: int = 200) -> void:
	for i in frames:
		rig._process(0.016)


## Dentro de casas de 2 a 8 células de lado, em qualquer giro: a câmera fica acima das paredes (ou dentro da planta), olha o corpo de
## cima na faixa de inclinação e cabe no recorte da tela; o plano vem do tamanho da planta (cômodo grande: mais baixo e mais longe).
func test_inside_the_house_the_camera_looks_from_above_the_walls_and_the_body_is_framed() -> void:
	var kept_floor := _real_floor()
	var arms := {}
	for size in [2.0, 3.0, 4.0, 8.0]:
		var entry := _entry(size)
		for yaw in [0.0, 1.3, 3.0, -2.0]:
			for pitch_deg in [0.0, -15.0, -60.0]:
				var body := Node3D.new()
				body.position = Vector3(size / 2.0, 0, size / 2.0)
				var rig := _rig([entry], body, yaw, pitch_deg)
				_settle(rig)
				var cam := _camera_world(rig)
				var tag := "casa %s, giro %s, olhar de rua %s" % [size, yaw, pitch_deg]
				var box: AABB = entry[1]
				var head: Vector3 = body.position + Vector3(0, rig.eye_height(), 0)
				var above := cam.origin.y >= box.end.y - 0.001
				check(above or box.has_point(cam.origin), "acima das paredes ou dentro da planta (%s): %s" % [cam.origin, tag])
				var down := rad_to_deg(asin(clampf(-(-cam.basis.z).y, -1.0, 1.0)))
				check(down >= 20.0 and down <= 66.0, "olha o corpo de cima, em diagonal (%s°): %s" % [snappedf(down, 0.1), tag])
				var to_head := (head - cam.origin).normalized()
				check((-cam.basis.z).dot(to_head) > 0.97, "o corpo está no centro da vista: %s" % tag)
				check(cam.origin.distance_to(head) >= CameraRig.min_arm() * 0.6, "nunca colada na cabeça: %s" % tag)
				if absf(yaw) < 0.01 and pitch_deg == -15.0:
					arms[size] = rig.arm
				rig.free()
	check(float(arms[8.0]) > float(arms[2.0]), "o braço cresce com a planta (%s > %s)" % [arms[8.0], arms[2.0]])
	check(InteriorCamera.default_pitch(_entry(8.0)) > InteriorCamera.default_pitch(_entry(2.0)), "cômodo grande olha mais de frente (menos para baixo)")
	var covered := CameraRig.covering(Vector3(2, 1.4, 5), Vector3(2, 1, 2), [_entry(4.0)])
	check(covered.is_empty(), "a casa em foco não vira translúcida como prédio do mundo")
	Grid.floor_meters = kept_floor


## A casa de verdade (3×3 do motor) com a câmera do rig e o corpo adotado: nada opaco entre a câmera e o peito (parede, móvel), e o que
## está atrás do corpo continua opaco (a planta se lê). Roda a conta do jogo: rig -> `update_near`.
func test_nothing_opaque_stands_between_the_camera_and_the_chest_walls_and_furniture() -> void:
	var kept_floor := _real_floor()
	var cells: Array = []
	var walls: Array = []
	for y in 3:
		for x in 3:
			cells.append({"cell": {"x": x, "y": y}, "material": 3})
	for k in 3:
		walls.append({"a": {"x": k, "y": 0}, "b": {"x": k, "y": 1}, "material": 1}) # topo
		walls.append({"a": {"x": k, "y": 3}, "b": {"x": k, "y": 4}, "material": 1}) # fundo (as arestas do contorno, como o motor manda)
	for yaw in [0.0, 1.6, 3.1, -1.6]:
		var view := InteriorView.new()
		tree.root.add_child(view)
		view.immersive = true
		view.show_interior({"building": 1, "floors": 1, "layout": [{"level": 0, "cells": cells, "walls": walls, "doors": []}], "occupants": []}, Vector2i(0, 0), ActionLabels.new())
		var actor := NpcActor.new()
		actor.setup(1, Vector2i(1, 1), -1, ActionLabels.new())
		tree.root.add_child(actor)
		actor.position = Vector3(1.5, 0, 1.5)
		view.guide_body(1, actor, 0)
		var rig := CameraRig.new()
		rig.add_child(Node3D.new())
		tree.root.add_child(rig)
		rig.blockers = func() -> Array: return [[-1, view.floor_box(), view.floor_box(), view.wall_boxes(), true]]
		rig.set_mode(rig.THIRD)
		rig.yaw = yaw
		rig.follow(actor)
		_settle(rig)
		var cam := _camera_world(rig).origin
		var chest := actor.position + Vector3(0, view.wall_height() * 0.55, 0)
		var toward := (cam - chest).normalized()
		# um móvel entre a câmera e o corpo e outro atrás dele
		var between := _piece(view, chest + toward * 0.7)
		var behind := _piece(view, chest - toward * 0.9)
		view.set_view_direction(Vector3.ZERO)
		view.update_near(cam)
		var tag := "giro %s" % yaw
		for wall in view._walls:
			if wall.get("lintel", false):
				continue
			var node: MeshInstance3D = wall.node
			if InteriorView._segment_hits_box(cam, chest, node.position, (wall.mesh as BoxMesh).size * 0.5):
				check(node.transparency > 0.8, "parede entre a câmera e o peito é translúcida (%s): %s" % [node.transparency, tag])
		check((between.get_child(0) as MeshInstance3D).transparency > 0.8, "móvel entre a câmera e o corpo é translúcido: %s" % tag)
		check((behind.get_child(0) as MeshInstance3D).transparency < 0.5, "móvel atrás do corpo segue opaco: %s" % tag)
		view.set_view_direction(Vector3.ZERO)
		view.release_guided()
		view.queue_free()
		actor.queue_free()
		rig.queue_free()
	Grid.floor_meters = kept_floor


func _piece(view: InteriorView, at: Vector3) -> Node3D:
	var holder := Node3D.new()
	var mesh := MeshInstance3D.new()
	mesh.mesh = BoxMesh.new()
	(mesh.mesh as BoxMesh).size = Vector3(0.4, 0.5, 0.4)
	holder.add_child(mesh)
	holder.position = at
	view.add_child(holder)
	view._register_piece(holder)
	return holder


## Atravessar a porta (entrar e sair) não dá salto de câmera: a posição muda no ritmo do corpo e da mola, quadro a quadro; e ao sair o
## olhar de rua de antes volta (o de dentro não o altera).
func test_entering_and_leaving_the_house_has_no_camera_jump_and_restores_the_street_view() -> void:
	var kept_floor := _real_floor()
	var entry := _entry(4.0, Vector3(0, 0, 0))
	var body := Node3D.new()
	body.position = Vector3(-4.0, 0, 2.0)
	var rig := _rig([entry], body, deg_to_rad(-90.0), -15.0) # de frente para a casa: o corpo anda para +x
	_settle(rig)
	var street_pitch := rig.view_pitch
	var last := _camera_world(rig).origin
	var worst := 0.0
	var step := 0.05 # 3 células/s a 60 quadros
	var inside_seen := false
	for i in 400:
		body.position.x += step if i < 200 else -step
		rig._process(0.016)
		var now := _camera_world(rig).origin
		worst = maxf(worst, now.distance_to(last))
		last = now
		inside_seen = inside_seen or rig._indoor
	check(inside_seen, "o corpo esteve dentro da casa")
	check(worst < 0.2, "nenhum quadro move a câmera mais de 0,2 célula (%s)" % worst)
	_settle(rig)
	check(not rig._indoor, "fora de novo")
	check(absf(rig.view_pitch - street_pitch) < deg_to_rad(1.0), "o olhar de rua voltou (%s° vs %s°)" % [rad_to_deg(rig.view_pitch), rad_to_deg(street_pitch)])
	check(is_equal_approx(rig.pitch, deg_to_rad(-15.0)), "o mouse de dentro não mexeu no olhar de rua guardado")
	rig.free()
	Grid.floor_meters = kept_floor


## Dentro da casa o mouse gira em volta do corpo e WASD (rig.heading) continua relativo à câmera; o scroll aproxima; o mouse inclina só
## dentro da faixa; o olhar de rua não muda.
func test_inside_the_house_the_mouse_orbits_the_body_and_the_walk_direction_follows_the_camera() -> void:
	var kept_floor := _real_floor()
	var entry := _entry(5.0)
	var body := Node3D.new()
	body.position = Vector3(2.5, 0, 2.5)
	var rig := _rig([entry], body, 0.0, -15.0)
	_settle(rig)
	check(rig._indoor, "dentro da casa")
	var head: Vector3 = body.position + Vector3(0, rig.eye_height(), 0)
	var before := _camera_world(rig).origin
	for turn in [Vector2(400, 0), Vector2(-900, 0), Vector2(0, 5000), Vector2(0, -5000)]:
		rig.look(turn)
		_settle(rig, 120)
		var cam := _camera_world(rig)
		var forward := Vector2((-cam.basis.z).x, (-cam.basis.z).z).normalized()
		check(forward.dot(rig.heading()) > 0.99, "WASD relativo à câmera depois de girar (%s vs %s)" % [forward, rig.heading()])
		var down := rad_to_deg(asin(clampf(-(-cam.basis.z).y, -1.0, 1.0)))
		check(down >= 20.0 and down <= 66.0, "a inclinação fica na faixa de dentro de casa (%s°)" % snappedf(down, 0.1))
	check(is_equal_approx(rig.pitch, deg_to_rad(-15.0)), "o olhar de rua não mudou")
	var moved := _camera_world(rig).origin
	check(moved.distance_to(before) > 0.5, "a câmera deu a volta no corpo")
	var radius_before := Vector2(moved.x - head.x, moved.z - head.z).length()
	check(radius_before > 0.1, "a câmera fica a uma distância do corpo")
	var far := rig.arm
	rig.zoom_third(0.5)
	_settle(rig)
	check(rig.arm < far * 0.8, "o scroll aproxima (%s < %s)" % [rig.arm, far])
	rig.toggle_person()
	_settle(rig)
	check(rig.mode == rig.FIRST, "a 1ª pessoa segue disponível dentro de casa")
	rig.free()
	Grid.floor_meters = kept_floor


## #268: a planta com cômodos (`entry[5]`): dois cômodos lado a lado dentro de uma casa de 8 x 4 células.
func _entry_rooms() -> Array:
	var entry := _entry(8.0)
	var h := Grid.floor_height()
	entry[1] = AABB(Vector3.ZERO, Vector3(8, h, 4))
	entry[2] = entry[1]
	entry.append([AABB(Vector3.ZERO, Vector3(3, h, 4)), AABB(Vector3(3, 0, 0), Vector3(5, h, 4))])
	return entry


## O enquadramento vem do cômodo do corpo (planta do motor): o cômodo que contém o olho; na porta (fora de todos), o mais perto; sem cômodos, a casa.
func test_the_frame_is_the_room_of_the_body_from_the_engine_plan() -> void:
	var entry := _entry_rooms()
	check(InteriorCamera.focus(Vector3(1, 1, 2), entry).size.x == 3.0, "no cômodo pequeno, o cômodo pequeno")
	check(InteriorCamera.focus(Vector3(6, 1, 2), entry).size.x == 5.0, "no grande, o grande")
	check(InteriorCamera.focus(Vector3(3.2, 1, 5.0), entry).position.x == 3.0, "fora de todos (soleira): o mais perto")
	check(InteriorCamera.focus(Vector3(1, 1, 1), _entry(4.0)).size.x == 4.0, "sem cômodos vale a casa inteira")
	var small := InteriorCamera.span(entry, Vector3(1.5, 1, 2))
	var large := InteriorCamera.span(entry, Vector3(5.5, 1, 2))
	check(small < large, "o cômodo pequeno pede enquadramento menor que o grande")
	check(InteriorCamera.span(entry, Vector3(0.2, 1, 0.2)) > InteriorCamera.span(entry, Vector3(1.5, 1, 2)), "o corpo no canto: o canto mais longe manda (corpo no centro da tela)")
	check(InteriorCamera.default_pitch(entry, Vector3(1.5, 1, 2)) < InteriorCamera.default_pitch(entry, Vector3(5.5, 1, 2)) + 0.0001, "cômodo pequeno olha mais para baixo")


## Com a câmera de verdade: o corpo no centro e o cômodo inteiro (os quatro cantos do chão) dentro da tela, nos dois cômodos e em giros diferentes;
## atravessar a porta não dá salto de câmera (o braço muda suave).
func test_the_body_is_centered_and_the_whole_room_is_in_view_and_crossing_rooms_is_smooth() -> void:
	var kept_floor := _real_floor()
	var entry := _entry_rooms()
	for spot in [Vector3(1.5, 0, 2.0), Vector3(5.5, 0, 2.0)]:
		for yaw in [0.0, 1.3, 3.0]:
			var body := Node3D.new()
			body.position = spot
			var rig := _rig([entry], body, yaw)
			_settle(rig)
			var world := _camera_world(rig)
			var tan_v := tan(deg_to_rad(rig.camera.fov) * 0.5)
			var room := InteriorCamera.focus(spot + Vector3(0, 1, 0), entry)
			for corner in [Vector3(room.position.x, 0, room.position.z), Vector3(room.end.x, 0, room.position.z), Vector3(room.position.x, 0, room.end.z), Vector3(room.end.x, 0, room.end.z)]:
				var local: Vector3 = world.affine_inverse() * corner # tela 16:9, campo vertical do rig
				var ahead: float = -local.z
				check(ahead > 0.0 and absf(local.y / ahead) < tan_v and absf(local.x / ahead) < tan_v * 16.0 / 9.0,
					"o canto %s do cômodo cabe na tela (corpo %s, giro %s)" % [corner, spot, yaw])
			rig.free()
	var walker := Node3D.new()
	walker.position = Vector3(1.5, 0, 2.0)
	var crossing := _rig([entry], walker, 0.5)
	_settle(crossing)
	var worst := 0.0
	var last: float = crossing.arm
	for i in 200:
		walker.position.x += 0.03 # anda 1,8 m/s no ritmo de 60 quadros: atravessa a porta entre os cômodos
		crossing._process(0.016)
		worst = maxf(worst, absf(crossing.arm - last))
		last = crossing.arm
	check(worst < 0.09, "atravessar de um cômodo ao outro não dá salto de braço (pior passo %s)" % worst)
	check(crossing.arm > 0.0, "o braço é válido")
	crossing.free()
	Grid.floor_meters = kept_floor
