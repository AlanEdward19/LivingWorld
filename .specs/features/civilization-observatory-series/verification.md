# Verificação independente, rodada 4: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F1, F2 e F3, os três de poder de teste). Código verificado: `c9055e0f`, base `b29ed9f2`.
Verifier sem autoria; conclusão formada antes de abrir o `verification.md` anterior. A recontagem independente não
achou nenhuma métrica errada: o código publicado está certo, mas três quebras reais em valor publicado passam em
todos os testes. Medição de custo e critério de +5% adiados por decisão do orquestrador (fora desta rodada).

## Falhas (bloqueantes)

| Id | Onde | Mutação sobrevivente | Efeito publicado medido | O que falta |
| --- | --- | --- | --- | --- |
| F1 | `tests/LivingWorld.Tests.Unit/Observation/CivilizationConservationTests.cs:52` e `:68`; métrica em `CivilizationObserver.cs:67` | `- all.Births` → `+ all.Births` em `net_migration` | seed 1, ano 6: 0 → 2; 14 de 30 anos-seed mudam (seeds 1 a 3, 10 anos, `default.json`) | Os dois braços (seed 1, 3 anos) têm 0 nascimentos em todos os anos: o termo de nascimentos da conservação nunca é exercido. Usar braço com nascimentos, exigir `births > 0` e afirmar `net_migration` |
| F2 | `tests/.../CivilizationObserverTests.cs:122`, `CivilizationScopeTests.cs:76`; código em `WealthInequality.cs:20` | `value * weight / wealth` → `value / wealth` | Gini 0,370 → 0,867 com pool de 50 (30 de 30 anos) | O bloco de peso > 1 do teste tem valor 0, e o teste de escopo compara com a própria `Gini`. Falta bloco de peso > 1 e valor > 0 contra resultado independente (a issue pede o pool no Gini) |
| F3 | `YearEventTally.cs:14`; testes usam `tally.AgeBandYears` (`CivilizationObserverValueTests.cs:121`, `CivilizationScopeTests.cs:213`) | `DefaultAgeBandYears` 5 → 10 | e₀ 2,5 → 5 (seed 1, ano 1); 12 de 30 anos-seed mudam | O plano fixa faixas de 5 anos e nada prende a largura usada pelo CLI. Afirmar e₀ de uma corrida do `CivilizationRunner` contra tábua de 5 anos calculada à parte |

## Fatia A: série anual

| Critério | Resultado | Evidência |
| --- | --- | --- |
| Observatório não altera `CanonicalHash` | PASS | Teste do gate; meu harness comparou relógio puro × `CivilizationRunner` em 6 seeds, 10 anos; O55 (escrita no tesouro) reprovada |
| Não é `ISimulationSystem`, sem escrita nem evento | PASS | `CivilizationObserver.cs:12` e `YearEventTally.cs:12`; só leituras no código |
| Soma das cidades = total; vivos + pool | PASS | 20 seeds × 10 anos no JSON (Python); `population_unassigned` = 0 em todo ano |
| Conservação por fonte independente | PASS no valor, FAIL no teste (F1) | Harness: `population_delta` e `net_migration` = −(ids que saíram do pool sem virar NPC nem morto), 6 seeds; no JSON, `net_migration` = 0 em 200 anos-seed |
| Nascimentos e mortes por fonte independente | PASS | `BirthDate`/`DeathDate` (estado + `ColdArchive`) × eventos crus, seeds 1, 2, 3, 7, 11, 19 |
| Mortes por causa | PASS | Eventos crus com junção por id e tick no estado, 6 seeds; soma = `deaths` em todo ano e cidade |
| `cities_founded`, `cities_merged` | PASS | Diferença anual de `world.Cities` e de `MergedIntoCityId`, 6 seeds. Único ponto de fusão: `WorldState.Cities.cs:49`, chamado por `SpatialSettlementFoundingSystem.cs:202` |
| Tábua de vida | PASS no valor, FAIL no teste (F3) | Valores sintéticos 50, 280/11 e 15; e₀ recalculado à parte igual nas 6 seeds |
| Gini com pool como bloco uniforme | PASS no valor, FAIL no teste (F2) | Diferença média absoluta (fórmula distinta) igual no mundo e por cidade, 6 seeds |
| Formato aberto `metric → valor`, chaves documentadas | PASS | `CivilizationSample.cs`; `docs/domain/civilization-observatory.md` |

## Fatia B: CLI e relatório

