# Godot server deltas checks

Profile: light
Plan: `.specs/features/godot-server-deltas/plan.md`

10 checks in 3 slices · 2 one-way doors · 0 open

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

### S3 - Cliente (AC 6; o AC 7 é o job `client` do CI, que roda `scripts/client-test.sh` inteiro)

**C7** - O cliente aplica o delta de interior (`entered`, `moved`, `exited`) à store e à cena sem pedir `GET /visual/subscribe` de novo (AC 6)
Proof: `bash scripts/client-test.sh --filter "test_app::test_interior_delta_updates_occupants_without_polling"`

**C8** - O delta de interior inclui como `moved` o ocupante que só mudou de ação (mesmo andar e célula), e não inclui quem não mudou (AC 1)
Proof: `bash scripts/test.sh --filter "FullyQualifiedName~InteriorOccupancyTests.Diff_reports_an_action_change_in_the_same_cell_as_moved"`

**C9** - O cliente lê `GET /world/calendar` e usa as durações do cenário para hora, dia, mês e ano; sem calendário vale 24/30/12 (AC 3)
Proof: `bash scripts/client-test.sh --filter "test_app::test_calendar_comes_from_world_calendar_route"`
Proof: `bash scripts/client-test.sh --filter "test_ui_modules::test_calendar_from_server_drives_hour_and_day"`

**C10** - O `ApiClient` valida `GET /npcs/{id}` contra `shape.npc-inspect.json` e o stream descarta delta de mundo, cidade ou interior que quebre o contrato, aceitando ação nula (AC 5)
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_api_client_validates_npc_detail_shape"`
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_stream_discards_a_delta_that_breaks_the_contract"`
Proof: `bash scripts/client-test.sh --filter "test_shape_validator::test_stream_validates_interior_deltas_and_accepts_null_action"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 2: frozen routes (2) | calendar C3 · npc inspect C4 | - |
| deltas congelados no cliente (3) | mundo C10 · cidade C10 · interior C10 | - |
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

- final art and sound; player mode - see the plan
