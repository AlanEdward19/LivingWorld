# Economy depth checks

Profile: light
Plan: `.specs/features/economy-depth/plan.md`

7 slices · 2 one-way doors · 1 open (resolved: city pays with its own treasury, recommended)

## Checks

### S1 - A cadeia tem insumo · 2 files · 20 KB · ~6k

**C1** - A production cycle withdraws exactly the declared Inputs and deposits exactly the declared Outputs (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InputSupplyTests.Cycle_consumes_exact_inputs_and_deposits_exact_outputs"`

**C2** - A workplace missing an input produces 0 and leaves stock and treasury identical (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InputSupplyTests.Missing_input_produces_nothing_and_changes_nothing"`

**C3** - Every resource of `scenarios/default.json` has at least one producer and at least one consumer (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ScenarioLoaderEconomyTests.Every_resource_has_a_producer_and_a_consumer"`

### S2 - Lenhador e pedreiro trabalham · 3 files · 30 KB · ~9k

**C4** - Sawmill and quarry hire workers in the live default world (40 initial NPCs; with the 20-NPC default the existing labor market leaves them empty, see Deviations) within 90 days and emit `Hired` (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~EconomyDepthScenarioTests.Sawmill_and_quarry_hire_workers"`

**C5** - A lumberjack employed at the sawmill is paid the declared wage (95) by `WagePaymentSystem` and the routine gives `Work` at 09h and not at 20h (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~EconomyDepthScenarioTests.Lumberjack_is_paid_and_works_in_the_declared_window"`

**C6** - A treasury that cannot cover the payroll pays everyone the same fraction and emits `WageUnpaid` (AC 6, existing rule)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~WagePaymentSystemTests"`

### S3 - A cidade compra material · 5 files · 60 KB · ~16k

**C7** - A missing input is bought from a workplace of the same city: the amount is the smaller of the shortfall and the seller's stock, at the seller's price, debited from the buyer's treasury (AC 7)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InputSupplyTests.City_buys_the_smaller_of_shortfall_and_seller_stock_at_seller_price|FullyQualifiedName~InputSupplyTests.Workplace_buys_three_days_of_inputs_from_a_neighbour_at_the_neighbour_price"`

**C8** - A treasury that cannot pay transfers 0 and leaves the canonical hash identical (AC 8)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InputSupplyTests.Insufficient_treasury_transfers_nothing_and_keeps_the_hash|FullyQualifiedName~InputSupplyTests.Treasury_covering_part_of_the_shortfall_buys_only_the_affordable_units"`

**C9** - With the default catalog, a house build withdraws exactly the recipe Inputs from `City.Stock` (AC 9)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~EconomyDepthScenarioTests.House_build_consumes_exactly_the_recipe_from_city_stock"`

**C10** - `City.Treasury` survives a snapshot round-trip, and a snapshot without the field loads with zero (door 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InputSupplyTests.City_treasury_round_trips_and_old_snapshots_load_as_zero"`

**C11** - The scenario loader reads the city `Treasury` and seeds the same initial economy buffer as `ScenarioRunner`; the live world has living NPCs after year 1 in 3 seeds (plan assumption)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ScenarioLoaderEconomyTests"`

### S4 - Escassez reage · 1 file · 12 KB · ~4k

**C12** - With wood production zeroed (40 NPCs), the wood price ends higher and fewer buildings complete in 360 days than the control, each in at least 18 of 20 seeds (AC 10)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~EconomyDepthScenarioTests.Zero_wood_raises_price_and_completes_fewer_buildings"`

### S5 - Conservação e custo · 2 files · 15 KB · ~5k

**C13** - Total money and the total of each resource (workplaces, households, city) are conserved at every daily sample over 10 years, except declared production, consumption and loss (AC 11)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~EconomyDepthConservationTests"`

**C14** - Year-10 population summed over seeds 1-3 of the live world (20 NPCs) is at least 90% of `tests/baselines/live-default-population.json`, recorded after the loader buffer fix and before the new content: [7,10,9] (AC 12)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~EconomyDepthConservationTests.Live_world_population_stays_within_90_percent_of_the_baseline"`

**C15** - µs per NPC-tick of the 1,000-NPC fixture is at most 105% and allocation per tick at most 102% of the value measured on `develop` (a39e0bf) (AC 13)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness"` run alternating base and head, 4 rounds each; numbers go in `verification.md`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: scenario ids (6) | wood 3 C3 · stone 5 C3 · sawmill local 3 C4 · quarry local 4 C4 · lumberjack 3 C5 · stonemason 4 C5 | - |
| door 2: `City.Treasury` persisted (3) | round-trip C10 · missing field C10 · loader reads it C11 | - |
| supply buyers (2) | workplace buys inputs C7 · city buys construction material C7 | - |
| supply refusals (3) | no seller stock C7 · treasury short C8 · no input declared C2 | - |
| resources in the live catalog (5) | wheat C3 · water C3 · wood C3 · stone C3 · iron absent from the live catalog C3 | - |
| conservation quantities (6) | money C13 · wheat C13 · water C13 · wood C13 · stone C13 · city stock C13 | - |

- Claims naming a boundary or a status: none (no new route, `Surface` is None)
- No other check claims more than the single case its proof exercises

## Swept

- validation: C11 (loader rejects a negative `Treasury`) and C3
- failure modes: C2, C8
- idempotency: n/a - the supply system runs once per day and re-running with satisfied targets transfers 0 (C7 asserts the second run is a no-op)
- authorization: n/a - no caller outside the simulation
- concurrency: n/a - the simulation is single-threaded per tick and ordered by `RunsAfter`
- data lifecycle: C10
- dependency failure: n/a - no external dependency
- state transitions: C9
- observability: n/a - no logging requirement; transfers are visible through stock and treasury

## Out of scope

- curandeiro e professor - sem consumidor (plan)
- imposto como fonte do tesouro da cidade - spec própria

## Handoff

S1-S5 total about 55k tokens (files touched about 150 KB / 4 = 38k plus tests), under the 150k budget - one builder

## Deviations (kept true)

- The plan assumed the default scenario had cities on and a building catalog. It does not: `ScenarioRunner` (gate fixture) has `CityRules` disabled, and `scenarios/default.json` (live) had `BuildingRecipes: {}`. Content went to `default.json`; the gate fixture is unchanged.
- `ScenarioLoaderV2` worlds had no initial economy buffer and died in year 1 (0 alive in 3 seeds). The loader now seeds it (C11).
- The existing labor market (`ProfessionScoreOf`: skill x personality x vacancy fraction, ties to the lowest id) sends adults to one industry on tick 1 and never moves employed workers, so with 20 NPCs the sawmill and quarry stay empty (C4 uses 40 NPCs). Tried and reverted: a switch margin and "leave the job on a trade change" cut the gate village by 20-60% (the gate fixture is fed by that tie-break); a labor-allocation spec is the next step.
- Forge input (iron chain) stayed out of the live catalog; iron lives only in the gate fixture (plan assumption).
