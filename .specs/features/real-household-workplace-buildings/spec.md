# Real Household & Workplace Buildings Specification

## Problem Statement

`Household.Location` and `Workplace.Location` have always been bare `CellCoord` fields assigned
at seeding/loading time — neither has ever been backed by a real `Building` entity. The only path
that does this correctly is `ConstructionSystem.CompleteProject`'s demand-driven workplace
construction (which resolves a real, collision-checked position via `BuildingPlacementResolver`
and creates a matching `Building`). Every other creation path — household seeding
(`PopulationGenerator.PairIntoHouseholds`), default-scenario workplace seeding
(`ScenarioRunner.SeedDefaultWorkplaces`), and authored-scenario workplace loading
(`ScenarioLoaderV2.LoadWorld`) — assigns a coordinate with nothing to render there. Since the
city map only ever draws buildings from `world.Buildings`, this means residents' houses and most
workplaces are invisible: NPCs are reported "sleeping at Domicílio 2" or "working" at a real
in-world coordinate with no building on the tile.

## Goals

- [ ] Every household created going forward gets exactly one real, placed `Building` (a house)
      via the same collision-checked, occupancy-aware placement machinery `dynamic-city-growth`
      already built (`BuildingPlacementResolver`/`CityOccupancy`/`OverflowPlacer`).
- [ ] Every workplace created going forward — whether seeded at world creation, loaded from an
      authored scenario, or built later via demand-driven construction — gets exactly one real,
      placed `Building` the same way.
- [ ] A household's/workplace's reported location and its `Building`'s actual position can never
      drift out of sync with each other.

## Out of Scope

| Feature | Reason |
| ------- | ------ |
| Backfilling existing/already-running worlds | User explicitly scoped this to new worlds only — "pode seguir sempre pensando em novos mundos... irei criar novos mundos" to test. Households/workplaces in worlds created before this feature ships keep their current bare-coordinate behavior. |
| Multi-household or multi-occupant buildings (hotels, taverns, apartment-style housing) | User explicitly rejected this — "ainda não temos o conceito de hotel, taverna ou prédios que possam viver mais de 1 pessoa." One household per house, one house per household, no exceptions. |
| Changing `ConstructionSystem`'s existing demand-driven workplace path | Already correct (resolves via `BuildingPlacementResolver`, creates a real `Building`) — this feature only fixes the paths that don't, it doesn't touch the one that already works. |
| Growing/overflow/absorption behavior for these new buildings | Already delivered by `dynamic-city-growth` (`.specs/features/dynamic-city-growth/`) — a seeded house/workplace is just another building placed through that same machinery; no new growth logic needed here. |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --------------------- | --------------- | --------- | ---------- |
| Backfill | None — new worlds only | User's explicit instruction | y |
| One building per household, never shared | Every household gets its own house `Building`; no two households ever own the same one | User's explicit correction — no hotel/tavern/shared-housing concept exists yet | y |
| Single source of truth for position | The household's/workplace's placement is resolved ONCE, at creation time, via `BuildingPlacementResolver` — the SAME resolved `CellCoord` is written to both the new `Building` (as an authored `Position`, so it never re-derives to a different value later) and the household's/workplace's own `Location` field in the same operation. This is chosen over a live "always look up the position from the Building" join, because `Household`/`Workplace` live in `LivingWorld.Domain` (no `WorldState` access) and are read from dozens of existing call sites (`RestPlaceResolver`, `NeedsRules`, `FoodResolver`, `ProductionSystem`, `VacancyIndex`, etc.) that would all need `WorldState` threaded through just to read a coordinate — a much bigger ripple than the "never drift" guarantee actually requires. Authoring the `Building`'s `Position` at creation (instead of leaving it null/re-derived like `ConstructionSystem` currently does) is what makes "can never drift" literally true: the same value is written twice in one atomic step, and the `Building`'s position is then fixed forever, not recomputed. | y — agent's engineering judgment on user's "replace, single source of truth" intent; flagged for Design sign-off since it's a real architectural choice, not the only valid reading |
| Seeding order (city must exist before a household/workplace can be placed inside it) | `ScenarioLoaderV2.LoadWorld` currently calls `world.AddWorkplace` (line ~47) BEFORE `world.AddCity` in at least one authored-city loop (line ~61) — occupancy-aware placement needs a real `City`/bounds to resolve against. Design must address this ordering; exact fix (reorder the loop, or defer placement resolution to a second pass after all cities exist) is a Design decision, not resolved here. | Found during Specify's codebase scan, not yet discussed with user | n — flagged for Design |
| Applies to which workplace creation paths | Both `ScenarioRunner.SeedDefaultWorkplaces` (default/scale scenarios) and `ScenarioLoaderV2.LoadWorld` (authored scenarios) — user's report ("vi NPCs trabalhando em tile vazio") and the investigation confirmed both lack a `Building` | y |
| Applies to which household creation path | `PopulationGenerator.PairIntoHouseholds` (the only path that creates households today) | y |

**Open questions:** none — all resolved above or logged as an explicit default pending Design sign-off (the seeding-order issue and the single-source-of-truth mechanism).

---

## User Stories

### P1: Households get a real placed house ⭐ MVP

