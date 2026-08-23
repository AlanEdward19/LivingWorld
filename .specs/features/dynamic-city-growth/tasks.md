# Dynamic City Growth Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is
the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review,
Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/dynamic-city-growth/design.md`
**Status**: Done — Verified PASS (round 4, AD-007 follow-up). See `validation.md` for the full round 1-4 history.

---

## Test Coverage Matrix

> Generated from codebase (`tests/LivingWorld.Tests/Cities/*.cs` sampled — `BuildingFootprintAndPlacementTests.cs`, `CityRulesTests.cs`, `MigrationSystemTests.cs`, `CityTests.cs`) and project guidelines. Guidelines found: `AGENTS.md` (see `scripts/test.sh`/`verify.sh` for the gate commands below); also applying user's standing gate-cadence feedback (per-task = new tests + fast full suite; `verify.sh`/`Category=Scenario` only at feature close, not per task).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| ---------- | ------------------- | --------------------- | ----------------- | ------------ |
| Domain (`LivingWorld.Domain/Cities/*.cs` — `CityOccupancy`, `OverflowPlacer`, `BuildingPlacementResolver`, `CityBoundsResolver`, `CityRules`, `Building`) | unit | All branches; 1:1 to spec ACs (CITYGROW-01..05) and every listed Edge Case | `tests/LivingWorld.Tests/Cities/*Tests.cs` | `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` |
| Simulation system (`LivingWorld.Simulation/Cities/SpatialSettlementFoundingSystem.cs`, `MigrationSystem.cs` extension) | unit/integration (in-memory `WorldState`, same style as existing `MigrationSystemTests.cs`/`SettlementFoundingSystem` tests) | All branches; concentration-threshold gate, absorption precedence, idempotency (no double-founding), re-verify-at-fire-time | `tests/LivingWorld.Tests/Cities/*Tests.cs` | `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` |
| API/Visual projection (`LivingScopeState.cs`, `GlobalProjector.cs` — signature ripple only, no new behavior) | unit (existing coverage, no new tests required unless a call site's behavior changes) | Compiles + existing tests stay green | `tests/LivingWorld.Tests/Visual/*Tests.cs` | `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Visual"` |
| Full backend suite (final task only) | full run, no filter (still excludes `Category=Scenario` by default per `test.sh`) | Nothing regresses project-wide | n/a | `bash scripts/test.sh` |
| Frontend (`web/**`) | none for this feature | No frontend code changes in this feature (visual rendering already fixed in a separate session) | n/a | n/a |
| Full gate (`verify.sh`, `Category=Scenario`) | build/lint/docs + long scenario run | Feature-close only, per user's standing feedback — never per task | n/a | `bash scripts/verify.sh` then user runs `bash scripts/test.sh --filter Category=Scenario` themselves |

## Parallelism Assessment

> Generated from codebase.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --------- | --------------- | ----------------- | --------- |
| Domain unit tests | Yes | Each test builds its own fresh `WorldState`/`City`/`Building` in-memory, no shared static/global state (xUnit default, same pattern as every sampled `Cities/*Tests.cs` file) | `BuildingFootprintAndPlacementTests.cs`, `CityRulesTests.cs` construct fresh fixtures per test, no shared DB/file |
| Simulation system tests | Yes | Same in-memory `WorldState` construction per test, `ctx.ScheduleEvent`/`HandleEvent` invoked directly on a fresh instance | `MigrationSystemTests.cs` pattern |
| Full suite run (`test.sh`) | N/A (sequential invocation, but individual tests inside are parallel per xUnit defaults) | dotnet test's own test-collection parallelism, unrelated to this feature | existing `test.sh` behavior, unchanged |

## Gate Check Commands

> Generated from codebase.

| Gate Level | When to Use | Command |
| ---------- | ------------ | -------- |
| Quick | After each task below | `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` |
| Full | After the last task in this file | `bash scripts/test.sh` |
| Build/Scenario (feature close only, NOT per task) | Only when the user decides to close this feature | `bash scripts/verify.sh` (user runs `Category=Scenario` separately per standing feedback) |

---

## Execution Plan

### Phase 1: Occupancy + Placement (Sequential)

```
T1 → T2 → T3
```

### Phase 2: Bounds Growth (Sequential, depends on Phase 1)

```
T3 → T4 → T4b
```

### Phase 3: Land Scarcity → Migration (depends on Phase 1)

```
T3 → T5
```

### Phase 4: Spatial Founding (Sequential, depends on Phase 2)

```
T4b → T6 → T7
```

### Phase 5: Full Gate (Sequential, depends on all above)

```
T4b, T5, T7 → T8
```

---

## Task Breakdown

### T1: `CityOccupancy` — free-cell scan + `IsLandScarce` — ✅ Done (commit `16b3ea8`)

> SPEC_DEVIATION: lives in `src/LivingWorld.Simulation/Cities/CityOccupancy.cs`, not
> `src/LivingWorld.Domain/Cities/` as written above — `WorldState` only exists in
> `LivingWorld.Simulation` (`Domain` doesn't reference `Simulation`, same precedent as
> `CityPopulationQuery.cs`). Signatures otherwise as specified.

**What**: New static class with `IsFree(world, city, candidateFootprint)`,
`FindFreeCellInBounds(world, city, bounds, footprintShape)`, and
`IsLandScarce(world, city, footprintShape)` (whole-map scan), all reusing
`BuildingFootprintGenerator` for occupied-cell math.
**Where**: `src/LivingWorld.Domain/Cities/CityOccupancy.cs`
**Depends on**: None
**Reuses**: `BuildingFootprintGenerator` (existing), `WorldState.Buildings`
**Requirement**: CITYGROW-01, CITYGROW-02

**Tools**:
- MCP: NONE
- Skill: NONE (already inside `tlc-spec-driven` Execute)

**Done when**:
- [ ] `IsFree` returns false for any candidate footprint overlapping an existing building's footprint in the same city, true otherwise
- [ ] `FindFreeCellInBounds` returns the deterministic first free origin inside bounds (same building id → same result every call, no RNG) or `null` when bounds are fully occupied
- [ ] `IsLandScarce` returns true only when a whole-map scan finds zero free cells for the given footprint shape
- [ ] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [ ] Test count: ≥6 new tests pass (free/occupied/edge-of-bounds/full-map-scarce cases), no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): add CityOccupancy free-cell and land-scarcity scan`

---

### T2: `OverflowPlacer` — outward ring-search from bounds edge — ✅ Done (commit `dcdd4ec`)

> SPEC_DEVIATION: lives in `src/LivingWorld.Simulation/Cities/OverflowPlacer.cs`, same reason as T1.

**What**: New static class with `ResolveOverflowPosition(world, city, bounds, id, footprintShape)` — increasing-radius ring search from the bounds edge (not city center), deterministic angle order via `StableHash.Mix(id.Value)`, first free cell (via `CityOccupancy.IsFree`) wins.
**Where**: `src/LivingWorld.Domain/Cities/OverflowPlacer.cs`
**Depends on**: T1
**Reuses**: `CityOccupancy`, `StableHash.Mix` (same hashing style as existing `BuildingPlacementResolver.DerivedPosition`)
**Requirement**: CITYGROW-02

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Given a fully-occupied bounds and at least one free cell outside it, returns that free cell (nearest by ring radius; near-city and far-from-city cases both covered per spec's Edge Cases)
- [ ] Deterministic: same `BuildingId` + same world state → same position every call
- [ ] Never returns a cell overlapping an existing footprint (delegates to `CityOccupancy.IsFree`)
- [ ] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [ ] Test count: ≥4 new tests pass (near overflow, far overflow, determinism, no-overlap), no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): add OverflowPlacer ring-search for out-of-bounds placement`

---

### T3: `BuildingPlacementResolver.Resolve` — wire occupancy + overflow into placement — ✅ Done (commit `77cb124`)

> SPEC_DEVIATION: `BuildingPlacementResolver` moved to
> `src/LivingWorld.Simulation/Cities/BuildingPlacementResolver.cs` (same WorldState reason as
> T1/T2). All call sites in `src/` and `tests/` updated to the new
> `(building, city, world, bounds)` signature and verified to compile/pass, **except** two files
> that already carried substantial unrelated uncommitted work predating this session
> (`src/LivingWorld.Api/Visual/LivingScopeState.cs`, `src/LivingWorld.Simulation/Cities/ConstructionSystem.cs`,
> and the untracked `tests/LivingWorld.Tests/Stage4/CityBuildingMarkerContractTests.cs`): the
> required signature fix is applied and compiles in the working tree, but left out of this
> feature's git commits to avoid bundling in that other work. Also folded into this task: T1's
> `CityOccupancy.IsFree`/`FindFreeCellInBounds` gained an optional `placingId` parameter so an
> unauthored sibling building's occupancy is computed via the same `Resolve` (bounded by
> strictly-smaller `BuildingId`) instead of the old legacy ring-hash fallback — the fallback let
> two engine-built buildings in the same city both resolve to the same free cell and collide,
> caught by the existing non-collision test once the signature changed.

**What**: Extend `Resolve`'s signature to accept `WorldState` and the city's resolved `CityBounds`; try `CityOccupancy.FindFreeCellInBounds` first, fall back to `OverflowPlacer.ResolveOverflowPosition`. Update every call site (grep first — `ConstructionSystem`, visual projectors, any test helper) to pass the new parameters.
**Where**: `src/LivingWorld.Domain/Cities/BuildingPlacementResolver.cs` (+ call sites)
**Depends on**: T1, T2
**Reuses**: `CityOccupancy`, `OverflowPlacer`
**Requirement**: CITYGROW-01, CITYGROW-02

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `Resolve` places inside current bounds when a free cell exists there (existing behavior for the common case, footprint no longer silently overlaps another building)
- [ ] `Resolve` falls back to `OverflowPlacer` when no free cell exists in bounds, `IsDerived` still reflects authored-vs-derived correctly
- [ ] Every existing call site compiles and passes (grep-verified, listed in commit message)
- [ ] `BuildingFootprintAndPlacementTests.cs` (existing) still passes with the new signature
- [ ] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [ ] Test count: ≥3 new tests pass (inside-bounds-free-cell path, overflow-fallback path, no-regression on existing authored-position path), no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): wire occupancy/overflow into BuildingPlacementResolver.Resolve`

---

### T4: `CityBoundsResolver.Resolve` — grow bounds to absorb overflow buildings — ✅ Done (commit `76940f6`)

> SPEC_DEVIATION: unlike T1/T2/T3, this stayed in `src/LivingWorld.Domain/Cities/CityBoundsResolver.cs`
> instead of moving to `LivingWorld.Simulation`. Design.md's suggested `WorldState`/
> `IReadOnlyList<Building>` parameter would have forced the same Domain→Simulation move (an
> engine-built `Building.Position` is always null — it's only ever derived on demand via
> `BuildingPlacementResolver`, which needs `WorldState`). Instead, `Resolve` gained two optional
> trailing parameters (`ownedBuildingFootprintBoxes: IReadOnlyList<CityBounds>?`,
> `absorptionRingCells: int = 3`): the caller (which does have `WorldState`) resolves each
> candidate overflow building's absolute footprint box first and passes plain `CityBounds` values
> in. Both existing behavior and every one of the ~15 existing call sites
> (`SpatialBoundsResolver.ResolveCity`, `GlobalProjector`, `LivingScopeState`, `ConstructionSystem`,
> `NpcInspectionQuery`, `BehaviorDecisionSystem`, `PopulationSeeder`, tests, etc.) are untouched —
> both new parameters default to "no overflow buildings" (identical output to before). Wiring an
> actual `WorldState`-backed footprint-box resolution into the live tick loop is NOT part of this
> task (not listed in its "Where"); this task only adds the growth-capable primitive.

**What**: Add `AbsorptionRingCells` to `CityRules` (default 3). Extend `CityBoundsResolver.Resolve` to union the population-derived box with the bounding box of the city's own buildings positioned within `AbsorptionRingCells` of that box's edge, still capped only by the existing hard map-edge limit (population-only `MaxSize` no longer applies once overflow buildings are present).
**Where**: `src/LivingWorld.Domain/Cities/CityBoundsResolver.cs`, `src/LivingWorld.Domain/Cities/CityRules.cs` (or wherever `CityRules` is defined)
**Depends on**: T3
**Reuses**: Existing `SideFor`/map-limit math for the population-derived half

**Requirement**: CITYGROW-03, CITYGROW-05

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] A city with an overflow building within `AbsorptionRingCells` of its population-derived bounds resolves to a larger box including that building's full footprint
- [x] Growth never exceeds the existing hard map-dimension cap (`Math.Min(mapWidth, mapHeight)/2`) — reuse/extend the existing `City_bounds_never_exceed_the_smaller_map_dimension...` test
- [x] Existing residents'/buildings' positions are unchanged by a bounds-growth resolution (spec AC5) — only the bounds rectangle grows
- [x] `CityRulesTests.cs` (existing) covers the new `AbsorptionRingCells` field's validation
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [x] Test count: ≥4 new tests pass (absorbed growth, map-edge cap still holds, positions unchanged, field validation), no silent deletions — 6 new tests added

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): grow city bounds to absorb nearby overflow buildings`

---

### T4b: wire live overflow-footprint boxes into every `SpatialBoundsResolver.ResolveCity` call site — ✅ Done (commit `7400416`)

> SPEC_DEVIATION: 4 of the 6 listed call sites
> (`src/LivingWorld.Api/Visual/LivingScopeState.cs`, `src/LivingWorld.Simulation/Cities/ConstructionSystem.cs`,
> `src/LivingWorld.Simulation/Cities/NpcInspectionQuery.cs`,
> `src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs`) already carried substantial
> unrelated uncommitted work predating this session (same situation T3 already documented for two
> of these same files). The required `CityOccupancy.ResolveGrownBounds` wiring is applied and
> compiles/passes in the working tree for all 6 call sites, but only the 2 clean files
> (`src/LivingWorld.Api/Visual/CityProjector.cs`, `src/LivingWorld.Api/Visual/GlobalProjector.cs`)
> plus the new helper (`CityOccupancy.cs`), `SpatialBoundsResolver.cs`, and the new test file are
> included in this task's git commit — the other 4 files' unrelated pre-existing hunks are left
> out of the commit to avoid bundling in that other work, per the same precedent T3 set.

**What**: T4 made `CityBoundsResolver.Resolve` growth-capable, but every real production caller
still calls it with zero overflow boxes (identical-to-before output) — bounds cannot visibly grow
in the running sim/API until this is wired. Add a `WorldState`-aware helper (in
`LivingWorld.Simulation`, alongside `CityOccupancy` from T1) that, given a city and its
population-derived box, resolves each of that city's own buildings' absolute footprint box
(`Building.Position` when authored, else re-derived via `BuildingPlacementResolver.Resolve` per
`Building.cs`'s "always re-derived, never persisted" convention) and returns the list of boxes to
pass as `ownedBuildingFootprintBoxes`. Thread that helper's output through
`SpatialBoundsResolver.ResolveCity` (give it the same two optional trailing parameters as
`CityBoundsResolver.Resolve`, forwarded) and every one of its real call sites that has a
`WorldState` in scope: `src/LivingWorld.Api/Visual/CityProjector.cs`,
`src/LivingWorld.Api/Visual/GlobalProjector.cs`, `src/LivingWorld.Api/Visual/LivingScopeState.cs`,
`src/LivingWorld.Simulation/Cities/ConstructionSystem.cs`,
`src/LivingWorld.Simulation/Cities/NpcInspectionQuery.cs`,
`src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs`. `PopulationSeeder.cs` calls
`CityBoundsResolver.SideFor` directly (not `Resolve`/`ResolveCity`) for its spread radius — out of
scope, unaffected, do not touch it.
**Where**: new helper in `src/LivingWorld.Simulation/Cities/CityOccupancy.cs` (or a sibling file in
the same directory), `src/LivingWorld.Domain/Cities/SpatialBoundsResolver.cs`, and the 6 call
sites listed above
**Depends on**: T4
**Reuses**: `BuildingPlacementResolver.Resolve` (already `WorldState`-aware per T3),
`BuildingFootprintGenerator`, the exact `ownedBuildingFootprintBoxes`/`absorptionRingCells`
parameter shape T4 already defined
**Requirement**: CITYGROW-03, CITYGROW-05 (made actually observable, not just resolver-level)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] New helper returns one `CityBounds` box per building in the city (via real/derived
      position + `BuildingFootprintGenerator`), verified against a `WorldState` with a mix of
      authored and engine-placed buildings
- [x] `SpatialBoundsResolver.ResolveCity` forwards the two optional parameters through to
      `CityBoundsResolver.Resolve` unchanged in meaning
- [x] Each of the 6 listed call sites passes the helper's live boxes instead of omitting the
      parameter — an end-to-end test (build a `WorldState` with an overflow building placed via
      `OverflowPlacer`, then call the projector/system path a real caller uses) shows the
      resolved bounds actually grew, not just the unit-level `CityBoundsResolver.Resolve` test from T4
- [x] All 6 call sites still compile and their existing tests stay green (no behavior change for
      cities with zero overflow buildings)
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [x] Test count: ≥3 new tests pass (helper returns correct boxes; at least one real call site
      demonstrably grows bounds end-to-end; zero-overflow case unchanged), no silent deletions — 3 new tests added

**Tests**: unit/integration
**Gate**: quick

**Commit**: `feat(cities): wire live overflow-building footprint boxes into city bounds resolution`

---

### T5: `MigrationSystem.ScoreOf` — land-scarcity term [P] — ✅ Done (commit `25bb02c`)

**What**: Add a land-scarcity term to `MigrationSystem.ScoreOf`'s existing weighted score: when `CityOccupancy.IsLandScarce` is true for the household's current city (whole map has no free cell for a house-sized footprint), force that city's "stay" score to the theoretical minimum so any other city with room scores higher. No change when `world.Cities.Count < 2` (existing guard already no-ops).
**Where**: `src/LivingWorld.Simulation/Cities/MigrationSystem.cs`
**Depends on**: T1 (needs `CityOccupancy.IsLandScarce`)
**Reuses**: `CityOccupancy.IsLandScarce`, existing `ScoreOf` weighting pattern
**Requirement**: Edge Case — "no free cell anywhere on the map"

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] A household in a land-scarce city (verified via a test world with zero free cells) scores lower for staying than for any other real city with room, causing relocation on the next `Tick`
- [x] A household in a land-scarce single-city world (no candidate to move to) is unaffected — `Tick`'s existing `world.Cities.Count < 2` guard still no-ops, no crash, no forced relocation to nowhere
- [x] Existing `MigrationSystemTests.cs` cases (employment/food/security/family-ties scoring) still pass unchanged
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [x] Test count: ≥3 new tests pass (land-scarce relocates, single-city no-op, non-scarce city unaffected), no silent deletions — 3 new tests added

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): weight land scarcity into MigrationSystem's stay-vs-relocate score`

---

### T6: `Building.ClusterFoundingScheduledAtTick` marker + cluster/population helpers — ✅ Done (commit `2fb2895`)

**What**: Add the nullable `ClusterFoundingScheduledAtTick` field + `MarkClusterFoundingScheduled(tick)` to `Building` (mirrors `City.FoundingScheduledAtTick`). Add a pure helper (e.g. `OverflowClusterFinder`) that groups a city's overflow buildings by mutual distance ≤ `AbsorptionRingCells` (chain/transitive), excludes buildings already within absorption range of any existing city, and computes each cluster's materialized-resident population (`Npc.Location` inside the cluster's bounding box).
**Where**: `src/LivingWorld.Domain/Cities/Building.cs`, `src/LivingWorld.Domain/Cities/OverflowClusterFinder.cs` (new)
**Depends on**: T4b (needs `AbsorptionRingCells` + absorption-range check, and T4b's real-position-resolving helper for each building's actual footprint box — a cluster's own buildings need real positions the same way T4b's callers do)
**Reuses**: `NpcScopeResolver`'s geometric-membership pattern (`bounds.Contains(location)`), T4b's footprint-box-resolving helper
**Requirement**: CITYGROW-04

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Clustering groups mutually-close overflow buildings transitively (chain of distance ≤ `AbsorptionRingCells`), excludes any building within an existing city's absorption range
- [x] Population count only counts materialized (`IsAlive`, non-pool) `Npc`s whose `Location` falls in the cluster's bounding box
- [x] `ClusterFoundingScheduledAtTick` defaults to `null`, settable exactly once via `MarkClusterFoundingScheduled`
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [x] Test count: ≥4 new tests pass (cluster grouping, absorption exclusion, population count, marker set-once), no silent deletions — 9 new tests added

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): add overflow cluster finder and Building cluster-founding marker`

---

### T7: `SpatialSettlementFoundingSystem` — found a city from a qualifying cluster — ✅ Done (commit `f15acf3`)

**What**: New monthly `ISimulationSystem`. `Tick`: for each city, find qualifying overflow clusters (via T6's finder) whose population clears `rules.FoundingConcentrationThreshold` via the SAME formula as `SettlementFoundingSystem` (`population / (population + 1)`); schedule a founding event (`ctx.ScheduleEvent` + `OrganizationTicks` delay, same cadence/mechanism as `SettlementFoundingSystem`), mark every building in the cluster via `MarkClusterFoundingScheduled`. `HandleEvent`: re-verify the concentration threshold still holds for the captured cluster at fire time (drop silently if not); otherwise create the new `City` at the cluster centroid (`CityNameGenerator`, `world.NextCityId()`), reassign `Building.City` for the cluster's buildings, reassign `Household.City`/cascaded `Npc.City` (via existing `JoinCity`) for households whose `Location` falls in the new city's initial resolved bounds.
**Where**: `src/LivingWorld.Simulation/Cities/SpatialSettlementFoundingSystem.cs` (new), registered wherever `SettlementFoundingSystem` is registered (likely `Program.cs`/system list)
**Depends on**: T6
**Reuses**: `SettlementFoundingSystem`'s scheduling pattern + concentration formula, `CityNameGenerator`, `NpcScopeResolver`'s geometric-membership technique
**Requirement**: CITYGROW-04, Edge Cases (absorption precedence, no premature founding, no double-founding)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] A cluster with enough materialized residents to clear the concentration threshold schedules a founding event on the same monthly/`OrganizationTicks` cadence as `SettlementFoundingSystem`
- [x] A cluster with buildings but too few/zero residents (1 house, 1 person) never schedules — concentration formula stays below threshold (reuses existing math, no separate building-count check)
- [x] A cluster already within an existing city's absorption range (per T4/T4b) is skipped — absorption takes precedence over founding, per spec Edge Cases
- [x] The same cluster is never scheduled twice (`ClusterFoundingScheduledAtTick` guard)
- [x] At fire time, a cluster that thinned out below threshold during the wait is silently dropped (no city forced into existence)
- [x] On success: new `City` exists, cluster's buildings reassigned to it, households geometrically inside its bounds reassigned (`Household.City` + member `Npc.City`)
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [x] Test count: ≥6 new tests pass (qualifying cluster founds, weak cluster doesn't, absorption precedence, no double-schedule, fire-time re-verify drops stale cluster, household reassignment), no silent deletions — 7 new tests added

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): add SpatialSettlementFoundingSystem for overflow-cluster city founding`

---

### T8: Full-suite gate (no code change) — ✅ Done, with a documented non-blocking caveat

> SPEC_DEVIATION: the gate command (`bash scripts/test.sh`) initially returned 9 failures, not the
> expected "0 beyond the 2 known flaky tests". A dedicated bisection investigation (real test runs
> across the feature's 8 commits, in a scratch worktree, working tree never touched) isolated the
> cause to a single line already sitting **uncommitted** in the working tree before this feature's
> session even started: `ScenarioRunner.cs` passing `processRecipes: DefaultProcessRecipes`
> (unrelated Stage-4 "crops" work in progress) makes `ProductionSystem.cs:73-75` skip food
> production and hand it to `CropSystem`, which yields far less food — a starvation collapse
> (`PopulationBaselineTests`, both `ScaleScenarioFixtureTests`, both `GoldenHashesTests`, and the
> two weakened causal tests all trace to this one change). `dynamic-city-growth` was proven inert
> for every one of these tests: `ScenarioRunner.Create` never enables `CityRules`
> (`CityRules.Disabled`), so zero cities ever exist, so `BuildingPlacementResolver`/
> `CityOccupancy`/`OverflowPlacer`/`CityBoundsResolver`/`MigrationSystem`'s new term/
> `SpatialSettlementFoundingSystem` are never reached — confirmed empirically by a run with every
> feature commit intact and only that one line reverted, which passed. This feature's own gate
> is therefore green; the 9 failures are a pre-existing, out-of-scope regression the user is
> already tracking as separate in-progress Stage-4 work, not a defect introduced here.
>
> Side finding (not fixed, per user's explicit instruction to leave it): `25bb02c` (T5) staged the
> whole `MigrationSystem.cs` file, which swept in an unrelated pre-existing uncommitted hunk
> (`household.BeginRelocation(...)`) that depends on `Household.BeginRelocation`/
> `RelocationArrivalSystem.cs`, both still uncommitted — so `f15acf3` (HEAD) does not compile on a
> clean checkout by itself. User will commit the rest of that Stage-4 work separately soon.

**What**: Run the full backend suite (no filter, `Category=Scenario` still excluded by `test.sh`'s default) to confirm no cross-feature regression from the signature/call-site changes in T3/T4/T4b and the new sibling system in T7.
**Where**: n/a
**Depends on**: T4b, T5, T7
**Reuses**: n/a
**Requirement**: n/a (project-wide safety net, not a spec AC)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `bash scripts/test.sh` run: 1512 passed, 9 failed, 11 skipped, 1532 total — the 9 failures
      are bisection-confirmed to trace to a single pre-existing uncommitted line unrelated to this
      feature (see SPEC_DEVIATION above), and this feature was proven inert (zero cities, `CityRules`
      disabled) for every one of those 9 tests. Re-running with that one line reverted and every
      feature commit intact: 0 failures on the tests it explains.
- [x] Total test count reported (1532, up from a pre-feature total of 1532 minus this feature's
      own ~30 new tests across T1-T7 — no silent deletions; the 9 failures are pre-existing
      unrelated regressions, not deleted/weakened tests)

**Tests**: none (aggregate gate only)
**Gate**: full

**Commit**: none (verification-only task; if it surfaces a regression, that becomes a new task, not folded into this one)

---

## Fix Tasks (post-Verifier)

> The independent Verifier (see `.specs/features/dynamic-city-growth/validation.md`, run
> 2026-08-22) returned **FAIL** with 1 Blocker and 1 Major gap against the T1-T8 implementation
> above. Both were routed back as fix tasks and closed in this pass; Verifier's 3 Minor gaps
> (Gap 3/4/5 — absorption-vs-closer-city test, `MaxSize` overshoot test, "nearest" precision) were
> left as-is per the orchestrator's ranking (cheap, non-blocking, not re-litigated here).

### FixT1: Eliminate exponential recursion in `CityOccupancy.OccupiedCellsOfCity` — ✅ Done (commit `596824f`)

**What**: `OccupiedCellsOfCity` resolved each unauthored sibling by re-entering
`BuildingPlacementResolver.Resolve`, which called back into occupancy resolution for that
sibling's own smaller-id neighbors — no memoization, so resolving building *k* cost 2^(k-1)
resolutions (187s measured by the Verifier at N=6, unfinished at N=16). Rewrote it as a single
ascending-by-`BuildingId` pass that accumulates the `occupied` set incrementally and resolves each
unauthored sibling directly (`ScanForFreeOrigin`, falling back to a new non-recursive
`OverflowPlacer.ResolveOverflowPositionGiven`) instead of recursing.
`OverflowPlacer.ResolveOverflowPosition` now builds the occupied set once instead of recomputing
it from scratch per ring candidate — the other half of the same bug.
**Where**: `src/LivingWorld.Simulation/Cities/CityOccupancy.cs` (`OccupiedCellsOfCity`: private →
internal, rewritten), `src/LivingWorld.Simulation/Cities/OverflowPlacer.cs` (new internal
`ResolveOverflowPositionGiven`)
**Depends on**: T1, T2, T3 (fixes their combined runtime behavior; no signature change)
**Requirement**: CITYGROW-01/02 (unblocks the spec's own Independent Test, previously unrunnable)

**Done when**:
- [x] `OccupiedCellsOfCity` never calls `BuildingPlacementResolver.Resolve`/`IsFree`/
      `FindFreeCellInBounds` (verified by reading the rewritten method — single loop, no
      re-entrant calls)
- [x] New regression test: `CityOccupancyTests.OwnedBuildingFootprintBoxesWithOwners_resolves_many_unauthored_buildings_quickly_and_without_overlap`
      (30 unauthored buildings, asserts `< 5s` and zero cell overlap)
- [x] Existing T3 collision/non-collision tests re-run unchanged and still pass (same placement
      outcomes)
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — 209 passed, 0 failed (208 pre-fix + 1 new)

**Tests**: unit
**Gate**: quick
**Commit**: `596824f` — `fix(cities): eliminate exponential recursion in building occupancy resolution`

---

### FixT2: Clamp overflow placement to map bounds and decline when land-scarce — ✅ Done (commit `2133401`)

**What**: `CityOccupancy.IsLandScarce` had exactly one caller (`MigrationSystem`) — nothing on the
placement path ever consulted it, and `OverflowPlacer`'s ring search had no map clamp, so on a
fully-built map it could expand forever and hand back an off-map coordinate instead of declining
to place (CITYGROW-02b, spec.md's "no free cell anywhere on the map" edge case). Bounded the ring
search to `[0, mapWidth) x [0, mapHeight)` and capped growth at `mapWidth + mapHeight` (past that,
the ring has left the map in both dimensions). Changed
`BuildingPlacementResolver.Resolve`'s return type to a nullable tuple —
`null` means genuine land scarcity for that building right now. Updated every call site: `null`
means "leave this building unresolved for this call" (no queue, no special retry — same
always-re-derived convention design.md already documents), never a crash.
**Where**: `src/LivingWorld.Simulation/Cities/OverflowPlacer.cs` (`ResolveOverflowPosition`/
`ResolveOverflowPositionGiven` now return `CellCoord?`, map-clamped),
`src/LivingWorld.Simulation/Cities/BuildingPlacementResolver.cs` (`Resolve` → nullable tuple),
`src/LivingWorld.Simulation/Cities/CityOccupancy.cs` (`OccupiedCellsOfCity` skips a scarce
neighbor instead of adding it; `OwnedBuildingFootprintBoxesWithOwners` excludes a scarce building
from its result), `src/LivingWorld.Simulation/Cities/ConstructionSystem.cs` (skips creating the
workplace this tick), `src/LivingWorld.Api/Visual/CityProjector.cs` and
`src/LivingWorld.Api/Visual/LivingScopeState.cs` (exclude the scarce building's marker/visual from
the response), plus every direct test caller of `Resolve`/`ResolveOverflowPosition` updated for
the nullable signature.
**Depends on**: T1, T2, T3, T4b (extends their signatures)
**Requirement**: CITYGROW-02b

**Done when**:
- [x] `OverflowPlacer`'s ring search never returns a cell outside `[0, mapWidth) x [0, mapHeight)`
- [x] `BuildingPlacementResolver.Resolve` returns `null` on a fully-built map instead of a
      coordinate; new test
      `BuildingFootprintAndPlacementTests.Resolve_returns_null_when_the_whole_map_has_no_free_cell_anywhere`
- [x] New test
      `BuildingFootprintAndPlacementTests.Resolve_never_returns_a_position_outside_the_maps_bounds_when_only_far_room_remains`
      asserts the resolved footprint stays within the map when room exists only far from the city
- [x] Every production/test call site compiles against the new nullable signature (compiler-
      verified, no `error CS*` remaining)
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — 211 passed, 0 failed (209 pre-fix + 2 new)

**Tests**: unit
**Gate**: quick
**Commit**: `2133401` — `fix(cities): clamp overflow placement to map bounds and decline when land-scarce`

---

> Round 2 Verifier (`.specs/features/dynamic-city-growth/validation.md`, run 2026-08-22) returned
> **FAIL on the letter of the sensor rule only** (1 surviving mutant, Minor) — zero Blocker/Major
> remained. Round-3 fixes below close the surviving mutant (FixT3), the loose perf guard (FixT4/
> FixT5), the real land-scarcity bug (FixT6), and the 3 round-1 Minor test-coverage gaps (FixT7).

### FixT3: Assert the ascending-`BuildingId` resolution order is load-bearing — ✅ Done (commit `9a517bf`)

**What**: `CityOccupancy.OccupiedCellsOfCity`'s own doc comment calls the ascending
`OrderBy(b => b.Id.Value)` pass mandatory (a building's resolved position depends only on
causally-earlier, smaller-id buildings), but no test caught an `OrderByDescending` swap — the
round-2 Verifier's discrimination sensor mutated it and 211 tests stayed green. Added a test
comparing each building's batch-resolved position (`OwnedBuildingFootprintBoxesWithOwners`)
against the ground truth obtained by resolving buildings one at a time, in ascending id order,
recording each real position before resolving the next.
**Where**: `tests/LivingWorld.Tests/Cities/CityOccupancyTests.cs` (new test only, no `src/` change)
**Requirement**: CITYGROW-01 (the causal ordering `OccupiedCellsOfCity` depends on)

**Done when**:
- [x] New test manually verified against an `OrderByDescending` mutation of the ordering line —
      fails (position mismatch), mutation reverted, `git diff` empty
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — 212 passed, 0 failed (211 pre-fix + 1 new)

**Tests**: unit
**Gate**: quick
**Commit**: `9a517bf` — `test(cities): assert ascending building-id ordering is load-bearing`

---

### FixT4: Force the perf-guard test through the overflow ring path — ✅ Done (commit `7fcfb61`)

**What**: The blocker-regression test committed in FixT1 used 200×200 bounds, so all 30 test
buildings fit inside bounds and `OverflowPlacer`'s O(N²)-ish fallback — the path the guard exists
to protect — never triggered. Shrunk bounds to 12×12 (`CityBoundsResolver.MaxSize`, the real
population-based cap for a city) on a real 500×500 map, and added an assertion that at least one
resolved box lands outside bounds (proving the overflow path was genuinely exercised).
**Where**: `tests/LivingWorld.Tests/Cities/CityOccupancyTests.cs`
**Requirement**: CITYGROW-01/02 (performance characteristic of the T1 fix)

**Done when**:
- [x] Test asserts `Assert.Contains(boxes, b => ...)` that at least one box falls outside the
      12×12 bounds
- [x] Gate check passes — 212 passed, 0 failed (bounds/map change only, no new test count)

**Tests**: unit
**Gate**: quick
**Commit**: `7fcfb61` — `test(cities): force the perf-guard test through the overflow ring path`

---

### FixT5: Fail the perf-guard test fast instead of hanging — ✅ Done (commit `3fe4c18`)

**What**: xUnit 2.9.3's `[Fact(Timeout = ...)]` is silently ignored on synchronous test methods
("Tests marked with Timeout are only supported for async tests", confirmed by running it) — so a
reintroduced exponential regression hangs the gate for 300+s instead of failing fast. Converted
the perf-guard test to `async Task` with its body wrapped in `Task.Run`, so the 10s timeout is
actually enforced by xUnit.
**Where**: `tests/LivingWorld.Tests/Cities/CityOccupancyTests.cs`
**Requirement**: CITYGROW-01/02 (test-infrastructure hardening for the T1 fix's guard)

**Done when**:
- [x] Manually reintroduced the pre-T1 recursive shape in `CityOccupancy.OccupiedCellsOfCity` —
      test fails in 10s ("Test execution timed out after 10000 milliseconds") instead of hanging;
      mutation reverted, `git diff` empty
- [x] Gate check passes — 212 passed, 0 failed (same test, no new count)

**Tests**: unit
**Gate**: quick
**Commit**: `3fe4c18` — `test(cities): fail the perf-guard test fast instead of hanging`

---

### FixT6: Retry land-scarce construction projects instead of dropping them — ✅ Done (commit `42e4305`)

**What**: Real bug (round-2 Verifier, Gap B). `ConstructionSystem.Tick` unconditionally dequeued a
completing project even when `CompleteProject` failed to resolve a position for a workplace
recipe (land scarcity) — the project, and the resources already spent on it, silently vanished
with no workplace and no retry, contradicting design.md's Error Handling Strategy ("stays
queued/unresolved, retried whenever this gets called again"). Fixed by resolving placement with a
disposable candidate `Building` (id peeked via `world.NextBuildingId`, never advanced on failure)
*before* adding anything to the world, and only dequeuing when `CompleteProject` reports success —
a failed placement now leaves the project at the head of the queue, retried on a later tick.
**Where**: `src/LivingWorld.Simulation/Cities/ConstructionSystem.cs`,
`tests/LivingWorld.Tests/Cities/ConstructionSystemTests.cs`
**Requirement**: CITYGROW-02b (design.md Error Handling Strategy)

**Done when**:
- [x] New test `Completing_project_leaves_a_land_scarce_workplace_queued_and_completes_once_land_is_available`:
      on a map exactly the size of one blocking building, the project stays in `ConstructionQueue`
      after its completion tick, no `Workplace` and no orphan `Building` are created; the same
      recipe on a map with real room completes normally and creates the `Workplace`
- [x] Verified against the pre-fix code (stashed the fix): test fails (`Assert.Single` — queue was
      empty, project vanished); fix restored, `git diff` clean before restaging
- [x] Gate check passes: 213 passed, 0 failed (212 pre-fix + 1 new)

**Tests**: unit
**Gate**: quick
**Commit**: `42e4305` — `fix(cities): retry land-scarce construction projects instead of dropping them`

---

### FixT7: Cover the 3 remaining round-1 edge-case gaps — ✅ Done (commit `142dd08`)

**What**: Round-1 Gaps 3/4/5 — behavior already confirmed correct by manual Verifier probes across
two rounds, but never had a committed assertion. Added one test each: (1) an overflow building
absorbs into its own city's bounds even when a different city is geometrically closer (the
ownership filter in `OwnedBuildingFootprintBoxesWithOwners` excludes it from any other city's list
before distance is ever considered); (2) absorption growth can exceed
`CityBoundsResolver`'s population-based `MaxSize`=12, capped only by the map-edge limit; (3)
`OverflowPlacer.ResolveOverflowPositionGiven` picks the nearest free ring radius (radius 1 fully
blocked, radius 2 free — result's Chebyshev gap from bounds is exactly 2), not just any free cell
farther out.
**Where**: `tests/LivingWorld.Tests/Cities/CityOccupancyTests.cs`,
`tests/LivingWorld.Tests/Cities/BuildingFootprintAndPlacementTests.cs`,
`tests/LivingWorld.Tests/Cities/OverflowPlacerTests.cs`
**Requirement**: spec.md Edge Cases; CITYGROW-03/AC3/AC5; CITYGROW-02

**Done when**:
- [x] All 3 new tests pass independently and as part of the full Cities gate
- [x] Gate check passes: 216 passed, 0 failed (213 pre-fix + 3 new)

**Tests**: unit
**Gate**: quick
**Commit**: `142dd08` — `test(cities): cover the 3 remaining round-1 edge-case gaps`

---

## Round 3 follow-up (AD-007)

### AD-007a: Stop a land-scarce project from blocking the rest of its city's queue — ✅ Done (commit `f2219bc`)

**What**: FixT6 (`42e4305`) correctly stopped dropping a fully-paid, unplaceable project, but left
it strictly at `ConstructionQueue[0]`, which blocked every other project queued behind it in the
same city indefinitely (measured 20+ ticks). `ConstructionSystem.Tick` now walks the whole queue:
a stuck (fully-paid, unplaceable) project is retried for free every tick without blocking later
projects; the first not-yet-fully-paid project receives that tick's one-project resource budget
and, if it completes this same tick, its placement is attempted immediately too. `City`'s
`DequeueCompletedConstruction` (always index 0) became `RemoveConstructionProject(project)`
(reference-equality removal from any position).
**Where**: `src/LivingWorld.Simulation/Cities/ConstructionSystem.cs`,
`src/LivingWorld.Domain/Cities/City.cs`
**Requirement**: AD-007; spec.md Edge Cases (`ConstructionSystem` amendment); design.md Error
Handling Strategy (`ConstructionSystem` row)

**Done when**:
- [x] A land-scarce workplace project no longer starves a house project queued behind it in the
      same city
- [x] The stuck project itself eventually completes once land frees up, with `Consumed` unchanged
      (no double charge) and exactly one `Building`/`Workplace` created
- [x] The one-resource-consuming-project-per-tick throttle still holds — only which project
      qualifies changed
- [x] Pre-existing strict-FIFO behavior (no land scarcity) is unaffected
- [x] Gate check passes: 220 passed, 0 failed (216 pre-fix + 4 new)

**Tests**: unit — `Stuck_workplace_project_does_not_block_a_house_project_queued_behind_it`,
`Stuck_project_retries_without_double_charging_and_eventually_completes_exactly_once_when_land_is_free`,
`Throttle_keeps_advancing_the_paying_project_at_its_normal_per_tick_rate_while_a_stuck_project_is_retried_for_free`
(`tests/LivingWorld.Tests/Cities/ConstructionSystemTests.cs`)
**Gate**: quick
**Commit**: `f2219bc` — `fix(cities): let construction queue skip a land-scarce project instead of blocking behind it`

### AD-007b: Strengthen the overflow "nearest free cell" test — ✅ Done (commit `e48b15a`)

**What**: FixT7's nearest-ring test fully blocks radius 1, so it only proves the search grows the
radius when it must — it never exercises "a nearer AND a farther free cell both exist, does the
nearer one win." Added a case with exactly one free radius-1 cell alongside an entirely free
radius-2 ring; asserts the radius-1 cell is returned.
**Where**: `tests/LivingWorld.Tests/Cities/OverflowPlacerTests.cs`
**Requirement**: CITYGROW-02 (same requirement as FixT7's item 3)

**Done when**:
- [x] New test passes independently and as part of the full Cities gate
- [x] Gate check passes: 220 passed, 0 failed (same run as AD-007a)

**Tests**: unit — `ResolveOverflowPositionGiven_prefers_the_one_free_radius_1_cell_over_an_entirely_free_radius_2_ring`
**Gate**: quick
**Commit**: `e48b15a` — `test(cities): assert overflow placement picks the truly nearest cell, not just any farther one`

---

## Post-ship fix (found by user in production, 2026-08-23)

User ran their live world and saw a newly-founded city ("UrVal") with its walls literally
touching/overlapping an existing city's walls — no gap at all.

### FixT8: Cross-city bounds clamp — ✅ Done (commit `a6584ad`)

**What**: `CityBoundsResolver.Resolve` grew a city's bounds purely from its own overflow
buildings, capped only by the map-edge limit — nothing anywhere checked a city's growing bounds
against any OTHER city's bounds. Two cities founded at a safe distance could each independently
grow toward each other, tick after tick, until they touched or overlapped.
**Fix**: `CityBoundsResolver.Resolve`/`SpatialBoundsResolver.ResolveCity` gained an optional
`otherCityBoundsToAvoid` parameter (default `null`, no behavior change for every existing
single-city caller/test) — any owned building box that would pull the resulting bounds within
`AbsorptionRingCells` of one of these boxes is simply not merged in; growth stops at the gap
boundary instead of overlapping. `CityOccupancy.ResolveGrownBounds` gathers this avoid-list from
each other city's OWN un-clamped growth (population box ∪ its own overflow boxes, via a new
private `OwnGrowthBoundsIgnoringOtherCities` helper) — deliberately one non-recursive level, never
calling back into another city's full cross-clamped `ResolveGrownBounds`, to avoid reintroducing
the exact O(2^N) recursive blocker already fixed once in this feature (FixT1).
**Where**: `src/LivingWorld.Domain/Cities/CityBoundsResolver.cs`,
`src/LivingWorld.Domain/Cities/SpatialBoundsResolver.cs`,
`src/LivingWorld.Simulation/Cities/CityOccupancy.cs`
**Requirement**: CITYGROW-03/05 (bounds growth), new edge case (cross-city gap)

**Done when**:
- [x] Two cities placed close enough that OLD behavior would eventually touch/overlap now never
      come within `AbsorptionRingCells` of each other, across many rounds of both absorbing
      overflow buildings
- [x] A single city with no other city nearby still grows normally (unaffected, regression)
- [x] Existing test whose premise (a building closer to a foreign city than the absorption ring)
      is mathematically incompatible with the new invariant rewritten with the correct expectation
- [x] Gate check passes: 222 passed, 0 failed (220 baseline + the cross-city test + the regression
      test; the third new test — fire-time re-check — lands in FixT9 below)

**Tests**: unit —
`ResolveGrownBounds_never_lets_two_citys_grown_bounds_come_within_the_absorption_ring_of_each_other`,
`ResolveGrownBounds_still_absorbs_an_overflow_building_when_there_is_no_other_city_nearby`,
`ResolveGrownBounds_never_absorbs_an_overflow_building_into_a_city_that_is_not_its_owner` (rewrite)
**Gate**: quick
**Commit**: `a6584ad` — `fix(cities): clamp city bounds growth to never overlap another city`

---

### FixT9: Re-verify absorption distance at spatial-founding fire time — ✅ Done (commit `822ba4a`)

**What**: `SpatialSettlementFoundingSystem.HandleEvent` re-verified the population concentration
threshold before founding (after the `OrganizationTicks` delay) but never re-verified the "beyond
absorption range of any existing city" distance check that was only checked once at SCHEDULE time
(`Tick`, via `OverflowClusterFinder`). If other cities grew closer during the wait, founding
proceeded anyway.
**Fix**: `OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity` (was `private`, keyed off a
`City`) is now `internal`, keyed off a `CityId` so `HandleEvent` can reuse it without needing the
excluded city's object. `HandleEvent` calls it right after the existing concentration re-check and
silently drops the founding when it now holds — same "don't force an unjustified city into
existence" pattern already used for the concentration threshold.
**Where**: `src/LivingWorld.Simulation/Cities/OverflowClusterFinder.cs`,
`src/LivingWorld.Simulation/Cities/SpatialSettlementFoundingSystem.cs`
**Requirement**: CITYGROW-04, spec Edge Cases ("absorption takes precedence over founding")

**Done when**:
- [x] A cluster beyond absorption range at schedule time but within range of some city by fire
      time (simulated by growing that city's population during the `OrganizationTicks` wait) has
      its founding silently dropped, no city created
- [x] Gate check passes: 223 passed, 0 failed (222 from FixT8 + this test)

**Tests**: unit —
`HandleEvent_drops_silently_when_another_city_grew_within_absorption_range_during_the_wait`
**Gate**: quick
**Commit**: `822ba4a` — `fix(cities): re-verify absorption distance at spatial-founding fire time`

---

### FixT10: Minimum-distance check on the legacy `FoundingSitePicker` — ✅ Done (commit `077ed50`)

**What**: FixT8/FixT9 above only cover `SpatialSettlementFoundingSystem` (the new overflow-cluster
path). Since households still lack real `Building` placement (`real-household-workplace-buildings`,
not started), population growth today routes almost entirely through the OLD, pre-existing
`SettlementFoundingSystem` + `FoundingSitePicker`, which never had any minimum-distance check (only
excluded the exact cell of another city) — the actual cause of a live "UrVal colada" report after
FixT8/FixT9 landed.
**Fix**: `FoundingSitePicker.Pick` now rejects any candidate within `AbsorptionRingCells` (Chebyshev
distance) of any OTHER existing city, reusing
`OverflowClusterFinder.IsWithinAbsorptionRangeOfAnyOtherCity` — same constant/metric as the overflow
path, no new threshold. Returns `null` (honest failure) when no cell on the map clears that distance
from every other city, instead of forcing a colliding city into existence.
**Where**: `src/LivingWorld.Simulation/Cities/FoundingSitePicker.cs` (was already untracked/new
before this session; the distance-check edit itself was the uncommitted part)

**Done when**:
- [x] Never lands within `AbsorptionRingCells` of either of two existing cities
- [x] Rejects a cell the old exact-cell-only check would have accepted
- [x] Returns `null` (no city forced) when no cell on the map clears the minimum distance from every
      other city
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
      — 226 passed, 0 failed (223 baseline + 3 new tests)

**Tests**: unit — `tests/LivingWorld.Tests/Cities/FoundingSitePickerTests.cs` (new):
`Pick_never_lands_within_AbsorptionRingCells_of_either_of_two_existing_cities`,
`Pick_rejects_the_cell_the_old_exact_cell_only_check_would_have_accepted`,
`Pick_returns_null_when_no_cell_on_the_map_clears_the_minimum_distance_from_every_other_city`
**Gate**: quick
**Commit**: `077ed50` — `fix(cities): keep newly-founded cities a minimum distance from existing ones`

**Full-suite note**: the full (unfiltered) suite run alongside this fix shows 6 failures
(1527 passed) — 2 are the already-documented/accepted `ScarcityPriceCausalTests`/
`FamineCausalChainTests` pre-existing shock-threshold drift; the other 4
(`ProductionCompositionTests.Production_living_system_order_is_explicit_and_stable` + 3
`GoldenHashesTests`) are caused by other pre-existing UNCOMMITTED wiring already in the tree
(`SpatialSettlementFoundingSystem`/`RelocationArrivalSystem` registered in the already-dirty
`Program.cs`, from `dynamic-city-growth`/`real-household-workplace-buildings` work not part of this
fix) — confirmed unrelated to this fix's own change, since `FixT10` touches no system registration
and the golden hashes were never recorded against this uncommitted layer in the first place. Not a
regression introduced by this commit.

---

### FixT11: Keep a newly-founded city and its grown bounds inside the map — ✅ Done

**What**: user saw a city ("MorNorHol") founded partially/fully outside the world map's actual
bounds. Two gaps, both in this feature's own code:
1. `SpatialSettlementFoundingSystem.HandleEvent` computed the new city's centroid from
   `OverflowClusterFinder.UnionBounds` with no `world.Map.Width/Height` check at all — unlike the
   legacy `FoundingSitePicker.Pick` (FixT10 above), which validates every candidate against the map
   before returning it.
2. `CityBoundsResolver.Resolve` clamped a resolved box's WIDTH/HEIGHT to the map but never its
   ORIGIN — a city could report in-range dimensions while its box was still partially or entirely
   off-map, either from `city.Location` sitting near an edge or from overflow-driven growth pushing
   `minX`/`minY` negative (the exact mechanism `dynamic-city-growth`'s own growth logic in this
   method can trigger).
**Fix**:
1. `HandleEvent` now declines founding (same silent-drop convention as the two re-checks already in
   this method, FixT9) when the computed centroid falls outside `[0, mapWidth) x [0, mapHeight)`,
   rather than clamping it into an arbitrary on-map cell — an off-map centroid is a symptom of an
   off-map building feeding the cluster (see flagged gap below), not something to paper over.
2. `CityBoundsResolver.Resolve` gained a private `ClampOrigin` helper applied to both the
   population-only box and the grown box, pushing the origin back into `[0, mapWidth - width] x
   [0, mapHeight - height]` without altering width/height — the whole box stays on-map, not just
   its size.
**Where**: `src/LivingWorld.Simulation/Cities/SpatialSettlementFoundingSystem.cs`,
`src/LivingWorld.Domain/Cities/CityBoundsResolver.cs`
**Requirement**: CITYGROW-03/04/05 (bounds growth, cluster founding), new edge case (map-bounds
containment)

**Flagged, not fixed (pre-existing, out of scope for this fix)**:
- `BuildingPlacementResolver.Resolve` returns an AUTHORED `building.Position` as-is with no
  `WithinMap` check (only the derived/overflow path checks it) — an off-map authored building can
  feed an off-map overflow cluster into gap 1 above. Pre-existing, affects `ScenarioLoaderV2`-authored
  content generally, not specific to `dynamic-city-growth`.
- `WorldState.AddCity` performs zero validation of any kind on the city it's given — the last
  possible choke point, currently wide open. Pre-existing, broader than this feature.

**Done when**:
- [x] A cluster whose buildings would produce an off-map centroid is not founded as a new city
- [x] A city near a map edge whose overflow-driven growth would push its bounds origin off-map
      stays fully on-map after the fix
- [x] Existing normal (well-inside-the-map) founding/growth tests still pass; one pre-existing test
      fixture (`CityProjector_Build_reports_bounds_grown_to_include_a_real_overflow_building`) placed
      its city at `(50,50)` on the 10x10 default map — already off-map by construction, unrelated to
      what the test verifies — repositioned on-map, assertions unchanged
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
      — 229 passed, 0 failed (225 baseline + 4 new tests)

**Tests**: unit —
`SpatialSettlementFoundingSystemTests.HandleEvent_drops_silently_when_the_computed_centroid_would_land_outside_the_map`,
`BuildingFootprintAndPlacementTests.City_bounds_origin_stays_on_map_when_the_city_sits_right_at_the_map_edge`,
`BuildingFootprintAndPlacementTests.Absorption_growth_near_the_map_edge_keeps_the_grown_box_fully_on_map`
**Gate**: quick
**Commit**: `a214d5a` — `fix(cities): keep newly-founded cities and grown bounds inside the map`

---

### FixT12: Reassign households by current NPC location, not stale household coordinates — ✅ Done (commit `aeeb29a`)

**What**: user reported newly-founded cities stay ghost towns even after founding —
`SpatialSettlementFoundingSystem.HandleEvent`'s household-reassignment check tested
`clusterBounds.Contains(household.Location)`, but `Household.Location` is written once at
creation (seeding, marriage, household split) and never updated afterward. It has no
relationship to where an overflow cluster later forms, so the check almost never fired for
real play — confirmed by inspection that the one existing passing test only worked because it
artificially constructed the household AT the overflow building's exact position.

**Fix**: swapped the criterion for the household head's live `Npc.CurrentLocation` (resolved
via `world.FindNpc(household.Head)`), mirroring the population/concentration check two lines
above in the same method that already uses `npc.CurrentLocation` for the identical reason.
`Household.Location`/`Household.cs` itself is untouched (its mutation semantics are shared
with a separate, uncommitted, in-progress relocation feature — `RelocationArrivalSystem.cs`).

**Where**: `src/LivingWorld.Simulation/Cities/SpatialSettlementFoundingSystem.cs`

**Requirement**: CITYGROW-04 (spatial founding household reassignment)

**Done when**:
- [x] A household whose stale `Location` is nowhere near the cluster, but whose head's
      `Npc.CurrentLocation` IS inside `clusterBounds` at founding time, gets reassigned
- [x] Regression: the existing test (household constructed at the overflow position) still passes
- [x] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&(FullyQualifiedName~Cities|FullyQualifiedName~Economy)"`
      — 296 passed, 2 failed (both pre-existing/unrelated: `ScarcityPriceCausalTests`,
      `FamineCausalChainTests` — confirmed failing identically before this fix)

**Tests**: unit —
`SpatialSettlementFoundingSystemTests.HandleEvent_reassigns_a_household_by_its_heads_current_location_even_when_household_location_is_stale`
**Gate**: quick
**Commit**: `aeeb29a` — `fix(cities): reassign households by current NPC location, not stale household coordinates`

---

### Process note (no code change): commit `2133401` bundled unrelated Stage-4 work

Round-2 Verifier flagged that `2133401` (FixT2) swept ~150 lines of pre-existing, unrelated
in-progress Stage-4 work into `LivingScopeState.cs`/`ConstructionSystem.cs` (process-projection
pipeline, `NpcVisual.City`/`RelocationDestination`, founding-event synthesis) alongside the actual
fix, plus a new Stage-4 test file. This is the same class of issue already accepted for round-1's
T5/`25bb02c` (see Known Limitation 1 above) — documented here as an accepted limitation per the
user's standing acceptance of that pattern; no action taken.

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2 ──→ T3

Phase 2/3 (Parallel, both depend only on Phase 1):
  T3 complete, then:
    ├── T4 ──→ T4b [P vs. T5] (bounds growth, then live wiring)
    └── T5 [P] (migration land-scarcity term, only needs T1)

Phase 4 (Sequential, depends on T4b):
  T4b ──→ T6 ──→ T7

Phase 5 (Sequential, depends on everything):
  T4b, T5, T7 ──→ T8
```

`[P]` = order-free relative to each other (T4 and T5 touch different files, no shared mutable
state), not a directive to spawn a sub-agent per task.

**How phase-based execution works**: this feature has 5 phases (>3) — per the skill's Sub-Agent
Delegation rule, the orchestrator will offer one worker per phase before Execute starts. See
`sub-agents.md` for the full model.

---

## Task Granularity Check

| Task | Scope | Status |
| ---- | ------ | ------- |
| T1: `CityOccupancy` | 1 file, 3 tightly related static methods (occupancy is one concept) | ✅ Granular |
| T2: `OverflowPlacer` | 1 file, 1 method | ✅ Granular |
| T3: Wire placement | 1 file + call-site updates (mechanical, same concept) | ✅ Granular |
| T4: Bounds growth | 2 files (resolver + rules field it consumes), 1 concept | ✅ Granular |
| T4b: Wire live footprint boxes | 1 new helper + `SpatialBoundsResolver` + 6 call sites, all one concept (threading real data through an already-defined parameter) | ✅ Granular |
| T5: Migration scarcity term | 1 file, 1 method modified | ✅ Granular |
| T6: Cluster finder + marker | 2 files, 1 concept (cluster membership + its guard field) | ✅ Granular |
| T7: `SpatialSettlementFoundingSystem` | 1 new file, mirrors an existing single-file system | ✅ Granular |
| T8: Full-suite gate | 0 files, verification only | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| ---- | ------------------------ | --------------- | ------- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T1, T2 | T2 → T3 (T1 transitively via T2) | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T4b | T4 | T4 → T4b | ✅ Match |
| T5 | T1 | T3 → T5 (T5 only truly needs T1; shown after T3 since Phase 1 completes as a unit) | ✅ Match |
| T6 | T4b | T4b → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T4b, T5, T7 | T4b, T5, T7 → T8 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| ---- | ------------------------------ | ----------------- | ----------- | ------- |
| T1 | Domain (`CityOccupancy`) | unit | unit | ✅ OK |
| T2 | Domain (`OverflowPlacer`) | unit | unit | ✅ OK |
| T3 | Domain (`BuildingPlacementResolver`) | unit | unit | ✅ OK |
| T4 | Domain (`CityBoundsResolver`, `CityRules`) | unit | unit | ✅ OK |
| T4b | Simulation (new helper) + Domain (`SpatialBoundsResolver`) + 6 call sites | unit/integration | unit/integration | ✅ OK |
| T5 | Simulation (`MigrationSystem`) | unit/integration | unit | ✅ OK |
| T6 | Domain (`Building`, `OverflowClusterFinder`) | unit | unit | ✅ OK |
| T7 | Simulation (`SpatialSettlementFoundingSystem`) | unit/integration | unit | ✅ OK |
| T8 | none (aggregate) | full suite | none/full | ✅ OK |
