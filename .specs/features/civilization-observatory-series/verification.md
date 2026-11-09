# Verificação independente, rodada 6: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F4, lacuna de teste restante do achado F3 da rodada 5). Código verificado: `689812e8`, base `main` = `38972071`.
Verifier sem autoria. O código publicado está certo; nenhum valor errado foi achado. F1 e F2 fechados; F3 fechado só em parte.
Histórico: rodada 5 reprovou por F1 a F3 (poder de teste em `CivilizationObserver.cs`); `689812e8` acrescentou 3 testes.

## Falha

| Id | Onde | Quebra que nenhum teste detecta | O que precisa mudar |
| --- | --- | --- | --- |
| F4 | `CivilizationObserver.cs:168` | `int count = Ages.Count == 0 ? 0 : Math.Max(Ages.Max() / bandYears + 1, DeathsByBand.Count);`: com escopo sem vivos, `count` = 0 e o e₀ publicado vira 0. Os 88 testes do observatório passam. O pedido da rodada 5 era "e com cidade sem vivos"; o teste novo (`CivilizationObserverValueTests.cs:202`) mantém vivos na cidade | Teste com cidade sem nenhum vivo e morte no ano, e₀ esperado à mão (morte aos 59 anos: 55,5, como no teste atual) |

Reprodução de F4: aplicar o mutante, `bash scripts/test.sh --filter FullyQualifiedName~Observation` sai 0; `bash scripts/civ-report.sh <cenário> 1 20 10 <saída>` com e sem ele muda **116 valores** de `life_expectancy_at_birth`:

| Cenário (20 seeds × 10 anos) | Valores que mudam | Exemplo |
| --- | --- | --- |
| `default.json` | 7 | seed 2, ano 8, cidade `4aad0690`: 0 vivos, 1 morte, 10,5 vira 0 |
| `test-scifi.json` | 73 | seed 1, ano 1, **mundo**: 2,5 vira 0 |
| `test-living-village.json` | 36 | seed 2, ano 4, **mundo**: 17,5 vira 0 |

Conferência de 10,5 pela fórmula de `LifeTable.cs:18-36`: faixa 2 aberta, exposição 0 + 1/2, m = 2, L = 0,5; faixas 0 e 1, 5 cada.

## F1 a F3: mutações e recontagem

| Achado | Mutante | Resultado | Teste que reprova |
| --- | --- | --- | --- |
| F1 | `sum.Deaths =` | Reprovado, esperado 5, obtido 2 | `Deaths_and_causes_of_chained_merged_cities_add_up_in_the_surviving_city` |
| F1 | `sum.ByCause[cause] = n` | Reprovado, esperado 3, obtido 1 | o mesmo |
| F2 | `sum.DeathsByBand[i] =` | Reprovado, esperado 56,5, obtido 57 | `Deaths_in_the_same_age_band_of_two_merged_cities_add_up_in_life_expectancy` |
| F3 | `count` sem `DeathsByBand.Count` | Reprovado, esperado 55,5, obtido 52,5 | `A_death_in_an_age_band_above_every_living_person_still_counts_in_life_expectancy` |
| F3 | `count` = 0 sem vivos (F4) | **Sobreviveu** | nenhum |

Recontagem à mão, reimplementando `LifeTable.cs:18-36` em Python:
- F1: A = 2 idade + 1 doença, B = 1 idade + 1 doença: 5 mortes, 3 idade, 2 doença; nascimentos 2 + 1 = 3. Bate.
- F2: faixas 0 a 10 sem morte somam 55; faixa 11 aberta com 3 mortes, exposição 3 + 1,5, L = 1,5: **56,5**. Mutantes: 58,5 (1 morte) e 57 (2 mortes). Bate.
- F3: faixa 11 com 1 morte, exposição 0,5, m = 2, L = 0,5: **55,5**. Mutante: faixa 10 aberta, sem mortes: 52,5. Bate.

## Critérios das fatias

