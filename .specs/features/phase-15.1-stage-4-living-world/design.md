# Phase 15.1 — Stage 4: Living World Integration Design
**Spec**: `.specs/features/phase-15.1-stage-4-living-world/spec.md` — **Status**: Draft for approval
## Recommended Architecture
Adopt an engine-to-frontend observability contract over the existing projection/realtime path, plus
canonical multi-step activity processes. Polling hides transitions; raw domain events couple React to
internals. The motor owns decisions and progress; presentation adapters turn them into friendly cues.
```mermaid
flowchart LR
  C["Clock + activity processes"] --> W["World state + event log"]
  W --> P["Snapshots / typed deltas"] --> G["Realtime gateway"] --> S["React store"]
  S --> U["Map · inspectors · HUD · timeline · interactions"]
  O["Capability catalog"] -. coverage .-> C
  O -. consumer keys .-> U
```
## Coverage Contract
- `LivingWorldCapabilityCatalog` at the API/presentation boundary maps stable IDs to engine sources,
  channel, DTO/event kind, React consumer key, visual descriptor, and optional exclusion reason.
- `capability-matrix.md` is the human inventory; a test asserts it matches the runtime catalog.
- Reflection enumerates concrete `ISimulationSystem` and all `WorldEventKind` values; missing or
  duplicate classifications fail. Only non-world instrumentation may be `DiagnosticOnly`.
- React exports typed `frontendCapabilityConsumers`; representative contract fixtures must change
  visible state or enable the declared interaction. Merely registering a key does not pass.
## Canonical Activity and Process Model
`NpcActivityPlan` holds action, reason, target, destination, ordered route, step, progress, status, and
blocked reason. `MovementSystem` moves materialized actors through valid adjacent cells;
`ArrivalResolutionSystem` applies effects only after arrival. Ordered candidates and seeded RNG preserve replay.
```csharp
record RestPlaceRef(RestPlaceKind Kind, long TargetId, CellCoord Location, double RecoveryEfficiency);
record ResourceProcess(long Id, ProcessKind Kind, long ActorId, long TargetId,
  long StartedAtTick, long CompletesAtTick, ProcessStatus Status);
record CropBatch(long Id, int CropResourceId, CellCoord Plot, long PlantedAtTick,
  long MatureAtTick, long WaterRequired, long WaterDelivered);
```
- `RestPlaceCatalog` maps ground, dwelling, bed, and future furniture to scenario-authored recovery
  efficiency. Current `HomelessSleepEfficiency` migrates to the ground entry; no duplicate v2 contract.
- `ResourceCatalog` gains preparation/edibility metadata; `ProcessRecipe` declares inputs, output,
  workplace type, duration, and skill. Existing production recipes remain for genuinely atomic goods.
- Default wheat uses `Plant → Water → Grow → Harvest`; food requiring cooking uses `Collect inputs →
  Cook → Eat`. Default water uses `Travel → Collect → Carry → Deliver`; quantities stay conserved.
- Crop growth is scheduled by planted/maturity ticks, not scanned hourly. Water delivery and harvest
  are explicit actions; a missing source, container capacity, workplace, or route produces `Blocked`.
- Applying the new scenario schema in place is a public-contract change and remains pending approval.
## Runtime Composition
`ScenarioRunner.DefaultSystems()` remains the composition root and adds process scheduling/resolution
beside behavior, population, economy, skills, relationships, cities, history, narrative, and periods.
Demand can request buildings/workplaces; completed recipes provision them. Disabling each world-changing
group in paired same-seed scenarios must change the canonical outcome within the declared horizon.
## Projection and Realtime Contract
```typescript
interface ScopeTickDelta {
  tick: number; sequence: number
  npcUpserts: NpcVisual[]; npcRemoved: number[]
  cityUpserts: CityVisual[]; buildingUpserts: BuildingVisual[]
  processUpserts: ProcessVisual[]; indicators: IndicatorUpdate[]
  events: NotableVisualEvent[]
}
```
- `NpcVisual` carries map-critical action/plan/location; full inspection loads on selection.
- `ProcessVisual` supplies kind, target, progress, resource/item, and presentation descriptor key.
- `ActionVisualCatalog` is data-driven: e.g. sleeping `Zzz`, eating food, cooking steam/fire, crop
  growth/watering, bucket carrying. CSS/canvas animation never advances or decides canonical state.
- Every cue has text/ARIA equivalent and reduced-motion fallback. Unknown descriptors show a readable
  generic activity badge, never a raw enum or invisible action.
- Entity maps make duplicates idempotent; sequence gaps resnapshot. Buildings use authoritative locations.

## Frontend Surfaces
| Surface | Responsibilities |
| --- | --- |
| Map | Entities, buildings, routes, destinations, carried resource, action/process cues |
| NPC inspector | Needs, rest quality, consumed food, inventory task, family, job, skills, action |
| City/building | Housing, beds, stocks, sources, plots/crops, recipes, jobs, demand, construction |
| HUD | Tick/date/period, speed/status, aggregates/materialization |
| Timeline/knowledge | Life, economy, resource-chain, city, history, narrative, period events |

Knowledge exposes permitted beliefs, not global truth. Conversation keeps read-only snapshots and
schema-validated proposals; provider failure shows deterministic fallback and never stops ticks.

## Existing Code and Migration
| Existing component | Evolution |
| --- | --- |
| `BehaviorDecisionSystem`, `NeedsRules` | Generalize sleep/eat completion into target-aware processes |
| `ProductionRecipe`, stocks, conservation counters | Reuse for inputs/outputs; add staged recipe state |
| `MapPathfinder`, cell resources | Route to rest/work/source/plot and validate natural resources |
| Visual projectors, realtime gateway, simulation store | Add typed process/entity upserts and descriptors |
| NPC/narrative/conversation/period endpoints | Feed inspectors, knowledge, interaction, HUD |

## Focused Verification and Risks
| Requirement | Executable proof |
| --- | --- |
| LWV-01 | Injected unmapped system/event or missing consumer fails coverage |
| LWV-02 | Seeded commute plus lifecycle/family/skill state reaches declared consumers |
| LWV-03 | Same-seed ground/bed control; raw/prepared food; plant/mature/water/harvest/cook chains |
| LWV-04 | Per-tick construction, migration, founding, economy, population conservation |
| LWV-05 | Belief-safe history/narrative/period UI; rejected LLM action leaves hash unchanged |
| LWV-06 | Snapshot+deltas equals fresh projection; duplicate/gap/scope/process-progress cases |

Risks: process explosion is bounded to materialized actors and scheduled transitions; payload growth is
bounded by map-critical deltas and on-selection detail. Iteration uses only repository scripts with
narrow filters—never broad Scenario, `verify.sh`, or nightly. Project-pattern ADR follows approval.
