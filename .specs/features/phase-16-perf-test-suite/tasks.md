# Test Suite Performance Tasks

## Execution Protocol (MANDATORY -- do not skip)

Implement these tasks with the `tlc-spec-driven` skill: **activate it by name
and follow its Execute flow and Critical Rules.** Do not search for skill
files by filesystem path. The skill is the source of truth for the full flow
(per-task cycle, sub-agent delegation, adequacy review, Verifier,
discrimination sensor).

**If the skill cannot be activated, STOP and tell the user — do not proceed
without it.**

---

**Design**: `.specs/features/phase-16-perf-test-suite/design.md`
**Status**: Draft

---

## Test Coverage Matrix

> Generated from codebase (`rules/eval-criteria.md`, existing `tests/LivingWorld.Tests/**`
> samples) and spec. Guidelines found: `rules/eval-criteria.md` (R1-R5 — no
> magic numbers in criteria, ≤10y assert-every-tick in gate, causal effects
> need a control arm). No project-wide coverage-threshold config found
> (no `.nycrc`/coverage gate in CI — no CI exists at all, per design.md).

| Code Layer | Required Test Type | Coverage Expectation | Location Pattern | Run Command |
| --- | --- | --- | --- | --- |
| Measurement/profiling tooling (baseline capture, CPU/alloc profile) | none | Not code with branches — verified by the artifact it produces (`baseline-timings.md` exists, is well-formed, and its numbers are reproducible on a second run) | `.specs/features/phase-16-perf-test-suite/baseline-timings.md` | manual run, no automated test |
| Test-execution config (`xunit.runner.json` / parallel-degree setting) | none | Config/entity layer — build gate only, per strong default | `tests/LivingWorld.Tests/xunit.runner.json` | `dotnet build LivingWorld.sln` |
| Simulation hot-path code (`src/LivingWorld.Simulation/**`) | unit (existing) | **No new behavior** → every existing test touching the optimized method must still pass **unmodified**, byte-for-byte same assertions. If the optimization adds a new branch (e.g. a parallel/sequential split), it must be proven order-independent using the same hash-compare-vs-sequential pattern already used by `NpcWakeScheduler`/`ParallelDecayTests.cs` | `tests/LivingWorld.Tests/**` (existing files only — no new test files expected unless a new branch is introduced) | `bash scripts/test.sh --filter Category!=Scenario` (quick), full suite for final validation |

## Parallelism Assessment

> Generated from codebase — confirm before Execute.

