# Disease epidemics checks

Profile: light
Plan: `.specs/features/disease-epidemics/plan.md`

8 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Contágio determinístico

**C1** - An infected NPC infects a susceptible one in the same cell (seeded roll of the pair and day) and not one in another cell or an immune one; contagion 0 never spreads (AC 1, AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~DiseaseTests.An_infected_npc_infects|FullyQualifiedName~DiseaseTests.A_zero_contagion"`

**C2** - The same world gives the same epidemic, in one process and across two processes (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~DiseaseTests.The_same_world"`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~DeterminismTwoProcessTests.Same_seed_writes_the_same_epidemic"`

**C3** - An infection ends in immunity that blocks reinfection and then expires; lethality 1 kills at the end with a `Disease` event (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~DiseaseTests.An_infection_ends|FullyQualifiedName~DiseaseTests.A_lethality_of_one"`

**C4** - An outbreak starts only in a city over the population threshold and logs one `OutbreakStarted` event; the default scenario has no disease and a disabled system changes nothing (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~DiseaseTests.An_outbreak_starts|FullyQualifiedName~DiseaseTests.The_default_scenario"`

### S2 - Efeito demográfico

**C5** - A finished outbreak among 400 NPCs in one cell with lethality 0.2 kills between 0.5x and 1.5x of the declared lethality and leaves no active infection (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~DiseaseTests.Mortality_of_a_finished_outbreak"`

**C6** - The village with a moderate disease keeps at least 70% of the control population after 5 years in 8 seeds (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~DiseaseTests.The_village_with_a_moderate_disease"`

**C7** - Infections, immunities and the rules survive a snapshot round trip with an equal canonical hash, and each canonical list changes the hash when mutated (door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~DiseaseTests.Infections_immunities|FullyQualifiedName~WorldSnapshotTests"`

**C8** - Time per NPC-tick and allocation per tick are unchanged with the disease off (the system returns first thing) and two processes agree on the hash (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness|FullyQualifiedName~DeterminismTwoProcessTests|FullyQualifiedName~GoldenHashesTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: disease state (3) | infection C1 C3 · immunity C3 · rules C7 | - |
| infection endings (2) | cure with immunity C3 · death C3 C5 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: n/a - the rules come from the scenario; a missing disease profile skips the infection (no crash)
- failure modes: C3 (death through the shared `NpcDeath`), C1 (immune and distant NPCs stay healthy)
- idempotency: n/a - one infection per NPC and disease (`AddInfection` ignores a second one), tested by the contagion check
- authorization: n/a - local simulation, no caller identity
- concurrency: n/a - single-threaded tick; the daily system reads and writes the world serially
- data lifecycle: C3 (immunity expires and is purged, infections are removed on resolution)
- dependency failure: n/a - no external dependency
- state transitions: C3 (susceptible, infected, cured and immune, susceptible again; or dead)
- observability: C4 (`OutbreakStarted`), C3 (`Disease` death event)

## Out of scope

- named emergent diseases (phase 24); medicine as a profession; congenital defects - see the plan

## Deviations (kept true)

- The infection state lives in the world (`Infections`, `Immunities`, sorted lists) rather than as a field on the NPC: the lists are tiny and the NPC type, its change notifier and its serialization stay untouched. The door ("one active infection per NPC and disease, state as (diseaseId, endDay)") holds.
- Contagion is by sharing a map cell the same day (the buildings are cells too); the outbreak source is population density of a city only (the plan also named bad water, which has no state yet).
- The default scenario keeps the disease off, as the plan assumed; the canonical hash still changes once (new empty fields), so goldens and pins were re-recorded (AD-100).
- A sick NPC keeps working and moving normally; the effect is the death at the end of the infection and the immunity after it.

## Handoff

Eight source files plus tests, one builder.
