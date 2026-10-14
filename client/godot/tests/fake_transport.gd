extends RefCounted
## Transporte falso: respostas roteiradas por "MÉTODO caminho-prefixo", registro de chamadas e
## canais que o teste empurra/derruba à mão. Nenhum socket, nenhum servidor.

const Channel := preload("res://Contract/channel.gd")


class FakeChannel extends "res://Contract/channel.gd":
	var open := true

	func push(data: Variant) -> void:
		message.emit(data)

	func drop() -> void:
		open = false
		closed.emit()

	func close() -> void:
		open = false


var routes: Dictionary = {} ## "GET /visual/subscribe" -> Dictionary{status, body} ou Callable -> Dictionary
var calls: Array[String] = [] ## "GET /caminho?query"
var bodies: Dictionary = {} ## "POST /caminho" -> lista dos corpos enviados
var channels: Array = []


func on(key: String, response: Variant) -> void:
	routes[key] = response


func request(method: String, path: String, body: Variant = null) -> Dictionary:
	calls.append("%s %s" % [method, path])
	if body != null:
		bodies.get_or_add("%s %s" % [method, path], []).append(body)
	for key in routes:
		var parts: PackedStringArray = key.split(" ", true, 1)
		if parts[0] == method and path.begins_with(parts[1]):
			var response = routes[key]
			if response is Callable:
				response = response.call(path)
			return response
	return {"status": 0, "body": null}


func open_channel(path: String):
	var channel := FakeChannel.new()
	channels.append(channel)
	calls.append("WS %s" % path)
	return channel


func count_calls(prefix: String) -> int:
	var total := 0
	for call in calls:
		if call.begins_with(prefix):
			total += 1
	return total
