# Verificação independente, rodada 7: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F5 e F6, lacunas de teste). Código verificado: `37233e15`, base `main` = `38972071`. Verifier sem autoria.
O código publicado está certo: nenhum valor errado foi achado. F4 da rodada 6 está fechado. Custo e `--full` passam.

## Falhas

| Id | Onde | Quebra que nenhum teste detecta | O que precisa mudar |
| --- | --- | --- | --- |
| F5 | `CivilizationObserver.cs:64` | `c.FoundedAtTick <= tick` virar `< tick`: cidade fundada no último tick do ano some da série (o ano seguinte exige `> _previousTick`). Os 90 testes do filtro `Observation` e os de `--scenarios` passam | Teste com cidade de `FoundedAtTick` igual ao tick de fim de ano: conta 1 nesse ano e 0 no seguinte |
| F6 | `CivilizationObserver.cs:152` | Sem a guarda `Ages.Count == 0 ? 0 :`, `age_mean` vira NaN em escopo sem materializados, e `summary.final` perde a média. Os 90 testes passam (`AssertScope` só confere `age_mean` com `ages.Count > 0`, `CivilizationScopeTests.cs:79`) | Teste com escopo sem vivos: `age_mean` = 0, e `summary.final.age_mean.mean` calculada com esse 0 |

Reprodução de F5. `WorldClock.Tick` avança a data antes dos sistemas (`WorldClock.cs:25`), então o tick de fim de ano roda e pode fundar.
Todo cenário versionado usa `OrganizationTicks: 1`: as 305 fundações de 3 cenários (20 seeds × 10 anos) têm `tick % 720 = 1`, e o mutante não muda nenhum valor neles. A entrada concreta é `default.json` com `OrganizationTicks: 7920`, valor aceito por `CityRules.cs:78` (só exige > 0): fundações agendadas no mês 1 caem no fim do ano.

| Entrada: `civ-report` 20 × 10 | Fundações, código atual | Fundações, mutante | Valores publicados que mudam |
| --- | --- | --- | --- |
| `default.json` com `OrganizationTicks: 7920` | 59 | 8 | 34 (exemplo: seed 1, ano 1, `cities_founded` 1 vira 0) |

Reprodução de F6: aplicar o mutante e rodar `bash scripts/civ-report.sh scenarios/test-living-village.json 1 20 10 <saída>`. `summary.final.age_mean.mean` passa de 26,44684849013628 para 0 (seeds 2, 4 e 12 terminam sem materializados; o NaN contamina a soma e `WriteNumber` grava 0). Nos cenários `default` e `test-scifi` nenhum valor muda.

## F4 da rodada 6: fechado

Mutante em `CivilizationObserver.cs:168`: `int count = Ages.Count == 0 ? 0 : Math.Max(Ages.Max() / bandYears + 1, DeathsByBand.Count);`.
`bash scripts/test.sh --filter FullyQualifiedName~Observation` sai 1: 2 reprovados, 88 aprovados, `Expected: 55,5 / Actual: 0`, em `A_city_with_nobody_alive_at_year_end_still_has_life_expectancy_from_its_deaths` e `A_world_with_nobody_alive_at_year_end_still_has_life_expectancy_from_the_deaths_of_the_year`. É o motivo certo: sem vivos, `count` = 0 e e₀ vira 0.

Conta à mão por `LifeTable.cs:18-36`, morte aos 59 anos, faixas de 5: faixa = 59/5 = 11, então 12 faixas. Faixas 0 a 10: sem mortes, taxa 0, q = 0, L = 5·(1+1)/2 = 5 cada, soma 55. Faixa 11, aberta: exposição 0 + 1/2, m = 1/0,5 = 2, L = l/m = 0,5. **e₀ = 55,5**, o valor dos testes (`CivilizationObserverValueTests.cs:238,252`).

## Critérios das fatias

