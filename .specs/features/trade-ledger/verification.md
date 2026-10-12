# Trade ledger verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 7c4ea25..e81e4fd
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Proofs run at HEAD e81e4fd, one `dotnet test` per project, `-c Release --logger "console;verbosity=normal"`; every named test appears individually as Passed. Each test shown to exist with `rg -n`/`grep -n` in `tests/**/TradeLedgerTests.cs`, `TradeEndpointTests.cs`, `TradeLedgerLongRunTests.cs`, `ApiContractTests.cs`.

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | purchase adds exactly qty/value paid; aggregate == wallet spend == shop treasury | Unit `--filter "TradeLedgerTests / LaborAllocationTests / EmployerRationTests"` 22 passed (both C1 tests Passed) | `tests/LivingWorld.Tests.Unit/Economy/TradeLedgerTests.cs:94` `Assert.Equal(spent, today.Sum(e => e.Value))`; `:95` `Assert.Equal(shop.Treasury.Amount, today.Sum(e => e.Value))`; `:125-126` `Assert.Equal(2, wheat.Purchases)` / `Assert.Equal(wheat.Quantity * 4, wheat.Value)` | PASS |
| C2 | refused purchase leaves ledger, shop, relationships untouched | same invocation | `TradeLedgerTests.cs:107` `Assert.Empty(world.TradeLedger)`; `:108` `Assert.Empty(world.Relationships)`; `:110` `Assert.Equal(Money.Zero, shop.Treasury)`; `:111` `Assert.Equal(1000, shop.Stock[Wheat])`. Hash identity not asserted directly (AC 2 says "hash identical"); the four state facts stand in for it | PASS |
| C3 | ledger survives snapshot round trip with equal canonical hash; two equal runs same ledger | same | `TradeLedgerTests.cs:196` `Assert.Equal(world.TradeLedger, restored.TradeLedger)`; `:197` `Assert.Equal(WorldSnapshot.CanonicalHash(world), WorldSnapshot.CanonicalHash(restored))`; `:208` `Assert.Equal(first, Run())` (two runs are in-process, 60 days, not two processes: see gaps) | PASS |
| C4 | buyer gains trade relation with lowest-id present attendant; only first contact is a `Trade` event with `buyer / attendant / shop / resource / value` | same | `:145` `Assert.True(relation.Trust > 0)`; `:146-147` `Assert.False(world.Relationships.ContainsKey(... absent/other))`; `:148` `Assert.Single(sink.Events, e => e.Kind == WorldEventKind.Trade)` after two `Buy` calls; `:149` payload equality. The value field is compared to itself (`trade.Payload.Split(' / ')[4]`), so the value is not pinned (precision gap) | PASS |
| C5 | shop with nobody present writes only the ledger | same | `:160` `Assert.NotEmpty(world.TradeLedger)`; `:161` `Assert.Empty(world.Relationships)`; `:162` `Assert.Empty(world.PendingTradeContacts)` | PASS |
| C6 | days older than 60 fold into months losing nothing; ledger bounded | same | `:178` `Assert.Equal(total, world.TradeLedger.Sum(e => e.Value))`; `:179-180` quantity 600, purchases 300; `:181` `Assert.Equal(WorldState.TradeLedgerDetailDays, world.TradeLedger.Count(e => !e.Monthly))`; `:182` monthly `<= 300 / 30` | PASS |
| C7 | 3 years gate village: ledger never exceeds markets x 2 x (60+36); has compacted months | `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "TradeLedgerLongRunTests / ScaleScenarioSensorTests.One_month... / MeasureHarness / LaborAllocationVillageTests / EconomyDepthConservationTests.Live_world..."` 6 passed | `tests/LivingWorld.Tests.LongRunning/Economy/TradeLedgerLongRunTests.cs:26` `Assert.True(maxCount <= bound, ...)`; `:32` `Assert.True(world.TradeLedger.Any(e => e.Monthly), ...)`. `maxCount` is sampled 3 times (year ends), not "never": sampling gap, see gaps | PASS |
| C8 | bytes/NPC/yr <=110% of baseline; us/NPC-tick <=103%, alloc <=102% of value before the ledger | same LongRunning invocation (Scale sensor + MeasureHarness Passed) plus alternating MeasureHarness runs, ledger-before = 2700aed | bytes: `ScaleScenarioSensorTests.cs:84-85` `AssertInRelativeBand(sample.BytesPerAliveNpcPerYear, baseline..., 0.01)` (1% band, tighter than 110%; baseline was re-recorded in this commit 129,633 -> 130,314 = +0.5%); snapshot bytes/alive 10855 -> 10859 (+0.04%). Alloc/tick 2700aed: 542,442 / 542,424 / 542,461 / 542,262 / 542,356; HEAD: 542,667 / 542,724 / 542,611 / 542,679 / 542,630 / 542,761 / 543,246 -> +0.05% (<=102%). us/NPC-tick 2700aed: 0.639 0.641 0.641 0.687 0.647 (mean 0.651); HEAD: 0.700 0.670 0.656 0.616 0.648 0.667 0.650 (mean 0.658) -> 101% (<=103%, inside the +-7% machine noise, the 103% line is not resolvable on this machine) | PASS |
| C9 | GET interval answers 200, days in order, purchases summed per day/resource, outside days absent | `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "TradeEndpointTests / ApiContractTests / EconomyDepthScenarioTests.<2 names>"` 11 passed | `tests/LivingWorld.Tests.Integration/Trade/TradeEndpointTests.cs:43` `Assert.Equal(HttpStatusCode.OK, ...)`; `:46` `Assert.Equal(2, days.Length)`; `:47` `days[0].day == 10`, `:51` `Assert.Equal(2, days[0].GetProperty("purchases").GetInt64())`; `:52` `days[1].day == 12` | PASS |
| C10 | inverted interval 400, unknown workplace 404 | same | `TradeEndpointTests.cs:62` `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)`; `:70` `Assert.Equal(HttpStatusCode.NotFound, response.StatusCode)` | PASS |
| C11 | route and two schemas in frozen contract | same | `tests/contracts/openapi.v1.json:1604` path `/workplaces/{id}/trades`, `:502` `"TradeDay"`, `:560` `"WorkplaceTrades"`; `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:26` `AssertMatchesContract("openapi.v1.json", ...)` (OpenApi_document_matches_the_committed_contract Passed) | PASS |

