extends "res://tests/test_base.gd"
## #269 (6º teste de jogo): rótulo de prédio menor e nítido, câmera de rua na soleira, a própria casa entra ao chegar à porta e os avisos honestos. O cliente só desenha o que o motor diz.

const BuildingLabels := preload("res://Scenes/render/building_labels.gd")
const PlayerHud := preload("res://Scenes/player_hud.gd")
const CameraRig := preload("res://Scenes/camera_rig.gd")
const Grid := preload("res://Scenes/grid.gd")


func test_a_building_label_never_grows_past_its_screen_size_when_the_camera_is_close_and_keeps_the_world_size_from_afar() -> void:
	var label := Label3D.new()
	label.font_size = 40
	label.pixel_size = 0.0045
	var original := label.pixel_size
	BuildingLabels.fit_to_screen(label, 20.0) # câmera longe: poucos pixels por metro
	check(is_equal_approx(label.pixel_size, original * 40.0 / float(BuildingLabels.GLYPH_PX)), "de longe o texto tem o tamanho de sempre no mundo (%s)" % label.pixel_size)
	eq(label.font_size, BuildingLabels.GLYPH_PX, "a fonte é desenhada grande: não borra ao encolher")
	BuildingLabels.fit_to_screen(label, 600.0) # câmera colada na fachada: muitos pixels por metro
	var tall_px := float(label.font_size) * label.pixel_size * 600.0
	check(tall_px <= BuildingLabels.SCREEN_TEXT_PX + 0.01, "colada, o texto tem no máximo %d px de altura na tela (%s)" % [int(BuildingLabels.SCREEN_TEXT_PX), tall_px])
	BuildingLabels.fit_to_screen(label, 20.0)
	check(is_equal_approx(label.pixel_size, original * 40.0 / float(BuildingLabels.GLYPH_PX)), "voltando a afastar, volta ao tamanho do mundo (sem lembrar do encolhido)")
	label.free()


func test_the_street_camera_on_the_doorstep_keeps_a_minimum_distance_so_the_head_does_not_cover_the_screen() -> void:
	var boxes := [[3, AABB(Vector3(-5, 0, 0.1), Vector3(10, 5, 3))]] # a fachada nas costas do corpo: a câmera não tem para onde recuar
	var eye := Vector3(0, 1.4, 0)
	var plan := CameraRig.plan_third(eye, 0.0, 0.0, CameraRig.third_distance(), boxes, 1.0)
	var spot := eye + Vector3(0, plan.lift, 0) + Vector3(0, 0, plan.arm)
	check(eye.distance_to(spot) >= Grid.units(2.0), "a câmera da soleira fica a mais de 2 m do olho (%s)" % eye.distance_to(spot))


func test_arriving_at_the_own_house_does_not_open_the_wheel_over_the_door_because_the_engine_goes_in() -> void:
	var hud := PlayerHud.new()
	hud.is_active = true
	hud.home_building = 4
	var said: Array = []
	hud.warning.connect(func(text: String): said.append(text))
	hud._go_then = {"kind": "building", "id": 4, "label": "Sua casa"}
	await hud._after_go() # sem `api`: se tentasse abrir a roda, quebraria; o motor é quem entra
	eq(said, ["Entrando em casa…"], "a própria casa: o motor entra sozinho, a roda não abre")
	hud.free()
