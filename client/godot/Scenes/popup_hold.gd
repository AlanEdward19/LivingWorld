extends Node
## #240: pede ao motor a pausa de popup (comando "hold" do canal /players/ws). Ligado enquanto houver popup aberto, renovado a cada
## RENEW_S (o servidor solta sozinho em 30 s sem renovar, ao desconectar e se um segundo jogador entrar; só o único conectado é honrado).
## Sem canal aberto (espectador, sem sessão) não faz nada.

const RENEW_S := 10.0

var api
var held := false ## o último pedido de popup
var sent: Array = [] ## on/off enviados (os testes leem)
var _timer: Timer


func bind(modal) -> void:
	modal.changed.connect(set_hold)
	_timer = Timer.new()
	_timer.wait_time = RENEW_S
	_timer.timeout.connect(func(): _send(true))
	add_child(_timer)


func set_hold(on: bool) -> void:
	if on == held:
		return
	held = on
	if on:
		_timer.start()
	else:
		_timer.stop()
	_send(on)


func _send(on: bool) -> void:
	if api == null or api.player_channel == null or not api.player_channel.is_open():
		return
	sent.append(on)
	api.player_hold(on)
