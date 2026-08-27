# Fase 16.3 — Living World Cohesion Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review, Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/phase-16-3-world-cohesion/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Gerado por amostragem do repo (`AGENTS.md` → `rules/tests.md` + `rules/simulation-determinism.md`) — confirmar antes de Execute.

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Domain — `*Rules` novos (`BodyRules`, `PowerUtilityRules`, `CausalRules`, `AttentionRules`) | Unit | Todo branch de `Create`/validator; `Default` cobre limites (mesmo padrão `FamilyRulesTests`) | `tests/LivingWorld.Tests/{Population,Extraordinary,Behavior}/*RulesTests.cs` | `bash scripts/test.sh --filter "FullyQualifiedName~RulesTests"` |
| Domain — `Npc` campos novos (`Height`/`Weight`/`MuscleMass`), `WorldEvent` campos novos | Unit | Round-trip/serialização, clamp de faixa, `[JsonIgnore]` onde aplicável (AD-026) | `tests/LivingWorld.Tests/Population/NpcTests.cs`, `tests/LivingWorld.Tests/*/WorldEventTests.cs` | `bash scripts/test.sh --filter "FullyQualifiedName~Npc\|FullyQualifiedName~WorldEvent"` |
| Simulation — sistemas novos (`DecisionContextBuilder`, `BodyMechanic`, `PowerOpportunityProvider`, `AttentionRouter`, `DecisionContextCache`, `CausalDiagnostics`) | Unit + **Determinism (mandatory por sistema novo, `rules/tests.md:13-18`)** | 1:1 com cada AC do spec da story; determinismo = mesma seed → mesmo resultado, teste dedicado por sistema | `tests/LivingWorld.Tests/Behavior/*Tests.cs`, `tests/LivingWorld.Tests/Extraordinary/*Tests.cs`, `tests/LivingWorld.Tests/History/*Tests.cs` | `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~<Area>"` |
| Simulation — refactor `BehaviorDecisionSystem.SelectByUtility`/`UtilityBaseOf` | Unit + Determinism | Todos os `[Theory]` existentes continuam passando com a assinatura nova (`BehaviorDecisionSystemTests.cs`); casos novos de memória/relação/corpo/power divergindo decisão | `tests/LivingWorld.Tests/Behavior/BehaviorDecisionSystemTests.cs` | `bash scripts/test.sh --filter "FullyQualifiedName~BehaviorDecisionSystemTests"` |
| Architecture — banimento de nondeterminismo (`Random`/`DateTime.Now`/`Guid.NewGuid` em Domain/Simulation) | Architecture (existente, não recriar) | Todo código novo desta fase passa no teste de arquitetura já existente sem exceção nova | `tests/LivingWorld.Tests/**/ArchitectureTests.cs` (nome exato a confirmar em Task 1) | `bash scripts/test.sh --filter "FullyQualifiedName~Architecture"` |
| Infrastructure — `EventLogRecord` migração EF | Integration | Round-trip de evento com `EventId`/`CauseEventId`/`SourceSystem` novos; leitor antigo não quebra com colunas nullable | `tests/LivingWorld.Tests/**/EventLogRecordTests.cs` (novo) | `bash scripts/test.sh --filter "FullyQualifiedName~EventLogRecord"` |
| Scenario — `test-living-village`, golden hashes | Scenario (`[Trait("Category","Scenario")]`, poucos, caros) | Cadeia causal cross-system reproduzível determinística; golden regravado e documentado via AD quando schema de `WorldState` muda | `tests/LivingWorld.Tests/GoldenHashesTests.cs`, novo `tests/LivingWorld.Tests/Scenario/LivingVillageScenarioTests.cs` | `bash scripts/test.sh --filter "Category=Scenario&FullyQualifiedName~LivingVillage"` |
| Entity/Config puro (ex.: `ActionType.UsePower` enum value sozinho) | none | Build gate cobre; qualquer switch que precise do valor novo já está coberto pelas próprias tasks que o tocam | — | build gate only |

## Parallelism Assessment

> Gerado por amostragem do repo — confirmar antes de Execute.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| Unit (Domain `*Rules`, `Npc`, `WorldEvent`) | Yes | Sem estado compartilhado, `WorldState`/objetos novos por teste | Padrão já usado por `FamilyRulesTests`/`HeredityServiceTests` |
| Unit + Determinism (Simulation, novos sistemas) | Yes | `WorldState`/`TickContext` construídos por teste, sem singleton compartilhado | Padrão `CombatMechanicTests`/`BehaviorDecisionSystemTests` |
| Integration (`EventLogRecord`/EF) | No, a menos que siga o padrão de fixture próprio já usado por mutadores de API | Repositório/DB compartilhado entre testes se não isolado | `.specs/STATE.md` "API fixture hygiene" — mutadores usam `IClassFixture` próprio, nunca `DisableParallelization` de assembly |
| Scenario (`test-living-village`, golden) | No (já roda fora do gate rápido, `Category=Scenario`) | Execução longa, comparação determinística ponta-a-ponta | `scripts/test.sh` default `--filter Category!=Scenario` |

## Gate Check Commands

