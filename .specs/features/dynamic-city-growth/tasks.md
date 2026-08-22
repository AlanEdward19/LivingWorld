# Dynamic City Growth Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is
the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review,
Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/dynamic-city-growth/design.md`
**Status**: In Progress

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
T3 → T4
```

### Phase 3: Land Scarcity → Migration (depends on Phase 1)

```
T3 → T5
```

### Phase 4: Spatial Founding (Sequential, depends on Phase 2)

```
T4 → T6 → T7
```

### Phase 5: Full Gate (Sequential, depends on all above)

```
T4, T5, T7 → T8
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

### T4: `CityBoundsResolver.Resolve` — grow bounds to absorb overflow buildings

**What**: Add `AbsorptionRingCells` to `CityRules` (default 3). Extend `CityBoundsResolver.Resolve` to union the population-derived box with the bounding box of the city's own buildings positioned within `AbsorptionRingCells` of that box's edge, still capped only by the existing hard map-edge limit (population-only `MaxSize` no longer applies once overflow buildings are present).
**Where**: `src/LivingWorld.Domain/Cities/CityBoundsResolver.cs`, `src/LivingWorld.Domain/Cities/CityRules.cs` (or wherever `CityRules` is defined)
**Depends on**: T3
**Reuses**: Existing `SideFor`/map-limit math for the population-derived half

**Requirement**: CITYGROW-03, CITYGROW-05

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] A city with an overflow building within `AbsorptionRingCells` of its population-derived bounds resolves to a larger box including that building's full footprint
- [ ] Growth never exceeds the existing hard map-dimension cap (`Math.Min(mapWidth, mapHeight)/2`) — reuse/extend the existing `City_bounds_never_exceed_the_smaller_map_dimension...` test
- [ ] Existing residents'/buildings' positions are unchanged by a bounds-growth resolution (spec AC5) — only the bounds rectangle grows
- [ ] `CityRulesTests.cs` (existing) covers the new `AbsorptionRingCells` field's validation
- [ ] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [ ] Test count: ≥4 new tests pass (absorbed growth, map-edge cap still holds, positions unchanged, field validation), no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): grow city bounds to absorb nearby overflow buildings`

---

### T5: `MigrationSystem.ScoreOf` — land-scarcity term [P]

**What**: Add a land-scarcity term to `MigrationSystem.ScoreOf`'s existing weighted score: when `CityOccupancy.IsLandScarce` is true for the household's current city (whole map has no free cell for a house-sized footprint), force that city's "stay" score to the theoretical minimum so any other city with room scores higher. No change when `world.Cities.Count < 2` (existing guard already no-ops).
**Where**: `src/LivingWorld.Simulation/Cities/MigrationSystem.cs`
**Depends on**: T1 (needs `CityOccupancy.IsLandScarce`)
**Reuses**: `CityOccupancy.IsLandScarce`, existing `ScoreOf` weighting pattern
**Requirement**: Edge Case — "no free cell anywhere on the map"

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] A household in a land-scarce city (verified via a test world with zero free cells) scores lower for staying than for any other real city with room, causing relocation on the next `Tick`
- [ ] A household in a land-scarce single-city world (no candidate to move to) is unaffected — `Tick`'s existing `world.Cities.Count < 2` guard still no-ops, no crash, no forced relocation to nowhere
- [ ] Existing `MigrationSystemTests.cs` cases (employment/food/security/family-ties scoring) still pass unchanged
- [ ] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [ ] Test count: ≥3 new tests pass (land-scarce relocates, single-city no-op, non-scarce city unaffected), no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): weight land scarcity into MigrationSystem's stay-vs-relocate score`

---

### T6: `Building.ClusterFoundingScheduledAtTick` marker + cluster/population helpers

