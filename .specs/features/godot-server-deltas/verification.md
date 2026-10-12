# godot-server-deltas verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 9f8ace6..00cb2eb (fix 94b56e0..00cb2eb)
**Round**: 3 - scoped
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

Carried from 94b56e0: profile `light`, step 1 does not run; no binding source.

## Checks

All proofs re-run at 00cb2eb: integration batch (InteriorDeltaTests, SimulationControlEndpointsTests, ApiContractTests) 26 passed, 0 failed; `bash scripts/client-test.sh` exit 0, 56 passed, 0 failed, each named C7-C9 test `ok`. Only C11 was touched by the fix (claim narrowed in checks.md:47, one line removed in TickLoopService.cs).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | entra vira upsert, sai vira removed | batch passed at 00cb2eb | carried from 94b56e0 - `InteriorDeltaTests.cs:50`, `:58` | PASS |
| C2 | troca de ação vira upsert; sem mudança, vazio | batch passed at 00cb2eb | carried from 94b56e0 - `InteriorDeltaTests.cs:79-80`, `:86` | PASS |
| C3 | prédio inexistente não publica, laço segue | batch passed at 00cb2eb | carried from 94b56e0 - `InteriorDeltaTests.cs:102-103` | PASS |
| C4 | sequence seguinte e replay | batch passed at 00cb2eb | carried from 94b56e0 - `InteriorDeltaTests.cs:121`, `:125` | PASS |
| C5 | status traz o calendário do mundo | batch passed at 00cb2eb | carried from 94b56e0 - `SimulationControlEndpointsTests.cs:209`, `:215-217` | PASS |
| C6 | 3 shapes existem e batem | batch passed at 00cb2eb | carried from 94b56e0 - `ApiContractTests.cs:102-103` | PASS |
| C7 | cliente aplica delta de interior sem reler snapshot | `test_app::test_interior_delta_updates_occupants_without_polling` ok | carried from 94b56e0 - `test_app.gd:86`, `:90`, `:93-94` | PASS |
| C8 | hoursPerDay 12 dirige hora e data | `test_ui_modules::test_calendar_from_server_drives_hour_and_day` ok | carried from 94b56e0 - `test_ui_modules.gd:470-478` | PASS |
| C9 | ApiClient valida npc-detail e status; stream descarta delta fora do contrato | 4 named client tests ok | carried from 94b56e0 - `test_shape_validator.gd:154`, `:158`, `:165-166` | PASS |
| C10 | frame WS com upserts e toCursor | batch passed at 00cb2eb | carried from 94b56e0 - `InteriorDeltaTests.cs:137`, `:139`, `:144` | PASS |
| C11 | reassinar um interior recebe todos os ocupantes no primeiro delta; mundo e cidade não mudam | batch passed at 00cb2eb | verified at 00cb2eb - claim now interior-only (`checks.md:47`); `InteriorDeltaTests.cs:168` unsubscribe, `:169` publish with no subscriber, `:177` `Assert.Single(delta.Upserts)`; implementation `TickLoopService.cs:85` evicts `_lastInterior` only | PASS |

## Plan conformance

- Carried from 94b56e0: landings 1 and 2, statuses 200/101/403, performance, `ScenarioRunner.Create` optional calendar param, `.gitignore` for `*.uid`.
- Verified at 00cb2eb: `git diff 9f8ace6 HEAD -- TickLoopService.cs` shows the World/City path unchanged - same `ParseScope`, `LivingScopeProjector.Build`, `_lastStates` diff and publish; the only edits are caching `SubscribedScopeKeys` in a local and routing `interior:` keys to `PublishInterior` before the World/City guard (before the feature those keys hit the same `continue`). No `_lastStates` eviction remains, so world/city behaviour is exactly as at 9f8ace6.
- Real tree `git status --porcelain` empty before and after the proofs and the mutant.

## Faults injected

Scratch worktree (`git worktree add --detach` at 00cb2eb), removed at the end; real tree untouched.

| Mutation | Location | Killed |
| --- | --- | --- |
| Remove the `_lastInterior` stale cleanup | `TickLoopService.cs:85` | yes - C11 `Resubscribing_starts_from_every_occupant_again`: `Assert.Single() Failure: The collection was empty` (1 failed, 5 passed) |
| Carried from 94b56e0: status hard-coded 24, skip PublishInterior, interior shape action as number | see round 2 | yes |
| Carried from 2d9987d: projector action diff, store ignores removed, day_cycle ignores hoursPerDay | see round 1 | yes |

The round-2 surviving mutant (`_lastStates` cleanup) no longer applies: that line was deleted.

## Gaps

1. Carried from 94b56e0: `"null"` in a recorded shape is a wildcard on both sides (`shape_validator.gd:64`, `ApiContractTests.cs:116`); null-recorded fields are not type-checked. Disclosed in checks.md Coverage notes; accepted trade-off.
2. Carried from 94b56e0: C11 holds only when a publish happens while nobody is subscribed; unsubscribe and resubscribe within one tick still diffs against the old state. Harmless (the resubscribe GET returns a fresh snapshot); the claim's wording "entre dois publicados" now discloses it.
3. Carried from 2d9987d: C7 runs with `validate_shapes=false` (mitigated by C9); AC 1 floor/cell changes covered only via entry; empty interior delta every tick per subscribed interior.

## Gate

- Proofs at 00cb2eb: integration batch 26 passed, 0 failed; `client-test.sh` 56 passed, 0 failed.
- `bash scripts/verify.sh`: not re-run (scoped round); carried from 2d9987d, failing only on the known arm64 pins (OptimizationEquivalenceTests seed 2, 4 GoldenHashesTests).
