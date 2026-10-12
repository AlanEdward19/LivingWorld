# godot-server-deltas verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 9f8ace6..2d9987d
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

Profile `light`: step 1 does not run. The plan marks no source as binding (Sources: issue #39 and
`.specs/features/godot-client/plan.md`, both context only).

## Checks

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | entra vira upsert, sai vira removed, pelo canal do gateway | `test.sh --project integration` batch (14/14 = 5 InteriorDeltaTests + 1 Status + 8 ApiContractTests) exit 0 | `InteriorDeltaTests.cs:50` - `Assert.Equal(npcs[0].Id, Assert.Single(enteredDelta.Upserts).Npc)`; `:58` - `Assert.Equal(npcs[0].Id, Assert.Single(exitedDelta.Removed))`; read from `RealtimeGateway.SubscribeChannel` after `loop.PublishDeltas()` | PASS |
| C2 | troca de ação vira upsert; sem mudança, delta vazio | same batch, exit 0 | `InteriorDeltaTests.cs:79-80` - `Assert.Empty(quietDelta.Upserts)` / `Assert.Empty(quietDelta.Removed)`; `:86` - `Assert.Equal(ActionType.Work, Assert.Single(changedDelta.Upserts).Action)` | PASS |
| C3 | prédio inexistente não publica e o laço segue | same batch, exit 0 | `InteriorDeltaTests.cs:102` - `Assert.False(missing.TryRead(out _))`; `:103` - `Assert.True(world.TryRead(out _))` in the same `PublishDeltas()` call (an exception would fail the test) | PASS |
| C4 | sequence seguinte e replay devolve | same batch, exit 0 | `InteriorDeltaTests.cs:121` - `Assert.Equal(first!.ToCursor.Sequence + 1, second!.ToCursor.Sequence)`; `:125` - `Assert.Contains(replay.Value!, e => e.ToCursor.Sequence == second.ToCursor.Sequence)` | PASS |
| C5 | status traz os 3 campos do calendário do mundo | same batch, exit 0 | `SimulationControlEndpointsTests.cs:199-201` - `Assert.Equal(calendar.HoursPerDay, status!.HoursPerDay)` etc. over HTTP. Weak: factory calendar is the 24/30/12 default, so a hard-coded 24 survives (see Faults) | PASS |
| C6 | 3 shapes existem e batem | same batch, exit 0 | `ApiContractTests.cs:31` - `InlineData("npc-detail", "/npcs/0")` into `:41` `AssertMatchesContract`; `:102-103` - `AssertMatchesContract("shape.visual-delta.json" ...)` / `("shape.interior-delta.json" ...)`. Deltas are hand-built envelopes serialized with `JsonSerializerDefaults.Web`, the same options as `RealtimeEndpoints.cs:143` WireOptions | PASS |
| C7 | cliente aplica entrada, ação, saída sem reler snapshot | `client-test.sh` (one batch, 55 passed, 0 failed) - `ok test_app::test_interior_delta_updates_occupants_without_polling` | `test_app.gd:86` - `eq(...occupant_nodes.keys().size(), 2, "quem entrou aparece")`; `:90` - action 1; `:93` - `eq(...keys(), [2], "quem saiu some")`; `:94` - `eq(transport.count_calls("GET /visual/subscribe?scope=Interior"), calls_before)` (the URL `api_client.gd:66` uses for the snapshot). Timer removal proven by search, not by the test (see gaps) | PASS |
| C8 | hoursPerDay 12 dirige hora E data; default 24/30/12 | same client batch - `ok test_ui_modules::test_calendar_from_server_drives_hour_and_day` | `test_ui_modules.gd:470` - `"12:00"` at tick 18; `:473` - `"00:00"` at tick 12; `:476` - `{"year": 2, "month": 2, "day": 4}`; `:478` - default `{"year": 1, "month": 2, "day": 2}` | PASS |
| C9 | ApiClient valida npc-detail e status; stream descarta delta fora do contrato | same client batch - `ok` for all 3 named tests | `test_shape_validator.gd:103` - `eq((await api.npc_detail(7)).ok, false, "campo ausente é rejeitado")`; `:66` - `eq(response.ok, false, ...)`; `:133-134` - `eq(applied.size(), 1, ...)` / `eq(errors.size(), 1, ...)` (World scope only) | PASS |
| C10 | frame WS real com upserts e toCursor após snapshot | integration batch, exit 0 | `InteriorDeltaTests.cs:146-147` - `Assert.Contains("\"upserts\"", json)` / `Assert.Contains("\"toCursor\"", json)` over `_factory.Server.CreateWebSocketClient()` on `/visual/ws?scope=Interior` | PASS |

## Plan conformance

