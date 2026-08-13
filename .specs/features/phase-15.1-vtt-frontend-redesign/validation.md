# Phase 15.1 — E2.1 Bundle (T1 / T2 / T3 / T4) Validation

**Date**: 2026-08-12
**Spec**: `.specs/features/phase-15.1-vtt-frontend-redesign/spec.md`
**Diff range**: `cbca11a..b2f207c` (5 commits: `cbca11a` T1, `9b56137` T2, `bed254d` T3, `b2f207c` T4, plus `ba256a0` docs-only)
**Verifier**: independent sub-agent (author ≠ verifier)
**Scope**: this report covers only the E2.1 bundle — "Fundação engine-facing", Estágio 2 (T1-T4). It supersedes the previous validation.md content, which covered the unrelated E2.2 bundle (T20/T21/T30) on a different diff range; that content is not preserved here (see git history for it).

---

## Task Completion

| Task | Status  | Commit    | Notes |
| ---- | ------- | --------- | ----- |
| T1   | ✅ Done | `cbca11a` | `SimulationControlEndpoints.cs` (new), one `Map*` line in `Program.cs`, `/simulation` proxy entry in `web/vite.config.ts` — all in the same commit. |
| T2   | ✅ Done | `9b56137` | `ScopeTickDelta.cs`/`ScopeDeltaBuilder.cs` (new), pure diff function, no `WorldState` dependency. |
| T3   | ✅ Done | `bed254d` | `TickLoopService.cs` (new) + `Program.cs` registration + `RealtimeGateway.SubscribedScopeKeys` addition (deviation from "Where", documented in tasks.md and justified below). |
| T4   | ✅ Done | `b2f207c` | Retention window + subscriber-gated `Publish` + `_sequenceByScope` added to existing `RealtimeGateway.cs`. |

All four tasks are marked `[x]` in `tasks.md` with matching commit hashes; `git log --oneline cbca11a..b2f207c -- src tests` confirms exactly these 3 feature commits (T1 is the base of the range and is included by inspecting `cbca11a` directly).

---

## Spec-Anchored Acceptance Criteria (evidence-or-zero)

### T1 (tasks.md:294-301, VTT2-27..30)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| 5 routes respond; `speed <= 0` → 400, no `TicksPerSecond` change | `POST /simulation/pause\|resume\|speed\|step`, `GET /simulation/status` all wired; boundary at `<= 0` | `src/LivingWorld.Api/Simulation/SimulationControlEndpoints.cs:32-33` — `if (request.TicksPerSecond <= 0) return Results.BadRequest(...)`; `tests/.../SimulationControlEndpointsTests.cs:61-71` — posts `0.0` after setting `2.0`, asserts `HttpStatusCode.BadRequest` and `status.TicksPerSecond == 2.0` (unchanged) | ✅ PASS |
| `step` while running → 409 | `!host.IsPaused` gate | `SimulationControlEndpoints.cs:41-42` — `if (!host.IsPaused) return Results.Conflict(...)`; `SimulationControlEndpointsTests.cs:89-103` — resumes, then asserts `HttpStatusCode.Conflict` and clock unchanged (`Assert.Equal(before, world.CurrentDate.TotalHours)`) | ✅ PASS |
| N pause/resume/speed calls never change canonical hash | hash stability under control-plane calls | `SimulationControlEndpointsTests.cs:106-121` — 3 iterations of pause+resume+speed, `Assert.Equal(hashBefore, WorldSnapshot.CanonicalHash(world))` | ✅ PASS |
| `/simulation` in `vite.config.ts` proxy, same commit | recurring bug class (T1 note references `/worlds`/`/periods` prior omissions) | `git show cbca11a -- web/vite.config.ts` — `+ "/simulation": { target: "http://localhost:5289" },` present in the same commit as the endpoint file | ✅ PASS |
| Gate: 7 passed | `dotnet test --filter FullyQualifiedName~SimulationControl` | Verified independently — see Gate Check below | ✅ PASS |

