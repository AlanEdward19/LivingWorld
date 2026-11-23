extends RefCounted
## Canal de push de um escopo (WebSocket em produção, falso nos testes).
## Emite `message` com cada frame JSON já desserializado e `closed` ao cair.
## O canal do jogador (AD-197a) também envia: `send` manda um comando e `is_open` diz se já dá para mandar.

signal message(data)
signal closed()


func close() -> void:
	pass


func send(_data) -> bool:
	return false


func is_open() -> bool:
	return false