| Fatia | Critério | Resultado | Evidência própria |
| --- | --- | --- | --- |
| A | Observatório não altera `CanonicalHash` | PASS | `CivilizationObserverTests.cs:22`; mutante com `world.NextCityId()` no `Sample` reprova esse teste |
| A | Soma das cidades = total | PASS | `CivilizationObserverTests.cs:68`; Python em 600 anos-seed (3 cenários): 0 violações |
| A | Conservação de população | PASS | `CivilizationConservationTests.cs:52,68,85`; Python: Δ = nascimentos − mortes + migração, 0 violações, `net_migration` nunca > 0 |
| A | Tábua de vida sintética | PASS | `CivilizationObserverTests.cs:100,107,115` |
| A | Sink = estado; causas somam mortes | PASS | `CivilizationObserverTests.cs:81,92`; Python por cidade e mundo, 0 violações |
| A | Poder de teste das métricas | **FAIL** | F4 |
| B | Dois processos, JSON byte-idêntico | PASS | `civ-report.sh` 2×, 20 × 10: `cmp` ok, SHA-256 `54b07219…`; teste LongRunning `Civ_report_…` |
| B | Paralelo = sequencial | PASS | `CivilizationReportTests.cs:35` |
| B | 20 × 10 sem exceção; contrato | PASS | Script saiu 0 nos 3 cenários; `CivilizationDiversityBaselineTests.cs:21` no `--full` |
| B | `scripts/README.md`, formato documentado | PASS | `git diff`; `docs/domain/civilization-observatory.md` |
| C | Congelado = 0, embaralhado > 0, à mão | PASS | `TrajectoryDiversityTests.cs:23,34,46`; mutante sem escala reprovado |
| C | Baseline de 20 seeds | PASS | `CivilizationDiversityBaselineTests.cs:30`; índice do relatório 0,26875306511675806 = baseline, após o merge |
| Comum | Gate +5% | Não medido | Adiado pelo orquestrador desde a rodada 5; não conta aqui. Observação isolada: 88 testes, 12 s (carga alta) |

Baseline do cenário de referência (D) e fatias D e E: fora desta verificação.

## Merge e determinismo

- `main` = merge-base = `38972071`. `git diff --name-status main HEAD`: só 4 modificados (`.gitignore`, `scripts/README.md`, `Program.cs`, `DeterminismTwoProcessTests.cs`); o resto é novo.
- Goldens: nenhum regravado. Em `tests/baselines` só há o novo `civilization-diversity.json`; `scenarios/` intocado.
- Sem `Random`, `DateTime`, `Guid`, `Stopwatch` nem libm em `Simulation/Observation/` (grep vazio). `Parallel.For` só no Workers, com um mundo por seed.
- Observador não escreve no mundo: só leitura em `CivilizationObserver.cs`; o mutante de escrita reprova.
- `git diff -- src/` vazio e `git status` limpo depois das mutações.

## Mutações (19, uma a uma, restauradas)

18 reprovadas: F1 (2), F2, F3, `count` só por `DeathsByBand`, cadeia `while`→`if`, `sum.Births =`, exposição `alive + deaths`, `Group.Add` (3), guarda `Deaths == 0`, `net_migration` sem mortes, escrita no mundo, faixa fixa no sink, aberta `l*m` na tábua, diversidade sem escala, relatório sem ordenar seeds.
1 sobrevivente: F4, não equivalente (muda 116 valores publicados).

## Gate

`bash scripts/verify.sh --full`: saída 0, `verify: OK`, 17 min 58 s, carga 10,7 a 4,6. Nenhum teste de custo falhou; sem repetição.

| Suíte | Aprovados | Ignorados | Falhas |
| --- | --- | --- | --- |
| Unit (gate) | 2538 | 3 | 0 |
| Unit `Scenario` | 23 | 1 | 0 |
| Integration | 450 | 0 | 0 |
| LongRunning | 146 | 8 | 0 |