> Gerado por amostragem do repo — confirmar antes de Execute.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | Depois de cada task com testes unit/determinism | `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~<Area>"` |
| Full | Depois de cada fase (Phase 1-7) e antes de qualquer commit que mude schema canônico | `bash scripts/test.sh` (default `Category!=Scenario`, suíte inteira) |
| Scenario | Depois da Phase 7 (P3) e sempre que `test-living-village`/golden mudar | `bash scripts/test.sh --filter "Category=Scenario"` |
| Build | Fechamento da fase (gate final, doc#162) | `bash scripts/verify.sh` |

---

## Execution Plan

### Phase 1: Causal Event Provenance — P1a (Sequential)

```
T1 → T2 → T3 → T4 → T5
```

### Phase 2: Body/Health Minimal Causal System — P1c (Sequential com 1 ramo paralelo)

```
T6 → T7 → T8 ┬→ T9  [P]
             └→ T10 [P]
T9, T10 → T11
```

### Phase 3: Decision Context Integration — P1b (Sequential, depende de Phase 2 pro campo Body)

```
T12 → T13 → T14 → T15 → T16 → T17
```

### Phase 4: Powers Full Utility Integration — P1d (depende de Phase 3)

```
T18 → T19 → T20 → T21 ┬→ T22 [P]
                       └→ T23 [P]
T22, T23 → T24
```

### Phase 5: Intent Persistence & Attention Router — P2a (depende de Phase 1 + Phase 3)

```
T25 → T26 → T27 ┬→ T28 [P]
                 └→ T29 [P]
T28, T29 → T30
```

### Phase 6: Pressure / Opportunity Formalization — P2b (depende de Phase 3)

```
T31 → T32 → T33
```

### Phase 7: Diagnostics, Metrics & Vertical Validation Scenario — P3 (depende de todas as fases anteriores)

```
T34 → T35 → T36 → T37 → T38 → T39
```

---

## Task Breakdown

### T1: Estender `WorldEvent` com `EventId`/`CauseEventId`/`SourceSystem` + contador `_nextHistoryEventId`

**What**: `WorldEvent` ganha `long EventId`, `long? CauseEventId`, `string SourceSystem`; `WorldState` ganha `_nextHistoryEventId`/`NextHistoryEventIdAndAdvance()` (contador irmão de `_nextEventId`, nunca reaproveita — AD-013).
**Where**: `src/LivingWorld.Simulation/WorldEvent.cs`, `src/LivingWorld.Simulation/WorldState.cs`
**Depends on**: None
**Reuses**: padrão `_nextEventId`/`NextEventIdAndAdvance` (`WorldState.cs:17,21,579`)
**Requirement**: COH-01

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `WorldEvent` record tem os 3 campos novos, `EventId` nunca omitido
- [ ] `WorldState` expõe `NextHistoryEventId`/`NextHistoryEventIdAndAdvance()` seguindo o padrão exato de `_nextEventId`
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~WorldEvent|FullyQualifiedName~WorldState"`
- [ ] Nenhum teste existente quebra (baseline de contagem confirmado antes/depois)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(history): add causal EventId/CauseEventId/SourceSystem to WorldEvent`

---

### T2: `TickContext.LogEvent` overload aditivo + wrapper de compatibilidade

**What**: Novo overload `LogEvent(kind, payload, sourceSystem, causeEventId = null)`; assinatura antiga vira wrapper (`sourceSystem: "Unknown"`, `causeEventId: null`) — nenhum dos ~57 call sites existentes quebra.
**Where**: `src/LivingWorld.Simulation/TickContext.cs`
**Depends on**: T1
**Reuses**: `TickContext.LogEvent` existente (`TickContext.cs:13`)
**Requirement**: COH-01, COH-03

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Overload novo mint `EventId` via `WorldState.NextHistoryEventIdAndAdvance()`
- [ ] Wrapper antigo compila e roda idêntico (todos os ~57 call sites inalterados)
- [ ] Gate check passa: `bash scripts/test.sh` (suíte inteira — garante que nenhum call site quebrou)
- [ ] Test count: baseline mantido + testes novos de T1/T2 passam

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(history): add additive LogEvent overload carrying causal provenance`

---

### T3: `EventLogRecord` — colunas novas + migração EF + `SqliteWorldRepository`

**What**: `EventLogRecord` ganha `EventId`/`CauseEventId`/`SourceSystem` nullable; migração EF aditiva; `SqliteWorldRepository` grava os campos novos ao lado de `Sequence` (padrão AD-029, sem autoincrement de DB).
**Where**: `src/LivingWorld.Infrastructure/EventLogRecord.cs`, migração EF nova, `src/LivingWorld.Infrastructure/SqliteWorldRepository.cs`
**Depends on**: T2
**Reuses**: padrão de sequence assignment (`SqliteWorldRepository.cs:50-64`)
**Requirement**: COH-04

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Migração EF gerada sem `Sqlite:Autoincrement` (ADR-0002)
- [ ] Round-trip de evento com campos novos preservado (write → read)
- [ ] Leitor antigo (sem os campos) não falha — colunas nullable
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~EventLogRecord"`
- [ ] Test count: 3+ novos (round-trip, nullable-safe, sequence intacto)

**Tests**: integration
**Gate**: full
**Commit**: `feat(infrastructure): persist causal provenance fields on EventLogRecord`

---

### T4: `ResolveRootCauseEventId` + `CausalRules.MaxCauseChainDepth` + `CausalChainTooDeepException`

**What**: Função pura que percorre `CauseEventId` até achar raiz ou `maxDepth`; nova `CausalRules` (cenário-driven, template `NeedsRules`); nova exceção mesmo shape de `TickBudgetExceededException`.
**Where**: `src/LivingWorld.Simulation/History/CausalProvenance.cs` (novo), `src/LivingWorld.Domain/History/CausalRules.cs` (novo), `src/LivingWorld.Simulation/CausalChainTooDeepException.cs` (novo)
**Depends on**: T1
**Reuses**: padrão `TickBudgetExceededException`/`NeedsRules.MaxActionSelectionSteps` (`TickBudgetExceededException.cs`, `NeedsRules.cs:12`)
**Requirement**: COH-02

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `ResolveRootCauseEventId` retorna a raiz correta em cadeia de 1, 3 e N eventos
- [ ] Cadeia sem causa (`CauseEventId = null`) retorna o próprio evento como raiz (AC3 do spec)
- [ ] Ciclo/profundidade excedida lança `CausalChainTooDeepException` nomeando o evento culpado
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~CausalProvenance"`
- [ ] Test count: 5+ (raiz simples, cadeia longa, sem causa, ciclo, limite exato)

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(history): resolve RootCauseEventId on demand with cycle guard`

---

### T5: Determinismo de cadeia causal + migração de 3 call sites piloto

**What**: Teste de determinismo (mesma seed → mesma cadeia `CauseEventId`→`RootCauseEventId`); migra 3 call sites piloto identificados no survey (`ExtraordinaryInvocationEngine` uso→custo→efeito, `NatalitySystem.StillBirth`, `BookRediscoverySystem`) para passar `CauseEventId`/`SourceSystem` explícitos, provando o padrão pros demais ~54 (auditoria completa fica em P3/T34).
**Where**: `src/LivingWorld.Simulation/Extraordinary/ExtraordinaryInvocationEngine.cs`, `src/LivingWorld.Simulation/Population/NatalitySystem.cs`, `src/LivingWorld.Simulation/History/BookRediscoverySystem.cs`
**Depends on**: T2, T4
**Reuses**: overload de T2
**Requirement**: COH-05

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Teste de determinismo com 2 runs mesma seed produz cadeia idêntica
- [ ] 3 call sites piloto passam `CauseEventId`/`SourceSystem` reais (não `"Unknown"`)
- [ ] Cadeia `ExtraordinaryUseAttempted→ExtraordinaryCostPaid→ExtraordinaryEffectApplied` reconstruível via `ResolveRootCauseEventId`
- [ ] Gate check passa: `bash scripts/test.sh` (suíte inteira)

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(history): wire CauseEventId through pilot call sites and prove determinism`

---

### T6: `BodyRules` (cenário-driven)

**What**: Novo record `BodyRules(HeightMean, HeightStdDev, WeightMean, WeightStdDev, MuscleMassMean, MuscleMassStdDev, MuscleMassMin, MuscleMassMax, Enabled)`, `Create` validador, `Default`.
**Where**: `src/LivingWorld.Domain/Population/BodyRules.cs` (novo)
**Depends on**: None
**Reuses**: template `FamilyRules.cs`
**Requirement**: COH-21 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `Create` rejeita stddev negativo, min > max (mesmos moldes de mensagem de erro de `FamilyRules`)
- [ ] `Default` documentado com valores plausíveis
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~BodyRulesTests"`
- [ ] Test count: 4+ (válido, cada campo inválido)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add scenario-configurable BodyRules`

---

### T7: `Npc` — campos `Height`/`Weight`/`MuscleMass` + geração seed/nascimento

**What**: 3 campos canônicos novos em `Npc`; geração em `PopulationGenerator` (seed) via stream `"height-{npcId}"`/`"weight-{npcId}"`/`"musclemass-{npcId}"`; geração em `NatalitySystem` (nascimento) via `ctx.StreamFor`, clamp em `BodyRules.Min/Max`.
**Where**: `src/LivingWorld.Domain/Population/Npc.cs`, `src/LivingWorld.Domain/Population/PopulationGenerator.cs`, `src/LivingWorld.Simulation/Population/NatalitySystem.cs`
**Depends on**: T6
**Reuses**: padrão `HeredityService.RollInitial`/`InheritVitality` + `WorldRngRegistry.StableHash`/`ctx.StreamFor` (`HeredityService.cs:15-39`, `PopulationGenerator.cs:53-56`, `NatalitySystem.cs:92-95`)
**Requirement**: COH-21

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] NPC criado no seed recebe os 3 campos, sempre dentro de `[Min,Max]`
- [ ] NPC nascido em runtime recebe os 3 campos pelo mesmo mecanismo de stream
- [ ] Teste "nunca fora da faixa através de 200 seeds" (mesmo idioma de `HeredityServiceTests.cs:45`)
- [ ] Teste de determinismo: mesma seed → mesmos valores
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~Npc|FullyQualifiedName~PopulationGenerator|FullyQualifiedName~NatalitySystem"`

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(population): generate Height/Weight/MuscleMass via seeded RNG streams`

---

### T8: `BodyMechanic` — funções puras de multiplier (neutro 1.0)

**What**: `WorkCapacityMultiplier(WorldState, Npc)`, `MovementCostMultiplier(WorldState, Npc)` — funções puras, mesmo shape de `AttributeMechanic.ProductMultiplier`, neutro 1.0 se `BodyRules.Enabled = false`.
**Where**: `src/LivingWorld.Simulation/Behavior/BodyMechanic.cs` (novo)
**Depends on**: T7
**Reuses**: shape `AttributeMechanic.cs:44-89`
**Requirement**: COH-22, COH-23

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `WorkCapacityMultiplier` cresce com `MuscleMass` maior, neutro quando desabilitado
- [ ] `MovementCostMultiplier` varia com `Weight`/`Height`, neutro quando desabilitado
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~BodyMechanic"`
- [ ] Test count: 4+ (cada multiplier, habilitado/desabilitado)

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): add BodyMechanic pure multipliers for work capacity and movement cost`

---

### T9: `WorkCapacityMultiplier` plugado em `ProductionSystem` [P]

**What**: `ProductionSystem.Produce` ganha `WorkCapacityMultiplier` como 4º fator ao lado de `skillMultiplier`/`strengthMultiplier` (linha 79).
**Where**: `src/LivingWorld.Simulation/Economy/ProductionSystem.cs`
**Depends on**: T8
**Reuses**: `StrengthMultiplierOf` (`ProductionSystem.cs:109-118`, mesmo padrão de média sobre `presentWorkers`)
**Requirement**: COH-22

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Dois NPCs golden-seeded, mesma skill/emprego, `MuscleMass` diferente → `produced` diferente (spec P1c Independent Test)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~ProductionSystem"`
- [ ] Test count: 2+ (multiplier aplicado, dois NPCs divergem)

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(economy): factor WorkCapacityMultiplier into production output`

---

### T10: `MovementCostMultiplier` plugado em `TravelResolution` [P]

**What**: Novo overload `TravelResolution.TicksBetween(map, origin, destination, movementCostMultiplier = 1.0)`; `BehaviorDecisionSystem` (`ActionType.Travel`) passa `BodyMechanic.MovementCostMultiplier(world, npc)`.
**Where**: `src/LivingWorld.Domain/Geography/TravelResolution.cs`, `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs`
**Depends on**: T8
**Reuses**: `MovementCost.Between` (`MovementCost.cs:8-21`)
**Requirement**: COH-23

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Overload antigo preservado (default multiplier 1.0, nenhum call site quebra)
- [ ] NPC com `Weight`/`Height` diferente produz custo de viagem diferente
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~TravelResolution|FullyQualifiedName~BehaviorDecisionSystemTests"`
- [ ] Test count: 2+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(geography): factor MovementCostMultiplier into travel time`

---

### T11: `ApplyWorkHardening` — `MuscleMass` cresce com trabalho pesado sustentado

**What**: Novo sistema `Daily` (categoria SLOW, doc#19) que incrementa `MuscleMass` de NPCs em trabalho físico pesado sustentado, respeitando `BodyRules.MuscleMassMax`.
**Where**: `src/LivingWorld.Simulation/Behavior/BodyMechanic.cs` (extend), registro em `ScenarioRunner.cs`
**Depends on**: T9, T10
**Reuses**: padrão de sistema `Daily` já existente (`ProductionSystem`/`MarketPricingSystem`, AD-042)
**Requirement**: COH-24, COH-25 (auditoria de consumidores futuros documentada, não implementada agora)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] NPC em trabalho pesado sustentado por N dias tem `MuscleMass` maior que baseline
- [ ] Clamp em `MuscleMassMax` respeitado
- [ ] Nota de auditoria (COH-25): campos sem consumidor em outro contexto (equipment/combat) documentados em comentário/issue rastreável pra P3 (T34), não implementados
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~BodyMechanic"`

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(behavior): grow MuscleMass slowly from sustained heavy labor`

---

### T12: `DecisionContext` + DTOs (`NeedsSnapshot`, `BodySnapshot`, `HouseholdSnapshot`, `RelationshipFact`)

**What**: Novo record `DecisionContext` e os 4 DTOs de suporte, exatamente como definidos em design.md § Data Models.
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionContext.cs` (novo)
**Depends on**: T8 (para `BodySnapshot` real)
**Reuses**: nenhum — tipo novo, mas campos espelham exatamente o que `SelectByUtility` já lê hoje
**Requirement**: COH-11 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Todos os campos de coleção default para lista vazia, nunca `null`
- [ ] Compila sem referenciar `WorldState` diretamente (é um snapshot puro)
- [ ] Gate check passa: build gate only (tipo sem lógica ainda)

