# Dynamic City Growth Specification

## Problem Statement

A city's spatial bounds are today a pure formula of population and map size
(`CityBoundsResolver.SideFor`, capped at 12 cells/side) with **no relationship at all** to how
many buildings actually exist. `BuildingPlacementResolver` never checks occupancy or the city's
bounds before placing a building — it always succeeds, hashing a position on a fixed-radius ring
around the city center. So a city can never actually "run out of room": buildings just silently
overlap or sit wherever the hash lands, with no visible or mechanical consequence.

The user wants scarcity to be real and consequential: when a city has no free cell for a needed
house or workplace, that building should be placed **outside** the city's current footprint
instead of overlapping/ignoring the constraint. Overflow buildings that cluster near an existing
city should let that city's footprint grow to absorb them; overflow buildings that cluster far
from any city should be able to found a brand new one, reusing the existing settlement-founding
concept.

## Goals

- [ ] Building placement respects real occupancy: a building only lands inside current city
      bounds if a free cell for its footprint actually exists there.
- [ ] When no free cell exists, the building is placed outside the city's bounds, at the nearest
      free (unoccupied) cell reachable by outward ring-search from the city edge.
- [ ] A city's resolved bounds grow to include its own overflow buildings once they are close
      enough to count as an extension (distance-based), instead of staying capped forever.
- [ ] Overflow buildings clustered far from any existing city can trigger founding a new city
      through the existing `SettlementFoundingSystem` mechanism (city creation + pool move),
      via a new spatial trigger alongside the existing population-concentration trigger.

## Out of Scope

| Feature                                                                 | Reason                                                                                                 |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| Land ownership / terrain claiming (who "owns" a cell before building)   | No such concept exists anywhere in the domain today; out of scope, occupancy = "a building sits here" only |
| NPC work-site assignment logic (hunting/farming site selection)         | Separate system (`BehaviorDecisionSystem`/production); this feature only changes *where a building can physically land*, not which building an NPC is assigned to |
| Visual/UI changes for overflow buildings                                | Already fixed in a separate bugfix session (buildings/workplaces now render from live delta, regardless of position) — no new frontend work needed here |
| Per-workplace shifts/wages/hierarchy redesign                            | Tracked separately, see project memory `project_dynamic_work_schedules` — unrelated axis |
| Changing `SettlementFoundingSystem`'s existing population-threshold path | That trigger stays as-is; this feature adds a second, independent spatial trigger alongside it |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --------------------- | --------------- | --------- | ---------- |
| Occupancy check granularity | A cell is "occupied" if it's part of any building's resolved footprint (existing `BuildingFootprintGenerator`) in that city; free-cell search scans city bounds cell-by-cell for the first origin where the new building's footprint fits without overlap | Reuses the footprint generator that already exists for rendering — no new footprint model | y (derived from "free-cell check", user-selected) |
| Overflow search pattern | Outward ring-search from the city edge (not city center) in increasing radius, first free cell (by deterministic angle order, same hashing style as today's `DerivedPosition`) wins | User picked "nearest free land outside bounds" (most realistic); deterministic order keeps placement stable/non-random per existing codebase convention | y |
| Absorption distance threshold | An overflow building absorbs into its own city's bounds when it sits within `AbsorptionRingCells` (new `CityRules` constant, default 3 — same magnitude as today's `DerivedRingRadius`) cells of the current resolved bounds edge | User picked "distance-based" over count-based; default approved by user | y |
| New-city spatial trigger threshold | Overflow buildings clustered together (mutual distance ≤ `AbsorptionRingCells`), beyond absorption range of any existing city, found a new city only when the cluster's own materialized-resident population clears `rules.FoundingConcentrationThreshold` via the SAME formula `SettlementFoundingSystem` already uses for normal founding (`population / (population + 1)`) — no separate building-count threshold | User rejected a raw building-count bar as too weak ("1 house/1 person doesn't found a city, a society does"); reusing the exact existing concentration formula/threshold means a spatial cluster clears the identical real bar a normal city needs | y |
| Bounds growth is still fully derived, never stored | `CityBoundsResolver`/`SpatialBoundsResolver` formula extended to also cover the extent of the city's own buildings (min bounding box containing population-derived size ∪ all owned building footprints within absorption range) — no new persisted "city size" field | Matches existing architectural principle (bounds/BuildingIds already explicitly *not* stored per `City.cs`'s own doc comments) — avoids introducing dual sources of truth | y |
| Interaction with map-size cap | Absorbed-building growth is allowed to exceed today's population-derived `MaxSize=12` cap, but never exceeds the existing hard map-dimension cap (`Math.Min(mapWidth, mapHeight)/2`) | Overflow is the whole point of this feature — capping it at the same population-only ceiling would defeat it; the map-edge cap still must hold (no city can exceed the world) | y |
| Interaction with upcoming T10 (demand-driven construction, `phase-15.1-stage-4-living-world`) | Independent: T10 gates *whether/when* a building gets queued (capacity/resource demand); this feature only changes *where* an already-approved building lands once construction completes | These are orthogonal concerns (when vs. where) — flagged so Design confirms no overlap when T10 lands | n |

