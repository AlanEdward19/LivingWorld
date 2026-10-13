# Tick cost scaling

## Problem

O custo por NPC-tick cresce com a população: `tests/baselines/scale-sensor.json` mede 1,06 µs com 1.000 NPCs e 1,51 µs com 5.000 (alta de 42%), e alocação de 504 KB por tick e 2,9 MB por tick. Num mundo ideal o custo por NPC é plano. `RescheduleBatchParallel` (`NpcWakeScheduler`) existe sem uso em produção e o gap review lista sweep em 10k e par on/off de lazy-need, wake e arquivo frio como ausentes.

Aquecer o tick custa a cada fase seguinte, que soma sistema sobre esse teto (ROADMAP: "a Fase 9 não desliza").

Quando isto entrar, o custo por NPC-tick fica dentro de 110% entre 1.000 e 10.000 NPCs e a alocação por tick deixa de crescer de forma super-linear.

## Flow

Mede primeiro; só otimiza o que o perfil apontar, sem mudar o hash canônico.

1. `MeasureHarness` (exists) - perfil por sistema em 1.000, 5.000 e 10.000 NPCs
2. `NpcWakeScheduler` (exists, extended) - decide se `RescheduleBatchParallel` entra, com prova de determinismo
3. `WorldState` (exists, extended) - índices e buffers reutilizados onde há alocação por tick
4. `scale-sensor.json` (exists) - baseline regravado de forma explícita, com o motivo

## Impact

| Front | What changes |
| --- | --- |
| domain | nenhum termo novo |
| stored data | none - hash canônico e golden idênticos (critério) |

## Relations

None - nenhuma entidade nova.

## Surface

None - nothing consumed outside

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. paralelismo | só entra `RescheduleBatchParallel` se o hash for idêntico ao serial em 20 seeds e dois processos | paralelizar o tick inteiro: ordem de execução entra no hash e quebra o determinismo (ADR-0005) |

- Nada mais aqui Ã© difÃ­cil de reverter

## Criteria

### S1: Custo plano (P1)

**Acceptance Criteria**

1. The system SHALL medir µs por NPC-tick a 10.000 NPCs em no máximo 110% do medido a 1.000
2. The system SHALL manter a alocação por NPC-tick a 10.000 em no máximo 110% da medida a 1.000
3. WHEN o sensor roda THEN the system SHALL falhar se o custo passar do baseline versionado

**Independent test:** `ScaleScenarioSensorTests` com a faixa nova.

### S2: Sem regressão de resultado (P1)

**Acceptance Criteria**

4. The system SHALL manter o golden `tests/golden/world-hashes.json` inalterado
5. The system SHALL produzir hash idêntico em dois processos, mesma seed e mesmos ticks
6. IF uma otimização muda o hash THEN the system SHALL reverter a otimização

**Independent test:** golden e dois processos.

## Out of scope

| Excluded | Why |
| --- | --- |
| otimização que muda o hash | vai por AD e regrava golden |
| Postgres e escala horizontal | ADR futuro |
| bytes por NPC por ano | issue do `snapshot-positional-binary` |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| horizonte | gate rápido (14 dias) para 10.000 NPCs | AD-083, orçamento de tempo dos testes | n |
| perfil | `dotnet-counters`/stopwatch por sistema | já usado pelo `MeasureHarness` | n |

**Open questions:** none - all resolved or logged above.

## Observable

None - no user-facing surface

## Sources

- `tests/baselines/scale-sensor.json`
- `docs/roadmap/phase-09-scale.md`
- ADR-0005 - simulação determinística semeada
