# Dead Code Audit - Closeout (specs-closeout 2026-10-12)

Tabela de rastreio em `spec.md` (linhas 208-241) usa "Implemented - gate pendente"; abaixo o estado
conferido contra o codigo atual. Nenhum teste/gate foi executado nesta passada (somente leitura).

| Requisito | Estado | Evidencia |
| --- | --- | --- |
| AUDIT-01..12 (inventario, versoes vN, higiene, duplicacao) | Verified | `report.md` (resumo, P1, duplicacao, comentarios) - entregaveis de auditoria, nao de codigo |
| EXEC-01 (migrar PopulationArchitectureTests p/ V2 antes de remover v1) | Verified | `grep ScenarioLoader\b` em src/tests so acha outros loaders; `Scenarios/` so tem `ScenarioLoaderV2.cs` |
| EXEC-02 (remover DecisionMetrics) | Deferred | Dropped por decisao: tem consumidor de teste real (`LivingVillageScenarioTests`, report.md) |
| EXEC-03 (build sem ref pendente a ScenarioLoader v1) | Verified | grep em src/ e tests/ sem referencia a `ScenarioLoader` nao-V2; build nao rodado agora |
| EXEC-04 (gate rapido verde pos-remocao) | Pending - gate nao executado nesta passada (usuario) |
| EXEC-05 (save persiste ColdArchive) | Verified | `src/LivingWorld.Infrastructure/PersistentWorldRunner.cs` (param `coldTier`, salva na fronteira) |
| EXEC-06 (resume carrega cold tier) | Verified | `ColdTierPersistence` plugado em `PersistentWorldRunner.cs`, `Api/Program.cs`, `Workers/Program.cs` |
| EXEC-07 (round-trip no caminho de producao) | Verified | `tests/LivingWorld.Tests.Integration/EventLog/PersistentWorldRunnerTests.cs`, `Infrastructure/ColdTierPersistenceTests.cs` |
| EXEC-08 (sensor de escala dentro do teto) | Pending - sensor nao rodado; sem evidencia de decisao em STATE.md |
| EXEC-09 (reuso de Magic/FormatVersion) | Pending - formato em `ColdTierPersistence.cs` nao reconferido linha a linha |
| EXEC-10 (helper compartilhado repos Sqlite) | Verified | `src/LivingWorld.Infrastructure/Repositories/VersionedQuery.cs` |
| EXEC-11 (Global/CityLayerBuilder compartilhados) | Pending - existe `Api/Visual/Layers/LayerBuildResult.cs`, mas helper do switch nao confirmado |
| EXEC-12 (threshold de fundacao compartilhado) | Verified | `Simulation/Cities/Founding/FoundingConcentration.cs` usado por `SettlementFoundingSystem` e `SpatialSettlementFoundingSystem` |
| EXEC-13 (`Resolve<T>` generico) | Verified | `Domain/Shared/ScenarioRuleDefaults.cs`; usado em AttentionRules, PowerUtilityRules, PowerInheritanceRules, BodyRules |
| EXEC-14 (hash golden inalterado) | Pending - golden/scale-sensor nao rodados nesta passada |
| EXEC-15 (citacao-pura reescrita) | Verified | grep `Fase N`/`Phase N` em src/*.cs (sem obj/bin/Migrations) = 0; `WorldEventKind.cs` = 0 |
| EXEC-16 (WHY preservado) | Pending - qualidade do texto restante nao revisada |
| EXEC-17 (build limpo por projeto) | Pending - build nao rodado |
| EXEC-18 (re-varredura sem sobra) | Verified | mesmo grep: 0 ocorrencias de `Fase N` em src/ (padrao `task N`/`ADR-NNNN` nao varrido) |

Resumo EXEC (18): 10 Verified, 1 Deferred, 7 Pending (todos dependem de gate/build nao executado).
Itens fora de escopo declarados no spec (Worker, ExampleCounterSystem, DecisionSandbox) seguem
como decisao futura do usuario; `ExampleCounterSystem.cs` ainda existe em `Simulation/Scheduling`.
