# Test Suite Performance Specification

## Problem Statement

The full `dotnet test` run (all categories, including 100-year and paired-seed
scenario tests) takes several hours, so it is never run in full during normal
work — only the filtered gate (`scripts/test.sh`, `Category!=Scenario`) runs
regularly. This means long-horizon invariants (money conservation, life
tables, referential integrity) go unverified for long stretches. We need the
entire suite runnable in under 1 hour, achieved purely by making the
simulation itself run faster — not by storing or skipping simulated work.

## Goals

- [ ] Full suite (`dotnet test`, no category filter) completes in **under 1
      hour** on a standard dev machine.
- [ ] Every optimization produces **bit-identical simulation results**
      (verified by the existing hash-invariance tests) — only wall-clock time
      changes, never behavior or output.
- [ ] Baseline timing AND CPU-utilization profiling are captured before
      optimizing, so effort goes to whatever actually dominates the runtime
      (today: assumed, not measured).

## Out of Scope

| Feature | Reason |
| --- | --- |
| Any form of caching, snapshotting, or persisting simulated world state to disk or in memory across tests | **User explicitly rejected this** — a world-state cache risks growing to gigabytes across the many `(seed, config)` combinations in the suite, which is unacceptable regardless of the wall-clock benefit. All speedup must come from the simulation itself running faster, not from avoiding re-simulation. |
| Skipping/fast-forwarding simulated years via closed-form approximation | Same rejection — any mechanism that avoids actually computing a tick's state is off the table; only genuine compute-speed improvements to the real tick loop are in scope. |
| CI pipeline (GitHub Actions/Azure Pipelines) to enforce the <1h budget on push | No CI exists in this repo today; adding one is an infra decision separate from suite performance itself. `scripts/test.sh` + a documented local timing check cover the immediate need. |
| Reducing simulated-year coverage (e.g. dropping the 100y or 20-seed paired tests) | The long horizons exist to catch drift/invariant violations only visible over time; the goal is to make them *fast*, not remove them. |
| Distributed/cloud test execution (e.g. splitting the suite across machines) | Single-dev-machine, in-process parallelization is expected to be sufficient; revisit only if it isn't. |

---

## Assumptions & Open Questions

| Assumption / decision | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Scope of "<1h" | Whole suite, `dotnet test` with no filter, scenario tests included | User explicitly chose "suite inteira, cenários incluídos" | y |
| Optimization strategy | Pure compute-speed work: (a) confirm/tune test-level parallelism, (b) profile the tick loop and systems for real hot paths, (c) optimize those hot paths (algorithmic fixes, reduced allocation, wider use of the existing closed-form/parallel patterns already in the codebase) | User rejected caching/fast-forward outright after seeing it in design review: "Quero somente alternativas de otimização das simulações." Every remaining lever is genuine engine/test speed, none of it changes what is computed. | y |
| Output-identity bar | Every optimized code path must still pass the existing hash-invariance tests (`TickLoopHashInvarianceTests.cs`, `ZeroRoundTripsTests.cs`, `MaterializationRoundTripTests.cs`) unmodified | User: "não quero que afete o motor como ele funciona, quero ter o máximo possível do mesmo resultado em menos tempo" — reusing the suite's own existing correctness bar avoids inventing a new one | y |
| Baseline measurement | Task 1 of implementation records per-test-class wall-clock timing AND CPU utilization (dotnet-trace or `dotnet test` duration logger + OS-level CPU sampling) before any optimization work | Cannot tell whether the bottleneck is single-threaded compute cost per tick, under-utilized parallelism, or allocation/GC pressure without measuring — optimizing blind risks solving the wrong problem | y |
| No new manual "engine version" bookkeeping | N/A — dropped along with caching; nothing to invalidate anymore | Was only needed to invalidate a checkpoint cache; with caching removed entirely, this concern is moot | y |

**Open questions:** none — all resolved or logged above.

---

## User Stories

### P1: Baseline + profiling ⭐ MVP

**User Story**: As the developer, I want to know exactly which tests/systems
dominate the suite's runtime and whether CPU is actually saturated during a
run, so optimization effort targets the real bottleneck instead of a guess.

**Why P1**: Every later story depends on this data. Optimizing without a
baseline makes "it's faster now" unverifiable and risks tuning the wrong
thing (e.g. adding parallelism when the real cost is single-tick allocation
churn, or vice versa).

**Acceptance Criteria**:

1. WHEN `dotnet test LivingWorld.sln` is run with a per-class/per-method
   duration logger THEN the report SHALL list wall-clock time for every test
   class, sorted descending, committed as
   `.specs/features/phase-16-perf-test-suite/baseline-timings.md`.
2. WHEN the 3 slowest test classes from (1) run THEN a CPU/allocation profile
   (e.g. `dotnet-trace` or equivalent) SHALL be captured for each, identifying
   the top methods by self-time, recorded in the same baseline document.
3. WHEN the baseline run's CPU utilization is measured across all logical
   cores THEN the report SHALL state whether the machine is CPU-saturated
   during the run (i.e. whether more parallelism could help at all, or
   whether the bottleneck is single-threaded per-tick cost).

**Independent Test**: Run the profiled baseline once; confirm
`baseline-timings.md` exists with a descending time-ranked list, a hot-method
breakdown for the 3 slowest classes, and a stated CPU-utilization verdict.

---

### P2: Tune test-execution parallelism

**User Story**: As the developer, I want independent tests to actually use
all available cores during a run, so wall-clock time drops without touching
any simulation code.

**Why P2**: Investigation found xUnit already parallelizes test classes by
default (no config disables it) — this story is about *confirming and tuning
the degree* (e.g. `MaxParallelThreads`), not introducing parallelism from
scratch. It's the lowest-risk lever: zero behavior change, pure scheduling.

