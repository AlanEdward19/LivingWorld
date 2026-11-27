extends RefCounted
## #248: a máscara de passos que o motor dá ao redor do corpo (`GET /players/{id}/ground`): para cada célula, um bit por vizinha dizendo se
## o passo é permitido pela regra do chão do motor (parede, porta, soleira, canto, entre prédios, borda do mapa). O cliente prevê o
## passo consultando isto e nunca tem regra própria: quando o motor mudar a regra (parede em aresta, #226), o cliente acompanha sem mudar.
## Gente é regra à parte: a célula cheia (`crowd_limit`) barra o passo.

var origin := Vector2i.ZERO ## célula do canto de cima à esquerda da janela
var size := 0 ## lado da janela, em células
var floor_level := 0
var building_id := -1
var crowd_limit := 3
var mask := PackedInt32Array()


static func from_body(body: Dictionary):
	var grid = load("res://Scenes/ground_grid.gd").new()
	grid.origin = Vector2i(int(body.get("x", 0)), int(body.get("y", 0)))
	grid.size = int(body.get("size", 0))
	grid.floor_level = int(body.get("floor", 0))
	grid.building_id = int(body.buildingId) if body.get("buildingId") != null else -1
	grid.crowd_limit = int(body.get("crowdLimit", 3))
	var raw: Array = body.get("mask", [])
	grid.mask = PackedInt32Array()
	grid.mask.resize(raw.size())
	for i in raw.size():
		grid.mask[i] = int(raw[i])
	if grid.mask.size() != grid.size * grid.size:
		grid.size = 0
		grid.mask = PackedInt32Array()
	return grid


func is_valid() -> bool:
	return size > 0


## A célula está dentro da janela, com `margin` células de folga até a borda?
func covers(cell: Vector2i, margin: int = 0) -> bool:
	var at := cell - origin
	return at.x >= margin and at.y >= margin and at.x < size - margin and at.y < size - margin


## O passo de `from` a `to` (vizinhas) é permitido pelo chão? Fora da janela não é (o corpo pede outra janela antes de chegar à borda).
func can_step(from: Vector2i, to: Vector2i) -> bool:
	if from == to:
		return true
	var d := to - from
	if absi(d.x) > 1 or absi(d.y) > 1:
		return false
	var at := from - origin
	if at.x < 0 or at.y < 0 or at.x >= size or at.y >= size:
		return false
	return ((mask[at.y * size + at.x] >> ((d.y + 1) * 3 + (d.x + 1))) & 1) == 1