### T2 (tasks.md:319-325, VTT2-11)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| Diff returns only changed-cell NPCs + removed ids | pure set diff over `Dictionary<NpcId, CellCoord>` | `src/LivingWorld.Api/Visual/ScopeDeltaBuilder.cs:12-19`; `tests/.../ScopeDeltaBuilderTests.cs:17-25` (`Diff_returns_only_npcs_that_changed_cell`) — `Assert.Equal([new NpcPositionDelta(Npc1, new CellCoord(1,0))], delta.Moved)` (Npc2 unchanged, correctly excluded); `:28-37` (`Diff_includes_ids_removed_from_the_scope`) — `Assert.Equal([Npc2], delta.Removed)` | ✅ PASS |
| Identical state → empty delta | no false positives on same-cell NPCs | `ScopeDeltaBuilderTests.cs:40-48` — `Assert.Empty(delta.Moved); Assert.Empty(delta.Removed)` with `after` a copy of `before` | ✅ PASS |
| No layer recomputation on the delta path — `Diff` never receives `WorldState` | structural boundary, checked by reflection | `ScopeDeltaBuilderTests.cs:71-78` (`Diff_never_receives_WorldState_or_any_layer_builder_type`) — `Assert.DoesNotContain(method.GetParameters(), p => p.ParameterType == typeof(WorldState))` against `ScopeDeltaBuilder.Diff`'s actual signature (`long, IReadOnlyDictionary<NpcId,CellCoord>, IReadOnlyDictionary<NpcId,CellCoord>`) | ✅ PASS |
| Gate: 6 passed | `dotnet test --filter FullyQualifiedName~ScopeDelta` | Verified independently | ✅ PASS |

### T3 (tasks.md:344-350, VTT2-26)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| Loop active + not paused → `CurrentDate.TotalHours` advances | `RunOneCycle` ticks exactly once | `src/LivingWorld.Api/Simulation/TickLoopService.cs:56-61` — `if (simulationHost.IsPaused) return;` then `worldHost.Clock.Tick(world)`; `tests/.../TickLoopServiceTests.cs:20-32` — `Assert.Equal(before + 1, ...)` | ✅ PASS |
| Paused → no tick | early return | `TickLoopServiceTests.cs:34-47` — `Assert.Equal(before, ...)` after `simulationHost.Pause()` | ✅ PASS |
| Publishes delta only to subscribed scopes | `gateway.SubscribedScopeKeys` iterated, not "every scope in the world" | `TickLoopService.cs:67-81` — loop is `foreach (var scopeKey in gateway.SubscribedScopeKeys)`; `TickLoopServiceTests.cs:50-69` — subscribes only to `worldScope`, asserts a delta was written there (`reader.TryRead` true, `IsType<ScopeTickDelta>`), then asserts an **unsubscribed** city scope's replay is empty (`Assert.Empty(replay.Value!)`) | ✅ PASS |
| Disabled by default in test env, gated by `TICK_LOOP_ENABLED` | no `IHostedService` auto-start absent the env var | `git show bed254d -- src/LivingWorld.Api/Program.cs` — `builder.Services.AddSingleton<TickLoopService>();` always registered (so tests can resolve it directly), but `if (builder.Configuration["TICK_LOOP_ENABLED"] == "true") builder.Services.AddHostedService(...)` gates the actual background start; test class doc comment confirms `TICK_LOOP_ENABLED` "continua ausente/false no processo de teste"; all 4 `TickLoopServiceTests` call `loop.RunOneCycle()` directly, never `StartAsync` | ✅ PASS |
| Code comment declares the tick-decision boundary | loop decides *when*, never *what* | `TickLoopService.cs:8-15` (XML doc) — explicitly states the loop "decide QUANDO chamar `WorldClock.Tick` — nunca O QUE o tick faz", cites `rules/simulation-determinism.md` | ✅ PASS |
| Gate: 4 passed | `dotnet test --filter FullyQualifiedName~TickLoop` | Verified independently | ✅ PASS |

### T4 (tasks.md:372-376, VTT2-26 operational)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| Log doesn't grow unboundedly after N publishes with a subscriber | bounded by `retentionPerScope` | `src/LivingWorld.Api/Realtime/RealtimeGateway.cs:83-84` — `if (entries.Count > retentionPerScope) entries.RemoveRange(0, entries.Count - retentionPerScope);`; `tests/.../RealtimeGatewayRetentionTests.cs:14-26` — 20 publishes, `retentionPerScope: 5`, `Assert.Equal(5, everything.Value!.Count)` | ✅ PASS |
| Replay of an active subscriber still returns everything not yet seen | correctness preserved despite truncation | `RealtimeGatewayRetentionTests.cs:29-44` — cursor at sequence 17 after 20 publishes/retention 5, `Assert.Equal(3, pending.Value!.Count)` (sequences 18,19,20) and `Assert.All(..., e => e.ToCursor.Sequence > 17)` | ✅ PASS, **with a caveat** — see gap note below |
| Scope without any subscriber never accumulates history | early-return before touching `_log` | `RealtimeGateway.cs:70-71` — `if (!_subscribers.TryGetValue(...) || channels.Count == 0) return;` placed before any `_log`/`_sequenceByScope` mutation; `RealtimeGatewayRetentionTests.cs:47-57` — 10 publishes with zero subscribers, `Assert.Empty(everything.Value!)` | ✅ PASS |
| `RealtimeGatewayEndpointTests` intact | pre-existing behavior unbroken | Not in this bundle's test files, but gate run below shows `RealtimeGateway` filter (10 passed) is a superset covering both new (3) and pre-existing tests | ✅ PASS (see Gate Check) |
| Gate: 10 passed | `dotnet test --filter FullyQualifiedName~RealtimeGateway` | Verified independently | ✅ PASS |

