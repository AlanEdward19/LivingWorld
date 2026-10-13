# Godot server deltas verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 46655cf..1062d10 (server slice C1-C6 landed on develop in 822f519, sanity-checked; client slice C7-C10 judged in full)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

All 10 checks are proven with located evidence and every named test exists and ran green at 1062d10.
The FAIL comes from one surviving mutant: the client store can drop `changedFloor` entirely and the
whole client suite stays green (57/57). No check claims client handling of `changedFloor`, so the
fourth interior transition is unproven on the client side even though the server sends it.

## Binding sources

Profile is light, so step 1 does not run. The plan marks no source as binding (`STATE.md` and the
`godot-client` plan are listed as context only).

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | interior delta in the tick occupants change, none otherwise | integration batch, `TickLoopServiceTests.An_observed_interior_publishes_a_delta_in_the_very_tick_its_occupants_change_and_never_otherwise` Passed | `tests/LivingWorld.Tests.Integration/Simulation/TickLoopServiceTests.cs:181` - `Assert.Equal(changed, got)`; :182 `Assert.IsType<InteriorTickDelta>(envelope!.Payload)`; :187 `Assert.True(changes > 0, ...)` | PASS |
| C2 | canonical hash equal with and without interior subscriber | integration batch, `TickLoopServiceTests.The_canonical_hash_is_the_same_with_and_without_an_interior_subscriber` Passed | `TickLoopServiceTests.cs:210` - `Assert.Equal(Run(observe: false), Run(observe: true))` | PASS |
| C3 | `GET /world/calendar` 200 with hours/day, days/month, months/year, frozen | integration batch, `ApiContractTests.Response_shape_matches_the_committed_contract(name: "world-calendar")` Passed | `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:34` InlineData world-calendar; :38 `Assert.Equal(HttpStatusCode.OK, response.StatusCode)`; :41 `AssertMatchesContract`; `tests/contracts/shape.world-calendar.json` has hoursPerDay, daysPerMonth, monthsPerYear | PASS |
| C4 | `GET /npcs/{id}` frozen shape and 404 for unknown id | integration batch, `ApiContractTests.Npc_inspection_shape_matches_the_committed_contract_and_an_unknown_id_is_404` Passed | `ApiContractTests.cs:54` - `AssertMatchesContract("shape.npc-inspect.json", ...)`; :56 `Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/npcs/987654321")).StatusCode)` | PASS |
| C5 | OpenAPI and every frozen shape fail the contract tests on change | integration batch, all 9 ApiContractTests Passed (OpenApi, 5 route shapes, npc-inspect, city/interior, delta shapes) | `ApiContractTests.cs:161` - `Assert.True(File.ReadAllText(path)... == text..., "contrato {file} mudou ...")`; :26 OpenAPI `AssertMatchesContract("openapi.v1.json", ...)` | PASS |
| C6 | differ classifies entered, moved, changed floor, exited as four lists | unit, `InteriorOccupancyTests.Diff_classifies_entered_moved_changed_floor_and_exited_correctly` Passed | `tests/LivingWorld.Tests.Unit/Cities/InteriorOccupancyTests.cs:183-186` - `Assert.Equal([entered], delta.Entered)`, `[movedAfter]` Moved, `[floorChangedAfter]` ChangedFloor, `[exited.Npc]` Exited | PASS |
| C7 | client applies entered, moved, exited to store and scene without re-subscribing | `bash scripts/client-test.sh` (one batch, 57 passed) - `ok test_app::test_interior_delta_updates_occupants_without_polling` | `client/godot/tests/test_app.gd:86` `eq(...occupant_nodes.keys().size(), 2, "quem entrou aparece")`; :90 `eq(...occupant_nodes[2].action, 1, "mudar de ação atualiza o NPC")` (action-only moved); :93 `eq(...keys(), [2], "quem saiu some")`; :94 `eq(transport.count_calls("GET /visual/subscribe?scope=Interior"), calls_before, ...)` | PASS |
| C8 | action-only change in same cell is `moved`; unchanged occupant is not | unit, `InteriorOccupancyTests.Diff_reports_an_action_change_in_the_same_cell_as_moved` Passed | `InteriorOccupancyTests.cs:197` - `Assert.Equal([eating], delta.Moved)`; :198-200 Entered/ChangedFloor/Exited empty; :201 `Assert.Empty(InteriorOccupancyDiffer.Diff([working], [working]).Moved)` | PASS |
| C9 | client reads `/world/calendar` and uses it for hour, day, month, year; default 24/30/12 | client batch - `ok test_app::test_calendar_comes_from_world_calendar_route`, `ok test_ui_modules::test_calendar_from_server_drives_hour_and_day` | `test_app.gd:164-166` - `eq(app.day_cycle.hours_per_day, 12.0, ...)`, days_per_month 10, months_per_year 4; `test_ui_modules.gd:470` `eq(cycle.clock_text(), "12:00", ...)`; :476 `eq(date, {"year": 2, "month": 2, "day": 4}, ...)`; :478 default `{"year": 1, "month": 2, "day": 2}` | PASS |
| C10 | ApiClient validates npc-inspect; stream discards world, city or interior delta breaking contract; null action accepted | client batch - `ok test_shape_validator::test_api_client_validates_npc_detail_shape`, `ok ...test_stream_discards_a_delta_that_breaks_the_contract`, `ok ...test_stream_validates_interior_deltas_and_accepts_null_action` | `client/godot/tests/test_shape_validator.gd:98` ok true, :103 `eq((await api.npc_detail(7)).ok, false, "campo ausente é rejeitado")`; :126 applied 1, then broken npcUpserts discarded with 1 contract error; :158 `eq(applied.size(), 1, "delta de interior com ação nula é aceito")`, then missing `exited` discarded with 1 error | PASS |