| Test Type | Parallel-Safe? | Isolation Model | Evidence |
| --- | --- | --- | --- |
| Existing xUnit scenario/domain tests | Yes | Every test builds its own `(WorldState, WorldClock)` via `ScenarioRunner.Create(seed, ...)` — no shared mutable statics found across `tests/` except a one-time `[ModuleInitializer]` env reset that never mutates per-test | `ScenarioRunner.cs:335-339`, `TestEnvironmentSetup.cs:17-21` (see design.md finding #2) |
| Baseline/profiling runs (T1, T2) | **No** | Must run alone, undisturbed, to get an accurate wall-clock/CPU reading — running them concurrently with other load would invalidate the very numbers they exist to produce | n/a — measurement methodology, not test isolation |
| 3× consecutive full-suite stability check (T4) | N/A (it *is* the parallelism-safety test) | Runs the full suite as configured; its job is to prove parallel execution is safe, not to itself run in parallel with anything else | spec.md PERF-05 |

## Gate Check Commands

> Generated from codebase — confirm before Execute.

| Gate Level | When to Use | Command |
| --- | --- | --- |
| Quick | After any hot-path code change (T5+) | `bash scripts/test.sh` (default `Category!=Scenario`) |
| Full | After parallelism tuning (T3/T4) and after each hot-path task, to confirm scenario-tier tests are also unaffected | `bash scripts/test.sh --filter Category=Scenario` |
| Build | Config-only tasks (T1 setup, xunit.runner.json changes) | `dotnet build LivingWorld.sln` |
| Final | End of feature (T_final) | `dotnet test LivingWorld.sln` (no filter — the actual <1h target being measured) |

---

## Execution Plan

### Phase 1: Baseline & Profiling (Sequential)

```
T1 → T2
```

### Phase 2: Parallelism Tuning (Sequential, depends on Phase 1's CPU verdict)

```
T1 → T3 → T4
```

### Phase 3: Hot-Path Optimization (Sequential per method, count set by T2's findings — see note)

```
T2 → T5 → T6 → T7
```

> **Open count, by design:** T5-T7 are placeholders for "the hot methods T2
> actually finds" (spec.md's Requirement Traceability defers this
> deliberately — see design.md's "Open item for Tasks phase"). T2 profiles
> the 3 slowest test classes, so up to 3 optimization tasks are expected, but
> could be fewer (if slow classes share one root cause) or need a 4th if T2
> finds a bottleneck outside the top-3 wall-clock ranking. **Do not
> pre-guess the target methods** — T5's actual scope is written when T2
> completes and its findings are known, following the same task template as
> T5 below.

### Phase 4: Final Measurement (Sequential, depends on Phase 2 + Phase 3)

```
T4, T7 → T8
```

---

## Task Breakdown

### T1: Instrumented baseline run (timing + CPU utilization)

**What**: Run the full, unfiltered suite once with a duration logger and OS-level CPU sampling active; write `.specs/features/phase-16-perf-test-suite/baseline-timings.md` with (a) every test class ranked by wall-clock time descending, (b) a stated CPU-saturation verdict across all logical cores during the run.
**Where**: `.specs/features/phase-16-perf-test-suite/baseline-timings.md` (new file); no source change.
**Depends on**: None
**Reuses**: `dotnet test --logger "console;verbosity=detailed"` (or `trx` + a duration report), OS CPU sampling (e.g. `dotnet-counters` or platform Task Manager/`perf` equivalent — pick whichever is available, document which was used)
**Requirement**: PERF-01, PERF-03

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `baseline-timings.md` exists with a descending wall-clock ranking of every test class
- [x] CPU-utilization verdict is explicitly stated (saturated / not saturated, with the observed peak %)
- [x] This is a one-time measurement run — no code changed, so gate = build only

**Tests**: none (measurement artifact, not code)
**Gate**: build

**Status**: ✅ Done — see `baseline-timings.md` (2026-08-19). Full suite = 8h03m,
1378/1392 passed, 3 pre-existing failures surfaced (out of scope, flagged in
the doc). CPU avg 31.2%, only 1.1% of samples ≥80% → not saturated.

---

### T2: Hot-method profile of the dominant test method

**What**: ~~Run `dotnet-trace` against each of the 3 slowest test classes~~ — **scope narrowed by T1's own data**: `LongRunScaleTests` is 8h03m of the 8h03m suite total (96% of it is one single `[Fact]`, `Ten_k_population_ten_years_within_perf_budget`, at 7h45m alone). The other 19 classes combined don't even reach 1 hour and mostly overlap it in wall-clock (CPU was idle, not saturated). Profiling 3 classes mechanically would have wasted a profiling pass on classes that don't matter. Run `dotnet-trace` (cpu-sampling) against **only** `Ten_k_population_ten_years_within_perf_budget`, for a bounded 2-minute slice (steady per-tick cost — no need to capture the full 7h45m), and append the hot-method breakdown to `baseline-timings.md`.
**Where**: `.specs/features/phase-16-perf-test-suite/baseline-timings.md` (append); no source change.
**Depends on**: T1
**Reuses**: `dotnet-trace` (in-box .NET SDK tool, per design.md Tech Decisions)
**Requirement**: PERF-02

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] `Ten_k_population_ten_years_within_perf_budget`'s hot-method breakdown (top methods by self-time) is in `baseline-timings.md`
- [x] Breakdown states whether the hot path is engine code (`src/LivingWorld.Simulation/**`) or test-side — T1 already found a ~388× per-tick slowdown for a 20× population increase (500→10,000), consistent with an O(n²)+ system, so this is expected to land on a specific per-NPC system, not test-side overhead

**Tests**: none (measurement artifact)
**Gate**: build

**Status**: ✅ Done — `dotnet-trace` output was unusable (unresolved stacks);
used direct per-system Stopwatch instrumentation instead (temporary, reverted
before commit — see baseline-timings.md for the full trail). Found:
`BehaviorDecisionSystem` = 98.4% of per-tick cost at 10k population, root
cause = `CityPopulationQuery.Population()` (full O(population) NPC scan)
called once per NPC per tick inside `MoveOneAmbientStep`. Single, well-scoped
target — T6/T7 not expected to be needed.

---

### T3: Tune (or rule out) test-execution parallel degree [P]

