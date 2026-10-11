# npc-building-occupancy verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 3254c6e..aaf950f
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

None marked binding in `plan.md` (Sources lists `godot-api-readiness/plan.md` and ADR-0018 as context only). Step 1 is `ui`-only and does not run under `light`.

## Checks

Proof run (one invocation, at HEAD `aaf950f`): `bash scripts/test.sh --all --filter "FullyQualifiedName~BuildingLinkTests|FullyQualifiedName~BuildingOccupancySystemTests|FullyQualifiedName~ScenarioLoaderV2Tests.Authored_workplace_links_to_its_building|FullyQualifiedName~ConstructionSystemTests.Completing_project_leaves_a_land_scarce_workplace_queued_and_completes_once_land_is_available|FullyQualifiedName~OptimizationEquivalenceTests.Default_scenario_ninety_days_keeps_the_recorded_hashes"` exit 0 - `Passed: 15, Failed: 0`. Count reconciled with `rg`: BuildingLinkTests 4 + BuildingOccupancySystemTests 7 + ScenarioLoaderV2 1 + ConstructionSystem 1 + OptimizationEquivalence 2 InlineData = 15; each named method exists exactly once (`rg -n` hits below). All proofs are in files touched by the diff.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | household Dwelling is its own type -1 house, same city, Location in footprint, unique | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingLinkTests.cs:25` - `Assert.Equal(-1, house.BuildingTypeId)`; `:29` `Assert.Contains(household.Location, footprint)`; `:31` `Assert.Equal(world.Households.Count, world.Households.Select(h => h.Dwelling).Distinct().Count())` | PASS |
| C2 | default workplaces link to building at their Location with matching type | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingLinkTests.cs:43` - `Assert.Equal(workplace.Location, building.Position)`; `:44` `Assert.Equal(workplace.LocationType.Id, building.BuildingTypeId)` | PASS |
| C3 | V2 authored workplace Building == the only building | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Periods/ScenarioLoaderV2Tests.cs:293` - `Assert.Equal(Assert.Single(world.Buildings).Id, Assert.Single(world.Workplaces).Building)` | PASS |
| C4 | construction-created workplace Building == built building | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/Construction/ConstructionSystemTests.cs:251` - `Assert.Equal(Assert.Single(freeWorld.Buildings).Id, workplace.Building)` | PASS |
| C5 | dangling Dwelling makes the sweep report an error | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingLinkTests.cs:52` - `Assert.Empty(ReferentialIntegritySweep.Check(world))`; `:56` `Assert.NotEmpty(ReferentialIntegritySweep.Check(world))` | PASS |
| C6 | snapshot round-trip keeps both links; missing fields load null | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingLinkTests.cs:67-68` - `Assert.Equal(world.Households.Select(h => h.Dwelling), restored.Households.Select(h => h.Dwelling))` (and Workplaces/Building); `:74-75` `Assert.All(old.Households, h => Assert.Null(h.Dwelling))`, `Assert.All(old.Workplaces, w => Assert.Null(w.Building))` | PASS |
| C7 | Sleep at household cell -> Interior.Building == Dwelling, floor in range | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingOccupancySystemTests.cs:36` - `Assert.Equal(dwelling, npc.Interior!.Building)`; `:38` `Assert.InRange(npc.Interior.Floor.Value, 0, floors - 1)` | PASS |
| C8 | Work at workplace cell -> Interior.Building == Workplace.Building, floor 0 | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingOccupancySystemTests.cs:64` - `Assert.Equal(workplace.Building, npc.Interior!.Building)`; `:65` `Assert.Equal(0, npc.Interior.Floor.Value)` | PASS |
| C9 | Sleep away from household cell -> Interior null | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingOccupancySystemTests.cs:50` - `Assert.Null(npc.Interior)` | PASS |
| C10 | Sleep -> Eat exits the building | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingOccupancySystemTests.cs:74` - `Assert.NotNull(npc.Interior)`; `:79` `Assert.Null(npc.Interior)` | PASS |
| C11 | Place pure, floor in range (0 when ground), walkable; 5 types x 20 ids x 30 NPCs | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingOccupancySystemTests.cs:94` - `Assert.Equal(first, BuildingOccupancySystem.Place(new NpcId(n), building, ground))`; `:95` `Assert.InRange(first.Floor.Value, 0, layout.Floors - 1)`; `:96` `if (ground) Assert.Equal(0, first.Floor.Value)`; `:98` `Assert.True(InteriorWalkability.IsWalkable(cell.Material))` (types `{-1,0,1,2,7}` at `:85`) | PASS |
| C12 | seed 77, 14 days, two runs -> same Interior map, non-empty | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingOccupancySystemTests.cs:116` - `Assert.NotEmpty(first)`; `:117` `Assert.Equal(first, Run())` | PASS |
| C13 | Dwelling to missing building keeps NPC outside | batch above, exit 0 | `tests/LivingWorld.Tests.Unit/Cities/BuildingOccupancySystemTests.cs:129` - `Assert.Null(npc.Interior)` (after `LiveIn(new BuildingId(9_999_999))` at `:125`) | PASS |

