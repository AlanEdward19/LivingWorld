# Phase 15.1 — Stage 4: Living World Integration Tasks
**Design**: `.specs/features/phase-15.1-stage-4-living-world/design.md` — **Status**: Execute complete (T1–T28); verifier PASS 2026-08-22. LWV-05.4 deferred (T7). No commit until `verify.sh`.

## Execution Protocol (MANDATORY -- do not skip)
Execute with `tlc-spec-driven`; activate it by name and follow its Execute/Verifier flow. If unavailable,
STOP. Use only repository scripts. Never run `Category=Scenario` during tasks. Per `AGENTS.md`, do not
commit until the complete stage passes `scripts/verify.sh`; the user owns the later nightly run.

## Test Coverage Matrix
> Generated from `AGENTS.md`, `rules/tests.md`, `rules/eval-criteria.md`, `scripts/test.sh`, sampled xUnit/API/Vitest tests, and the spec. Confirm before Execute.
| Code layer | Required test | Coverage expectation | Location | Run command |
| --- | --- | --- | --- | --- |
| Domain/simulation | unit + determinism | Every AC/branch/edge; disable-new-system changes hash | `tests/LivingWorld.Tests/Stage4/**` | Q.NET |
| API/projection/realtime | integration | Snapshot, delta, scope, replay, error/ordering cases | same | Q.NET |
| React store/components/canvas | Vitest + Testing Library | Every consumer, visible/a11y fallback, duplicate/gap cases | `web/tests/**` | Q.NET (also runs fast Vitest) |
| Scenario schema/catalog | unit + loader contract | Valid, missing, invalid, in-place migration | `tests/LivingWorld.Tests/Stage4/**` | Q.NET |

## Parallelism Assessment
> Generated from codebase — confirm before Execute.
| Test type | Parallel-safe? | Isolation model | Evidence |
| --- | --- | --- | --- |
| Pure xUnit domain | Yes | New `WorldState` and seeded RNG per test | behavior/economy test samples |
| API/tick/realtime integration | No | Mutable singleton host/factory | `TickLoopServiceTests.cs` |
| Vitest component/store | Yes | Per-test stores, mocks, jsdom cleanup | `web/tests/**` |
| Focused vertical scenario | No | Mutable world and ordered clock | `rules/tests.md` |

## Gate Check Commands
> Generated from codebase — confirm before Execute.
| Gate | When | Command |
| --- | --- | --- |
| Q.NET | Every task; replace class exactly | `bash scripts/test.sh --filter "FullyQualifiedName~<Class>&Category!=Scenario"` |
| Feature | End of each execution phase | `bash scripts/test.sh --filter "FullyQualifiedName~Stage4&Category!=Scenario"` |
| Build | End of each execution phase | `bash scripts/build.sh` |
| Final | Only after all tasks | inspect tags, then `bash scripts/verify.sh`; user runs nightly/`Category=Scenario` |

## Execution Plan
```text
Phase 1 — Existing engine contract: T1 → T2 → T3 → T4
Phase 2 — Existing behavior becomes visible: T5 → T6 → T7 → T8 → T9 → T10 → T11
Phase 3 — Missing capabilities, deliberately last: T12 → T13 → T14 → T15 → T16 → T17
Phase 4 — Map visibility gaps (motor OK, client missing): T18 → T19 → T20 → T21 → T22
Phase 5 — NPC animations (2D cues, data-driven): T23 → T24 → T25 → T26 → T27 → T28
```
No task is marked `[P]`: shared contracts and the requested integration order are safer sequentially.

