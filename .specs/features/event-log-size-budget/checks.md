# Event log size budget checks

Profile: light
Plan: `.specs/features/event-log-size-budget/plan.md`

7 checks in 2 slices · 1 one-way door · 0 open

## Checks

### S1 - Sensor do log

**C1** - The persisted event log is measured in bytes per living NPC per year by event kind at 1,000 NPCs with monthly snapshots (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~EventLogSensorTests.Compacted_event_log_stays_under"`; the per-kind table is `tests/baselines/event-log-bytes.json`

**C2** - The sensor fails when the total passes 110% of the versioned baseline (AC 2)
Proof: same test, second assertion; mutation: lowering a baseline entry below the measured total makes it fail

### S2 - Meta da Fase 9

**C3** - The compacted log measures at most 50 B per living NPC per year at 1,000 NPCs (measured about 25) (AC 3)
Proof: same test, first assertion (`BudgetBytesPerNpcPerYear`)

**C4** - Every non-noise event (history, death, hiring, birth, estate) leaves compaction byte for byte equal and in order, and the noise kinds keep their counts in the aggregates (AC 4, door 1)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~EventLogSensorTests.Compaction_keeps"`; `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~EventLogCompactorTests"`

**C5** - Two processes write the same compacted log (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~DeterminismTwoProcessTests.Same_seed_writes_the_same_compacted_event_log"`

**C6** - The persisted kind of every event is read back correctly even when a batch arrives out of tick order (the bug found while measuring) (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~EventLogKindEncodingTests"`; mutation: reverting the ordered write fails `Repository_round_trip_decodes_kinds_correctly...`

**C7** - Time per NPC-tick and allocation per tick are unchanged (compaction runs at the snapshot boundary, outside the tick) and the hash is identical in two processes (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~MeasureHarness|FullyQualifiedName~GoldenHashesTests"`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: what compacts (2) | noise aggregated C4 · history and death immutable C4 | - |
| kinds compacted (3) | ResourceLost C4 · WageUnpaid C4 · PlantMatured C4 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: C6 (the kind decode on out-of-order batches)
- failure modes: C6 (a read must never assign a wrong kind)
- idempotency: n/a - compaction runs once per snapshot batch on the drained buffer
- authorization: n/a - local persistence, no caller identity
- concurrency: n/a - the runner serializes snapshot and drain behind its gate
- data lifecycle: C4 (old worlds keep their raw events; new batches are aggregated; nothing to migrate)
- dependency failure: n/a - no external dependency
- state transitions: n/a - no state machine touched
- observability: C1 (per-kind bytes)

## Out of scope

- snapshot bytes; cold archive of the dead; gzip compression - see the plan

## Deviations (kept true)

- The aggregate is per month and kind within a snapshot batch, not "daily per city": with about 4 noise events a day a daily aggregate barely saves and its fixed cost per world would exceed 30 B/NPC at 1,000 NPCs. With the API's 24-tick snapshots the aggregate is daily.
- The aggregate is stored as the noise kind with `SourceSystem = EventLogCompactor` and payload `count|sum`, not as a new event kind (no enum or catalog change); the sum is the last numeric field of the payload (amount or value).
- Only three kinds are noise (ResourceLost, WageUnpaid, PlantMatured); Hired and Fired (about 14 B/NPC/year) stay because they are the employment history.
- The sensor counts kind plus payload bytes, like the existing snapshot sensors; fixed columns (tick, sequence, ids) are excluded.
- A pre-existing bug was fixed on the way (C6): out-of-order batches decoded wrong kinds.

## Handoff

Four source files, one new project file group of tests, one builder.
