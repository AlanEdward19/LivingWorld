# Taxes treasury verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 31ad674..3486ba5 (HEAD, feature commit 3486ba5)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Profile `light` does not require fault injection; the mutation the brief asked for was run anyway (below). All named tests were seen individually in the run output (no empty filter). Real tree clean after the run.

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | worker gets net, city gets tax, employer pays gross, money unchanged | Unit `WageTaxTests.The_employer_city_withholds...` Passed (Unit run 95/95) | `WageTaxTests.cs:69` - `Assert.Equal(190, worker.Wallet.Amount)`; `:70` - `Assert.Equal(10, city.Treasury.Amount)`; `:72` - sum equals `before` | PASS |
| C2 | zero rate / no city withholds nothing; short employer taxes only what it paid | `WageTaxTests.A_zero_rate...` and `An_employer_short...` Passed | `WageTaxTests.cs:82` - `Assert.Equal(200, zeroWorker.Wallet.Amount)`; `:88` - orphan wallet 200; `:101` - `Assert.Equal(5, city.Treasury.Amount)` | PASS |
| C3 | money identity incl. city treasuries at 1/10/100 y | LongRunning `MoneyConservationTests` (1 m, 1 y, 10 y, 100 y) and `EconomyDepthConservationTests` seeds 1-3 Passed (18/18 in the run); mutation M1 | `MoneyConservationTests.cs:19-20` - total sums wallets + workplace + `Cities.Sum(Treasury)`; `:25,34,43,72` - `AssertMoneyConserved` | PASS |
| C4 | treasury too small buys nothing, hash unchanged | `InputSupplyTests.Insufficient_treasury_transfers_nothing_and_keeps_the_hash` Passed | `InputSupplyTests.cs:177` - `Assert.Equal(before, WorldSnapshot.CanonicalHash(world))`; `:178` - no stock | PASS |
| C5 | city with funds buys at seller price, ledger records spending | Unit `City_buys_the_smaller...` + `City_material_purchase_is_recorded...` Passed; Integration `EconomyDepthScenarioTests` 6/6 Passed | `InputSupplyTests.cs:165` - `Assert.Equal((city.Id, 0L, 60L), (month.City, month.Revenue, month.Spending))`; `:189-190` - stock 2, treasury 2 | PASS (see gap G1) |
| C6 | one record per city+month, sums, snapshot round trip with equal hash | `WageTaxTests.The_treasury_ledger...`, `The_ledger_survives...`, `WorldSnapshotTests` (TreasuryLedger case) Passed | `WageTaxTests.cs:115` - `Assert.Equal([(0L,15L,4L),(1L,1L,0L)], ...)`; `:128` - ledger equal; `:130` - canonical hash equal; `WorldSnapshotTests` param `TreasuryLedger` Passed | PASS |
| C7 | GET 200 / 400 / 404 and frozen contract | Integration `TreasuryEndpointTests` 2/2, `ApiContractTests` 6/6 Passed (14/14 in the run) | `TreasuryEndpointTests.cs:39` - `Assert.Equal(HttpStatusCode.OK, ...)`; `:44-45` - revenue 50 / spending 25; `:55` - BadRequest; `:56` - NotFound; `ApiContractTests.cs:26` - `AssertMatchesContract("openapi.v1.json", ...)` | PASS |
| C8 | perf unchanged, two processes agree, goldens | LongRunning `DeterminismTwoProcessTests` 4/4 and `GoldenHashesTests` 5/5 Passed; `MeasureHarness` A/B (below) | `DeterminismTwoProcessTests` - `Same_seed_produces_identical_hashes_across_two_separate_processes` Passed; goldens in `tests/golden/world-hashes.json` regenerated on purpose | PASS |

## Faults injected

| Mutation | Location | Killed |
| --- | --- | --- |
| M1 (C1/C3): tax deducted from worker but `CreditTreasury` to the city removed | `WagePaymentSystem.cs` (the `world.FindCity(workplace.City)!.CreditTreasury(...)` line) | yes - LongRunning 7 of 9 failed (1 m, 1 y, 10 y, 100 y, depth seeds 1-3); Unit `WageTaxTests` 2 of 5 failed (`:72`, `:100`) |