**Tests**: none
**Gate**: build
**Commit**: `feat(behavior): add DecisionContext record and supporting DTOs`

---

### T13: `DecisionContextBuilder.Build` — needs/body/household (sem memory/belief/relationship/power ainda)

**What**: Primeira fatia do builder — popula `Needs`, `Body`, `Household`, `Personality`, `CurrentAction` a partir de `WorldState`/`Npc`, espelhando 1:1 o que `SelectByUtility`/`UtilityBaseOf` já leem hoje. `RelevantMemories`/`RelevantBeliefs`/`KnownRelationships`/`PowerOpportunities` ficam vazios nesta task (preenchidos nas próximas).
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionContextBuilder.cs` (novo)
**Depends on**: T12
**Reuses**: mesmos acessos que `BehaviorDecisionSystem.cs:296-397` já faz hoje
**Requirement**: COH-11

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `DecisionContext` construído para um NPC contém needs/body/household/personality corretos, comparável byte-a-byte com o que o código antigo lia
- [ ] NPC sem household → `Household = null`, sem erro
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~DecisionContext"`
- [ ] Test count: 3+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): build DecisionContext needs/body/household slice`

---

### T14: `DecisionContextBuilder` — memória e crença relevantes

**What**: Preenche `RelevantMemories` via `MemoryRecall.Recall` e `RelevantBeliefs` via `NpcBeliefQuery.BeliefsOf` (query derivada da pressão/need ativa mais alta do NPC).
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionContextBuilder.cs` (extend)
**Depends on**: T13
**Reuses**: `MemoryRecall.Recall` (`MemoryRecall.cs:12`), `NpcBeliefQuery.BeliefsOf` (`NpcBeliefQuery.cs:15`)
**Requirement**: COH-12, COH-13

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] NPC sem memória/crença relevante → listas vazias, sem erro (Edge Case)
- [ ] NPC com memória relevante ("foi traído por X") aparece em `RelevantMemories`
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~DecisionContext"`
- [ ] Test count: 3+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): wire memory and belief recall into DecisionContext`

---

### T15: `DecisionContextBuilder` — relações conhecidas

**What**: Preenche `KnownRelationships` via `world.Relationships`/`RelationshipSystem` — só relações já existentes (lazy, AD-061), nunca cria entrada nova a partir da decisão.
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionContextBuilder.cs` (extend)
**Depends on**: T14
**Reuses**: `world.Relationships` (`WorldState.cs:204`), `RelationshipKey`
**Requirement**: COH-14

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] NPC que nunca encontrou ninguém → `KnownRelationships` vazio, sem scan global
- [ ] NPC com relação existente aparece com os 4 eixos corretos
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~DecisionContext"`

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): wire known relationships into DecisionContext`

---

### T16: Refactor `SelectByUtility`/`UtilityBaseOf` para receber `DecisionContext`

**What**: Assinatura migra de `(WorldState world, Npc npc, NeedsRules rules, ...)` para `(DecisionContext ctx, NeedsRules rules, ...)`; todo acesso interno troca `world.X`/`npc.X` por `ctx.X`. `BehaviorDecisionSystem.Tick` chama `DecisionContextBuilder.Build` uma vez por NPC antes de `SelectByUtility`.
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs:296,355` (refactor in-place)
**Depends on**: T15
**Reuses**: lógica de scoring existente (não muda, só a fonte dos dados)
**Requirement**: COH-11 (AD-011)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Todos os `[Theory]` existentes de `BehaviorDecisionSystemTests.cs` continuam passando sem alteração de expectativa
- [ ] Golden hash do cenário default idêntico (nenhuma mudança de comportamento, só de fonte de dado)
- [ ] Gate check passa: `bash scripts/test.sh` (suíte inteira)
- [ ] Test count: baseline mantido, 0 regressão

