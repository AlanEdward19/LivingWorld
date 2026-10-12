# Taxes treasury checks

Profile: light
Plan: `.specs/features/taxes-treasury/plan.md`

8 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Receita conservada

**C1** - A paid wage is cut by the rate: the worker gets the net, the employer city treasury gets the tax, the employer pays the same gross, and total money is unchanged (AC 1, AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~WageTaxTests.The_employer_city_withholds"`

**C2** - A zero rate, or a workplace without a city, withholds nothing; an employer short on cash taxes only what it paid and money stays conserved (AC 1, AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~WageTaxTests.A_zero_rate|FullyQualifiedName~WageTaxTests.An_employer_short"`

**C3** - Over 1, 10 and 100 simulated years the money identity `wallets + workplace and city treasuries == initial + minted - destroyed` holds on every sample (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MoneyConservationTests|FullyQualifiedName~EconomyDepthConservationTests"`

**C4** - A city with too little treasury for a work's material buys nothing and the world hash is unchanged; the postponed purchase happens once revenue arrives (AC 2, AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InputSupplyTests.Insufficient_treasury|FullyQualifiedName~InputSupplyTests.A_work_postponed"`

### S2 - Gasto vira cidade

**C5** - A city with enough treasury buys the construction material at the seller price and the purchase is recorded as spending in the monthly ledger (AC 4, AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InputSupplyTests.City_buys|FullyQualifiedName~InputSupplyTests.City_material_purchase"`; `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~EconomyDepthScenarioTests"` (the live world builds works)

**C6** - The ledger keeps one record per city and month, sums revenue and spending, and survives a snapshot round trip with an equal canonical hash (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~WageTaxTests.The_treasury_ledger|FullyQualifiedName~WageTaxTests.The_ledger_survives|FullyQualifiedName~WorldSnapshotTests"`

**C7** - `GET /cities/{id}/treasury?fromDay=0&toDay=29` answers 200 with balance and the months of the interval; an inverted interval answers 400; an unknown city answers 404; the route is in the frozen contract (AC 6, AC 7)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~TreasuryEndpointTests|FullyQualifiedName~ApiContractTests"`

**C8** - Time per NPC-tick and allocation per tick are unchanged (the tax runs in the monthly wage system) and two processes agree on the hash (AC 8)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness|FullyQualifiedName~DeterminismTwoProcessTests|FullyQualifiedName~GoldenHashesTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `GET /cities/{id}/treasury` statuses (3) | 200 C7 · 400 C7 · 404 C7 | - |
| door 1: treasury shape (2) | balance per city C1 · monthly aggregate C6 | - |
| tax cases (3) | taxed C1 · rate zero or no city C2 · short employer C2 | - |

- Claims naming a status code or response shape: C7 - crosses the HTTP boundary
- No other check claims more than the single case its proof exercises

## Swept

- validation: C7 (interval), C2 (rate zero, no city)
- failure modes: C4 (treasury too small), C2 (short employer)
- idempotency: n/a - a wage is paid once a month; the ledger is a sum, not a retried write
- authorization: n/a - local read-only API, no caller identity in the project
- concurrency: n/a - single-threaded tick; the route reads the world the tick loop owns
- data lifecycle: C6 (one record per city and month, bounded by months)
- dependency failure: n/a - no external dependency
- state transitions: n/a - no state machine touched
- observability: C7 (route), C6 (ledger)

## Out of scope

- inheritance tax; corruption and diversion - see the plan

## Deviations (kept true)

- Tax on purchases (the plan mentioned the trade ledger as the source of the taxable value) was not built: only the wage is taxed; a purchase tax would tax the same coin twice. Recorded in AD-098.
- The route answers 400 for an inverted interval besides 200 and 404 (same as the trade route).
- The city can also receive the estate of a dead NPC without heirs (estate-inheritance); that revenue is recorded in the same ledger.
- The spending side is the existing material purchase for works (AD-090); no new "wall" or "well" building recipe was added: those are scenario data (`BuildingRecipes`).
- A rate outside 0 to 0.5 (NaN included) is rejected by `EconomyRules.Create`, and a non-numeric `WageTaxRate` in a scenario is an error (both tested); a missing field means 0.
- The default rate (5%) re-recorded goldens, pins and the contract on purpose.

## Handoff

Eleven source files plus tests, one builder.
