extends Node
## Transporte real: HTTP (HTTPRequest) e WebSocket (WebSocketPeer sondado em _process).
## Único ponto do cliente que conhece URL, sockets e bytes.

const Channel := preload("res://Contract/channel.gd")

var base_url := "http://localhost:5214"

var _channels: Array = []


class WsChannel extends "res://Contract/channel.gd":
	var peer := WebSocketPeer.new()
	var _announced_close := false

	func poll() -> void:
		peer.poll()
		match peer.get_ready_state():
			WebSocketPeer.STATE_OPEN:
				while peer.get_available_packet_count() > 0:
					var parsed = JSON.parse_string(peer.get_packet().get_string_from_utf8())
					if parsed != null:
						message.emit(parsed)
			WebSocketPeer.STATE_CLOSED:
				if not _announced_close:
					_announced_close = true
					closed.emit()

	func close() -> void:
		_announced_close = true
		peer.close()


func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
	var http := HTTPRequest.new()
	add_child(http)
	var verbs := {"GET": HTTPClient.METHOD_GET, "POST": HTTPClient.METHOD_POST, "PUT": HTTPClient.METHOD_PUT, "DELETE": HTTPClient.METHOD_DELETE}
	var headers := PackedStringArray(["Content-Type: application/json", "Accept: application/json"])
	# Pausa, velocidade e passo são do admin (ADR-0015): play.sh sobe a API com ADMIN_TOKEN e o cliente com o mesmo valor.
	var admin_token := OS.get_environment("LW_ADMIN_TOKEN")
	if admin_token != "":
		headers.append("X-Admin-Token: " + admin_token)
	for name in extra_headers:
		headers.append("%s: %s" % [name, extra_headers[name]])
	var payload := "" if body == null else JSON.stringify(body)
	if http.request(base_url + path, headers, verbs[method], payload) != OK:
		http.queue_free()
		return {"status": 0, "body": null}
	var done: Array = await http.request_completed
	http.queue_free()
	if done[0] != HTTPRequest.RESULT_SUCCESS:
		return {"status": 0, "body": null}
	var text: String = (done[3] as PackedByteArray).get_string_from_utf8()
	return {"status": int(done[1]), "body": JSON.parse_string(text) if text != "" else null, "text": text}


func open_channel(path: String):
	var channel := WsChannel.new()
	channel.peer.inbound_buffer_size = 1 << 25 # o snapshot do mundo passa dos 64 KiB padrão
	channel.peer.max_queued_packets = 4096
	channel.peer.connect_to_url(base_url.replace("http", "ws") + path)
	_channels.append(channel)
	return channel


func _process(_delta: float) -> void:
	for channel in _channels.duplicate():
		channel.poll()
		if channel._announced_close:
			_channels.erase(channel)
