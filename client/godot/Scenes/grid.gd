extends RefCounted
## Células do servidor <-> espaço 3D. Uma célula = 1 unidade; Y é altura.

const CELL := 1.0

## ADR-0052: a escala que o motor diz (`GET /world/info`, `scale`); sem ela (servidor antigo), as medidas de antes.
static var cell_meters := 1.5
static var floor_meters := 1.5 ## antes do motor dizer: um andar de uma célula (o desenho de antes)
static var door_meters := 1.17
static var door_width_meters := 0.84
static var window_sill_meters := 0.6
static var window_meters := 0.54


## Aplica a escala do motor: metros de cada medida viram unidades da grade (uma célula = uma unidade).
static func use_scale(scale: Dictionary) -> void:
	if scale.is_empty():
		return
	cell_meters = maxf(float(scale.get("cellMeters", cell_meters)), 0.01)
	floor_meters = float(scale.get("floorMeters", floor_meters))
	door_meters = float(scale.get("doorMeters", door_meters))
	door_width_meters = float(scale.get("doorWidthMeters", door_width_meters))
	window_sill_meters = float(scale.get("windowSillMeters", window_sill_meters))
	window_meters = float(scale.get("windowMeters", window_meters))


## Metros em unidades da grade.
static func units(meters: float) -> float:
	return meters / cell_meters


## Altura de um andar em unidades (o motor diz; 2 unidades com 3 m por andar e 1,5 m por célula).
static func floor_height() -> float:
	return units(floor_meters)


static func to_world(cell: Vector2i, height: float = 0.0) -> Vector3:
	return Vector3((cell.x + 0.5) * CELL, height, (cell.y + 0.5) * CELL)


static func to_cell(point: Vector3) -> Vector2i:
	return Vector2i(floori(point.x / CELL), floori(point.z / CELL))
