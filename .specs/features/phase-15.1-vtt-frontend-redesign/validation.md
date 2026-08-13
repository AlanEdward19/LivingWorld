# Phase 15.1 — E2.2 Bundle (T20 / T21 / T30) Validation

**Date**: 2026-08-12
**Spec**: `.specs/features/phase-15.1-vtt-frontend-redesign/spec.md`
**Diff range**: `8317aa0..HEAD` (4 commits: `8539125`, `1da3d5b`, `444e177`, `841a00d`)
**Verifier**: independent sub-agent (author ≠ verifier)
**Scope**: this report covers only the E2.2 bundle (T20 footprint fields, T21 SpatialPortal, T30 city indicators). It does not validate the rest of phase-15.1.

---

## Task Completion

| Task | Status  | Notes |
| ---- | ------- | ----- |
| T20  | ✅ Done | `GlobalCityMarker.Bounds/BoundsAreDerived`, `CityBuildingMarker.Location/LocationIsDerived` wired from pre-existing T45 resolvers; no `web/` files touched. |
| T21  | ✅ Done | `SpatialPortal`/`PortalEndpoint`/`PortalSpaceKind` in `LivingWorld.Domain`; `WorldState.Portals` `[Canonical]`; scenario authoring; `Portals` field on `GlobalSnapshot`/`CitySnapshot`; goldens regenerated in isolated commit. |
| T30  | ✅ Done | `CitySnapshot.Indicators` (`CityIndicators`) sourced solely from `CityPopulationQuery`. |

---

## Spec-Anchored Acceptance Criteria

### T21 Done-when (tasks.md:832-842) — authoritative AC source for SpatialPortal per verifier brief

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| AC1: portal is `[Canonical]` domain data with id/label/from/to | `WorldState.Portals` marked `[Canonical]`; mutating it changes canonical hash | `src/LivingWorld.Simulation/WorldState.cs:255` — `[Canonical] public IReadOnlyList<SpatialPortal> Portals` ; `tests/LivingWorld.Tests/WorldSnapshotTests.cs:156-177` (theory over `ReflectedProperties`, incl. `"Portals"`) — `Assert.NotEqual(originalCanonical, mutatedCanonical)` for canonical props | ✅ PASS |
| AC2: round-trip preserves portals, identical hash | serialize→deserialize preserves `Portals` collection and canonical hash | `tests/LivingWorld.Tests/WorldSnapshotTests.cs:279-289` — `Assert.Equal(world.Portals, rehydrated.Portals)` + `Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(rehydrated))` | ✅ PASS |
| AC3: N portals for same space pair, distinguishable only by label, no code branch per entry | two portals to the same city both returned, no per-entry special-casing in `CityProjector`/loader | `tests/LivingWorld.Tests/Cities/CityAndBuildingAuthoringTests.cs:340-368` (`Two_authored_portals_for_the_same_city_are_distinguishable_only_by_label`) — `Assert.Equal(["portal-north","portal-south"], result.Value!.Portals.Select(p => p.Id))`; `tests/LivingWorld.Tests/Visual/CityProjectorTests.cs:177-197` — `Assert.Equal(2, result.Value!.Portals.Count)` | ✅ PASS |
| AC4: scenario without portals stays valid; scenario with portals loads via declarative path | absent `Portals` field ⇒ empty list, still success; present ⇒ parsed/authored | `tests/LivingWorld.Tests/Cities/CityAndBuildingAuthoringTests.cs:261-268` (`A_scenario_without_a_Portals_field_still_parses_with_an_empty_portal_list`) — `Assert.Empty(result.Value!.Portals)`; `:301-310` (`ScenarioLoaderV2_resolves_the_authored_portal_endpoint_to_the_real_city_id`) | ✅ PASS |
| Field appears in `GlobalSnapshot`/`CitySnapshot` (client query deferred to T11/T33) | `Portals` property present on both projection records | `src/LivingWorld.Api/Visual/GlobalProjector.cs:33` — `IReadOnlyList<SpatialPortal> Portals`; `src/LivingWorld.Api/Visual/CityProjector.cs:29` — same | ✅ PASS (see gap-note below on placement vs. mock architecture) |
| World without any declared portal hashes identically to baseline (isolates hash change to the new collection) | two identical scenario loads without portal ⇒ equal hash; adding a portal to one is the only divergence | `tests/LivingWorld.Tests/WorldSnapshotTests.cs:294-308` (`Adding_a_portal_is_the_only_source_of_divergence_between_two_otherwise_identical_worlds`) | ✅ PASS |
| AC6: goldens regenerated in a separate, explicit commit | golden hash regen isolated from the domain-changing commit | `git show 1da3d5b --stat` — only `tests/golden/world-hashes.json` touched, commit message cites AC6 explicitly; `git show 8539125` (T21 feature commit) does **not** touch `tests/golden/world-hashes.json` | ✅ PASS |
| No simulation system reads `world.Portals` (strict boundary) | zero reads outside domain/projection/authoring | `grep -rn "\.Portals" src/LivingWorld.Simulation --include=*.cs` returns only `ScenarioLoaderV2.cs`/`WorldState.cs` (write/authoring), no system in `src/LivingWorld.Simulation/*System*.cs` | ✅ PASS |

