# Phase 15.1 Stage 4 Living World Validation

**Date**: 2026-08-22
**Spec**: `.specs/features/phase-15.1-stage-4-living-world/spec.md`
**Diff range**: uncommitted T1–T28 working tree (no Stage 4 commit; HEAD `72f6c3b`)
**Verifier**: independent sub-agent (author ≠ verifier) — re-verify iteration 2/3

---

## Task Completion

| Task | Status | Notes |
| ---- | ------ | ----- |
| T1–T6, T8–T28 | ✅ Done | Breakdown marks ✅; granularity/header still say ⏳ |
| T7 | ⚠️ Partial | LWV-05.4 period HUD/catalog refresh deferred (accepted this round); `hud.period` → `events` only |

No commits (authors forbidden). Parallel History/Llm/Narrative/Periods/Population/Simulation repairs excluded.

---

## Spec-Anchored Acceptance Criteria

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| ------------------------- | -------------------- | ----------------------- | ------ |
| LWV-01.1 CI classifies every system/event | exactly one classification + consumer or DiagnosticOnly | `CapabilityCoverageTests.cs:27,42` — `Assert.Empty(invalid)` | ✅ |
| LWV-01.2 missing consumer fails | living capability with 0 keys fails | `CapabilityCoverageTests.cs:75`; `FrontendCapabilityContractTests.cs:20` — `Assert.Equal(expected, actual)` | ✅ |
| LWV-01.3 diagnostic is not world fiction | only `ExampleCounterSystem`; no consumer | `CapabilityCoverageTests.cs:53,64` | ✅ |
| LWV-02.1 named action/reason/target/needs/job/skills | inspector fields from canonical state | `NpcLivingInspectorTests.cs:28-33`; `NpcInspector.test.tsx:55-62` | ✅ |
| LWV-02.2 life/work/relationship events update + timeline | audience-safe label, no payload | `LivingTimelineTests.cs:50-52` — `Assert.Equal("Um habitante faleceu", visual.Label)` | ✅ |
| LWV-02.3 commute to real workplace; no fake work | Travel then arrive; work only there | `PurposefulCommuteTests.cs:144-151,166,172-173` — Travel then `ActionType.Work` at workplace | ✅ |
| LWV-02.4 aggregates stay counts | no invented identity | `NpcInspector.test.tsx:95` — `não está materializado` | ✅ |
| LWV-03.1 rest place + quality + Zzz/a11y | ground < house < bed; location/duration | `RestQualityTests.cs:166-170`; `RestPresentationTests.cs:82-85`; `NpcInspector.test.tsx:129` | ✅ |
| LWV-03.2 edible only; wheat raw does not feed; UI names food | hunger 0, wheat stock 4; Cru vs Preparado | `CookingLifecycleTests.cs:98-99`; `FoodPresentationTests.cs:69-70`; `NpcInspector.test.tsx:156,171` | ✅ |
| LWV-03.3 plant→water→mature→harvest; no instant wheat | Growing not harvestable | `CropLifecycleTests.cs:62-64,76,91` | ✅ |
| LWV-03.4 water travel→collect→carry→deliver; conserved | carry 1 then stock 1; no remote | `WaterLogisticsTests.cs:84-86,118-128` | ✅ |
| LWV-03.5 cook chain + cues | kitchen; cook-food / eat-prepared | `CookingLifecycleTests.cs:137-139,191,217` | ✅ |
| LWV-04.1 demand→authoritative building | enqueue then complete | `AutonomousConstructionTests.cs:82-83,113-115` | ✅ |
| LWV-04.2 migrate/found; membership on arrival; conserved | city changes after arrival; distinct site | `LiveSettlementEvolutionTests.cs:118-121,136-138`; `InterCityMigrationVisibilityTests.cs:98-101,134` | ✅ |
| LWV-04.3 inspectors update without reload | construction % in HUD/inspector | `CityInspector.test.tsx:118` — `40%`; `CityView.test.tsx:238` — `25%` | ✅ |
| LWV-04.4 scaffold + progress before building | process before `world.Buildings` | `LivingScopeConstructionVisualTests.cs:59-62,87`; `constructionSite.test.ts:23-27` — `not.toBeNull()` then `progress === 0.4` | ✅ |
| LWV-04.5 completed building at API coord | `location` + `locationIsDerived` | `CityBuildingMarkerContractTests.cs:23-25,54-55`; `cityBuildingPlacement.test.ts:23,35-36`; `BuildingInspector.test.tsx:56,85` | ✅ |
| LWV-04.6 founding without 2nd city; timeline names it | 2 cities; label `Um novo assentamento foi fundado` | `SettlementFoundingVisibilityTests.cs:57-61,84`; `foundingVisibility.test.tsx:38,85` | ✅ |
| LWV-04.7 inter-city travel; membership after arrival | `RelocationDestination` then null | `InterCityMigrationVisibilityTests.cs:100-101,140-141`; `migrationRoute.test.ts:84-86` | ✅ |
| LWV-05.1 knowledge browsable, no truth leak | no truth payload | `LivingTimelineTests.cs:50-52,74` | ✅ |
| LWV-05.2 biography/chronicle + fallback | isolated facts; honest empty | `LivingInteractionSurfaceTests.cs:55-58,73-74` | ✅ |
| LWV-05.3 conversation; invalid does not mutate | hash unchanged | `LivingInteractionSurfaceTests.cs:118-119` | ✅ |
| LWV-05.4 period evolves → HUD/catalog refresh + transformation event | labels refresh; transformation shown | no `file:line` asserting current-period HUD/catalog refresh | ⚠️ Deferred (T7: no current-period in `WorldState`; not a FAIL this round) |
| LWV-06.1 ordered typed delta incl. process progress | progress on upserts | `RestPresentationTests.cs:118`; `LivingScopeConstructionVisualTests.cs:87`; `frontendCapabilityConsumers.test.ts:26` (`progress: 0.5`) | ✅ |
| LWV-06.2 replay equals fresh; dup idempotent; gap resnapshot | `Assert.Equal(after, replayed)` | `LivingDeltaContractTests.cs:74,91,105`; `RestPresentationTests.cs:137` | ✅ |
| LWV-06.3 scope cross same tick | remove + upsert tick 22 | `LivingDeltaContractTests.cs:144-147` | ✅ |
| LWV-07.1 data-driven cue; unknown never blank | unknown → `Atividade 77` / question; every ActionType has a cue | `npcAnimationCompleteness.test.ts:105-113`; Travel hidden + route cue: `npcAnimationCatalog.ts:83-85`; `ExistingActionVisualTests.cs:168-173`; `npcAnimationCompleteness.test.ts:77-84` | ⚠️ Accepted SPEC_DEVIATION (route is the Travel cue) |
| LWV-07.2 process progress drives staged cue | cook 0.25 vs 0.9 ring | `workCraftAnimations.test.ts:113,125`; `sustenanceRestAnimations.test.ts:152` | ✅ |
| LWV-07.3 life events at event location + timeline | burst at actor cell; audience-safe label | `LifecycleEventLabelContractTests.cs:47-48` — `Assert.Equal(cell, …Location)`; `lifecycleAnimations.test.ts:113-114` — `{x:8,y:5}` not first NPC `{2,2}`; `LivingScopeState.cs:188-189,226-230` | ✅ |
| LWV-07.4 reduced-motion: stop motion, keep cue | `cue.motion===false`, opacity 1 | `npcAnimationCompleteness.test.ts:119-121` | ✅ |
| LWV-07.5 CI maps every ActionType / Stage4 process / LWV-07 kind | exactly one animated spec; missing motor key fails | `ExistingActionVisualTests.cs:113-141,145-148` (`MotorStage4ProcessDescriptors` includes `construction`); `npcAnimationCompleteness.test.ts:20-34,87-93` (`REQUIRED_*` in the test file) | ✅ |

