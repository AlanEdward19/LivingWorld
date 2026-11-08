# Verificação independente: observatório de civilização (#112, fatias A, B e C)

**Veredito: FAIL** (F1 a F4; os demais critérios passam). Commit `f7adb403`, base `b29ed9f2`, Verifier sem autoria no código.

## Gates executados

| Comando | Resultado |
| --- | --- |
| `bash scripts/verify.sh` | exit 0; Unit 2480 ok, 3 skip, 0 falhas |
| `bash scripts/test.sh --scenarios --filter ...CivilizationDiversityBaselineTests` | 3/3 ok |
| `bash scripts/test.sh --project longrunning --filter ...Civ_report` | 1/1 ok |
`compare-simulation-cost.sh` não rodado, por instrução (adiado para o fim da integração).

## Fatia A: série anual

| # | Critério | Status | Evidência |
| --- | --- | --- | --- |
| A1 | Hash igual com e sem observatório, 10 anos | PASS | `Observing_ten_years...` ok; M1 reprova |
| A2 | Soma das cidades = total = vivos + pool | PASS | teste ok; M3 reprova; 400 amostras (20 seeds × 2 braços × 10 anos) sem divergência |
| A3 | Nascimentos − mortes ± migração fecha com a variação | **FAIL (F1)** | identidade verdadeira nos dados, mas o teste é tautológico; M5 sobrevive |
| A4 | Sink de nascimentos/mortes = estado | PASS | teste ok; conferido em 20 seeds por contagem própria |
| A5 | Mortes por causa somam o total | PASS | soma ok; o mapeamento em si está em F3 |
| A6 | Tábua de vida sintética (50, 280/11, 15) | PASS | valores conferidos à mão; M2 reprova |
| A7 | Métricas pedidas pela issue, por cidade e total | **FAIL (F2, F3)** | `cities_founded` errado; vários valores sem teste |
| A8 | Gini com pool como massa uniforme | PASS | fórmula de Lorenz conferida; 2 testes sintéticos |
| A9 | Fórmula de e₀ ligada ao estado | PASS | recálculo independente a partir de `BirthDate`/`DeathDate`: 200 amostras, 0 divergências |

## Fatia B: CLI `civ-report`

| # | Critério | Status | Evidência |
| --- | --- | --- | --- |
| B1 | Mesma seed em dois processos: JSON byte-idêntico | PASS | 20×10 duas vezes, sha256 `3b8f0d99...` igual, `cmp` ok; controle no teste; M12 reprova |
| B2 | Paralelo = sequencial (sem estado compartilhado) | PASS | 20 processos de 1 seed = relatório paralelo, 0 divergências; M11 reprova |
| B3 | Contrato do JSON | PASS | 2×2 e 20×10 ok; M10 reprova |
| B4 | 20 seeds × 10 anos sem exceção | PASS | CLI exit 0; 20 seeds, 10 anos cada |
| B5 | `seeds`/`anos` ≤ 0 falham com exceção | **FAIL (F4)** | comportamento existe (exit 134), mas sem teste; M9 sobrevive |
| B6 | Cultura de formatação | PASS | `LC_ALL=pt_BR.UTF-8` gera arquivo byte-idêntico; 0 decimais com vírgula, 0 NaN/Inf |
| B7 | Script e `scripts/README.md` | PASS | `civ-report.sh` roda; linha no README |

Estado estático: só contadores de diagnóstico, versões com `Interlocked` e caches por instância. Ordem de `Dictionary` não chega à
saída (somas comutativas, métricas por chave ordinal). `RescheduleBatchParallel` não tem chamador em `src/`.

## Fatia C: diversidade

| # | Critério | Status | Evidência |
| --- | --- | --- | --- |
| C1 | Mundo congelado = 0 | PASS | teste ok; M4b reprova |
| C2 | Embaralhado > 0 | PASS | teste ok; M4a reprova |
| C3 | Par à mão (população 1, geral 0,2) | PASS | conferido; M4a e M4b reprovam |
| C4 | Unidade e ordem das seeds não mudam o índice | PASS | testes ok |
| C5 | Baseline 20×10 | PASS | CLI reproduz `0.26875306511675806`; M13 reprova |
| C6 | Sem limiar literal (R3) | PASS | valores vêm do baseline; `> 0` e `<= 0` são estruturais |