## Deviations judged

- AC 4 p90 dropped (plan and checks both edited after approval to say so): justified (p90 needs the day's distribution) and recorded in `plan.md` AC 4, door 1 and Assumptions, and in checks Deviations. True: the code emits only first-contact events (`TradeLedgerSystem.cs`, `BehaviorDecisionSystem.Effects.cs` `NoteTrade`). Note the plan was rewritten rather than the criterion left standing; acceptable because the change is explicit.
- "Ledger counts purchases, not distinct buyers": true (`TradeAggregate.cs:9`), AC 1 asked for "compradores" in the old flow text only.
- AC 3 proven per transaction, not whole-world money: true and honest (C1 test uses one shop).
- AC 8 allocation measured against the branch head before the ledger: true; I reproduced it at 2700aed (+0.05%). That reference omits the employer ration (also in e81e4fd), so the number bundles ledger + ration, not the ledger alone; the claim is still bounded.
- Not recorded: AC 9 says "two processes"; C3 and the long run use two runs in one process (can mask static state, which the perf commit adds in `ConditionalWeakTable` caches). Not recorded: AC 7 base is the re-recorded `scale-sensor.json` (self-referential), though the +0.5% against the old value I computed from the diff is well inside 110%.

## Other diff items (item f)

- `WorldEventKind.Trade` is appended after `Exhaustion` at the end of the enum (`WorldEventKind.cs` diff): persisted ordinals stable. Confirmed.
- Behavior with no dedicated check: Trade deltas Trust +0.5 / Respect +0.2 per purchase (`ScenarioRunner.cs:149-150`) are only asserted as `Trust > 0` (C4), magnitude and downstream effect (courtship, aid) are covered only through population floors (labor C10/C16: the 343 -> 348 move is attributed to them in labor Deviations). `SignificanceCalculator` Trade = 0.30 and `LivingEventPresentationCatalog` label are unchecked cosmetics.
- Design notes (for the author, not check failures): `RecordTrade` (`WorldState.Economy.cs`) does a linear `IndexOfTrade` scan and a full `List.Sort` on every new (shop, resource, day); `CompactTradeLedger` is quadratic-ish on removes. Size is bounded by shops x resources x 96 so it is cheap at the gate village (measured +1% us), but ~O(N) per purchase and O(N log N) per new aggregate scales with city count x resources sold, not NPC count. `NoteTrade` applies `ApplyEvent(Trade)` and `MarkContact` on EVERY purchase (`BehaviorDecisionSystem.Effects.cs`, `ApplyEvent` also allocates `Enum.GetValues<>()` per call) and `MarkPropertyDirty(TradeLedger)` rehashes the whole ledger each hash: acceptable at measured scale (bytes +0.04%, alloc +0.05%) but it is per-purchase hash churn on relationships. `PendingTradeContacts` is volatile: a save mid-hour loses that hour's `Trade` event while the relationship (canonical) survives, so the event is never re-emitted; "first contact" is "no buyer->attendant Relationship exists" so a pair with any prior relation never emits. The GET route reads a `List` the tick thread mutates (`TradesOf` `.Where(...).ToList()`), same as other read routes.

## Gaps (ranked, none flips a verdict)

1. C7 asserts the bound at 3 yearly samples while claiming "never exceeds"; the test header still says "C8" and promises "money matches what shops received" but asserts no money identity (`TradeLedgerLongRunTests.cs:5-6`).
2. C4 value field of the event payload is not pinned (`TradeLedgerTests.cs:149` compares the payload with its own fifth field).
3. C3/AC 9 "two processes" proven in-process.
4. C2 does not assert hash equality (AC 2 wording), only the state components.
5. C9 sums purchases for one resource only per day; two resources on the same day are not asserted.
6. C8 us limit (103%) is below machine noise; it passes (101%) but cannot be resolved reliably.

## Gate

Unit 22 passed, 0 failed; Integration 11 passed, 0 failed; LongRunning 6 passed, 0 failed (`verify.sh --full` not run in light profile).