## Task Breakdown
| Task | Atomic deliverable / location | Depends | Reuses | Req | Done when / tests / gate | Tools |
| --- | --- | --- | --- | --- | --- | --- |
| T1 ✅ | Capability catalog + completeness gate in API/tests | None | `ISimulationSystem`, `WorldEventKind`, matrix | LWV-01 | Missing/duplicate/false diagnostic fails; **6 new tests passed**; Q.NET `CapabilityCoverageTests` | `apply_patch`; `tlc-spec-driven` |
| T2 ✅ | Production composition registers every existing living system in deterministic order | T1 | `ScenarioRunner.DefaultSystems` | LWV-01/02/04/05 | API clock inventory and disable-group causal proof; **5 new tests passed**; Q.NET `ProductionCompositionTests` | same |
| T3 ✅ | Typed snapshot/delta contract for current NPC/city/building/indicator/event state | T2 | visual projectors, gateway | LWV-06 | Replay, duplicate, gap, scope crossing; **8 new tests passed**; Q.NET `LivingDeltaContractTests` | same |
| T4 ✅ | React consumer registry and normalized delta application | T3 | `simulationStore.ts` | LWV-01/06 | Every catalog consumer resolves and mutates view state; **5 .NET + 5 web tests passed**; Q.NET `FrontendCapabilityContractTests` | `apply_patch`; `react-best-practices` |
| T5 ✅ | NPC inspector consumes identity, family, needs, health, job, skills, action, target, and LOD | T4 | `NpcInspectionQuery`, materialize endpoint | LWV-02 | Selection refreshes relevant deltas; aggregates stay counts; **6 .NET + 7 web tests passed**; Q.NET `NpcLivingInspectorTests` | same |
| T6 ✅ | Audience-safe event timeline and belief/knowledge view | T5 | event log/history queries | LWV-02/05 | All mapped events render labels; truth leak fixture fails; **≥5 tests**; Q.NET `LivingTimelineTests` | same |
| T7 ✅ | Chronicle, biography, conversation, and period surfaces in the selected context | T6 | existing endpoints/fallbacks | LWV-05 | All endpoints used; invalid proposal hash unchanged; **≥6 tests**; Q.NET `LivingInteractionSurfaceTests` — **5 .NET + 7 web tests passed**. Period HUD refresh (LWV-05.4) deferred to a later period-HUD task: no canonical current-period field in `WorldState`; do not invent period state here | same |
| T8 ✅ | Data-driven visual cues for existing actions, including accessible animated sleep `Zzz` | T7 | map renderer, current action | LWV-02 | Known actions render cue; unknown/reduced-motion/text fallbacks; **≥5 tests**; Q.NET `ExistingActionVisualTests` — **6 .NET + 7 new web tests passed** | `apply_patch`; `frontend-design` |
| T9 ✅ | Canonical purposeful commute for work/home with blocked state | T8 | pathfinder, behavior, workplace | LWV-02/06 | Adjacent route, work after arrival, return, no teleport/death effect; **6 new tests passed**; Q.NET `PurposefulCommuteTests` — blocked state fix regenerated `tests/baselines/action-switches.json` (deterministic ripple, same class as AD-005) | `apply_patch`; `coding-guidelines` |
| T10 ✅ | Demand-driven construction creates authoritative building and workplace | T9 | city demand, recipes, construction | LWV-04 | Demand→queue→completion; no capacity means no fake work; **≥5 tests**; Q.NET `AutonomousConstructionTests` — **6 tests passed** | same |
| T11 ✅ | Household migration/founding travel plus live authoritative map integration | T10 | migration, founding, deltas/store | LWV-04/06 | Arrival-only membership, distinct seeded city, conservation and replay equality; **≥7 tests**; Q.NET `LiveSettlementEvolutionTests` — **7 tests passed** | same |
| T12 ✅ | Rest-place catalog and target-aware rest, replacing homeless-only efficiency in place | T11 | sleep action, household/buildings | LWV-03 | Ground/house/bed control proves quality direction and unreachable blocking; **9 tests passed**; Q.NET `RestQualityTests` | same |
| T13 ✅ | Rest quality/progress reaches inspector and map cue | T12 | process visual, `Zzz` cue | LWV-03/06 | Location, quality, remaining duration, a11y and replay visible; **6 .NET + 2 web tests passed**; Q.NET `RestPresentationTests` | `apply_patch`; `frontend-design` |
| T14 ✅ | Resource/process catalogs: preparation, edibility, staged inputs/outputs, scheduler | T13 | stocks, recipes, conservation | LWV-03 | Schema valid/invalid; raw wheat rejected; completion/cancel/death conserved; **9 tests passed**; Q.NET `ResourceProcessCatalogTests` | `apply_patch`; `coding-guidelines` |
| T15 ✅ | Water chain `travel→collect→carry→deliver` with visual progress | T14 | cell water/resources, routes, stocks | LWV-03/06 | No remote use; missing source/route blocks; quantity conserved; **7 tests passed**; Q.NET `WaterLogisticsTests` | same |
| T16 ✅ | Food chain `collect inputs→cook→eat` creates and consumes an edible prepared output | T15 | process core, water, stocks | LWV-03/06 | Raw/prepared control, valid cooking place, conservation, cues/replay; **7 tests passed**; Q.NET `CookingLifecycleTests` | same |
| T17 ✅ | Crop chain `plant→water→mature→harvest` replaces instant default wheat | T16 | process core, water, production | LWV-03/06 | No instant/early harvest; maturity/water/worker controls, cues/replay; **8 tests passed**; Q.NET `CropLifecycleTests` | same |
| T18 ✅ | City map places completed buildings at authoritative API coordinates | T17 | `CityProjector`, `BuildingPlacementResolver`, `CityView` | LWV-04 | Web `CityBuildingMarker` exposes `location` + `locationIsDerived`; `CityView` uses motor coords instead of ring-only layout; **3 .NET + 5 placement + CityView click tests passed**; Q.NET `CityBuildingMarkerContractTests` | `apply_patch`; `frontend-design` |
| T19 ✅ | Construction-in-progress visible on city map (scaffold/site + progress) | T18 | `livingState.processes`, `ConstructionSystem` | LWV-04 | In-queue projects render before completion; inspector/city HUD shows progress; **4 .NET + renderer/HUD Vitest passed**; Q.NET `LivingScopeConstructionVisualTests` | same |
| T20 ✅ | NPC city-map tokens render pawn + action/process overlays (no blank tile) | T19 | `MapView`, `renderer`, `livingState` | LWV-02/06 | Pawn visible at city LOD; work/rest/food/water/crop cues overlay NPC location; **3 overlay + 4 renderer Vitest passed** | same |
| T21 ✅ | Inter-city migration routes visible on world map (travel → arrival) | T20 | `MigrationSystem`, `RelocationArrivalSystem`, world map | LWV-04 | When `Cities.Count ≥ 2`, migrating households show route/travel and new city membership after arrival; **3 .NET + 4 web tests passed**; Q.NET `InterCityMigrationVisibilityTests` | same |
| T22 ✅ | Settlement founding visible on world map (new city marker + timeline) | T21 | `SettlementFoundingSystem`, `FoundingSitePicker`, `map.founding` | LWV-04 | Founding does **not** require a 2nd city beforehand — motor can spawn one from the mother city; client shows new `cityUpsert`, distinct site, pool transfer legible in timeline/inspector; no UI guard hides it; **3 .NET + 4 web tests passed**; Q.NET `SettlementFoundingVisibilityTests` | same |
| T23 ✅ | `NpcAnimationCatalog` — unified data-driven animation contract | T22 | `actionVisuals.ts`, `renderer.ts`, `global.css` | LWV-07 | Single catalog maps `ActionType`, `ProcessVisual.descriptorKey`, and lifecycle `WorldEventKind` → animation spec (keyframes, duration, a11y label, reduced-motion static fallback); **7 Vitest** contract tests | `frontend-design` |
| T24 ✅ | Work & craft animations (trabalhar, cozinhar, construir) | T23 | work/cook/construction processes | LWV-07 | Animated cues for `Work`, `cook-food`, `construction`, `collect-water`/`carry-water`/`deliver-water`; progress-aware where `ProcessVisual.progress` exists; **≥6 Vitest** renderer tests | same |
| T25 ✅ | Social & romance animations (encontro, amar, casamento) | T24 | socialize, courtship, marriage events | LWV-07 | Animated cues for `Socialize`, `CourtshipStarted`/`Succeeded`/`Rejected`, `Marriage`; two-NPC link when both materialized at same tick; **≥6 Vitest** | same |
| T26 ✅ | Life-cycle animations (nascer, morrer, parto) | T25 | `WorldEventKind` birth/death family | LWV-07 | Timed bursts for `Birth`, `Death`, `Starvation`, `MaternalDeath`, `StillBirth` at event location; audience-safe (sem gore); timeline + map; **≥6 Vitest** + .NET label contract | same |
| T27 ✅ | Sustenance & rest animations (comer, beber, dormir) | T26 | eat/sleep/drink processes | LWV-07 | Extends sleep `Zzz` pattern to all rest kinds + `eat-raw`/`eat-prepared` + water carry/drink; inspector parity; **≥5 Vitest** | same |
| T28 ✅ | Animation completeness gate (nenhuma ação/evento órfão) | T27 | coverage tests pattern | LWV-07 | CI fails if any `ActionType`, Stage4 process descriptor, or LWV-07 event lacks animation; `prefers-reduced-motion` stops motion but never hides cue; **≥4 tests** (extend `ExistingActionVisualTests` pattern) | same |

