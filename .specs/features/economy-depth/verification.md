# Economy depth verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 78fa74f..6296097
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Checks

Proofs run at HEAD 6296097, one `dotnet test -c Release --logger "console;verbosity=normal"` per project (Unit 14 passed, Integration 4 passed, LongRunning conservation 2 passed, MeasureHarness 1 passed). Every named test shows as `Passed` individually in the output.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | cycle withdraws exact Inputs, deposits exact Outputs | Unit, `InputSupplyTests.Cycle_consumes_exact_inputs_and_deposits_exact_outputs` Passed | `tests/LivingWorld.Tests.Unit/Economy/InputSupplyTests.cs:105-106` - `Assert.Equal(10 - 2 * 2, forge.Stock[Wood]); Assert.Equal(5 * 2, forge.Stock[Iron]);` | PASS |
| C2 | missing input -> 0 produced, stock and treasury identical | Unit, `Missing_input_produces_nothing_and_changes_nothing` Passed | `InputSupplyTests.cs:118-119` - `Assert.False(forge.Stock.ContainsKey(Iron) && forge.Stock[Iron] > 0); Assert.Equal(before, WorldSnapshot.CanonicalHash(world));` (wood 0, treasury 100) | PASS |
| C3 | every live-catalog resource has producer and consumer | Unit, `ScenarioLoaderEconomyTests.Every_resource_has_a_producer_and_a_consumer` Passed | `tests/LivingWorld.Tests.Unit/Periods/ScenarioLoaderEconomyTests.cs:76-77` - `Assert.True(produced.Contains(resource), ...); Assert.True(consumed.Contains(resource), ...)`; `:65-66` asserts 3 and 5 are in the set. Wheat/water consumer is hard-coded into `consumed` (line 71), wood/stone consumers come from building recipes | PASS (see finding 3) |
| C4 | sawmill and quarry hire within 90 days, `Hired` emitted | Integration, `Sawmill_and_quarry_hire_workers` Passed | `tests/LivingWorld.Tests.Integration/Economy/EconomyDepthScenarioTests.cs:58-59` - `Assert.Contains(sawmill.Id.Value.ToString(), hiredAt); Assert.Contains(quarry.Id.Value.ToString(), hiredAt);` after `clock.Run(world, 90 * 24)` (line 55), 40 NPCs by default of `Load` | PASS |
| C5 | lumberjack paid 95, `Work` at 09h not at 20h | Integration, `Lumberjack_is_paid_and_works_in_the_declared_window` Passed | `EconomyDepthScenarioTests.cs:75` - `Assert.Equal(before + 95, worker.Wallet.Amount);` `:77-78` - `Assert.Equal(ActionType.Work, ...hour: 9); Assert.NotEqual(ActionType.Work, ...hour: 20);` | PASS |
| C6 | short treasury pays same fraction + `WageUnpaid` (existing rule) | Unit, whole `WagePaymentSystemTests` (4 tests) Passed | `tests/LivingWorld.Tests.Unit/Economy/Labor/WagePaymentSystemTests.cs:135-136` - `Assert.Equal(new Money(15), e1.Wallet); Assert.Equal(new Money(15), e2.Wallet);` and `:139` `Assert.Equal(2, unpaid.Count);` (class untouched by the diff, existing rule as the check says) | PASS |
| C7 | city buys min(shortfall, seller stock) at seller price from city treasury | Unit, `City_buys_the_smaller_of_shortfall_and_seller_stock_at_seller_price` Passed | `InputSupplyTests.cs:146-149` - `Assert.Equal(15, city.Stock[Wood]); Assert.Equal(60, sawmill.Treasury.Amount); Assert.Equal(940, city.Treasury.Amount);` (need 20, seller 15, price 4, treasury 1000); `:153` second run hash identical | PASS (see finding 1) |
| C8 | treasury cannot pay -> 0 transferred, hash identical | Unit, `Insufficient_treasury_transfers_nothing_and_keeps_the_hash` Passed | `InputSupplyTests.cs:165-166` - `Assert.Equal(before, WorldSnapshot.CanonicalHash(world)); Assert.False(city.Stock.ContainsKey(Wood));`. Refusal is real: seller has 15 wood at price 4 (line 160), city treasury 3 (line 159) so not even one unit is affordable | PASS (see finding 2) |
| C9 | house build withdraws exactly recipe Inputs from `City.Stock` | Integration, `House_build_consumes_exactly_the_recipe_from_city_stock` Passed | `EconomyDepthScenarioTests.cs:95-97` - `Assert.Equal(25 - 20, city.Stock.GetValueOrDefault(Wood)); Assert.Equal(0, ...Stone); Assert.Equal(1, HousesBuilt(world));`; recipe values asserted at `:87-88` (20 wood, 10 stone) from the live default catalog | PASS |
| C10 | `City.Treasury` round-trips; snapshot without field loads 0 | Unit, `City_treasury_round_trips_and_old_snapshots_load_as_zero` Passed | `InputSupplyTests.cs:176-177` - `Assert.Equal(1234, restored.Cities[0].Treasury.Amount); Assert.Equal(CanonicalHash(world), CanonicalHash(restored));` and `:182` `Assert.Equal(0, old.Cities[0].Treasury.Amount);` after `Remove("Treasury")` (line 180) | PASS |
| C11 | loader reads city `Treasury`, seeds the buffer, living NPCs after year 1 in 3 seeds | Unit, `ScenarioLoaderEconomyTests` (4 tests) Passed | `ScenarioLoaderEconomyTests.cs:24` - `Assert.Equal(20000, ...Treasury.Amount)`; `:46-47` - `Assert.Equal(50L * h.Members.Count, h.Stock[food])`, `Assert.Equal(50, n.Wallet.Amount)` (buffer proven); `:51` `Assert.True(world.Npcs.Any(n => n.IsAlive), ...)` over seeds 1,2,3 after `Run(360*24)`; `:35-36` negative treasury rejected, error contains "Treasury" | PASS |
| C12 | zero wood: price higher and fewer buildings, each >= 18 of 20 seeds, vs control | Integration, `Zero_wood_raises_price_and_completes_fewer_buildings` Passed (9 s) | `EconomyDepthScenarioTests.cs:117-118` - `Assert.True(cheaper >= 18, ...); Assert.True(fewer >= 18, ...)`. Loop `for seed 1..20` (line 104) loads control and treated with the same seed (106-107) and compares `treatedPrice > controlPrice` (113) and `HousesBuilt(treated) < HousesBuilt(control)` (114) | PASS |
| C13 | money and every resource conserved at each daily sample for 10 years | LongRunning, `Money_and_every_resource_are_conserved_at_every_daily_sample_over_ten_years` Passed | `tests/LivingWorld.Tests.LongRunning/Economy/EconomyDepthConservationTests.cs:64` - `Assert.True(expectedMoney == TotalMoney(world), ...)` (money includes NPC wallets, workplace and city treasuries, line 40-43); `:72` - `Assert.True(left == right, ...)` over resources `{1,2,3,5}` (line 55 = wheat, water, wood, stone) with `Stocked` summing workplaces + households + `c.Stock` (45-48), produced/consumed/lost on each side; sampled `tick % 24` over `10 * OneYearInHours` (58-61) | PASS (see finding 4) |
| C14 | year-10 population of live world (20 NPCs) seeds 1-3 >= 90% of baseline | LongRunning, `Live_world_population_stays_within_90_percent_of_the_baseline` Passed | `EconomyDepthConservationTests.cs:90` - `Assert.True(total >= 0.9 * baseline.Sum(), ...)`; baseline `tests/baselines/live-default-population.json` reads `[7,10,9]` (sum 26, threshold 23.4), population 20 and `Run(10 * OneYearInHours)` at lines 85-86 | PASS |
| C15 | scale1000 µs/NPC-tick <= 105%, alloc/tick <= 102% of develop a39e0bf | LongRunning, `MeasureHarness.Measure` Passed (26 s), `LW_MEASURE_OUT` captured | My single run at HEAD: `scale1000 microsPerNpcTick=0.601 allocPerTick=504240`. Orchestrator alternating runs: develop mean 0.639 (0.584-0.691), head mean 0.642 (0.624-0.666) = +0.5% vs 105% limit; alloc 504,142-504,249 B/tick on both (<= +0.02% vs 102% limit). My 0.601 sits inside both ranges and the 7% noise band; alloc 504,240 is inside the shared range. Measurement, no assert (as the check states) | PASS |

