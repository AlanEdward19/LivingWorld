# Fase 12 (Narrativa) Tasks
## Execution Protocol (MANDATORY -- do not skip)
Implement these tasks com a skill `tlc-spec-driven` ativa (fluxo Execute completo). Se a skill falhar, parar.
---
**Design**: `.specs/features/phase-12-narrative/design.md`  
**Status**: Draft
## Test Coverage Matrix
> Generated from codebase + guidelines: `AGENTS.md`, `rules/eval-criteria.md`, `rules/simulation-determinism.md`, `rules/llm-boundary.md`, `scripts/test.sh`, `scripts/verify.sh`.
| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Narrative domain/simulation (`ClaimBuilder`, `Aggregator`, `Validator`, renderer) | unit + integration | 1:1 com NARR-01..12 + edge cases de ancoragem e fallback | `tests/LivingWorld.Tests/Narrative/*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Narrative read queries + endpoints (`chronicles`, `biographies`, `reports`) | integration | happy + not found + filtros local/período + contrato de metadados | `tests/LivingWorld.Tests/Narrative/*EndpointTests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
| Fronteira Verdade vs Crença + mutação | unit + arquitetura | reflexão cobrindo handlers de jogo e par de mutação obrigatório | `tests/LivingWorld.Tests/Narrative/*Security*Tests.cs` | `bash scripts/verify.sh` |
| Scheduling/custo/determinismo narrativo | integration + scenario curto | monthly fora do diário + mesma seed mesma estrutura + LLM off/on muda só prosa | `tests/LivingWorld.Tests/Narrative/*Scenario*Tests.cs` | `bash scripts/test.sh --filter Category!=Scenario` |
## Parallelism Assessment
> Generated from codebase — confirm before Execute.
| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| unit | Yes | objetos/estado instanciados por teste | `FakeLlmProviderTests.cs` |
| integration | Yes | `WebApplicationFactory`/`ScenarioRunner` por teste | `NpcEndpointTests.cs` |
| determinismo 2-processos | No | comparação sequencial entre dois processos externos | `DeterminismTwoProcessTests.cs` |
| arquitetura/reflexão | Yes | leitura de assembly compilado, sem estado mutável | `ArchitectureTests.cs` |

## Gate Check Commands
| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | task com unit/integration local | `bash scripts/test.sh --filter Category!=Scenario` |
| Full | task de segurança/fronteira/API | `bash scripts/verify.sh` |
| Build | fechamento de fase | `bash scripts/build.sh && bash scripts/lint.sh && bash scripts/test.sh --filter Category!=Scenario` |

## Execution Plan
### Phase 1 (Sequential)
`T1 -> T2 -> T3`
### Phase 2 (Parallel + merge)
`T3 -> { T4 [P], T5 [P] } -> T6`
### Phase 3 (Sequential)
`T6 -> T7 -> T8`
### Phase 4 (Sequential)
`T8 -> T9 -> T10`

## Task Breakdown
### T1: Criar contratos narrativos estruturados
**What**: introduzir `NarrativeClaim`, `NarrativeDraft`, `NarrativeDocument` e IDs, sem texto livre fora de claim. **Where**: `src/LivingWorld.Domain/Narrative/*.cs`, `src/LivingWorld.Simulation/Narrative/*.cs`. **Depends on**: None. **Reuses**: padrão `record` + IDs tipados. **Requirement**: NARR-01..04. **Tests**: unit. **Gate**: Quick.
**Status**: ✅ Complete (commit b990d3b) — `NarrativeId` + `NarrativeClaim`/`NarrativeDraft`/`NarrativeDocument` em `src/LivingWorld.Domain/Narrative/`; NARR-02..04 (validação de ancoragem) seguem para T3 (`ClaimAnchorValidator`). Nenhum arquivo criado em `src/LivingWorld.Simulation/Narrative/` — não havia lógica de simulação a introduzir neste task.

### T2: Implementar `WindowedHistoryAggregator`
**What**: agregar eventos por local/período e ordenar por significância (K-top) antes de renderizar. **Where**: `src/LivingWorld.Simulation/Narrative/WindowedHistoryAggregator.cs`. **Depends on**: T1. **Reuses**: consultas/índices de história da fase 10. **Requirement**: NARR-05..07. **Tests**: integration. **Gate**: Quick.
**Status**: ✅ Complete — `TopFacts(world, location, periodStartTick, periodEndTick, topK)` reusa `HistoryIndex.ByYear` (evita full scan de `WorldState.Facts`), filtra por tick/local e ordena por `Fact.Significance` decrescente (desempate por `FactId`).

### T3: Implementar `ClaimAnchorValidator`
**What**: validar `eventIds` não vazios e bloquear nome/número órfão no texto final. **Where**: `src/LivingWorld.Simulation/Narrative/ClaimAnchorValidator.cs`. **Depends on**: T2. **Requirement**: NARR-01..04. **Tests**: unit + integration. **Gate**: Full.

### T4 [P]: Implementar renderer determinístico + fallback
**What**: renderer por template como padrão e render LLM opcional sem alterar estrutura de claims. **Where**: `src/LivingWorld.Simulation/Narrative/NarrativeRenderer.cs`. **Depends on**: T3. **Reuses**: `ILlmProvider`, `NullLlmProvider`. **Requirement**: NARR-08, NARR-12. **Tests**: unit + integration. **Gate**: Quick.

### T5 [P]: Implementar `NpcBiographyQuery`
**What**: gerar linha do tempo por participação do NPC, em ordem cronológica, sem eventos após morte. **Where**: `src/LivingWorld.Simulation/Narrative/NpcBiographyQuery.cs`. **Depends on**: T3. **Reuses**: dados de história + morte de NPC. **Requirement**: NARR-16..18. **Tests**: integration. **Gate**: Quick.

### T6: Implementar job periódico de crônicas
**What**: job batch idempotente por chave `(local,periodStart,periodEnd)` em frequência `Monthly` (nunca diário). **Where**: `src/LivingWorld.Simulation/Narrative/ChronicleGenerationSystem.cs`. **Depends on**: T4, T5. **Reuses**: `EventScheduler`/registro de sistemas. **Requirement**: NARR-05..08 + edge case de concorrência. **Tests**: integration + scenario curto. **Gate**: Full.

### T7: Expor endpoints narrativos de leitura
**What**: `GET /narratives/chronicles`, `GET /narratives/biographies/{npcId}`, `GET /narratives/reports` com metadados de ancoragem. **Where**: `src/LivingWorld.Api/Program.cs` (+ handlers). **Depends on**: T6. **Reuses**: padrão endpoint de `NpcInspectionQuery`. **Requirement**: NARR-19..21. **Tests**: integration. **Gate**: Full.

### T8: Integrar crença por confiança na assimilação de relatos
**What**: aplicar limiar de confiança para entrada em memória semântica e manter separação crença/verdade. **Where**: `src/LivingWorld.Simulation/Narrative/BeliefAssimilationService.cs`. **Depends on**: T7. **Reuses**: consultas de crença da fase 10 + memória semântica. **Requirement**: NARR-13..15. **Tests**: integration. **Gate**: Full.

### T9: Blindar segurança estrutural (Truth vs Belief) + mutação
**What**: testes por reflexão para impedir handler de jogo acessar verdade canônica e par de mutação do validador de ancoragem. **Where**: `tests/LivingWorld.Tests/Narrative/*Security*Tests.cs`. **Depends on**: T8. **Requirement**: NARR-13..15. **Tests**: unit + arquitetura. **Gate**: Full.

### T10: Fechamento de determinismo e custo
**What**: provar llm-on/off com mesmos `eventIds`/cadeia de distorção, leitura não altera hash, transmissão altera hash, e sistema narrativo fora do tick diário. **Where**: `tests/LivingWorld.Tests/Narrative/*Scenario*Tests.cs`. **Depends on**: T9. **Requirement**: NARR-05..12 + sucesso da fase. **Tests**: integration + scenario curto + 2-processos quando aplicável. **Gate**: Full.

## Diagram-Definition Cross-Check
| Task | Depends On (body) | Diagram | Status |
| --- | --- | --- | --- |
| T1 | None | root | ✅ |
| T2 | T1 | T1->T2 | ✅ |
| T3 | T2 | T2->T3 | ✅ |
| T4 | T3 | T3->{T4,T5} | ✅ |
| T5 | T3 | T3->{T4,T5} | ✅ |
| T6 | T4,T5 | {T4,T5}->T6 | ✅ |
| T7 | T6 | T6->T7 | ✅ |
| T8 | T7 | T7->T8 | ✅ |
| T9 | T8 | T8->T9 | ✅ |
| T10 | T9 | T9->T10 | ✅ |

## Test Co-location Validation
| Task | Code Layer | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Domain/Simulation contract | unit | unit | ✅ |
| T2 | Aggregator | integration | integration | ✅ |
| T3 | Anchor validator | unit+integration | unit+integration | ✅ |
| T4 | Renderer | unit+integration | unit+integration | ✅ |
| T5 | Biography query | integration | integration | ✅ |
| T6 | Scheduled generation | integration+scenario | integration+scenario | ✅ |
| T7 | API endpoints | integration | integration | ✅ |
| T8 | Belief assimilation | integration | integration | ✅ |
| T9 | Security boundary | unit+arquitetura | unit+arquitetura | ✅ |
| T10 | Determinism/cost gates | integration+scenario | integration+scenario | ✅ |