**Tests**: unit + determinism
**Gate**: full
**Commit**: `refactor(behavior): SelectByUtility reads DecisionContext instead of raw WorldState`

---

### T17: Testes de divergência comportamental (memória/crença/relação/household diferentes → decisão diferente)

**What**: 4 testes dedicados provando as ACs mais importantes do P1b: dois NPCs com estado material idêntico mas memória (ou crença, ou relação, ou composição de household) diferente produzem decisões diferentes.
**Where**: `tests/LivingWorld.Tests/Behavior/DecisionContextIntegrationTests.cs` (novo)
**Depends on**: T16
**Reuses**: builders de cenário já usados em `BehaviorDecisionSystemTests.cs`
**Requirement**: COH-13, COH-14, COH-15, COH-16

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Teste "memória diferente → decisão diferente" passa (COH-13)
- [ ] Teste "relação diferente → decisão diferente" passa (COH-14)
- [ ] Teste "household muda composição → pressão/oportunidade reflete" passa (COH-15)
- [ ] Teste "sem fatores relevantes → decisão não quebra" passa (COH-16)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~DecisionContextIntegrationTests"`

**Tests**: unit + determinism
**Gate**: full
**Commit**: `test(behavior): prove memory/belief/relationship/household divergence in decisions`

---

### T18: `PowerUtilityRules` (cenário-driven)

**What**: Novo record `PowerUtilityRules(CostWeight, RiskWeight, ReliabilityWeight, UrgencyWeight)`, `Create`, `Default`.
**Where**: `src/LivingWorld.Domain/Extraordinary/PowerUtilityRules.cs` (novo)
**Depends on**: None (paralelizável com Phase 1-3, mas sequenciado aqui por causa da ordem de execução declarada)
**Reuses**: template `PowerInheritanceRules.cs`
**Requirement**: COH-31 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `Create` valida pesos não-negativos
- [ ] `Default` documentado
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~PowerUtilityRulesTests"`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(domain): add scenario-configurable PowerUtilityRules`

---

### T19: `PowerOpportunity` record + heurística de custo/risco a partir de `PowerDescriptor`

