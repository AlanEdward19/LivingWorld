# Godot server deltas checks

Profile: light
Plan: `.specs/features/godot-server-deltas/plan.md`

6 checks in 2 slices · 2 one-way doors · 1 open (client side, see Out of scope)

## Checks

### S1 - Delta de interior

**C1** - An observed interior publishes an InteriorTickDelta in the very tick its occupants change and nothing in the ticks where they do not (AC 1, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~TickLoopServiceTests.An_observed_interior"`

**C2** - The canonical hash after N ticks is equal with and without an interior subscriber (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~TickLoopServiceTests.The_canonical_hash_is_the_same"`

**C6** - The occupant differ classifies entered, moved, changed floor and exited as four distinct lists (one occupant per list in the test), the four transitions the delta carries (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InteriorOccupancyTests.Diff_classifies"`

### S2 - Calendario e NPC congelados

**C3** - `GET /world/calendar` answers 200 with hours per day, days per month and months per year of the scenario, and its shape is frozen (AC 3, door 2)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~ApiContractTests.Response_shape_matches_the_committed_contract"`

**C4** - `GET /npcs/{id}` has a frozen shape for an existing id and answers 404 for an unknown one (AC 4, door 2)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~ApiContractTests.Npc_inspection_shape"`

**C5** - The OpenAPI document and every frozen shape fail the contract tests when a frozen route changes (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~ApiContractTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 2: frozen routes (2) | calendar C3 · npc inspect C4 | - |
| interior transitions (4) | entered C6 · moved C6 · changed floor C6 · exited C6 | - |

- Claims naming a status code or response shape: C3 (200 and shape), C4 (200, 404 and shape)
- No other check claims more than the single case its proof exercises

## Swept

- validation: C4 (unknown id)
- failure modes: n/a - read-only projection
- idempotency: C1 (no delta when nothing changed)
- authorization: n/a - the interior scope keeps the existing viewer-mode rule
- concurrency: n/a - the tick loop publishes serially
- data lifecycle: n/a - the baseline per interior is dropped when nobody listens
- dependency failure: n/a - no external dependency
- state transitions: C1
- observability: C1

## Out of scope

- Godot client consuming the delta (AC 6), `client-test.sh` (AC 7) and reading the calendar - open, tracked in the issue for this spec
- final art and sound; player mode - see the plan