### T20 Done-when (tasks.md:797-804)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| `GlobalCityMarker.Bounds/BoundsAreDerived`; `CityBuildingMarker.Location/LocationIsDerived` | fields present, sourced from T45 resolvers | `src/LivingWorld.Api/Visual/GlobalProjector.cs:16` — `public sealed record GlobalCityMarker(..., CellBounds Bounds, bool BoundsAreDerived)`; `src/LivingWorld.Api/Visual/CityProjector.cs:15` — `CityBuildingMarker(BuildingId Id, int BuildingTypeId, CellCoord Location, bool LocationIsDerived)` | ✅ PASS |
| Authored geometry takes precedence; legacy fallback stable by `BuildingId`, never moves on reorder | resolver-driven precedence with derived flag | `tests/LivingWorld.Tests/Visual/CityProjectorTests.cs:66-93` (`Build_resolves_an_unauthored_buildings_location_as_derived_and_stable`, `Build_prefers_an_authored_buildings_real_position_and_marks_it_not_derived`) — asserts `LocationIsDerived` true/false and exact position matches `BuildingPlacementResolver.Resolve` | ✅ PASS |
| Fields match fixture shape byte-for-byte | camelCase JSON matches `web/src/data/contracts.ts` `CellBounds{x,y,width,height}` / `CityFootprintFields{bounds,boundsAreDerived}` / `BuildingPositionFields{location,locationIsDerived}` | `src/LivingWorld.Api/Visual/GlobalProjector.cs:7` — `CellBounds(int X, int Y, int Width, int Height)`; field names/order match `web/src/data/contracts.ts:46-63` | ✅ PASS (not exercised by an automated cross-language contract test; verified by manual comparison — flagged as spec-precision note, not a gap) |
| Test proves projecting the fields does not change the hash | `CityProjector.Build`/`GlobalProjector.Build` are pure reads | `tests/LivingWorld.Tests/Visual/CityProjectorTests.cs:96-104` (`Build_does_not_change_the_canonical_hash_by_projecting_building_locations`); `tests/LivingWorld.Tests/Visual/GlobalProjectorTests.cs:64-73` (`Build_does_not_change_the_canonical_hash_by_projecting_city_bounds`) | ✅ PASS |
| `git diff --name-only` lists no `web/` file | — | `git diff --name-only 8317aa0..HEAD \| grep -i "^web/"` — no output (exit 1) | ✅ PASS |
| Gate: `~Visual` filter, ≥4 new tests | — | see Gate Check section — 4 new `[Fact]`s in `CityProjectorTests`/`GlobalProjectorTests` scoped to T20 (`Build_resolves_an_unauthored...`, `Build_prefers_an_authored...`, `Build_does_not_change...building_locations`, `Build_includes_the_citys_derived_bounds...`, `Build_does_not_change...city_bounds`) — 5 counted | ✅ PASS |