## Parallel Execution Map
`T1→…→T22→T23→T24→T25→T26→T27→T28`

## Task Granularity Check
| Tasks | Atomic unit | Status |
| --- | --- | --- |
| T1–T17 | One contract, surface, or user-visible causal behavior per task | ✅ |
| T18–T22 | One map-visibility gap per task | ✅ |
| T23–T28 | One animation family per task + final coverage gate | ✅ |

## Diagram-Definition Cross-Check
| Task | Depends on | Diagram shows | Status |
| --- | --- | --- | --- |
| T1 | None | start | ✅ |
| T2/T3/T4/T5/T6/T7/T8 | immediately previous task | matching arrow for each | ✅ |
| T9/T10/T11/T12/T13/T14/T15/T16/T17 | immediately previous task | matching arrow for each | ✅ |
| T18/T19/T20/T21/T22 | immediately previous task | matching arrow for each | ✅ |
| T23/T24/T25/T26/T27/T28 | immediately previous task | matching arrow for each | ✅ |

## Known gaps (user report 2026-08-22)
- **Construction/buildings invisible**: motor projects `construction` processes and `CityBuildingMarker`
  with `Location`, but web `CityBuildingMarker` omits coordinates and `CityView` still uses client-side
  ring layout; `MapView`/`renderer` never draw `livingState.processes`. → T18–T19.
