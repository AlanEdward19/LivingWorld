extends RefCounted
## #237: a troca cidade ↔ mundo enquanto o jogador anda com o corpo. O cliente sabe onde o corpo está (a posição fina que o motor
## confirma) e onde ficam os limites das cidades, então pede o escopo certo assim que o corpo cruza a borda, em vez de esperar o
## corpo sumir do desenho (600 ms) e perguntar à ficha. E, enquanto o boneco não está no escopo desenhado, a câmera segue um
## ponto de apoio na posição fina do corpo (sem subir "aguardando corpo" nem congelar).

const Grid := preload("res://Scenes/grid.gd")

const ENTER_MARGIN := 1 ## células para dentro da borda para entrar na cidade (a saída é na própria borda: histerese de uma célula)
const HANDOVER_CELLS := 0.35 ## o boneco novo só recebe a câmera quando está a menos disto do ponto de apoio
const ASK_EVERY_MS := 1500 ## o mesmo pedido de escopo não se repete antes disto

var _asked_ms := -10000
var anchor: Node3D = null ## o ponto de apoio da câmera (nulo enquanto o boneco do corpo existe)


## O escopo que a posição fina do corpo pede agora: ["City", id], ["World", ""] ou [] (o atual serve). `kind` é o escopo desenhado,
## `city` o snapshot da cidade em foco, `cities` os marcadores do mundo ({id: {bounds: Rect2i}}).
static func wanted(kind: String, fine: Vector2, city: Dictionary, cities: Dictionary) -> Array:
	if fine == Vector2.INF:
		return []
	var cell := Vector2i(floori(fine.x), floori(fine.y))
	if kind == "City":
		var bounds = city.get("bounds")
		if bounds is Rect2i and bounds.size != Vector2i.ZERO and not bounds.has_point(cell):
			return ["World", ""]
	elif kind == "World":
		var ids: Array = cities.keys()
		ids.sort()
		for id in ids:
			var bounds = cities[id].get("bounds")
			if bounds is Rect2i and bounds.size != Vector2i.ZERO and bounds.grow(-ENTER_MARGIN).has_point(cell):
				return ["City", String(id)]
	return []


## Já pode pedir o escopo? (um pedido por ASK_EVERY_MS; marca o instante quando devolve true)
func may_ask(now_ms: int) -> bool:
	if now_ms - _asked_ms < ASK_EVERY_MS:
		return false
	_asked_ms = now_ms
	return true


## O ponto de apoio, posto na posição fina do corpo (células) à altura `height`, com a escala `body_scale` do último boneco; criado sob `parent` na primeira vez.
func hold_at(parent: Node, fine: Vector2, height: float, body_scale: Vector3 = Vector3.ONE) -> Node3D:
	if anchor == null or not is_instance_valid(anchor):
		anchor = Node3D.new()
		anchor.name = "BodyAnchor"
		parent.add_child(anchor)
	anchor.position = Vector3(fine.x * Grid.CELL, height, fine.y * Grid.CELL)
	anchor.scale = body_scale # a câmera mede o olho pela escala do corpo (criança, adulto): o ponto de apoio tem a do último boneco
	return anchor


## O boneco voltou (ou a posse acabou): o ponto de apoio sai.
func release() -> void:
	if anchor != null and is_instance_valid(anchor):
		anchor.queue_free()
	anchor = null