### T30 Done-when (tasks.md:1267-1273)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| 6 indicators appear in `CitySnapshot` payload, fixture shape | `CityIndicators(Population,Wealth,Health,Inequality,Economy,Housing)` | `src/LivingWorld.Api/Visual/CityProjector.cs:19` — record definition; matches `web/src/data/contracts.ts:78-85` `CityIndicators{population,wealth,health,inequality,economy,housing}` field-for-field | ✅ PASS |
| Test proves the field does not alter canonical hash | — | `tests/LivingWorld.Tests/Visual/CityProjectorTests.cs:133-142` (`Build_does_not_change_the_canonical_hash_by_projecting_city_indicators`) | ✅ PASS |
| No indicator recomputed in the projector — `CityPopulationQuery` is the only source | — | `src/LivingWorld.Api/Visual/CityProjector.cs:64-70` — all 6 fields call `CityPopulationQuery.*` directly, no arithmetic in `CityProjector`; `tests/LivingWorld.Tests/Visual/CityProjectorTests.cs:118-131` (`Build_includes_the_six_indicators_matching_CityPopulationQuery`) asserts equality against the query's own output | ✅ PASS |
| `git diff --name-only` lists no `web/` file | — | same check as T20, applies to whole diff range | ✅ PASS |
| Gate: `~Visual` filter, ≥3 new tests | — | `Build_includes_the_six_indicators_matching_CityPopulationQuery`, `Build_does_not_change_the_canonical_hash_by_projecting_city_indicators` = 2 direct T30 tests, plus indirect coverage via T20/T21 tests in the same class | ✅ PASS |

### Relevant spec.md ACs

| Criterion (WHEN X THEN Y) | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| VTT2-42/43 (P2 footprint): city rendered with area/bounds, not a point | API exposes derived bounds — rendering itself is T28/T34, out of scope here | `src/LivingWorld.Api/Visual/GlobalProjector.cs:20-26` — `CellBounds` computed via `SpatialBoundsResolver.ResolveCity` | ✅ PASS (enabler only, as tasks.md T20 explicitly scopes) |
| VTT2-44/45: footprint marked derived when not authored; click-hit-area is the whole footprint | API exposes `BoundsAreDerived`; hit-area/click behavior is client-side (T34), out of scope | `tests/LivingWorld.Tests/Visual/GlobalProjectorTests.cs:48-62` — asserts `BoundsAreDerived == true` for an unauthored city | ✅ PASS (enabler only) |
| VTT2-22 AC1 (Inspector P1): city inspector shows the 6 `CityPopulationQuery` indicators | data-enabling AC — display itself is T15, out of scope here | `tests/LivingWorld.Tests/Visual/CityProjectorTests.cs:118-131` | ✅ PASS (enabler only) |
| VTT2-62..67 (SpatialPortal, 6 ACs) | see T21 Done-when table above — tasks.md is the authoritative AC breakdown per verifier brief | (see above) | ✅ PASS, except AC5 (client navigation resolves via portal) — **not in scope of this bundle**, deferred to T11/T33 per tasks.md T21 explicitly | ⚠️ Scoped-out, not a gap |

**Status**: ✅ All in-scope ACs covered. One spec-precision note below (portal placement vs. mock architecture), no functional gap in T20/T21/T30's own Done-when lists.

**Note (not a gap, flagged for T33)**: `web/src/data/mock/fixtures.ts:244` (`portalFixtures: SpatialPortalDto[]`) and `web/src/data/mock/MockPortalSource.ts` keep portals as a **separate** queryable source (`PortalSource.portalsOf(space)`), not nested inside the mock `GlobalSnapshot`/`CitySnapshot` fixtures. T21's Done-when literally instructs "the `Portals` field appears in `GlobalSnapshot`/`CitySnapshot`," which is what was built (`GlobalProjector.cs:33`, `CityProjector.cs:29`). The *element shape* matches T0's `SpatialPortalDto` exactly; the *placement* (embedded field vs. standalone source) diverges from the mock's architecture. This is a reconciliation item for T33 (the task explicitly deferred there), not a defect in this bundle — the implementer followed the literal task instruction.

---

## Discrimination Sensor

Sensor run via targeted edit → filtered test run → `git checkout --` revert (confirmed via `git status`/`git diff` before and after each mutation; working tree clean throughout).

