# Trade ledger checks

Profile: light
Plan: `.specs/features/trade-ledger/plan.md`

11 checks in 4 slices · 1 one-way door · 0 open

## Checks

### S1 - Toda compra deixa registro

**C1** - A completed purchase adds exactly the quantity and value paid to the shop's daily aggregate, one purchase per buyer, and the aggregate value equals what left the wallet and entered the shop treasury (AC 1, AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TradeLedgerTests.A_purchase_enters_the_ledger_with_exactly_what_left_the_wallet|FullyQualifiedName~TradeLedgerTests.Two_purchases_the_same_day_sum_into_one_aggregate"`

**C2** - A refused purchase leaves the ledger, the shop treasury and stock, and the relationships untouched (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TradeLedgerTests.A_refused_purchase_leaves_the_ledger_the_shop_and_the_relationships_untouched"`

**C3** - The ledger survives a snapshot round trip with an equal canonical hash, and two equal runs write the same ledger (AC 9, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TradeLedgerTests.The_ledger_survives_a_snapshot_round_trip_and_two_equal_runs_write_the_same_ledger"`

### S2 - Contraparte e relação

**C4** - The buyer gains a trade relationship with the lowest-id attendant present at the shop (not with absent employees or other attendants), and only the first contact of the pair is a `Trade` event with `buyer|attendant|shop|resource|value` (AC 4, AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TradeLedgerTests.The_buyer_gains_a_trade_relationship_with_the_lowest_id_attendant_present_and_only_the_first_contact_is_an_event"`

**C5** - A shop with nobody present only writes the ledger: no relationship and no pending contact (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TradeLedgerTests.A_shop_with_nobody_present_only_writes_the_ledger"`

### S3 - Orçamento de armazenamento e custo

**C6** - Days older than the 60-day window fold into months without losing a cent, a unit or a purchase; the ledger holds the window plus at most one aggregate per month (AC 7)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TradeLedgerTests.Days_older_than_the_window_fold_into_months_without_losing_a_cent_and_the_ledger_stays_bounded"`

**C7** - Over 3 simulated years of the gate village the ledger never exceeds markets x 2 resources x (60 + 36) aggregates and has compacted months (AC 7)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~TradeLedgerLongRunTests.Three_year_ledger_stays_bounded_and_every_aggregate_is_consistent"`

**C8** - Bytes per NPC per year of the 1,000-NPC fixture stay within 110% of the baseline, µs per NPC-tick within 103% and allocation per tick within 102% of the value measured before the ledger (AC 7, AC 8)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline|FullyQualifiedName~MeasureHarness"`; numbers go in `verification.md`

### S4 - Consulta

**C9** - `GET /workplaces/{id}/trades?fromDay=0&toDay=20` answers 200 with the days of the interval in order, purchases summed per day and resource; days outside the interval are absent (AC 10)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~TradeEndpointTests.Interval_returns_the_days_in_order_with_summed_purchases"`

**C10** - An inverted interval answers 400 and an unknown workplace answers 404 (AC 11)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~TradeEndpointTests.Inverted_interval_is_rejected_with_400|FullyQualifiedName~TradeEndpointTests.Unknown_workplace_is_404"`

**C11** - The route and its two schemas are in the frozen contract `tests/contracts/openapi.v1.json`
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~ApiContractTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `GET /workplaces/{id}/trades` statuses (3) | 200 C9 · 400 C10 · 404 C10 | - |
| door 1: ledger persisted (3) | round trip C3 · bounded C6 · long run C7 | - |
| purchase outcomes (3) | closed with attendant C4 · closed without attendant C5 · refused C2 | - |
| ledger periods (2) | daily C1 · monthly C6 | - |

- Claims naming a status code or response shape: C9, C10 - both cross the HTTP boundary
- No other check claims more than the single case its proof exercises

## Swept

- validation: C10 (interval), C2 (refused purchase)
- failure modes: C2
- idempotency: n/a - a purchase is not retried; the aggregate is a sum, one record per completed transaction (C1)
- authorization: n/a - local read-only API, no caller identity in the project
- concurrency: n/a - single-threaded tick; the route reads the world the tick loop owns, like every other read route
- data lifecycle: C6, C7 (compaction)
- dependency failure: n/a - no external dependency
- state transitions: C4 (no relationship -> relationship, event only on that transition)
- observability: C4 (`Trade` event), C9 (route)

## Out of scope

- comércio entre cidades; imposto e tesouro; crédito - see the plan

## Deviations (kept true)

- AC 4 said the event fires for a purchase above the day's p90 **or** at first contact. Only first contact became an event: a p90 needs the day's distribution, an extra store for an event that adds little over the aggregate. Relation growth (AC 5) happens on every purchase.
- The ledger counts purchases, not distinct buyers (the record does not keep who bought); recorded in `TradeAggregate`.
- AC 3 was proven against the shop treasury and the buyer's wallet of the same transaction (C1); a whole-world money identity would need to separate sales from wages and is already covered by the money conservation tests.
- AC 8's allocation limit was measured against the branch head before the ledger, after the allocation fixes (see AD-092).

## Handoff

Seven files touched (TradeAggregate, WorldState x3, snapshot, hasher, TradeLedgerSystem, buy hook, endpoint) plus tests, about 40k tokens, under the 150k budget - one builder
