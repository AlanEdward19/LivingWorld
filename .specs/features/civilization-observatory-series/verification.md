# Verificação independente final: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F1 a F4, todos de poder de teste). Código verificado: `3fb9dd39`, base `b29ed9f2`. Verifier sem
autoria. Nenhuma métrica saiu errada na recontagem independente: o código está certo, mas 13 de 117 mutações
sobreviveram, e 10 delas mudam valor publicado que a issue pede ou que o plano define.

## Evidência de execução

| Comando | Resultado |
| --- | --- |
| `bash scripts/verify.sh` | saída 0, `verify: OK`, Unit 2500 aprovados, 3 pulados, 0 reprovados |
| `bash scripts/test.sh --scenarios --filter ...CivilizationDiversityBaselineTests` | saída 0, 3/3 |
| `bash scripts/test.sh --project longrunning --filter ...Civ_report` | saída 0, 1/1 |
| `bash scripts/civ-report.sh scenarios/default.json 1 20 10 <saída>` | saída 0, sem exceção |

Custo (`compare-simulation-cost.sh`, +5% do gate): adiado para o fim da integração, por instrução; não avaliado.

## Fatia A: série anual

| Critério | Status | Evidência |
| --- | --- | --- |
| Observatório não altera `CanonicalHash` | PASS | sonda própria, 8 seeds × 10 anos × 2 braços: hash igual com e sem; H01 e H02 reprovadas |
| Só leitura, fora do tick | PASS | não é `ISimulationSystem`; `CivilizationObserver.cs` e `YearEventTally.cs` só leem |
| Soma das cidades = total, em todo ano | PASS | 20 × 10: 200 anos, 0 divergências; sonda confere contra o estado |
| Conservação por fonte independente do resíduo | PASS | sonda sobre o estado + `ColdArchive`: 0 divergências; 20 × 10: `net_migration` total 0; braço com pool: 400 emigrados |
| `net_migration` ≤ 0, `population_unassigned` = 0 | PASS | 20 × 10 e sonda: nenhum ano viola |
| Nascimentos e mortes por fonte independente | PASS | sonda por `BirthDate`/`DeathDate`: 30 e 76 (padrão), 26 e 93 (pool), 0 divergências |
| Mortes por causa somam `deaths` | PASS | sink próprio por `Kind` + `SourceSystem`: 57 fome/sede, 19 idade, 0 divergências; C01 a C09 reprovadas |
| `cities_founded` e `cities_merged` | PASS | ids novos e delta de `MergedIntoCityId` por ano: 37 e 25 (8 seeds), 0 divergências; 20 × 10: 93 e 63 |
| Tábua de vida sintética (50, 280/11, 15) | PASS | 280/11 refeito à mão; L01 a L05 reprovadas. Truncamento de `q`: ver F1 |
| Gini com pool uniforme | PASS | sonda por definição (Σ\|xi−xj\|/2n²μ), pool expandido, erro < 1e-9; G01 a G05 reprovadas |
| Desemprego por adultos | FAIL | valor certo, sem teste sobre idosos: F2 |
| Formato aberto, chaves documentadas | PASS | dicionário `metric → valor`; `docs/domain/civilization-observatory.md`, 61 linhas |

## Fatia B: CLI e relatório

| Critério | Status | Evidência |
| --- | --- | --- |
| Mesma seed em dois processos, JSON byte-idêntico | PASS | 20 × 10 com cultura `de_DE` e sem PGO: `cmp` idêntico, sha256 `54b07219...`; teste LongRunning verde |
| Paralelo sem estado compartilhado | PASS | 20 processos de 1 seed = série do `Parallel.For`, seed a seed; R04 reprovada. Estáticos são imutáveis ou `ConditionalWeakTable` por instância |
| Ordem de `Dictionary` e cultura no JSON | PASS | métricas por ordinal; máquina em pt-BR gera ponto decimal; JSON válido, sem NaN nem BOM |
| 20 × 10 sem exceção, contrato válido | PASS | script saída 0; `Twenty_seeds_for_ten_years...` verde; B02 reprovada |
| Guardas `seeds` e `anos` ≤ 0 | PASS | J05 e J06 reprovadas |
| Script e README | PASS | `scripts/civ-report.sh`, linha em `scripts/README.md`; `5 3 2` gera seeds [5,6,7] × 2 anos |
| Ordem dos argumentos do modo CLI | FAIL | correta hoje, sem teste: F3 |
| Valores de `scenario`, `tick`, `summary` | FAIL | corretos hoje (recálculo em Python, 24 chaves, 0 divergências), sem teste: F4 |

