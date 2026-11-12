# Suíte de testes do LivingWorld: para onde vai o tempo e como chegar a ~5 min

Legenda: **[V]** verificado no código/log · **[M]** medido (dono, log ou `.specs/features/parallel-scenario-seeds/plan.md`, motor mais antigo) · **[E]** estimativa por leitura.
Unidade de custo: 1 vila-ano (vy) = 100 NPCs × 8.640 ticks ≈ 0,55–0,6 s de CPU numa thread (30 anos ≈ 17 s; 100 anos/150 NPCs ≈ 55 s). [E]
Máquina: 8 núcleos (6P + 2E, `sysctl`). [V]

## 0. Conflito com uma decisão anterior do dono (ler antes)
`.specs/features/phase-16-perf-test-suite/design.md` e `spec.md` [V] registram, citando o dono: **nenhum cache nem snapshot de estado de mundo
"em nenhuma forma"**, e **não reduzir horizonte nem seeds**. As propostas P5 (compartilhar corrida) e P8 (mover o século para o nightly)
esbarram nisso. Precisam de um "sim" do dono e de um ADR (`AGENTS.md`: decisão de arquitetura → ADR). P5 não grava estado em disco nem
guarda GB: roda o mundo uma vez e entrega resultados escalares ou o mundo final, só leitura, a vários asserts. Ainda assim, deixa de recomputar.

## 1. Para onde vai o tempo hoje

### Estrutura [V]
- Não há `xunit.runner.json`, `.runsettings` nem `CollectionBehavior`. Vale o padrão do xUnit 2.9: **cada classe é uma coleção**, até 8
  coleções em paralelo, **testes da mesma classe e linhas de `[Theory]` em série**.
- `DisableParallelization = true`: `PopulationBaselineCollection` (Unit) e `ScalePerformanceCollection` (11 classes do LongRunning). Elas rodam
  **sozinhas e em série no fim**, depois de todas as coleções paralelas.
- Paralelismo aninhado sem coordenação: `ParallelSeeds.Degree = min(cores, 8)`, `Parallel.For` sem limite (`FamilyPairedScenarioTests`
  `Final_population…` e `CivilizationReportCommand.BuildJson` com `-1`) e grau 4 (`FamilyDiversityCalibrationTests.Measure`), tudo dentro
  de 8 classes paralelas. Até ~64 threads de CPU em 8 núcleos, com o GC de estação de trabalho (padrão). Nenhum csproj liga o Server GC.
- `verify.sh --full`: check-docs → build → lint (`dotnet format --verify-no-changes`, em série) → **4 `dotnet test` em série, sem
  `--no-build`** (cada um refaz a checagem incremental do MSBuild; o log mostra os 4 builds). O assembly Unit é carregado e descoberto 2 vezes
  (rápido e `--scenarios`).
- O LongRunning só exclui `Heavy`. **`Category=Measure` roda no gate**: `MeasureHarness.Measure` (não asserta nada, grava em `/tmp`),
  `WorldCreationCostTests` e `RuntimeHistoryCostTests`, que asserta razão de CPU ≤ 1,03 (assert de tempo dentro do gate).

### Por suíte
| Suíte | Testes | Tempo | Fonte |
|---|---|---|---|
| build + lint + check-docs | — | ~1–2 min (o build no-op levou 1,2 s; o lint não foi medido) | [M]/[E] |
| Unit rápido | 3.890 | ~1 min | [M] log |
| Unit `Scenario` | 26 | 5 min 9 s | [M] log |
| Integration | 451 | 42 s | [M] log |
| LongRunning sem Heavy | ~149 | "vários minutos"; no gate atual já passou de ~10 min (indício pela hora de gravação do log, não medido) | [M]/[E] |
CPU total estimada do `--full`: **~3.000–3.500 s de CPU**. A 7 núcleos efetivos, um empacotamento perfeito daria ~7–8 min. [E]

