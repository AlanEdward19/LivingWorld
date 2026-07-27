# LESSONS — auto-maintained by scripts/lessons.py

> Machine-owned. Do NOT hand-edit. Changes are overwritten on the next `lessons.py` write.
> Canonical state lives in `.specs/lessons.json`. Edit lessons only via the script.
> promote_threshold=2 distinct features · window_days=45 · quarantine_threshold=2

## Confirmed (load these at Specify/Design)

Corroborated across multiple features. Safe to apply as guidance.

_none_

## Candidates (under observation — do NOT load as guidance yet)

Seen once or not yet corroborated. Tracked, not trusted.

### L-001 — When an AC has a compound THEN clause (abort path + happy-path invariant), write one assertion per clause — an abort-only test leaves the happy-path guarantee unverified even if every other test incidentally relies on it.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `tests/LivingWorld.Tests/Behavior` · harmful: 0
- features: phase-04-needs
- evidence: NEEDS-09 (tests/LivingWorld.Tests/Behavior)
- last seen: 2026-07-27T12:18:24Z

### L-002 — When a spec assumption states one multiplier feeds two downstream systems (e.g. quantity and price), write a test asserting the effect on each downstream system separately — a passing test on one does not prove the other was wired.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `tests/LivingWorld.Tests/Economy` · harmful: 0
- features: phase-06-skills
- evidence: SKILL-11 (tests/LivingWorld.Tests/Economy/ProductionSystemSkillTests.cs, ProductionSystemTests.cs) — only quantity asserted, no price-effect test
- last seen: 2026-07-27T00:00:00Z

### L-003 — When a scenario test substitutes a production function call in isolation for an end-to-end system test (a logged SPEC_DEVIATION), add a companion test that exercises the real call site inside its actual system/event handler — proving the function works is not the same as proving the wiring calls it correctly.
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `tests/LivingWorld.Tests/Population` · harmful: 0
- features: phase-06-skills
- evidence: SKILL-09 T17 (tests/LivingWorld.Tests/Population/PairedScenarioTests.cs) — RateGene.Inherit tested in isolation, NatalitySystem.HandleEvent's own call site (src/LivingWorld.Simulation/Population/NatalitySystem.cs:62) never exercised
- last seen: 2026-07-27T00:00:00Z

## Quarantined (failed when applied — ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
