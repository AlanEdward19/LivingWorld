# Weather drought checks

Profile: light
Plan: `.specs/features/weather-drought/plan.md`

7 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Safra varia

**C1** - At the start of each year each region gets one factor in thousandths, inside the scenario bounds, drawn from a seeded stream per (region, year) and never rewritten (AC 1, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ClimateTests.Each_region_gets_one_factor"`

**C2** - The same seed draws the same years in one process and across two processes; over 20 years in 10 seeds the factors vary and average near the middle of the range (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ClimateTests.The_same_seed_draws"`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~DeterminismTwoProcessTests.Same_seed_draws_the_same_climate"`

**C3** - Without climate every year is 1000, the system draws nothing and the default scenario keeps its behavior (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ClimateTests.Without_climate"`

**C4** - Old years leave the ledger: it holds at most the kept years for every region (bounded) (door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ClimateTests.Old_years"`

### S2 - Seca tem consequência

**C5** - A year below 600 cuts the plant harvest in proportion (10 x 300 / 1000 = 3) and the crop harvest of the region (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ClimateTests.A_drought_year_cuts"`

**C6** - A forced drought in year 5 cuts the food harvest of the half year below 60% of the control, the price does not fall, and the village still has living NPCs after 20 years (AC 4, AC 5, AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ClimateTests.A_forced_drought_in_year_five"`

**C7** - The rules and the years survive a snapshot round trip with an equal canonical hash and change the hash when mutated; time per NPC-tick and allocation per tick are unchanged with the climate off; two processes agree on the hash (AC 7, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~ClimateTests.The_climate_rules|FullyQualifiedName~WorldSnapshotTests"`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness|FullyQualifiedName~DeterminismTwoProcessTests|FullyQualifiedName~GoldenHashesTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: climate shape (3) | one factor per (region, year) C1 · bounded ledger C4 · never rewritten C1 | - |
| harvest sources scaled (2) | plant harvest C5 · crop harvest C6 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: C1 (bounds), C3 (climate off)
- failure modes: C6 (drought survived for 20 years)
- idempotency: n/a - the year is drawn once per (region, year); a second write is ignored (C1)
- authorization: n/a - local simulation, no caller identity
- concurrency: n/a - single-threaded tick; the yearly system writes the world serially
- data lifecycle: C4 (old years purged)
- dependency failure: n/a - no external dependency
- state transitions: n/a - the factor of a year never changes after the draw
- observability: n/a - the factor is readable from the world; no new event

## Out of scope

- daily weather and meteorological events; storage and silos; aid between cities - see the plan

## Deviations (kept true)

- The harvest sources scaled are the ones that produce the village food: the crop harvest in `CropSystem`, the flora yield in `FloraLifecycleSystem` and the food output of `ProductionSystem` recipes. Water does not depend on rain in this model.
- The factor is drawn at the first tick of each year; the first year (index 0) has no draw and uses 1000.
- The default scenario keeps the climate off, so the village behavior is unchanged; the canonical hash changed once (new empty fields) and goldens and pins were re-recorded (AD-101).
- The migration reaction to hunger is the existing city mechanism; no new migration code was added or tested here.

## Handoff

Nine source files plus tests, one builder.