Notes on the checks:
- C1 filter `An_observed_interior` matches two tests (`..._very_tick_...` and `..._publishes_only_what_changed_and_nothing_when_nobody_moves`); both Passed.
- C10 proves the `City` mapping only through `DELTA_SHAPE_BY_SCOPE` in `client/godot/Contract/cursor_stream.gd:16` ("City": "visual-delta"); no test pushes a City-scoped delta. Same shape as World, so low risk.
- Field names: the client reads exactly `entered`, `moved`, `changedFloor`, `exited`, `npc`, `floor`, `localCell`, `action` (`client/godot/Store/world_store.gd:183-187`, `Scenes/interior_view.gd:128-132`), matching `tests/contracts/shape.interior-delta.json`; `{value}` wrappers are unwrapped by `id_of`. Wire serializer is `JsonSerializerDefaults.Web` (`src/LivingWorld.Api/Realtime/RealtimeEndpoints.cs:143`), same as the contract test (`ApiContractTests.cs:91`).

## Coverage

Profile is light: the Coverage join is not recomputed as a gate. Read-through only - the plan's
interior transitions set (4) is proven on the server (C6) but on the client only for 3 members; the
`changedFloor` member has no client proof (see Faults).

## Test policy rows

`checks.md` carries no Test policy section.

## Faults injected

Scratch worktree at 1062d10 (`git worktree add --detach`), real tree porcelain baseline empty and
identical after removal.

| Mutation | Location | Killed |
| --- | --- | --- |
| store ignores `moved` (loop over entered and changedFloor only) | `client/godot/Store/world_store.gd:185` | yes - test_interior_delta_updates_occupants_without_polling FAIL |
| store ignores `exited` (erase replaced by pass) | `client/godot/Store/world_store.gd:184` | yes - test_interior_delta_updates_occupants_without_polling FAIL |
| differ drops `b.Action != a.Action` from moved | `src/LivingWorld.Api/Visual/Projection/InteriorOccupancyDiffer.cs:30` | yes - Diff_reports_an_action_change_in_the_same_cell_as_moved Failed (C8) |
| app never calls `api.world_calendar()` | `client/godot/Scenes/app.gd:89` | yes - test_calendar_comes_from_world_calendar_route FAIL |
| store ignores `changedFloor` (loop over entered and moved only) | `client/godot/Store/world_store.gd:185` | no - full client suite 57 passed, 0 failed |

## Findings (ranked)

1. Client `changedFloor` handling is unproven: the surviving mutant above. `_interior_delta` helper in `client/godot/tests/test_app.gd:73` hardcodes `"changedFloor": []`, so no client test ever sends a floor change. A wrong merge would leave an NPC on the old floor in the interior view (`interior_view.gd:143` floor meta). Needs a check plus a test step that moves an occupant to floor 1 and asserts its floor/visibility.
2. Precision gap in the frozen delta contracts: `shape.interior-delta.json` records occupant `action` as `"null"` and `shape.visual-delta.json` records `currentAction` as `"null"`, which the client validator treats as "any value" (`client/godot/Contract/shape_validator.gd:66`). The non-null action type (currently a number, no string-enum converter in the Api) is therefore not frozen by either side; a switch to string enums would pass both contract tests and break `int(occupant.action)`.
3. `isEmpty` (a computed property, `src/LivingWorld.Api/Visual/Scope/InteriorTickDelta.cs:16`) is now part of the frozen wire contract and required by the client validator. Not wrong, but an accidental public surface.
4. Stale comments: `client/godot/Scenes/day_cycle.gd:3` and `:13` still say the day length comes from `/simulation/status`; `day_cycle.sync(..., calendar)` keeps a calendar parameter production never passes (`app.gd:257`); `test_shape_validator.gd:59,69` status fixtures still carry hoursPerDay/daysPerMonth/monthsPerYear. No stale `upserts`/`removed` interior handling remains in client or src (world delta `removed`/`npcRemoved` are real server fields).
5. Minor: the store applies an interior delta without checking `payload.buildingId` against the open interior (`world_store.gd:178`); safe only because the stream is scope-filtered. C2 subscribes to `Buildings.First()`, which may have no occupants in 5 ticks, so the hash equality is measured with little publishing activity.

## Gate

- `bash scripts/client-test.sh` - 57 passed, 0 failed (all 6 named client proofs `ok`)
- `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~ApiContractTests|FullyQualifiedName~TickLoopServiceTests"` - 17 passed, 0 failed
- `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~InteriorOccupancyTests"` - 10 passed, 0 failed
- (`scripts/test.sh` was not used for the .NET runs only because it does not pass a logger through; same project, configuration and filter.)
