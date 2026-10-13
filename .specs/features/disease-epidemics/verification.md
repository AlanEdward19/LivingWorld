# Disease epidemics verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 1611ce7^..1611ce7 (plus verifier fixes in the working tree)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | same-cell spread, not other cell or immune, contagion 0 never spreads | `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter DiseaseTests` exit 0 (12 passed) | `tests/LivingWorld.Tests.Unit/Health/DiseaseTests.cs:109-111` - `Assert.True(world.IsInfected(neighbour.Id, 1))`, `Assert.False(world.IsInfected(distant.Id, 1))`; `:124` - `Assert.False(world.IsInfected(neighbour.Id, 1))` | PASS |
| C2 | same epidemic in one and across two processes | Unit filter above; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "MeasureHarness or DeterminismTwoProcessTests or GoldenHashesTests"` exit 0 (12 passed, 1 skipped recorder) | `DiseaseTests.cs:140` - `Assert.Equal(Run(), Run())`; `tests/LivingWorld.Tests.LongRunning/DeterminismTwoProcessTests.cs` test `Same_seed_writes_the_same_epidemic_across_two_separate_processes` - `Assert.Equal(a, b)` and deaths > 0 | PASS |
| C3 | immunity blocks reinfection then expires; lethality 1 kills with a Disease event | Unit filter above | `DiseaseTests.cs:152,157,161` - `Assert.True(world.IsImmune(sick.Id, 1))`, `Assert.False(world.IsInfected(sick.Id, 1))`, `Assert.DoesNotContain(world.Immunities, ...)`; `:174` - `Assert.Contains(sink.Events, e => e.Kind == WorldEventKind.Disease && ...)` | PASS |
| C4 | outbreak only over threshold with one event; default scenario has no disease; disabled system changes nothing | Unit filter above | `DiseaseTests.cs:183,190` - `Assert.DoesNotContain(... OutbreakStarted)`, `Assert.Single(sink.Events, e => e.Kind == WorldEventKind.OutbreakStarted)`; `:64,68` - `Assert.Single(world.Infections)`, `Assert.False(defaultWorld.DiseaseRules.Enabled)` | PASS |
| C5 | mortality of a finished outbreak within 0.5x-1.5x of 0.2 | Unit filter above | `DiseaseTests.cs:206` - `Assert.InRange(mortality, 0.2 * 0.5, 0.2 * 1.5)`; `:207` - `Assert.Empty(world.Infections)` | PASS |
| C6 | village with moderate disease keeps at least 70% of control in 8 seeds | Unit filter above (Scenario, 36 s) | `DiseaseTests.cs:247` - `Assert.True(sick >= 0.7 * control, ...)`; `:246` (added) `Assert.True(outbreaks > 0, ...)` | PASS |
| C7 | infections, immunities, rules survive a round trip with equal hash; each canonical list changes the hash | `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "DiseaseTests or WorldSnapshotTests"` exit 0 (98 passed) | `DiseaseTests.cs:224` - `Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored))`; `WorldSnapshotTests` theory cases `Infections`, `Immunities`, `DiseaseRules` each passed | PASS |
| C8 | alloc and time unchanged with disease off; two processes agree | LongRunning filter above | `DiseaseTests.cs:82` (added) - `Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before)` over 1000 disabled ticks with 1000 NPCs; `DeterminismTwoProcessTests.Same_seed_produces_identical_hashes_across_two_separate_processes` passed; microseconds ratio is measured only (see gaps) | PASS |

## Gate

`dotnet test tests/LivingWorld.Tests.Unit -c Release --filter DiseaseTests` - 12 passed, 0 failed; LongRunning filter - 12 passed, 0 failed, 1 skipped.

## Gaps found and fixed by the verifier

- C8 and AC 6: `MeasureHarness` is documented "not a gate" and asserts nothing, so the 103% and 102% claims are measured, never asserted. Added a zero-allocation proof for the disabled system; the microseconds ratio is still not proven by any test.
- C4: the author's test ended in `Assert.NotNull(before)` (vacuous) and used a bare `NewWorld()` as the "default scenario". It now asserts no new immunities and that `ScenarioRunner.Create(1)` has the disease disabled.
- C6: the 70% check would pass with no outbreak at all. It now also requires `OutbreakStarted` events.
- Bug: `OutbreakDailyChance = NaN` made `roll >= NaN` false, so an outbreak started every day. Comparison negated in `OutbreakSystem.cs`; test `A_nan_outbreak_chance_never_starts_an_outbreak`.
- Noted, not changed: contagion is per shared map cell (plan said building); already recorded in checks Deviations.