**Open questions:** none — all resolved above or logged as an explicit default pending Design sign-off.

---

## User Stories

### P1: Overflow placement + city growth end-to-end ⭐ MVP

**User Story**: As someone watching the simulation, I want a city that's run out of room to
actually place new houses/workplaces outside its current footprint — and have that footprint
grow to include them when they're close by — so that city growth looks organic instead of
buildings silently overlapping or getting stranded with no visual relationship to any city.

**Why P1**: User explicitly chose the full end-to-end loop (placement + absorption + new-city
founding) as the MVP — a placement-only slice would still leave overflow buildings looking
disconnected from any city, which was the original complaint.

**Acceptance Criteria**:

1. WHEN a building is placed (construction completes) and a free cell for its footprint exists
   within the city's currently resolved bounds THEN the system SHALL place it at that free cell
   (existing hash-ring behavior is retired as the primary path; it may remain as tie-break order
   among multiple free candidates).
2. WHEN a building is placed and NO free cell exists within the city's currently resolved bounds
   THEN the system SHALL place it at the nearest free cell found by outward ring-search from the
   city's bounds edge — near or far, whichever is actually free — and SHALL NOT fail, block, or
   silently overlap another building's footprint. WHEN no free cell exists anywhere on the map
   THEN the system SHALL treat it as land scarcity feeding `MigrationSystem` (see Edge Cases)
   rather than placing or queuing the building.
3. WHEN an overflow building's position is within `AbsorptionRingCells` of the city's
   currently-resolved bounds edge THEN the next resolution of that city's bounds SHALL expand to
   include that building's full footprint, up to the existing hard map-dimension cap.
4. WHEN overflow buildings from one or more households are mutually within `AbsorptionRingCells`
   of each other, all outside every existing city's absorption range, AND the cluster's own
   materialized-resident population clears `rules.FoundingConcentrationThreshold` using the SAME
   formula `SettlementFoundingSystem` already uses (`population / (population + 1)`) THEN the
   system SHALL schedule a new-city founding for that cluster on the same monthly cadence as
   `SettlementFoundingSystem`, using its existing founding mechanism (create `City`, no
   double-founding of the same cluster). A cluster of buildings with too few or zero materialized
   residents SHALL NOT found a city, regardless of building count.
5. WHEN a city's bounds grow to absorb an overflow building THEN existing residents' and
   workplaces' addresses/positions SHALL remain unchanged (growth only extends the bounds
   rectangle/box, it never moves existing buildings).

**Independent Test**: Seed a small city (`MaxSize`-capped bounds) with enough households/
workplaces queued that at least one has no free cell left; run ticks until construction
completes; assert (a) the overflow building's position lands outside the pre-overflow bounds with
no footprint overlap anywhere, (b) a subsequent bounds resolution includes it, (c) if enough
overflow buildings are seeded far from the city, a new `City` appears via the existing founding
event pipeline.

---

## Edge Cases

