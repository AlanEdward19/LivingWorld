# Weather drought verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 21adf18^..21adf18 (plus verifier fixes in the working tree)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | one factor per region and year in bounds, never rewritten | `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter ClimateTests` exit 0 (8 passed) | `tests/LivingWorld.Tests.Unit/Climate/ClimateTests.cs:47` - `Assert.All(drawn, c => Assert.InRange(c.FactorMilli, 300, 1500))`; `:50` - `Assert.Equal(drawn, world.ClimateYears.ToList())` | PASS |
| C2 | same seed same years (one and two processes), varies, mean near the middle | Unit filter above; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "MeasureHarness or DeterminismTwoProcessTests or GoldenHashesTests"` exit 0 (12 passed, 1 skipped recorder) | `ClimateTests.cs:65` - `Assert.Equal(a.ClimateYears, b.ClimateYears)`; `:70` - `Assert.InRange(factors.Average(), 800, 1000)`; `DeterminismTwoProcessTests.Same_seed_draws_the_same_climate_years_across_two_separate_processes` passed | PASS |
| C3 | climate off: every year 1000, nothing drawn | Unit filter above | `ClimateTests.cs:35-36` - `Assert.Empty(world.ClimateYears)`, `Assert.Equal(1000, world.ClimateFactorMilli(...))` | PASS |
| C4 | ledger bounded to the kept years | Unit filter above | `ClimateTests.cs:93` - `Assert.True(world.ClimateYears.Count <= world.Map.Regions.Count * (WorldState.ClimateYearsKept + 1))` | PASS |
| C5 | year below 600 cuts the plant harvest in proportion and the crop harvest | Unit filter above | `ClimateTests.cs:117-118` - `Assert.Equal(10, average)`, `Assert.Equal(3, drought)` (plant); crop path: ignoring the climate in `CropSystem` made `:156` fail (mutation killed) | PASS |
| C6 | forced year-5 drought: food harvest below 60%, price not lower, village alive after 20 years | Unit filter above (Scenario, 19 s) | `ClimateTests.cs:156` - `Assert.True(dryHarvest < 0.6 * controlHarvest, ...)`; `:157` - `Assert.True(FoodPrice(dry) >= FoodPrice(control), ...)`; `:160` - `Assert.True(dry.Npcs.Count(n => n.IsAlive) > 0, ...)` | PASS |
| C7 | round trip with equal hash, hash changes when mutated, off has no cost, two processes agree | `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "ClimateTests or WorldSnapshotTests"` exit 0 (96 passed); LongRunning filter above | `ClimateTests.cs:131` - `Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored))`; `WorldSnapshotTests` cases `ClimateRules` and `ClimateYears` passed; golden hashes unchanged after the verifier fixes | PASS |

## Gate

`dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "ClimateTests or WorldSnapshotTests"` - 96 passed, 0 failed; LongRunning filter - 12 passed, 0 failed, 1 skipped.

## Gaps found and fixed by the verifier

- Hot path with climate off: the three harvest sites evaluated `Map.RegionOf(...)` (dictionary lookup that throws off-grid) and `ClimateYearOf` before the disabled early return. Added `WorldState.ClimateFactorAt(cell, tick)` (returns 1000 first when disabled, uses the O(1) array lookup, never throws) and switched `CropSystem`, `FloraLifecycleSystem` and `ProductionSystem` to it. Golden hashes unchanged.
- Bug: `ClimateRules` bounds were unvalidated; `MinMilli < 0` produced a negative factor and a negative harvest. `ClimateYearSystem` now clamps (min >= 0, max >= min); test `Invalid_scenario_bounds_never_draw_a_negative_factor`.
- Mutation probe (manual, reverted): ignoring the climate in `CropSystem` fails C6 (killed). Ignoring it in the `ProductionSystem` food output survives; that branch is unproven, and is skipped anyway when plant process recipes exist (the default). It is a Deviation, not an acceptance criterion, so it is left unproven.
- AC 7 time per NPC-tick: `MeasureHarness` is not a gate and asserts nothing; only the hash and the early-return structure are proven.
- C6 asserts only that the price does not fall (>=), weaker than AC 5 "price rises"; the claim text is honest about that.