- **NPC “tile em branco”**: `entitiesOf` supplies position/action but renderer overlays/process cues are
  not wired on the city map at detail LOD. → T20.
- **NPCs não saem da vila**: dois mecanismos distintos no motor:
  - **Fundação** (`SettlementFoundingSystem`): **não precisa de 2ª cidade** — com limiar de
    concentração atingido (default 0.1, cenários `medieval.json`), agenda `organizationTicks` e cria
    cidade nova em sítio distinto (`FoundingSitePicker`), transferindo o **pool agregado** inteiro.
    T11 prova conservação; o front hoje não mostra o novo assentamento → **T22**.
  - **Migração** (`MigrationSystem`): precisa de **`Cities.Count ≥ 2`**; domicílios materializados
    viajam (`Travel`) e só mudam cidade na chegada (`RelocationArrivalSystem`). Commute intra-cidade
    não é emigração. → **T21**.
  - **Nuance motor (não é bloqueio de front)**: fundação hoje move o pool agregado no evento
    agendado — não há expedição materializada de NPCs a pé. Caravana física seria evolução futura
    do motor, não pré-requisito para o cliente mostrar a cidade fundada.
- **Animações de NPC**: hoje só `Sleep` tem animação (`Zzz` + badge pulse); `Work`, `Socialize`,
  `Eat`, processos (`cook-food`, etc.) e eventos de vida (`Birth`, `Death`, `Marriage`, …) usam
  ícone estático ou só timeline textual. → **T23–T28** (catálogo unificado + famílias + gate).

## Test Co-location Validation
| Tasks | Layer | Matrix requires | Task says | Status |
| --- | --- | --- | --- | --- |
| T1–T3, T9–T12, T14–T17 | Domain/API/integration | unit + integration + determinism as applicable | named focused class and minimum count | ✅ |
| T4–T8, T13, T23–T28 | API + React | integration + Vitest/Testing Library | contract class plus fast web suite | ✅/⏳ |

## Approval Before Execute
Confirm task order and tools. Default tools are `apply_patch`, repository scripts, `tlc-spec-driven`,
`coding-guidelines`, `react-best-practices`, and `frontend-design`; no MCP or sub-agent is required.
