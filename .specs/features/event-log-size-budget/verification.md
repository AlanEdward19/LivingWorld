# Event log size budget verification

**Verdict**: PASS
**Profile**: light
**Diff range**: ded6f9d..56558de (HEAD, feature commit 56558de)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

Profile `light` does not require fault injection; the two mutations the brief asked for were run anyway and are listed below.

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | log bytes per living NPC per year by kind, 1,000 NPCs, monthly snapshots | `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "...EventLogSensorTests.Compacted_event_log_stays_under"` exit 0, test listed Passed (28 s) | `EventLogSensorTests.cs:66` - `byKind[kind] = ... GetByteCount(kind) + GetByteCount(payload)) / npcYears`; per-kind table `tests/baselines/event-log-bytes.json` (12 kinds) | PASS |
| C2 | fails above 110% of the versioned baseline | same run; mutation M1 below | `EventLogSensorTests.cs:79` - `Assert.True(measured.Total <= 1.10 * baseline.Values.Sum(), ...)` | PASS |
| C3 | compacted total <= 50 B/NPC/yr (about 25) | same run | `EventLogSensorTests.cs:75` - `Assert.True(measured.Total <= BudgetBytesPerNpcPerYear, ...)`; I measured 25.2 (see Measurements) | PASS |
| C4 | non-noise events equal and in order, noise counts kept in aggregates | LongRunning `EventLogSensorTests.Compaction_keeps...` Passed (21 s); Integration `EventLogCompactorTests` 4/4 Passed | `EventLogSensorTests.cs:102` - `Assert.True(rawKept.Count == persistedKept.Count && firstDiff < 0, ...)`; `:111` - `Assert.Equal(rawCount, aggregatedCount)` per the 3 kinds; `EventLogCompactorTests.cs:57-60` - `Assert.Same(birth, compacted[0])`, death, estate | PASS |
| C5 | two processes write the same compacted log | `...DeterminismTwoProcessTests.Same_seed_writes_the_same_compacted_event_log` Passed (2 s) | `DeterminismTwoProcessTests.cs:28` - `Assert.Equal(a, b)` (after `Assert.Equal(64, a.Length)` at `:27`); digest command `Program.cs` `eventlog-digest` | PASS |
| C6 | persisted kind decodes right on out-of-order batch | `EventLogKindEncodingTests` 8/8 Passed; mutation M2 below | `EventLogKindEncodingTests.cs:89` - `Assert.Equal([("early-1","Birth"),("early-2","Death"),("mid","Death"),("late-1","Death"),("late-2","Birth")], loaded.Select(e => (e.Payload, e.Kind)))` | PASS |
| C7 | us/NPC-tick <= 103%, alloc/tick <= 102%, hash identical | `MeasureHarness` A/B (HEAD~1 worktree `../LivingWorld-ab-ev` vs HEAD, 2 rounds each, worktree removed); `GoldenHashesTests` 5/5 Passed; C5 run | see Performance | PASS |

All named tests were seen in the run output individually (no empty filter). Real tree porcelain clean after the run.

## Faults injected

| Mutation | Location | Killed |
| --- | --- | --- |
| M1 (C2): `"Hired": 10.48` -> `1.0` in baseline (sum 25.2 -> 15.7) | `tests/baselines/event-log-bytes.json` | yes - `EventLogSensorTests.cs:79`, "25.2 B/NPC/ano passa de 110% do baseline versionado (15.7)" |
| M2 (C6): `events.OrderBy(e => e.Tick)` -> `events` | `SqliteWorldRepository.cs:69` | yes - `Repository_round_trip_decodes_kinds_correctly...` failed (1 of 8) |

Both restored with `git checkout <path>`; `git status --short` empty afterwards.

## Performance (C7, scale1000, `LW_MEASURE_OUT`)

| Round | HEAD~1 us/NPC-tick | HEAD us/NPC-tick | HEAD~1 alloc/tick | HEAD alloc/tick |
| --- | --- | --- | --- | --- |
| 1 | 0.907 | 0.889 (98.0%) | 241149 | 241036 (99.95%) |
| 2 | 0.867 | 0.834 (96.2%) | 241042 | 240993 (99.98%) |