**Acceptance Criteria**:

1. WHEN Task 1's baseline shows CPU is not saturated during a run THEN the
   parallel thread count SHALL be tuned (e.g. via `xunit.runner.json`
   `parallelizeAssembly`/`maxParallelThreads`, or `dotnet test -- 
   XUnit.MaxParallelThreads=<N>`) until CPU utilization rises measurably and
   wall-clock time drops.
2. WHEN parallelism is tuned THEN the full suite SHALL be run 3× consecutively
   with identical pass/fail results and identical hash-invariance assertions
   each time, proving no test gained cross-test interference.
3. IF Task 1's baseline shows CPU is already saturated THEN this story SHALL
   be recorded as **not applicable** (with the measurement as evidence) rather
   than forcing a parallelism change that can't help.

**Independent Test**: Compare wall-clock time and CPU utilization before and
after the tuning change; confirm 3 consecutive full-suite runs are stable.

---

### P3: Optimize the real hot paths found by profiling

**User Story**: As the developer, I want the specific methods that Task 1's
profiler identifies as dominating tick cost to be measurably faster, with the
simulation output completely unchanged, so the 100-year and paired-seed tests
stop being the long pole without any change in what they verify.

**Why P3**: This is the actual engine-speed work — everything else in this
feature is measurement or scheduling. Scoped to *whatever profiling actually
finds* rather than a pre-guessed list, since Task 1 explicitly exists because
today's hot path is unknown.

**Acceptance Criteria**:

1. WHEN a hot method identified in Task 1's profile is optimized (e.g.
   reducing per-tick allocations, replacing a repeated per-NPC scan with a
   precomputed/incremental value, extending the existing
   `NpcWakeScheduler.RescheduleBatchParallel` / `LazyNeed.ValueAt`
   closed-form/parallel patterns to a comparable hot system) THEN every
   existing test that exercises that code path SHALL still pass unmodified,
   including all hash-invariance and monotonic-field tests.
2. WHEN an optimized hot path is measured in isolation (micro-benchmark or
   before/after timing of the specific test class it powers) THEN it SHALL
   show a measurable wall-clock improvement over the Task 1 baseline for that
   class.
3. WHEN all P3 optimizations land THEN the full-suite wall-clock time SHALL
   be re-measured and compared against the Task 1 baseline and the P2
   parallelism-only number, isolating how much each layer contributed.

**Independent Test**: Re-run the exact test classes flagged as slowest in
Task 1's baseline before/after each optimization; diff shows lower wall-clock
time and zero change in assertion outcomes.

---

## Edge Cases

- WHEN an optimization changes iteration order (e.g. parallelizing a
  previously-sequential per-NPC loop) THEN any test asserting an
  order-dependent side effect (not just end-state) SHALL be checked
  explicitly — parallelizing a loop must not silently change *when* things
  happen, only how fast the total computation completes.
- WHEN a profiled hot method is shared by multiple systems THEN optimizing it
  SHALL be verified against every system's own test coverage, not just the
  one test class that happened to be slowest.
- WHEN full-suite wall-clock is still over 1 hour after P1-P3 THEN the
  baseline document SHALL be updated with the remaining gap and a clear
  statement of what was tried and what's left, rather than silently declaring
  success.

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
| --- | --- | --- | --- |
| PERF-01 | P1: Baseline timing report | Design | Pending |
| PERF-02 | P1: Hot-method profile of 3 slowest classes | Design | Pending |
| PERF-03 | P1: CPU-saturation verdict | Design | Pending |
| PERF-04 | P2: Parallel-degree tuning (or N/A verdict) | Design | Pending |
| PERF-05 | P2: 3× consecutive full-suite stability check | Design | Pending |
| PERF-06 | P3: Hot-path optimization(s), output-identical | Design | Pending |
| PERF-07 | P3: Per-optimization before/after measurement | Design | Pending |
| PERF-08 | P3: Final full-suite re-measurement vs. baseline | Design | Pending |

**ID format:** `PERF-NN`

**Status values:** Pending → In Design → In Tasks → Implementing → Verified

**Coverage:** 8 total, 0 mapped to tasks, 8 unmapped ⚠️ (Tasks phase not yet run)

---

## Success Criteria

- [x] `dotnet test LivingWorld.sln` (no filter, all categories) completes in
      under 1 hour on the reference dev machine, down from the current
      multi-hour baseline. **Met: 8h03m19s → 36m7s.**
- [x] `dotnet test --filter Category=Scenario` alone also measurably
      improves versus its own current baseline. **Met: the target test alone
      went 7h45m12s → 24m30s.**
- [x] Every existing hash-invariance/monotonic-field test still passes —
      **for the three zero-behavior-change fixes (T5 items 1-3),
      unmodified.** A fourth fix (cohabitation group-size cap) was added
      mid-feature with explicit user approval in chat after investigating
      whether its O(k²) growth was intentional design (it wasn't) —
      this one *does* change simulation output at scale by design, so
      `tests/golden/world-hashes.json` and `tests/baselines/scale-sensor.json`
      were deliberately regenerated (not silently weakened) to reflect it.
      This is a logged deviation from the original "zero behavior change"
      framing, not a silent scope violation — see baseline-timings.md T5/T8
      and the chat record for the rationale.
- [x] Baseline, profiling, and post-optimization timings are all recorded in
      `.specs/features/phase-16-perf-test-suite/baseline-timings.md`.
- [x] No new caching, snapshotting, or persisted world-state mechanism exists
      anywhere in the codebase as a result of this feature — confirmed, all
      four fixes are either pure algorithmic/data-structure improvements
      (fixes 1-3) or a bounded-computation change (fix 4), none add
      caching/persistence.
