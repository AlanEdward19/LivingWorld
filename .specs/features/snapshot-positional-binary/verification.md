# Snapshot positional binary verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 10e604d..working tree (uncommitted; issue-23 changes are in the same tree)
**Round**: 2 - full
**Verifier**: independent sub-agent (author != verifier)

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | 20 seeds x 10y: equal hash, bytes stable on re-encode | Unit `PositionalSnapshotTests` (13 passed in batch of 37) | `Codec_round_trips_twenty_seeds_after_ten_years` and `Codec_round_trips_many_seeds_with_equal_hash_and_equal_bytes` ran and passed | PASS |
| C2 | every persistable property in the tree | same run | `Every_persistable_world_property_is_in_the_encoded_tree` passed; top-level properties only (nested NPC fields not enumerated) | PASS |
| C3 | identical bytes, one process and two | Unit same run; LongRunning `DeterminismTwoProcessTests` | `Same_seed_and_ticks_write_identical_bytes` and `Same_seed_writes_identical_positional_snapshot_bytes_across_two_separate_processes` passed | PASS |
| C4 | bytes/NPC/year <= 50% of 131,737 | LongRunning `ScaleScenarioSensorTests and BytesPerNpcPerYear`: 4 passed, 1 record test skipped | `ScaleScenarioSensorTests.cs:88` `Assert.True(... <= 0.5 * 131_737)`; baseline 43,863 (33.3%) for 1,000, 56,846 for 5,000 | PASS |
| C5 | diff holds only changed fields of changed dirty NPCs | Unit `BinarySnapshotWriterDiffTests` 11 passed | `Delta_has_no_entry_for_a_dirty_npc_that_did_not_change`, `Delta_contains_only_changed_npc_fields`, `Delta_compares_only_the_dirty_npcs` ran | PASS |
| C6 | diff on base rebuilds world over a year, smaller than full | Unit (2 named) | both `Delta_applied_to_the_base_...across_a_year` and `Delta_is_smaller_than_a_full_snapshot_...` passed | PASS |
| C7 | incremental == from-scratch every 100 ticks, 10y, 3 seeds | Unit `IncrementalHasherTests` | theory rows seed 77/12, 78/30, 79/60 each passed (plus 2 facts) | PASS |
| C8 | persist-every-tick equals continuous; golden matches code | LongRunning `GoldenHashesTests` 4 passed (1 record skipped); Integration `PersistentWorldRunnerTests` | `Resuming_from_a_snapshot_at_t_and_running_to_T_matches_the_continuous_hash_at_T` x4 passed; goldens 42/100, 42/3650, 43/3650, 42/17280 passed. Golden file still differs from HEAD (issue-23 re-record, declared in checks.md) | PASS |
| C9 | base then diffs, new base after 12, reopen continues 3,650, failed save, replaced base | Integration `PositionalSnapshotPersistenceTests` | 7 tests passed incl. `A_failed_save_drops_the_base...` (`:183` `Assert.Throws<IOException>`) and `A_diff_whose_base_row_was_replaced...` (`:207` `Assert.Throws<InvalidDataException>`); runner checks canonical hash on reload (`PersistentWorldRunner.cs:122`); `:46` `rows[13].BaseTick`, `:98` new base after reopening from a diff | PASS |
| C10 | legacy row loads; bad blocks rejected | Integration legacy test; Unit `Invalid_or_mismatched_blocks_are_rejected` | both passed (`:116` hash equal, `:123` tick 72) | PASS |
| C11 | zero DB round trips in tick | Integration `ZeroRoundTripsTests` 2 passed | both ran | PASS |
| C12 | load <= JSON, save <= 120%, base < 50 MB, us and alloc within 102% | LongRunning `SnapshotCostTests` + `MeasureHarness` (LW_MEASURE_OUT) passed | 10,000 NPCs: load 3,159 ms vs JSON 5,677; save full 3,648 vs 4,149, delta 2,979; bytes 22.9% of JSON. Harness: 0.957 us/NPC-tick, 532,532 B/tick alloc vs earlier HEAD A/B 0.950 us / 530,717-530,964 (+0.7% us, +0.3% alloc). us is not asserted by a test (noise ~5%, declared in checks.md) | PASS |

## Gaps

None blocking. Round-1 gaps 1-7 are closed (C8 proof now runs the LongRunning golden; C1/C3/C7 strengthened; C4 asserts 50%; failed-save and replaced-base tests exist; reload hash check; new base after reopening from a diff). Remaining, accepted and declared: C12 speed limit unasserted; AC 7 literal "golden unchanged" reworded in checks.md because issue-23 re-recorded the goldens; AC 4 Phase 9 goal of 50 B not reached (33% of baseline met).

## Issue 23 review

1. `NeedsRestocking` is now stateless and reads only the world; `TravelTicksCache` is a pure memo keyed by the immutable `WorldMap` (readonly lists), so a mid-day reload decides the same. No `ProvisionMemo` remains. Round-1 finding 1 closed.
2. Residual: if the wallet covers only the cheaper essential (water) but not the food, `NeedsRestocking` stays true while food is scarce, and each wake buys water until the wallet drops below its price. Bounded by the wallet, but it can spend the NPC's money on water it does not need. No test covers a wallet between the water and food prices.
3. `DrawFromWell` is now capped to the missing water (`:251-258`), but still fires at any `NearestEssentialsMarket`, not only a well workplace; a shop owner's water is given away free with no ledger when the buyer cannot pay. Tests (`ProvisioningRealismTests.cs:88-111`) use a shop as the "well", so this is untested.
4. `ProvisioningRealismTests.cs:140` still asserts `1 < target * 0.5`, mirroring the implementation constant `RestockShortfall`; it will not catch a wrong factor.
5. `scenarios/default.json` CostWeights change (travel times for all goldens and five baselines) must be in `docs/decisions-log.md` as separate from the snapshot feature; not confirmed here.

## Gate

Unit 37/37 (snapshot, diff, hasher, provisioning), Integration 18/18, LongRunning 15 run: 13 passed, 2 record tests skipped, 0 failed.
