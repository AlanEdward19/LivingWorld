extends Node
## QUEDA para servidor antigo (AD-177): relê a camada `Roads` sem recarregar a cena. Desde a AD-181 o escopo manda as ruas
## no retrato (`livingState.roads`) e por delta (`roadUpserts`/`roadRemoved`): com isso (`store.roads_live`) esta releitura
## NÃO roda. Sem o campo: a cada REFRESH_SECONDS, e logo depois de um prédio novo chegar com um lote que encosta numa rua
## ainda desconhecida, pede o retrato do mundo pela rota sem estado e troca só as ruas (o chão redesenha; nada mais).

const REFRESH_SECONDS := 30.0
const HINT_DELAY := 2.0 ## prédio novo pediu rua desconhecida: relê logo, mas junta os pedidos de uma rajada

var app
var refreshes := 0 ## testável: quantas releituras aplicaram ruas novas
var _clock := 0.0
var _hint := -1.0
var _busy := false


func setup(p_app) -> void:
	app = p_app
	app.store.buildings_changed.connect(_on_buildings)


## Lote que encosta numa rua que a camada ainda não tem: a rua nasceu com o prédio, vale reler.
func _on_buildings(ids: Array) -> void:
	if app.store.roads_live:
		return
	for id in ids:
		var parcel: Dictionary = app.store.buildings.get(id, {}).get("lot", {})
		var street = parcel.get("street")
		if street != null and not app.store.roads.has(street):
			if _hint < 0.0:
				_hint = HINT_DELAY
			return


func _process(delta: float) -> void:
	if app == null or app.store == null or not app.store.kind in ["World", "City"] or app.store.roads_live:
		return
	_clock += delta
	var hinted := false
	if _hint >= 0.0:
		_hint -= delta
		hinted = _hint < 0.0
	if (_clock >= REFRESH_SECONDS or hinted) and not _busy:
		_clock = 0.0
		_hint = -1.0
		refresh()


func refresh() -> void:
	if _busy:
		return
	_busy = true
	var response: Dictionary = await app.api.world_snapshot()
	_busy = false
	if not response.get("errors", []).is_empty():
		app._on_contract_error(response.errors) # mesma faixa do topo do stream: divergência nunca fica muda
	if not response.ok or not response.body is Dictionary or not response.body.get("payload") is Dictionary:
		return
	var layers = response.body.payload.get("layers")
	if layers is Dictionary and app.store.refresh_roads(layers.get("Roads")):
		refreshes += 1
