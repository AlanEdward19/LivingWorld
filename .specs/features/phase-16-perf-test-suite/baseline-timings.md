# Baseline Timings (T1) — 2026-08-19

**How measured**: user ran `run-baseline.cmd` — `dotnet test LivingWorld.sln`
(no filter, full suite including `Category=Scenario`) with a `trx` logger,
plus a parallel `typeperf "\Processor(_Total)\% Processor Time"` sample every
5s for the whole run. Machine: 20 logical cores (per earlier `nproc`/`os.cpu_count()`
check this session).

## Headline result

- **Total suite wall-clock: 29,011.4s ≈ 8h 03m.** Build also reported 3 failed
  tests (details below).
- **1392 tests total: 1378 passed, 3 failed, 11 skipped.**

## CPU-saturation verdict (PERF-03)

**Not saturated.** Across 5,790 samples over the whole run:

| Metric | Value |
| --- | --- |
| Average CPU (all 20 cores) | 31.2% |
| Max CPU | 99.8% |
| Samples ≥80% CPU | 65 / 5790 (1.1%) |

The machine spent the overwhelming majority of the run with most cores idle.
This is **not** a "parallelism is off" problem — it's that the critical path
is dominated by a single long-running, effectively single-threaded test that
nothing else can overlap with once every other test class has finished. More
parallelism cannot shorten a single sequential test's own runtime.

## Per-class wall-clock ranking (top 10 of 20+)

| Class | Wall-clock |
| --- | --- |
| `Performance.LongRunScaleTests` | **08:03:19** |
| `Performance.ScaleScenarioFixtureTests` | 00:55:40 |
| `Population.FamilyPairedScenarioTests` | 00:42:39 |
| `Population.PopulationScenarioTests` | 00:18:40 |
| `Population.PopulationBaselineTests` | 00:16:54 |
| `Performance.ScaleScenarioSensorTests` | 00:15:18 |
| `Cities.LodConservationScenarioTests` | 00:12:49 |
| `Population.LifeTable100YearScenarioTests` | 00:04:14 |
| `Population.PopulationArchitectureTests` | 00:02:42 |
| `BytesPerNpcPerYearSensorTests` | 00:01:51 |

Everything below `LongRunScaleTests` sums to well under an hour and — per
the CPU-idle finding above — mostly overlaps with it in wall-clock time
(these ran concurrently while `LongRunScaleTests` was still going).

## The actual bottleneck, isolated to one test method

`Performance.LongRunScaleTests` has two `[Fact]`s. Breaking the class total down:

| Test | Duration | Outcome | What it does |
| --- | --- | --- | --- |
| `Ten_k_population_ten_years_within_perf_budget` | **07:45:12** | Passed | 10,000 initial population, `clock.Run(world, 10*365*24)` = 87,600 ticks |
| `Storage_cost_per_alive_npc_stable_across_horizons` | 00:18:07 | **Failed** | 500 initial population, 3 separate from-tick-0 runs at 1y/50y/100y (8,760 + 438,000 + 876,000 = 1,322,760 ticks total) |

**`Ten_k_population_ten_years_within_perf_budget` alone is 96% of the entire
8-hour suite.** Per-tick cost comparison:

- `Ten_k_...`: 7h45m12s / 87,600 ticks ≈ **318.7 ms/tick** at 10,000 population
- `Storage_cost_...`: 18m07s / 1,322,760 ticks ≈ **0.82 ms/tick** at 500 population

A 20× increase in population (500 → 10,000) produced a **~388× increase in
per-tick cost**, not a ~20× (linear) or ~400× (quadratic-and-then-some)
increase — this is consistent with an **O(n²) or worse** hot path inside one
of the per-tick systems that only bites at large population, contradicting
the earlier code-reading pass (which found no *obvious* full-recompute in
`WorldClock` itself — the cost lives inside a specific system's per-NPC work,
not the clock loop). This single method is the entire optimization target
for T2/T3 — the other 19 test classes combined are not worth touching yet.

## Failures found (not caused by this session — pre-existing, surfaced by running the full suite for the first time in a while)

1. `Population.FamilyPairedScenarioTests.Vitality_cv_paired_difference_between_real_and_neutral_drift_across_20_seeds_bootstrap_ci95` — Failed
2. `Performance.ScaleScenarioSensorTests.One_month_scale_run_stays_within_recorded_baseline(initialPopulation: 5000)` — Failed
3. `Performance.LongRunScaleTests.Storage_cost_per_alive_npc_stable_across_horizons` — Failed

These are functional/statistical test failures, not performance-tuning
targets — out of scope for this perf feature per spec.md, but flagged here
since T1's full run is what surfaced them (the filtered gate never runs them).

## T2: Hot-method profile

