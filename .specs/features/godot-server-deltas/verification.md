# godot-server-deltas verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 9f8ace6..94b56e0 (fix 2d9987d..94b56e0)
**Round**: 2 - scoped
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

Carried from 2d9987d: profile `light`, step 1 does not run; no binding source.

## Checks

All proofs re-run at 94b56e0: integration batch (InteriorDeltaTests, SimulationControlEndpointsTests, ApiContractTests) 26 passed, 0 failed, exit 0; `bash scripts/client-test.sh` 56 passed, 0 failed, exit 0, each named test `ok`. Rows the fix did not touch keep their round-1 evidence and say so.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | entra vira upsert, sai vira removed | batch exit 0 at 94b56e0 | carried from 2d9987d - `InteriorDeltaTests.cs:50`, `:58` | PASS |
| C2 | troca de ação vira upsert; sem mudança, vazio | batch exit 0 at 94b56e0 | carried from 2d9987d - `InteriorDeltaTests.cs:79-80`, `:86` | PASS |
| C3 | prédio inexistente não publica, laço segue | batch exit 0 at 94b56e0 | carried from 2d9987d - `InteriorDeltaTests.cs:102-103` | PASS |
| C4 | sequence seguinte e replay | batch exit 0 at 94b56e0 | carried from 2d9987d - `InteriorDeltaTests.cs:121`, `:125` | PASS |
| C5 | status traz o calendário do mundo | batch exit 0 at 94b56e0 | verified at 94b56e0 - `SimulationControlEndpointsTests.cs:209` world built with `WorldCalendar(12, 10, 4)` via the new optional param; `:215-217` assert 12/10/4 over HTTP; `ResetCanonicalWorld()` in finally. Hard-coded-24 mutant now killed | PASS |
| C6 | 3 shapes existem e batem | batch exit 0 at 94b56e0 | verified at 94b56e0 - `ApiContractTests.cs:102-103`; samples now carry null action/city/location so `shape.interior-delta.json:16` and `shape.visual-delta.json:58`, `:86-87` record `"null"` | PASS |
| C7 | cliente aplica delta de interior sem reler snapshot | client batch `ok` | carried from 2d9987d - `test_app.gd:86`, `:90`, `:93-94` | PASS |
| C8 | hoursPerDay 12 dirige hora e data | client batch `ok` | carried from 2d9987d - `test_ui_modules.gd:470-478` | PASS |
| C9 | ApiClient valida npc-detail e status; stream descarta delta fora do contrato, interior incluso | client batch `ok` (4 named tests) | carried from 2d9987d for the first 3; new `test_shape_validator.gd:154` null action, `:158` accepted, `:165-166` interior delta without `removed` discarded and announced, with `validate_shapes = true` | PASS |
| C10 | frame WS com upserts e toCursor | batch exit 0 at 94b56e0 | verified at 94b56e0 - `InteriorDeltaTests.cs:137` 10 s CancellationTokenSource used by both receives (`:139`, `:144`); skip-PublishInterior mutant now fails in 11 s | PASS |
| C11 | reassinar um interior recebe todos os ocupantes no primeiro delta | batch exit 0 at 94b56e0 | verified at 94b56e0 - `InteriorDeltaTests.cs:168` unsubscribe, publish with no subscriber, resubscribe, `:177` `Assert.Single(delta.Upserts)`; implementation `TickLoopService.cs:85` | PASS |

## Plan conformance

- Carried from 2d9987d: landings 1 and 2, statuses 200/101/403, performance (`PublishInterior` only for subscribed keys).
- `ScenarioRunner.Create` gains `WorldCalendar? calendar = null` as the last optional param (`ScenarioRunner.cs:294`), used as `calendar ?? DefaultCalendar` (`:303`): with no argument the world is built exactly as before. Confirmed by reading and by the full integration project (350 passed); no hash suite run this round (unit/LongRunning carry known arm64 pin failures).
- `.gitignore:19` adds `client/godot/**/*.uid`. After `client-test.sh` in the real tree `git status --porcelain` was empty (round-1 harness gap 7 closed, `project.godot` no longer rewritten).
- `TickLoopService.cs:86` also evicts `_lastStates` for World/City scopes with no subscriber - a behaviour change on the existing World stream that no check claims explicitly and no proof exercises (see Faults).

## Faults injected

Scratch worktree (`git worktree add --detach` at 94b56e0), reverted after each mutant, removed at the end; real-tree porcelain empty before and after.

| Mutation | Location | Killed |
| --- | --- | --- |
| Status `HoursPerDay` hard-coded `24` | `SimulationControlEndpoints.cs:67` | yes - C5 second proof: Expected 12, Actual 24 |
| Remove both stale-cleanup lines | `TickLoopService.cs:85-86` | yes - C11: `Assert.Single() Failure: The collection was empty` |
| Skip `PublishInterior` | `TickLoopService.cs:92` | yes - C10 fails by `OperationCanceledException` after 11 s, no hang |
| Interior shape `action` back to `"number"` | `shape.interior-delta.json:16` | yes - `test_stream_validates_interior_deltas_and_accepts_null_action` FAIL |
| Remove only the `_lastStates` cleanup | `TickLoopService.cs:86` | no - survived: InteriorDeltaTests plus Realtime and ScopeDelta tests 13/13 pass |
| Carried from 2d9987d: projector action diff, store ignores removed, day_cycle ignores hoursPerDay | see round 1 | yes |

## Gaps

1. (blocking) Surviving mutant on a surface the fix created: the World/City `_lastStates` eviction (`TickLoopService.cs:86`) is unproven, while C11's claim says "o servidor esquece o estado de escopos sem assinante" (all scopes) but its proof exercises an interior only. Either add a World-scope resubscribe assertion to C11 (or a C12), or narrow the claim to interiors and drop the `_lastStates` line.
2. `"null"` in a recorded shape is a wildcard on both sides (`shape_validator.gd:64`, `ApiContractTests.cs:116`). Action, city, event location and the other null-recorded fields are no longer type-checked: a server switching `currentAction` to a string enum would pass the server contract test and the client validator. Disclosed in checks.md Coverage notes; accepted trade-off, not a defect.
3. C11 holds only when a publish happens while no one is subscribed; an unsubscribe and resubscribe inside one tick still diffs against the old state. Harmless because the resubscribe GET returns a fresh snapshot, but the claim states it unconditionally.
4. Carried from 2d9987d: C7 runs with `validate_shapes=false` (now mitigated: C9 covers interior delta validation); AC 1 floor/cell changes covered only via entry; empty interior delta every tick per subscribed interior.

## Gate

- Proofs: integration batch 26 passed, 0 failed; `client-test.sh` 56 passed, 0 failed.
- Full integration project: 350 passed, 0 failed.
- `bash scripts/verify.sh`: not re-run this round (scoped); round 1 result carried from 2d9987d, failing only on the known arm64 pins (OptimizationEquivalenceTests seed 2, 4 GoldenHashesTests).
