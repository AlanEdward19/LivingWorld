extends "res://tests/test_base.gd"
## #246 (teste de jogo de UX do sandbox): a placa 3D da cidade não cobre a tela de quem joga, o nome do bairro vira aviso discreto, casa de um
## andar não mostra "Andar 1 de 1" e a pausa não deixa avisos do topo por baixo do painel.

const CityRenderer := preload("res://Scenes/render/city_renderer.gd")
const Store := preload("res://Store/world_store.gd")
const InteriorStage := preload("res://Scenes/interior_stage.gd")
const InteriorViewTest := preload("res://tests/test_interior_view.gd")
const ActionLabels := preload("res://Store/action_labels.gd")
const T := preload("res://tests/test_inspector.gd")


func _store_with_city() -> Store:
	var store := Store.new()
	store.kind = "World"
	store.cities = {"city-a": {"id": "city-a", "name": "Jardim das Acácias", "population": 2, "bounds": Rect2i(10, 14, 3, 3), "location": Vector2i(11, 15),
		"density": {"inside": 1, "outdoors": 0, "pooled": 0, "temperature": 12, "actions": [{"action": -1, "count": 1}]}}}
	return store


func test_the_city_sign_can_be_hidden_and_stays_hidden_after_a_redraw() -> void:
	var cities := CityRenderer.new()
	cities.rebuild(_store_with_city())
	var label: Label3D = cities.nodes["city-a"].get_node("CityLabel")
	eq(label.visible, true, "o espectador vê a placa")

	cities.set_labels_visible(false)
	eq(label.visible, false, "no modo jogador a placa some")
	cities.rebuild(_store_with_city())
	eq(cities.nodes["city-a"].get_node("CityLabel").visible, false, "e continua sumida depois de redesenhar")

	cities.set_labels_visible(true)
	eq(cities.nodes["city-a"].get_node("CityLabel").visible, true, "ao sair do personagem a placa volta")
	cities.free()


func test_entering_player_mode_announces_the_neighbourhood_as_a_discreet_toast() -> void:
	var app = await T.make_app(tree, T.transport())
	app.store.kind = "City"
	app.store.city = {"id": "city-a", "name": "Jardim das Acácias"}
	app.set_player_mode(true)

	check("Jardim das Acácias" in app.toasts.visible_texts(), "o nome do bairro aparece como aviso: %s" % [app.toasts.visible_texts()])
	eq(app.world_view.cities.labels_visible, false, "e a placa 3D da cidade some")

	app.set_player_mode(false)
	eq(app.world_view.cities.labels_visible, true, "voltando a observar, a placa volta")
	app.queue_free()


func test_a_one_floor_house_does_not_say_floor_one_of_one() -> void:
	var info: Dictionary = InteriorViewTest.new()._info()
	var stage := InteriorStage.new()
	stage.world = Node3D.new()
	var camera := Camera3D.new()
	stage.enter(info, Vector2i(10, 10), ActionLabels.new(), camera)
	eq(stage.floor_label.visible, true, "três andares: o rótulo do andar aparece")
	stage.leave()

	info.floors = 1
	info.layout = [info.layout[0]]
	stage.enter(info, Vector2i(10, 10), ActionLabels.new(), camera)
	eq(stage.floor_label.visible, false, "um andar só: sem rótulo que cobre as dicas de teclas")
	stage.leave()
	stage.world.free()
	stage.free()
	camera.free()


func test_the_pause_menu_hides_the_top_notices_while_open() -> void:
	var app = await T.make_app(tree, T.transport())
	app.toasts.push("Um aviso qualquer")
	await app.pause_menu.open()
	eq(app.toasts.visible, false, "os avisos não ficam por baixo do título da pausa")
	await app.pause_menu.close()
	eq(app.toasts.visible, true, "e voltam ao continuar")
	app.queue_free()


func test_the_key_hint_strip_fades_after_a_while_of_play() -> void:
	var app = await T.make_app(tree, T.transport())
	app.player_mode = true
	await app.fade_key_hints(0.05, 0.1)
	await tree.create_timer(0.4).timeout
	check(app.player_frame.hints.modulate.a < 0.4, "a faixa de teclas recua: %s" % app.player_frame.hints.modulate.a)
	app.queue_free()
