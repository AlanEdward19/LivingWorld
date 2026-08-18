# Phase 15.1 Live Polish Validation

**Date**: 2026-08-13
**Spec**: `.specs/features/phase-15.1-live-polish/spec.md`
**Diff range**: `1127c6bbe5049af2f862fbcadc3ee46f769c0d29..working-tree`
**Verifier**: independent sub-agent (author != verifier)
**Scope of this round**: LIVE-08..12 only

## Spec-Anchored Acceptance Criteria

| AC | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| LIVE-08 | Every authored settlement becomes one city at its coordinate; the first is the population home. | `web/tests/scenarioDefaults.test.ts:16` — `expect(scenario.Cities).toEqual([...all four settlements...])`; `:22` — `expect([scenario.VillageX, scenario.VillageY]).toEqual([1, 2])`; loader linkage at `tests/LivingWorld.Tests/Periods/ScenarioLoaderV2Tests.cs:85,87` — city count and every NPC joined to the village city. | PASS |
| LIVE-09 | A resident inside its city footprint is absent from both world snapshot and realtime delta. | `tests/LivingWorld.Tests/Visual/GlobalProjectorTests.cs:97` — `Assert.DoesNotContain(snapshot.ExternalNpcs, ...)`; `tests/LivingWorld.Tests/Simulation/TickLoopServiceTests.cs:115-116` — `Assert.Empty(delta.Moved)` and `Assert.Empty(delta.Removed)`. | PASS |
| LIVE-10 | Idle, Work, and Socialize each make one deterministic valid ambient step that remains inside the city footprint. | `tests/LivingWorld.Tests/Behavior/BehaviorDecisionSystemTests.cs:177-205` exercises all three actions; `:200` asserts destination differs from origin, `:201` asserts it remains in resolved bounds, and `:205` asserts equal-seed reproducibility. Map validity follows because resolved bounds are clipped to the map. | PASS |
| LIVE-11 | Blank worlds and built-in nonproductive presets do not starve residents due to missing food/water stock. | `web/tests/scenarioDefaults.test.ts:28-29` — economy disabled and no workplaces; `tests/LivingWorld.Tests/Periods/DefaultPeriodSeederTests.cs:25-26` — the same for every built-in template. | PASS |
| LIVE-12 | NPC token radius never exceeds 10 screen pixels at any zoom. | `web/tests/map-engine/renderer.test.ts:223-234` — extreme zoom asserts exact radius `10`; production cap at `web/src/map-engine/renderer.ts:531` is `Math.min(10, ...)`. | PASS |

**Status**: 5/5 ACs matched.

## Discrimination Sensor

| Mutation | File:line | Description | Result |
| --- | --- | --- | --- |
| 1 | `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs:79` | Scratch-only removal of `ActionType.Socialize` from ambient movement. | KILLED: Socialize theory row failed at `Assert.NotEqual` (`BehaviorDecisionSystemTests.cs:200`); 48/49 passed, 1 failed. |

**Sensor depth**: lightweight, one highest-risk behavior mutation.
**Scratch safety**: mutation ran only in `.verifier-scratch-live-polish`; scratch was deleted afterward and the real tree was never mutated.
**Result**: 1/1 killed — PASS.

## Gate Check

- **Author evidence**: `bash scripts/test.sh --filter "FullyQualifiedName~BehaviorDecisionSystemTests|FullyQualifiedName~GlobalProjectorTests|FullyQualifiedName~TickLoopServiceTests|FullyQualifiedName~DefaultPeriodSeederTests"` — 49 passed, 0 failed, 0 skipped; web 275 passed.
- **Build evidence**: `bash scripts/build.sh` passed before the final seeder/test-only adjustment.
- **Verifier sensor run**: same authorized .NET filter in scratch — 48 passed and the expected Socialize test failed, killing the mutant. The script stopped before web tests because the .NET failure was intentional.
- **Not run by instruction**: `verify.sh`, nightly, `Category=Scenario`, or any broad/long API suite.
- **Test-count baseline**: unavailable; no test deletion or weakened pre-existing assertion observed in the scoped diff.

## Code Quality and Edge Cases

- LIVE-08, LIVE-09, LIVE-11, and LIVE-12 changes are surgical and match existing boundaries.
- Seeded RNG and ordered ambient candidates comply with `rules/simulation-determinism.md`.
- LIVE-10's three ambient actions are protected for movement, city containment, and deterministic replay.
- No unrelated refactor, skipped test, or public-contract expansion was observed in this scoped round.

## Summary

**Overall**: PASS — ready for the scoped LIVE-08..12 round. All 5 ACs have exact assertion evidence and the highest-risk Socialize mutation was killed. No interactive UAT was needed for these automated outcomes.