**Gap note (spec-precision, not a Done-when failure)**: tasks.md's T4 "What" describes the retention strategy as "descartando entradas abaixo do menor cursor de assinante ativo" (discarding entries below the lowest cursor of any active subscriber) — i.e., a *cursor-aware* prune. The actual implementation (`RealtimeGateway.cs:83-84`) is a simpler **fixed-size sliding window** per scope (last `retentionPerScope` entries, full stop), which does not track subscriber cursors at all. For a single subscriber that reads at least every `retentionPerScope` publishes, behavior is indistinguishable from the spec's stated design (which is exactly what all 3 new tests exercise). But if a subscriber goes silent for longer than `retentionPerScope` publishes, or if two subscribers to the same scope have very different read cadences, the fixed window can silently drop entries the slower subscriber hasn't replayed yet — the described "keep down to the slowest active cursor" guarantee is not actually implemented. This is a real behavioral gap between the task's own prose and the code, though every literal Done-when bullet as tested still passes (none of the 3 tests exercises multi-subscriber or long-silence scenarios). Recommend a follow-up task/lesson before this is relied upon with more than one concurrent viewer per scope, or before retention windows are tuned down from generous defaults.

---

## Discrimination Sensor (lightweight tier, 3 mutations, scratch-only)

All mutations applied to the working tree only, verified via `git status`/`git diff` clean before and after, reverted with `git checkout --`.