### Os 20 que mais custam (estimados por leitura, salvo [M])
| # | Teste/classe | Trabalho | Por que custa |
|---|---|---|---|
| 1 | LR `FamilyDiversityCalibrationTests.Vitality_does_not_collapse_at_century_checkpoints` | 20 seeds × 100 a = ~2.000 vy (~1.100 s CPU) | grau 4 → ≥4,5 min de parede; é o maior item do gate |
| 2 | LR `FamilyDiversity…` `Current` (Lazy, usado por 3 testes + FamilyPaired) | 20 × 10 a × 2 braços = 400 vy | grau 4; o braço real é o prefixo do #1 |
| 3 | LR `Strong_selection_is_rejected_by_the_same_band` | 400 vy | grau 4 |
| 4 | Unit `TechDiscoveryTests` (3 Scenario na **mesma classe**, em série) | 400 + 180 + 40 vy | [M] isolados: 124–169 s, 72–76 s, 30–43 s → provável caminho crítico dos 5 min |
| 5 | Unit `OntogenyDebtsTests.Orphan_care…` | 20 × 2 × 6 a = 240 vy | [M] 82–90 s |
| 6 | Unit `VillageSurvivesGenerationsTests` | 2 × 60 a, Theory **em série** | ~70 s de cadeia serial |
| 7 | Unit `CivilizationDiversityBaselineTests` | 20 × 10 a, mundo vivo (`default.json`) | `Parallel.For` sem limite |
| 8 | Unit `PopulationBaselineTests` | 20 × 10 a = 200 vy | coleção não paralela, cauda serial |
| 9 | Unit `DiseaseTests` (8 seeds × 2 braços × 5 a) | 80 vy | [M] ~50 s |
| 10 | Unit `ClimateTests` drought | 2 × 20 a, em série | ~25 s |
| 11 | LR `FamilyPairedScenarioTests` (classe serial) | seed 42 × 10 a **4 vezes**, 10 seeds × 10 a, ~5.000 carreiras de 60 a | repetição pura |
| 12 | LR `MeasureHarness` (coleção serial) | 3 × 1.000 NPCs × 30 d + 5 × 10 a + 3 × 10 a vivo | só medição |
| 13 | LR `LifeTable100YearScenarioTests` | até 100 a (para cedo se passar) | — |
| 14 | LR `LaborAllocationVillageTests` | 5 × 10 a + 5 × 3 a, em série | prefixos de #2 |
| 15 | LR `EconomyDepthConservationTests` | 3 × 10 a vivo + 8 × 5 a vivo, em série | — |
| 16 | LR `DeterminismTwoProcessTests` + `PersistenceCrossProcess` | ~28 processos `dotnet`, até 5 a cada | arranque de processo |
| 17 | LR seed 42 × 10 a repetido | `PopulationScenario`, `ReferentialIntegrity`, `BytesPerNpc`, `Employment` (1 a e 10 a) | o mesmo mundo, várias vezes |
| 18 | LR `MoneyConservation` 1 a / 10 a / 100 a e `ResourceConservation` 1 a / 10 a (seed 42, pop 20) | prefixos uns dos outros | barato (pop 20) |
| 19 | LR resto da `ScalePerformanceCollection` (ScaleSensor, ObservationalLod, EventLog 1.000 NPCs, ForesightHotPath) | cauda serial | — |
| 20 | Unit `PositionalSnapshot` (20 × 10 a, pop 12) e `IncrementalHasher` (hash a cada 100 ticks) | — | [M] ~27 s |
Mais barato do que parece: `BehaviorDecisionSystemHysteresisTests` (465 ms, log), `GoldenHashes` (≤ 17.280 ticks), `CanonBounded` (sem relógio). [V]

**Como medir** (rodar quando o gate atual terminar; nada disso foi rodado agora):
`dotnet test tests/LivingWorld.Tests.<P> -c Release --no-build --filter "<filtro do test.sh>" --logger "trx;LogFileName=<p>.trx" --results-directory <dir>`.
Depois, ordene `UnitTestResult@duration` do .trx (o tempo por teste não inclui a espera da coleção). Para ver o paralelismo efetivo,
use `/usr/bin/time -l` em volta (user+sys ÷ real). Para a linha do tempo por classe:
`--logger "console;verbosity=detailed" -- xUnit.DiagnosticMessages=true`.