**What**: Novo record `PowerOpportunity`; função que deriva `EstimatedCost`/`EstimatedRisk` de `PowerDescriptor.Costs`/`Reliability`/`FailureModes` (heurística documentada — Risk & Concerns do design).
**Where**: `src/LivingWorld.Simulation/Extraordinary/PowerOpportunity.cs` (novo)
**Depends on**: T18
**Reuses**: `PowerDescriptor` (`PowerDescriptor.cs:7-22`)
**Requirement**: COH-31 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `Reliability = "Guaranteed"` → risco baixo fixo; `"ResolutionCheck"` → risco maior
- [ ] `Costs.Count` maior → `EstimatedCost` maior
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~PowerOpportunity"`
- [ ] Test count: 3+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(extraordinary): derive PowerOpportunity cost/risk heuristic from PowerDescriptor`

---

### T20: `PowerOpportunityProvider.ApplicableTo` — filtra por Mode + `CurrentStageIndex`

**What**: Preenche o gap "MISSING" do survey — novo método que itera `ExtraordinaryCarrierState` do NPC, filtra por `IsAvailable` (Mode) **e** por `CurrentStageIndex`/`PowerDescriptor.Stages` (16.2), devolve `IReadOnlyList<PowerOpportunity>`.
**Where**: `src/LivingWorld.Simulation/Extraordinary/PowerOpportunityProvider.cs` (novo)
**Depends on**: T19
**Reuses**: `IsAvailable` (`ExtraordinaryInvocationEngine.cs:357-365`), `ExtraordinaryMechanicRegistry.Resolve`
**Requirement**: COH-31, COH-32

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] NPC sem carrier manifestado → lista vazia
- [ ] NPC com capacidade aplicável e stage liberada → aparece na lista
- [ ] NPC com capacidade mas stage bloqueada → NÃO aparece (16.2 respeitado)
- [ ] Todos os 27 mechanics do registry são alcançáveis pelo mecanismo de exposição (teste de cobertura, mesmo espírito de `PowerEvolutionCoverageTests.cs`)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~PowerOpportunityProvider"`
- [ ] Test count: 27-category coverage + 4 casos de filtro = 6+

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(extraordinary): add PowerOpportunityProvider filtering by mode and evolution stage`

---

### T21: `ActionType.UsePower` + `Npc.PendingPowerInvocation` + `ActionCatalog` entry

**What**: Novo valor de enum `UsePower = 7`; campo volátil `[JsonIgnore] Npc.PendingPowerInvocation` (`PowerOpportunity?`); entrada de duração em `ActionCatalog` (proteção estática AD-040).
**Where**: `src/LivingWorld.Domain/Behavior/ActionType.cs`, `src/LivingWorld.Domain/Population/Npc.cs`, `src/LivingWorld.Simulation/Behavior/ActionCatalog.cs`
**Depends on**: T20
**Reuses**: proteção estática `ActionCatalog.Create` (AD-040)
**Requirement**: COH-33 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `ActionCatalog.Create` continua reprovando estaticamente se `UsePower` não tiver duração declarada (teste de cobertura confirma)
- [ ] Todo switch existente sobre `ActionType` (`PersonalityWeighting.TraitValueOf`, `NpcWakeScheduler`) lida com `UsePower` sem exceção não tratada — task cobre cada switch encontrado (ver Risk do design)
- [ ] Gate check passa: `bash scripts/test.sh` (suíte inteira — pega qualquer switch esquecido)
- [ ] Test count: baseline + 2 novos (catalog + switch coverage)

**Tests**: unit
**Gate**: full
**Commit**: `feat(behavior): add ActionType.UsePower with catalog duration and PendingPowerInvocation`

---

### T22: `PowerOpportunityUtility` scoring + integração em `SelectByUtility` [P]

**What**: `PowerOpportunityUtility(PowerOpportunity, DecisionContext, PowerUtilityRules)` — combina custo/risco/confiabilidade/urgência; `SelectByUtility` compara candidatos `ActionType` fixos com candidatos `PowerOpportunity` dinâmicos (via `ctx.PowerOpportunities`), vencedor `UsePower` seta `PendingPowerInvocation`.
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (extend)
**Depends on**: T21
**Reuses**: `SelectByUtility` já refatorado em T16
**Requirement**: COH-33

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] NPC com Teleport disponível e pressão `ReachDestinationUrgently` alta escolhe `UsePower` quando o score compensa (spec P1d Independent Test)
- [ ] NPC sem capacidade nunca considera a opção (candidatos vazios)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~BehaviorDecisionSystemTests|FullyQualifiedName~PowerOpportunity"`
- [ ] Test count: 4+

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(behavior): score PowerOpportunity candidates alongside fixed ActionType in utility`

---

### T23: Execução de `UsePower` — chama `ExtraordinaryInvocationEngine.Invoke` + `PowerInvoked` event [P]

**What**: Quando a ação em curso é `UsePower`, o ponto de execução (mesmo lugar onde `Eat`/`Work`/etc. resolvem) chama `ExtraordinaryInvocationEngine.Invoke` com o `PendingPowerInvocation`, loga `WorldEventKind.PowerInvoked` (novo valor no enum) com `CauseEventId` apontando pro evento de decisão.
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (extend), `src/LivingWorld.Domain/History/WorldEventKind.cs`
**Depends on**: T21
**Reuses**: `ExtraordinaryInvocationEngine.Invoke` (`ExtraordinaryInvocationEngine.cs:52-87`), `LogEvent` overload (T2)
**Requirement**: COH-33

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `UsePower` executado dispara `PowerInvoked` com `CauseEventId` setado
- [ ] Consequências normais (`ExtraordinaryEffectApplied` etc.) continuam disparando como hoje
- [ ] `PendingPowerInvocation` limpo após execução
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~BehaviorDecisionSystemTests"`

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(behavior): execute UsePower through ExtraordinaryInvocationEngine and log PowerInvoked`

---

### T24: Golden hash regression sweep — todos os 27 mechanics via cenários existentes

**What**: Roda cada golden/cenário existente que usa powers (16.1/16.2) contra o pipeline novo; para cada divergência, documenta AD explícito em `STATE.md` (padrão AD-065/069) ou corrige regressão real.
**Where**: `tests/golden/world-hashes.json`, `.specs/STATE.md`
**Depends on**: T22, T23
**Reuses**: `GoldenHashesTests.cs`, procedimento AD-065/069
**Requirement**: COH-34, COH-35, COH-36

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Todo golden com powers roda; divergências viram AD numerado (nunca silenciosas)
- [ ] Teste "Agent-com-capacidade vs Agent-sem-capacidade" confirma diferença só nas oportunidades (COH-35, spec P1d Independent Test)
- [ ] `ControlMechanic.TryDelegatedAction` (possessão) continua funcionando intocado (COH-36)
- [ ] Gate check passa: `bash scripts/test.sh` (suíte inteira) + `bash scripts/test.sh --filter "Category=Scenario"`

**Tests**: unit + determinism + scenario
**Gate**: full
**Commit**: `test(extraordinary): confirm power-utility migration preserves golden behavior or document AD`

---

### T25: `AttentionRules` (cenário-driven) + `Intent` domain type

**What**: `AttentionRules` (limiares de relevância por critério doc#59); `Npc` ganha `CurrentIntent`, `IntentStartedTick`, `IntentTarget`, `IntentStatus` (enum `Active`/`Completed`/`Invalidated`).
**Where**: `src/LivingWorld.Domain/Behavior/AttentionRules.cs` (novo), `src/LivingWorld.Domain/Population/Npc.cs` (extend)
**Depends on**: T17 (precisa de `DecisionContext` estável)
**Reuses**: template `*Rules`; `Npc.CurrentAction`/`ActionStartedAtTick` como precedente de shape
**Requirement**: COH-41 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Campos novos serializam/round-trip corretamente
- [ ] `IntentStatus` transições válidas testadas (Active→Completed, Active→Invalidated)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~Npc"`

**Tests**: unit
**Gate**: quick
**Commit**: `feat(population): add CurrentIntent/IntentStatus fields to Npc`

---

### T26: Plano adaptável — falha local tenta alternativa antes de invalidar Intent

