extends RefCounted
## Identidade local do jogador: um `playerId` gerado uma vez e guardado em `user://player.cfg`.
## Ainda não há autenticação (#63): quem tem o id, controla o NPC. Aleatoriedade aqui é do cliente, não da simulação.

const DEFAULT_PATH := "user://player.cfg"


static func load_or_create(path: String = DEFAULT_PATH) -> String:
	var config := ConfigFile.new()
	if config.load(path) == OK:
		var saved := String(config.get_value("player", "id", ""))
		if saved != "":
			return saved
	var id := "p-" + Crypto.new().generate_random_bytes(8).hex_encode()
	config.set_value("player", "id", id)
	config.save(path) # sem disco, o id vale só nesta sessão
	return id
