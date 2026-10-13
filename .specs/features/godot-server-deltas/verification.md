# Godot server deltas verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 46655cf..f90aaff (round 2 scoped to the fix 1062d10..f90aaff)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Round 1 (FAIL at 1062d10) had one blocking gap: the client store could drop `changedFloor` and the
client suite stayed green. The fix (f90aaff) adds C11 and
`test_app::test_interior_delta_moves_an_occupant_to_another_floor`, plus cosmetic cleanup of stale
calendar references. The round-1 surviving mutant was re-injected at f90aaff and is now killed.
All proofs re-ran in full at f90aaff and are green. Rows the fix could not touch are carried.

## Binding sources

Carried from 1062d10: profile is light, step 1 does not run; the plan marks no binding source.

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | interior delta in the tick occupants change, none otherwise | integration batch at f90aaff, 17 passed | carried from 1062d10: `tests/LivingWorld.Tests.Integration/Simulation/TickLoopServiceTests.cs:181` `Assert.Equal(changed, got)`; :182 `Assert.IsType<InteriorTickDelta>` | PASS |
| C2 | canonical hash equal with and without interior subscriber | integration batch at f90aaff | carried from 1062d10: `TickLoopServiceTests.cs:210` `Assert.Equal(Run(observe: false), Run(observe: true))` | PASS |
| C3 | `GET /world/calendar` 200 with hours/day, days/month, months/year, frozen | integration batch at f90aaff | carried from 1062d10: `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:38` OK status; :41 `AssertMatchesContract` | PASS |
| C4 | `GET /npcs/{id}` frozen shape and 404 for unknown id | integration batch at f90aaff | carried from 1062d10: `ApiContractTests.cs:54` shape; :56 NotFound for `/npcs/987654321` | PASS |
| C5 | OpenAPI and every frozen shape fail the contract tests on change | integration batch at f90aaff | carried from 1062d10: `ApiContractTests.cs:161` file-equality assertion; :26 OpenAPI | PASS |
| C6 | differ classifies entered, moved, changed floor, exited as four lists | unit batch at f90aaff, 10 passed | carried from 1062d10: `tests/LivingWorld.Tests.Unit/Cities/InteriorOccupancyTests.cs:183-186` | PASS |
| C7 | client applies entered, moved, exited without re-subscribing | client batch at f90aaff, 58 passed, `ok test_app::test_interior_delta_updates_occupants_without_polling` | carried from 1062d10 (lines unmoved): `client/godot/tests/test_app.gd:86`, :90, :93, :94 | PASS |
| C8 | action-only change in same cell is `moved`; unchanged is not | unit batch at f90aaff | carried from 1062d10: `InteriorOccupancyTests.cs:197`, :201 | PASS |
| C9 | client reads `/world/calendar` for hour, day, month, year; default 24/30/12 | client batch at f90aaff, both named proofs ok | verified at f90aaff (citation moved): `client/godot/tests/test_app.gd:181-183` hours 12.0, days 10, months 4; carried from 1062d10: `client/godot/tests/test_ui_modules.gd:470`, :476, :478 | PASS |
| C10 | ApiClient validates npc-inspect; stream discards broken world/city/interior delta; null action accepted | client batch at f90aaff, all three named proofs ok | carried from 1062d10: `client/godot/tests/test_shape_validator.gd:98`, :103, :126, :158 (fix only edited fixtures at :59 and :69 of an unrelated status test) | PASS |
| C11 | `changedFloor` occupant changes floor in store and scene (floor and height), no re-read | client batch at f90aaff, `ok test_app::test_interior_delta_moves_an_occupant_to_another_floor` | verified at f90aaff: `client/godot/tests/test_app.gd:103` precondition floor 1; :109 store floor 0; :110 scene floor meta 0; :111 `position.y` 0.0 | PASS |

## Coverage

Profile is light: not a gate. Read-through: the interior transitions set (4) is now proven on the
server (C6) and on the client (entered, moved, exited by C7; changed floor by C11).

## Test policy rows

`checks.md` carries no Test policy section.

## Faults injected

Scratch worktree at f90aaff (`git worktree add --detach` in the scratchpad), removed after; real
tree porcelain empty before and after. Rows from round 1 are carried from 1062d10.

| Mutation | Location | Killed |
| --- | --- | --- |
| store ignores `moved` | `client/godot/Store/world_store.gd:185` | yes - carried from 1062d10 |
| store ignores `exited` | `client/godot/Store/world_store.gd:184` | yes - carried from 1062d10 |
| differ drops action change from moved | `src/LivingWorld.Api/Visual/Projection/InteriorOccupancyDiffer.cs:30` | yes - carried from 1062d10 (C8) |
| app never calls `api.world_calendar()` | `client/godot/Scenes/app.gd:89` | yes - carried from 1062d10 |
| store ignores `changedFloor` (merge loop over entered and moved only) | `client/godot/Store/world_store.gd:185` | yes - verified at f90aaff: test_interior_delta_moves_an_occupant_to_another_floor FAIL, suite 57 passed 1 failed |

## Findings (ranked)

Accepted, non-blocking (carried from 1062d10):
1. `"null"` fields in `shape.interior-delta.json` (occupant `action`) and `shape.visual-delta.json` (`currentAction`) accept any type in `client/godot/Contract/shape_validator.gd:66`; the non-null action type is not frozen.
2. `isEmpty` (computed, `src/LivingWorld.Api/Visual/Scope/InteriorTickDelta.cs:16`) is on the frozen wire contract.
3. The store does not check `payload.buildingId` (`client/godot/Store/world_store.gd:177`); safe because the stream is scope-filtered.

New, minor:
4. Stale comment survives the cleanup: `client/godot/Scenes/day_cycle.gd:2-3` still says the client interpolates between `/simulation/status` readings with a 24 h day (line 13 was fixed).
5. C11 reads `app.store.interior.occupants[0]` (`test_app.gd:109`), relying on insertion order of the merged dictionary; correct today, brittle if the fixture gains occupants.

## Gate

- `bash scripts/client-test.sh` at f90aaff - 58 passed, 0 failed (C7, C9 x2, C10 x3, C11 named proofs all `ok`)
- `bash scripts/test.sh --project integration --filter "FullyQualifiedName~ApiContractTests|FullyQualifiedName~TickLoopServiceTests"` - 17 passed, 0 failed, exit 0
- `bash scripts/test.sh --filter "FullyQualifiedName~InteriorOccupancyTests"` - 10 passed, 0 failed, exit 0
