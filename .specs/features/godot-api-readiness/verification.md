# Verification - godot-api-readiness

**Verdict**: PASS
**Profile**: light
**Diff range**: a39e0bf..ca7d493 (S4 lands in 36187d2; ca7d493 touches no .NET file)
**Round**: 4 - scoped
**Verifier**: independent sub-agent (author != verifier)

## Round 4 - scoped

Verified at `ca7d493`. Scope: the new slice S4 / check C20 (commit `36187d2`: `TickLoopService.PublishDeltas()`
made public, `SimulationControlEndpoints` step and advance-year call it, new integration test, `checks.md` +C20
and a Coverage row, `plan.md` +AC 4.1). Everything else carried from `0cff1cc` (round 3 PASS); `36187d2` touches
no file cited by C1-C12, C18, C19. Regression: all proofs re-run at `ca7d493` after `bash scripts/build.sh`
(exit 0) in 2 batched invocations - unit (6 proofs) `Passed 8, Failed 0`; integration (10 proofs incl. C20)
`Passed 17, Failed 0` (round 3 had 16; +1 is C20). C20 re-run alone: `Passed 1`. The integration factory does
not start the background loop (`src/LivingWorld.Api/Program.cs:128` gates the hosted service on
`TICK_LOOP_ENABLED=true`), so the deltas the test reads can only come from the endpoints.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C20 | With a World-scope subscriber, step and advance-year each deliver a delta whose `ToCursor.Sequence` is the previous plus 1 | `--project integration ...SimulationControlEndpointsTests.Step_and_advance_year_publish_a_delta_to_subscribers` 1 passed (also in the batch of 17) | `tests/LivingWorld.Tests.Integration/Simulation/SimulationControlEndpointsTests.cs:169` - `Assert.True(reader.TryRead(out var afterStep), ...)`; `:170` - `Assert.Equal(afterStep!.FromCursor.Sequence + 1, afterStep.ToCursor.Sequence)`; `:173` - `Assert.True(reader.TryRead(out var afterYear), ...)`; `:174` - `Assert.Equal(afterStep.ToCursor.Sequence + 1, afterYear!.ToCursor.Sequence)`. Production: `src/LivingWorld.Api/Simulation/SimulationControlEndpoints.cs:46`, `:56` call `loop.PublishDeltas()`; `src/LivingWorld.Api/Simulation/TickLoopService.cs:74` | PASS |

### Faults (round 4)

Scratch `git worktree add --detach` of `ca7d493` in the session scratchpad; real tree `git status --porcelain`
empty before and after; worktree removed and pruned (`git worktree list` back to one).

| Mutant | Location | Proof | Killed |
| --- | --- | --- | --- |
| M-A1 delete `loop.PublishDeltas();` from step | `src/LivingWorld.Api/Simulation/SimulationControlEndpoints.cs:46` | C20 failed at `:169` "step publica um delta para quem assina" | yes |
| M-A2 delete `loop.PublishDeltas();` from advance-year | `src/LivingWorld.Api/Simulation/SimulationControlEndpoints.cs:56` | C20 failed at `:173` "advance-year publica um delta para quem assina" | yes |

### Round 4 notes (non-blocking)

1. Precision gap between AC 4.1 and C20: the AC says "cada escopo com assinante"; C20 proves the World scope only. City goes through the same `PublishDeltas` loop (`TickLoopService.cs:80-84`), and Interior is skipped by design (`:82-83`, pre-existing), so the AC wording over-promises for Interior.
2. C20 checks delivery and sequence continuity, not that the delta carries the post-step tick (`ToCursor.Tick`); a delta published before the clock moved would still pass.

## Round 3 record (carried from 0cff1cc)

Rows C1-C12, C18, C19 below are carried from 0cff1cc; their proofs were re-run green at `ca7d493` in round 4
(batches above) and no cited file changed since.

## Checks

