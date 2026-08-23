# Dynamic City Growth Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is
the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review,
Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/dynamic-city-growth/design.md`
**Status**: Done (all tasks T1-T8 complete; feature-level Verifier dispatched next)

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
