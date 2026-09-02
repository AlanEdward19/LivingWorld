# Fase 28 — Locks e ownership (OBRIGATÓRIO)

> **Regra**: worker só edita arquivos listados em `owns`. Qualquer arquivo em `shared`
> é **serial** — um worker por vez, commit antes do próximo.

## Arquivos compartilhados (SERIAL — nunca paralelo)

| Arquivo | Dono sequencial |
|---|---|
| `src/LivingWorld.Simulation/WorldState.cs` | worker-integration |
| `src/LivingWorld.Api/Program.cs` | worker-integration |
| `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` | T6 (após commit T1) |

## Ownership por task

| Task | `owns` (único dono) | Proibido tocar |
|---|---|---|
| T1 | `Domain/Cognition/NpcCognitionLog.cs`, `tests/.../Cognition/NpcCognitionLogTests.cs` | qualquer outro teste |
| T2 | `Simulation/Observation/ObservationRegistry.cs`, `tests/.../Observation/ObservationRegistryTests.cs` | WorldState, Program |
| T3 | `Domain/Population/LazyPosition.cs`, `tests/.../Population/LazyPositionTests.cs` | — |
| T5 | `Simulation/Behavior/CosmeticDetailSystem.cs`, `tests/.../Behavior/CosmeticDetailSystemTests.cs` | WorldState |
| T6 | `BehaviorDecisionSystem.cs` (só wiring), `tests/.../Behavior/BehaviorDecisionSystemCognitionTests.cs` | outros testes Behavior |
| T7 | `Api/WatchlistEndpoints.cs`, `tests/.../Api/WatchlistEndpointsTests.cs` | Program (só 1 linha via integration) |
| T8 | `Api/ObservationScopeEndpoints.cs`, `tests/.../Api/ObservationScopeEndpointsTests.cs` | Program (só 1 linha via integration) |
| T15 | `Domain/Interning/StringInternPool.cs`, `tests/.../Interning/StringInternPoolTests.cs` | — |
| T16 | `Simulation/Snapshot/SnapshotStringInterning.cs`, `WorldSnapshot.cs` | SqliteWorldRepository |
| T17 | `Infrastructure/EventLogKindEncoding.cs`, `SqliteWorldRepository.cs` | WorldSnapshot |
| **integration** | `WorldState.cs`, `Program.cs` (registro de endpoints + side-stores) | tudo acima já commitado |

## Protocolo de execução

1. **Phase 1** (T1,T2,T3,T15): paralelo OK — zero overlap.
2. **Commits atômicos** antes de qualquer task que dependa.
3. **Phase 2+**: máximo 1 worker em arquivos `shared`; demais tasks em paralelo só se `owns` não colide.
4. **Integration worker** roda por último em cada batch: wire WorldState + Program num único commit.
5. Worker que precisa de arquivo fora de `owns` → **PARE**, reporte ao orquestrador.

## Estado atual (2026-08-30)

- Phase 1 **fechada** (4 commits em `feat/phase-28-cognition`)
- Phase 2 **pausada** — colisão detectada; retomar só com este protocolo
