extends Node
## Transporte real: HTTP (HTTPRequest) e WebSocket (WebSocketPeer sondado em _process).
## Único ponto do cliente que conhece URL, sockets e bytes.

const Channel := preload("res://Contract/channel.gd")
const Cost := preload("res://Contract/frame_cost.gd")

var base_url := OS.get_environment("LW_API_URL") if OS.get_environment("LW_API_URL") != "" else "http://localhost:5214" ## LW_API_URL: medida de FPS numa porta própria

var _channels: Array = []


class WsChannel extends "res://Contract/channel.gd":
	var peer := WebSocketPeer.new()
	var _announced_close := false
	var _skipped := false

	func poll() -> void:
		peer.poll()
		match peer.get_ready_state():
			WebSocketPeer.STATE_OPEN:
				while peer.get_available_packet_count() > 0:
					if skip_first_snapshot and not _skipped:
						_skipped = true
						peer.get_packet() # o snapshot de abertura do canal: já temos o da assinatura
						continue
					var started := Time.get_ticks_usec()
					var parsed = JSON.parse_string(peer.get_packet().get_string_from_utf8())
					Cost.add("parse", started)
					if parsed != null:
						message.emit(parsed)
			WebSocketPeer.STATE_CLOSED:
				if not _announced_close:
					_announced_close = true
					closed.emit()

	func close() -> void:
		_announced_close = true
		peer.close()

	func send(data) -> bool:
		return is_open() and peer.send_text(JSON.stringify(data)) == OK

	func is_open() -> bool:
		return peer.get_ready_state() == WebSocketPeer.STATE_OPEN


## Cabeçalhos de toda requisição: JSON, token de admin (se houver em LW_ADMIN_TOKEN) e os extras (ex.: X-Admin-Actor).
## Pausa, velocidade e passo são do admin (ADR-0015): play.sh sobe a API com ADMIN_TOKEN e o cliente com o mesmo valor.
func build_headers(extra_headers: Dictionary = {}) -> PackedStringArray:
	var headers := PackedStringArray(["Content-Type: application/json", "Accept: application/json"])
	var admin_token := OS.get_environment("LW_ADMIN_TOKEN")
	if admin_token != "":
		headers.append("X-Admin-Token: " + admin_token)
	for name in extra_headers:
		headers.append("%s: %s" % [name, extra_headers[name]])
	return headers


func request(method: String, path: String, body: Variant = null, extra_headers: Dictionary = {}) -> Dictionary:
	var http := HTTPRequest.new()
	add_child(http)
	var verbs := {"GET": HTTPClient.METHOD_GET, "POST": HTTPClient.METHOD_POST, "PUT": HTTPClient.METHOD_PUT, "DELETE": HTTPClient.METHOD_DELETE}
	var headers := build_headers(extra_headers)
	var payload := "" if body == null else JSON.stringify(body)
	if http.request(base_url + path, headers, verbs[method], payload) != OK:
		http.queue_free()
		return {"status": 0, "body": null}
	var done: Array = await http.request_completed
	http.queue_free()
	if done[0] != HTTPRequest.RESULT_SUCCESS:
		return {"status": 0, "body": null}
	var text: String = (done[3] as PackedByteArray).get_string_from_utf8()
	var started := Time.get_ticks_usec()
	var parsed = await _parse(text) if text != "" else null
	Cost.add("parse_http", started)
	return {"status": int(done[1]), "body": parsed, "text": text}


const THREAD_PARSE_CHARS := 120000 ## #249: corpo maior que isto (snapshot de cidade ou mundo) é lido fora do quadro


## JSON de resposta. Os grandes (snapshots, ~100 ms no fio principal) saem para uma tarefa em segundo plano e o quadro segue;
## o resultado é o mesmo de `JSON.parse_string`.
func _parse(text: String) -> Variant:
	if text.length() < THREAD_PARSE_CHARS or not is_inside_tree():
		return JSON.parse_string(text)
	var box: Array = [null]
	var task := WorkerThreadPool.add_task(func() -> void: box[0] = JSON.parse_string(text))
	while not WorkerThreadPool.is_task_completed(task):
		await get_tree().process_frame
	WorkerThreadPool.wait_for_task_completion(task)
	return box[0]


## `extra_headers` vão no aperto de mão (o canal do jogador leva `Authorization: Bearer`, AD-197a).
func open_channel(path: String, extra_headers: Dictionary = {}):
	var channel := WsChannel.new()
	if not extra_headers.is_empty():
		var handshake := PackedStringArray()
		for name in extra_headers:
			handshake.append("%s: %s" % [name, extra_headers[name]])
		channel.peer.handshake_headers = handshake
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