| # | File | Mutation | Filtered test run | Result |
| - | ---- | -------- | ------------------ | ------ |
| 1 | `SimulationControlEndpoints.cs` | `<= 0` → `< 0` (speed validation boundary) | `Speed_with_a_non_positive_value_returns_400_and_does_not_change_ticks_per_second` failed (500 Internal Server Error instead of 400 — `SimulationHost.SetSpeed` throws on `0`) | ✅ Killed |
| 2 | `RealtimeGateway.cs` | Retention truncation condition effectively disabled (`entries.Count > retentionPerScope` → `entries.Count > retentionPerScope * 1000`, i.e. never triggers at test scale) | `Log_does_not_grow_past_the_retention_window_after_many_publishes` failed (log had 20 entries, expected 5) | ✅ Killed |
| 3 | `ScopeDeltaBuilder.cs` | Dropped the moved-cell case: `!before.TryGetValue(id, out var previousLocation) \|\| previousLocation != location` → `!before.TryGetValue(id, out var previousLocation)` | `Diff_returns_only_npcs_that_changed_cell` failed (Npc1's cell change from `(0,0)`→`(1,0)` was no longer reported as moved) | ✅ Killed |

Sensor result: **3/3 mutants killed.** `git status`/`git diff` confirmed clean before mutation 1, clean after revert of mutation 3 (final check), and the repo returned to `cbca11a..b2f207c` HEAD state with no residual changes.

---

## Code Quality Check

| Aspect | Finding |
| --- | --- |
| Touched files vs. "Where" | T1, T2 match exactly. T4 matches exactly (`RealtimeGateway.cs` only, as listed). T3 touched `RealtimeGateway.cs` (adding `SubscribedScopeKeys`) in addition to its listed `TickLoopService.cs` + `Program.cs` — **this deviation is explicitly called out in tasks.md's own "Nota"** under T3: "`RealtimeGateway` ganhou `SubscribedScopeKeys` (gap real — sem ele o loop não tem como saber quais escopos publicar, e o `Where` desta task não listava esse arquivo)." This is a reasonable, minimal, additive-only change (one new read-only property, no behavior change to existing members) and is honestly documented rather than silently smuggled in. Acceptable. |
| T4 also changed `Snapshot()`'s sequence computation | `Snapshot()` (`RealtimeGateway.cs:37`) switched from `_log.TryGetValue(...).Count` to `_sequenceByScope.GetValueOrDefault(...)`. This is a necessary consequence of the retention fix, not scope creep: once `_log` is truncated, `entries.Count` is no longer a valid proxy for the total historical sequence count, so `Snapshot()`'s cursor would silently regress/repeat sequence numbers after the first truncation without this change. Correctly identified and fixed together. |
| Minimum code / no scope creep | All 4 diffs are small (51-176 LOC incl. tests) and additive. No unrequested abstractions (no interfaces, no factories, no config knobs beyond the existing `retentionPerScope`/`TICK_LOOP_ENABLED` pattern already used elsewhere in the codebase, e.g. `WorldHost`). |
| Matches existing patterns | `SimulationControlEndpoints.cs` mirrors `WorldStartEndpoints.cs`'s thin `MapPost`/lambda style (verified by reading both). Test class doc comments and `IClassFixture<WebApplicationFactory<Program>>` usage match `WorldCreateEndpointsTests.cs`/`VisualGateTests.cs` conventions already in the repo. |
| Speculative tests | None found — every test in the 4 new test files traces to a specific Done-when bullet (see AC tables above); no extra assertions unrelated to the task's stated criteria. |

---

## Gate Check

```
dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~SimulationControl|FullyQualifiedName~ScopeDelta|FullyQualifiedName~TickLoop|FullyQualifiedName~RealtimeGateway"
```

Result: **Passed! Failed: 0, Passed: 27, Skipped: 0, Total: 27** (7 SimulationControl + 6 ScopeDelta + 4 TickLoop + 10 RealtimeGateway — matches the per-task counts in tasks.md exactly). Full `verify.sh`/`Category=Scenario` suite intentionally **not** run per this project's test-gate-cadence rule (reserved for phase closure).

---

## Fix Plans / Lessons

1. **Spec-precision gap (T4, non-blocking)**: tasks.md's stated retention design ("discard below the lowest active subscriber cursor") is not what's implemented (fixed-size sliding window per scope). Every literal Done-when bullet passes because the tests only exercise single-subscriber scenarios within the retention window. Suggest either (a) updating tasks.md's "What" wording to match the simpler, actually-shipped design ("keep the last `retentionPerScope` entries per scope, all-or-nothing across scope subscribers"), or (b) filing a follow-up task if true per-subscriber-cursor-aware pruning is needed once multiple concurrent viewers per scope with divergent read cadences becomes a real usage pattern. Low priority at this stage (Estágio 2, engine-facing only, no real frontend clients yet).
2. No surviving mutants; no SPEC_DEVIATION beyond what tasks.md itself already documents (T3's `SubscribedScopeKeys` addition).

---

## Requirement Traceability

| Requirement | Task(s) | Covered by |
| --- | --- | --- |
| VTT2-27, VTT2-28, VTT2-29, VTT2-30 | T1 | `SimulationControlEndpointsTests.cs` (7 tests) |
| VTT2-11 | T2 | `ScopeDeltaBuilderTests.cs` (6 tests) |
| VTT2-26 | T3 | `TickLoopServiceTests.cs` (4 tests) |
| VTT2-26 (operational viability) | T4 | `RealtimeGatewayRetentionTests.cs` (3 new) + pre-existing `RealtimeGateway*` tests (7, confirmed intact within the same 10-test filtered run) |

---

## Summary

**Verdict: PASS.** All 4 tasks (T1-T4) are Done, commits match tasks.md, every Done-when bullet across all four tasks has a concrete `file:line` assertion whose asserted value matches the spec-defined outcome (16/16 spec-anchored checks pass). The discrimination sensor killed all 3 injected mutants (speed-validation boundary, retention truncation, moved-cell diff condition), and the repo was left clean before/after. Gate run independently reproduced 27/27 passing, matching tasks.md's recorded counts. Code quality is clean: touched files match "Where" almost exactly, with T3's one listed deviation (`RealtimeGateway.SubscribedScopeKeys`) self-documented in tasks.md and judged reasonable, and T4's incidental `Snapshot()` fix judged a necessary consequence rather than scope creep. One non-blocking spec-precision gap is recorded above (T4's retention strategy is simpler than its own prose describes) but does not fail any stated acceptance criterion at this stage of the project.