## Fatia C: diversidade

| Critério | Status | Evidência |
| --- | --- | --- |
| Congelado = 0, embaralhado > 0 | PASS | `TrajectoryDiversityTests`; D07 a D12 reprovadas |
| Fórmula e baseline de 20 seeds | PASS | reimplementação em Python bate com o baseline em 1e-12 (índice 0,268753...); B01 reprovada |
| Cada métrica lê a própria série | PASS | D01 a D04 e D06 reprovadas |
| R3, sem limiar literal | PASS | limiares vêm do baseline; literais de teste são entrada sintética ou valor feito à mão |
| Baseline do cenário de referência | fora | depende da fatia D, por instrução |

## Escopo

29 arquivos, 25 novos, 4 alterados, 2048 inserções e 0 remoções (`git diff --numstat`). Nenhum golden ou hash regravado:
o único baseline tocado é `civilization-diversity.json`, novo. Fatias D e E intocadas.

## Mutações

117 aplicadas uma a uma, escolhidas sem consultar rodadas anteriores, restauradas com `git checkout`; `git status` limpo
ao final. 104 reprovadas (97 no gate rápido, 5 em `Scenario`, 2 em LongRunning); 13 sobreviveram. Cobertura: valor
zerado (V), causa (C), escopo e soma (S), Gini e pool (G), tábua (L), fusão (F), filtros e drenagem (T), runner (R),
hash (H), diversidade (D), JSON e CLI (J), baseline e contrato (B). V22 e V23 foram refeitas (a primeira forma não
compilava; a segunda era equivalente).

## FAIL

- **F1, truncamento de `q` sem teste.** `src/LivingWorld.Simulation/Observation/LifeTable.cs:30`. Sem o `Math.Min`
  (L10), nenhum teste reprova, e 117 de 700 valores do relatório 20 × 10 mudam, com e₀ negativa (seed 1, ano 1: 2,5 vira
  −0,83; mínimo −37,8). Precisa de tábua sintética com faixa em que `n·m/2 > 1` e valor feito à mão.
- **F2, idosos no denominador do desemprego sem teste.** `src/LivingWorld.Simulation/Observation/CivilizationObserver.cs:95`.
  Com `!= LifeStage.Child` (S14), nenhum teste reprova, e 70 de 700 valores mudam (seed 2, ano 6: 0,0769 vira 0,0625). O
  plano define adultos por `LifeStageRules`. Precisa de fixture com idoso sem emprego em `CivilizationScopeTests`.
- **F3, ordem `<seeds> <anos>` do CLI sem teste.** `src/LivingWorld.Workers/Program.cs:20`. J16 (troca) sobrevive porque
  o teste usa `3 2 2`, simétrico (`DeterminismTwoProcessTests.cs:109-111`), e só compara bytes. Precisa de argumentos
  distintos e de conferir quantas seeds e quantos anos o JSON traz.
- **F4, valores do JSON só têm teste de forma.** `src/LivingWorld.Simulation/Observation/CivilizationReport.cs:17,28,59-60,67-69`
  e `src/LivingWorld.Workers/CivilizationReportCommand.cs:29`. Sobrevivem `scenario` com caminho absoluto (J02), `mean`,
  `min` e `max` errados (J07 a J09), `summary.seeds` e `years` = 0 (J10, J11) e `tick` = 0 (J14). Precisa de teste de
  valor em `CivilizationReportTests` para esses campos, contra `SeedRun` conhecido.

## Observações não bloqueantes

- G06 (guarda de riqueza 0 no Gini) e D05 (guarda per capita): só agem com riqueza total 0 ou mundo extinto, ausentes
  em 20 × 10. D05 vale cobrir na fatia D, em que extinção é plausível: `WriteNumber` zeraria o `Infinity` em silêncio.
- D13 (`Min` → `Max` de anos): equivalente no caminho do CLI, em que toda seed tem os mesmos anos.
- `deaths_age` inclui combate (`CombatEncounterSystem` usa `Death`): documentado no plano; a issue manda mapear por kind.
- Clones de `NpcInstantiationMechanic` entram sem `Birth` e cairiam em `net_migration`: não ocorre no cenário padrão.
- Os braços de `CivilizationConservationTests` têm 3 anos; a sonda deste Verifier cobriu 10 anos, sem divergência.
- `cities_founded` e `cities_merged` só existem no mundo, não por cidade; não bloqueia o uso previsto.