| Fatia | Critério | Resultado | Evidência própria |
| --- | --- | --- | --- |
| A | Observatório não altera `CanonicalHash` (no gate) | PASS | `CivilizationObserverTests.cs:22`, sem `Category=Scenario`; mutante que chama `world.NextCityId()` no sink reprova só esse teste |
| A | Soma das cidades = total; conservação | PASS | `CivilizationObserverTests.cs:68`, `CivilizationConservationTests.cs:52,68,85`; `--full` |
| A | Tábua de vida sintética | PASS | `CivilizationObserverTests.cs:100,107,115`; mutantes de `q`, de `L` e da faixa aberta reprovam |
| A | Métricas por cidade e mundo, formato aberto | PASS no valor, **FAIL no poder de teste** | `CivilizationObserver.cs:141-161`; F5 e F6 |
| B | Dois processos, JSON byte-idêntico | PASS | `civ-report.sh` 2×, 20 × 10: `cmp` ok, SHA-256 `54b07219…`; `DeterminismTwoProcessTests.cs:102` no `--full` |
| B | 20 × 10 sem exceção; contrato; paralelo = sequencial | PASS | Script saiu 0 em 3 cenários; `CivilizationDiversityBaselineTests.cs:21`, `CivilizationReportTests.cs:35` |
| B | Script e formato documentados | PASS | `scripts/civ-report.sh`, `scripts/README.md`, `docs/domain/civilization-observatory.md` |
| C | Congelado = 0, embaralhado > 0, valor à mão | PASS | `TrajectoryDiversityTests.cs:23,34,46` |
| C | Baseline de 20 seeds | PASS | Índice do relatório 0,26875306511675806 = `tests/baselines/civilization-diversity.json` |
| Comum | Gate até +5% | Inconclusivo | 3 pares alternados de `verify.sh`, carga ~8: `main` 99,3 / 86,0 / 89,8 s; branch 98,8 / 95,2 / 94,1 s. Mediana +6,0%, média +4,8%; o ruído entre rodadas passa de 10%. Não conta no veredito |

Fatias D e E e o baseline do cenário de referência: fora desta verificação.

## Determinismo e pureza

- Grep em `Simulation/Observation/` (8 arquivos da feature): sem escrita em `world.*`, `ctx.`, `LogEvent`, `ScheduleEvent`, `Random`, `DateTime`, `Guid.New`, `Stopwatch` nem libm. `Parallel.For` só no Workers, um mundo por seed.
- Goldens: `git diff --name-status 38972071 HEAD` só modifica `.gitignore`, `scripts/README.md`, `Program.cs` e `DeterminismTwoProcessTests.cs`. Em `tests/baselines` só há o novo `civilization-diversity.json`; `scenarios/` intocado.
- `git diff -- src/` vazio e `git status` limpo depois de cada leva de mutações.

## Mutações (38, uma a uma, restauradas): 25 reprovadas, 13 sobreviventes

Reprovadas: F4; Gini sem ordenar; escala só por anos; payload sem corte em `|`; faixa sem largura; escrita no mundo pelo sink; `Drain` sem zerar; fundação `>=`; população anterior sem pool; mediana inteira; faixa mais velha −1; pool sem per capita; `population_unassigned` invertido; `Group.Add` sem riqueza; `q` e `L` da tábua; faixa aberta ignorada; ordem dos anos; runner sem seed; `MaxIterations` = 1; guardas de mediana e desemprego; fundadas só ativas; exaustão fora; `min` trocado.

| Sobrevivente | Classificação |
| --- | --- |
| `<= tick` (F5), guarda de `age_mean` (F6) | **Lacuna real**: muda valor publicado pelo `civ-report` |
| `metrics ??=` ignorado, `ageBandYears` do runner ignorado | Parâmetro sem nenhum chamador: não muda a saída do CLI; extensão por `DefaultMetrics` segue valendo |
| Sem `Take(years)`, sem `Where` de anos vazios, sem `Where(ContainsKey)` | Exigem séries desiguais ou chaves faltando, que o CLI não gera |
| Escala sem `Math.Abs` | Métricas padrão nunca negativas (`Money.cs:16`) |
| Guarda de largura de faixa | Sem entrada pelo CLI |
| `_previousTick = 0` | Equivalente: mundo carregado parte do tick 0 |
| Descarte de cidade não ativa em `byCity`, seed invertida no comando | Equivalentes: só cidades ativas são lidas; o relatório ordena por seed |
| Métricas sem ordenar | Ordem de inserção é fixa; byte-identidade e contrato seguem valendo |

## Gate e custo

`bash scripts/verify.sh --full`: saída 0, `verify: OK`, 18 min 3 s, carga 10,6 a 4,6. Sem falha de custo; sem repetição.

| Suíte | Aprovados | Ignorados | Falhas |
| --- | --- | --- | --- |
| Unit (gate) | 2540 | 3 | 0 |
| Unit `Scenario` | 23 | 1 | 0 |
| Integration | 450 | 0 | 0 |
| LongRunning | 146 | 8 | 0 |

`compare-simulation-cost.sh` contra `main` em `38972071`, depois do `--full`, carga 4,4 a 3,5, saída 0:

| Medida | `main` (mediana) | Branch (mediana) | Razão | Teto |
| --- | --- | --- | --- | --- |
| µs/NPC-tick | 0,818 | 0,807 | 98,66% | 103% |
| Alocação/tick | 258762 | 258725 | 99,99% | 102% |
