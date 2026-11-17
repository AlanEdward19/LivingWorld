extends RefCounted
## Hora do mundo contínua para quem anda pela rota (AD-181): o tick do último delta (`store.last_tick`) e o do relógio
## (`/simulation/status`) a ancoram; entre eles avança `ticksPerSecond` por segundo, nunca mais de um tick à frente do
## servidor (o motor publica um delta por tick). Pausado não anda; o passo manual chega pelo delta e salta para ele.

var tick := 0.0
var rate := 1.0 ## ticks por segundo (velocidade do jogo)
var paused := false
var server_tick := -1


## Tick que o servidor disse (delta ou status): o relógio nunca fica atrás dele.
func observe(server: int) -> void:
	if server < 0:
		return
	if server > server_tick:
		server_tick = server
	if tick < float(server_tick):
		tick = float(server_tick)


func set_status(server: int, ticks_per_second: float, is_paused: bool) -> void:
	rate = maxf(ticks_per_second, 0.0)
	paused = is_paused
	observe(server)


func advance(seconds: float) -> void:
	if paused or server_tick < 0:
		return
	tick = minf(tick + seconds * rate, float(server_tick) + 1.0)
