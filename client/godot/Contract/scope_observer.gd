extends Node
## Diz ao servidor o que a câmera observa (`POST /observation/scope`). O servidor só grava o rastro de
## cognição de NPC em escopo `cidade`/`interior`, então sem isto o painel "Cérebro" nasceria vazio.
## Reenvia o escopo a cada `interval` s (o servidor descarta fonte sem batimento em 30 s) e retira a
## fonte com `scope: null` ao sair. Nunca escreve no mundo.

var api
var source_id := "godot-%d" % OS.get_process_id()
var scope: Dictionary = {} ## {kind, cityId?, buildingId?}; vazio = nada publicado
var interval := 10.0


func _ready() -> void:
	var timer := Timer.new()
	timer.wait_time = interval
	timer.autostart = true
	timer.timeout.connect(heartbeat)
	add_child(timer)


## Troca de escopo: só envia se mudou.
func set_scope(kind: String, city_id: String = "", building_id: String = "") -> void:
	var next := {"kind": kind}
	if city_id != "":
		next["cityId"] = city_id
	if building_id != "":
		next["buildingId"] = building_id
	if next == scope:
		return
	scope = next
	await api.observation_scope(source_id, scope)


func heartbeat() -> void:
	if not scope.is_empty():
		await api.observation_scope(source_id, scope)


func clear() -> void:
	scope = {}
	await api.observation_scope(source_id, scope)
