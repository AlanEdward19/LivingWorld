# Dynamic City Growth Validation — ROUND 3 (+ ROUND 4 follow-up appended)

> **Superseded verdict:** round 3's FAIL below was resolved by the AD-007 follow-up
> (`f2219bc`, `e48b15a`). See **[Round 4 (AD-007 follow-up)](#round-4-ad-007-follow-up)** at the end
> of this file for the current verdict (**PASS**). The round 1–3 history is kept verbatim.

**Date**: 2026-08-23
**Spec**: `.specs/features/dynamic-city-growth/spec.md`
**Diff range**: `72f6c3b..e9524d1` (20 commits: 11 feature + `596824f`/`2133401` (round-2 fixes) +
`dd6fac9` (docs) + `9a517bf`/`7fcfb61`/`3fe4c18`/`42e4305`/`142dd08`/`e9524d1` (round-3 fixes))
**Verifier**: independent sub-agent (author ≠ verifier), round 3. Everything below was re-derived
from scratch against the current tree — line numbers, mutation results and probe measurements are
this round's own measurements. All sensor mutations applied in place to files verified clean
beforehand and restored via `git checkout --`; final `git diff` on all four target files is empty
and no scratch probe file remains.

**Verdict**: ❌ **FAIL — 1 NEW Major finding (Fix D contradicts both spec.md and design.md and
introduces permanent FIFO head-of-line blocking) + 1 surviving mutant (Minor).**

Round-1's Blocker and Major, and round-2's surviving mutant, are all **genuinely closed** and
independently re-verified this round. Fix D is a real, correct fix for the bug it targets — but the
*shape* of the fix deviates from the documented design in an unmarked way and adds a new failure
mode. See Gap 1.

---

## Round 2 → Round 3 gap disposition

| Round-2 gap | Severity | Round-3 status |
| ----------- | -------- | -------------- |
| Gap D — reversed `.OrderBy` survived the sensor | Minor (surviving mutant) | ✅ **CLOSED** — mutation M3 now killed by **two** tests (Fix A's new consistency test *and* Fix B's tightened perf guard) |
| Gap A — perf guard used 200×200 bounds, never entered the overflow ring | Minor | ✅ **CLOSED** — my own probe measures **23 of 30** boxes landing outside the 12×12 bounds; the ring is genuinely exercised |
| Gap B — land-scarce workplace silently dropped, project dequeued, resources lost | Minor (real bug) | ✅ **FIXED** (the bug) / ⚠️ **new Major on the fix's shape** — see Gap 1 |
| Gap C — perf guard hung 300 s+ instead of failing | Minor | ✅ **CLOSED** — mutation M4 now fails in **exactly 10 s** via the enforced timeout |
| Gap E — `2133401` commit hygiene | Minor (process) | ⚠️ **Documented, not fixed** (`tasks.md:661-668`, docs-only commit `e9524d1`) — accepted per the user's standing acceptance of the same pattern |
| Gap 3 — own-city-only absorption untested | Minor | ✅ **CLOSED** — `CityOccupancyTests.cs:265-268` |
| Gap 4 — growth past `MaxSize`=12 untested | Minor | ✅ **CLOSED** — `BuildingFootprintAndPlacementTests.cs:191` |
| Gap 5 — "nearest" ring cell never asserted | Minor | ⚠️ **PARTIALLY closed** — new test bounds the radius only loosely; mutation M5 (skip radius 1) still **SURVIVES**. See Gap 2 |

---

## 1. Fix D deep-dive (the round-3 change that matters)

### Static reading — `ConstructionSystem.cs` in full

The retry mechanism is **mechanically sound**. Traced every step:

| Step | Code | Behavior on land scarcity |
| ---- | ---- | ------------------------- |
| `Tick` head-of-queue | `:24` `var project = city.ConstructionQueue[0];` | only the FIFO head advances |
| resource charge | `:33-40` `DueThisTick` → `WithdrawStock`/`RecordConsumption` | on the retry tick `tickIndex = TicksToBuild - 0 + 1 > TicksToBuild`, so `targetCumulative = total` and `amountDue = total - Consumed = 0` → **`due` is empty, nothing re-charged** |
| tick decrement | `:42` `project.Advance()` | `ConstructionProject.Advance` is clamped (`if (TicksRemaining > 0) TicksRemaining--`) → **stays 0, never goes negative**, so the completion branch re-fires |
| conditional dequeue | `:49-50` `if (CompleteProject(...)) city.DequeueCompletedConstruction();` | returns `false` → **project stays queued** |
| placement-before-commit | `:67-74` disposable `candidate` built from `world.NextBuildingId` (peek), `Resolve` checked *before* `AddBuilding` | **no `Building`, no `Workplace`, and the id counter never advances** on failure |

`world.NextBuildingId` (`WorldState.cs:256`) is a pure `[Canonical]` getter; the real building is
then created with `NextBuildingIdAndAdvance()` (`:76`) — the **same** id value, so the footprint
hash the placement was resolved against is exactly the footprint that gets built. Correct.

Note the `else` branch (`:85`, non-workplace recipes) still adds unconditionally and returns
`true`. That is **correct, not an oversight**: `Building.Position` is always re-derived, never
persisted, so a house on a full map self-heals once land frees. Only `Workplace.Position` is
stored, which is exactly the asymmetry Fix D guards. Good targeting.

### Empirical — my own probes (independent of the committed test)

Scratch xUnit probe (`ZzVerifier3ProbeTests`, since deleted; untracked, tree never modified).
The committed test uses **two separate worlds** for the two halves, so it never actually proves a
retry. I proved each half directly:

**Probe 1** — map sized to exactly one rectangular footprint (5×3), fully tiled by one authored
building, candidate id forced **above** the blocker's id so `CityOccupancy`'s `placingId` filter
cannot accidentally exclude the blocker. Ran the system **5 consecutive ticks**:

| tick | queue | `TicksRemaining` | `Consumed[Timber]` | `city.Stock` | `Workplaces` | `Buildings` | `NextBuildingId` |
| ---- | ----- | ---------------- | ------------------ | ------------ | ------------ | ----------- | ---------------- |
| 1–5 | **1** | **0** | **10** | **90** | **0** | **1** (blocker only) | unchanged |

→ project genuinely persists, resources charged **exactly once**, no orphan `Building`, no
`Workplace`, no id leakage, no drift across repeated retries.

**Probe 2** — the *exact* state a failed retry leaves behind (recipe fully paid,
`TicksRemaining == 0`, still at the queue head), enqueued directly into a city **with** free land,
`ticksToBuild: 3` so the retry `tickIndex` exceeds `TicksToBuild`:

→ one tick: queue **empty** (really dequeued), **1** `Workplace` created (`MaxVacancies` 1) at
`(98,98)`, **1** `Building` owned by the city, and `city.Stock` still **100** — **zero extra
charge on the successful retry**. This is the "a LATER tick DOES complete it" half, proven.

**Verdict on Fix D as a fix**: it is a **real, correct** fix, not a test that happens to pass.
Every claim in its commit message about the mechanism holds under independent construction.

### But: the fix contradicts both spec.md and design.md, unmarked — see Gap 1.

---

## 2. Blocker + Major spot-check (round-2 closures, still true after round 3)

Round 3 changed exactly **one** production file (`ConstructionSystem.cs`), so neither closure could
regress structurally. Confirmed anyway:

- **Blocker (2^N recursion)**: my Probe 3 resolved 30 unauthored buildings against 12×12 bounds
  (the tight, ring-exercising shape) in **246 ms**, 618 cells all distinct. No cliff. Mutation M4
  re-confirms the recursive edge is the only thing that reintroduces it.
- **Major (nullable `Resolve` + map-bounded ring)**: signature still nullable
  (`src/LivingWorld.Simulation/Cities/BuildingPlacementResolver.cs:28`). Re-grepped **every**
  production call site: `CityOccupancy.cs:100-101` (`?.Position` + `continue`),
  `ConstructionSystem.cs:74` (`return false` — now the *better* handling), `CityProjector.cs:64-66`
  and `LivingScopeState.cs:105-107` (both `is { } resolved ? … : null`). **No site coerces `null`
  into a default.** Ring still capped by `maxRadius = mapWidth + mapHeight`
  (`OverflowPlacer.cs:50`) with the whole-footprint `WithinMap` predicate (`:60`), and mutation M5b
  proves the loop is live.

---

## 3. Spec-Anchored Acceptance Criteria (re-derived, line numbers re-measured this round)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --------- | -------------------- | ----------------------- | ------ |
| **CITYGROW-01** — free cell in bounds → place there | Footprint wholly inside resolved bounds, never overlapping | `BuildingFootprintAndPlacementTests.cs:299` — `Assert.True(CityOccupancy.Translate(shape, resolved.Value.Position).All(bounds.Contains))`; non-collision `:282` — `Assert.NotEqual(resolvedA!.Value.Position, resolvedB!.Value.Position)`; **NEW** causal-order guard `CityOccupancyTests.cs:328` — `Assert.Equal(truthPosition[id], box.Origin)` | ✅ PASS |
| **CITYGROW-02a** — no free cell in bounds → **nearest** free cell by outward ring-search | Position outside bounds, footprint free, **minimal** radius | `BuildingFootprintAndPlacementTests.cs:319` — `Assert.False(translated.All(bounds.Contains))` + `:320` `Assert.True(CityOccupancy.IsFree(...))`; determinism `OverflowPlacerTests.cs:85`; **NEW** nearest-ring `:171` — `Assert.Equal(2, gap)` | ⚠️ PASS with spec-precision gap — the "nearest" clause is now *loosely* bounded (breaks at radius ≥ 5) but a radius-1 skip still survives (Gap 2) |
| **CITYGROW-02b** — no free cell anywhere → land scarcity feeding `MigrationSystem`, **rather than placing or queuing** | Must not place; household need routes to `MigrationSystem`; **explicitly not queued** | Decline-to-place: `BuildingFootprintAndPlacementTests.cs:340` — `Assert.Null(resolved)`; off-map guard `:362`. `MigrationSystem` half: `MigrationSystemTests.cs:286` — `Assert.Equal(destination.Id, household.PendingRelocationCity)`; non-scarce control `:310`. **Queuing half now asserted in the OPPOSITE direction**: `ConstructionSystemTests.cs:225` — `Assert.Single(scarceCity.ConstructionQueue)` | ❌ **GAP** — the code now does precisely what the AC forbids ("**or queuing**"), and a committed test pins that behavior in. See Gap 1 |
| **CITYGROW-03** — overflow within `AbsorptionRingCells` → next resolution includes the **full footprint**, up to hard map cap | Grown box contains every cell; never exceeds `Math.Min(mapW,mapH)/2` | `BuildingFootprintAndPlacementTests.cs:134-135` (both corners `Contains`); map cap `:165` — `Assert.True(grown.Width <= 10)`; **NEW** `MaxSize` overshoot `:191` — `Assert.True(grown.Width > 12)`; end-to-end `CityGrownBoundsTests.cs:97-100` | ✅ PASS — Gap 4 closed |
| **CITYGROW-04** — mutually-close cluster, outside every absorption range, clearing `FoundingConcentrationThreshold` via the **SAME** formula → schedule founding on `SettlementFoundingSystem`'s cadence, no double-founding | Identical formula; `now + OrganizationTicks`; one event/cluster; low residents → none | Formula identical: `SpatialSettlementFoundingSystem.cs:97` vs `SettlementFoundingSystem.cs:62,72`. `SpatialSettlementFoundingSystemTests.cs:77` — `Assert.Equal(world.CurrentDate.TotalHours + 10, pending.TargetTick)`; zero residents `:90` `Assert.Empty`; no double-schedule `:125` `Assert.Single`; absorption precedence `:110` | ✅ PASS (unchanged; `Frequency => Monthly` still only proven indirectly via `TargetTick`) |
| **CITYGROW-05** — bounds growth never moves existing residents/workplaces | Existing `Position`/`Orientation` unchanged | `BuildingFootprintAndPlacementTests.cs:206-207` — `Assert.Equal(new CellCoord(52,52), authored.Position)` + `Assert.Equal(0, authored.Orientation)`; **NEW** own-city-only `CityOccupancyTests.cs:268` — `Assert.Equal(baseB, grownB)` | ✅ PASS |

**Status**: ❌ **4/5 ACs matched the spec outcome; CITYGROW-02b now diverges from its own AC text.**
1 spec-precision gap remains (nearest-ness).

---

## 4. Edge Cases (re-derived)

- [x] **Map fully built → route to `MigrationSystem` instead of queuing indefinitely** —
      ⚠️ **half regressed.** Placement side is right (`Resolve` → `null`, ring map-bounded, all four
      production call sites decline). Migration side is right (`MigrationSystem.cs:45,59`,
      3 tests). But the *construction* side now **queues indefinitely** — the exact wording this
      edge case rules out ("instead of queuing the building indefinitely for space that will never
      appear"). Measured: my Probe 4 ran 20 ticks on a full map and the head project never left the
      queue. See Gap 1.
- [x] **Absorbs into its own city, never a closer other city** — ✅ **now committed**:
      `CityOccupancyTests.cs:265-266` (`grownA.Contains` / `!grownB.Contains` for every footprint
      cell) + `:268` (`Assert.Equal(baseB, grownB)` — B's bounds provably untouched). Gap 3 closed.
- [x] **Absorption takes precedence over founding** — `OverflowClusterFinderTests.cs:86,109`;
      `SpatialSettlementFoundingSystemTests.cs:110` with a deliberately trivial 0.01 threshold.
- [x] **Buildings with too few / zero residents never found a city** —
      `SpatialSettlementFoundingSystemTests.cs:90`; `OverflowClusterFinderTests.cs:151`.

---

## 5. Gate Check

- **Gate command**: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"`
  (Quick gate from tasks.md; run in the working tree — still the only state that compiles, see
  Known Limitations)
- **Result**: **216 passed, 0 failed, 0 skipped** (`LivingWorld.Tests.dll`, net10.0); web suite
  **403 passed / 67 files**. Script exit code **0**. No `error CS` / `warn CS`.
- **Test count delta**: round 1 → 208, round 2 → 211, round 3 → **216** (+5: 1 causal-order,
  1 land-scarcity retry, 3 edge-case tests). Fix B/C modified an existing test in place, hence no
  count change from them.
- **Test integrity**: no test deleted in any of the 6 commits. Assertions were **strengthened** in
  every case: the perf guard gained a real overflow-exercise assertion (`CityOccupancyTests.cs:225`)
  *and* an enforced timeout (`:193` `[Fact(Timeout = 10_000)]`), and its 200×200 bounds were
  tightened to 12×12. No assertion weakened anywhere.
- **Skipped tests**: none in the filtered gate.

---

## 6. Discrimination Sensor (5 NEW mutations + 1 re-confirmation; none reused from round 2)

Sensor depth: **expanded (6 mutation runs)** — Fix D is a behavior change on a data-integrity path
(resources spent), so it got two mutations of its own. Baseline confirmed clean via `git diff`
before each mutation; each restored with `git checkout --` and re-verified immediately after.

| # | File:line | Mutation | Targets | Result |
| - | --------- | -------- | ------- | ------ |
| **M1** | `ConstructionSystem.cs:49-50` | **Flipped the "don't dequeue on null" logic back to always-dequeue** — `if (CompleteProject(...)) city.Dequeue...` → `CompleteProject(...); city.Dequeue...` (the exact pre-`42e4305` shape) | Fix D's core claim | ✅ **Killed** — `Completing_project_leaves_a_land_scarce_workplace_queued_and_completes_once_land_is_available` |
| **M2** | `ConstructionSystem.cs:74-77` | **Reintroduced the orphan `Building`** — moved `AddBuilding` back *above* the `Resolve` null-check, so a failed placement leaves a `Building` behind and burns an id | Fix D's other half (no orphan / no id leak) | ✅ **Killed** — same test |
| **M3** | `CityOccupancy.cs:180` | **Inverted the mandatory ascending order** — `.OrderBy(b => b.Id.Value)` → `.OrderByDescending(...)`. *This is round 2's surviving mutant, re-run verbatim.* | Fix A | ✅ **Killed (was ❌ SURVIVED)** — **2** failures: the new `..._places_each_building_using_the_causal_ascending_id_order` **and** the tightened perf guard (which now detects the resulting overlap). Round-2 Gap D is genuinely closed, with redundancy. |
| **M4** | `CityOccupancy.cs:188-189` | **Reintroduced the 2^N recursion** — `ScanForFreeOrigin(...) ?? ResolveOverflowPositionGiven(...)` → `BuildingPlacementResolver.Resolve(building, city, world, bounds)?.Position` | Fix C (fail-fast) + blocker fix | ✅ **Killed in 10 s** (`Duration: 10 s`, wall clock 16 s including build) — **was a 300 s+ hang in round 2.** Fix C independently confirmed working. |
| **M5** | `OverflowPlacer.cs:52` | **Broke nearest-ness by one ring** — `for (int radius = 1; ...)` → `radius = 2` (a building lands 2 cells out when 1 cell was free) | Fix F item 3 / CITYGROW-02a "nearest" | ❌ **SURVIVED** — 216 passed, 0 failed. See Gap 2 |
| **M5b** | `OverflowPlacer.cs:52` | Same, but starting at `radius = 5` | how loose the constraint really is | ✅ Killed — 2 failures (`ResolveOverflowPositionGiven_picks_the_nearest_free_ring_...` and `Resolve_falls_back_to_the_overflow_ring_...`). So nearest-ness is pinned only to within ±3 cells. |

**Result**: **5 of 6 killed, 1 survived** — ❌ FAIL on the sensor rule. Round 2's surviving mutant
is dead; a new, narrower one took its place at the same conceptual site (Gap 5 / nearest-ness).

**Final cleanliness check**: `git diff` on `ConstructionSystem.cs`, `CityOccupancy.cs`,
`OverflowPlacer.cs`, `BuildingPlacementResolver.cs` — **empty**. `git status` shows no
`ZzVerifier*` leftovers; the scratch probe file was deleted. The only working-tree changes are the
pre-existing unrelated Stage-4 ones, byte-identical to session start (verified by comparing the
session-start `git status --porcelain` against the final one).

---

## 7. Ranked Gaps

### Gap 1 — MAJOR (NEW): Fix D contradicts spec.md AC2b *and* design.md, unmarked, and permanently blocks the city's construction queue

- **What the documents say**:
  - `spec.md:80-83` (CITYGROW-02, AC2): "WHEN no free cell exists anywhere on the map THEN the
    system SHALL treat it as land scarcity feeding `MigrationSystem` … **rather than placing or
    queuing** the building."
  - `spec.md:110-114` (Edge Cases): "…instead of **queuing the building indefinitely** for space
    that will never appear."
  - `design.md:181` (Error Handling Strategy): "**No queue.** … the building placement simply stays
    unresolved for that tick, retried automatically next time a building needs placing (**no
    persisted queue, no special-cased retry logic** — same 'try again next call' nature as
    `BuildingPlacementResolver.Resolve` already has today)."
- **What the code does**: `ConstructionSystem.cs:45-51` keeps the project in
  `city.ConstructionQueue` indefinitely, via a special-cased conditional dequeue. Its own comment
  (`:45-48`) and the commit message cite "design.md, Error Handling Strategy" as the authority for
  this — but that row says the **opposite**. The justification is misattributed.
- **New failure mode (measured, not theoretical)**: `Tick` only ever advances
  `city.ConstructionQueue[0]` (`:24`), so a stuck head blocks the whole city forever. My Probe 4
  (full map, workplace project enqueued ahead of a house project, 20 ticks):

  | after 20 ticks | queue | house `TicksRemaining` | house `Consumed` | buildings |
  | -------------- | ----- | ---------------------- | ---------------- | --------- |
  | measured | **2** | **1** (never advanced a single tick) | **0** | 1 (blocker only) |

  A house recipe — whose placement branch (`:85`) **can never fail** — is starved permanently
  behind the scarce workplace project. That is strictly worse than the pre-fix behavior on this
  axis (which at least let the queue drain), and it is a *new* consequence introduced this round.
- **Why it is still arguably the right engineering call**: the pre-fix behavior destroyed resources
  the city had already paid (round-2 Gap B, a real bug). Dropping the project silently is worse
  than retrying it. The problem is not the intent — it is that the deviation is (a) undocumented,
  (b) misattributed to a document that says the opposite, and (c) shipped without addressing the
  head-of-line consequence. This codebase already has an established convention for exactly this
  situation — `// SPEC_DEVIATION` markers (see `ConstructionProject.cs:24`, `OverflowPlacer.cs:5`,
  `City.cs:29`) — and Fix D uses none.
- **Fix options** (the likely-correct resolution is to amend the docs, not revert the code):
  1. Amend `spec.md` AC2b + Edge Case and `design.md`'s Error Handling row to say the project stays
    queued and is retried, add a `// SPEC_DEVIATION` marker at `ConstructionSystem.cs:45`, **and**
    handle head-of-line blocking (skip a blocked head and try the next project, or move the blocked
    project to the tail).
  2. Or make the scarce project dequeue *and refund* `project.Consumed` back into `city.Stock`,
    matching the spec's "rather than … queuing" literally, with no resource loss.
- **Priority**: **Major** — a committed test (`ConstructionSystemTests.cs:225`) now pins in behavior
  that directly contradicts the feature's own acceptance criterion, and it comes with a measured
  permanent-starvation failure mode.

### Gap 2 — MINOR (surviving mutant): "nearest" free cell is only loosely asserted

- **Root cause**: `OverflowPlacerTests.cs:145-171` (Fix F item 3) blocks the **whole** radius-1
  ring and asserts the result's Chebyshev gap is exactly 2. That rules out *skipping past* radius 2,
  but it cannot detect the ring loop simply *starting* at radius 2 — mutation **M5** leaves all 216
  tests green. M5b shows the real constraint is only "starts at radius ≤ 4".
- **Why it matters**: `spec.md:78-81` (AC2) says "the **nearest** free cell found by outward
  ring-search". `AbsorptionRingCells` defaults to 3, so an off-by-one or off-by-two in the ring
  start silently changes whether an overflow building is absorbed by its city or instead becomes a
  new-city cluster seed — i.e. it flips CITYGROW-03 vs CITYGROW-04 behavior with no test failing.
- **Fix**: one test with radius 1 **partially** free — occupy all of the radius-1 ring *except* one
  cell whose footprint fits, assert the resolved gap is exactly **1**. That kills M5.
- **Priority**: **Minor** (correctness precision; committed code is correct, only the guard is
  loose). Note this is round-1/round-2's Gap 5, downgraded from "unasserted" to "loosely asserted"
  but **not fully closed**, despite `tasks.md:645-651` claiming it as done.

### Gap 3 — MINOR (unchanged, accepted): residual O(N²) in `OwnedBuildingFootprintBoxesWithOwners`

`CityOccupancy.cs:100` still calls `Resolve` once per building, each rebuilding the occupied set.
Re-measured this round on the tight 12×12 shape: **246 ms for N=30**. Round 2 measured N=100 at
3.5 s. Polynomial, not the round-1 cliff. The guard now genuinely exercises the ring (23/30 boxes
overflow), so it would at least *see* a future regression. No correctness impact; not blocking.

### Gap 4 — MINOR (process, carried over): `2133401` commit hygiene

Documented in `tasks.md:661-668` by `e9524d1` and accepted per the user's standing acceptance of the
same pattern from T5/`25bb02c`. No code change; no behavioral risk. Not re-litigated.

### Gap 5 — MINOR (unchanged): `SpatialSettlementFoundingSystem.Frequency => Monthly` not directly asserted

Cadence is proven only indirectly via `TargetTick` (`SpatialSettlementFoundingSystemTests.cs:77`).
One-line assertion would close it.

---

## 8. Code Quality (the 6 round-3 commits)

| Principle | Status |
| --------- | ------ |
| Minimum code / no features beyond what was asked | ✅ — 5 of 6 commits are **test-only** or **docs-only**. The single `src/` change (`42e4305`) is 24+/11− in one file, adds no type, no config knob, no abstraction. Textbook surgical. |
| No abstractions for single-use code | ✅ — `CompleteProject` changed `void` → `bool` rather than gaining a result type or an exception path. Simplest thing that works. |
| No unnecessary flexibility | ✅ — the disposable-candidate trick reuses the existing `world.NextBuildingId` peek getter instead of introducing an id-reservation mechanism. |
| Only touched files required for task | ✅ — `9a517bf`, `7fcfb61`, `3fe4c18`, `142dd08` touch **only** test files; `42e4305` touches exactly its one `src/` file + its test; `e9524d1` is docs-only. **A clear improvement over round 2's Gap E** — none of the six repeats that mistake. |
| Didn't "improve" unrelated code | ✅ |
| Matches existing patterns/style | ✅ — Portuguese doc comments with `dynamic-city-growth, round-3 fix X` provenance markers and requirement IDs follow the project convention exactly; helper duplication (`MakeWorldWithMap`, `FindRectangularFootprint`) is copied per-test-class with an explicit "mesmo helper de …" note, matching how the existing test classes already do it. |
| Would a senior engineer approve? | ⚠️ — **Yes on the mechanics, no as-submitted.** The fix is correct, minimal, and well-reasoned, and Fix C in particular shows real diligence (it discovered that xUnit's `Timeout` is silently ignored on sync tests and fixed it properly rather than papering over it). But a reviewer would block on Gap 1: the comment cites design.md as authorizing behavior design.md explicitly forbids, there is no `SPEC_DEVIATION` marker where the codebase's own convention calls for one, and the head-of-line consequence went unexamined. |
| Tests map to ACs and are non-shallow | ⚠️ — 4 of the 5 new tests are genuinely discriminating (verified by M1/M2/M3). The 5th (nearest-ring) is **not** — M5 survives it (Gap 2), so `tasks.md`'s "Done when" for FixT7 item 3 overstates what was achieved. |
| Spec-anchored outcome check | ❌ — 4/5; CITYGROW-02b's committed assertion now targets the **opposite** of its AC text (Gap 1). |
| Per-layer Coverage Expectation met | ✅ — all 4 spec Edge Cases now have committed assertions (round 2 had 3 of 4); domain/simulation units ~1:1 with ACs. |
| Every test maps to a spec requirement — no unclaimed tests | ✅ — all 5 new tests carry `dynamic-city-growth, round-3 fix X` + AC/Edge-Case references in their doc comments. |
| Documented guidelines followed | ✅ — `AGENTS.md` gate scripts used; the user's standing gate-cadence preference respected (`Category=Scenario` / `verify.sh` deferred to feature close). |

---

## 9. Known, Accepted Limitations (unchanged)

1. **HEAD does not compile on a clean checkout** — `25bb02c` (T5) swept in a pre-existing hunk
   depending on still-uncommitted `Household.BeginRelocation` / `RelocationArrivalSystem.cs`;
   `2133401` repeats the pattern. Explicitly accepted by the user; the gate is therefore run in the
   working tree, the only state that compiles. Re-confirmed this round (exit 0, no `CS` diagnostics).
2. **Part of the T3/T4b wiring remains uncommitted** in `NpcInspectionQuery.cs`,
   `BehaviorDecisionSystem.cs`, and the `LivingWorldCapabilityCatalog.cs` FOUNDING extension,
   because those files carry unrelated pre-existing Stage-4 work.
3. **The 9 full-suite failures from T8** trace to unrelated uncommitted Stage-4 crops work; this
   feature is inert in those scenarios (`CityRules.Disabled` → zero cities). Out of scope.

---

## 10. Requirement Traceability Update

| Requirement | Round-2 Status | Round-3 Status |
| ----------- | -------------- | -------------- |
| CITYGROW-01 | ✅ Verified | ✅ **Verified** — causal-ordering invariant now guarded (M3 killed twice) |
| CITYGROW-02 | ✅ Verified | ❌ **Needs Fix** — 02b's construction path now queues indefinitely, contradicting the AC (Gap 1); 02a's "nearest" clause still loosely guarded (Gap 2) |
| CITYGROW-03 | ✅ Verified | ✅ **Verified** — `MaxSize` overshoot now asserted |
| CITYGROW-04 | ✅ Verified | ✅ Verified |
| CITYGROW-05 | ✅ Verified | ✅ **Verified** — own-city-only absorption now asserted |

---

## Summary

**Overall**: ❌ **Not Ready — 1 Major remains.** This is the 3rd and final round of the bound;
escalate to the user.

**Spec-anchored check**: **4/5** ACs matched the spec outcome (round 2: 5/5 — CITYGROW-02b
regressed against its own AC text); 1 spec-precision gap
**Sensor**: 6 mutation runs — **5 killed, 1 survived**
**Gate**: 216 passed, 0 failed, 0 skipped (+5 vs. round 2); web 403 passed; exit 0

**What round 3 genuinely bought** (verified independently, not taken on the commits' word):
- **Round 2's surviving mutant is dead.** The mandatory ascending-`BuildingId` ordering is now
  guarded by two independent tests — Fix A's consistency test *and*, as a bonus, Fix B's tightened
  perf guard.
- **The perf guard is real now.** 23 of its 30 buildings genuinely overflow the 12×12 bounds
  (measured); previously **zero** did, on 200×200 bounds.
- **It fails fast.** A reintroduced exponential regression now goes red in **10 s** instead of
  hanging **300 s+**. Fix C also uncovered and worked around a genuine xUnit trap (`Timeout` is
  silently ignored on synchronous tests) rather than hiding it.
- **Fix D is a real fix for a real bug.** Independently reproduced: the project stays queued across
  5 ticks, resources are charged **exactly once** (`Consumed`=10, stock=90, stable), no orphan
  `Building`, no `Workplace`, no id leakage — and the exact post-retry state **does** complete on a
  later tick with **zero** extra charge. Every mechanical claim in its commit message holds.
- **All 4 spec Edge Cases now have committed assertions** (round 2: 3 of 4).
- **Commit hygiene improved markedly** — 5 of 6 commits are test-only or docs-only; none repeats
  round-2's Gap E.

**Issues found** (ranked):
1. **Gap 1 — MAJOR (new)**: Fix D makes a land-scarce project sit in `city.ConstructionQueue`
   forever. `spec.md:80-83` says "rather than placing **or queuing**", `spec.md:110-114` says "instead
   of queuing the building indefinitely", and `design.md:181` says "**No queue** … no persisted
   queue, no special-cased retry logic" — yet the code's own comment cites design.md as its
   authority, and no `// SPEC_DEVIATION` marker was added where this codebase's convention calls
   for one. Measured consequence: because `Tick` only advances `ConstructionQueue[0]`, a house
   project whose placement can never fail was starved for 20 straight ticks behind the stuck head.
   Resolution is probably to amend spec.md + design.md and handle head-of-line blocking — **not**
   to revert, since the pre-fix behavior destroyed already-paid resources.
2. **Gap 2 — MINOR (surviving mutant)**: the new "nearest free ring" test cannot detect the ring
   loop starting at radius 2 instead of 1 (M5 survives; only radius ≥ 5 is caught). One test with a
   *partially* free radius-1 ring asserting gap == 1 closes it. `tasks.md:645-651` claims this gap
   closed; it is not.
3. **Gap 3 — MINOR**: residual O(N²) (246 ms at N=30 tight bounds, 3.5 s at N=100). Not blocking.
4. **Gap 4 — MINOR (process, accepted)**: `2133401`'s bundled Stage-4 work, documented not fixed.
5. **Gap 5 — MINOR**: `Frequency => Monthly` still only proven indirectly.

**Next steps**: Gap 1 needs a human decision, because the right fix is most likely a **spec/design
amendment** (plus a `SPEC_DEVIATION` marker and head-of-line handling) rather than a code revert —
that is a product call, not a mechanical one, and it is exactly the kind of thing the 3-round bound
exists to escalate. Gap 2 is one cheap test. Gaps 3–5 can ride along or be deferred.

---
---

# Round 4 (AD-007 follow-up)

**Date**: 2026-08-23
**Diff range**: `e9524d1..e48b15a` (2 commits: `f2219bc` queue skip-ahead fix + 3 tests, `e48b15a`
nearest-cell precision test)
**Verifier**: independent sub-agent (author != verifier), round 4. Focused re-check of the two
round-3 open items only — not a full feature re-audit. Everything below is this round's own
measurement, re-derived from the current tree; the 3 committed AD-007 tests were **not** relied on
for the behavioral claims (6 independent scratch tests were written, run, and deleted).

**Verdict**: ✅ **PASS — both round-3 open items closed. No blocking findings. Nothing left open
that requires a code change.**

---

## R4.1 Authority chain (read before judging the code)

| Document | Says | Consistent with code? |
| -------- | ---- | --------------------- |
| `.specs/STATE.md` `AD-007` | queue is "FIFO among non-stuck projects, stuck held at position, retried for free"; **exactly one project's resources per city per tick**; user chose skip-ahead over drop-silently and retry-in-place | ✅ |
| `spec.md` Edge Cases (amendment 2026-08-23) | stuck project stays queued, resources never lost, no orphan `Building`, **and SHALL NOT block any other project** in the same city | ✅ |
| `design.md` Error Handling Strategy (`ConstructionSystem` row) | same, plus "resources charged exactly once, never twice" | ✅ |

Round-3 Gap 1 was "code contradicts spec.md/design.md, unmarked". The resolution taken is the
correct one for this codebase's conventions: **the documents were amended** (with the rejected
alternatives and the reason recorded in `AD-007`), so there is no longer a deviation to mark — a
`// SPEC_DEVIATION` comment here would now be wrong. The amendment text is honest about the history
(it names both rejected options and why), so a future reader is not misled.

---

## R4.2 Independent re-derivation of the 4 behavioral claims

Six scratch tests were written from scratch in
`tests/LivingWorld.Tests/Cities/ZzVerifierScratchAd007Tests.cs` — deliberately different scenarios,
tick counts and assertion targets from the 3 committed tests, notably asserting **`city.Stock`**
(which no committed test does) and asserting queue **identity** via `Assert.Same`. Run result:
**226 passed, 0 failed** (220 committed + 6 scratch). File deleted afterwards; `git status` clean.

| Claim | Scratch probe | Result |
| ----- | ------------- | ------ |
| **(a)** a stuck project no longer blocks others behind it | `V1`: scarce map, queue = [stuck workplace, house(4t), house(4t)], **30 ticks** | ✅ Both houses built; stuck project still queued at `TicksRemaining == 0`. Round-3's measured 20+ tick starvation is gone. |
| **(b)** one-resource-consuming-project-per-tick throttle still genuinely enforced | `V2`: queue = [stuck, house(10t), house(10t)]; asserted **per tick** that exactly one house advanced by exactly 1 tick, the other untouched, **and `city.Stock` fell by exactly 1/tick** (10 timber / 10 ticks) | ✅ Never two projects' resources in one tick. Also confirmed the stuck project's own *paying* tick consumes the whole budget (houses do not advance that tick). |
| **(c)** stuck project charged exactly once across retries | `V3`: 5 paying ticks then **25 free retry ticks**; asserted `Consumed == 10` **and** `city.Stock == 990` unchanged, `Workplaces` empty, `Buildings.Count == 1` (only the pre-seeded blocker) | ✅ No double charge, no orphan `Building`, no id leak. Stronger than the committed test, which checks `Consumed` but not `city.Stock`. |
| **(d)** normal (non-scarce) FIFO unchanged for the common case | `V5`: 200x200 free map, 2 house projects of 3 ticks; asserted the second is untouched on **every** tick until the first completes, then completes itself; total charge exactly 2 recipes (`Stock == 980`) | ✅ Strict FIFO preserved. The pre-existing `Queue_processes_only_the_head_project_leaving_the_second_untouched` (`ConstructionSystemTests.cs:153`) still passes unmodified and still guards this. |

**Mechanism read (`ConstructionSystem.cs:27-64`)**: the loop is `foreach (project in queue.ToList())`
-> skip-and-`continue` for every `TicksRemaining == 0` (stuck) project at zero cost -> unconditional
`break` after the **first** not-yet-paid project, whether or not it could actually pay. The `break`
is what preserves the throttle; the `continue` is what unblocks the queue. Both are load-bearing and
both are killed by tests (R4.4).

---

## R4.3 `City.RemoveConstructionProject` (queue-removal correctness)

`src/LivingWorld.Domain/Cities/City.cs:118` —
`public void RemoveConstructionProject(ConstructionProject project) => _constructionQueue.Remove(project);`

- **Removes the specific instance, not index 0**: `ConstructionProject` is a `public sealed class`
  (`ConstructionProject.cs:7`) with no `Equals`/`==` override, so `List<T>.Remove` falls through
  `EqualityComparer<T>.Default` to **reference** equality. The doc-comment's claim is accurate. (Had
  it been a `record`, value equality could have matched a different instance — worth knowing for
  whoever touches this type next.)
- **Order of survivors preserved**: `List.Remove` is a stable shift-left. Proven empirically by
  scratch `V6` (`Assert.Same` on both survivors' positions after a **middle** removal) and by scratch
  `V4`: with two stuck workplaces at indices 0 and 1, a house at index 2 completes and removes
  **itself** — both stuck projects survive and the house is gone from the queue. Under the old
  `DequeueCompletedConstruction()` (always index 0) this exact case would have deleted a stuck
  project and left the finished house queued forever; mutation M3 below confirms it.
- **No stale callers**: `grep` over `src/` and `tests/` finds zero remaining references to
  `DequeueCompletedConstruction`; the only two call sites of the new method are
  `ConstructionSystem.cs:37` and `:58`.

---

## R4.4 Discrimination Sensor — 4 NEW mutations (none reused from rounds 1-3 except M4r)

All mutations were applied to files verified clean by `git diff` beforehand, restored from a
byte-exact backup copy immediately after, and each file re-verified with an empty `git diff`.
Mutation runs used `FullyQualifiedName!~ZzVerifierScratch`, so **only the committed tests** could
score a kill — a mutant killed only by my own scratch tests would have counted as SURVIVED.

| # | File:line | Mutation | Targets | Result |
| - | --------- | -------- | ------- | ------ |
| **M1** | `ConstructionSystem.cs:38` | Stuck-branch `continue;` -> `break;` — i.e. **revert to the pre-fix behavior where a stuck project blocks everything behind it** (round-3 Gap 1's bug, reintroduced exactly) | the skip-ahead itself | ✅ **Killed** — 2 failures: `Stuck_workplace_project_does_not_block_a_house_project_queued_behind_it`, `Throttle_keeps_advancing_the_paying_project_at_its_normal_per_tick_rate_...` |
| **M2** | `ConstructionSystem.cs:63` | Throttle `break;` -> `continue;` — **every project in the queue advances and pays in the same tick** | the one-project-per-tick resource throttle | ✅ **Killed** — 2 failures: `Throttle_keeps_advancing_...` **and** the pre-existing Fase-8 `Queue_processes_only_the_head_project_leaving_the_second_untouched`. The old FIFO guard is still doing real work. |
| **M3** | `City.cs:118` | `_constructionQueue.Remove(project)` -> `_constructionQueue.RemoveAt(0)` — the old always-index-0 dequeue | the `City.cs` half of the fix | ✅ **Killed** — `Stuck_workplace_project_does_not_block_a_house_project_queued_behind_it` |
| **M4r** | `OverflowPlacer.cs:52` | `for (int radius = 1; ...)` -> `radius = 2`. **This is round 3's surviving mutant M5, re-run verbatim.** | `e48b15a` / CITYGROW-02 nearest-ness | ✅ **Killed (was ❌ SURVIVED in round 3)** — exactly 1 failure, and it is the new test: `ResolveOverflowPositionGiven_prefers_the_one_free_radius_1_cell_over_an_entirely_free_radius_2_ring` |

**Result**: **4/4 killed** — ✅ PASS. Round-3's only surviving mutant is dead, killed specifically by
the test added for it.

**Nearest-cell precision (round-3 Gap 2), re-derived by hand**: for `CityBounds((0,0),4,4)`,
`RingCells(radius:1)` is the perimeter `x in [-1,4]` at `y in {-1,4}` plus `y in [0,3]` at
`x in {-1,4}`. The new test occupies that entire perimeter except `(4,0)` and leaves the whole
radius-2 ring free. `(4,0)` is therefore the **only** valid radius-1 candidate (every other radius-1
cell is either occupied or rejected by `WithinMap`), while dozens of radius-2 cells are valid.
Asserting `found == (4,0)` therefore distinguishes radius 1 from radius **2** — a one-cell
difference, versus round 3's measured +/-3-cell looseness. The ring's angular hash offset does not
weaken it: the inner loop sweeps the whole ring, so `(4,0)` is found regardless of the start index.
**Gap 2 closed as claimed.**

**Round 1-3 closures spot-checked (still true)**:
- O(2^N) blocker: `OverflowPlacer.ResolveOverflowPosition` still computes `occupied` **once** per
  search (`OverflowPlacer.cs:30`) and hands it to `ResolveOverflowPositionGiven`; no per-cell
  `CityOccupancy.IsFree` re-entry anywhere. The perf-guard tests still pass inside the 4 s gate.
- Map-clamp / nullable `Resolve`: `maxRadius = mapWidth + mapHeight` plus the `WithinMap` filter
  (`OverflowPlacer.cs:53,64,69`) still bound the search, and `CompleteProject` still returns `false`
  on a null `Resolve` (`ConstructionSystem.cs:87`) instead of fabricating a position.

---

## R4.5 Gate Check

- **Command**: `bash scripts/test.sh --filter "Category!=Scenario&FullyQualifiedName~Cities"` — run
  in the working tree, with the accepted pre-existing caveat that HEAD alone does not compile
  standalone because of unrelated in-flight Stage-4 work.
- **Result**: ✅ **220 passed, 0 failed, 0 skipped** (.NET) + 403 passed / 67 files (the web Vitest
  suite that `scripts/test.sh` also runs). Duration 4 s for the .NET filter.
- **Test count**: 216 before this round's 2 commits -> **220** after (**+4**: 3 in `f2219bc`, 1 in
  `e48b15a`) — matches `tasks.md`'s claim exactly. No test deleted and no assertion weakened: both
  commits are purely additive in the test files, and neither touches a pre-existing test.
- With the 6 scratch probes temporarily added: 226 passed, 0 failed.

---

## R4.6 Code Quality — `f2219bc` and `e48b15a`

| Check | Status |
| ----- | ------ |
| No features beyond what was asked | ✅ `f2219bc` = 3 files (1 domain method, 1 system loop, 1 test file); `e48b15a` = 1 test file |
| No abstractions for single-use code | ✅ no new type, no new interface; the fix is a loop restructure plus a one-line method body |
| Only touched files required for the task | ✅ |
| Didn't "improve" unrelated code | ✅ `CompleteProject`, `DueThisTick`, `StartConstruction` untouched |
| Matches existing patterns/style | ✅ same Portuguese comment convention, same `dynamic-city-growth AD-007:` provenance tagging, same doc-comment-on-domain-mutator convention as `EnqueueConstruction`/`Materialize` |
| Commit hygiene | ✅ both commits are scoped and their messages describe exactly what changed (contrast round-2 Gap E / `2133401`) |
| Tests map to a spec requirement, non-shallow | ✅ all 4 new tests trace to the amended `spec.md` Edge Case / `AD-007`; each asserts concrete values (`TicksRemaining`, `Consumed`, exact `CellCoord`); none is a smoke test |
| Would a senior engineer approve? | ✅ yes, with the nits below |

**Non-blocking nits** (reported, not fixed):

1. **`ToList()` per city per tick** (`ConstructionSystem.cs:27`) — the old code had an
   `if (city.ConstructionQueue.Count == 0) continue;` early-out and indexed `[0]` with zero
   allocation; now every city allocates a snapshot list on every daily tick, empty queue included.
   The snapshot itself is *required* for correctness (the stuck branch mutates the list and then
   `continue`s, which would throw on a live enumerator), but the lost early-out is free to restore.
   Given Fase 16's perf work this is worth a one-line follow-up if construction ever shows up in a
   profile; it is not a correctness issue and the perf-guard tests are unaffected.
2. **Missing-recipe semantics silently changed** (`ConstructionSystem.cs:29`) — that `continue` used
   to mean "skip this whole city this tick" (it sat in the per-city loop); it now means "skip this
   project and let the next one pay". Only reachable if a `BuildingRecipe` disappears from the
   catalog mid-run (`StartConstruction` validates it at enqueue time), so it is effectively dead code
   today and the new behavior is arguably better. Untested and undocumented either way.
3. **Two projects can now *complete* in one tick** — a stuck project placing successfully plus the
   paying project reaching `TicksRemaining == 0` in the same tick. Consistent with `AD-007`'s wording
   (the invariant is one project's **resources** per tick, and the stuck one spends nothing), but a
   reader who remembers Fase 8 as "one completion per city per tick" will be surprised. Worth a
   sentence in the `AD-007` trade-off text if anyone ever builds a rate assumption on top of it.
4. **`TicksRemaining == 0` as the "stuck" sentinel** is only unambiguous because
   `BuildingRecipe.Create` rejects `TicksToBuild <= 0` (`CityCatalog.cs:23`). If that validation is
   ever relaxed, a zero-tick recipe would be enqueued already "stuck" and would build **for free**
   (the stuck branch never charges). Currently unreachable; a comment or an assert at the sentinel
   would make the coupling explicit.
5. **Test-only duplication** — `AdSevenTinyCatalog` / `AdSevenTinyCostWeights`
   (`ConstructionSystemTests.cs:246-250`) are verbatim copies of `TinyCatalog` / `TinyCostWeights` a
   hundred lines above in the same class, and `MakeAdSevenScarceWorld` re-implements most of
   `MakeWorldWithMap`. Harmless, but the next person adding a scarce-world test now has two
   near-identical helpers to choose from.

---

## R4.7 Round-3 gap disposition

| Round-3 gap | Severity | Round-4 status |
| ----------- | -------- | -------------- |
| Gap 1 — Fix D contradicts spec/design + FIFO head-of-line blocking | MAJOR | ✅ **Closed.** Documents amended (`AD-007`, `spec.md` Edge Cases, `design.md` Error Handling row) *and* the blocking behavior fixed in code (`f2219bc`). Starvation independently re-tested over 30 ticks: gone. |
| Gap 2 — "nearest" free cell only loosely asserted (surviving mutant M5) | MINOR | ✅ **Closed.** `e48b15a` kills the exact `radius = 2` mutant that survived round 3, verified by re-running it. |
| Gap 3 — residual O(N^2) in `OwnedBuildingFootprintBoxesWithOwners` | MINOR | ⏭️ Unchanged, previously accepted. Not touched this round; still bounded by the perf-guard test. |
| Gap 4 — `2133401` commit hygiene | MINOR | ⏭️ Process-only, documented in `tasks.md`; not fixable retroactively. |
| Gap 5 — `SpatialSettlementFoundingSystem.Frequency => Monthly` proven only indirectly | MINOR | ⏭️ Unchanged, previously accepted. |

---

## R4.8 Cleanliness

`git diff` on `src/LivingWorld.Simulation/Cities/ConstructionSystem.cs`,
`src/LivingWorld.Domain/Cities/City.cs` and `src/LivingWorld.Simulation/Cities/OverflowPlacer.cs`:
**empty**. The scratch probe file `ZzVerifierScratchAd007Tests.cs` was deleted and does not appear in
`git status`. The only working-tree changes are the pre-existing, unrelated Stage-4 ones present at
session start.

---

## R4.9 Summary

**Overall**: ✅ **Ready** — this closes out the feature. Nothing blocking remains.

**Spec-anchored check**: both round-3 open items trace to amended spec text with matching
assertions; no new spec-precision gap.
**Sensor**: 4/4 killed (including round 3's survivor, re-run verbatim).
**Gate**: 220 passed, 0 failed, 0 skipped (+4 vs. pre-round; 226/0 with scratch probes).

**What works**: skip-ahead is real (30-tick starvation probe clean), the resource throttle is real
(per-tick `city.Stock` assertions), the stuck project is charged exactly once (25 free retries with
`Consumed` and `city.Stock` both frozen), removal targets the specific instance and preserves
survivor order (two-stuck-projects probe + `Assert.Same`), strict FIFO in the non-scarce common case
is untouched, and nearest-cell placement is now pinned to a single cell of precision.

**Issues found**: none blocking. Five non-blocking nits in R4.6, the most actionable being the lost
`Count == 0` early-out before `ToList()`.

**Next steps**: none required. Remaining Minor gaps 3-5 stay in their previously accepted state.
