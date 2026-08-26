# Living World Cohesion — Integration Audit (Fase 16.3)

Fonte: COH-61 / doc#22–23. Status pós P1–P2 (causal events, Body, DecisionContext, Powers utility, Intent/Attention, Pressure/Opportunity/Trace).

## System Integration Matrix

| System | In DecisionContext | Causal consumer | Event provenance | Notes |
| --- | --- | --- | --- | --- |
| Events | n/a (carrier) | `CauseEventId` / `RootCauseEventId` | Pilots wired (Extraordinary, BookRediscovery, StillBirth) | ~54 `LogEvent` 2-arg call sites still default `SourceSystem=Unknown` (T5 backlog) |
| DecisionContext | self | `SelectByUtility` reads scoped DTO | DecisionTrace non-canonical | Built on wake; category cache (PERF-12 pattern) |
| Body | `BodySnapshot` | WorkCapacity + MovementCost | MuscleMass growth via labor | Height/Weight/MuscleMass generated seeded |
| Memory | `RelevantMemories` | Utility divergence proven | Fact/report path unchanged | Was PRESENTATION_ONLY → now CAUSAL in decision |
| Belief | `RelevantBeliefs` | Utility divergence proven | Unchanged | Same migration as Memory |
| Relationships | `KnownRelationships` | Utility divergence proven | RelationshipSystem untouched | 4 axes exposed as facts |
| Household | `HouseholdSnapshot` | Stock/members in utility | Deposit/Withdraw mark dirty | Economy+Household dirty flags |
| Powers | `PowerOpportunities` | `ActionType.UsePower` in utility | PowerInvoked + engine chain | Full mechanic registry scorável |
| Intent | `CurrentAction` + Intent fields | Persistence + retry before invalidate | PowerInvoked links decision→invoke | Active intent skips full reconsider |
| Attention | n/a (router) | Event-driven wake batch | Price/resource events route wakes | vs full-reconsideration metrics |

## Attribute Integration Matrix

| Attribute | Class | Evidence / next use |
| --- | --- | --- |
| Height | FUTURE_DEPENDENCY | BodySnapshot + generation; equipment/combat sizing deferred (16.4+) |
| Weight | FUTURE_DEPENDENCY | Same as Height — not yet equipment/combat consumer |
| MuscleMass | CAUSAL | Grows from heavy labor; feeds `WorkCapacityMultiplier` |
| WorkCapacityMultiplier | CAUSAL | ProductionSystem output |
| MovementCostMultiplier | CAUSAL | Travel time |
| Vitality | CAUSAL | Mortality / conception floors (pre-16.3) |
| Upbringing | PARTIALLY_INTEGRATED | Wealth/wage channel; not yet full DecisionContext pressure |
| Hunger/Thirst/Sleep/Social | CAUSAL | NeedsSnapshot + urgency wakes |
| Personality | CAUSAL | Utility weights |
| CurrentIntent / IntentStatus | CAUSAL | Persistence + AttentionRouter gate |
| Trust/Affection/Respect/Familiarity | CAUSAL | Via RelationshipFact in context |
| Power stage / reliability | CAUSAL | PowerOpportunity cost/risk |
| Memory / Belief payloads | CAUSAL | Recall into context |
| Household stock | CAUSAL | Economy slice of context |
| LOD aggregate pools | PRESENTATION_ONLY* | Zoom/materialization — not in DecisionContext (*by design) |

Legend: **CAUSAL** = real consumer in sim/decision · **PARTIALLY_INTEGRATED** = some consumers, gaps remain · **PRESENTATION_ONLY** = UI/read-model only · **FUTURE_DEPENDENCY** = stored/generated, consumer deferred · **UNUSED** = none.

## Backlog (not blocking closeout)

1. Migrate remaining ~54 `TickContext.LogEvent(kind, payload)` sites off `Unknown`.
2. Height/Weight → equipment/combat consumers when those systems land.
3. Event coalescing for write-many-per-tick noise (doc#82) — FUTURE_DEPENDENCY.