- Landing 1: `InteriorTickDelta(long Tick, IReadOnlyList<InteriorOccupant> Upserts, IReadOnlyList<NpcId> Removed)` at `InteriorProjector.cs:24` - matches.
- Landing 2: `SimulationStatusResponse(..., int HoursPerDay, int DaysPerMonth, int MonthsPerYear)` at `SimulationControlEndpoints.cs:8-9`, filled from `worldHost.Current.Calendar` at `:67-69` - matches.
- Shapes `tests/contracts/shape.npc-detail.json`, `shape.visual-delta.json`, `shape.interior-delta.json` exist; exercised by C6 (server) and C9 (client, npc-detail and visual-delta). `shape.simulation-status.json` carries the 3 new fields.
- Surface statuses: `200` status by C5; `101` WS by C10; `403` existing (Observable row, `RealtimeGateway.Authorize`).
- Performance: `rg PublishInterior` - only `TickLoopService.cs:87` inside `foreach (var scopeKey in gateway.SubscribedScopeKeys)` (`:83`), so work only for subscribed interiors. `rg _interior_timer` over `client/godot` returns nothing; `_poll_interior` removed.

## Faults injected

Run on request in a scratch worktree (`git worktree add --detach`), removed after; real-tree porcelain matched the empty baseline.

| Mutation | Location | Killed |
| --- | --- | --- |
| Diff compares only Floor and LocalCell (action change dropped) | `InteriorProjector.cs:30` | yes - C2 `Assert.Single() Failure: The collection was empty` |
| Skip `PublishInterior` | `TickLoopService.cs:87` | yes - C1, C2, C4 fail; C3 passes (expected); C10 HANGS forever (no receive timeout) instead of failing |
| Status `HoursPerDay` hard-coded `24` | `SimulationControlEndpoints.cs:67` | no - C5 and ApiContractTests 9/9 pass |
| Store ignores interior `removed` | `world_store.gd:182-183` | yes - C7 FAIL |
| `day_cycle.sync` ignores `hoursPerDay` | `day_cycle.gd:31` | yes - C8 FAIL (18:00 instead of 12:00; date {1,1,7}) |

## Gaps

1. Surviving mutant on C5: the test compares against the factory calendar, which equals the 24/30/12 default, so the server returning constants passes. Needs a non-default calendar (e.g. 12 h) in the fixture.
2. Contract types nullable fields as non-null: `InteriorOccupant.Action` is `ActionType?` (`InteriorProjector.cs:13`, filled from `n.CurrentAction` at `:45`) and `NpcVisual.CurrentAction`/`City` are nullable (`LivingScopeState.cs:23-24`), but `shape.interior-delta.json` and `shape.visual-delta.json` freeze them as `"number"`/object (recorded from a hand-built sample). With `validate_shapes` on (production default, `cursor_stream.gd:25`) a delta with a null action is rejected at `cursor_stream.gd:133`, raising `contract_error` and forcing a replay. No test covers a null action.
3. C10 has no timeout on `ReceiveAsync` (`InteriorDeltaTests.cs:143`): when no frame arrives it hangs the run instead of failing.
4. C7 runs with `validate_shapes=false` (`test_app.gd` `_app_with_transport`) and emits on `stream.delta_applied` directly, so the client never validates an interior delta against `shape.interior-delta`; C9 covers only the World delta shape. The "no polling" assertion cannot see a 1 s timer inside a synchronous test; it holds only because the timer was removed (search proof).
5. AC 1 names floor and cell changes; no check covers them (plan Out of scope explains the engine only sets them on entry, so entry covers them). Precision gap in the checks, not a code defect.
6. Minor: `_lastInterior` (`TickLoopService.cs:28`) is never evicted on unsubscribe, so after resubscribing the first delta diffs against stale state, contrary to the plan assumption "primeiro delta traz todos os ocupantes"; an empty delta is published every tick per subscribed interior (asserted as intended by C2).
7. Harness, not this feature: `bash scripts/client-test.sh` in the real tree rewrites `client/godot/project.godot` (drops `[rendering] renderer/rendering_method`) and creates 38 untracked `*.gd.uid` files; restored by the verifier.

## Gate

- Proofs: integration batch 14 passed, 0 failed; `client-test.sh` 55 passed, 0 failed (each named test `ok`).
- Full integration: 348 passed, 0 failed.
- `bash scripts/verify.sh` exit 1, stopped at unit: only `OptimizationEquivalenceTests.Default_scenario_ninety_days_keeps_the_recorded_hashes(seed: 2)` failed (1996 passed). LongRunning: only 4 `GoldenHashesTests` failed (113 passed). Both are the known arm64 pin mismatch, not this feature.
