extends RefCounted
## Forma e placa do prédio pela função (ADR-0047 etapa 3): o estado do prédio (`buildingStates`: função, tipo de local,
## fechado) vira enfeite gerado por código — chaminé de casa, toldo de mercado, bigorna de ferraria, forno de padaria,
## cerca de fazenda, barris de taverna, lanterna de hospedaria — e uma placa com o nome do tipo. Malhas e materiais
## compartilhados; nada projeta sombra além do que o prédio já projeta.

const Props := preload("res://Scenes/npc_props.gd")

## Palavras do nome do tipo de local -> estilo. Sem casar, "shop" (caixotes) para oficina.
const STYLE_WORDS := [
	[["fazend", "farm", "sítio", "sitio", "lavour", "campo", "pomar", "horta"], "farm"],
	[["ferr", "smith", "forja"], "smithy"],
	[["padar", "baker", "moinho", "mill", "forno"], "bakery"],
	[["merc", "market", "loja", "shop", "feira", "armaz", "store"], "market"],
	[["tavern", "estalag", "bar", "inn"], "tavern"],
	[["mina", "mine", "pedreir", "quarr"], "quarry"],
	[["templo", "igreja", "temple", "church", "capela"], "temple"],
]
const SIGN_COLORS := {"dwelling": Color("6b8f5a"), "lodging": Color("4a6a9a"), "farm": Color("a08f3a"), "smithy": Color("5a5a66"),
	"bakery": Color("b0703a"), "market": Color("9a4a3a"), "tavern": Color("7a4a2a"), "quarry": Color("8a8f98"),
	"temple": Color("e0d6b8"), "shop": Color("8a6a43"), "structure": Color("777777")}


## Estilo pelo estado: casa, hospedaria, um dos estilos de oficina ou estrutura.
static func style_of(state: Dictionary) -> String:
	var function := String(state.get("function", ""))
	if "workplace" in function:
		var name := (String(state.get("location_type_name", "")) + " " + String(state.get("type_name", ""))).to_lower()
		for entry in STYLE_WORDS:
			for word in entry[0]:
				if word in name:
					return entry[1]
		return "shop"
	if "lodging" in function:
		return "lodging"
	if "dwelling" in function:
		return "dwelling"
	return "structure" if function != "" else ""


## Texto da placa: nome do tipo de local (oficina) ou do tipo de prédio; "Fechado" quando fechado.
static func sign_text(state: Dictionary) -> String:
	var name := String(state.get("location_type_name", ""))
	if name == "":
		name = String(state.get("type_name", ""))
	if bool(state.get("closed", false)) and name != "":
		name += " (fechado)"
	return name


## Acrescenta os enfeites ao nó do prédio. `door` = ponto da porta no chão, `outward` = direção para fora.
static func dress(root: Node3D, state: Dictionary, top: Vector3, door: Vector3, outward: Vector3, height: float) -> void:
	var style := style_of(state)
	if style == "":
		return
	var dressing := Node3D.new()
	dressing.name = "Dress"
	root.add_child(dressing)
	var side := Vector3(outward.z, 0, -outward.x)
	var closed := bool(state.get("closed", false))
	match style:
		"dwelling":
			Props.part(dressing, Props._box(Vector3(0.22, 0.6, 0.22)), Color("7a6a5c"), top + Vector3(0.2, 0.25, 0.2))
		"lodging":
			Props.part(dressing, Props._sphere(0.09), Color("ffd27a"), door + outward * 0.35 + side * 0.45 + Vector3(0, 0.95, 0))
		"farm":
			for i in 5:
				var at := door + outward * 1.3 + side * (i - 2) * 0.45
				Props.part(dressing, Props._box(Vector3(0.05, 0.35, 0.05)), Color("8a6a43"), at + Vector3(0, 0.17, 0))
			Props.part(dressing, Props._box(Vector3(1.9, 0.04, 0.04)), Color("8a6a43"), door + outward * 1.3 + Vector3(0, 0.28, 0),
				Vector3(0, atan2(side.x, side.z) + PI / 2.0, 0))
			Props.part(dressing, Props._cylinder(0.18, 0.18, 0.3, 10), Color("d9b44a"), door + outward * 0.8 - side * 0.9 + Vector3(0, 0.15, 0),
				Vector3(PI / 2.0, 0, 0))
		"smithy":
			Props.part(dressing, Props._box(Vector3(0.3, 0.22, 0.16)), Color("4a4f57"), door + outward * 0.8 + side * 0.6 + Vector3(0, 0.11, 0))
			Props.part(dressing, Props._box(Vector3(0.28, 0.9, 0.28)), Color("5a4a40"), top + Vector3(-0.2, 0.4, -0.2))
		"bakery":
			Props.part(dressing, Props._sphere(0.32, 0.4), Color("b8865a"), door + outward * 0.7 - side * 0.8 + Vector3(0, 0.12, 0))
			Props.part(dressing, Props._box(Vector3(0.2, 0.7, 0.2)), Color("8a6a5c"), top + Vector3(0.2, 0.3, -0.2))
		"market":
			var awning := Props.part(dressing, Props._box(Vector3(1.2, 0.04, 0.6)), Color("c84a3a"), door + outward * 0.5 + Vector3(0, 0.95, 0),
				Vector3(0.25, atan2(outward.x, outward.z), 0))
			awning.name = "Awning"
			for i in 2:
				Props.part(dressing, Props._box(Vector3(0.3, 0.25, 0.3)), Color("a27b52"), door + outward * 0.9 + side * (i * 0.7 - 0.35) + Vector3(0, 0.12, 0))
		"tavern":
			for i in 2:
				Props.part(dressing, Props._cylinder(0.14, 0.14, 0.34, 10), Color("6b4a2b"), door + outward * 0.6 + side * (0.6 + i * 0.32) + Vector3(0, 0.17, 0))
		"quarry":
			for i in 3:
				Props.part(dressing, Props._box(Vector3(0.3, 0.2, 0.25)), Color("9a9ea6"), door + outward * 0.9 - side * (0.5 + i * 0.3) + Vector3(0, 0.1 + (i % 2) * 0.1, 0))
		"temple":
			Props.part(dressing, Props._cylinder(0.0, 0.3, 0.8, 4), Color("e0d6b8"), top + Vector3(0, 0.6, 0))
		"shop":
			Props.part(dressing, Props._box(Vector3(0.35, 0.3, 0.35)), Color("a27b52"), door + outward * 0.8 + side * 0.6 + Vector3(0, 0.15, 0))
	if closed:
		for angle in [0.7, -0.7]:
			Props.part(dressing, Props._box(Vector3(0.62, 0.05, 0.03)), Color("2a1d16"), door + outward * 0.53 + Vector3(0, 0.4, 0),
				Vector3(0, atan2(outward.x, outward.z), angle))
	var text := sign_text(state)
	if text == "" or style == "structure":
		return
	var board := Props.part(dressing, Props._box(Vector3(0.5, 0.22, 0.04)), SIGN_COLORS.get(style, Color("8a6a43")),
		door + outward * 0.56 + Vector3(0, minf(height, 1.0) * 0.9 + 0.12, 0), Vector3(0, atan2(outward.x, outward.z), 0))
	board.name = "Sign"
	var label := Label3D.new()
	label.name = "SignText"
	label.text = text
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.pixel_size = 0.006
	label.font_size = 22
	label.outline_size = 8
	label.position = board.position + Vector3(0, 0.3, 0)
	label.visibility_range_end = 26.0
	dressing.add_child(label)
