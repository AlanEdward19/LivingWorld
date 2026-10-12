# Tick cost scaling checks

Profile: light
Plan: `.specs/features/tick-cost-scaling/plan.md`

7 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Custo plano

**C1** - Allocation per NPC-tick at 10,000 NPCs is at most 110% of the value at 1,000 (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~ScaleProfileTests.Allocation_is_flat"`

**C2** - Time per NPC-tick at 10,000 NPCs is at most 125% of the value at 5,000 (flat beyond the cache knee); the 10,000 over 1,000 ratio is measured and recorded, not asserted (AC 1, deviation)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~ScaleProfileTests"`; the per-size, per-system table goes in `verification.md`

**C3** - The sensor fails when the cost passes the versioned baseline: bytes within 1% of `scale-sensor.json` and the µs and allocation ceilings (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~ScaleScenarioSensorTests"`

**C4** - The allocation profile is reproducible: the top allocating types at 10,000 NPCs can be listed (GCAllocationTick) and the fixes (closures hoisted at method entry, per-decision trace, struct results, reused buckets and sets) removed them (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~AllocProfileTests"`; before and after bytes sampled go in `verification.md`

### S2 - Sem regressão de resultado

**C5** - The golden file is unchanged by this spec (AC 4, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~GoldenHashesTests"`; `git diff HEAD~1 -- tests/golden` (no output)

**C6** - Two processes agree on the hash and the pins stay (AC 5, AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~DeterminismTwoProcessTests"`; `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~OptimizationEquivalenceTests"`

**C7** - The decision trace and the cognition log still agree with the old behavior: a recorded trace is built exactly as before, and a decision without capture never exposes a trace (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~Cognition|FullyQualifiedName~DecisionTrace|FullyQualifiedName~Utility"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: parallelism (2) | serial tick kept, hash identical C5 · two processes agree C6 | - |
| sizes measured (3) | 1,000 C1 C2 · 5,000 C2 C3 · 10,000 C1 C2 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: n/a - measurement only
- failure modes: C7 (trace not captured must not be read)
- idempotency: n/a - nothing is retried; measurement and allocation changes only
- authorization: n/a - local simulation, no caller identity
- concurrency: n/a - the tick is serial; the reused HashSet and scheduler buckets live inside one tick
- data lifecycle: n/a - no stored data changes, the golden is unchanged (C5)
- dependency failure: n/a - no external dependency
- state transitions: n/a - no state machine touched, results are hash-identical (C5)
- observability: C4 (allocation profile)

## Out of scope

- optimization that changes the hash; Postgres; bytes per NPC per year - see the plan

## Deviations (kept true)

- AC 1 (time at 10,000 within 110% of 1,000) is not met and is not reachable without a data-oriented layout (which changes serialization and the hash): per-system time per NPC roughly doubles between 1,000 and 3,000 NPCs for every system, including `needs-decay`, a plain loop (cache effect), then stays flat (10,000 within +3% of 5,000). Measured after the work: 0.92 µs at 1,000 and 1.69 µs at 10,000 (1.8x), against 5.7 and 2.03 µs before the warm-up fix of the profile. The criterion becomes "flat beyond the knee" (C2) with AD-096.
- `RescheduleBatchParallel` was not adopted: rescheduling is about 10% of the tick and the serial tick keeps the hash independent of thread timing (ADR-0005); no gain justifies the determinism proof.
- Each profiled world is warmed for 3 days before counting: the relationship and index dictionaries double in size during the first days at 10,000 NPCs, which is growth, not steady-state cost (without it 10,000 read 1.112x of 1,000 in allocation).
- The sensor's `BytesPerAliveNpcPerYear` moved slightly (43,863 to 43,870 and 56,845 to 56,829, within 1%) with identical hashes: the baseline was recorded before later behavior commits (AD-093, AD-094).
- The sensor's `MicrosPerAliveNpcTick` baseline is informational (it is a cold single run; the asserted values are bytes and the ceilings).
- The reusable `HashSet` in `BehaviorDecisionSystem` is an instance field: valid because one clock ticks one world at a time.

## Handoff

Eleven source files plus tests, one builder.
