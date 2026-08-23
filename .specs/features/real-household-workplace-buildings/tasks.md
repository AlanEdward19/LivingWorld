# Real Household & Workplace Buildings Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name and follow its
Execute flow and Critical Rules.** Do not search for skill files by filesystem path. The skill is
the source of truth for the full flow (per-task cycle, sub-agent delegation, adequacy review,
Verifier, discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed without it.**

---

**Design**: `.specs/features/real-household-workplace-buildings/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase (`tests/LivingWorld.Tests/Population/PopulationGeneratorTests.cs`,
> `tests/LivingWorld.Tests/Cities/*.cs` sampled) and this project's guidelines
> (`AGENTS.md`/`scripts/test.sh`; user's standing gate-cadence feedback — per-task new tests + a
> narrow filtered run, `verify.sh`/`Category=Scenario` only at feature close).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| ---------- | ------------------- | --------------------- | ----------------- | ------------ |
| Domain (`CityCatalog` house recipe, `PopulationGenerator`'s new location-input parameter) | unit | All branches; 1:1 to spec ACs HOMEWORK-01/02/04 and listed Edge Cases | `tests/LivingWorld.Tests/Population/*Tests.cs`, `tests/LivingWorld.Tests/Cities/CityCatalogTests.cs` | `bash scripts/test.sh --filter "Category!=Scenario&(FullyQualifiedName~Population\|FullyQualifiedName~Cities)"` |
| Simulation (`PopulationSeeder`, `ScenarioRunner.SeedDefaultWorkplaces`, `ScenarioLoaderV2`) | unit/integration (in-memory `WorldState`, same style as existing seeder/loader tests) | All branches; 1:1 to spec ACs HOMEWORK-01..08 and listed Edge Cases | `tests/LivingWorld.Tests/Population/*Tests.cs`, `tests/LivingWorld.Tests/ScenarioLoaderV2Tests.cs` (or wherever that suite lives — confirm exact file in T2/T4) | same filter as above |
| Full backend suite (final task only) | full run, no filter (excludes `Category=Scenario` by `test.sh` default) | Nothing regresses project-wide | n/a | `bash scripts/test.sh` |
| Full gate (`verify.sh`, `Category=Scenario`) | build/lint/docs + long scenario run | Feature-close only, per user's standing feedback | n/a | `bash scripts/verify.sh`; user runs `Category=Scenario` themselves |

## Parallelism Assessment

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --------- | --------------- | ----------------- | --------- |
| Domain/Simulation unit tests | Yes | Each test builds its own fresh `WorldState`/`City`/catalog in-memory, no shared static state | Same pattern as every `dynamic-city-growth` test and existing `PopulationGeneratorTests.cs` |
| Full suite run | N/A (sequential invocation; xUnit's own intra-suite parallelism unaffected) | n/a | existing `test.sh` behavior |

## Gate Check Commands

| Gate Level | When to Use | Command |
| ---------- | ------------ | -------- |
| Quick | After each task below | `bash scripts/test.sh --filter "Category!=Scenario&(FullyQualifiedName~Population\|FullyQualifiedName~Cities\|FullyQualifiedName~ScenarioLoaderV2)"` |
| Full | After the last task | `bash scripts/test.sh` |
| Build/Scenario (feature close only) | User decides to close the feature | `bash scripts/verify.sh` (user runs `Category=Scenario` separately) |

---

## Execution Plan

### Phase 1: Independent groundwork (Parallel)

```
T1 [P]  (confirm/add house BuildingTypeId)
T3 [P]  (default-scenario workplace placement)
T4 [P]  (authored-scenario workplace placement + reorder)
```

### Phase 2: Household placement (depends on T1)

```
T1 → T2
```

### Phase 3: Full Gate (depends on all above)

```
T2, T3, T4 → T5
```

---

## Task Breakdown

### T1: Confirm/add a house `BuildingTypeId` in `CityCatalog` [P]

**What**: Read `src/LivingWorld.Domain/Cities/CityCatalog.cs` in full. If a house/residential
`BuildingRecipe` already exists (used today only by `ConstructionDemandSystem`'s aggregate
housing-capacity math), reuse its `BuildingTypeId` — record which one in this task's Done-when
and in T2's task body. If none exists, add the minimal recipe needed (following the exact shape
of existing recipes in the same catalog — inputs/outputs/`TicksToBuild`/`HousingCapacityProvided`
etc.), matching whatever a "house" should look like per the catalog's existing conventions. Do
NOT invent a second, parallel house-type concept if one already exists.
**Where**: `src/LivingWorld.Domain/Cities/CityCatalog.cs` (read-only confirmation, or minimal
addition if genuinely missing)
**Depends on**: None
**Reuses**: Existing `BuildingRecipe`/`CityCatalog` shape
**Requirement**: HOMEWORK-01, HOMEWORK-02 (both need a real house building type to place)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] The house `BuildingTypeId` to use for this feature is identified and documented in this
      task's notes (either "reused existing id N" or "added new recipe, id N")
- [ ] If added: `CityCatalogTests.cs` covers the new recipe's validation same as existing recipes
- [ ] Gate check passes
- [ ] Test count: existing count unchanged if reusing, +N if adding (documented)

**Tests**: unit (only if a recipe is added; none if purely confirming an existing one)
**Gate**: quick
**Commit**: `feat(cities): add house building recipe` (only if one was missing) — or no commit if
purely a confirmation (fold the finding into T2's commit instead)

---

### T2: Household placement — `PopulationSeeder` + `PopulationGenerator`

**What**: Read `src/LivingWorld.Domain/Population/PopulationGenerator.cs`'s `PairIntoHouseholds`
(and whatever calls it, e.g. `GenerateInitial`) in FULL before touching anything — this design
only summarized it. Change its signature to accept an `IReadOnlyList<CellCoord> householdLocations`
(one per household it's about to create, same order it already pairs NPCs into households) instead
of whatever internal spawn-radius/village-fallback scatter logic currently picks each household's
location. In `src/LivingWorld.Simulation/Population/PopulationSeeder.cs` (the caller), BEFORE
calling the generator: for the number of households about to be created, resolve each one's real
position via `BuildingPlacementResolver.Resolve` (using T1's house `BuildingTypeId`,
`CityOccupancy.ResolveGrownBounds` for the bounds argument) and call `world.AddBuilding` for each
resolved position (authored, i.e. pass the resolved `CellCoord` as the `Building`'s `position`
constructor argument so it's fixed, not re-derived) — then pass that exact list of positions into
the generator. The household's `Location` (still assigned inside `PairIntoHouseholds`/wherever
`new Household(...)` is called) must equal the corresponding `Building`'s position exactly (same
value, from the same list).
**Where**: `src/LivingWorld.Domain/Population/PopulationGenerator.cs`,
`src/LivingWorld.Simulation/Population/PopulationSeeder.cs`
**Depends on**: T1
**Reuses**: `BuildingPlacementResolver.Resolve`, `CityOccupancy.ResolveGrownBounds`
**Requirement**: HOMEWORK-01, HOMEWORK-02, HOMEWORK-04

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Every household `PopulationSeeder.SeedInitial` creates has a matching `Building` in
      `world.Buildings` whose position equals that household's `Location` exactly
- [ ] No two households ever resolve to the same `Building`/position (assert directly)
- [ ] `PopulationGeneratorTests.cs` (existing) updated to pass in explicit locations instead of
      relying on whatever internal scatter it exercised before — same test intent preserved, not
      weakened (if a test specifically asserted the OLD scatter behavior, either adapt it to
      assert the new location-list-consuming behavior or, if it tested something now
      structurally impossible, note why in the commit — do not silently delete)
- [ ] Gate check passes
- [ ] Test count: ≥3 new tests (household-gets-real-building, no-two-households-share-a-building,
      location-equals-building-position), existing `PopulationGeneratorTests.cs` tests still pass
      (possibly adapted, not deleted)

**Tests**: unit
**Gate**: quick
**Commit**: `feat(population): give every seeded household a real, placed house building`

---

### T3: Default-scenario workplace placement — `ScenarioRunner.SeedDefaultWorkplaces` [P]

**What**: Read `src/LivingWorld.Simulation/ScenarioRunner.cs`'s `SeedDefaultWorkplaces` in full.
Replace the bare `DefaultVillageLocation` assignment for each default workplace (farm, forge) with
a real resolved position via `BuildingPlacementResolver.Resolve` (same pattern as
`ConstructionSystem.CompleteProject` already uses correctly) — create the matching `Building`
(authored position) before constructing each `Workplace`.
**Where**: `src/LivingWorld.Simulation/ScenarioRunner.cs`
**Depends on**: None
**Reuses**: `BuildingPlacementResolver.Resolve`, `CityOccupancy.ResolveGrownBounds`
**Requirement**: HOMEWORK-05, HOMEWORK-08

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Both default workplaces (farm, forge) each get a real `Building` whose position equals the
      workplace's `Location`
- [ ] The two workplaces never resolve to the same position
- [ ] Gate check passes
- [ ] Test count: ≥2 new tests (each default workplace has a matching building; the two don't
      collide), no silent deletions

**Tests**: unit
**Gate**: quick
**Commit**: `feat(economy): give default-scenario workplaces real placed buildings`

---

### T4: Authored-scenario workplace placement — `ScenarioLoaderV2.LoadWorld` [P]

**What**: Read `src/LivingWorld.Simulation/ScenarioLoaderV2.cs`'s `LoadWorld` in full (design.md
already found the exact ordering bug — the `definition.Economy.Workplaces` loop runs before the
`definition.City.Cities` loop; verify this yourself against the current file before editing, it
may have shifted). Reorder so cities are created first. For each authored workplace: (1) assign it
to its nearest city by distance (Chebyshev, consistent with `CityBoundsResolver`'s existing
metric) — handle the degenerate zero-city case explicitly (decide and document the fallback,
e.g. skip real placement and keep today's bare-`Location` behavior only for that edge case,
marked with a `// SPEC_DEVIATION` comment if so); (2) check `CityOccupancy.IsFree` at the
AUTHORED `workplace.Location` first — if free, create the `Building` there (authored, preserving
resource-adjacency intent per design.md); (3) if occupied, fall back to
`BuildingPlacementResolver`'s normal resolution (free cell in bounds, then overflow).
**Where**: `src/LivingWorld.Simulation/ScenarioLoaderV2.cs`
**Depends on**: None
**Reuses**: `CityOccupancy.IsFree`, `BuildingPlacementResolver.Resolve`, `ChebyshevGap`-style
distance metric (reuse or mirror `CityBoundsResolver`'s existing one, don't reinvent)
**Requirement**: HOMEWORK-06, HOMEWORK-08

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] Cities are created before workplaces are placed (verify by reading the reordered code, and
      by a test that would have failed under the old order — e.g. a workplace whose nearest-city
      assignment requires the city to already exist)
- [ ] An authored workplace at a genuinely free location gets a `Building` at that EXACT authored
      position (not moved)
- [ ] An authored workplace whose location collides with something else gets a `Building` at a
      different, real, occupancy-checked position instead (never silently overlapping)
- [ ] Each authored workplace is assigned to its nearest city by distance — test with 2+ cities at
      different distances from one workplace, assert the nearer one is chosen
- [ ] Zero-city degenerate case doesn't crash — explicit test, documented fallback behavior
- [ ] Gate check passes
- [ ] Test count: ≥4 new tests (reorder-matters, authored-location-preferred-when-free,
      fallback-on-collision, nearest-city-wins, zero-city-doesn't-crash — 5 total), no silent
      deletions

**Tests**: unit
**Gate**: quick
**Commit**: `feat(scenario): give authored workplaces real placed buildings, nearest-city assignment`

---

### T5: Full-suite gate (no code change)

**What**: Run the full backend suite to confirm no cross-feature regression from the seeding/
loading changes in T2/T3/T4.
**Where**: n/a
**Depends on**: T2, T3, T4
**Reuses**: n/a
**Requirement**: n/a (project-wide safety net)

**Tools**: MCP: NONE / Skill: NONE

**Done when**:
- [ ] `bash scripts/test.sh` passes with zero new failures beyond already-documented
      pre-existing flaky/unrelated failures (check `.specs/STATE.md` for the current accepted
      list before judging — it may have grown since `dynamic-city-growth` closed)
- [ ] Total test count reported vs. pre-feature baseline (no silent deletions)

**Tests**: none (aggregate gate only)
**Gate**: full
**Commit**: none (verification-only; a regression found here becomes a new task, not folded in)

---

## Parallel Execution Map

```
Phase 1 (Parallel):
  T1 [P]
  T3 [P]
  T4 [P]

Phase 2 (depends on T1):
  T1 ──→ T2

Phase 3 (depends on everything):
  T2, T3, T4 ──→ T5
```

`[P]` = order-free relative to each other (T1/T3/T4 touch different files, no shared mutable
state) — not a directive to spawn a sub-agent per task. This feature has 3 phases (≤3), so per the
skill's Sub-Agent Delegation rule, execution can proceed inline without a formal per-phase
sub-agent offer — though the same phase-worker dispatch pattern used for `dynamic-city-growth` may
still be used operationally if the user prefers it.

---

## Task Granularity Check

| Task | Scope | Status |
| ---- | ------ | ------- |
| T1: House building type | 1 file, read-or-minimal-add | ✅ Granular |
| T2: Household placement | 2 files, 1 concept (feed resolved locations into pairing) | ✅ Granular |
| T3: Default workplace placement | 1 file, 1 concept | ✅ Granular |
| T4: Authored workplace placement | 1 file, 1 concept (reorder + nearest-city + prefer-authored-when-free) | ✅ Granular |
| T5: Full-suite gate | 0 files, verification only | ✅ Granular |

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| ---- | ------------------------ | --------------- | ------- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | None | None | ✅ Match |
| T4 | None | None | ✅ Match |
| T5 | T2, T3, T4 | T2, T3, T4 → T5 | ✅ Match |

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| ---- | ------------------------------ | ----------------- | ----------- | ------- |
| T1 | Domain (`CityCatalog`) | unit (conditional) | unit | ✅ OK |
| T2 | Domain (`PopulationGenerator`) + Simulation (`PopulationSeeder`) | unit | unit | ✅ OK |
| T3 | Simulation (`ScenarioRunner`) | unit | unit | ✅ OK |
| T4 | Simulation (`ScenarioLoaderV2`) | unit | unit | ✅ OK |
| T5 | none (aggregate) | full suite | none/full | ✅ OK |