## Plan conformance (read at HEAD)

| Plan item | Evidence | Result |
| --- | --- | --- |
| Seeding sets `building:` | `src/LivingWorld.Simulation/Scenarios/ScenarioRunner.Seeding.cs:60` (farm), `:66` (forge) | yes |
| ScenarioLoaderV2 sets `building:` | `src/LivingWorld.Simulation/Scenarios/ScenarioLoaderV2.cs:212`; `:169` is the documented no-city legacy path without a building (out of AC 1.2 scope) | yes |
| ConstructionSystem sets `building:` | `src/LivingWorld.Simulation/Cities/Construction/ConstructionSystem.cs:113` | yes |
| Household i -> house i | `src/LivingWorld.Simulation/Population/PopulationSeeder.cs:108` `houseIds.Add(house.Id)`; `:43` `generated.Households[i].LiveIn(houseIds[i])`; alignment proven empirically by C1 footprint assertion | yes |
| Registered after BehaviorDecisionSystem, `[RunsAfter]` | `src/LivingWorld.Simulation/Scenarios/ScenarioRunner.Systems.cs:68-69`; `src/LivingWorld.Simulation/Cities/BuildingOccupancySystem.cs:19` `[RunsAfter(typeof(BehaviorDecisionSystem))]`, `:25` Hourly | yes |
| Hash re-recorded | `tests/golden/world-hashes.json` (4 entries) and `OptimizationEquivalenceTests.cs:36-37`; at HEAD both green (unit batch above; `bash scripts/test.sh --project longrunning --filter "FullyQualifiedName~GoldenHashesTests"` 4 passed, 1 skipped) | yes |

## Baseline re-recording analysis

- Scratch worktree (`git worktree add`, removed after; real-tree porcelain identical before/after): `OptimizationEquivalenceTests.Default_scenario_ninety_days_keeps_the_recorded_hashes` FAILS at `3254c6e`, `0a4f208`, `9704c95`, `a700309^`, `a700309` **and at `4a86dbf`, the commit that recorded those hashes**, with the same local actual canonical (`15e0dd39...` seed 1, `ef6947ff...` seed 2) at every one. GoldenHashesTests also fails at `3254c6e`.
- Conclusion: no code commit between `4a86dbf` and `3254c6e` changed the 90-day hash on this machine, so the pre-existing failure is not a regression absorbed from godot-api-readiness; it is a recorded-vs-local mismatch present since recording - most likely platform/environment dependence (this machine is darwin arm64; CI is `ubuntu-latest`, `.github/workflows/ci.yml:14`).
- Because `aaf950f` is the only commit in range, the HEAD delta vs the local base actual is attributable to this feature alone. What cannot be concluded: whether the new values (recorded on arm64) match on CI x64; if the original mismatch was platform-induced, CI will now fail these tests.

## Swept

No `Swept` row resolves to `existing` (rows map to C5/C13/C12/C6/C7/C8/C10 or `n/a`). The `concurrency n/a` rationale (runs after BehaviorDecisionSystem on the same thread) matches `ScenarioRunner.Systems.cs:68-69`.

## Level and sampling gaps (non-blocking)

1. Hash portability risk: new baselines recorded on arm64; originals already mismatched locally at their own recording commit -> likely CI (ubuntu x64) failure. Confirm on CI before merge.
2. C13 test named `Dead_npc_or_missing_building_stays_outside` exercises only the missing-building path; the `!npc.IsAlive` branch (`BuildingOccupancySystem.cs:48`) has no proof (no check claims it, but the name overstates).
3. AC 1.3 names `Workplace.Building` dangling too; only `Dwelling` is proven (C5). Covered by the same reflection path (`ReferentialIntegritySweep.cs:41`), but no assertion.
4. C1/C2/C5/C6 sample one seed (31, 40 NPCs); C2 does not assert exactly farm+forge are present (iterates all workplaces after `Assert.NotEmpty`).
5. C7 dwelling (type -1, 3x3) likely has a single floor, so the in-range assertion is trivially `[0,0]` there; multi-floor placement is covered only by C11 at the `Place` level.

## Gate

`bash scripts/verify.sh` - exit 0, `verify: OK`; Unit 1954 passed, 0 failed, 3 skipped. Proof batch: 15 passed, 0 failed. Golden (longrunning): 4 passed, 0 failed, 1 skipped.
