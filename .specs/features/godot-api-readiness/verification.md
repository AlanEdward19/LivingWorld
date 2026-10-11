# godot-api-readiness verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: a39e0bf..9bdc54d
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

Scope: checks C1-C12, C18, C19 of `checks.md` (the range also carries `npc-building-occupancy`, judged
elsewhere). Round 1 was verified at `aaf950f`; the fix diff is `aaf950f..9bdc54d` (`git show HEAD`).
All proofs re-run in full at HEAD `9bdc54d` after `scripts/build.sh` (exit 0), batched in 2 invocations:
unit 8 passed / integration 16 passed, 0 failed. Each proof also re-run alone to attribute counts:
unit 1,1,1,1,3,1; integration 1 (C7) + 1 (`Dead_npcs_are_excluded_and_name_ties_break_by_id`, not named
by any check),1,3,1,1,1, `ApiContractTests` 6 (of which `City_and_interior_shapes_match_the_committed_contract` 1), 1. None "No test".

## Checks

Verified at `9bdc54d` (proofs re-run for every row; citations refreshed for the 3 test files the fix touched,
the rest carried from `aaf950f` - those files are unchanged by the fix).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | `Floors` >= 1, pure, order-independent | `...BuildingLayoutGeneratorTests.Floors_are_pure_and_at_least_one` 1 passed | `tests/LivingWorld.Tests.Unit/Cities/BuildingLayoutGeneratorTests.cs:18` - `Assert.InRange(first, 1, 3)`; `:24` - `Assert.Equal(forward, backward)` (carried from aaf950f) | PASS |
| C2 | Interior `layout` lists cells + material per floor (unit + HTTP) | `...InteriorProjectorTests.Layout_lists_cells_with_material_per_floor` 1 passed; `--project integration ...ApiContractTests.City_and_interior_shapes_match_the_committed_contract` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/InteriorProjectorTests.cs:67` - `Assert.Equal(new[] { 0, 1 }, layout.FloorLayouts.Select(f => f.Level))`; `:69-70` Door/Stair single per floor; HTTP `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:67` - `Assert.Equal(layout["floors"]!.GetValue<int>(), layout["floorLayouts"]!.AsArray().Count)` + shape `:110` vs `shape.interior-subscribe.json` | PASS |
| C3 | Floors > 1: exactly 1 `Stair` per floor at same (x,y) | `...BuildingLayoutGeneratorTests.Multi_floor_has_one_stair_at_same_xy` 1 passed | `tests/LivingWorld.Tests.Unit/Cities/BuildingLayoutGeneratorTests.cs:41` - `Assert.All(stairs, s => Assert.Single(s))`; `:42` - `Assert.Single(stairs.Select(s => s[0]).Distinct())` (carried from aaf950f) | PASS |
| C4 | Every occupant (one on floor 0, one on floor 1) has `Floor` in `[0, Floors-1]` | `...InteriorProjectorTests.Occupant_floor_within_building_floors` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/InteriorProjectorTests.cs:96` - `Assert.Equal(new[] { 0, 1 }, snapshot.Occupants.Select(o => o.Floor.Value).Order())`; `:97` - `Assert.All(snapshot.Occupants, o => Assert.InRange(o.Floor.Value, 0, snapshot.Layout.Floors - 1))` | PASS |
| C5 | Out-of-range floor fails, hash unchanged | `...InteriorEntryTests.Floor_out_of_range_fails_and_hash_unchanged` 3 passed (-1, 2, 99) | `tests/LivingWorld.Tests.Unit/Cities/InteriorEntryTests.cs:43` - `Assert.False(result.IsSuccess)`; `:45` - `Assert.Equal(before, WorldSnapshot.CanonicalHash(world))` (carried from aaf950f) | PASS |
| C6 | City marker has `floors` + ground footprint (unit + HTTP) | `...CityProjectorTests.Building_marker_has_floors_and_ground_footprint` 1 passed; `ApiContractTests.City_and_interior_shapes_match_the_committed_contract` 1 passed | unit `tests/LivingWorld.Tests.Unit/Visual/Projection/CityProjectorTests.cs:242` - `Assert.Equal(BuildingLayoutGenerator.FloorsOf(...), marker.Floors)` (carried); HTTP `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:59` - `Assert.True(marker!["floors"]!.GetValue<int>() >= 1)`; `:60` - `Assert.Contains(marker["groundFootprint"]!.AsArray(), c => c!["material"]!.GetValue<int>() == 2)` (Door = 2 per `BuildingMaterial.cs:9-11`), looped over every marker | PASS |
| C7 | `/npcs?cityId=X`: only living of X (a dead one seeded), sorted name then id on ties; empty city -> `[]`, 0 | `--project integration ...City_filter_returns_living_sorted` 1 passed | Named proof `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:61` seeds `SeedCity("Caio", "Ana", "Bia")` - no dead NPC, no tie; asserts only `:68` `Assert.Equal(new[] { "Ana", "Bia", "Caio" }, names)` and `:70-71` empty page. The dead/tie evidence lives in a different test the check does not name (`Dead_npcs_are_excluded_and_name_ties_break_by_id`, `:82-83` total 3 / no "Zed" - discriminating; `:86` `Assert.Equal(anaIds.Order().ToArray(), anaIds)` - non-discriminating: the two "Ana" are inserted in id order into a `List` (`WorldState.Economy.cs:23`), and `OrderBy` is stable, so deleting `.ThenBy(n => n.Id.Value)` at `NpcDirectoryEndpoints.cs:31` still passes) | FAIL |
| C8 | `q=ana` finds "Ana" and "ANA" | `...Query_is_case_insensitive` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:97` - `Assert.Equal(2, page.GetProperty("total").GetInt32())`; `:98-99` every name lower-cases to "ana" | PASS |
| C9 | `take=201`, `skip=-1` -> 400 | `...Invalid_paging_returns_400` 3 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:110` - `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` over `[InlineData("take=201")]` `:103`, `[InlineData("skip=-1")]` `:104` | PASS |
| C10 | `total` same for skip=0,take=1 and skip=5,take=50 | `...Total_ignores_paging` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:121` - `Assert.Equal(8, first.GetProperty("total").GetInt32())`; `:122` - `Assert.Equal(8, later.GetProperty("total").GetInt32())` | PASS |
| C11 | `/visual/catalog/actions` one item per `ActionType` (8) | `...ActionLabelCatalogTests.Endpoint_lists_every_action_once` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/ActionLabelCatalogTests.cs:23` - `Assert.Equal(8, expected.Length)`; `:24` - `Assert.Equal(expected, served.Order().ToArray())` (carried from aaf950f) | PASS |
| C12 | `ActionType` without label fails coverage test | `...ActionLabelCatalogTests.Every_enum_member_has_label` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/ActionLabelCatalogTests.cs:32` - `Assert.False(string.IsNullOrWhiteSpace(ActionLabelCatalog.LabelOf(action)), ...)`; `:34` - `Assert.Throws<ArgumentOutOfRangeException>(...)` (carried from aaf950f) | PASS |
| C18 | Recorded contract matches the new routes/responses of `Surface` | `...ApiContractTests` 6 passed | `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:110` - `Assert.True(File.ReadAllText(path)... == text...)`; contract now `tests/contracts/shape.npc-directory.json:5` `"cityId"`, `tests/contracts/openapi.v1.json:955` `"200"` and `:965` `"400"` under `/npcs` (`:911`); code `NpcDirectoryEndpoints.cs:8` `CityId CityId`, `:39-40` `.Produces` 200/400 | PASS |
| C19 | Existing Player+World 403 stays intact (non-regression; plan lists no 403 for City/Interior) | `...RealtimeGatewayEndpointTests.Subscribe_to_world_scope_as_player_is_denied_with_403_and_no_body` 1 passed | `tests/LivingWorld.Tests.Integration/Visual/RealtimeGatewayEndpointTests.cs:43` - `Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode)`; `:45` - `Assert.Empty(body)` (carried from aaf950f) | PASS |

## Round-1 gaps re-judged

Verified at `9bdc54d`.

| # | Round-1 gap | Now | Closed |
| --- | --- | --- | --- |
| 1 | `/npcs` served `city`; openapi lacked 400 | code `NpcDirectoryEndpoints.cs:8`, contract `shape.npc-directory.json:5`, plan Surface `plan.md:38` and Landing door 2 `plan.md:45` all `cityId`; openapi `/npcs` 200 + 400 | yes |
| 2 | C7 "only living" / "then id" unasserted | a new test seeds dead "Zed" and two "Ana", but C7's Proof still names the old test; tie assertion cannot fail (see C7) | no |
| 3 | C2/C6 level gap | HTTP proof added; `:59`, `:60`, `:67` assert values, not only shape | yes |
| 4 | Surface 403 for City/Interior unreachable | plan Surface rows now `200` only; Coverage row `GET /visual/subscribe statuses (1) 200 C2` | yes |
| 5 | Swept authorization overstated | see below - accurate | yes |
| 6 | C4 sampled one occupant | two occupants, floors 0 and 1 (`:92`, `:96-97`) | yes |

Checks edits vs round 1: C2/C6 gained a second proof, C4 and C7 claims got more specific (tighter). C19 was
narrowed from "any unauthorized scope -> 403" to "the existing Player+World 403"; that equals exactly what
round 1 verified (World scope only), and it follows the plan's Surface edit that dropped a 403 the code never
served, so no previously verified claim got weaker. Note: C7's tightened claim text ("um morto é semeado") is
now false of its own named proof.

## Swept (existing) re-read

Verified at `9bdc54d`.

| Row | Cited constraint | Found |
| --- | --- | --- |
| authorization | `RealtimeGateway.Authorize` covers subscribe/replay/sse/ws; `/npcs` and `/visual/catalog/actions` public like `GET /npcs/{id}` | yes - `Authorize` at `src/LivingWorld.Api/Realtime/RealtimeGateway.cs:25-28` is called from the four routes (`RealtimeEndpoints.cs:22,30,38,67`); `/npcs` (`NpcDirectoryEndpoints.cs:19`), `/visual/catalog/actions` (`ActionLabelCatalog.cs:28`) and `/npcs/{id:long}` (`Program.cs:145`) map with no auth; no `UseAuthorization`/`RequireAuthorization` anywhere in `src/LivingWorld.Api` |

## Gaps (ranked)

1. C7 - proof line does not carry the claim: `City_filter_returns_living_sorted` (`NpcDirectoryEndpointTests.cs:61-71`) seeds no dead NPC and no name tie, yet the claim says one is seeded. Fix: name `Dead_npcs_are_excluded_and_name_ties_break_by_id` as a C7 proof.
2. C7 - tie-break assertion is non-discriminating: `NpcDirectoryEndpointTests.cs:86` passes with `.ThenBy(n => n.Id.Value)` removed (`NpcDirectoryEndpoints.cs:31`), because ties are inserted in id order and `OrderBy` is stable. Seed the tied names in reverse id order (construct the tied NPCs so list order differs from id order) so dropping `ThenBy` breaks it.

## Gate

`bash scripts/test.sh --filter "<6 unit proofs>"` - 8 passed, 0 failed; `bash scripts/test.sh --project integration --filter "<8 integration proofs + Dead_npcs...>"` - 16 passed, 0 failed; per-proof re-runs all passed. `scripts/verify.sh` not run (out of verifier scope).