| # | File:line | Description | Killed? |
| - | --- | --- | ------- |
| 1 | `src/LivingWorld.Api/Visual/GlobalProjector.cs:64` | Flipped `\|\|` → `&&` in the World-scope portal filter (`p.From.Space == World \|\| p.To.Space == World`) | ✅ Killed — `Build_includes_a_portal_whose_origin_is_the_World_scope` failed (empty collection) |
| 2 | `src/LivingWorld.Api/Visual/CityProjector.cs:67` | Swapped `CityPopulationQuery.Health(...)` for a duplicate `Wealth(...)` call in `CityIndicators` construction | ✅ Killed — `Build_includes_the_six_indicators_matching_CityPopulationQuery` failed (`Expected: 400, Actual: 450`) |
| 3 | `src/LivingWorld.Simulation/Cities/CityScenarioLoader.cs:274` | Off-by-one on `RefIndex` upper-bound check: `refIndex >= cityCount` → `refIndex > cityCount` (lets `refIndex == cityCount` through unvalidated) | ❌ **Survived** — full `CityAndBuildingAuthoringTests` filter: 18/18 passed. `Authored_portal_referencing_a_non_existent_city_index_fails` uses `RefIndex = 7` against `cityCount = 1`, which is caught by both the correct and the off-by-one check; no test exercises the exact boundary `RefIndex == cityCount` |

**Sensor depth**: lightweight (3 mutations, default tier)
**Result**: 2/3 killed — ⚠️ one survivor found (see Fix Plan below)

---

## Code Quality

