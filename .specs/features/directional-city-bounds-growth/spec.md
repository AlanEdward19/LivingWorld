# Directional City Bounds Growth Specification

**Status**: Recorded (Specify only) — not yet Designed/Tasked/Executed, per user's explicit
request on 2026-08-23 ("isso pode virar uma spec... me diga o nome da spec gerada").

## Problem Statement

`CityBoundsResolver.Resolve` grows a city as a single symmetric square centered on `city.Location`
— its side either grows or it doesn't, uniformly in all four directions. If growth in ANY
direction would exceed the map edge or collide with a neighboring city (post-`FixT13`/`FixT17`'s
clamps), the resolver currently shrinks the WHOLE box's side to satisfy the tightest-blocked
direction, rather than letting the unblocked directions keep growing independently. A city pinned
against the map edge on one side, or with a neighbor on one side, stops growing altogether instead
of expanding into whichever directions remain open.

## Goals

- [ ] A city's bounds grow independently per edge (north/south/east/west), not as one shared side
      value — an edge that would cross the map boundary or another city's bounds simply stops
      extending further in that direction, while the other edges keep growing normally.
- [ ] A city boxed in on one or two sides (map edge, or an adjacent city) still grows normally on
      its remaining open sides as population/overflow buildings justify it.

## Out of Scope

| Feature | Reason |
| ------- | ------ |
| Fixing FixT18 (mother/daughter city merging) | Separate, already-recorded bug — this spec is about growth shape, not about whether two cities should become one. Related but independent. |
| Non-rectangular city footprints | Out of scope — cities stay axis-aligned rectangles, just no longer required to be square/symmetric. |

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --------------------- | --------------- | --------- | ---------- |
| Growth model | Per-edge (4 independent edges: min-X, max-X, min-Y, max-Y), each clamped independently against the map boundary and every other city's current bounds, rather than one shared side length | User's explicit description: "se ela não pode crescer para a esquerda, nada impede ela de crescer para direita ou outra direção" | y |
| Interaction with `FixT13`/`FixT17`'s existing clamps | This spec generalizes them — the existing "don't cross the map edge" / "don't overlap another city" checks become per-edge instead of whole-box | Same underlying constraints, just applied with finer granularity | n — needs Design confirmation this doesn't regress FixT8-FixT17's tests |

**Open questions:** none blocking Specify; the FixT13/FixT17 interaction is flagged for Design.

## User Stories

### P1: City grows into whichever directions remain open

**User Story**: As someone watching the simulation, I want a city pinned against the map edge or
a neighboring city on one side to keep growing on its other sides, so a city near a border or
another settlement doesn't just stop growing altogether.

**Acceptance Criteria**:

1. WHEN a city's growth in one direction would cross the map boundary THEN that specific edge
   SHALL stop extending further, while the opposite and perpendicular edges SHALL continue to grow
   normally as justified by population/overflow.
2. WHEN a city's growth in one direction would bring it within `AbsorptionRingCells` of another
   city's bounds (or cause an overlap) THEN that specific edge SHALL stop extending further in
   that direction only, while the other edges SHALL continue to grow normally.
3. WHEN a city is blocked on ALL four sides (fully boxed in) THEN it SHALL stop growing entirely
   — same honest-failure behavior already established elsewhere in `dynamic-city-growth`, not a
   forced/degenerate result.

**Independent Test**: Place a city near a map corner with a neighbor on one side; grow its
population/overflow; assert its bounds expand on the two open sides while staying flush against
the map edge and the neighbor's minimum gap on the two blocked sides.
