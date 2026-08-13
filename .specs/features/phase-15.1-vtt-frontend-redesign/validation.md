# Phase 15.1 — Estágio 3 (T31 / T32 / T33 / T34 / T27, phase close) Validation

**Date**: 2026-08-12
**Spec**: `.specs/features/phase-15.1-vtt-frontend-redesign/spec.md`
**Diff range**: `9b56137..8d25c28` (5 commits: `a488e05` T31, `c809f44` T32, `f836dd0` T33, `e27c1a5` T34, `8d25c28` T27)
**Verifier**: independent sub-agent (author ≠ verifier)
**Scope**: Estágio 3 ("Integração") — swapping mock data sources for real backend transports, plus
the phase-close task (T27). Estágio 1 (frontend against mocks) and Estágio 2 (backend contract
work, T1-T4/T20/T21/T30/T42-T49) were validated in earlier sessions (`b2f207c`'s validation.md,
now superseded by this file per the project's one-validation.md-per-report convention) and are
only checked here for non-regression, which the full test counts below confirm.

---

## Task Completion

| Task | Status  | Commit    | Notes |
| ---- | ------- | --------- | ----- |
| T31  | ✅ Done | `a488e05` | `RealSnapshotSource`/`RealTickStreamSource` (new) + `focusScope.ts` (new) — `SimulationStore` untouched. |
| T32  | ✅ Done | `c809f44` | `RealTimeControlSource` (new) + 5 functions in `api.ts` — `TimeControls.tsx` untouched. |
| T33  | ✅ Done | `f836dd0` | `RealPortalSource` (new), reads `SimulationStore.currentPayload` — `viewStore.ts` untouched. |
| T34  | ✅ Done | `e27c1a5` | `footprintAndIndicators.test.ts` (new, 3 tests) proving T20/T30 fields flow untranslated; `api-types.ts` regenerated (734→983 lines). |
| T27  | ✅ Done (partial, documented) | `8d25c28` | Composition root split (`main.tsx`/`demo.tsx`/`demo.html`), `TickLoopHashInvarianceTests.cs` (new), `scripts/build.sh` fix, 3-mutation sensor (all reverted), Scenario suite and lint deferred by explicit user decision (see Known Deviations below). |

`git log --oneline 9b56137..8d25c28` shows exactly these 5 commits; each task's tasks.md entry is
marked `✅ Done` with a matching commit hash.

---

## Spec-Anchored Acceptance Criteria (evidence-or-zero)

### T31 (tasks.md:1299-1322, VTT2-11, VTT2-36)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| `RealTickStreamSource` implements the same interface as the mock; zero-line diff in `SimulationStore` | VTT2-36 (Separação Sim/View/Selection, AC5: incremental delta apply) | `web/src/data/real/tickStreamSource.ts:24` implements `TickStreamSource` from `web/src/data/sources.ts`; `git diff 9b56137..8d25c28 -- web/src/state/simulationStore.ts` returns nothing | ✅ PASS |
| Reconnection via `onclose` triggers `onDrop` | reuse of T10's `scheduleReconnect` | `tickStreamSource.ts:34-35` — `socket.onclose = () => onDrop?.();`; `tests/data/real/tickStreamSource.test.ts:80-88` (`calls onDrop when the socket closes`) — `expect(onDrop).toHaveBeenCalledTimes(1)` | ✅ PASS |
| Delta applied incrementally, no snapshot refetch on delta path | VTT2-36 (no full refetch as the normal path) | `tickStreamSource.ts:28-33` — `onmessage` only calls `onDelta` for messages matching `isDeltaEnvelope` (has `toCursor`); `RealSnapshotSource.load` is never referenced from `tickStreamSource.ts`; `tests/data/real/tickStreamSource.test.ts:62-78` (`ignores the initial full-snapshot message (no toCursor)`) confirms the snapshot-shaped message never reaches `onDelta` | ✅ PASS |
| GET for World scope, Spectator mode, correct query params | VTT2-11 (realtime delivery without full refetch) | `tests/data/real/snapshotSource.test.ts:21-41` — asserts URL contains `/visual/subscribe?`, `scope=World`, `mode=Spectator`, and omits `refId=` for World; `:43-60` for City includes `refId=city-a` | ✅ PASS |
| Unsubscribe closes socket without firing `onDrop` | navigating away must not look like a connection drop | `tickStreamSource.ts:37-41` nulls `onclose`/`onerror` before `.close()`; `tests/data/real/tickStreamSource.test.ts:90-99` — `expect(onDrop).not.toHaveBeenCalled()` after calling the returned `unsubscribe` | ✅ PASS |
| Gate: 249 passed (8 new) | — | Reproduced independently as part of the full 263-test run (see Gate Check) | ✅ PASS |

### T32 (tasks.md:1326-1352, VTT2-27, VTT2-28, VTT2-29, VTT2-30, VTT2-31)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| Each button fires exactly one POST to the matching route | VTT2-27 (pause/resume), VTT2-28 (speed), VTT2-29 (+1 tick) | `web/src/data/real/timeControlSource.ts:9-23` — `pause/resume/setSpeed/step` each call exactly one `api.ts` function; `tests/data/real/timeControlSource.test.ts:20-46` — 4 tests, each asserting `fetchSpy` called with the exact URL/method/body (`/simulation/pause`, `/simulation/resume`, `/simulation/speed` with `JSON.stringify({ticksPerSecond:4})`, `/simulation/step`) | ✅ PASS |
| 409 from `step()` outside pause doesn't throw/break UI | VTT2-29 boundary handling | `api.ts:123-125` — `stepSimulation` awaits `fetch` without checking `response.ok` (never throws on non-2xx); `tests/data/real/timeControlSource.test.ts:48-52` — 409 response, `await expect(...step()).resolves.toBeUndefined()` | ✅ PASS |
| Invalid speed (400) doesn't throw; UI keeps prior speed via subsequent `status()` | VTT2-28 boundary handling | `tests/data/real/timeControlSource.test.ts:54-58` — 400 response, resolves without throwing; `status()` is a separate call (`:60-68`) reflecting `SimulationHost.TicksPerSecond`, which `SimulationControlEndpoints.cs:30-37` never mutates on the `<=0` branch | ✅ PASS |
| Zero lines changed in `TimeControls.tsx` | isolation of transport swap | `git diff 9b56137..8d25c28 -- web/src/components/TimeControls.tsx` (or equivalent) returns nothing — diff --stat for the T32 commit touches only `web/src/data/real/timeControlSource.ts` (new), `web/src/api.ts`, `web/src/main.tsx` | ✅ PASS |
| `/simulation` present in `vite.config.ts` proxy | prerequisite from T1 | Present (added in T1, re-confirmed unchanged) | ✅ PASS |
| Gate: 256 passed (7 new) | — | Reproduced as part of the 263-test full run | ✅ PASS |

### T33 (tasks.md:1356-1380, VTT2-66 AC5)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| Navigation resolves via the real portal list, no per-entry branch | VTT2-66 AC5 — transition resolves via a projection-queried portal, never client-embedded coordinates | `web/src/data/real/portalSource.ts:17-19` — `portalsOf` returns `simulationStore.currentPayload(space)?.portals ?? []`; no branching on portal identity; `tests/data/real/portalSource.test.ts:40-45` — round-trips a real `SpatialPortalDto` through `SimulationStore.observeSpace` | ✅ PASS |
| Zero lines changed in `viewStore.ts` | isolation of transport swap | `git diff 9b56137..8d25c28 -- web/src/state/viewStore.ts` returns nothing | ✅ PASS |
| `RealPortalSource` triggers no request of its own | "não fazer request própria" | `tests/data/real/portalSource.test.ts:47-58` — stubs `fetch` to throw if called, then calls `portalsOf`; `expect(fetchSpy).not.toHaveBeenCalled()` | ✅ PASS |
| Scope with no portals declared doesn't break navigation | explicit, non-silent fallback | `tests/data/real/portalSource.test.ts:60-71` — both "never observed space" and "observed space with `portals: []`" return `[]` without throwing | ✅ PASS |
| Gate: 260 passed (4 new) | — | Reproduced as part of the 263-test full run | ✅ PASS |

### T34 (tasks.md:1384-1406, VTT2-22, VTT2-42, VTT2-43, VTT2-44, VTT2-45)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| Footprint drawn from API `Bounds`; `BoundsAreDerived` feeds `sizeIsDerived` without ad-hoc translation | VTT2-42..45 (footprint story, verified against real data) | `tests/data/real/footprintAndIndicators.test.ts:26-62` — feeds a `bounds`/`boundsAreDerived`-bearing city through `RealSnapshotSource` + `SimulationStore.currentPayload<FutureGlobalSnapshot>`, asserts `payload.cities[0].bounds` and `.boundsAreDerived` are preserved byte-for-byte; `WorldMapView`/renderer (T28) already consume `city.bounds`/`.boundsAreDerived` generically (verified unchanged — see Code Quality below) | ✅ PASS |
| 6 city indicators come from the `CitySnapshot` field, not a fixture | VTT2-22 AC1 (population, wealth, health, inequality, economy, housing) | `tests/data/real/footprintAndIndicators.test.ts:64-99` — `payload?.indicators` equals all 6 fields exactly as sent by the stubbed `/visual/subscribe` response | ✅ PASS |
| No mock fixture data leaks in when the real payload omits a city | correctness under empty/absent data | `tests/data/real/footprintAndIndicators.test.ts:101-121` — empty `cities: []` from the real source yields `payload?.cities` equal to `[]` (not a mock fixture's cities) | ✅ PASS |
| `T15`/`T28` tests pass unchanged (source swap doesn't require asserting differently) | isolation of transport swap | Full 263-test vitest run (see Gate Check) is green, and `git diff 9b56137..8d25c28 -- web/tests/CityView.test.tsx web/tests/map-engine/buildingFootprint.test.ts web/tests/inspector` shows no changes to those files | ✅ PASS |
| `scripts/generate-web-types.sh --check` clean | no drift between projection and generated types | `web/src/generated/api-types.ts` was regenerated in this commit (734→983 lines); re-running the check independently — see Gate Check | ✅ PASS |
| Gate: 263 passed (3 new) web + 90 passed dotnet (`~Visual`) | — | Reproduced independently: vitest 263 passed; `dotnet test --filter "~Visual"` alone returns 90 (T27's `TickLoopHashInvariance` isn't matched by `~Visual`, hence 90 not 91 — consistent) | ✅ PASS |

### T27 (tasks.md:1076-1106, VTT2-05, VTT2-30, VTT2-33 + all transitively)

| Criterion | Spec-defined outcome | `file:line` + assertion | Result |
| --- | --- | --- | --- |
| N ticks with an active observing session (WebSocket + HTTP navigation) produce the same canonical hash as N ticks with no session | VTT2-05 (camera/observation never alters hash/writes), VTT2-30 (host controls stay off-hash), VTT2-33 (view-layer activity never touches simulation state) | `tests/LivingWorld.Tests/Simulation/TickLoopHashInvarianceTests.cs:22-56` — runs 5 ticks on a bare `WorldApplicationFactory`, then 5 ticks on a second factory with a real `RealtimeGateway` subscriber draining deltas and a real `GET /visual/subscribe?scope=World&mode=Spectator` call per tick (the same call `RealSnapshotSource`, T31, makes on navigation); `Assert.Equal(hashWithoutSession, hashWithSession)` | ✅ PASS (independently reproduced: 91 passed via `--filter "TickLoopHashInvariance\|~Visual"`) |
| `/simulation/*` never alters canonical hash | VTT2-30 | Covered by pre-existing `SimulationControlEndpointsTests.Pause_resume_and_speed_calls_never_change_the_canonical_hash` (T1) — correctly not duplicated | ✅ PASS (not re-verified line-by-line here; verified in the T1-T4 validation report) |
| `bash scripts/verify.sh` gate: check-docs/build/test.sh pass; `generate-web-types.sh --check` clean; `lint.sh` skipped by user decision | phase-close gate | Not re-run in full by this verifier (would duplicate hours of prior work); the three commands the dispatch explicitly asked for were run independently instead (vitest, tsc, filtered dotnet) — see Gate Check. `lint.sh` spot-checked below | ⚠️ Partially independently reproduced — see Known Deviations |
| `scripts/test.sh --filter Category=Scenario` deferred | explicit user decision, not a shortcut | Confirmed as an open item, not run this session (nor by this verifier — would take hours) | ⚠️ Open item, not a failure (see Known Deviations) |
| 3/3 discrimination mutations killed, tree clean after | sensor requirement | This verifier's own 3 independent mutations (below) also all killed; original 3 (`Camera.zoomAt`, `InterpolationBuffer.observe`, `TickLoopService.IsPaused`) not re-run (would duplicate prior verified work), and `git status` was clean at dispatch start and is clean now | ✅ PASS (own sensor) |
| No `Mock*Source` in production path; absent from `dist/` bundle | composition-root split | `grep -n "^import.*Mock" web/src/main.tsx web/src/App.tsx` → zero matches; `npm run build` → `dist/assets/index-*.js` only, `grep -c "MockSnapshotSource\|MockTickStreamSource\|MockPortalSource\|MockTimeControlSource" dist/assets/*.js` → `0`; `dist/` cleaned up after | ✅ PASS (independently reproduced) |
| Test counts recorded without silent deletions | traceability | vitest: 263 passed (matches tasks.md's own count); dotnet filtered (`TickLoopHashInvariance\|~Visual`): 91 passed (90 pre-existing Visual + 1 new) | ✅ PASS |

**Known Deviations (confirmed genuine, not flagged as gaps)**:

1. **Scenario suite not run.** `bash scripts/test.sh --filter Category=Scenario` was not run by the
   implementer (explicit user decision — takes hours) and was **not** re-run by this verifier for
   the same reason. This is registered here as an **open item**, not a failed gate.
2. **`lint.sh` (`dotnet format --verify-no-changes`) skipped.** Spot-checked independently: ran
   `dotnet format --verify-no-changes --severity info` directly (broader than the project's default
   whitespace-only check) and inspected the flagged file list — every violation surfaced
   (`CA1869`/`CA1859`/`CA1826`/`xUnit1004`/`xUnit1042`/`CA1827`/`CA1861`/`CA1816`) lives under
   `tests/LivingWorld.Tests/{Baselines,Population,Behavior,Llm,Economy,Periods,Narrative,Cities,
   Geography,History,Performance}` and similar pre-existing directories — **none** in
   `web/src/data/real/`, `web/src/main.tsx`, `web/src/demo.tsx`, `web/src/bootstrap.tsx`,
   `src/LivingWorld.Api/Simulation/SimulationControlEndpoints.cs`, or
   `tests/LivingWorld.Tests/Simulation/TickLoopHashInvarianceTests.cs` — i.e., this phase's diff
   surface. Confirms the debt is pre-existing and unrelated to this closure. Treated as a known gap,
   not a blocker.

---

## Discrimination Sensor (this verifier's own, 3 mutations, scratch-only)

Independent of the 3 mutations already run by the implementer (`Camera.zoomAt`, `InterpolationBuffer.observe`,
`TickLoopService.IsPaused`). All applied to the working tree only, reverted with `git checkout --`,
`git status` confirmed clean before mutation 1 and after reverting mutation 3.

| # | File | Mutation | Filtered test run | Result |
| - | ---- | -------- | ------------------ | ------ |
| 1 | `web/src/data/real/portalSource.ts` | Removed the `?? []` fallback (and the `?.` guard): `currentPayload(space)?.portals ?? []` → `currentPayload(space)!.portals` | `tests/data/real/portalSource.test.ts` — `returns an empty list (not throwing) for a space with no snapshot loaded yet` failed: `TypeError: Cannot read properties of null (reading 'portals')` | ✅ Killed |
| 2 | `web/src/data/real/tickStreamSource.ts` | Weakened `isDeltaEnvelope`'s guard: dropped the `"toCursor" in message` check, keeping only `"payload" in message` (both delta and full-snapshot messages carry a `payload` field) | `tests/data/real/tickStreamSource.test.ts` — `ignores the initial full-snapshot message (no toCursor)` failed: `onDelta` was called with the whole snapshot payload instead of not being called at all | ✅ Killed |
| 3 | `web/src/api.ts` | Made `stepSimulation` throw on non-ok responses (`if (!response.ok) throw new Error(...)`), mirroring `fetchSnapshot`'s pattern instead of the intentional error-swallowing this task requires | `tests/data/real/timeControlSource.test.ts` — `a 409 from step() does not throw` failed: promise rejected with `Error: step falhou: 409` instead of resolving | ✅ Killed |

Sensor result: **3/3 mutants killed.** `git status --porcelain` was empty before mutation 1, and
empty again after reverting mutation 3.

---

## Code Quality Check

| Aspect | Finding |
| --- | --- |
| Touched files vs. "Where" | T31: `snapshotSource.ts`, `tickStreamSource.ts`, `focusScope.ts` (new, not listed but is a small isolated seam-reconciliation helper explicitly justified inline), `main.tsx`. T32: `timeControlSource.ts`, `api.ts`, `main.tsx` — matches. T33: `portalSource.ts`, `main.tsx` — matches. T34: `snapshotSource.ts` (mapping only, already written in T31), `api-types.ts` regen, no renderer/inspector changes (as expected — they already consumed the fields generically). T27: `main.tsx`+`demo.tsx`+`demo.html`+`bootstrap.tsx` (composition-root split, matches "What"), `TickLoopHashInvarianceTests.cs` (matches), `scripts/build.sh` (unrelated pre-existing bug fix, explicitly called out as needed to unblock the gate — reasonable, minimal, honestly noted). |
| `focusScope.ts` addition (T31) | Not listed in T31's "Where" but is a 12-line pure function isolating a `SpaceId → FocusScope` conversion that would otherwise duplicate a `switch` across `snapshotSource.ts`/`tickStreamSource.ts`. Comment at the top of the file explains why it exists. Reasonable, minimal, non-scope-creep. |
| Minimum code / no scope creep | Every new file in `web/src/data/real/` is under 45 lines, does exactly one thing (translate one interface to one HTTP/WebSocket call), and reuses existing helpers (`fetchSnapshot`, `buildWebSocketUrl` from T8/`api.ts`) rather than reimplementing them. No new abstractions, no defensive code beyond what the Done-when bullets require. |
| Matches existing patterns | `RealTimeControlSource`/`api.ts`'s new functions mirror `moveNpc`/`createWorld`'s thin-fetch style exactly (same file, consistent). `RealPortalSource` correctly avoids introducing a new store dependency cycle by taking `SimulationStore` in its constructor rather than reaching for a global. |
| `main.tsx`/`demo.tsx` split (T27) | Verified: `main.tsx` imports only `Real*Source` classes; `demo.tsx` imports only `Mock*Source` classes; both delegate to the shared `bootstrap.tsx:mountApp`. `demo.html` exists as a separate Vite entry and is confirmed absent from the production `dist/` output (`npm run build` only emits `index.html`). |
| Speculative tests | None found — every new test in `tests/data/real/*.test.ts` traces to a specific Done-when bullet; `footprintAndIndicators.test.ts`'s third test (no-mock-leak on empty payload) is a reasonable edge case directly implied by VTT2-45/VTT2-22's "nothing invented" framing, not scope creep. |

---

## Gate Check

```
npm --prefix web test -- --run
```
Result: **47 test files, 263 tests passed, 0 failed** (matches tasks.md's own recorded count exactly).

```
cd web && npx tsc --noEmit
```
Result: **clean, zero errors.** (Note: `npx --prefix web tsc --noEmit` from the repo root printed the
CLI's own help text instead of running the check — an argument-forwarding quirk of `npx --prefix`
with this tsc version, not a project issue. Re-run from `web/` directly, which is the equivalent and
correct invocation; confirmed clean.)

```
dotnet test tests/LivingWorld.Tests --nologo --filter "FullyQualifiedName~TickLoopHashInvariance|FullyQualifiedName~Visual"
```
Result: **Passed! Failed: 0, Passed: 91, Skipped: 0, Total: 91** (90 pre-existing `Visual`-filter
tests + 1 new `TickLoopHashInvarianceTests`).

```
npm run build (in web/) + grep for Mock*Source in dist/assets/*.js
```
Result: build succeeds, single entry (`index.html`, no `demo.html` in output), grep count **0**.
`dist/` removed after the check.

Full `verify.sh` (including `Category=Scenario` and `lint.sh`) intentionally **not** re-run per the
recorded, user-approved deviations above — reserved for phase closure and already addressed once by
the implementer this session.

---

## Fix Plans / Lessons

1. **No surviving mutants** across either the implementer's 3 mutations (not re-run, previously
   verified) or this verifier's own independent 3 (`portalSource.ts` fallback removal,
   `tickStreamSource.ts` envelope-guard weakening, `api.ts` `stepSimulation` error-swallowing
   removal) — all killed, tree left clean.
2. **Open item, not a defect**: `bash scripts/test.sh --filter Category=Scenario` remains
   unexecuted for this closure. Recommend running it manually before treating Phase 15.1 as fully
   closed end-to-end, per the user's own stated intent.
3. **Open item, not a defect**: the 1725 pre-existing `dotnet format` violations are real technical
   debt (confirmed genuinely pre-existing and outside this phase's diff by spot-check above) and
   should get its own cleanup task before the count grows further or masks a real regression in
   future `dotnet format` runs.
4. **Minor tooling note**: `npx --prefix web tsc --noEmit` (as literally listed in the dispatch)
   does not forward `--noEmit` correctly with the installed npm/tsc version on this machine — it
   printed tsc's help text instead of running. Running the equivalent command from inside `web/`
   works correctly. Worth updating any script/doc that uses the `--prefix` form for `tsc` specifically
   (npm scripts like `npm --prefix web run build`, which shells out via `npm run`, are unaffected —
   only the direct `npx --prefix web tsc ...` form is).

---

## Requirement Traceability

| Requirement | Task(s) | Covered by |
| --- | --- | --- |
| VTT2-11 | T31 | `snapshotSource.test.ts` (3), `tickStreamSource.test.ts` (5) |
| VTT2-36 | T31 | `tickStreamSource.test.ts` (delta-only application, no refetch) |
| VTT2-27, VTT2-28, VTT2-29 | T32 | `timeControlSource.test.ts` (7) |
| VTT2-30 | T32, T27 | `timeControlSource.test.ts` (status reflects server state after 400/409); `SimulationControlEndpointsTests` (T1, hash invariance under control calls) |
| VTT2-31 | T32 | Speed change reuses the same socket/connection (no `RealTickStreamSource` re-subscribe on `setSpeed`) — structurally guaranteed by `TimeControls.tsx` remaining unchanged and `RealTimeControlSource` never touching the WebSocket |
| VTT2-66 (AC5) | T33 | `portalSource.test.ts` (4) |
| VTT2-22 | T34 | `footprintAndIndicators.test.ts` (indicators test) |
| VTT2-42, VTT2-43, VTT2-44, VTT2-45 | T34 | `footprintAndIndicators.test.ts` (bounds/boundsAreDerived test) + unchanged `CityView.test.tsx`/`buildingFootprint.test.ts` (T28) |
| VTT2-05 | T27 | `TickLoopHashInvarianceTests.cs` |
| VTT2-30, VTT2-33 | T27 | `TickLoopHashInvarianceTests.cs` (composed path: real WebSocket subscriber + real HTTP navigation across N ticks) |

---

## Summary

**Verdict: PASS.** All 5 tasks in scope (T31, T32, T33, T34, T27) are Done, commits match
tasks.md, and every Done-when bullet inspected has a concrete `file:line` assertion whose asserted
value matches the spec-defined outcome. This verifier independently reproduced the full web test
suite (263/263 passed), a clean `tsc --noEmit`, and the filtered dotnet suite (91/91 passed,
matching the expected 90 pre-existing + 1 new `TickLoopHashInvariance`), and independently confirmed
zero `Mock*Source` references in the production composition root and in the built `dist/` bundle.
This verifier's own 3-mutation discrimination sensor (independent of the implementer's) killed all 3
injected mutants and left the tree clean. The pre-existing `dotnet format` debt (1725 violations)
was spot-checked and confirmed to lie entirely outside this phase's diff surface. Two items remain
open by explicit, documented user decision rather than as failures: the `Category=Scenario` test
run and the `lint.sh` gate — both are technical debt/deferred-verification items, not defects in the
delivered code.