**Status**: ⚠️ Spec-precision / deferral flagged — no in-scope uncovered AC that blocks PASS

**Payload/conjunction**: `location`/`locationIsDerived` asserted on value (`CityBuildingMarkerContractTests`, placement, BuildingInspector honesty note). Process `progress` asserted (0 / 0.25 / 0.4 / 0.5). `relocationDestination` asserted equal to destination cell then null. Event `location` asserted equal to NPC cell and used by bursts. Animation spec fields (`keyframes`, `durationMs`, `a11yLabel`, `hidden`, `reducedMotionFallback`) asserted. Residual: `frontendCapabilityConsumers.test.ts:89` still omits `processes.size` (progress is covered on the upsert fixture and motor tests).

---

## Discrimination Sensor

Scratch copies in `%TEMP%\lw-stage4-sensor-20260822-r2`; working tree hashes restored (`construction` still at `npcAnimationCatalog.ts:31,103`; placement uses `building.location`; renderer has no `false &&` guard).

| Mutation | File:line | Description | Killed? |
| -------- | --------- | ----------- | ------- |
| 1 | `cityBuildingPlacement.ts:23` | API `location` → ring `(6,0)` | ✅ Killed (`cityBuildingPlacement.test.ts:23,35`) |
| 2 | `renderer.ts:254` | Skip construction scaffold (`if (false && …)`) | ✅ Killed (`renderer.test.ts:402`; `workCraftAnimations.test.ts:149`) |
| 3 | `npcAnimationCatalog.ts` | Removed `construction` from **both** `STAGE4_PROCESS_DESCRIPTORS` and `PROCESS_SPECS` | ✅ Killed by completeness alone: `ExistingActionVisualTests` (`Every_stage4_process_descriptor…`, `Process_specs_map_contains_construction…`); `npcAnimationCompleteness.test.ts:89` — `missing === ['construction']` |