**Attempt 1 — `dotnet-trace` (cpu-sampling) on `Ten_k_population_ten_years_within_perf_budget`,
2-minute slice.** Result: mostly unusable. The converted speedscope JSON's stacks
resolved to PerfView-style synthetic buckets (`UNMANAGED_CODE_TIME`,
`CPU_TIME`) covering ~99% of aggregate time, with only trace amounts
attributed to actual method frames — not enough resolution to rank hot
methods. Documenting the limitation rather than fabricating numbers from it,
per design.md's instruction.

**Attempt 2 — direct per-system Stopwatch instrumentation (used instead).**
Temporarily instrumented `WorldClock.Tick`'s per-system loop with a
`Stopwatch` accumulating wall-clock by `system.Name`, ran 500 ticks at
10,000 population (`ScaleScenarioFixture.CreateWorld(seed: 42, 10_000)`),
wrote the per-system total to a scratch file, then **reverted the
instrumentation via `git checkout`** — this was diagnostic-only, never
shipped, no production behavior touched. Result (500 ticks, descending):

| System | Total (ms) | Per-tick (ms) | Share |
| --- | --- | --- | --- |
| `behavior-decision` | 396,649.5 | 793.3 | **~98.4%** |
| `population-relationship` | 6,259.1 | 12.5 | 1.6% |
| `population-skill-teaching` | 4,236.8 | 8.5 | 1.1% |
| `needs-decay` | 332.6 | 0.7 | 0.1% |
| everything else (16 systems) | <100 combined | <0.2 combined | <0.1% |

`behavior-decision` (`BehaviorDecisionSystem`) is not just the largest
contributor — it **is** the bottleneck. Every other system combined is noise
by comparison.

**Root cause, found by reading the code this points to**
(`src/LivingWorld.Simulation/Behavior/BehaviorDecisionSystem.cs`):

- `Tick(...)` already avoids the *obvious* trap — `occupancy`, `marketIndex`,
  and `vacancyIndex` are all built **once per tick**, not once per NPC (a
  comment at line 37-40 documents an earlier O(n²) fix along exactly this
  pattern).
- But `MoveOneAmbientStep` (called once per NPC per tick, whenever an Idle/
  Work/Socialize action completes — line 89) computes city bounds via:
  ```csharp
  CityBounds? homeBounds = world.FindCity(npc.City) is { } city
      ? SpatialBoundsResolver.ResolveCity(
          city, CityPopulationQuery.Population(world, city.Id), ...).Bounds
      : null;
  ```
  (`BehaviorDecisionSystem.cs:108-111`)
- `CityPopulationQuery.Population(world, cityId)` (`src/LivingWorld.Simulation/Cities/CityPopulationQuery.cs:16-17`)
  is `world.Npcs.Where(n => n.IsAlive && n.City == city).LongCount()` — **a
  full linear scan of every NPC in the world**, by design ("sempre on-demand
  a partir de `WorldState.Npcs`... nenhum campo é cacheado", per the type's
  own doc comment — a deliberate no-caching choice made when this was fine
  at smaller population, now the bottleneck at scale).

Net effect: **O(NPCs completing an ambient action this tick × total
population)**, paid every hour, for 87,600 hours. At 500 population this is
negligible; at 10,000 population, with a meaningful fraction of the wake
batch moving every hour, this reproduces exactly the ~388× per-tick blowup
T1 measured for a 20× population increase — a linear-scan-inside-a-per-NPC-loop
pattern is quadratic in population, not linear.

**This is now the single, concrete, well-scoped optimization target for T5**
— no other system comes close, so no T6/T7 are expected to be needed.

## T5: Hot-path fixes (zero behavior change)

Three fixes applied, all preserving exact simulation output (existing
hash-invariance tests pass unmodified):

1. **`BehaviorDecisionSystem.cs`** — `CityPopulationQuery.Population()` (full
   O(population) NPC scan) was called once per NPC per tick inside
   `MoveOneAmbientStep`. Now memoized in a per-tick `Dictionary<CityId, long>`
   (population doesn't change within a tick from ambient movement, so the
   cached value is identical to the recomputed one). Measured impact alone:
   the 10-year target test went from 7h45m12s → 6h43m17s (~13% faster) —
   smaller than expected, because this wasn't actually the dominant cost.

