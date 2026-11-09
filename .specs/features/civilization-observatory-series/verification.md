# Verificação independente, rodada 8: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F7 e F8, lacunas de teste). Código verificado: `e712e6c1`, base `main` = `38972071`. Verifier sem autoria.
O código publicado está certo: nenhum valor errado foi achado. F5 e F6 da rodada 7 estão fechados. `--full` passa.

## Falhas

| Id | Onde | Quebra que nenhum teste detecta | O que precisa mudar |
| --- | --- | --- | --- |
| F7 | `CivilizationObserver.cs:103` | `(double)WealthSum / Count` virar `(double)(WealthSum / Count)` (divisão inteira): a riqueza por cabeça do pool é truncada e o `gini` publicado muda. Os 96 testes do filtro `Observation` e os de `--scenarios` passam | Teste de valor com pool de soma não divisível pela contagem e Gini feito à mão. Hoje só há 1000/10 (`CivilizationScopeTests.cs:34`) e `pool * 100` (`CivilizationConservationTests.cs:22`) |
| F8 | `CivilizationReportCommand.cs:28`, soma em `CivilizationReport.cs:62,68` | Inverter a seed por índice (`firstSeed + (ulong)(seeds - 1 - i)`) muda a ordem da soma em `summary.final.*.mean`, que segue a ordem de `runs`, não a de seed. Muda a saída em cenário versionado; magnitude de 1 ulp. A rodada 7 classificou como equivalente: **não procede** | Somar em ordem de seed em `WriteSummary` (o mutante vira equivalente) ou fixar o valor exato em teste |

Reprodução de F7. Entrada: `scenarios/default.json` com `"EmigrationRatePerDeficitUnit": 0` e `Cities[0].AggregatePool` = `{Count: 30, WealthSum: 1000, HealthSum: 3000}`. O carregador exige esses campos (`CityScenarioLoader.cs:234-241`) e o próprio repositório usa taxa 0 com pool de 50 (`CivilizationConservationTests.cs:54`). Comando: `civ-report <cenário> 1 20 10 <saída>`, sem e com o mutante.

| Valor (20 × 10) | Código atual | Mutante |
| --- | --- | --- |
| Valores publicados que mudam | n/d | 205 |
| Seed 1, ano 1, `gini` | 0,6325129052350743 | 0,6337636475081836 |
| `summary.final.gini.mean` | 0,8248824169166944 | 0,8253474034919434 |
| `summary.diversity.index` | 0,1505856885688931 | 0,15048877316390014 |

Com a taxa padrão (0,1) o pool some antes do primeiro fim de ano e o mutante não muda nada. Nos 3 cenários versionados, o pool é 0 em todo fim de ano.

Reprodução de F8: aplicar o mutante e rodar `civ-report` 20 × 10. Em `default.json`, 3 valores mudam (`summary.final.age_mean.mean` 33,29441819618291 vira 33,2944181961829; `gini` e `unemployment_rate` também). Em `test-living-village.json`, 2 valores. Em `test-scifi.json`, nenhum. Nenhum teste reprova. No código atual o CLI sempre passa as seeds em ordem, e a byte-identidade vale.

## F5 e F6 da rodada 7: fechados

| Id | Mutante | Resultado de `bash scripts/test.sh --filter FullyQualifiedName~Observation` | Valor refeito à mão |
| --- | --- | --- | --- |
| F5 | `:64` `<= tick` vira `< tick` | Sai 1: 1 reprovado, 95 aprovados, `Expected: 1 / Actual: 0`, em `A_city_founded_on_the_last_tick_of_a_year_counts_in_that_year_and_not_in_the_next` | Início 0 (sonda). Janela do ano 1 = (0, 100], cidade em 100: conta 1. Ano 2 = (100, 200]: 0. Cidade inicial em 0 fica fora: limite inferior aberto |
| F6 | `:152` sem a guarda | Sai 1: 1 reprovado, 95 aprovados, `Expected: 0 / Actual: NaN`, em `Age_mean_is_zero_in_a_scope_without_living_people_and_the_report_summary_uses_that_zero` | Idades lidas direto de `world.Npcs`, seed 2, com sonda temporária já removida: 21+9+59+41+4+4+27+50+59+34+59+14+33+38+53+41+17+15+37+39 = 654; 654/20 = 32,7; (32,7+0)/2 = 16,35 |

## Critérios das fatias