## Swept (existing rows re-read against the code)

| Row | Cited constraint | Found |
| --- | --- | --- |
| validation: C11 negative `Treasury` | loader rejects negative | yes - `src/LivingWorld.Simulation/Cities/CityScenarioLoader.cs` diff: `if (treasury < 0) return ...Fail("Cities[].Treasury: não pode ser negativo")`, proven by `Negative_city_treasury_is_rejected_at_load` Passed |
| failure modes C2, C8 | refusal leaves state identical | yes - tests above; `City.TryDebitTreasury` leaves `Treasury` unchanged on failure (`src/LivingWorld.Domain/Cities/City.cs`) |
| data lifecycle C10 | missing field reads as zero | yes - C10 removes the field and asserts 0 |
| idempotency (re-run transfers 0) | claimed C7 second-run no-op | yes - `InputSupplyTests.cs:151-153` |
| state transitions C9 | construction consumes recipe | yes - `:95-97` |

## Findings (non-blocking under light; recorded, not softened)

1. Precision gap in checks.md Coverage: row "supply buyers: workplace buys inputs -> C7" resolves to nothing C7's proof runs. C7's filter names only `City_buys_the_smaller_of...`. The workplace-as-buyer path is asserted by `InputSupplyTests.Workplace_buys_three_days_of_inputs_from_a_neighbour_at_the_neighbour_price` (`InputSupplyTests.cs:132-135`, Passed in my class-level run) but no check names it, so a regression there is not gated by any listed proof command. Add it to C7's filter or give it its own check.
2. C8 only forces refusal at "cannot afford a single unit" (treasury 3, price 4). It does not distinguish "transfer 0" from "transfer the affordable part" when the treasury covers some but not all of the shortfall. The AC wording ("não cobre o preço" -> 0) is met; partial affordability is unspecified and unproven.
3. C3's `consumed` set hard-codes wheat and water as consumed (`ScenarioLoaderEconomyTests.cs:71`) and the resource set is `PriceFloor` keys + food + water, so a catalog resource without a `PriceFloor` entry escapes the check. Iron's absence from the live catalog is a plan assumption, not asserted here.
4. C13 is one seed (2, 40 NPCs) and asserts nothing that the supply or construction paths actually moved goods in that run, so conservation could hold with those paths idle. Sampling gap, not a failing assertion.

## Gate

Unit (InputSupplyTests + ScenarioLoaderEconomyTests + WagePaymentSystemTests) - 14 passed, 0 failed; Integration (EconomyDepthScenarioTests) - 4 passed, 0 failed; LongRunning (EconomyDepthConservationTests) - 2 passed, 0 failed; MeasureHarness - 1 passed, 0 failed. Total 21 passed, 0 failed. Full `scripts/verify.sh` was not run by the Verifier (light, scoped proofs only).