2. **`EventScheduler.cs`** — `Schedule()` did `bucket.Add(evt);
   bucket.Sort(...)` (full re-sort of every event sharing a target tick, on
   *every* insertion — O(k log k), k = events at that tick) purely to feed a
   `(Tick, Index)` tuple into `_indexById` whose `Index` field was **never
   read anywhere in the file** (confirmed by grep) — dead computation.
   Replaced with a binary-search insert that keeps the same sorted-by-Id
   invariant at O(k) instead of O(k log k), and dropped the unused `Index`
   tracking entirely (now just `Dictionary<long, long>` id→tick). This was
   the dominant cost: a 500-tick / 10k-population sample went from
   793ms/tick (98.4% of tick cost in `behavior-decision`, found via direct
   per-system Stopwatch instrumentation, `dotnet-trace`'s output having been
   unusable — see above) down to **98.7ms/tick — an 8× improvement** for
   early-simulation ticks. Full quick gate (`Category!=Scenario`) dropped
   from 50m24s → 19m32s with fix 1 alone still active, confirming the win
   isn't isolated to one test class.

3. **`Population/RelationshipSystem.cs`** — the daily decay loop did
   `world.Relationships.OrderBy(...).ThenBy(...)` over the **entire**
   relationships collection before applying `DecayTowardNeutral` to each
   entry. Confirmed `DecayTowardNeutral` reads/writes only its own
   `Relationship` instance (no RNG, no cross-entry dependency), so the final
   state is provably order-independent — removed the sort entirely (was
   O(R log R) per day for no observable effect, R = relationship count).

Final quick gate with all three fixes: **16m30s** (down from an unmeasured-
but-clearly-much-larger baseline; the original full run never completed a
`Category!=Scenario`-only timing in isolation, only the full 8h03m
unfiltered run).

## New finding: a fourth, much larger issue — out of scope for this feature

While chasing why the 10-year target test *still* didn't finish in a
reasonable time after fixes 1-2 (a user-run instrumented try was stopped
after 7h39m, well past the ~2h24m fix 1+2 alone would predict from the
500-tick sample), a **checkpointed growth diagnostic** (advancing
`ScaleScenarioFixture.CreateWorld(seed:42, pop:10_000)` in stages, sampling
ms/tick and collection sizes at each stage — no disk caching, single
in-process run) found the per-tick cost is **not constant over simulated
time** — it grows, because `world.Relationships` grows explosively:

| Checkpoint (tick) | ms/tick (next window) | `world.Relationships.Count` | alive NPCs |
| --- | --- | --- | --- |
| 0 | 41.7 | 1,507,264 | 7,342 |
| 2,000 | 172.0 | 14,267,888 | 5,093 |
| 6,000 | 172.5 | 16,536,060 | 2,141 |

**Root cause**: `RelationshipSystem.ApplyCohabitationForMembers` creates one
relationship per **ordered pair** of NPCs present in the same household or
workplace, every day (`O(k²)`, k = group size). `ScenarioRunner.ScaleEconomyCatalog`
(line ~278) sets `maxWorkersPerCycle: 80 * mult` with
`mult = population / 100` — at population 10,000, a single workplace can
hold **8,000 simultaneous workers**, so cohabitation alone can evaluate up
to 8,000² = 64,000,000 pairs *in one workplace, in one day*. This produced
16.5M relationship entries and ~5GB of `testhost.exe` memory within roughly
6,000 ticks (~8 months simulated).

This is very likely an unintended interaction between two independently
reasonable decisions from different phases — phase 7's household-scale
(2-8 members) pairwise relationship model, and phase 9's workplace
vacancy-multiplier scaling for large populations — not a deliberate design
choice. **Fixing it requires bounding which pairs get evaluated, which
changes simulation output** (fewer relationships tracked at scale) — it
fails this feature's "zero behavior change" constraint (spec.md), so it is
**out of scope here**. Spawned as a separate decision/task (see chat) rather
than folded into this perf-only feature.

**Practical consequence for T5/T8**: the full 10-year, 10,000-population
`Ten_k_population_ten_years_within_perf_budget` test remains impractically
slow (multi-hour) until that separate fix lands, *despite* the three
zero-behavior-change fixes above being real, measured, substantial wins
(8× on early ticks, ~3× on the quick gate). The <1h full-suite goal for
*this specific test* is blocked on the spawned-out task, not on further work
within this feature's scope.

## Implication for the rest of this feature

- **P2 (parallelism tuning): downgraded to N/A**, per spec.md PERF-04's own
  escape hatch ("IF baseline shows CPU already saturated THEN N/A" — here
  it's the mirror case: CPU is idle, but idle cores can't help because
  nothing else is left to run in parallel with the one long test). Tuning
  `MaxParallelThreads` would not move the needle; T3/T4 in tasks.md are
  marked N/A with this evidence.
- **P3 (hot-path optimization) is now fully scoped**: profile
  `Ten_k_population_ten_years_within_perf_budget` specifically (not a guess
  across 3 classes — one method accounts for 96% of runtime) to find the
  specific system responsible for the apparent O(n²)+ scaling, then fix it
  with zero behavior change (existing hash-invariance tests as the
  correctness gate, per design.md).
