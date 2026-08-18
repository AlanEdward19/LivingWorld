# Phase 15.1 Live Polish Specification

## Problem

The playable frontend does not reliably resume the last created world and does not make
simulation progress legible. City scale, initial NPC placement, and arbitrary terrain colors
also make the map look incorrect even when the canonical simulation is running.

## Assumptions

- `Continue` means the single latest world slot; multiple save slots remain out of scope.
- A fresh install may still host an internal fallback world, but it must not overwrite a user save.
- Year is derived from the world's own calendar, never from a hard-coded Gregorian conversion.
- Terrain IDs have no biome semantics yet; the UI may use only a restrained natural palette.

## Acceptance Criteria

- LIVE-01: WHEN the app is restarted after creating a world THEN `Continue` SHALL load that
  persisted world rather than a newly generated fallback.
- LIVE-02: WHEN two procedural worlds use different seeds and otherwise equal inputs THEN their
  generated map cells SHALL differ; equal seeds SHALL remain byte-identical.
- LIVE-03: WHEN an initial population has more than one household THEN NPC spawn locations SHALL
  be deterministically distributed across valid cells near the village instead of all overlapping.
- LIVE-03b: WHEN an NPC completes an Idle action THEN it SHALL take one deterministic valid step,
  so a running simulation produces authoritative movement even without a workplace destination.
- LIVE-04: WHEN simulation status is requested THEN it SHALL include current tick and calendar year.
- LIVE-05: WHEN time advances, pauses, changes speed, or steps THEN the HUD SHALL refresh and show
  the authoritative tick and year.
- LIVE-06: WHEN a city is projected on the world map THEN its derived footprint SHALL be compact
  and never exceed half of the smaller map dimension for maps large enough to do so.
- LIVE-07: WHEN terrain is rendered THEN non-water terrain SHALL use a restrained vegetation/soil
  palette with no arbitrary purple tiles, while rivers remain blue.
- LIVE-08: WHEN settlements are authored on the map THEN every settlement SHALL become one city
  at the authored coordinate, and the initial population SHALL use the first settlement as home.
- LIVE-09: WHEN a resident remains inside its city's footprint THEN it SHALL not be projected as
  an external NPC on the world map or its realtime delta stream.
- LIVE-10: WHEN an NPC completes Idle, Work, or Socialize THEN it SHALL take one deterministic
  valid ambient step without leaving its city's footprint.
- LIVE-11: WHEN a blank world is created without an authored productive economy THEN residents
  SHALL not starve merely because the default economy lacks food and water stock.
- LIVE-12: WHEN an NPC is rendered at any zoom THEN its token radius SHALL remain at most 10
  screen pixels and SHALL not scale to the visual size of a city.

## Out of Scope

- Multiple save slots or save-management UI.
- Named biome semantics or a new art pipeline.

## Verification

- Focused .NET and web tests derived from LIVE-01..07.
- Final `bash scripts/verify.sh` exits 0.