**What**: Based on T1's CPU-saturation verdict — if cores were idle, add/adjust `tests/LivingWorld.Tests/xunit.runner.json` (`parallelizeAssembly`, `maxParallelThreads`) to raise utilization; if cores were already saturated, write the N/A verdict with T1's evidence into `baseline-timings.md` instead of changing anything.
**Where**: `tests/LivingWorld.Tests/xunit.runner.json` (new or modified) — or no file change if N/A.
**Depends on**: T1 (needs its CPU verdict; does not depend on T2)
**Reuses**: xUnit's native parallel-degree config (design.md Tech Decisions — no custom scheduler)
**Requirement**: PERF-04

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [x] Either `xunit.runner.json` is added/changed with a documented rationale tying it to T1's measured idle-core %, or the N/A verdict is recorded with T1's evidence
- [ ] ~~`dotnet build LivingWorld.sln` succeeds~~ N/A, no change made
- [ ] ~~Test count unchanged~~ N/A, no change made

**Tests**: none (config/entity layer per matrix)
**Gate**: build

**Status**: ✅ N/A per spec.md PERF-04 AC3's escape hatch — T1 shows CPU average
31.2%, only 1.1% of samples ≥80%: cores are idle, but *not* because
parallelism is capped — because the critical path is one single-threaded
7h45m test that nothing else can overlap with once every other class
finishes. Raising `MaxParallelThreads` cannot shorten one sequential test's
own runtime. No `xunit.runner.json` added. Real lever is T2's hot-path
finding, not scheduling.

---

### T4: 3× consecutive full-suite stability check

**What**: Run `dotnet test LivingWorld.sln` (no filter) three times in a row after T3's change (or confirm N/A skip if T3 made no change); confirm identical pass/fail results and identical hash-invariance assertions across all 3 runs.
**Where**: No source change — verification task.
**Depends on**: T3
**Reuses**: Existing hash-invariance tests as the stability oracle.
**Requirement**: PERF-05

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] 3 consecutive full-suite runs produce identical pass/fail counts
- [ ] No flaky/order-dependent failures introduced by any parallelism change
- [ ] Results (pass count, any variance) recorded in `baseline-timings.md`

**Tests**: full suite run 3× (this task IS the test)
**Gate**: full

**Status**: ✅ N/A — T3 made no parallelism change, so there's nothing new to
stability-check. Not re-running the 8h suite 3× (24h) to prove a no-op is
stable. Revisit only if a future task changes test-execution parallelism.

---

### T5: Optimize hot method #1 (target set by T2's findings)

**What**: Apply the specific optimization T2's profile recommends for the #1 hot method (e.g. reduce per-tick allocation, extend `LazyNeed.ValueAt`-style closed form, or `NpcWakeScheduler.RescheduleBatchParallel`-style parallel batching to a comparable system) — exact method and technique are named when T2 completes, not before.
**Where**: `src/LivingWorld.Simulation/**` — exact file set by T2's finding.
**Depends on**: T2
**Reuses**: Whichever existing pattern (`LazyNeed`/`NpcWakeScheduler`) fits the specific hot method found — chosen at implementation time, not pre-decided.
**Requirement**: PERF-06, PERF-07

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Every existing test exercising the optimized method passes unmodified (no assertion changed, none deleted) — proves zero behavior change
- [ ] If a new parallel/incremental branch was introduced, a hash-compare-vs-sequential test proves order-independence (same template as `ParallelDecayTests.cs`)
- [ ] The specific test class T2 flagged as slow because of this method is re-timed and shows a measurable improvement over its T1 baseline number
- [ ] Gate check passes: `bash scripts/test.sh` (quick) and `bash scripts/test.sh --filter Category=Scenario` (full)
- [ ] Test count unchanged unless a new order-independence test was added (documented, not silently dropped)

**Tests**: unit (existing, unmodified) + possibly one new order-independence test if a new branch was introduced
**Gate**: full

**Commit**: `perf(simulation): optimize [method] hot path found in T2 profiling`

---

### T6, T7: Optimize hot methods #2, #3 (same template as T5) [P after T5's pattern is proven]

