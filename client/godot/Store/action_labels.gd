extends RefCounted
## Rótulos de ação servidos por GET /visual/catalog/actions. O cliente não escreve texto de ação.

var labels: Dictionary = {} ## int -> String


func load_from(items: Array) -> void:
	labels.clear()
	for item in items:
		labels[int(item.actionType)] = String(item.label)


## Ação sem rótulo no catálogo mostra o id, sem inventar texto.
func label_for(action: int) -> String:
	if action < 0:
		return ""
	return labels.get(action, "ação %d" % action)
