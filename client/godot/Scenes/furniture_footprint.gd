extends RefCounted
## #266: a pegada do móvel em células, como o motor a entende (`footprint` [largura, profundidade] do descritor; 1×1 sem ela). A origem é a célula de trás à esquerda;
## o giro (0, 90, 180, 270 graus em torno de Y) gira a pegada em volta dela, com o fundo para -z no giro 0. Só conta: quem decide o lugar é o motor.


static func size_of(descriptor: Dictionary) -> Vector2i:
	var footprint: Variant = descriptor.get("footprint")
	if footprint is Array and footprint.size() >= 2:
		return Vector2i(maxi(1, int(footprint[0])), maxi(1, int(footprint[1])))
	return Vector2i(1, 1)


## Um ponto local (i à direita, j à frente), em células, no mundo para o giro dado (a mesma conta do motor: `ObjectFootprint.Turn`).
static func turn(i: float, j: float, facing: int) -> Vector2:
	match posmod(facing, 360):
		90:
			return Vector2(j, -i)
		180:
			return Vector2(-i, -j)
		270:
			return Vector2(-j, i)
	return Vector2(i, j)


## Onde fica o centro da pegada em relação ao centro da célula de origem, em células.
static func center_offset(size: Vector2i, facing: int) -> Vector2:
	return turn((size.x - 1) / 2.0, (size.y - 1) / 2.0, facing)


## As células que a peça ocupa (origem + giro).
static func cells(origin: Vector2i, size: Vector2i, facing: int) -> Array[Vector2i]:
	var out: Array[Vector2i] = []
	for j in size.y:
		for i in size.x:
			var step := turn(i, j, facing)
			out.append(origin + Vector2i(int(step.x), int(step.y)))
	return out