**What**: Quando uma ação local dentro de um plano falha (ex.: vendedor indisponível), tenta alternativas (outro vendedor, household member, estoque) antes de marcar `IntentStatus = Invalidated`.
**Where**: `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs` (extend, caminho de `Buy`/`Eat`)
**Depends on**: T25
**Reuses**: lógica de fallback já existente em `Buy`/household stock (`BehaviorDecisionSystem.cs:371-397`)
**Requirement**: COH-42

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Vendedor indisponível → tenta household stock antes de invalidar
- [ ] Todas alternativas falham → `IntentStatus = Invalidated`, reconsideração completa dispara
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~BehaviorDecisionSystemTests"`
- [ ] Test count: 3+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): retry plan alternatives before invalidating Intent`

---

### T27: `AttentionRouter.RouteRelevantNpcs`

**What**: Dado um `WorldEvent`, retorna o conjunto de NPCs que precisam reconsiderar, por critério doc#59 (localização, household, relação, dependência de intent, conhecimento, dependência econômica, condição física, magnitude, urgência, ameaça, interação de capacidade).
**Where**: `src/LivingWorld.Simulation/Behavior/AttentionRouter.cs` (novo)
**Depends on**: T25
**Reuses**: `world.Relationships`, `world.FindHousehold`, `DecisionContext` (para dependência de intent)
**Requirement**: COH-43

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Evento de baixa magnitude (ex.: preço +1%) NÃO roteia a cidade inteira
- [ ] NPC com dependência real do evento (household afetado, relação, intent dependente) É roteado
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~AttentionRouter"`
- [ ] Test count: 6+ (um por critério principal + caso negativo)

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(behavior): add AttentionRouter to scope wake-relevant NPCs to an event`

---

### T28: `NpcWakeScheduler` — novo motivo de wake roteado [P]

**What**: `NpcWakeScheduler.ComputeNextWakeTick` ganha um gatilho novo: evento roteado pelo `AttentionRouter` agenda wake imediato pro NPC alvo, ao lado dos gatilhos já existentes (threshold, fim de ação).
**Where**: `src/LivingWorld.Simulation/Behavior/NpcWakeScheduler.cs` (extend)
**Depends on**: T27
**Reuses**: `NpcWakeBatch`/`WorldState.ReplaceNpcWake` (dedupe já existente)
**Requirement**: COH-43, COH-44

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Evento roteado agenda wake sem duplicar entrada (dedupe intacto)
- [ ] NPC sem Intent válido não é afetado (comportamento antigo preservado)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~NpcWakeScheduler|FullyQualifiedName~NeedsDecaySystemTests"`

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(behavior): route event-driven wakeups through NpcWakeScheduler`

---

### T29: `DecisionContextCache` + `MarkDirty` reusando `TouchCanonical` [P]

**What**: `DecisionContextCategory` enum (flags); `MarkDirty(world, npcId, category)` chamado nos mesmos pontos de mutação que já chamam `TouchCanonical` (PERF-12); `BuildIncremental` reconstrói só categorias dirty, copia o resto do cache do wake anterior.
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionContextCache.cs` (novo)
**Depends on**: T17
**Reuses**: mecanismo `TouchCanonical`/`IncrementalHasher.MatchesCanonical` (PERF-12, Fase 9)
**Requirement**: COH-45

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Categoria limpa não dispara `MemoryRecall`/`NpcBeliefQuery`/`RelationshipSystem` de novo (verificável por contagem de chamadas em teste)
- [ ] Categoria dirty reconstrói e produz resultado idêntico ao `DecisionContextBuilder.Build` completo
- [ ] Resultado canônico final idêntico entre modo "full reconstruct" e "incremental" (spec P2a Independent Test, doc#98)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~DecisionContextCache"`
- [ ] Test count: 5+

**Tests**: unit + determinism
**Gate**: full
**Commit**: `feat(behavior): add per-category DecisionContext cache reusing TouchCanonical`

---

### T30: Métricas de redecisão — decisions/wakeups por agent-day, comparação full vs event-driven

**What**: Instrumenta contadores (decisions/agent-day, wakeups/agent-day, % wakeups mudando intent) e um teste comparativo full-reconsideration vs event-driven no mesmo cenário determinístico.
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionMetrics.cs` (novo), teste dedicado
**Depends on**: T28, T29
**Reuses**: nenhum sistema novo de coleta — contadores simples sobre eventos já existentes
**Requirement**: COH-44, COH-45

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Mesmo resultado canônico final nos dois modos (doc#98)
- [ ] Número de decisões/wakeups mensuravelmente menor no modo event-driven
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~DecisionMetrics"`

**Tests**: unit + determinism
**Gate**: full
**Commit**: `test(behavior): compare full-reconsideration vs event-driven attention metrics`

---

### T31: `PressureModel.DerivePressures`

**What**: Função pura `DecisionContext → IReadOnlyList<Pressure>` (ex.: `AcquireFood`, `EarnIncome`, `ProtectHousehold`), combinando múltiplos fatores quando aplicável (doc#34), sem duplicar estado canônico (doc#33).
**Where**: `src/LivingWorld.Simulation/Behavior/PressureModel.cs` (novo)
**Depends on**: T17
**Reuses**: `DecisionContext` já construído
**Requirement**: COH-51, COH-52

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `ProtectHouseholdPressure` combina ≥3 fatores (dependentes, relação, ameaça) num teste dedicado
- [ ] Nenhum campo canônico novo redundante introduzido (revisão de código confirma)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~PressureModel"`
- [ ] Test count: 4+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): derive Pressure list from DecisionContext`

---

### T32: `OpportunityModel.DeriveOpportunities`

**What**: Função pura `DecisionContext → IReadOnlyList<Opportunity>` (ex.: `FoodAtMarket`, `NearbyJob`, `PotentialPartner`, `ExtraordinaryCapability`), filtrada só pelo que o NPC conhece/percebe/alcança/tem permissão (doc#38-39).
**Where**: `src/LivingWorld.Simulation/Behavior/OpportunityModel.cs` (novo)
**Depends on**: T31
**Reuses**: `DecisionContext.PowerOpportunities`, `KnownRelationships`, `Household`
**Requirement**: COH-53

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Oportunidade que o NPC não conhece NUNCA aparece (teste negativo explícito)
- [ ] `ExtraordinaryCapability` aparece só quando `PowerOpportunities` não está vazio
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~OpportunityModel"`
- [ ] Test count: 4+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): derive Opportunity list from DecisionContext`

---

### T33: `DecisionTrace` — top factors, blocking factors, alternativas

**What**: Record volátil `DecisionTrace` (`WakeReason`, `PreviousIntent`, `TopPressures`, `KnownOpportunities`, `Winner`, `WinningUtility`); `SelectByUtility` popula e expõe (não persiste, doc#84).
**Where**: `src/LivingWorld.Simulation/Behavior/DecisionTrace.cs` (novo), extend `BehaviorDecisionSystem.cs`
**Depends on**: T31, T32
**Reuses**: `SelectByUtility` já refatorado (T16)
**Requirement**: COH-54

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `DecisionTrace` identifica top positive/negative factors e alternativas conhecidas num teste dedicado
- [ ] `DecisionTrace` não é `[Canonical]`, não afeta golden hash (confirmado por teste)
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~DecisionTrace"`

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(behavior): expose DecisionTrace with top factors and alternatives`

---

### T34: Auditoria — `docs/audits/living-world-cohesion-audit.md` (System + Attribute Integration Matrix)

**What**: Documento de auditoria com as duas matrizes (doc#22-23) cobrindo Events/DecisionContext/Body/Memory/Belief/Relationships/Household/Powers/Intent/Attention — inclui os ~54 call sites de `LogEvent` ainda não migrados (de T5) classificados por prioridade.
**Where**: `docs/audits/living-world-cohesion-audit.md` (novo)
**Depends on**: T5, T17, T24, T30, T33
**Reuses**: nenhum — documento de síntese
**Requirement**: COH-61

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] System Integration Matrix cobre os 10 sistemas listados na spec
- [ ] Attribute Integration Matrix classifica `Height`/`Weight`/`MuscleMass`/`Vitality`/`Upbringing`/etc como CAUSAL/PARTIALLY_INTEGRATED/PRESENTATION_ONLY/FUTURE_DEPENDENCY/UNUSED
- [ ] Gate check passa: build gate only (documentação)

**Tests**: none
**Gate**: build
**Commit**: `docs(audit): publish living-world-cohesion System and Attribute Integration Matrix`

---

### T35: `CausalDiagnostics.CausalDepth` + `SystemsTouchedByCausalChain`

**What**: Funções puras sobre a cadeia `CauseEventId`, usando o mesmo `maxDepth` guard de T4.
**Where**: `src/LivingWorld.Simulation/History/CausalDiagnostics.cs` (novo)
**Depends on**: T5
**Reuses**: `ResolveRootCauseEventId` (T4)
**Requirement**: COH-62

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] `CausalDepth` retorna profundidade correta em cadeia conhecida de teste
- [ ] `SystemsTouchedByCausalChain` retorna o conjunto correto de `SourceSystem`
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~CausalDiagnostics"`
- [ ] Test count: 4+

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `feat(history): add CausalDepth and SystemsTouchedByCausalChain diagnostics`

