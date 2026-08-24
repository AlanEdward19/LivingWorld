# City house layout — acceptance anchor

## Scope

Validate the first residential-layout correction without changing simulation behavior.

## Acceptance criteria

- **HOUSE-01 — Compact residence.** An initial residence has a 3×3 occupied
  footprint with exactly one interior floor cell, in every cardinal orientation.
- **HOUSE-02 — Deterministic variety.** Derived building orientation is one of
  0/90/180/270 degrees, repeats for the same identity, and is not constant across
  a representative sequence of identities.
- **HOUSE-03 — Contained rendering.** The projected orientation reaches the web
  placement unchanged; every rendered footprint cell is inside both its entity
  extent and the resolved city bounds.
- **HOUSE-04 — Interior home.** Population seeding assigns each household its
  residence's interior floor cell, so dwelling rest/arrival resolves inside the
  residence rather than at the footprint origin or door.
- **HOUSE-05 — Commute preserved.** An employed NPC still travels from home to the
  real workplace over simulation ticks, only works after arrival, and returns to
  the household location when the routine leaves the work window.

## Exclusions

- No architecture redesign or backend/API contract expansion.
- No fixes are authorized during independent verification.