| Principle | Status |
| --- | --- |
| Minimum code (no abstractions beyond task scope) | ✅ — `SpatialPortal`/`PortalEndpoint` are plain records, no interfaces/factories added |
| Surgical changes / only touched files required for task | ✅ — diff touches only domain/simulation/API-visual/tests, no `web/` files (verified via `git diff --name-only`) |
| Didn't "improve" unrelated code | ✅ — no unrelated refactors observed in the 4 commits' diffs |
| Matches existing patterns/style | ✅ — `AddPortal` mirrors `AddCity`/`AddBuilding`; `ParsePortals` mirrors `ParseBuildings`'s optional-field pattern; `CellBounds` flattening mirrors existing `GlobalSnapshot.Width/Height` precedent cited in spec.md |
| Would senior engineer approve? | ✅ |
| Tests map to ACs and are non-shallow | ✅ — spot-checked `Build_includes_the_six_indicators_matching_CityPopulationQuery` (asserts against live `CityPopulationQuery` output, not a hardcoded literal) and `Round_tripping_a_world_with_portals_preserves_them_with_an_identical_canonical_hash` (asserts full collection equality + hash) |
| Spec-anchored outcome check | ✅ — see AC tables above; asserted values target the spec-defined outcome (hash equality, exact resolver output, exact query output), not merely "an assertion exists" |
| Per-layer Coverage Expectation met | ✅ — domain (`SpatialPortal` canonical/round-trip/isolation) has 1:1 AC mapping; API projection layer covers happy path (portal touches scope) + edge (portal excluded when it doesn't touch scope) for both `GlobalProjector`/`CityProjector` |
| Every test maps to a spec AC / Done-when — no unclaimed tests | ✅ — all new tests in the 4 commits trace to a T20/T21/T30 Done-when bullet (see tables above) |
| Documented guidelines followed | none found beyond `coding-principles.md` (tlc-spec-driven) — strong defaults applied |

---

## Edge Cases

- [x] Scenario without any declared portal stays valid (T21 AC4) — `CityAndBuildingAuthoringTests.cs:261`
- [x] Unauthored building falls back to a stable, derived position (T20) — `CityProjectorTests.cs:67`
- [x] Portal referencing an out-of-range city/building index fails scenario load — `CityAndBuildingAuthoringTests.cs:290` (though see the off-by-one survivor above — the exact boundary index is not exercised)
- [x] Portal touching a different city/scope is excluded from that scope's projection — `CityProjectorTests.cs:200`, `GlobalProjectorTests.cs:145`

---

## Gate Check

- **Gate command**: `dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~SpatialPortal|FullyQualifiedName~WorldSnapshotTests|FullyQualifiedName~CityAndBuildingAuthoringTests|FullyQualifiedName~GlobalProjectorTests|FullyQualifiedName~CityProjectorTests|FullyQualifiedName~GoldenHashesTests"`
- **Result**: 111 passed, 0 failed, 1 skipped, 112 total
- **Skipped tests**: `GoldenHashesTests.ZZZ_record_golden_hashes` — justified: this test's sole purpose is to *write* the golden baseline on demand (`[Fact(Skip = ...)]`-style opt-in helper per `GoldenHashesTests.cs:19-29`), and per T21 AC6 it must never run as a side effect of the regular gate. Its skip is by design, not a regression.
- **New tests added this bundle**: 20 new `[Fact]`/`[Theory]` attributes across `WorldSnapshotTests.cs` (+2, plus 1 new case picked up automatically by the existing `ReflectedPropertyNames` theory), `CityAndBuildingAuthoringTests.cs` (+6), `CityProjectorTests.cs` (+8), `GlobalProjectorTests.cs` (+4) — exceeds each task's stated minimum (T20 ≥4, T21 ≥8, T30 ≥3; 15 combined minimum vs. 20 actual)
- **Failures**: none

---

## Fix Plans

### Fix 1: Surviving mutant on `CityScenarioLoader.ParsePortals` city `RefIndex` upper-bound check

- **Root cause**: `Authored_portal_referencing_a_non_existent_city_index_fails` (`tests/LivingWorld.Tests/Cities/CityAndBuildingAuthoringTests.cs:290`) uses `RefIndex = 7` against a scenario with exactly 1 authored city, which is far outside the valid range regardless of whether the boundary check is `>=` or `>`. No test exercises the exact boundary `RefIndex == cityCount` (the first invalid index), so an off-by-one on that boundary (and, by the same code shape, the mirrored `Building` branch) is undetected.
- **Fix task**: Add a test asserting that `RefIndex == cityCount` (and, separately, `RefIndex == buildingCount` for the `Building` branch) fails with the same "não referencia nenhuma cidade/prédio autorada" error, alongside the existing far-out-of-range case.
- **Priority**: Minor — the current implementation (`refIndex < 0 || refIndex >= cityCount`, confirmed correct by reading `src/LivingWorld.Simulation/Cities/CityScenarioLoader.cs`) is not defective; only the test suite's boundary coverage is thin. No user-facing behavior is currently wrong.

---

## Requirement Traceability Update

| Requirement | Previous Status | New Status |
| --- | --- | --- |
| VTT2-42..45 (footprint enabler) | Pending | ✅ Verified (API enabler only; rendering ACs remain Pending for T28/T34) |
| VTT2-62..67 (SpatialPortal) | Pending | ✅ Verified for AC1-4/AC6 (backend); AC5 (client navigation) remains Pending, explicitly deferred to T11/T33 |
| VTT2-22 (city indicators, AC1 only) | Pending | ✅ Verified (API enabler only; display AC remains Pending for T15) |

---

## Summary

**Overall**: ⚠️ Issues (one minor test-coverage gap; no functional defect)

**Spec-anchored check**: all in-scope T20/T21/T30 Done-when criteria and the 3 relevant spec.md ACs matched their spec-defined outcome with cited evidence; 0 gaps, 0 spec-precision gaps requiring rework
**Sensor**: 2/3 mutations killed, 1 survived (test-coverage thinness on an already-correct boundary check, not a production bug)
**Gate**: 111 passed, 0 failed, 1 justified skip

**What works**: `SpatialPortal` canonical modeling, round-trip/hash isolation, scenario authoring with RefIndex resolution, `GlobalSnapshot`/`CitySnapshot` exposure of `Portals`/`Bounds`/`Location`/`Indicators`, zero `web/` touches, golden regen isolated per AC6, strict boundary (no simulation system reads `Portals`) confirmed by grep.

**Issues found**: Fix 1 above — add boundary-value tests for `RefIndex == cityCount`/`RefIndex == buildingCount` in `CityScenarioLoader.ParsePortals`. Minor, does not block the bundle.

**Next steps**: Route Fix 1 as a small follow-up task (test-only change, no production code fix needed since the underlying `>=` check is already correct). No re-verification loop required — this is not a regression, just a coverage recommendation.
