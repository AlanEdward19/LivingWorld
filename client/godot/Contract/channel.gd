extends RefCounted
## Canal de push de um escopo (WebSocket em produção, falso nos testes).
## Emite `message` com cada frame JSON já desserializado e `closed` ao cair.
## O canal do jogador (AD-197a) também envia: `send` manda um comando e `is_open` diz se já dá para mandar.

signal message(data)
signal closed()

## #249: o servidor manda o snapshot do escopo logo ao aceitar o canal, e o cliente já o recebeu pela assinatura (HTTP): ler o JSON
## de novo custava um quadro inteiro (~100 ms numa cidade). Com isto ligado o primeiro pacote é descartado sem ler; o que saiu
## entre os dois aparece como lacuna no próximo delta e o replay o traz (o contrato de sempre).
var skip_first_snapshot := false


func close() -> void:
	pass


func send(_data) -> bool:
	return false


func is_open() -> bool:
	return false
