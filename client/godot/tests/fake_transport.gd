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

	var sent: Array = [] ## comandos enviados pelo canal do jogador
	var replier: Callable ## (comando) -> resposta; vazio = o teste responde à mão com push

	func send(data) -> bool:
		if not open:
			return false
		sent.append(data)
		if replier.is_valid():
			var reply = replier.call(data)
			Engine.get_main_loop().process_frame.connect(func(): message.emit(reply), CONNECT_ONE_SHOT)
		return true

	func is_open() -> bool:
		return open


var routes: Dictionary = {} ## "GET /visual/subscribe" -> Dictionary{status, body} ou Callable -> Dictionary
var calls: Array[String] = [] ## "GET /caminho?query"
var headers_of: Dictionary = {} ## "POST /caminho" -> lista dos cabeçalhos extras enviados
var bodies: Dictionary = {} ## "POST /caminho" -> lista dos corpos enviados
var channels: Array = []


func on(key: String, response: Variant) -> void:
	routes[key] = response


func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
	calls.append("%s %s" % [method, path])
	if not extra_headers.is_empty():
		headers_of.get_or_add("%s %s" % [method, path], []).append(extra_headers)
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


var channel_headers: Dictionary = {} ## "WS caminho" -> cabeçalhos do aperto de mão
var channel_replier: Callable ## respostas automáticas do canal do jogador (comando -> resposta)


func open_channel(path: String, extra_headers: Dictionary = {}):
	var channel := FakeChannel.new()
	channels.append(channel)
	calls.append("WS %s" % path)
	channel_headers["WS %s" % path] = extra_headers
	if channel_replier.is_valid():
		channel.replier = channel_replier
	return channel


func count_calls(prefix: String) -> int:
	var total := 0
	for call in calls:
		if call.begins_with(prefix):
			total += 1
	return total