**What**: Add the nullable `ClusterFoundingScheduledAtTick` field + `MarkClusterFoundingScheduled(tick)` to `Building` (mirrors `City.FoundingScheduledAtTick`). Add a pure helper (e.g. `OverflowClusterFinder`) that groups a city's overflow buildings by mutual distance ≤ `AbsorptionRingCells` (chain/transitive), excludes buildings already within absorption range of any existing city, and computes each cluster's materialized-resident population (`Npc.Location` inside the cluster's bounding box).
**Where**: `src/LivingWorld.Domain/Cities/Building.cs`, `src/LivingWorld.Domain/Cities/OverflowClusterFinder.cs` (new)
**Depends on**: T4 (needs `AbsorptionRingCells` + absorption-range check)
**Reuses**: `NpcScopeResolver`'s geometric-membership pattern (`bounds.Contains(location)`)
**Requirement**: CITYGROW-04

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Clustering groups mutually-close overflow buildings transitively (chain of distance ≤ `AbsorptionRingCells`), excludes any building within an existing city's absorption range
- [ ] Population count only counts materialized (`IsAlive`, non-pool) `Npc`s whose `Location` falls in the cluster's bounding box
- [ ] `ClusterFoundingScheduledAtTick` defaults to `null`, settable exactly once via `MarkClusterFoundingScheduled`
- [ ] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [ ] Test count: ≥4 new tests pass (cluster grouping, absorption exclusion, population count, marker set-once), no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): add overflow cluster finder and Building cluster-founding marker`

---

### T7: `SpatialSettlementFoundingSystem` — found a city from a qualifying cluster

**What**: New monthly `ISimulationSystem`. `Tick`: for each city, find qualifying overflow clusters (via T6's finder) whose population clears `rules.FoundingConcentrationThreshold` via the SAME formula as `SettlementFoundingSystem` (`population / (population + 1)`); schedule a founding event (`ctx.ScheduleEvent` + `OrganizationTicks` delay, same cadence/mechanism as `SettlementFoundingSystem`), mark every building in the cluster via `MarkClusterFoundingScheduled`. `HandleEvent`: re-verify the concentration threshold still holds for the captured cluster at fire time (drop silently if not); otherwise create the new `City` at the cluster centroid (`CityNameGenerator`, `world.NextCityId()`), reassign `Building.City` for the cluster's buildings, reassign `Household.City`/cascaded `Npc.City` (via existing `JoinCity`) for households whose `Location` falls in the new city's initial resolved bounds.
**Where**: `src/LivingWorld.Simulation/Cities/SpatialSettlementFoundingSystem.cs` (new), registered wherever `SettlementFoundingSystem` is registered (likely `Program.cs`/system list)
**Depends on**: T6
**Reuses**: `SettlementFoundingSystem`'s scheduling pattern + concentration formula, `CityNameGenerator`, `NpcScopeResolver`'s geometric-membership technique
**Requirement**: CITYGROW-04, Edge Cases (absorption precedence, no premature founding, no double-founding)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] A cluster with enough materialized residents to clear the concentration threshold schedules a founding event on the same monthly/`OrganizationTicks` cadence as `SettlementFoundingSystem`
- [ ] A cluster with buildings but too few/zero residents (1 house, 1 person) never schedules — concentration formula stays below threshold (reuses existing math, no separate building-count check)
- [ ] A cluster already within an existing city's absorption range (per T4) is skipped — absorption takes precedence over founding, per spec Edge Cases
- [ ] The same cluster is never scheduled twice (`ClusterFoundingScheduledAtTick` guard)
- [ ] At fire time, a cluster that thinned out below threshold during the wait is silently dropped (no city forced into existence)
- [ ] On success: new `City` exists, cluster's buildings reassigned to it, households geometrically inside its bounds reassigned (`Household.City` + member `Npc.City`)
- [ ] Gate check passes: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
- [ ] Test count: ≥6 new tests pass (qualifying cluster founds, weak cluster doesn't, absorption precedence, no double-schedule, fire-time re-verify drops stale cluster, household reassignment), no silent deletions

**Tests**: unit
**Gate**: quick

**Commit**: `feat(cities): add SpatialSettlementFoundingSystem for overflow-cluster city founding`

---

### T8: Full-suite gate (no code change)

**What**: Run the full backend suite (no filter, `Category=Scenario` still excluded by `test.sh`'s default) to confirm no cross-feature regression from the signature changes in T3/T4 and the new sibling system in T7.
**Where**: n/a
**Depends on**: T4, T5, T7
**Reuses**: n/a
**Requirement**: n/a (project-wide safety net, not a spec AC)

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] `bash scripts/test.sh` passes with zero new failures beyond the 2 pre-existing, already-documented flaky failures noted in `.specs/STATE.md` (`Vitality_cv_...`, `Storage_cost_per_alive_npc_stable_across_horizons`)
- [ ] Total test count reported and compared against the pre-feature baseline (no silent deletions across the whole feature)

**Tests**: none (aggregate gate only)
**Gate**: full

**Commit**: none (verification-only task; if it surfaces a regression, that becomes a new task, not folded into this one)

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 ──→ T2 ──→ T3

Phase 2/3 (Parallel, both depend only on Phase 1):
  T3 complete, then:
    ├── T4 [P] (bounds growth)
    └── T5 [P] (migration land-scarcity term, only needs T1)

Phase 4 (Sequential, depends on T4):
  T4 ──→ T6 ──→ T7

Phase 5 (Sequential, depends on everything):
  T4, T5, T7 ──→ T8
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
| T5 | T1 | T3 → T5 (T5 only truly needs T1; shown after T3 since Phase 1 completes as a unit) | ✅ Match |
| T6 | T4 | T4 → T6 | ✅ Match |
| T7 | T6 | T6 → T7 | ✅ Match |
| T8 | T4, T5, T7 | T4, T5, T7 → T8 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| ---- | ------------------------------ | ----------------- | ----------- | ------- |
| T1 | Domain (`CityOccupancy`) | unit | unit | ✅ OK |
| T2 | Domain (`OverflowPlacer`) | unit | unit | ✅ OK |
| T3 | Domain (`BuildingPlacementResolver`) | unit | unit | ✅ OK |
| T4 | Domain (`CityBoundsResolver`, `CityRules`) | unit | unit | ✅ OK |
| T5 | Simulation (`MigrationSystem`) | unit/integration | unit | ✅ OK |
| T6 | Domain (`Building`, `OverflowClusterFinder`) | unit | unit | ✅ OK |
| T7 | Simulation (`SpatialSettlementFoundingSystem`) | unit/integration | unit | ✅ OK |
| T8 | none (aggregate) | full suite | none/full | ✅ OK |
