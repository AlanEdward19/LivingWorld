# Dead Code Audit - Closeout (specs-closeout 2026-10-12)

Tabela de rastreio em `spec.md` (linhas 208-241) usa "Implemented - gate pendente"; abaixo o estado
conferido contra o codigo atual. Nenhum teste/gate foi executado nesta passada (somente leitura).

| Requisito | Estado | Evidencia |
| --- | --- | --- |
| AUDIT-01..12 (inventario, versoes vN, higiene, duplicacao) | Verified | `report.md` (resumo, P1, duplicacao, comentarios) - entregaveis de auditoria, nao de codigo |
| EXEC-01 (migrar PopulationArchitectureTests p/ V2 antes de remover v1) | Verified | `grep ScenarioLoader\b` em src/tests so acha outros loaders; `Scenarios/` so tem `ScenarioLoaderV2.cs` |
| EXEC-02 (remover DecisionMetrics) | Deferred | Dropped por decisao: tem consumidor de teste real (`LivingVillageScenarioTests`, report.md) |
| EXEC-03 (build sem ref pendente a ScenarioLoader v1) | Verified | grep em src/ e tests/ sem referencia a `ScenarioLoader` nao-V2; build nao rodado agora |
| EXEC-04 (gate rapido verde pos-remocao) | Verified | `bash scripts/verify.sh --full` sai 0 em 2026-10-13 (2153 unit + 15 cenario + 365 integracao + 126 longrunning) |
| EXEC-05 (save persiste ColdArchive) | Verified | `src/LivingWorld.Infrastructure/PersistentWorldRunner.cs` (param `coldTier`, salva na fronteira) |
| EXEC-06 (resume carrega cold tier) | Verified | `ColdTierPersistence` plugado em `PersistentWorldRunner.cs`, `Api/Program.cs`, `Workers/Program.cs` |
| EXEC-07 (round-trip no caminho de producao) | Verified | `tests/LivingWorld.Tests.Integration/EventLog/PersistentWorldRunnerTests.cs`, `Infrastructure/ColdTierPersistenceTests.cs` |
| EXEC-08 (sensor de escala dentro do teto) | Verified | `ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline` passa 2026-10-13 (alocacao +5% e tempo 10x da baseline) |
| EXEC-09 (reuso de Magic/FormatVersion) | Verified | `ColdTierPersistence.cs:20-21,47,61,92,108` (`LWCLD1`, `FormatVersion`); `PersistentWorldRunner.cs:71,86,101` so chama `Save`/`Load`, sem serializacao propria |
| EXEC-10 (helper compartilhado repos Sqlite) | Verified | `src/LivingWorld.Infrastructure/Repositories/VersionedQuery.cs` |
| EXEC-11 (Global/CityLayerBuilder compartilhados) | Verified | `LayerBuildResult.OutOfScope` (`LayerBuildResult.cs:21`) chamado por `GlobalLayerBuilder.cs:35` e `CityLayerBuilder.cs:18` |
| EXEC-12 (threshold de fundacao compartilhado) | Verified | `Simulation/Cities/Founding/FoundingConcentration.cs` usado por `SettlementFoundingSystem` e `SpatialSettlementFoundingSystem` |
| EXEC-13 (`Resolve<T>` generico) | Verified | `Domain/Shared/ScenarioRuleDefaults.cs`; usado em AttentionRules, PowerUtilityRules, PowerInheritanceRules, BodyRules |
| EXEC-14 (hash golden inalterado) | Pending - os goldens foram regravados em 2026-10-13 (AD-108, log/cosseno deterministicos), entao ja nao comparam com o valor pre-consolidacao; `GoldenHashesTests` e scale-sensor passam com os novos |
| EXEC-15 (citacao-pura reescrita) | Verified | grep `Fase N`/`Phase N` em src/*.cs (sem obj/bin/Migrations) = 0; `WorldEventKind.cs` = 0 |
| EXEC-16 (WHY preservado) | Pending - qualidade do texto restante nao revisada |
| EXEC-17 (build limpo por projeto) | Verified | `bash scripts/build.sh` e `lint.sh` saem 0 com 0 erros (verify.sh --full, 2026-10-13) |
| EXEC-18 (re-varredura sem sobra) | Verified | mesmo grep: 0 ocorrencias de `Fase N` em src/ (padrao `task N`/`ADR-NNNN` nao varrido) |

Resumo EXEC (18, 2026-10-13): 15 Verified, 1 Deferred, 2 Pending (EXEC-14 perdeu a referencia pre-consolidacao; EXEC-16 qualidade de texto, nao medivel).
Itens fora de escopo declarados no spec (Worker, ExampleCounterSystem, DecisionSandbox) seguem
como decisao futura do usuario; `ExampleCounterSystem.cs` ainda existe em `Simulation/Scheduling`.
