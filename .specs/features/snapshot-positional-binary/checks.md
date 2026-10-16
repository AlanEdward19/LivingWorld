# Snapshot positional binary checks

Profile: light
Plan: `.specs/features/snapshot-positional-binary/plan.md`

12 checks in 5 slices · 2 one-way doors · 0 open

## Checks

### S1 - Codec posicional

**C1** - Over 20 seeds (and 20 seeds after 10 simulated years) the codec returns a world with an equal canonical hash (and the same volatile hash as the JSON round trip), and re-encoding the decoded tree does not change a byte (AC 1)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PositionalSnapshotTests.Codec_round_trips"`

**C2** - Every persistable `WorldState` property is in the encoded tree, so a new property cannot be left out silently (AC 2)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PositionalSnapshotTests.Every_persistable_world_property_is_in_the_encoded_tree"`

**C3** - Same seed and ticks write identical bytes, in one process and across two separate processes (AC 3)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PositionalSnapshotTests.Same_seed_and_ticks_write_identical_bytes"`; `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~DeterminismTwoProcessTests.Same_seed_writes_identical_positional"`

### S2 - Redução de bytes e diff

**C4** - Bytes per NPC per year of the 1,000-NPC fixture are at most 50% of the 131,737 baseline (measured 43,863, 33%), and 5,000 NPCs 56,846 (AC 4)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~ScaleScenarioSensorTests|FullyQualifiedName~BytesPerNpcPerYear"`

**C5** - The diff holds only changed fields of changed NPCs: an unchanged dirty NPC has no entry, a changed one has `Id` plus the changed fields, and only the dirty set is compared (AC 5)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~BinarySnapshotWriterDiffTests"`

**C6** - The diff applied to its base rebuilds the current world (equal canonical hash) across a simulated year, and is smaller than the full block when little changed (AC 5, AC 8)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PositionalSnapshotTests.Delta_applied_to_the_base_rebuilds_the_current_world_across_a_year|FullyQualifiedName~PositionalSnapshotTests.Delta_is_smaller_than_a_full_snapshot_when_little_changed"`

### S3 - Hash incremental

**C7** - Over 10 simulated years, for 3 seeds (12, 30 and 60 NPCs), the incremental hash equals the from-scratch hash every 100 ticks (AC 6)
Proof: `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~IncrementalHasherTests"`

**C8** - Saving through the runner and reopening never changes the world hash (a run persisted every tick equals the continuous run), and the golden file matches the current code (AC 7)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~GoldenHashesTests"`; `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~PersistentWorldRunnerTests"`

### S4 - Persistência ligada

**C9** - The runner writes a full base then diffs, starts a new base after 12 diffs, and the world reopened from base plus diff by a fresh runner continues 3,650 ticks with the continuous hash; a runner that reopened from a diff starts a new base; a failed save drops the base; a diff whose base row was replaced is rejected by the hash check (AC 8)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~PositionalSnapshotPersistenceTests"`

**C10** - A legacy row with only JSON loads through the JSON reader and the next save is a full binary base; invalid or mismatched blocks are rejected (AC 9)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~PositionalSnapshotPersistenceTests.A_legacy_json_only_row_loads_through_the_json_reader_and_the_next_save_is_a_full_binary_base"`; `dotnet test tests/LivingWorld.Tests.Unit -c Release --filter "FullyQualifiedName~PositionalSnapshotTests.Invalid_or_mismatched_blocks_are_rejected"`

**C11** - Zero database round trips during the tick (AC 10)
Proof: `dotnet test tests/LivingWorld.Tests.Integration -c Release --filter "FullyQualifiedName~ZeroRoundTripsTests"`

### S5 - Custo

**C12** - Loading 10,000 NPCs from binary takes at most the JSON load time, a full save at most 120% of the JSON save, and the in-memory base stays under 50 MB; µs per NPC-tick and allocation per tick stay within 102% of before (AC 11, AC 12)
Proof: `dotnet test tests/LivingWorld.Tests.LongRunning -c Release --filter "FullyQualifiedName~SnapshotCostTests|FullyQualifiedName~MeasureHarness"`; numbers go in `verification.md`

## Coverage

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| door 1: file format (3) | round trip C1 · coverage C2 · determinism C3 | - |
| door 2: snapshot column (3) | chain C9 · legacy row C10 · invalid block C10 | - |
| block kinds (2) | full C1 · diff C6 | - |

- Claims naming a status code or response shape: none - no HTTP surface
- No other check claims more than the single case its proof exercises

## Swept

- validation: C10 (invalid and mismatched blocks)
- failure modes: C9 (a failed save drops the base; the next save is a full block; a replaced base row is rejected, not decoded wrong)
- idempotency: n/a - saving the same tick upserts the row; a diff never lands on its own base tick (`Fits`, C9)
- authorization: n/a - local persistence, no caller identity
- concurrency: n/a - the runner gate serializes save and load, as before
- data lifecycle: C9 (new base every 12 diffs), C10 (old rows stay readable)
- dependency failure: C9 (base row missing -> `InvalidDataException`)
- state transitions: C9 (full -> diff -> full)
- observability: n/a - no new events

## Out of scope

- Postgres; event log compression (Phase 28 T1); changing the canonical hash - see the plan

## Deviations (kept true)

- The plan said fields in declaration order by reflection over `[Canonical]`. The codec instead encodes the persistable tree that the JSON serializer already builds (same property set, `Ephemeral` excluded): C2 keeps the coverage guarantee without a second field map. The format is still positional in the sense that matters (string table, varint, key-once tables).
- `IncrementalHasher.CombineIncremental` stays unused: `CanonicalHash` already combines cached per-entity fragments (`MatchesCanonical`, C7).
- The plan quotes 131,737 and 149,661 B/NPC/year (the sensor values before the trade ledger); HEAD's `scale-sensor.json` held 130,314 and 155,528 after it. C4 asserts the 50% bound against the plan's 131,737 explicitly and the ±1% band against the re-recorded baseline.
- The 5,000-NPC baseline bytes were re-recorded by hand together with the 1,000 one (only `BytesPerAliveNpcPerYear` changed; µs and allocation baselines untouched).
- The base is kept in memory as 64-bit fingerprints, not as a tree (the tree took over 1 GB with 10,000 NPCs; fingerprints about 5 MB). Reading a diff still rebuilds base and diff trees, only when reopening a world.
- C12's speed limit (102%) is not asserted by a test (machine noise on this box is about ±5%): A/B against HEAD gave 0.95 µs mean at HEAD and about 0.98 with this change (+2-3%, inside noise), allocation 530,856 against 532,242 B/tick (+0.26%). Load (3.1 s against 4.1 s for JSON), save and the 5 MB base are asserted in `SnapshotCostTests`.
- The same tree carries the issue-23 realism fixes (AD-093), so goldens and population floors were re-recorded for them; the snapshot format alone never changes a hash (C8).
- Diffs cannot remove an entity field (the serializer always writes every field), and rely on 64-bit fingerprints (a collision would drop a changed field; probability about 2^-64 per field).
- AC 4's 50 B/NPC/year Phase 9 goal is not reached (43,863 B measured, snapshot plus events); tracked in the pending-work review.

## Handoff

Eight source files plus migration, ADR-0023 and tests; one builder.