**Sensor depth**: lightweight
**Result**: 3/3 killed — PASS ✅

---

## Interactive UAT Results

| # | Test | Result | Details |
| - | ---- | ------ | ------- |
| 1 | Map/inspector visual pass | ⏭️ Skip | User away; deferred |

---

## Code Quality

| Principle | Status |
| --------- | ------ |
| Minimum code | ✅ |
| Surgical changes | ✅ `BuildingInspector.tsx:36` gates the honesty note on `locationIsDerived !== false`; `BuildingInspector.test.tsx:56,85` |
| No scope creep | ✅ |
| Matches patterns | ✅ |
| Spec-anchored outcome check | ⚠️ 05.4 deferred; 07.1 documented Travel deviation |
| Per-layer Coverage Expectation | ✅ completeness lists live in C# motor arrays + Vitest `REQUIRED_*` in test files (sensor 3 killed). `capability-matrix.md` still has no CI parser (design inventory; catalog/consumer tests cover the spec CI rule) |
| Every test maps to a spec requirement | ✅ Stage4 / T18–T28 web tests map; unclaimed extras not in scope |
| Documented guidelines followed: `rules/tests.md`, `AGENTS.md` (no `verify.sh` / Scenario) | ✅ |

Residuals (not FAIL): `Date.now()` pulse in renderer (cosmetic); founding timeline synthesized from `City.FoundedFromCityId` (`LivingScopeState.cs:190-196`); `SettlementFounded` (20) stays out of LWV-07 burst family (T22); queued site uses process cell, completed building uses marker `location`.

---

## Edge Cases

- [x] Blocked/cancelled/dead: no teleport / no finish (`RestQualityTests.cs:186-187`; `ResourceProcessCatalogTests.cs` cancel/death; `WaterLogisticsTests.cs:84`)
- [x] Cues cosmetic: renderer does not mutate `process.progress` (`workCraftAnimations.test.ts:177-178`)
- [x] Unknown never blank (`npcAnimationCompleteness.test.ts:105-113`)
- [x] Reduced-motion keeps cue (`npcAnimationCompleteness.test.ts:119-121`)

---

## Gate Check

- **Build**: `bash scripts/build.sh` — **PASSED**. .NET Release 0 warnings; `web` `tsc -b` + vite build succeeded (prior TS18047 on `constructionSite.test.ts` narrowed via `expect(entity).not.toBeNull()` + `entity!`)
- **Feature**: `bash scripts/test.sh --filter "FullyQualifiedName~Stage4&Category!=Scenario"` — **142** .NET passed, **402** Vitest passed (script always runs full `web` suite), **0** failed, **0** skipped
- **`scripts/verify.sh`**: intentionally **not run**
- **Test count before feature**: not independently reconstructed (no Stage 4 commit)
- **After**: 23 Stage4 .NET classes; 142 Stage4 tests in this run (prior iteration recorded 139)
- **Skipped tests**: none
- **Failures**: none

---

## Fix Plans

None that block this iteration.

### Follow-up (not a FAIL): LWV-05.4 period HUD/transformation

- **Root cause**: no canonical current-period field in `WorldState`; `hud.period` aliases the events slice
- **Later task**: Project current period + transformation event; assert HUD/catalog labels refresh on evolution
- **Priority**: Major (accepted T7 deferral)

---

## Requirement Traceability Update

Recorded here only (spec.md not edited).

| Requirement | Previous Status | New Status |
| ----------- | --------------- | ---------- |
| LWV-01 | In Design | ✅ Verified |
| LWV-02 | In Design | ✅ Verified |
| LWV-03 | In Design | ✅ Verified |
| LWV-04 | In Design | ✅ Verified |
| LWV-05 | In Design | ⚠️ Verified except 05.4 deferred |
| LWV-06 | In Design | ✅ Verified |
| LWV-07 | In Design | ✅ Verified (Travel cue = documented deviation) |

---

## Lessons

Clean PASS (no surviving mutant, no blocking uncovered AC). `scripts/lessons.py` is **absent**; none recorded.

---

## Summary

**Overall**: ✅ Ready

**Spec-anchored check**: 29/31 ACs matched spec outcome | 2 flagged (05.4 deferred; 07.1 accepted Travel deviation)
**Sensor**: 3/3 mutations killed
**Gate**: Build passed; Feature 544 passed, 0 failed

**What works**: Living catalog/composition, embodied rest/food/water/crops, commute, construction/migration/founding visibility, typed deltas/replay, animation families, event-cell bursts, reduced-motion, unknown-action fallback, completeness gate independent of catalog list exports, BuildingInspector honesty via `locationIsDerived`.

**Issues found**: LWV-05.4 still deferred (allowed). Travel remains hidden with `SPEC_DEVIATION` + route assertions (allowed).

**Next steps**: Period HUD when `WorldState` exposes current period. Interactive UAT still deferred.
