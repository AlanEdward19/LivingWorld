# Validation — city house layout

**Verdict: PASS**  
**Date:** 2026-08-24  
**Method:** evidence-or-zero, three original mutations, and one targeted round-2 mutation.

## Gate

Official command:
`bash scripts/test.sh --filter 'FullyQualifiedName~BuildingFootprintAndPlacementTests|FullyQualifiedName~PopulationSeederTests|FullyQualifiedName~CityBuildingMarkerContractTests|FullyQualifiedName~PurposefulCommuteTests|FullyQualifiedName~CityOccupancyTests'`

- .NET focused: **60/60 passed**, including `CityOccupancyTests` and both
  persisted-orientation cases.
- Web suite executed by the same script: **443/443 passed** across 67 files.
- Non-blocking pre-existing React `act(...)` warnings were emitted.

## Evidence by acceptance criterion

### HOUSE-01 — Compact residence: PASS

- Implementation: `BuildingFootprintGenerator.cs:20-34,39-67` keeps canonical shapes
  square at 3×3/4×4, fixes initial houses at 3×3, and derives their single interior floor.
- Assertion: `BuildingFootprintAndPlacementTests.cs:81-92` checks `(3,3,1)` for
  identities 1, 2 and 99; `web/tests/map-engine/buildingFootprint.test.ts:36-51`
  checks all cardinal rotations and preserved occupied cells/rotated door.

### HOUSE-02 — Deterministic variety: PASS

- Implementation: `BuildingFootprintGenerator.cs:14-23` maps stable identity/type hash
  to 0/90/180/270 while `Generate(id,type)` remains canonically neutral; `:73-77`
  applies an entity's explicit orientation (or its derived fallback).
- Consumers: `BuildingPlacementResolver.cs:34-35` derives orientation explicitly;
  `CityOccupancy.cs:103,211,242` uses `Generate(Building)` for effective occupancy.
- Assertion: `BuildingFootprintAndPlacementTests.cs:95-106` requires cardinal values,
  more than one distinct orientation, and identical repeated derivation.

### Post-gate orientation overload contract: PASS

- Implementation: `BuildingFootprintGenerator.cs:73-77` applies explicit entity
  orientation, and the three occupancy consumers call that overload.
- Assertion: `BuildingFootprintAndPlacementTests.cs:108-129` checks an asymmetric L-shape
  at 90°/270°, full geometry+material signature, difference from 0°, and one valid door.

### HOUSE-03 — Contained rendering: PASS

- Implementation: `SpatialBoundsResolver.cs:25` resolves bounds from the oriented
  footprint; `cityBuildingPlacement.ts:18-40` generates relative render cells using the
  marker orientation and sizes the entity from their maxima.
- Assertions: `PopulationSeederTests.cs:62-78` projects the city and proves every
  oriented building cell lies within grown bounds; `CityBuildingMarkerContractTests.cs:44-63`
  proves projected position/orientation match placement; `cityBuildingPlacement.test.ts:40-59`
  proves rendered cells and extents match the oriented generator without double rotation.

### HOUSE-04 — Interior home/rest arrival: PASS

- Implementation: `PopulationSeeder.cs:89-102` persists the resolved house and assigns
  its rotated `Floor` cell to `Household.Location`; `RestPlaceResolver.cs:13-23` resolves
  dwelling rest to that canonical location when no bed exists.
- Assertion: `PopulationSeederTests.cs:43-58` proves every seeded household location is
  a floor cell of exactly one corresponding rotated residence.

### HOUSE-05 — Commute preserved: PASS

- Behavioral chain: the corrected interior point is `Household.Location`; existing
  travel/rest logic consumes that canonical home rather than a footprint origin.
- Assertions: `PurposefulCommuteTests.cs:128-152` proves tick-consuming travel to the
  real workplace, `:154-174` proves work begins only after arrival, and `:176-231`
  proves tick-consuming return to the household location after the work window.

## Discrimination sensors (scratch only)

Scratch: `%LOCALAPPDATA%/Temp/livingworld-house-layout-revalidation-20260824`; one mutant at a
time, same official filter, then scratch deleted (`removed=True`). Real tree untouched.

1. Residence width `3 → 4`: **killed**, 4 failures; exact `(3,3,1)` assertion reported
   `(4,4,4)` after the post-gate square-shape correction.
2. Derived orientation `stable hash → 0`: **killed**, variety assertion failed at
   `BuildingFootprintAndPlacementTests.cs:103`.
3. Seeded home material `Floor → Door`: **killed**, all 12 households rejected by
   `PopulationSeederTests.cs:47-57`.
4. Round-2 targeted sensor, `Generate(Building)` forced to ignore persisted orientation
   and use 0°: **killed**, both 90°/270° cases failed at assertion line 124; dedicated
   scratch `livingworld-house-layout-orientation-sensor-r2` removed (`removed=True`).

## Gaps / confidence

- **P0/P1/P2 gaps: none found.**
- Confidence: **high**. Baseline is green, the original three risks remain discriminated,
  and the former P1 now has an exact assertion plus a killed targeted mutation.