**User Story**: As someone watching the simulation, I want every household to have a real house
building on the map matching where the inspector says they sleep, so the world looks coherent
instead of NPCs sleeping at coordinates with nothing there.

**Why P1**: This is the exact bug the user reported (screenshot: "Domicílio 2" with no house
rendered) — the core, concrete complaint.

**Acceptance Criteria**:

1. WHEN a household is created (`PopulationGenerator.PairIntoHouseholds`) THEN the system SHALL
   resolve a real, collision-checked position for it via `BuildingPlacementResolver` (reusing
   `dynamic-city-growth`'s occupancy/overflow machinery when the city has no free cell) and create
   exactly one `Building` (a house-type building) at that position.
2. WHEN that `Building` is created THEN the household's `Location` SHALL equal the `Building`'s
   authored `Position` exactly — the same resolved value, written in the same operation.
3. WHEN the city map is rendered for a city containing that household THEN the house `Building`
   SHALL appear on the map (this falls out of Goal 1 for free — the render pipeline already draws
   every `Building` in `world.Buildings`, per `CityProjector.cs`, no frontend change needed here).
4. WHEN two households are created in the same city THEN they SHALL NEVER resolve to the same
   `Building`/position — one house per household, always (per Out of Scope: no sharing).

**Independent Test**: Seed a new world; for every household in `world.Households`, assert a
`Building` exists in `world.Buildings` whose resolved position equals that household's `Location`,
and that no two households share the same `Building`.

---

### P1: Workplaces get a real placed building ⭐ MVP

**User Story**: As someone watching the simulation, I want every workplace — however it was
created — to have a real building on the map, so NPCs never appear to work at an empty tile.

**Why P1**: Same user report, same root cause, confirmed to affect the MORE common workplace
creation paths (seeding/loading), not just the already-correct demand-driven one.

**Acceptance Criteria**:

1. WHEN a workplace is created via `ScenarioRunner.SeedDefaultWorkplaces` (default/scale
   scenarios) THEN the system SHALL resolve a real position via `BuildingPlacementResolver` and
   create exactly one `Building` for it, the same way as for households.
2. WHEN a workplace is created via `ScenarioLoaderV2.LoadWorld` (authored scenarios) THEN the
   system SHALL do the same, for every authored workplace, regardless of whether its authored
   `Location` in the scenario JSON is already free or already occupied by something else (occupancy
   is always re-checked, never trusted blindly from authored data).
3. WHEN a workplace is created via `ConstructionSystem.CompleteProject` (existing, unchanged) THEN
   behavior SHALL remain exactly as it is today (already correct) — this feature does not modify
   that path.
4. WHEN any workplace's `Building` is created THEN the workplace's `Location` SHALL equal that
   `Building`'s authored `Position` exactly, same single-source-of-truth guarantee as households.

**Independent Test**: Seed a new default-scenario world and a new authored-scenario world; for
every workplace in `world.Workplaces` in both, assert a `Building` exists whose resolved position
equals that workplace's `Location`.

---

## Edge Cases

- WHEN a city has no free cell left when a household/workplace needs placing THEN the SAME
  overflow-then-land-scarcity behavior `dynamic-city-growth` already defined SHALL apply — nearest
  free cell outside bounds, or (if the whole map is genuinely full) land-scarcity signal, no queue,
  no silent failure. No new placement-failure behavior invented here.
- WHEN `ScenarioLoaderV2` authors a workplace `Location` that collides with another already-placed
  building THEN the system SHALL NOT honor the authored coordinate blindly — it SHALL run the same
  occupancy check as any other placement and resolve to a genuinely free cell instead, since
  honoring a colliding authored coordinate would recreate the exact bug this feature fixes.
- WHEN a city doesn't exist yet at the point a household/workplace would be placed (the
  seeding-order issue found during Specify) THEN placement SHALL be deferred until the city exists
  — never resolved against a nonexistent or wrong city. Exact mechanism is a Design decision.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --------------- | ----- | ------ | ------- |
| HOMEWORK-01 | P1: Households get a real placed house | Design | Pending |
| HOMEWORK-02 | P1: Households get a real placed house | Design | Pending |
| HOMEWORK-03 | P1: Households get a real placed house | Design | Pending |
| HOMEWORK-04 | P1: Households get a real placed house | Design | Pending |
| HOMEWORK-05 | P1: Workplaces get a real placed building | Design | Pending |
| HOMEWORK-06 | P1: Workplaces get a real placed building | Design | Pending |
| HOMEWORK-07 | P1: Workplaces get a real placed building | Design | Pending |
| HOMEWORK-08 | P1: Workplaces get a real placed building | Design | Pending |

**ID format:** `HOMEWORK-NN`, one per acceptance criterion above (in order).

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 8 total, 0 mapped to tasks, 8 unmapped ⚠️ (Design/Tasks not yet run)

---

## Success Criteria

- [ ] A newly-created world (default, scale, or authored scenario) has zero households and zero
      workplaces whose `Location` has no matching `Building` in `world.Buildings`.
- [ ] No two households or two workplaces ever resolve to the same `Building`.
- [ ] The city map visibly renders a house/workplace building for every resident/worker, closing
      the exact gap the user's screenshot showed.
