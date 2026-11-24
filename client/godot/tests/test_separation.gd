extends "res://tests/test_base.gd"
## AD-197r: pessoas sem se sobrepor, só no desenho (a única exceção liberada pelo dono à regra "o cliente só desenha o que o motor diz":
## "os npcs não se esbarrarem, e contornarem, isso ok ficar no visual apenas"). Determinístico: lugar por id, nada aleatório.

const T := preload("res://tests/test_inspector.gd")
const O := preload("res://tests/observable_fixtures.gd")
const NpcRenderer := preload("res://Scenes/render/npc_renderer.gd")
const NpcActor := preload("res://Scenes/npc_actor.gd")


func test_every_group_size_keeps_bodies_apart_deterministically() -> void:
	for count in range(2, 31):
		var ids: Array = []
		for i in count:
			ids.append(100 + i * 7)
		var offsets := NpcRenderer.spread_offsets(ids)
		var worst := 99.0
		for a in offsets:
			for b in offsets:
				if a != b:
					worst = minf(worst, (offsets[a] as Vector3).distance_to(offsets[b]))
		check(worst >= NpcRenderer.PERSONAL - 0.02, "grupo de %d: o mais perto está a %s" % [count, worst])
		eq(NpcRenderer.spread_offsets(ids.duplicate()), offsets, "mesmo grupo, mesmos lugares (%d)" % count)
		var reversed := ids.duplicate()
		reversed.reverse()
		eq(NpcRenderer.spread_offsets(reversed), offsets, "a ordem de chegada não muda o lugar (%d)" % count)


## Cinco no mesmo local de trabalho: cada um no seu lugar; o estado do motor (célula) não muda.
func test_five_people_in_one_workplace_cell_are_apart_and_the_engine_state_is_untouched() -> void:
	var app = await T.make_app(tree, O.transport())
	await app.enter_scope("City", "city-a")
	var upserts: Array = []
	for i in 5:
		upserts.append({"id": {"value": 50 + i}, "location": {"x": 4, "y": 4}, "currentAction": 2})
	app.stream.delta_applied.emit(O.delta({"npcUpserts": upserts}))
	for i in 60:
		app.world_view.npcs.advance(0.05, Vector3.ZERO, 40.0, false, false)
	var worst := 99.0
	for i in 5:
		eq(app.store.npcs[50 + i].cell, Vector2i(4, 4), "a célula é a do motor")
		eq(app.world_view.npc_nodes[50 + i].cell, Vector2i(4, 4), "o boneco segue na célula do motor")
		for j in range(i + 1, 5):
			var a: Vector3 = app.world_view.npc_nodes[50 + i].position
			var b: Vector3 = app.world_view.npc_nodes[50 + j].position
			worst = minf(worst, Vector2(a.x, a.z).distance_to(Vector2(b.x, b.z)))
	check(worst >= NpcRenderer.PERSONAL - 0.02, "ninguém empilhado: o mais perto está a %s" % worst)
	var snapshot: Dictionary = app.store.npcs[50].duplicate(true)
	for i in 30:
		app.world_view.npcs.advance(0.05, Vector3.ZERO, 40.0, false, false)
	eq(app.store.npcs[50], snapshot, "desenhar mais quadros não mexe no estado do motor")
	app.queue_free()


## Quem anda contorna quem está parado no caminho (só no desenho) e chega ao lugar exato.
func test_a_walker_goes_around_someone_standing_in_the_way() -> void:
	var renderer := NpcRenderer.new()
	tree.root.add_child(renderer)
	var walker := NpcActor.new()
	var standing := NpcActor.new()
	renderer.add_child(walker)
	renderer.add_child(standing)
	walker.setup(1, Vector2i(0, 0), 4, null)
	standing.setup(2, Vector2i(1, 0), 4, null)
	renderer.nodes = {1: walker, 2: standing}
	renderer._frame = NpcRenderer.STILL_EVERY # o índice de parados já existe quando ela sai
	renderer._separate()
	walker.move_to(Vector2i(2, 0), 2.0)
	var closest := 99.0
	for step in 50:
		renderer._frame = NpcRenderer.STILL_EVERY + step + 1
		renderer._separate()
		walker.advance_motion(0.05)
		closest = minf(closest, Vector2(walker.position.x, walker.position.z).distance_to(Vector2(standing.position.x, standing.position.z)))
	check(closest >= 0.4, "passa ao lado, sem atravessar: o mais perto foi %s" % closest)
	check(walker.position.is_equal_approx(Vector3(2.5, 0, 0.5)), "e chega ao lugar exato: %s" % walker.position)
	eq(walker.cell, Vector2i(2, 0), "a célula do motor não mudou pelo desvio")
	eq(standing.position, Vector3(1.5, 0, 0.5), "quem está parado não é empurrado")
	renderer.queue_free()


func test_steering_is_lateral_deterministic_and_zero_when_nobody_is_near() -> void:
	var still := {NpcRenderer._key(1.0, 0.05): [Vector4(1.0, 0, 0.05, 9.0)]}
	var a := NpcRenderer.steer_away(Vector3(0.8, 0, 0), Vector3(1, 0, 0), still)
	eq(a, NpcRenderer.steer_away(Vector3(0.8, 0, 0), Vector3(1, 0, 0), still), "mesma entrada, mesmo desvio")
	check(absf(a.x) < 0.001 and a.z < -0.1, "lateral e para o lado oposto ao do parado: %s" % a)
	eq(NpcRenderer.steer_away(Vector3(5, 0, 5), Vector3(1, 0, 0), still), Vector3.ZERO, "longe de todos: sem desvio")
	eq(NpcRenderer.steer_away(Vector3(0.8, 0, 0), Vector3.ZERO, still), Vector3.ZERO, "parado não desvia")
