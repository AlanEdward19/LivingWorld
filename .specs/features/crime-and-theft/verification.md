# Crime and theft verification

**Verdict**: PASS
**Profile**: light
**Diff range**: baf2fd2^..baf2fd2 (plus verifier fixes in the working tree)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | Steal scored only for desperate, penniless, no pantry, ill-tempered NPC with a victim in the cell; off never | `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter TheftTests` exit 0 (10 passed) | `tests/LivingWorld.Tests.Unit/Crime/TheftTests.cs:78,83,86,89` - `Assert.True/False(TheftResolver.Wants(...))`; `:114,117,120` (added) pantry and housemate cases; `:130,135` - `Assert.False(TheftResolver.Wants(off, ...))`, no victim | PASS |
| C2 | theft moves existing coin only, capped by the victim wallet | Unit filter above | `TheftTests.cs:151-153` - `Assert.Equal(before, TotalCoin(world))`, `Assert.Equal(50, thief.Wallet.Amount)`, `Assert.Equal(50, victim.Wallet.Amount)` | PASS |
| C3 | harsh village conserves money daily for a year and logs thefts | Unit filter above (Scenario, 3 s) | `TheftTests.cs:261` - `Assert.Equal(world.MoneyMinted.Amount - world.MoneyDestroyed.Amount, Total() - initialTotal)`; `:265` - `Assert.True(sink.Thefts > 0, ...)` | PASS |
| C4 | same seed same thefts, one and two processes | Unit filter above; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "MeasureHarness or DeterminismTwoProcessTests or GoldenHashesTests"` exit 0 (12 passed, 1 skipped recorder) | `TheftTests.cs:192` - `Assert.Equal(Run(), Run())`; `DeterminismTwoProcessTests.Same_seed_writes_the_same_thefts_across_two_separate_processes` passed (`Assert.Equal(a, b)`, thefts > 0) | PASS |
| C5 | discovered: Betrayal trust 50 to 42 and one event; undiscovered: nothing; failed: caught in the act | Unit filter above | `TheftTests.cs:155` - `Assert.Equal(42, world.Relationships[...].Trust)`; `:154` - `Assert.Single(sink.Events, ...)`; `:167-168` - `Assert.Empty(sink.Events)`, `Assert.Empty(quiet.Relationships)`; `:177` - failed attempt payload ends with zero loot and zero taken | PASS |
| C6 | two guards cut successful thefts, z at least 3 over 20 seeds | Unit filter above | `TheftTests.cs:224` - `Assert.True(z >= 3, ...)` (resolver level, 100 attempts x 20 seeds, not end-to-end through the decision system) | PASS |
| C7 | rules round trip with equal hash; Steal optional in the catalog (1 hour) and labelled | Unit filter above; `WorldSnapshotTests` (96 passed in the climate run) | `TheftTests.cs:238` - `Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored))`; `:64-65` - `Assert.Equal(1, ...DurationOf(ActionType.Steal))`, `Assert.Equal("Furtando", ...)`; `WorldSnapshotTests` case `TheftRules` passed | PASS |
| C8 | theft off: cost unchanged and golden stable | LongRunning filter above | `GoldenHashesTests.Gate_scenario_hash_matches_committed_golden_file` and `Long_horizon_scenario_hash_matches_committed_golden_file` passed; decision path adds only a `world.TheftRules.Enabled` check inside the existing urgent branch | PASS |

## Gate

`dotnet test tests/LivingWorld.Tests.Unit -c Release --filter TheftTests` - 10 passed, 0 failed; LongRunning filter - 12 passed, 0 failed, 1 skipped.

## Gaps found and fixed by the verifier

- C1 claims the pantry-stock exclusion but no test had a household. Added `A_pantry_stock_or_a_housemate_with_coin_does_not_make_a_victim_or_a_motive` (also proves a housemate is not a victim).
- AC 6 time ratio: `MeasureHarness` is not a gate; only goldens and structure prove "unchanged". No code change.
- C6 proves the guard effect on `TheftResolver.Resolve` only, not an end-to-end guarded village. Accepted at the resolver level.
- Reviewed with no defect: loot capped by wallet via `Math.Min`; debit then credit conserves coin; NaN or negative `MaxLootFraction`, `BaseSuccess`, `GuardEffect` degrade to a failed or minimal theft, never coin creation; victim scan in id order; named per-(thief, tick) stream.
- Known ceiling: `FindVictim` and `GuardsIn` scan all NPCs per desperate NPC when theft is on (O(n) each); off path costs one bool check.

## Follow-up (issue #42)

- C6 end to end: `A_village_with_guards_turns_the_attempts_into_failures_end_to_end_where_the_same_village_without_them_steals` runs a year through the decision system: without guards thefts succeed, with guards attempts happen and none succeeds.
- Theft-off cost: `Without_theft_there_is_no_index_and_asking_to_steal_allocates_nothing_per_tick` (0 bytes over 1000 calls); time and per-tick allocation also pinned by `ScaleScenarioSensorTests`.
- Cost ceiling lifted: `TheftIndex` (built once per tick, only with theft on) replaces the O(n) `FindVictim`/`GuardsIn` scans with a per-cell lookup and per-city guard counts; parity test `The_index_picks_the_same_victim_and_the_same_guards_as_the_full_scan`. Deviation: positions are those at the start of the tick (an NPC that walks into the cell mid-tick is a victim only from the next tick).
