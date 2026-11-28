extends RefCounted
## #259 (S3 do sandbox): o menu social da roda do E sobre uma pessoa. Tudo vem do motor: a oferta `person` traz `social` (as interações do catálogo, com
## `available` e o `reason` das que não valem agora) e `relation`; o cliente só agrupa por categoria, esmaece o que não vale e diz o motivo ao tocar.
## O texto de cada interação e de cada categoria é do catálogo do cenário, não daqui.


## A linha "Interagir ▸" da pessoa (vazia se o motor não oferece interações sociais).
static func main_entry(offer: Dictionary, title: String) -> Dictionary:
	var options: Array = offer.get("social", []) if offer.get("social") != null else []
	if options.is_empty():
		return {}
	return {"text": "%s: Interagir ▸" % title, "kind": "person", "id": int(offer.id), "verb": "social_menu", "resource": -1, "social": options, "title": title}


## As categorias na ordem do catálogo: [{id, name, total, available}].
static func categories(options: Array) -> Array:
	var order: Array = []
	var by_id := {}
	for option in options:
		var id := String(option.get("category", ""))
		if not by_id.has(id):
			by_id[id] = {"id": id, "name": String(option.get("categoryName", id)), "total": 0, "available": 0}
			order.append(id)
		by_id[id].total += 1
		if bool(option.get("available", false)):
			by_id[id].available += 1
	return order.map(func(id): return by_id[id])


## Segunda roda: uma linha por categoria ("Amigável (2/3) ▸"; a sem nenhuma disponível vem esmaecida) e a volta.
static func category_entries(main: Dictionary) -> Array:
	var list: Array = []
	for cat in categories(main.get("social", [])):
		list.append({"text": "%s (%d/%d) ▸" % [cat.name, cat.available, cat.total], "kind": "person", "id": int(main.id), "verb": "social_cat", "resource": -1,
			"social": main.social, "cat": cat.id, "title": main.get("title", ""), "dim": cat.available == 0})
	list.append(back_entry(main))
	return list


## Terceira roda: as interações da categoria; as que não valem agora ficam esmaecidas (`disabled`) com o motivo do motor em `reason`.
static func interaction_entries(cat_entry: Dictionary) -> Array:
	var list: Array = []
	for option in cat_entry.get("social", []):
		if String(option.get("category", "")) != String(cat_entry.get("cat", "")):
			continue
		var ok := bool(option.get("available", false))
		list.append({"text": "%s · %s" % [String(option.name), duration_text(int(option.get("seconds", 0)))], "kind": "person", "id": int(cat_entry.id),
			"verb": "social:" + String(option.id), "resource": -1, "disabled": not ok, "dim": not ok, "reason": String(option.get("reason", "")) if not ok else ""})
	list.append({"text": "‹ Voltar", "kind": "person", "id": int(cat_entry.id), "verb": "social_back", "resource": -1, "social": cat_entry.get("social", []),
		"title": cat_entry.get("title", "")})
	return list


static func back_entry(main: Dictionary) -> Dictionary:
	return {"text": "‹ Voltar", "kind": "person", "id": int(main.id), "verb": "social_root", "resource": -1}


## "8 s", "40 s", "30 min": a duração de mundo que o motor diz.
static func duration_text(seconds: int) -> String:
	if seconds <= 0:
		return "—"
	return "%d s" % seconds if seconds < 90 else "%d min" % int(round(seconds / 60.0))