Restored with `git checkout <path>`; `git status --short` empty afterwards.

## Performance (C8, scale1000, `LW_MEASURE_OUT`, worktree `../LivingWorld-ab-tax` at HEAD~1, removed)

| Round | HEAD~1 us/NPC-tick | HEAD us/NPC-tick | HEAD~1 alloc/tick | HEAD alloc/tick |
| --- | --- | --- | --- | --- |
| 1 | 0.872 | 0.867 (99.4%) | 241018 | 241162 (100.06%) |
| 2 | 0.826 | 0.846 (102.4%) | 240969 | 241080 (100.05%) |

Within noise and inside the 103% / 102% bounds. `snapshotBytesPerAlive` 10860 on both. The village/live scenarios diverge from HEAD~1 (tax changes the economy), as expected by the intentional re-record.

## Gap hunt

| Area | Finding |
| --- | --- |
| Floor rounding | `(long)Math.Floor(pay * rate)`: a pay under 20 at 5% withholds 0, conserved by construction (`net = pay - tax`). No test pins the sub-unit case. Non-blocking. |
| Rate validation | `EconomyRules.Create` rejects <0 and >0.5; `NaN` slips through (`is < 0 or > 0.5` is false for NaN). Code-only path, JSON cannot carry NaN. No test covers either bound. |
| Loader | `EconomyScenarioLoader` reads `WageTaxRate`; absent or non-numeric (e.g. a string) silently becomes 0 instead of an error. No test loads it. `scenarios/default.json:72` sets 0.05. |
| Old snapshots | `WorldSnapshot` reads `TreasuryLedger` with a missing-field fallback to empty; `EconomyRules` default param gives 0. Not tested for the new fields (the city-treasury old-snapshot test at `InputSupplyTests.cs:207` is for the earlier field). |
| Merged city | `WagePaymentSystem` uses `FindCity`, not `FindActiveCity`. `MergeCityInto` re-points workplaces to the mother city, so a payer never holds a tombstoned city id; no gap found. Old months stay under the daughter id (history), and the endpoint answers 200 with balance 0 for a tombstoned city. Acceptable. |
| `TreasuryOf` overlap | `(m+1)*D > from && m*D <= to` is correct (month m spans days m*D..(m+1)*D-1). Tests cover 0..29 and 0..59 only; day 29/30 boundary and `from` inside a month are not pinned. Non-blocking. |
| `RecordTreasury` order | `FindIndex` then `BinarySearch` with a (city.Value, month) comparer: deterministic, ledger stays sorted; snapshot round trip keeps it. Linear scan is O(records), at most one per city and month. |
| Ledger growth | Bounded: one record per city and month (100 y = 1200 per city). Not compacted, not unbounded per event. |
| DTO and route | `CityTreasury(CityId, Balance, FromDay, ToDay, Months)` with `Guid` route `{id:guid}`; defaults to the last 360 days; contract frozen in `openapi.v1.json`. Matches the plan. |
| Deviation: no purchase tax | Recorded in `checks.md` Deviations and AD-098; only the wage is taxed, which avoids taxing the same coin twice. Accepted. |
| Perf of monthly path | Tax runs in the monthly `WagePaymentSystem`, one `FindCity` per paid wage twice (dictionary lookup); A/B shows no measurable cost. |

## Gaps

- G1: AC 2 ("obra adiada") is proven end to end only by the unit test C4; AC 4 ("obra iniciada") is proven for the material purchase (C5 unit, `InputSupplyTests.cs:165`). The live-world run (`EconomyDepthScenarioTests`) builds works but asserts no ledger spending, so no test shows a postponed then started work in one world. Non-blocking for the checks as written.
- G2: Loader/validation/old-snapshot paths of `WageTaxRate` and the `TreasuryOf` day boundary have no test (not claimed by C1-C8).

## Gate

`dotnet test` Unit (95 passed), Integration (14 passed), LongRunning (18 passed), `MeasureHarness` 2x2 - 0 failed
