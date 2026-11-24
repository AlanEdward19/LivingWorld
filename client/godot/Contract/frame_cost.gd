extends RefCounted
## Custo por etapa do que chega do servidor (AD-188, medida com o mundo rodando): microssegundos somados de ler o JSON,
## conferir o contrato e aplicar o delta. Só contadores estáticos; o `bench` do `dev_steps.gd` zera e imprime.

static var usec: Dictionary = {}
static var count: Dictionary = {}


static func add(stage: String, since_usec: int) -> void:
	usec[stage] = int(usec.get(stage, 0)) + Time.get_ticks_usec() - since_usec
	count[stage] = int(count.get(stage, 0)) + 1


static func reset() -> void:
	usec = {}
	count = {}


## "apply=12.3ms/400 parse=..." (ms somados no período / quantas vezes).
static func report() -> String:
	var parts: Array[String] = []
	var keys := usec.keys()
	keys.sort()
	for key in keys:
		parts.append("%s=%.1fms/%d" % [key, usec[key] / 1000.0, count[key]])
	return " ".join(parts)