## Escopo e goldens

PASS. `tests/golden/world-hashes.json` não está no diff e `git diff --name-status` só traz arquivos novos mais `.gitignore`,
`scripts/README.md`, `Program.cs` e `DeterminismTwoProcessTests.cs`. `src/LivingWorld.Domain` sem mudança. `artifacts/` no
`.gitignore` se justifica: é a saída padrão de `civ-report.sh` e não há arquivo versionado nessa pasta.

## Reprovações

- **F1. Conservação tautológica.** `CivilizationObserver.cs:66-67` calcula `net_migration` como resíduo, então o assert de
  `CivilizationObserverTests.cs:58` é identidade algébrica. O `<= 0` da linha 59 é trivial: a emigração real, medida com contador
  temporário em `City.Emigrate`, foi 0 em todas as amostras do cenário padrão. Um vazamento de população seria rotulado como
  migração. M5 (população deslocada por ano) passa 56/56. Mudar: medir a migração por fonte independente (`City.Emigrate` não emite
  evento), ou exigir `net_migration == 0` num braço com `EmigrationRatePerDeficitUnit = 0`, e comparar `population_delta` com a
  diferença da série de `population`.
- **F2. `cities_founded` subconta.** `CivilizationObserver.cs:64` conta só `active`, e cidade fundada e fundida no mesmo ano some.
  Contagem por `FoundedAtTick` sobre `world.Cities`: 91 reais contra 28 reportadas nas 20 linhas divergentes, todas no ano 1
  (20/20 seeds; seed 7: 8 contra 1). Saldo também não fecha em seed 2 ano 8 e seed 10 ano 10. Mudar: contar em `world.Cities` e
  testar o valor.
- **F3. Valores sem teste (`rules/tests.md`).** M6 (fome/sede vira idade, `YearEventTally.cs:50`) passa 56/56: o teste de causas só
  prova a soma. M7 (zera `age_mean`, `age_median`, `unemployment_rate`, `workshops`, `buildings`, `life_expectancy_at_birth`,
  `cities_founded`, `cities_merged`) passa 56/56. M8 (remove o filtro de `SourceSystem`) passa 56/56: `default.json` não tem fauna,
  e o `Birth` da fauna começa com id numérico que pode colidir com id de NPC. Mudar: testes de valor para o mapeamento, para essas
  métricas e para o filtro.
- **F4. Critério B5 sem teste.** `CivilizationReportCommand.cs:22-23` sem cobertura; M9 (remove as guardas) passa 56/56. Mudar:
  teste de `ArgumentOutOfRangeException` para seeds e anos ≤ 0.

## Mutações (restauradas uma a uma; `git status` limpo ao final)

| Id | Mutação | Resultado |
| --- | --- | --- |
| M1 | observador avança id no mundo | reprovou (hash, baseline) |
| M2 / M3 | quebra `q` da tábua de vida / total ignora a primeira cidade | reprovou / reprovou |
| M4a / M4b | distância zerada / ignora a outra seed | reprovou / reprovou |
| M5 | desloca a população de delta e resíduo | **sobreviveu** (F1) |
| M6 / M7 / M8 | mapeamento / valores zerados / filtro | **sobreviveram** (F3) |
| M9 | remove guardas de argumento | **sobreviveu** (F4) |
| M10 / M12 / M13 | renomeia chave do JSON / tempo de máquina no JSON / adultera o baseline | reprovou / reprovou / reprovou |
| M11 | tally estático compartilhado | reprovou, só por `Births_and_deaths`; o teste paralelo não pegou |

## Observações (não reprovam)

- Custo do gate (+5%, critério comum da issue): sem antes/depois nos planos. Os 53 testes novos do gate somam 16 s isolados, contra
  2 min 52 s do Unit com worktrees concorrentes. Medição inconclusiva: **pendente** para a integração.
- O baseline roda em `Category=Scenario`: fora do `verify.sh` padrão do CI, dentro de `--full`/`--nightly`. A issue diz "(gate)".
- Doc diz `life_expectancy_at_birth` "0 = indefinida", mas ano sem mortes dá valor artificial (67,5 na seed 1, ano 3).
- `deaths_age` inclui combate: `WorldEventKind.Death` não distingue. Documentado no plano e coerente com "kinds existentes".
