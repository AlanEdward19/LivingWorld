extends RefCounted
## Células do servidor <-> espaço 3D. Uma célula = 1 unidade; Y é altura.

const CELL := 1.0


static func to_world(cell: Vector2i, height: float = 0.0) -> Vector3:
	return Vector3((cell.x + 0.5) * CELL, height, (cell.y + 0.5) * CELL)


static func to_cell(point: Vector3) -> Vector2i:
	return Vector2i(floori(point.x / CELL), floori(point.z / CELL))