- WHEN the map itself has no free cell anywhere reachable (fully built world) THEN the system
  SHALL treat this as real land scarcity and feed it into the existing `MigrationSystem`'s daily
  scoring (the household is pulled toward emigrating to a city elsewhere) — matches how real land
  scarcity drives emigration, not a synthetic backlog.
- WHEN a `ConstructionSystem` project completes payment but `BuildingPlacementResolver.Resolve`
  returns "no placement possible" for it (the same whole-map land-scarcity condition above) THEN
  the project SHALL remain queued for retry on a later tick — its resources are never lost and no
  orphan `Building` is ever created — AND it SHALL NOT block any other project in the same city's
  `ConstructionQueue` from advancing behind it (a stuck project is skipped for resource-consumption
  purposes every tick until placement succeeds, retried again next tick, while the next
  not-yet-stuck project in the queue receives that tick's resource budget instead).
  **(Amendment, 2026-08-23, AD-007):** this replaces an earlier "no queue, no persisted queue, no
  special-cased retry logic" wording for this exact condition — that wording described the
  *building-placement* decision correctly (`BuildingPlacementResolver` itself never queues or
  retries anything, it either resolves a position or returns null, immediately, every call), but
  did not anticipate that `ConstructionSystem`'s **pre-existing** (Fase 8) FIFO queue would need to
  keep a completed-but-unplaceable project around rather than silently discarding its `Building`
  and consumed resources. Dropping it silently was tried first and rejected — it destroyed
  resources with no recovery path. A plain retry-in-place was tried second and rejected — it let
  one stuck project starve every other project queued behind it in the same city (measured: 20+
  ticks). This skip-ahead behavior is the resolution: never lose resources, never block unrelated
  projects.
- WHEN two overflow buildings from different mother cities are mutually within
  `AbsorptionRingCells` of each other and of two different cities' absorption ranges THEN the
  building SHALL absorb into its own city (`Building.City`), never a different one, even if
  geometrically closer to the other city's edge.
- WHEN an overflow cluster sits within absorption range of an existing city AND its own resident
  population would otherwise clear the founding concentration threshold THEN absorption SHALL
  take precedence over founding a new city (a cluster next to an existing city extends it, it does
  not split off).
- WHEN an overflow cluster has buildings but too few (or zero) materialized residents actually
  living in them THEN the system SHALL NOT found a new city — a building alone, or a single
  resident alone, never clears the concentration threshold, matching the bar a normal city already
  needs.

---

## Requirement Traceability

| Requirement ID | Story                  | Phase  | Status  |
| --------------- | ----------------------- | ------ | ------- |
| CITYGROW-01     | P1: Overflow placement + city growth | Verified | ✅ Verified |
| CITYGROW-02     | P1: Overflow placement + city growth | Verified | ✅ Verified |
| CITYGROW-03     | P1: Overflow placement + city growth | Verified | ✅ Verified |
| CITYGROW-04     | P1: Overflow placement + city growth | Verified | ✅ Verified |
| CITYGROW-05     | P1: Overflow placement + city growth | Verified | ✅ Verified |

**ID format:** `CITYGROW-NN`, one per acceptance criterion above (in order).

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 5 total, 5 mapped to tasks, 0 unmapped — see `validation.md` for the full round 1-4
Verifier history and `.specs/STATE.md`'s AD-007 for the one deliberate spec amendment made along
the way.

**Coverage:** 5 total, 0 mapped to tasks, 5 unmapped ⚠️ (Design/Tasks not yet run)

---

## Success Criteria

- [ ] A city seeded past its population-derived bounds capacity places 100% of its
      houses/workplaces without footprint overlap, on or off its original bounds.
- [ ] `CityBoundsResolver`'s resolved bounds for a city with overflow buildings within absorption
      range include those buildings' full footprints, verified by a new test alongside the
      existing `City_bounds_never_exceed_the_smaller_map_dimension...` test.
- [ ] A cluster of overflow buildings far from any city produces exactly one new `City` via the
      existing founding event, never zero and never a duplicate for the same cluster.
