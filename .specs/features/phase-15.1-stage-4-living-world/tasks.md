# Phase 15.1 — Stage 4: Living World Integration Tasks
**Design**: `.specs/features/phase-15.1-stage-4-living-world/design.md` — **Status**: In Progress

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
| T7 ✅ | Chronicle, biography, conversation, and period surfaces in the selected context | T6 | existing endpoints/fallbacks | LWV-05 | All endpoints used; invalid proposal hash unchanged; **≥6 tests**; Q.NET `LivingInteractionSurfaceTests` — **5 .NET + 7 web tests passed**. Period HUD refresh (LWV-05.4) deferred: no canonical "current period" state exists yet in `WorldState` (see spec.md note) | same |
| T8 ✅ | Data-driven visual cues for existing actions, including accessible animated sleep `Zzz` | T7 | map renderer, current action | LWV-02 | Known actions render cue; unknown/reduced-motion/text fallbacks; **≥5 tests**; Q.NET `ExistingActionVisualTests` — **6 .NET + 7 new web tests passed** | `apply_patch`; `frontend-design` |
| T9 ✅ | Canonical purposeful commute for work/home with blocked state | T8 | pathfinder, behavior, workplace | LWV-02/06 | Adjacent route, work after arrival, return, no teleport/death effect; **6 new tests passed**; Q.NET `PurposefulCommuteTests` — blocked state fix regenerated `tests/baselines/action-switches.json` (deterministic ripple, same class as AD-005) | `apply_patch`; `coding-guidelines` |
| T10 | Demand-driven construction creates authoritative building and workplace | T9 | city demand, recipes, construction | LWV-04 | Demand→queue→completion; no capacity means no fake work; **≥5 tests**; Q.NET `AutonomousConstructionTests` | same |
| T11 | Household migration/founding travel plus live authoritative map integration | T10 | migration, founding, deltas/store | LWV-04/06 | Arrival-only membership, distinct seeded city, conservation and replay equality; **≥7 tests**; Q.NET `LiveSettlementEvolutionTests` | same |
| T12 | Rest-place catalog and target-aware rest, replacing homeless-only efficiency in place | T11 | sleep action, household/buildings | LWV-03 | Ground/house/bed control proves quality direction and unreachable blocking; **≥6 tests**; Q.NET `RestQualityTests` | same |
| T13 | Rest quality/progress reaches inspector and map cue | T12 | process visual, `Zzz` cue | LWV-03/06 | Location, quality, remaining duration, a11y and replay visible; **≥5 tests**; Q.NET `RestPresentationTests` | `apply_patch`; `frontend-design` |
| T14 | Resource/process catalogs: preparation, edibility, staged inputs/outputs, scheduler | T13 | stocks, recipes, conservation | LWV-03 | Schema valid/invalid; raw wheat rejected; completion/cancel/death conserved; **≥8 tests**; Q.NET `ResourceProcessCatalogTests` | `apply_patch`; `coding-guidelines` |
| T15 | Water chain `travel→collect→carry→deliver` with visual progress | T14 | cell water/resources, routes, stocks | LWV-03/06 | No remote use; missing source/route blocks; quantity conserved; **≥7 tests**; Q.NET `WaterLogisticsTests` | same |
| T16 | Food chain `collect inputs→cook→eat` creates and consumes an edible prepared output | T15 | process core, water, stocks | LWV-03/06 | Raw/prepared control, valid cooking place, conservation, cues/replay; **≥7 tests**; Q.NET `CookingLifecycleTests` | same |
| T17 | Crop chain `plant→water→mature→harvest` replaces instant default wheat | T16 | process core, water, production | LWV-03/06 | No instant/early harvest; maturity/water/worker controls, cues/replay; **≥8 tests**; Q.NET `CropLifecycleTests` | same |

## Parallel Execution Map
`T1→T2→T3→T4→T5→T6→T7→T8→T9→T10→T11→T12→T13→T14→T15→T16→T17`

## Task Granularity Check
| Tasks | Atomic unit | Status |
| --- | --- | --- |
| T1–T17 | One contract, surface, or user-visible causal behavior per task; tests co-located | ✅ Granular |

## Diagram-Definition Cross-Check
| Task | Depends on | Diagram shows | Status |
| --- | --- | --- | --- |
| T1 | None | start | ✅ |
| T2/T3/T4/T5/T6/T7/T8 | immediately previous task | matching arrow for each | ✅ |
| T9/T10/T11/T12/T13/T14/T15/T16/T17 | immediately previous task | matching arrow for each | ✅ |

## Test Co-location Validation
| Tasks | Layer | Matrix requires | Task says | Status |
| --- | --- | --- | --- | --- |
| T1–T3, T9–T12, T14–T17 | Domain/API/integration | unit + integration + determinism as applicable | named focused class and minimum count | ✅ |
| T4–T8, T13 | API + React | integration + Vitest/Testing Library | contract class plus fast web suite | ✅ |

## Approval Before Execute
Confirm task order and tools. Default tools are `apply_patch`, repository scripts, `tlc-spec-driven`,
`coding-guidelines`, `react-best-practices`, and `frontend-design`; no MCP or sub-agent is required.
