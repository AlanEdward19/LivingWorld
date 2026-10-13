# Crime and theft checks

Profile: light
Plan: `.specs/features/crime-and-theft/plan.md`

8 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Furto causal

**C1** - Steal is scored only for a desperate (need at or below the scenario bound), penniless NPC with no pantry stock, an ill-tempered personality weight, and someone with coin in the same cell; with theft off it never is (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TheftTests.Only_a_desperate|FullyQualifiedName~TheftTests.Nobody_steals"`

**C2** - A successful theft moves existing coin only: total coin is unchanged and the loot is capped by the victim's wallet (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TheftTests.A_successful_theft"`

**C3** - A harsh village keeps the money conserved every day for a year and logs discovered thefts (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TheftTests.A_harsh_village"`

**C4** - The same seed gives the same thefts in one process and across two processes (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TheftTests.The_same_world"`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~DeterminismTwoProcessTests.Same_seed_writes_the_same_thefts"`

### S2 - Consequencia

**C5** - A discovered theft applies Betrayal victim to thief (trust 50 falls to 42) and logs one TheftDiscovered; an undiscovered one logs nothing and leaves no relationship; a failed one is caught in the act (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TheftTests.A_successful_theft|FullyQualifiedName~TheftTests.An_undiscovered"`

**C6** - Two guards in the city cut the successful thefts against the no-guard control with z of at least 3 over 20 seeds (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TheftTests.Guards_in_the_city"`

**C7** - The theft rules survive a snapshot round trip with an equal canonical hash; Steal is optional in the action catalog (1 hour when omitted) and has a label (door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~TheftTests.The_theft_rules|FullyQualifiedName~TheftTests.Steal_is_an_extra"`

**C8** - With theft off, time per NPC-tick and allocation per tick are unchanged and the golden hash is stable (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness|FullyQualifiedName~GoldenHashesTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: Steal action (3) | scored only when eligible C1 · optional catalog entry C7 · label C7 | - |
| theft outcomes (3) | took and discovered C5 · took and hidden C5 · failed and caught C5 | - |

- Claims naming a status code or response shape: none - no new endpoint; the action label list in the frozen OpenAPI contract is re-recorded
- No other check claims more than the single case its proof exercises

## Swept

- validation: C1 (eligibility), C7 (catalog optional)
- failure modes: C5 (failed attempt), C1 (no victim, no theft)
- idempotency: n/a - each attempt is a distinct (thief, hour) draw
- authorization: n/a - local simulation, no caller identity
- concurrency: n/a - single-threaded tick
- data lifecycle: n/a - no new stored collection, only the rules record
- dependency failure: n/a - no external dependency
- state transitions: C2 (coin moves, never created)
- observability: C5 (TheftDiscovered event and Betrayal relationship)

## Out of scope

- justice, prison and penalty; violence and murder; organized crime - see the plan
