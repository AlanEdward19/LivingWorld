# godot-api-readiness verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: a39e0bf..aaf950f
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Scope: checks C1-C12, C18, C19 of `checks.md` only (the range also carries `npc-building-occupancy`, judged
elsewhere). Proofs run at HEAD `aaf950f`, batched in 2 invocations (unit 15 passed / integration 15 passed,
0 failed); the unit batch reported 15 where 8 cases were expected, so every proof was re-run alone to
attribute counts: 1,1,1,1,3,1 (unit) and 1,1,3,1,1,1,6,1 (integration), all Passed, none "No test".

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | `Floors` >= 1, pure, order-independent | `scripts/test.sh --filter "...BuildingLayoutGeneratorTests.Floors_are_pure_and_at_least_one"` 1 passed | `tests/LivingWorld.Tests.Unit/Cities/BuildingLayoutGeneratorTests.cs:18` - `Assert.InRange(first, 1, 3)`; `:24` - `Assert.Equal(forward, backward)` | PASS |
| C2 | Interior `layout` lists cells + material per floor | `...InteriorProjectorTests.Layout_lists_cells_with_material_per_floor` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/InteriorProjectorTests.cs:67` - `Assert.Equal(new[] { 0, 1 }, layout.FloorLayouts.Select(f => f.Level))`; `:68` - `Assert.All(layout.FloorLayouts, f => Assert.NotEmpty(f.Cells))` (level gap: projector-level, HTTP shape only via C18) | PASS |
| C3 | Floors > 1: exactly 1 `Stair` per floor at same (x,y) | `...BuildingLayoutGeneratorTests.Multi_floor_has_one_stair_at_same_xy` 1 passed | `tests/LivingWorld.Tests.Unit/Cities/BuildingLayoutGeneratorTests.cs:41` - `Assert.All(stairs, s => Assert.Single(s))`; `:42` - `Assert.Single(stairs.Select(s => s[0]).Distinct())` | PASS |
| C4 | Every Interior occupant floor in `[0, Floors-1]` | `...InteriorProjectorTests.Occupant_floor_within_building_floors` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/InteriorProjectorTests.cs:89` - `Assert.InRange(occupant.Floor.Value, 0, snapshot.Layout.Floors - 1)` (sampling: one occupant) | PASS |
| C5 | Out-of-range floor fails, hash unchanged | `...InteriorEntryTests.Floor_out_of_range_fails_and_hash_unchanged` 3 passed (-1, 2, 99) | `tests/LivingWorld.Tests.Unit/Cities/InteriorEntryTests.cs:43` - `Assert.False(result.IsSuccess)`; `:45` - `Assert.Equal(before, WorldSnapshot.CanonicalHash(world))` | PASS |
| C6 | City marker has `floors` + ground footprint | `...CityProjectorTests.Building_marker_has_floors_and_ground_footprint` 1 passed | `tests/LivingWorld.Tests.Unit/Visual/Projection/CityProjectorTests.cs:242` - `Assert.Equal(BuildingLayoutGenerator.FloorsOf(created.Id, created.BuildingTypeId), marker.Floors)`; `:246` - `Assert.Contains(marker.GroundFootprint!, c => c.Material == BuildingMaterial.Stair)` (level gap as C2) | PASS |
| C7 | `/npcs?cityId=X`: only living of X, sorted name then id; empty city -> `[]`, 0 | `--project integration ...City_filter_returns_living_sorted` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:56` - `Assert.Equal(new[] { "Ana", "Bia", "Caio" }, names)`; `:58-59` empty page. No dead NPC is seeded and no duplicate name exists, so "only living" and "then id" have no assertion (code at `src/LivingWorld.Api/NpcDirectory/NpcDirectoryEndpoints.cs:27,31` is unproven) | FAIL |
| C8 | `q=ana` finds "Ana" and "ANA" | `...Query_is_case_insensitive` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:69` - `Assert.Equal(2, page.GetProperty("total").GetInt32())` | PASS |
| C9 | `take=201`, `skip=-1` -> 400 | `...Invalid_paging_returns_400` 3 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:82` - `Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode)` over `[InlineData("take=201")]` `:75`, `[InlineData("skip=-1")]` `:76` | PASS |
| C10 | `total` same for skip=0,take=1 and skip=5,take=50 | `...Total_ignores_paging` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:93` - `Assert.Equal(8, first...total)`; `:94` - `Assert.Equal(8, later...total)` | PASS |
| C11 | `/visual/catalog/actions` one item per `ActionType` (8) | `...ActionLabelCatalogTests.Endpoint_lists_every_action_once` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/ActionLabelCatalogTests.cs:23` - `Assert.Equal(8, expected.Length)`; `:24` - `Assert.Equal(expected, served.Order().ToArray())` | PASS |
| C12 | `ActionType` without label fails coverage test | `...ActionLabelCatalogTests.Every_enum_member_has_label` 1 passed | `tests/LivingWorld.Tests.Integration/NpcDirectory/ActionLabelCatalogTests.cs:32` - `Assert.False(string.IsNullOrWhiteSpace(ActionLabelCatalog.LabelOf(action)))`; `:34` - `Assert.Throws<ArgumentOutOfRangeException>(...)` | PASS |
| C18 | Recorded contract matches the new routes/responses of `Surface` | `...ApiContractTests` 6 passed | `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:103` - `Assert.True(File.ReadAllText(path)... == text...)`; but the frozen contract contradicts the plan: `tests/contracts/shape.npc-directory.json:5` - `"city"` where Surface/Landing door 2 fix `cityId`; `tests/contracts/openapi.v1.json` `/npcs` declares only `200`, not Surface's `400` | FAIL |
| C19 | Unauthorized scope still 403, no body | `...RealtimeGatewayEndpointTests.Subscribe_to_world_scope_as_player_is_denied_with_403_and_no_body` 1 passed | `tests/LivingWorld.Tests.Integration/Visual/RealtimeGatewayEndpointTests.cs:43` - `Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode)`; `:45` - `Assert.Empty(body)` (pre-existing test, World scope only) | PASS |

## Surface / Landing vs code

| Item | Plan | Code / contract | Covered by |
| --- | --- | --- | --- |
| `GET /visual/subscribe` Interior 200 | `layout` + `occupants` | `layout.floorLayouts[].cells[].{cell,material}`, `layout.floors`, `occupants` (`shape.interior-subscribe.json:19-37`) | C2 (unit), C18 (HTTP) |
| `GET /visual/subscribe` City 200 | `floors` + ground footprint | `floors`, `groundFootprint` (`shape.city-subscribe.json:27-28`) | C6 (unit), C18 (HTTP) |
| `GET /visual/subscribe` City/Interior 403 | 403 | `RealtimeGateway.Authorize` (`src/LivingWorld.Api/Realtime/RealtimeGateway.cs:25-28`) denies only Player+World; 403 unreachable for City/Interior | C19 (World scope only) |
| `GET /npcs` shape | `{items:[{id,name,cityId,action,location}],total}` | `NpcDirectoryItem(Id, Name, City, Action, Location)` (`NpcDirectoryEndpoints.cs:8`) -> `city` | none - mismatch |
| `GET /npcs` 200 / 400 | 200, 400 | 200 C7; 400 C9 (`take<1` also 400, beyond plan) | C7, C9 |
| `GET /visual/catalog/actions` | `[{actionType:int,label:string}]`, 200 | matches `shape.action-catalog.json:3-4` | C11 |

## Swept (existing) re-read

| Row | Cited constraint | Found |
| --- | --- | --- |
| authorization | `RealtimeGateway.Authorize` covers `/visual/*` | partial - it gates `/visual/subscribe`, `/replay`, `/sse`, `/ws` (`RealtimeEndpoints.cs:22,30,38,67`); `/visual/catalog/actions` (`ActionLabelCatalog.cs:28`) is mapped with no `Authorize`. Harmless for a static label list, but the cited constraint does not cover it |

## Gaps (ranked)

1. C18 - frozen contract and API serve `city`, plan Surface/Landing one-way door 2 fixes `cityId` - `tests/contracts/shape.npc-directory.json:5`, `src/LivingWorld.Api/NpcDirectory/NpcDirectoryEndpoints.cs:8`. Also `openapi.v1.json` omits `/npcs` 400.
2. C7 - "only living" and "then by id" unasserted (no dead NPC, no tied name) - `NpcDirectoryEndpointTests.cs:49-59`.
3. Level gap - checks.md says C2/C6 proofs cross the boundary; they are projector unit tests (HTTP shape only via C18, values not asserted at HTTP).
4. Surface lists 403 for City/Interior subscribe, unreachable in code; C19 proves World scope only.
5. Swept authorization row overstates coverage of `/visual/*` (catalog route ungated).
6. Sampling: C4 proves a single occupant on floor 1 for "every occupant".

## Gate

`bash scripts/test.sh --filter "<6 unit proofs>"` - 15 passed, 0 failed; `bash scripts/test.sh --project integration --filter "<8 integration proofs>"` - 15 passed, 0 failed; per-proof re-runs all passed.