## 2. Mudanças priorizadas
| P | Mudança | Ganho [E] | Risco p/ qualidade | Arquivos |
|---|---|---|---|---|
| P1 | Casca: 1 build; `dotnet test --no-build`; Unit uma vez (sem filtro) no `--full`; lint em paralelo com os testes; LongRunning disparado primeiro e Integration em paralelo (logs por suíte, saída = OR) | 1–3 min | nulo (mesmos testes) | `scripts/test.sh`, `scripts/verify.sh`, `scripts/README.md` |
| P2 | Quebrar cadeias seriais: os 3 Scenario de TechDiscovery em classes próprias; VillageSurvives como `ParallelSeeds.Map([7,42])` ou 2 classes; braços de Climate/Famine em paralelo; testes caros de FamilyPaired em classes próprias | Scenario 5 → ~2,5 min | nulo: cada mundo é isolado (`plan.md` já provou que não há estado estático compartilhado) | só `tests/` |
| P3 | Grau coerente: `ParallelSeeds.Degree` em todo laço de seeds (fim do grau 4 e do sem limite); experimentar `DOTNET_gcServer=1` / `<ServerGarbageCollection>` nos csproj de teste e `maxParallelThreads` | 10–30% (medir) | nulo para o resultado: o GC não muda o hash; caches por `ConditionalWeakTable` só perdem acerto | csproj de teste, `xunit.runner.json` novo, `test.sh` |
| P4 | Século: grau 8 e início no t=0 (classe própria, primeira no LongRunning); **fundir `Current` (braço real) com o século**: mesmo seed e mesmas regras, então o ano 10 do século é o `Current` real; registra as métricas no checkpoint do ano 10 | ~2,5 min de parede; −200 vy | baixo: provar uma vez que as métricas do ano 10 são iguais às do `Current` antigo | `FamilyDiversityCalibrationTests.cs` |
| P5 | Rodar uma vez e assertar várias (em memória, sem disco; **precisa do sim do dono**, ver §0): seed 42 × 10 a × 4 em FamilyPaired → 1 `Lazy` com o sink; seeds 1–20 × 10 a padrão (PopulationBaseline, controle de Disease, FamilyPaired final, LaborAllocation) → 1 fixture com checkpoints anuais; TechDiscovery seeds 1–10 Pressured: 2 a é prefixo de 20 a, e 1–3 seguem até 30 a | −500–800 s de CPU | médio: acopla testes; fixture com teste próprio "rodada compartilhada == rodada isolada (hash)"; a cópia "again" do TechDiscovery **fica separada** (é o teste de determinismo) | `tests/**`, `Tests.Shared/` |
| P6 | Separar medição de gate: `--full` com `Category!=Measure`; Measure no `--nightly` e nos `compare-*-cost.sh` (rodando isolado, onde tempo vale) | ~1–1,5 min de cauda serial | baixo: a regressão de custo continua no nightly e nos scripts de comparação | `test.sh`, `verify.sh` |
| P7 | Prefixos redundantes **com a mesma checagem a cada amostra**: Money 1 a e 10 a (o de 100 a cobre), Resource 1 a, PopulationScenario 1 a, Employment 1 a, scifi 1 a, LOD 10 a (o de 100 a, seed 901, checa todo tick). **Não** vale para `ReferentialIntegrity` (checa só o estado final) | pequeno (~30–60 s de CPU) | baixo, se cada removido for prefixo estrito; muda a contagem de testes, precisa de aprovação | `tests/LivingWorld.Tests.LongRunning/**` |
| P8 | Parada sequencial onde o critério é contagem: `Two_isolated… ≥2 de 3`, `Isolated_cultures ≥5 de 10`, Famine `10/10` (para na 1ª falha) | pequeno; conflita com P2 (ondas) | baixo: parada determinística | TechDiscovery, Famine |

