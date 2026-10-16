# Closeout - Fase 9 Escala (specs-closeout 2026-10-12)

Estado: **Parcial**. Fase fechada no ROADMAP/AD-070; spec sem tabela de rastreio, logo esta
tabela. Evidencia = teste/arquivo achado; AD = `docs/decisions-log.md`.

| Requisito | Estado | Evidencia |
|---|---|---|
| PERF-01 cenario de escala estavel | Verified | `tests/LivingWorld.Tests.Shared/Performance/ScaleScenarioFixture.cs`; `ScaleScenarioFixtureTests` |
| PERF-02 sensor no gate (1 mes, 2 populacoes) | Verified | `LongRunning/Performance/ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline` |
| PERF-03 tetos como dado de cenario | Verified | `src/LivingWorld.Domain/Performance/PerfRules.cs` + `PerfRulesTests` |
| PERF-04 sem closure por NPC-tick | Pending - AD-096 achou fecho de lambda alocado; `AllocProfileTests`/`ForesightHotPathAllocTests` medem, sem prova de zero |
| PERF-05 NearestMarket sem LINQ | Verified | `MarketIndex` memoiza por origem; `Unit/Performance/MarketAndRegionIndexTests.The_market_index_names_the_same_nearest_market_as_a_scan_and_follows_a_new_market` (2026-10-13) |
| PERF-06 populacao por regiao/emprego sem re-materializar | Verified | `MarketAndRegionIndexTests` (`RegionPopulationIndex` e `VacancyIndex` iguais a varredura); `EmploymentSystem` itera `AliveNpcIndex`; `ProductionSystem.ApplySpoilage` com buffer reaproveitado em vez de `ToList()` por workplace (sai cedo sem decaimento declarado); hashes inalterados |
| PERF-07 indice de vivos nos Hourly | Parcial - `NeedsDecaySystem` e a ocupacao do `BehaviorDecisionSystem` iteram `AliveNpcIndex` (2026-10-13, hashes inalterados); `BuildingOccupancySystem` ainda varre `world.Npcs` porque e ele que tira o morto do interior, e isso precisa sair para o momento da morte antes de trocar o laco |
| PERF-08 decisao por evento agendado | Verified | `src/LivingWorld.Simulation/Behavior/Needs/NpcWakeScheduler.cs`; AD-070 |
| PERF-09 decaimento preguicoso | Verified | `LongRunning/Performance/LazyNeedTests`; `Domain/Population/LazyNeed.cs`; AD-069 |
| PERF-10 arquivo frio de mortos | Verified | `Unit/Population/Archive/ColdTierArchiveTests`; `Integration/Infrastructure/ColdTierPersistenceTests`; AD-070 |
| PERF-10 compactacao do event log | Verified | `LongRunning/Performance/EventLogSensorTests` (AD-097) |
| PERF-11 snapshot binario + delta | Verified | `Unit/Snapshot/BinaryDiffRoundTripTests`, `BinarySnapshotWriterDiffTests` (AD-093, LWSNAP2) |
| PERF-12 hash incremental por entidade suja | Pending - `Snapshot/IncrementalHasher.cs` iguala o hash do zero (`IncrementalHasherTests`) mas sem marca de sujo; STATE.md e AD-070 registram divida |
| PERF-13 RNG sob demanda | Verified | `Domain/Shared/WorldRngRegistry.StreamFor`; `OptimizationEquivalenceTests.StreamFor_matches_the_named_stream...` |
| PERF-14 EventScheduler sem sort/varredura | Verified | `Unit/Scheduling/EventSchedulerTests.Cancel_and_insertion_stay_ordered_and_correct_with_fifty_thousand_events...` (2026-10-13; teto de tempo largo, nao ruido) |
| PERF-15 paralelismo provado | Verified | `LongRunning/Performance/ParallelDecayTests.Parallel_reschedule_matches_sequential_hash` |
| PERF-16 sensor antes/depois de cada task | Pending - processo, sem artefato verificavel |
| PERF-17 reverter task sem ganho / ECS so se preciso | Pending - processo, sem artefato verificavel |
| Verif. 3 round-trip binario byte-identico | Verified | `Unit/Snapshot/BinaryDiffRoundTripTests` |
| Verif. 5/6 100 anos e 10k x 10 anos | Deferred - `LongRunScaleTests`/Category=Scenario so nightly, fora do gate (AD-070) |
| Meta de alvo (10k vivos <= 500 us/tick, <= 1 GB) | Pending - AD-096 e tick-cost-scaling seguem medindo; sem prova de fechamento |

Resumo (2026-10-13): PERF-05, 06 e 14 Verified; PERF-07 Parcial (falta `BuildingOccupancySystem`).
sem teste ou arquivo citado.
