# Closeout - Fase 9 Escala (specs-closeout 2026-10-12)

Estado: **Parcial**. Fase fechada no ROADMAP/AD-070; spec sem tabela de rastreio, logo esta
tabela. Evidencia = teste/arquivo achado; AD = `docs/decisions-log.md`.

| Requisito | Estado | Evidencia |
|---|---|---|
| PERF-01 cenario de escala estavel | Verified | `tests/LivingWorld.Tests.Shared/Performance/ScaleScenarioFixture.cs`; `ScaleScenarioFixtureTests` |
| PERF-02 sensor no gate (1 mes, 2 populacoes) | Verified | `LongRunning/Performance/ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline` |
| PERF-03 tetos como dado de cenario | Verified | `src/LivingWorld.Domain/Performance/PerfRules.cs` + `PerfRulesTests` |
| PERF-04 sem closure por NPC-tick | Pending - AD-096 achou fecho de lambda alocado; `AllocProfileTests`/`ForesightHotPathAllocTests` medem, sem prova de zero |
| PERF-05 NearestMarket sem LINQ | Pending - nao localizei teste; AD-074/075 citam hot paths, sem prova especifica |
| PERF-06 populacao por regiao/emprego sem re-materializar | Pending - sem teste especifico achado |
| PERF-07 indice de vivos nos Hourly | Verified | `Unit/Performance/OptimizationEquivalenceTests.Alive_npc_spatial_index_matches_a_brute_force_scan_after_movement_and_death` (indice espacial; AD-075) |
| PERF-08 decisao por evento agendado | Verified | `src/LivingWorld.Simulation/Behavior/Needs/NpcWakeScheduler.cs`; AD-070 |
| PERF-09 decaimento preguicoso | Verified | `LongRunning/Performance/LazyNeedTests`; `Domain/Population/LazyNeed.cs`; AD-069 |
| PERF-10 arquivo frio de mortos | Verified | `Unit/Population/Archive/ColdTierArchiveTests`; `Integration/Infrastructure/ColdTierPersistenceTests`; AD-070 |
| PERF-10 compactacao do event log | Verified | `LongRunning/Performance/EventLogSensorTests` (AD-097) |
| PERF-11 snapshot binario + delta | Verified | `Unit/Snapshot/BinaryDiffRoundTripTests`, `BinarySnapshotWriterDiffTests` (AD-093, LWSNAP2) |
| PERF-12 hash incremental por entidade suja | Pending - `Snapshot/IncrementalHasher.cs` iguala o hash do zero (`IncrementalHasherTests`) mas sem marca de sujo; STATE.md e AD-070 registram divida |
| PERF-13 RNG sob demanda | Verified | `Domain/Shared/WorldRngRegistry.StreamFor`; `OptimizationEquivalenceTests.StreamFor_matches_the_named_stream...` |
| PERF-14 EventScheduler sem sort/varredura | Pending - codigo usa insercao binaria e bucket por tick (`EventScheduler.cs`), sem teste dedicado achado |
| PERF-15 paralelismo provado | Verified | `LongRunning/Performance/ParallelDecayTests.Parallel_reschedule_matches_sequential_hash` |
| PERF-16 sensor antes/depois de cada task | Pending - processo, sem artefato verificavel |
| PERF-17 reverter task sem ganho / ECS so se preciso | Pending - processo, sem artefato verificavel |
| Verif. 3 round-trip binario byte-identico | Verified | `Unit/Snapshot/BinaryDiffRoundTripTests` |
| Verif. 5/6 100 anos e 10k x 10 anos | Deferred - `LongRunScaleTests`/Category=Scenario so nightly, fora do gate (AD-070) |
| Meta de alvo (10k vivos <= 500 us/tick, <= 1 GB) | Pending - AD-096 e tick-cost-scaling seguem medindo; sem prova de fechamento |

Resumo: 12 Verified, 8 Pending, 1 Deferred (linhas da tabela).
sem teste ou arquivo citado.