Carried from 0cff1cc (round 3: verified at `0cff1cc`, every proof re-run). Citations refreshed for the one file the fix touched
(`NpcDirectoryEndpointTests.cs`, rows C7-C10); other citations carried from `9bdc54d` - those files are
unchanged by the fix.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | `Floors` >= 1, pure, order-independent | `...BuildingLayoutGeneratorTests.Floors_are_pure_and_at_least_one` 1 passed | `tests/LivingWorld.Tests.Unit/Cities/BuildingLayoutGeneratorTests.cs:18` - `Assert.InRange(first, 1, 3)`; `:24` - `Assert.Equal(forward, backward)` (carried from 9bdc54d) | PASS |
| C2 | Interior `layout` lists cells + material per floor (unit + HTTP) | `...InteriorProjectorTests.Layout_lists_cells_with_material_per_floor` 1 passed; `--project integration ...ApiContractTests.City_and_interior_shapes_match_the_committed_contract` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/InteriorProjectorTests.cs:67` - `Assert.Equal(new[] { 0, 1 }, layout.FloorLayouts.Select(f => f.Level))`; HTTP `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:67` - `layout.floors` = `floorLayouts` count (carried from 9bdc54d) | PASS |
| C3 | Floors > 1: exactly 1 `Stair` per floor at same (x,y) | `...BuildingLayoutGeneratorTests.Multi_floor_has_one_stair_at_same_xy` 1 passed | `tests/LivingWorld.Tests.Unit/Cities/BuildingLayoutGeneratorTests.cs:41` - `Assert.All(stairs, s => Assert.Single(s))`; `:42` - single distinct (x,y) (carried from 9bdc54d) | PASS |
| C4 | Every occupant (floor 0 and floor 1) has `Floor` in `[0, Floors-1]` | `...InteriorProjectorTests.Occupant_floor_within_building_floors` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/InteriorProjectorTests.cs:96-97` (carried from 9bdc54d) | PASS |
| C5 | Out-of-range floor fails, hash unchanged | `...InteriorEntryTests.Floor_out_of_range_fails_and_hash_unchanged` 3 passed | `tests/LivingWorld.Tests.Unit/Cities/InteriorEntryTests.cs:43` - `Assert.False(result.IsSuccess)`; `:45` - hash equal (carried from 9bdc54d) | PASS |
| C6 | City marker has `floors` + ground footprint (unit + HTTP) | `...CityProjectorTests.Building_marker_has_floors_and_ground_footprint` 1 passed; `ApiContractTests.City_and_interior_shapes_match_the_committed_contract` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/CityProjectorTests.cs:242`; HTTP `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:59-60` (carried from 9bdc54d) | PASS |
| C7 | `/npcs?cityId=X`: only living of X (a dead one seeded), sorted name then id on ties; empty city -> `[]`, 0 | `--project integration ...City_filter_returns_living_sorted` 1 passed; `...Dead_npcs_are_excluded_and_name_ties_break_by_id` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:68` - `Assert.Equal(new[] { "Ana", "Bia", "Caio" }, names)`; `:70-71` empty page 0/0; `:77` seeds dead "Zed"; `:83-87` allocates low then high id but adds `new[] { high, low }` (list order = reverse id order, `WorldState.Population.cs:99` `_npcs.Add`); `:98-99` total 3, no "Zed"; `:100-103` - `Assert.Equal(new[] { lowId, highId }, ...)`. Both mutants killed (see Faults) | PASS |
| C8 | `q=ana` finds "Ana" and "ANA" | `...Query_is_case_insensitive` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:114` - `Assert.Equal(2, page.GetProperty("total").GetInt32())`; `:115-116` names lower-case to "ana" | PASS |
| C9 | `take=201`, `skip=-1` -> 400 | `...Invalid_paging_returns_400` 3 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:127` - `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` over `:120` `take=201`, `:121` `skip=-1` (`:122` `take=0` beyond plan) | PASS |
| C10 | `total` same for skip=0,take=1 and skip=5,take=50 | `...Total_ignores_paging` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:138-139` - total 8 on both pages; `:140-141` item counts 1 and 3 | PASS |
| C11 | `/visual/catalog/actions` one item per `ActionType` (8) | `...ActionLabelCatalogTests.Endpoint_lists_every_action_once` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/ActionLabelCatalogTests.cs:23-24` (carried from 9bdc54d) | PASS |
| C12 | `ActionType` without label fails coverage test | `...ActionLabelCatalogTests.Every_enum_member_has_label` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/ActionLabelCatalogTests.cs:32`, `:34` (carried from 9bdc54d) | PASS |
| C18 | Recorded contract matches the new routes/responses of `Surface` | `...ApiContractTests` 6 passed | `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:110`; `tests/contracts/shape.npc-directory.json:5` `"cityId"`; `tests/contracts/openapi.v1.json:955,965` 200/400 under `/npcs` (carried from 9bdc54d) | PASS |
| C19 | Existing Player+World 403 stays intact (non-regression) | `...RealtimeGatewayEndpointTests.Subscribe_to_world_scope_as_player_is_denied_with_403_and_no_body` 1 passed | `tests/LivingWorld.Tests.Integration/Visual/RealtimeGatewayEndpointTests.cs:43`, `:45` (carried from 9bdc54d) | PASS |

## Faults

Verified at `0cff1cc`. Injected only on the surface the fix touched, in a scratch `git worktree` at HEAD
(removed afterwards; real tree `git status --porcelain` empty before and after, worktree list back to one).

| Mutant | Location | Proof | Killed |
| --- | --- | --- | --- |
| delete `.ThenBy(n => n.Id.Value)` | `src/LivingWorld.Api/NpcDirectory/NpcDirectoryEndpoints.cs:31` | `Dead_npcs_are_excluded_and_name_ties_break_by_id` failed: Expected `[25, 26]`, Actual `[26, 25]` (other 6 NpcDirectory tests passed) | yes |
| delete `.Where(n => n.IsAlive)` | `src/LivingWorld.Api/NpcDirectory/NpcDirectoryEndpoints.cs:27` | same test failed: total Expected 3, Actual 4 | yes |

## Round-2 gaps re-judged

Verified at `0cff1cc`.

| # | Round-2 gap | Now | Closed |
| --- | --- | --- | --- |
| 1 | C7 named proof did not carry "dead seeded" / "tie" | `checks.md` C7 now names both tests; the second seeds dead "Zed" and two "Ana" | yes |
| 2 | C7 tie-break assertion could not fail | high id inserted first, exact `[lowId, highId]` asserted; ThenBy mutant killed | yes |

Checks edits vs round 2: C7 only gained a Proof line; claim text unchanged. No check weakened.

## Swept (existing) re-read

Carried from `9bdc54d` - no `src/` file changed in the fix.

| Row | Cited constraint | Found |
| --- | --- | --- |
| authorization | `RealtimeGateway.Authorize` covers subscribe/replay/sse/ws; `/npcs` and `/visual/catalog/actions` public like `GET /npcs/{id}` | yes - `src/LivingWorld.Api/Realtime/RealtimeGateway.cs:25-28` from `RealtimeEndpoints.cs:22,30,38,67`; `/npcs` (`NpcDirectoryEndpoints.cs:19`) and catalog (`ActionLabelCatalog.cs:28`) unauthenticated as declared |

## Gaps (ranked)

None blocking. Residual notes:
1. The tie-break test relies on `WorldState` internals (`NextNpcIdAndAdvance`, `AddNpc`) to force list order != id order; a future change that sorts `_npcs` on insert would make it non-discriminating again without failing.
2. C9 proof also asserts `take=0` -> 400, beyond the plan's AC 2.3 (harmless extra behaviour, not in Surface text).

## Gate

Round 4 at `ca7d493`: `bash scripts/build.sh` exit 0; `bash scripts/test.sh --filter "<6 unit proofs>"` - 8 passed,
0 failed; `bash scripts/test.sh --project integration --filter "<10 integration proofs incl. C20>"` - 17 passed,
0 failed; C20 alone 1 passed. `scripts/verify.sh` not run (out of verifier scope).

Round 3 (carried from 0cff1cc): `bash scripts/build.sh` exit 0; `bash scripts/test.sh --filter "<6 unit proofs>"` - 8 passed, 0 failed;
`bash scripts/test.sh --project integration --filter "<9 integration proofs>"` - 16 passed, 0 failed; per-proof
re-runs all passed. `scripts/verify.sh` not run (out of verifier scope).
