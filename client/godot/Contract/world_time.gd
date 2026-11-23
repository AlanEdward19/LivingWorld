extends RefCounted
## Contrato v2 (ADR-0050, AD-197b): o servidor manda o tempo de mundo em segundos (`second`, `…Second`, `…Seconds`). O cliente
## conta o tempo em horas do mundo (o "tick" de antes: relógio do dia, rota, processos); a troca de unidade é só aqui, na borda.

const SECONDS_PER_HOUR := 3600


## Hora do mundo (piso) de um instante em segundos; `fallback` se o valor não veio.
static func hours(value: Variant, fallback: int = 0) -> int:
	if value == null:
		return fallback
	return floori(float(value) / SECONDS_PER_HOUR)


## Hora do mundo contínua (com a fração da hora) de um instante em segundos.
static func hours_f(value: Variant, fallback: float = 0.0) -> float:
	return fallback if value == null else float(value) / SECONDS_PER_HOUR


## O campo `key` de `source` (em segundos) em horas do mundo.
static func hours_of(source: Variant, key: String, fallback: int = 0) -> int:
	return hours(source.get(key) if source is Dictionary else null, fallback)


## Horas do mundo em segundos, para mandar ao servidor.
static func seconds(hours_value: int) -> int:
	return hours_value * SECONDS_PER_HOUR