`snapshotBytesPerAlive` 10860 on both. Expected by construction: `EventLogCompactor` runs only inside `PersistentWorldRunner.Snapshot` (PersistentWorldRunner.cs:41), which `MeasureHarness` never calls. Goldens unchanged (no `tests/` golden file in the diff).

## Measurements I took (temporary probe test, deleted)

Same fixture as the sensor (seed 42, 1,000 NPCs, one year, monthly `runner.Snapshot`), 979.2 NPC-years:

| Quantity | Raw | Compacted |
| --- | --- | --- |
| kind + payload B/NPC/yr (what the sensor counts) | 85.2 | 25.2 |
| fixed columns, estimate (tick 8 + seq 4 + EventId 8 + cause + SourceSystem text) | 189.9 | 75.6 |
| both | 275.2 | 100.8 |
| events | 5195 | 2105 (18 aggregates) |

The 85 -> 25 claim reproduces. Raw noise counts: ResourceLost 1347, WageUnpaid 1600, PlantMatured 161. No noise event carries a CauseEventId and none is referenced as a cause by another event in this run.

## Coverage

Light profile: join not recomputed. Author rows read against tests: door 1 (noise aggregated, history/death immutable) -> C4; ResourceLost, WageUnpaid, PlantMatured -> `EventLogSensorTests.cs:111` loops over all three, unit test `EventLogCompactorTests.cs:27, :30, :60` touches each.

## Gaps and notes (non-blocking)

1. **"25 B" is kind+payload only.** The plan's 50 B budget reads as total log bytes; fixed columns are excluded (disclosed in checks.md Deviations, but not in the roadmap line `phase-09-scale.md:16` "medido: ~25 B"). With an estimate of the fixed columns the compacted log is about 100 B/NPC/yr, and the aggregate rows carry the 16-char `SourceSystem` text. Suggest qualifying the roadmap line ("kind + payload").
2. **Aggregate loses per-event provenance.** Aggregate has `EventId = 0` (record default) and `CauseEventId = null`, `SourceSystem = "EventLogCompactor"`. Safe today (grep: nothing reads persisted ResourceLost/WageUnpaid/PlantMatured rows; `LoadEvents` is used only by tests, none of which touch noise kinds; Api uses `LivingEventPresentationCatalog` on live events). A future cause chain pointing at a noise event id would dangle; no test pins this.
3. **Sum is weak by design.** `LastNumber` adds the last payload field across all workplaces/resources (ResourceLost mixes resources) and returns 0 for a non-numeric tail without a signal. No unit test uses a non-numeric noise payload (`EventLogCompactorTests.cs:54` uses `x|y|1`).
4. **AC 4 precision.** "Byte for byte" is asserted on kind and payload only (`EventLogSensorTests.cs:99-100`); tick, EventId and SourceSystem of kept events are not compared. The compactor passes the same instances (`Assert.Same`), so low risk.
5. **Out-of-order batches** never occur in the sensor fixture (0 inversions in raw events in my run); only the C6 unit test covers it, which M2 confirms kills the regression.
6. **Plan vs build (AC 1-6, door 1):** plan says "daily per city, noise = movement/trade/routine"; build is monthly per snapshot batch and 3 kinds, `Trade`, `Starvation` etc. stay raw. Logged in checks.md Deviations and AD-097; accepted here as a documented change, not hidden.
7. Month boundary: a batch containing ticks of two months yields two aggregates (`EventLogCompactorTests.cs:44`); aggregate tick is the first noise tick of the month, repository sorts by tick, so read order stays consistent.

## Gate

LongRunning EventLogSensorTests+DeterminismTwoProcessTests: 3 passed, 0 failed, 1 skipped (`ZZZ_record_event_log_baseline`, intentional); Integration EventLogKindEncodingTests+EventLogCompactorTests: 12 passed, 0 failed; GoldenHashesTests 5 passed; MeasureHarness 4 runs passed. `verify.sh` not run by instruction.
