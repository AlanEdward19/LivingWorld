extends RefCounted
## Canal de push de um escopo (WebSocket em produção, falso nos testes).
## Emite `message` com cada frame JSON já desserializado e `closed` ao cair.

signal message(data)
signal closed()


func close() -> void:
	pass