| Critério | Resultado | Evidência |
| --- | --- | --- |
| Mesma seed em dois processos: JSON byte-idêntico | PASS | 3 processos, 20×10: sha256 `54b07219…`, com `pt_BR`+`Asia/Tokyo`, invariante+`C`; teste LongRunning passa |
| Paralelo = sequencial | PASS | 20×10 com `degreeOfParallelism: 1` idêntico ao paralelo (`cmp`) |
| 20 seeds × 10 anos sem exceção; contrato | PASS | `bash scripts/civ-report.sh`, saída 0; forma igual a `tests/contracts/shape.civ-report.json`; teste `Scenario` passa |
| Estado estático compartilhado | PASS | `Building.s_layoutVersion` e `Relationship.s_axesVersion` são monotônicos com `Interlocked`: só invalidam a mais. Caches por `ConditionalWeakTable` ou função pura. Contadores de `DecisionContextCache` são diagnóstico |
| Ordem de `Dictionary` e cultura | PASS | Iterações sobre dicionário só somam; métricas e `final` em ordem ordinal; `Utf8JsonWriter` independe de cultura (bytes acima) |
| Duração ou data no JSON | PASS | Nenhum campo |
| Script e README | PASS | `scripts/civ-report.sh`, `scripts/README.md` |
| Sumário `final` | PASS | min, média e máx recalculados em Python para as 24 chaves |

## Fatia C: diversidade

| Critério | Resultado | Evidência |
| --- | --- | --- |
| Congelado = 0; embaralhado > 0 | PASS | `TrajectoryDiversityTests`; D06 a D13 reprovadas |
| Fórmula documentada e valor à mão | PASS | Recalculada em Python: índice 0,268753 igual ao JSON e ao baseline (tolerância 1e-10) |
| Baseline 20 seeds (parte do gate) | PASS | `tests/baselines/civilization-diversity.json`; parte de referência depende da fatia D, não exigida |
| R3: sem limiar literal | PASS | Só invariantes (`> 0`, `== 0`), valores à mão e parâmetros de braço; alvo vem do baseline |

## Escopo e gate

- Nenhum golden/hash regravado: em `tests/baselines` só um arquivo novo; `DeterminismTwoProcessTests.cs` só ganhou teste.
- Diff sem extrapolação: nada em `src/LivingWorld.Api`, `src/LivingWorld.Domain` nem `scenarios/`.
- `bash scripts/verify.sh` saiu 0 antes e depois das mutações: 2509 aprovados, 3 ignorados, 0 falhas.
- `Category=Scenario` da entrega: 3 aprovados. LongRunning `Civ_report`: 1 aprovado.

## Mutações

135 aplicadas (uma a uma, restauradas com `git checkout`, `git status` limpo no fim), cobrindo cada métrica e campo: 126
reprovadas (D08 só pelo baseline `Scenario`), 9 sobreviventes. P3 (modo CLI não reconhecido) reprova, mas o teste
travou porque o processo cai no modo host e `RunCommand` não tem prazo: reprovou depois que encerrei os processos.

| Sobrevivente | Classificação |
| --- | --- |
| O31, W05, T12 | **FAIL**: F1, F2 e F3 acima |
| O27 (`<= tick` → `< tick` em fundadas) | Não bloqueante: exige fundação na virada do ano; fundação = virada de mês + `OrganizationTicks`, que vale 1 nos 3 cenários do repositório. Alcançável só com cenário próprio de múltiplo de mês |
| O54 (`Technologies` em `Group.Add`) | Equivalente: `CivilizationObserver.cs:62` sobrescreve o valor do mundo |
| R20 (ordem ordinal das métricas) | Detalhe: mesmas chaves e valores, saída ainda determinística |
| D15 (`Take(years)`) | Inalcançável: o CLI dá o mesmo número de anos a toda seed |
| D16 (ordenar seeds na diversidade) | Equivalente: distância simétrica e CLI já em ordem |
| D18 (`Math.Abs` na escala) | Inalcançável: as séries publicadas não são negativas |

## Observações não bloqueantes

- `civilization-observatory-cli/plan.md` diz que nenhum sistema usa `Parallel`; `NpcWakeScheduler.cs:177` usa (cálculo puro por índice, sem efeito no determinismo).
- Nenhum teste executa `scripts/civ-report.sh`; a ordem dos argumentos foi conferida à mão (execução acima).
- O baseline roda em `Category=Scenario`, fora do `verify.sh` padrão (entra em `--full`), como o plano define.
- `net_migration` é resíduo: outra origem de NPC sem `Birth` (ex.: `NpcInstantiationMechanic`) cairia nele.
