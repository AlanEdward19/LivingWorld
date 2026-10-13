# Estate inheritance checks

Profile: light
Plan: `.specs/features/estate-inheritance/plan.md`

7 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Espólio conservado

**C1** - A dead NPC's wallet goes 100% to the living spouse, else the children (equal parts, remainder to the lowest id), else the parents, else the siblings, and the world balance is unchanged (AC 1, AC 3, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~EstateSystemTests.The_living_spouse_inherits|FullyQualifiedName~EstateSystemTests.Children_split_equally|FullyQualifiedName~EstateSystemTests.Without_spouse_or_children"`

**C2** - With no living heir the wallet goes to the city treasury (or stays with the dead NPC when there is no city) and the balance is unchanged (AC 2, AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~EstateSystemTests.Without_any_heir"`

**C3** - Over a simulated year of natural deaths the money identity `wallets + treasuries == initial + minted - destroyed` holds every day, and at least one NPC died (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~EstateSystemTests.Money_stays_balanced"`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MoneyConservationTests"`

**C4** - The pantry of a household left without anyone moves to the first heir's household instead of being logged as lost (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~EstateSystemTests.The_pantry_left"`

### S2 - Reação social

**C5** - Each heir gets an inheritance relationship with the dead (trust and affection rise) (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~EstateSystemTests.An_heir_gets"`

**C6** - A workplace owned by the dead passes to the lowest-id heir present there, else the lowest-id heir, else to nobody (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~EstateSystemTests.The_owned_workplace"`

**C7** - µs per NPC-tick stays within 103% and allocation per tick within 102% of the value measured before, and two processes agree on the hash (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness|FullyQualifiedName~DeterminismTwoProcessTests"`; numbers go in `verification.md`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: inheritance order (5) | spouse C1 · children C1 · parents C1 · siblings C1 · city C2 | - |
| heir outcomes for a workplace (3) | present heir C6 · absent heir C6 · no heir C6 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: n/a - input is the world state at death, no external input
- failure modes: C2 (no heir, no city: the money stays, nothing vanishes)
- idempotency: n/a - `NpcDeath.Apply` runs once per death and a dead NPC holds no wallet afterwards (C1)
- authorization: n/a - local simulation, no caller identity
- concurrency: n/a - single-threaded tick
- data lifecycle: C4 (residual pantry), C6 (owner cleared with no heir)
- dependency failure: n/a - no external dependency
- state transitions: C6 (owner -> heir -> nobody)
- observability: C1 (`EstateSettled` event payload), C5 (relationship)

## Out of scope

- testament and disinheritance; tax on inheritance; extraordinary-power inheritance - see the plan

## Deviations (kept true)

- `Workplace.Owner` is new (the plan assumed it existed). It is only assigned by authoring or by inheritance; no system makes a worker an owner yet, so owned workplaces appear only in authored scenarios. Not serialized when null, so worlds without owners keep their snapshot bytes.
- The plan said the owner's workplace goes to the lowest-id heir *present*; when none is present it goes to the lowest-id heir (ownership does not vanish with an absent heir).
- The household stock rule is narrower than "carteira e estoque": only the pantry of a household left empty moves (to the first heir's household); a household that keeps members keeps its pantry.
- The money rule changes behavior of the whole world (money from the dead recirculates instead of freezing in a dead wallet), so goldens, pins and population floors were re-recorded (AD-094).

- `HeirsOf` scans `world.Npcs` twice per death with an estate (O(N)); fine up to a few thousand NPCs, an index by parent is the upgrade path (poor NPCs skip it). µs per NPC-tick measured +2.4% and +3.1% against HEAD in two A/B runs on a box with ±7% run-to-run noise; allocation +0.01%.
- `Workplace.Owner` round trip (and no `Owner` bytes when null) is covered by `An_owner_survives_a_snapshot_round_trip...`.

## Handoff

Seven source files plus tests, one builder.