**Não cortar** (é o que pega regressão de verdade): goldens (`GoldenHashesTests`, baratos); `DeterminismTwoProcessTests`,
`PersistenceCrossProcessTests` e o digest de dois processos (únicos que pegam estado de processo); conservação de moeda e de recursos
(inclusive 100 a, que é barato com pop 20); os baselines de 20 seeds (`population`, `history-collapse`, `action-switches`,
`civilization-diversity`, `family-diversity`): comparam valor por seed, então não admitem parada antecipada nem menos seeds; a repetição
em processo do TechDiscovery; sensores de hash ligado/desligado; o piso do século (critério da fase 7, "100 anos").

## 3. Meta honesta
- Só P1–P4 + P6 (sem mudar o que se computa): `--full` ≈ **6–8 min** [E]. O piso é a CPU: o século sozinho é ~1/3 da CPU do gate.
- \+ P5 (exige a aprovação do §0): ≈ **5–6 min** [E].
- **≤ 5 min com confiança** exige tirar o século (`Vitality_does_not_collapse_at_century_checkpoints`, ~1.100 s de CPU) do `--full` para o
  `--nightly`. O CI já roda o nightly (`ci.yml`, cron 05:17). O `--full` fica com o braço de 10 anos pareado (`Real_vitality…`, `Strong…`).
- `--nightly` = `--full` + século + `Heavy` + `Measure` (+ regravação de baselines sob flag). Motivo: são os itens caros, de medição ou de
  tempo, que não mudam a cada PR de regra local. O `--full` continua pegando hash, conservação, baselines de 20 seeds e invariantes por tick.

## 4. Plano em fatias (cada uma num worktree próprio, só `tests/` e `scripts/`)
Aceite comum: (a) 3 rodadas alternadas antes/depois, mediana de parede e de CPU (`/usr/bin/time -l`), com o mesmo comando;
(b) mesmo `Total` no .trx por projeto (exceto remoções listadas e aprovadas); (c) injeção de defeito em cópia temporária, padrão
`scripts/verify-mutation.sh`: os mesmos testes reprovam antes e depois. Defeitos sugeridos: editar um valor de
`tests/baselines/family-diversity.json` (→ `Real_vitality…`), `tests/golden/world-hashes.json` (→ golden), `action-switches.json`
(→ histerese), desligar `orphanCare` no default (→ Orphan_care) e cunhar moeda sem contador (→ conservação).
| Fatia | Escopo | Aceite específico |
|---|---|---|
| S1 casca | P1 em `test.sh`/`verify.sh`: `--no-build`, Unit uma vez, lint e suítes em paralelo com LongRunning primeiro, saída ≠0 se qualquer uma falhar | `--full` mais rápido; log separado por suíte; um teste reprovado em qualquer suíte derruba o gate |
| S2 cadeias seriais | P2 (só mover testes para classes ou envolver em `ParallelSeeds`) | mesmos nomes de teste (FQN pode mudar de classe: listar o mapa); Scenario Unit ≤ 3 min |
| S3 grau e GC | P3; medir 3 configs (atual, Server GC, `maxParallelThreads` 4/8) e manter a melhor | hashes e baselines inalterados; ganho ≥ 10%, senão reverte |
| S4 século | P4 | o ano 10 do século reproduz o `Current` antigo (comparar os números gravados antes); LongRunning cai ≥ 2 min |
| S5 medição fora | P6 + `README`; `--nightly` inclui Measure | `--full` sem Measure; `--nightly` roda o MeasureHarness e grava em `/tmp` |
| S6 rodada compartilhada (após o ADR) | P5, um fixture por chave, com teste de equivalência por hash | o fixture reprova se a corrida compartilhada ≠ isolada; mesmos testes falham sob os defeitos acima |
| S7 prefixos (após aprovação) | P7 | cada removido mapeado para o teste que o cobre (mesma seed, regras e cadência) |
Ordem: S1, S2, S3, S4 e S5 são independentes e sem decisão pendente. S6 e S7 esperam o dono. Mover o século para o nightly é uma decisão
do dono, não uma fatia.
