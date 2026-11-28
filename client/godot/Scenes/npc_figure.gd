extends RefCounted
## #247: fábrica de figura única. A mesma pessoa tem a mesma aparência na rua e dentro da casa: quem cria o boneco (o renderer de
## NPCs do mundo e a vista de interior) passa por aqui, e o corpo vem do mesmo dado do motor (`observable` da store, senão o
## modelo de profissão e idade já lido), nunca de uma regra do cliente.
## `ctx` = {store, catalog (AssetCatalog), model_of: Callable id -> {profession, age}}; chaves ausentes valem "sem dado".

const NpcActor := preload("res://Scenes/npc_actor.gd")


## Cria o boneco já vestido; o chamador posiciona e guarda.
static func create(parent: Node, id: int, cell: Vector2i, action: int, labels, look: Dictionary, ctx: Dictionary) -> Node3D:
	var actor := NpcActor.new()
	parent.add_child(actor)
	actor.setup(id, cell, action, labels, look)
	dress(actor, id, ctx)
	return actor


## Roupa do ofício, chapéu, escala da idade, cabelo, pele de doente e luto pelo dado que o cliente já tem. Sem dado nenhum, o
## corpo fica o base (cor da camisa pelo id) até o dado chegar; chamar de novo quando chegar refaz só o que mudou.
static func dress(actor, id: int, ctx: Dictionary) -> void:
	if actor == null:
		return
	var store = ctx.get("store")
	var catalog = ctx.get("catalog")
	var npc = store.npcs.get(id) if store != null else null
	var obs: Dictionary = npc.get("obs", {}) if npc != null else {}
	if not obs.is_empty():
		actor.apply_observable(obs, catalog)
		return
	var model_of: Callable = ctx.get("model_of", Callable())
	if catalog == null or not model_of.is_valid():
		return
	var model: Dictionary = model_of.call(id)
	if not model.is_empty():
		actor.apply_model(catalog, int(model.profession), float(model.age))