**What**: Same shape as T5, one per remaining hot method T2 identifies. May run in parallel with each other (not with T5, to keep the first optimization's pattern/review simple) if they touch disjoint files; sequential if they touch the same file/system as T5.
**Where**: `src/LivingWorld.Simulation/**` — set by T2.
**Depends on**: T2 (and T5 only if the same file/system is touched — otherwise independent)
**Requirement**: PERF-06, PERF-07

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**: Same checklist as T5, scoped to that method.

**Tests**: unit (existing, unmodified) + possibly new order-independence test
**Gate**: full

**Commit**: `perf(simulation): optimize [method] hot path found in T2 profiling`

---

### T8: Final full-suite re-measurement vs. baseline

**What**: Run `dotnet test LivingWorld.sln` (no filter) once more; update `baseline-timings.md` with a final comparison table (T1 baseline → post-T3/T4 parallelism-only number → post-T5-T7 final number), stating whether the <1h Success Criterion was met, and if not, exactly what remains and why.
**Where**: `.specs/features/phase-16-perf-test-suite/baseline-timings.md` (final section).
**Depends on**: T4, and all of T5/T6/T7
**Reuses**: Same measurement approach as T1.
**Requirement**: PERF-08

**Tools**:
- MCP: NONE
- Skill: NONE

**Done when**:
- [ ] Final wall-clock time recorded and compared against T1's baseline
- [ ] `dotnet test --filter Category=Scenario` alone re-measured and compared to its own baseline
- [ ] Explicit statement: goal met, or honest gap + remaining candidates (no silent declaration of success)
- [ ] All existing tests still pass — full suite, zero regressions

**Tests**: full suite run (this task IS the final validation)
**Gate**: final

---

## Parallel Execution Map

```
Phase 1 (Sequential):
  T1 → T2

Phase 2 (Sequential, branches off T1):
  T1 → T3 → T4

Phase 3 (branches off T2, internal parallelism conditional on file overlap):
  T2 → T5 → { T6 [P], T7 [P] }   (T6/T7 parallel only if disjoint from T5's files)

Phase 4 (Sequential, joins everything):
  T4, T5, T6, T7 → T8
```

**Parallelism constraint:** T6/T7 are `[P]` relative to each other only —
both still depend on T2, and either loses its `[P]` flag if it turns out to
touch the same file/system as T5 or as each other (checked at Tasks-execution
time, once T2's actual findings are known).

---

## Task Granularity Check

| Task | Scope | Status |
| --- | --- | --- |
| T1: Baseline timing + CPU verdict | 1 artifact (single measurement pass) | ✅ Granular |
| T2: Hot-method profile of 3 classes | 1 artifact, 3 profiling runs of the same kind | ✅ Granular (cohesive — same tool, same output file) |
| T3: Tune/rule-out parallel degree | 1 config file (or 1 documented N/A) | ✅ Granular |
| T4: 3× stability check | 1 verification pass | ✅ Granular |
| T5: Optimize hot method #1 | 1 method/system | ✅ Granular |
| T6/T7: Optimize hot methods #2/#3 | 1 method/system each | ✅ Granular |
| T8: Final re-measurement | 1 artifact (comparison table) | ✅ Granular |

**Granularity check**: every task is one deliverable (one file, one
measurement pass, or one method-level optimization) — no task bundles
unrelated changes.

---

## Diagram-Definition Cross-Check

| Task | Depends On (task body) | Diagram Shows | Status |
| --- | --- | --- | --- |
| T1 | None | None | ✅ Match |
| T2 | T1 | T1 → T2 | ✅ Match |
| T3 | T1 | T1 → T3 | ✅ Match |
| T4 | T3 | T3 → T4 | ✅ Match |
| T5 | T2 | T2 → T5 | ✅ Match |
| T6 | T2 (+T5 if file overlap) | T5 → T6 [P] | ✅ Match (conditional dependency documented in prose, diagram shows the common case) |
| T7 | T2 (+T5 if file overlap) | T5 → T7 [P] | ✅ Match (same conditional note) |
| T8 | T4, T5, T6, T7 | T4, T5, T6, T7 → T8 | ✅ Match |

---

## Test Co-location Validation

| Task | Code Layer Created/Modified | Matrix Requires | Task Says | Status |
| --- | --- | --- | --- | --- |
| T1 | Measurement artifact | none | none | ✅ OK |
| T2 | Measurement artifact | none | none | ✅ OK |
| T3 | Test-execution config | none | none | ✅ OK |
| T4 | Verification run (no code) | n/a | full suite run | ✅ OK |
| T5 | Simulation hot-path code | unit (existing, unmodified) + conditional order-independence test | unit (existing, unmodified) + possibly new order-independence test | ✅ OK |
| T6 | Simulation hot-path code | same as T5 | same as T5 | ✅ OK |
| T7 | Simulation hot-path code | same as T5 | same as T5 | ✅ OK |
| T8 | Measurement artifact + full validation | full suite | full suite run | ✅ OK |

No ❌ violations.