| Fatia | Critério | Resultado | Evidência própria |
| --- | --- | --- | --- |
| A | Observatório não altera `CanonicalHash` (no gate) | PASS | `CivilizationObserverTests.cs:22`, sem `Category=Scenario`; `--full` |
| A | Soma das cidades = total; conservação | PASS | `CivilizationObserverTests.cs:68,81`, `CivilizationConservationTests.cs:52,68,85`; mutantes de sinal, delta e população anterior reprovam |
| A | Tábua de vida sintética | PASS | `CivilizationObserverTests.cs:100,107,115`; mutantes L2 a L7 reprovam |
| A | Métricas por cidade e mundo, formato aberto | PASS no valor, **FAIL no poder de teste** | `CivilizationObserver.cs:141-161`; F7 |
| B | Dois processos, JSON byte-idêntico | PASS | `bash scripts/civ-report.sh` em processo novo, `cmp` contra corrida anterior: idêntico, SHA-256 `54b07219…`; `DeterminismTwoProcessTests.cs:102` no `--full` |
| B | 20 × 10 sem exceção; contrato; paralelo = sequencial | PASS no valor, **FAIL no poder de teste** | 3 cenários saíram 0; `tests/contracts/shape.civ-report.json`, `CivilizationReportTests.cs`; `MaxDegreeOfParallelism = 1` dá bytes idênticos; F8 |
| B | Script e formato documentados | PASS | `scripts/civ-report.sh`, `scripts/README.md:16`, `docs/domain/civilization-observatory.md` (61 linhas) |
| C | Congelado = 0, embaralhado > 0, valor à mão | PASS | `TrajectoryDiversityTests.cs:23,34,46` |
| C | Baseline de 20 seeds | PASS | Índice do relatório 0,26875306511675806 = `tests/baselines/civilization-diversity.json`. Observação: o teste é `Category=Scenario`, roda no `--full`, não no `verify.sh` simples |
| Comum | Gate até +5% | **Sem conclusão** | Não remedido. O ruído da rodada 7 persiste; não conta no veredito |

Fatias D e E e o baseline do cenário de referência: fora desta verificação.

## Determinismo, pureza e custo

- `git grep` em `e712e6c1` nos 8 arquivos da feature e no comando: sem `Random`, `DateTime`, `Guid.New`, `Stopwatch`, libm, `LogEvent`, `ScheduleEvent`, `ctx.`, escrita em `world.*`. Único campo estático: `TrajectoryDiversity.DefaultMetrics`, somente leitura.
- Goldens: `git diff --stat 38972071 e712e6c1 -- tests/golden tests/baselines scenarios` só adiciona `civilization-diversity.json`. Nenhum golden regravado.
- `git diff 37233e15 e712e6c1 -- src/` vazio (0 bytes): a medição de custo da rodada 7 (98,66% CPU, 99,99% alocação) segue valendo.
- `git status` limpo depois das mutações e da remoção da sonda.

## Mutações (93, uma a uma, restauradas): 75 reprovadas, 18 sobreviventes

Reprovadas: F5, F6 e 73 próprias em `Observation/` e no comando (somas de fusão, cadeia de fusão, vivos, tecnologias, sinais e estado anterior, ano, adultos, desemprego, tesouro, mediana, guardas, faixas, exposição com divisão inteira, tábua de vida, Gini, filtros e causas do sink, `Drain`, 8760 ticks, campos e ordem do relatório, diversidade, guardas e escrita do comando). Uma delas só no `--scenarios`: `total / pairs` sem divisão (baseline).

| Sobrevivente | Saída do CLI em 3 cenários | Classificação |
| --- | --- | --- |
| `:103` divisão inteira (F7), seed invertida (F8) | Muda com a entrada acima / muda 1 ulp | **Lacuna real** |
| `_previousTick = 0` | Idêntica | Equivalente: mundo carregado parte do tick 0 (sonda; ano 1 em 8640) |
| Guarda de pool vazio; filtro `Weight > 0`; guarda `people <= 0` | Idêntica | Equivalentes: uma guarda cobre a outra; sem gente, a riqueza é 0 |
| `Math.Max(0, idade)` | Idêntica | Inalcançável: morto nunca tem idade negativa |
| `ageBandYears` do runner; `metrics ??=` | Idêntica | Parâmetros sem chamador |
| Sem `Take(years)`; sem `Where(ContainsKey)` | Idêntica | Exigem séries desiguais ou chaves faltando, que o CLI não gera |
| Sem ordenar seeds na diversidade; escala sem `Math.Abs` | Idêntica | Distância simétrica; métricas padrão não negativas (`Money.cs:16`) |
| Descarte de cidade não ativa em `byCity`; paralelismo 1 | Idêntica | Equivalentes: só cidades ativas são lidas; mundos independentes |
| Métricas sem ordenar; sem indentação; sem `\n` final | Bytes mudam, 0 valores | Só formato; determinismo mantido. Observação, não reprova |

Reclassificados em relação à rodada 7: seed invertida (lacuna); sem `Where` de anos vazios e guarda de largura de faixa agora reprovam.

## Gate

`bash scripts/verify.sh --full`: saída 0, `verify: OK`, 18 min 1 s, carga 7,5 a 4,5. Sem falha de custo; sem repetição.

| Suíte | Aprovados | Ignorados | Falhas |
| --- | --- | --- | --- |
| Unit (gate) | 2546 | 3 | 0 |
| Unit `Scenario` | 23 | 1 | 0 |
| Integration | 450 | 0 | 0 |
| LongRunning | 146 | 8 | 0 |