---

### T36: `iteration budget` / Event Storm Protection para ciclo real de produção

**What**: Guard determinístico separado do guard de cadeia causal (T4) — cobre ciclo de PRODUÇÃO real (A muda B muda A na mesma tick), reusando `TickBudgetExceededException`/`MaxActionSelectionSteps` por outro ângulo, conforme doc#81.
**Where**: `src/LivingWorld.Simulation/WorldClock.cs` (revisão — confirma se o guard existente já cobre; se não, extend)
**Depends on**: T4
**Reuses**: `TickBudgetExceededException`, `WorldClock.cs:57-58` (`DispatchDueEvents`, `maxIterationsPerTick`)
**Requirement**: COH-63

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Ciclo sintético A→B→A→B aborta deterministicamente, nomeando o sistema/evento culpado
- [ ] Guard existente (`WorldClock`) confirmado suficiente OU extendido — decisão documentada no PR
- [ ] Gate check passa: `bash scripts/test.sh --filter "FullyQualifiedName~WorldClock"`

**Tests**: unit + determinism
**Gate**: quick
**Commit**: `test(history): confirm event storm protection aborts deterministically on production cycle`

---

### T37: `test-living-village` — cenário base (40 Agents, 10 Households, 1 Settlement)

**What**: Novo cenário JSON (`scenarios/test-living-village.json`) + `ScenarioLoader` — Farmers/Baker/Blacksmith/Merchant/Guards/Workers, Food/Employment/Markets/Relationships/Memory/Beliefs/Skills/Body/Family, Powers opcional. Sem crise pré-escrita (doc#89).
**Where**: `scenarios/test-living-village.json` (novo), `tests/LivingWorld.Tests/Scenario/LivingVillageScenarioTests.cs` (novo)
**Depends on**: T17, T24, T30, T33
**Reuses**: `ScenarioLoader`/`MapScenarioLoader`/`PopulationScenarioLoader` existentes (AD-027)
**Requirement**: COH-64 (suporte)

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Cenário carrega e roda determinístico N ticks sem erro
- [ ] Nenhuma função tipo `CreateFoodCrisis()`/`MakeXHungry()` existe no código do cenário (revisão confirma)
- [ ] Gate check passa: `bash scripts/test.sh --filter "Category=Scenario&FullyQualifiedName~LivingVillage"`

**Tests**: scenario
**Gate**: full
**Commit**: `test(scenario): add deterministic test-living-village baseline scenario`

---

### T38: Choque `harvest output -30%` + verificação de cadeia causal cross-system

**What**: Injeta o choque determinístico; verifica via `CausalDiagnostics` que a cadeia `HarvestReduced → FoodStockReduced → PriceIncreased → PurchaseFailed → HungerCritical → IntentChanged → EmploymentAffected` (ou equivalente) toca ≥5 sistemas.
**Where**: `tests/LivingWorld.Tests/Scenario/LivingVillageScenarioTests.cs` (extend)
**Depends on**: T37
**Reuses**: `CausalDiagnostics` (T35)
**Requirement**: COH-64

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Cadeia causal capturada toca ≥5 sistemas distintos, sem scripting narrativo
- [ ] Determinístico entre 2 runs mesma seed
- [ ] Gate check passa: `bash scripts/test.sh --filter "Category=Scenario&FullyQualifiedName~LivingVillage"`

**Tests**: scenario
**Gate**: full
**Commit**: `test(scenario): prove cross-system causal chain from harvest shock in test-living-village`

---

### T39: Métricas finais + fechamento — `STATE.md` + `verify.sh`

**What**: Coleta as métricas doc#85 no cenário (`decisions/agent-day`, `wakeups/agent-day`, `causal depth médio/p95/máximo`, `cross-system chains`, `atributos sem consumidor`); atualiza `STATE.md` com baseline anterior/posterior, ADRs, golden hashes alterados; roda `bash scripts/verify.sh`.
**Where**: `.specs/STATE.md`, `tests/LivingWorld.Tests/Scenario/LivingVillageScenarioTests.cs` (extend)
**Depends on**: T38
**Reuses**: `DecisionMetrics` (T30), `CausalDiagnostics` (T35)
**Requirement**: COH-65, COH-66

**Tools**: MCP: NONE · Skill: NONE

**Done when**:

- [ ] Todas as métricas doc#85 coletadas e reportadas no teste
- [ ] `STATE.md` atualizado com baseline anterior/posterior + ADRs + golden hashes alterados e por quê
- [ ] `bash scripts/verify.sh` verde (usuário confirma antes de fechar a fase, AD-009)
- [ ] `docs/audits/living-world-cohesion-audit.md` (T34) linkado no fechamento

**Tests**: scenario
**Gate**: build
**Commit**: `docs(state): close phase 16.3 with metrics baseline and golden hash changes`

---

## Parallel Execution Map

```
Phase 1 (Sequential): T1 → T2 → T3 → T4 → T5

Phase 2 (mostly Sequential): T6 → T7 → T8, then:
  ├── T9  [P]
  └── T10 [P]
  T9, T10 → T11

Phase 3 (Sequential): T12 → T13 → T14 → T15 → T16 → T17

Phase 4 (mostly Sequential): T18 → T19 → T20 → T21, then:
  ├── T22 [P]
  └── T23 [P]
  T22, T23 → T24

Phase 5 (mostly Sequential): T25 → T26 → T27, then:
  ├── T28 [P]
  └── T29 [P]
  T28, T29 → T30

Phase 6 (Sequential): T31 → T32 → T33

Phase 7 (Sequential): T34 → T35 → T36 → T37 → T38 → T39
```

**Parallelism constraint:** `[P]` exige zero dependência não terminada, teste parallel-safe (Parallelism Assessment acima), e nenhum estado mutável compartilhado com outro `[P]` da mesma fase. T9/T10 tocam arquivos diferentes (`ProductionSystem`/`TravelResolution`); T22/T23 tocam o mesmo arquivo (`BehaviorDecisionSystem.cs`) em métodos diferentes — risco de conflito de merge se executados por agentes distintos ao mesmo tempo; T28/T29 tocam arquivos diferentes (`NpcWakeScheduler`/`DecisionContextCache`).

**Fases > 3**: esta feature tem 7 fases — ao entrar em Execute, ofertar um sub-agent por fase (sequencial), conforme protocolo do skill.

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1-T5 | 1 tipo/campo por task, 1-3 arquivos relacionados | ✅ Granular |
| T6-T11 | 1 componente por task | ✅ Granular |
| T12-T17 | 1 fatia do builder ou 1 refactor por task | ✅ Granular |
| T18-T24 | 1 componente/integração por task | ✅ Granular |
| T25-T30 | 1 componente por task | ✅ Granular |
| T31-T33 | 1 função pura por task | ✅ Granular |
| T34-T39 | 1 entregável de fechamento por task | ✅ Granular |

Nenhuma task cobre múltiplos componentes não-relacionados — todas em ≤3 arquivos coesos (mesma feature/refactor).

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1→T2 | ✅ Match |
| T3 | T2 | T2→T3 | ✅ Match |
| T4 | T1 | T1→T4 (fora da cadeia linear, mas T4 está listado sequencial após T3 no diagrama simplificado — dependência real é só T1) | ✅ Match (diagrama simplificado, dependência real documentada no corpo) |
| T5 | T2, T4 | segue T4 na sequência | ✅ Match |
| T6 | None | Phase 2 início | ✅ Match |
| T7 | T6 | T6→T7 | ✅ Match |
| T8 | T7 | T7→T8 | ✅ Match |
| T9 | T8 | T8→T9 [P] | ✅ Match |
| T10 | T8 | T8→T10 [P] | ✅ Match |
| T11 | T9, T10 | T9,T10→T11 | ✅ Match |
| T12 | T8 | Phase 3 depende de Phase 2 | ✅ Match |
| T13 | T12 | T12→T13 | ✅ Match |
| T14 | T13 | T13→T14 | ✅ Match |
| T15 | T14 | T14→T15 | ✅ Match |
| T16 | T15 | T15→T16 | ✅ Match |
| T17 | T16 | T16→T17 | ✅ Match |
| T18 | None (declarado sequencial na Phase 4) | Phase 4 início | ✅ Match |
| T19 | T18 | T18→T19 | ✅ Match |
| T20 | T19 | T19→T20 | ✅ Match |
| T21 | T20 | T20→T21 | ✅ Match |
| T22 | T21 | T21→T22 [P] | ✅ Match |
| T23 | T21 | T21→T23 [P] | ✅ Match |
| T24 | T22, T23 | T22,T23→T24 | ✅ Match |
| T25 | T17 | Phase 5 depende de Phase 3 | ✅ Match |
| T26 | T25 | T25→T26 | ✅ Match |
| T27 | T25 | T25→T27 | ✅ Match (T26 e T27 ambos dependem de T25, sequenciados no diagrama por conveniência de leitura, sem dependência entre si) |
| T28 | T27 | T27→T28 [P] | ✅ Match |
| T29 | T17 | Phase 5 depende de Phase 3 diretamente pra `DecisionContextCache` | ✅ Match (dependência real é T17, não T27 — T29 roda em paralelo com T28 mas sua dependência de fato é T17) |
| T30 | T28, T29 | T28,T29→T30 | ✅ Match |
| T31 | T17 | Phase 6 depende de Phase 3 | ✅ Match |
| T32 | T31 | T31→T32 | ✅ Match |
| T33 | T31, T32 | T32→T33 | ✅ Match |
| T34 | T5, T17, T24, T30, T33 | Phase 7 depende de todas anteriores | ✅ Match |
| T35 | T5 | T5→T35 (via Phase 1) | ✅ Match |
| T36 | T4 | T4→T36 (via Phase 1) | ✅ Match |
| T37 | T17, T24, T30, T33 | segue T34-T36 na sequência declarada | ✅ Match |
| T38 | T37 | T37→T38 | ✅ Match |
| T39 | T38 | T38→T39 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Domain/Simulation (`WorldEvent`, `WorldState`) | Unit | unit | ✅ OK |
| T2 | Simulation (`TickContext`) | Unit + Determinism | unit + determinism | ✅ OK |
| T3 | Infrastructure (`EventLogRecord`) | Integration | integration | ✅ OK |
| T4 | Simulation (`CausalProvenance`) | Unit + Determinism | unit + determinism | ✅ OK |
| T5 | Simulation (migração de call sites) | Unit + Determinism | unit + determinism | ✅ OK |
| T6 | Domain (`BodyRules`) | Unit | unit | ✅ OK |
| T7 | Domain/Simulation (`Npc`, geração) | Unit + Determinism | unit + determinism | ✅ OK |
| T8 | Simulation (`BodyMechanic`) | Unit + Determinism | unit + determinism | ✅ OK |
| T9 | Simulation (`ProductionSystem`) | Unit + Determinism | unit + determinism | ✅ OK |
| T10 | Simulation/Domain (`TravelResolution`) | Unit + Determinism | unit + determinism | ✅ OK |
| T11 | Simulation (`BodyMechanic` extend) | Unit + Determinism | unit + determinism | ✅ OK |
| T12 | Simulation (`DecisionContext` tipo) | none (matrix: tipo sem lógica) | none | ✅ OK |
| T13-T17 | Simulation (`DecisionContextBuilder`, `BehaviorDecisionSystem`) | Unit + Determinism | unit + determinism | ✅ OK |
| T18 | Domain (`PowerUtilityRules`) | Unit | unit | ✅ OK |
| T19-T20 | Simulation (`Extraordinary`) | Unit + Determinism | unit + determinism | ✅ OK |
| T21 | Domain/Simulation (`ActionType`, `Npc`, `ActionCatalog`) | Unit | unit | ✅ OK |
| T22-T24 | Simulation (`BehaviorDecisionSystem`, golden) | Unit + Determinism (+Scenario em T24) | unit + determinism (+scenario) | ✅ OK |
| T25 | Domain (`AttentionRules`, `Npc`) | Unit | unit | ✅ OK |
| T26-T30 | Simulation (`Behavior`) | Unit + Determinism | unit + determinism | ✅ OK |
| T31-T33 | Simulation (`Behavior`) | Unit + Determinism | unit + determinism | ✅ OK |
| T34 | Docs | none | none | ✅ OK |
| T35-T36 | Simulation (`History`, `WorldClock`) | Unit + Determinism | unit + determinism | ✅ OK |
| T37-T39 | Scenario | Scenario | scenario | ✅ OK |

Nenhuma violação — nenhuma task usa `Tests: none` fora do que a matrix permite (T12 documentação de tipo puro, T34 documentação).

---

## Tips

- **[P] = Order-free** dentro da mesma fase, nunca entre fases (fases são sempre sequenciais nesta feature)
- **7 fases** → ofertar sub-agent por fase ao entrar em Execute (protocolo do skill)
- **Golden hash muda em T7, T16 (não deveria!), T21, T24, T37-39** — T16 é refactor puro, NÃO deve mudar golden; se mudar, é regressão real, não schema change legítimo
- **Determinismo é mandatório por sistema novo** (`rules/tests.md`) — todo componente novo do design.md tem pelo menos 1 teste de determinismo dedicado
- **AD-011/012/013 já registrados em `STATE.md`** — Execute conforma a essas convenções, não precisa redecidir
