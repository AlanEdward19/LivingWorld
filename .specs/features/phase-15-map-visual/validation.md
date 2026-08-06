# Fase 15 (Mapa visual VTT 2D) — Validation Report
**Verifier**: independent (did not author T1-T9)
**Commit range**: c6888d0..34e8473 (main)
**Date**: 2026-08-06

## Task Completion
| Task | Where (declared) | Where (actual) | Status |
| --- | --- | --- | --- |
| T1 | `src/LivingWorld.Api/Visual/*.cs`, `src/LivingWorld.Simulation/Visibility/*.cs` | `VisualScope.cs`, `VisualCursor.cs`, `VisualSnapshotEnvelope.cs`, `Layers/VisualLayerId.cs`, `Layers/LayerProjectionCatalog.cs`, `Layers/LayerBuildResult.cs` | Done |
| T2 | `Program.cs`, `Infrastructure/*.cs` | `Program.cs` wired to `SqliteWorldRepository`/`PersistentWorldRunner` | Done |
| T3 | `Realtime/*.cs` | `RealtimeGateway.cs`, `RealtimeEndpoints.cs` | Done |
| T4 | `Visual/Global*.cs`, `Visual/Layers/*.cs` | `GlobalProjector.cs`, `Layers/GlobalLayerBuilder.cs` | Done (5/14 layers deferred, documented) |
| T5 | `Visual/City*.cs`, `Visual/Interior*.cs`, `Visual/Layers/*.cs` | `CityProjector.cs`, `InteriorProjector.cs`, `Layers/CityLayerBuilder.cs` | Done (all 5 city-only layers NotYetModeled, documented) |
| T6 | `Visual/NpcTokens/*.cs`, `web/src/assets/npc-tokens/*` | `NpcTokens/NpcTokenComposer.cs`, `NpcTokenCatalog.cs`, `NpcTokenDescriptor.cs` — no `web/src/assets/npc-tokens/*` files (catalog is code-only, string ids not asset files) | Done, minor deviation (no physical asset files — acceptable, catalog is the asset registry) |
| T7 | `Simulation/Visibility/*.cs`, `Api/VisualInput/*.cs` | `PlayerMovementValidator.cs`, `PlayerVisibilityService.cs`, `CityVisibilityFilter.cs` (Api, not Simulation — documented SPEC_DEVIATION), `VisualInputEndpoints.cs` | Done, `/interact` deferred (documented, no AC requires it) |
| T8 | `web/*`, `LivingWorld.sln`, `scripts/*.sh` | `web/src/**`, web added to sln, `scripts/test.sh` updated | Done with a real gap — see AC below (move endpoint never called from UI) |
| T9 | `scripts/verify.sh`, `tests/LivingWorld.Tests/Visual/*Gate*Tests.cs` | `VisualGateTests.cs`, `generate-web-types.sh`, `verify.sh` updated | Done |

## Spec-Anchored AC Check
| Story | AC | Evidence (file:line) | Status |
| --- | --- | --- | --- |
| Espectador global | AC1 (visão simplificada + LOD) | `GlobalProjector.cs:28-47`; `GlobalProjectorTests.cs:33-56` | PASS |
| Espectador global | AC2 (eventos ativos) | `GlobalProjector.cs:23` `ActiveEvents` always `[]` — deferred, documented in context.md T4 | DEFERRED (known) |
| Espectador global | AC3 (drill-down contínuo) | `RealtimeEndpoints.cs:21-27` generic `/visual/subscribe` reused per scope; `App.test.tsx:65-81` proves world→city continuity via same hook | PASS |
| Camadas derivadas | AC1 (render camada sobre grid) | `GlobalLayerBuilder.cs:21-35`, `CityLayerBuilder.cs:16-18`; `LayerProjectionCatalogTests.cs:18-26` | PASS |
| Camadas derivadas | AC2 (catálogo exige builder) | `VisualGateTests.cs:38-54` `Every_visual_layer_id_is_covered_by_exactly_one_layer_builder` | PASS |
| Camadas derivadas | AC3 (deltas, sem write-back) | `VisualGateTests.cs:57-70`, `RealtimeGatewayEndpointTests.cs:71-82` canonical hash unchanged | PASS |
| Personagem FOW | AC1 (move via click/WASD, validação server, delta) | Server side: `PlayerMovementValidator.cs:11-18`, `VisualInputEndpoints.cs:20-36`, `VisualInputEndpointTests.cs:18-33`. Client side (fixed post-verification): `PlayerMoveControls.tsx` wires click buttons + WASD/arrow keydown to `moveNpc()`; `PlayerMoveControls.test.tsx:1-49`. | PASS (was PARTIAL, fixed in `b60f6f6`) |
| Personagem FOW | AC2 (fog + áreas visitadas permanecem visíveis) | Radius-only: `PlayerVisibilityService.cs:12-18`; "visited stays visible" clause explicitly deferred, documented in context.md T7 | DEFERRED (known) |
| Personagem FOW | AC3 (admin override) | `PlayerVisibilityService.cs:14`, `CityVisibilityFilter.cs:17-24`; `CityVisibilityFilterTests.cs:41-50`, `CityFowSubscribeEndpointTests.cs:52-65` | PASS |
| Resolução por foco | AC1-4 (global/cidade/interior/rebaixar) | `RealtimeEndpoints.cs:108-137` payload varies by scope kind; `App.tsx:57-79` renders exactly one view per focus; drill-down back buttons rebaixar (`CityView.tsx:15-17`, `InteriorView.tsx:14-16`) | PASS |
| Token NPC | AC1 (compor por camadas) | `NpcTokenComposer.cs:12-24` | PASS |
| Token NPC | AC2 (determinístico) | `NpcTokenComposerTests.cs:20-28` | PASS |
| Token NPC | AC3 (só camadas previstas mudam) | `NpcTokenComposerTests.cs:55-70` | PASS |
| Edge: reconexão sem write | — | `RealtimeGatewayEndpointTests.cs:71-82`, `VisualGateTests.cs:57-70` | PASS |
| Edge: movimento inválido rejeitado, hash inalterado | — | `VisualInputEndpointTests.cs:36-50` | PASS |
| Edge: subscribe sem permissão nega e não vaza | — | `RealtimeGatewayEndpointTests.cs:36-45`, `RealtimeGatewayEndpointTests.cs:85-92` (WS pre-upgrade 403) | PASS |

## Discrimination Sensor
| Target | Mutation | Test run | Result |
| --- | --- | --- | --- |
| `PlayerMovementValidator.Validate` adjacency (`ChebyshevDistance(...) > 1` → `> 5`) | Widened step-distance boundary to allow up to 5-cell jumps | `PlayerMovementValidatorTests` (4 tests) | **SURVIVED** — 4/4 still passed. `Validate_fails_for_a_cell_more_than_one_step_away` uses target `(npc.X+5, npc.Y)`; the default scenario map is a fixed 10x10 grid (`ScenarioRunner.DefaultMap`) and the NPC starts at `(5,5)` (`DefaultVillageLocation`), so `(10,5)` is already outside map bounds. The pre-existing `TryGetCell` bounds check fails first and masks the distance-boundary mutation — the test never actually exercises the `>1` vs `>5` boundary in isolation. Real weak-test finding (see Ranked Gaps). |
| `RealtimeGateway.Authorize` player-vs-world rule (`scope.Kind == VisualScopeKind.World` → `== VisualScopeKind.City`) | Redirected the player-mode deny rule from `World` to `City` | `RealtimeGatewayEndpointTests` (7 tests) | **KILLED** — 3/7 failed: `Subscribe_to_world_scope_as_player_is_denied_with_403_and_no_body`, `Replay_for_an_unauthorized_scope_is_denied_with_403`, `Websocket_subscribe_to_an_unauthorized_scope_is_rejected_with_403_before_upgrade` all expected 403 and got 200/400 instead. |
| `CityVisibilityFilter.ApplyFog` radius filter (dropped the `.Where(CanSee(...))` clause, kept all residents) | FOW filter became a no-op pass-through | `CityVisibilityFilterTests` + `CityFowSubscribeEndpointTests` (5 tests) | **KILLED** — 2/5 failed: `ApplyFog_keeps_only_residents_within_sight_radius_of_the_player` (collection had 2 items instead of 1) and `Player_mode_only_sees_residents_within_sight_radius_of_their_own_npc` (far NPC leaked into the response). |

All three mutations were injected one at a time, run against a `-c Release` build (to avoid colliding with the concurrent full-suite run's locked Debug binaries), confirmed, then reverted via `git checkout --` before moving to the next. Working tree confirmed clean after each revert.

## Gate Check
- Quick (`--filter FullyQualifiedName~LivingWorld.Tests.Visual`): 68 dotnet + 14 web (vitest) tests, all passed (run directly in this verification pass).
- Full (`bash scripts/test.sh`, no filter): **not re-run to completion in this pass** — the run was cancelled by the user for time after ~19 minutes (it was still mid-suite, past the slow baseline-recording tests, when killed). Not treated as a failure: this session's implementer already ran the full suite to completion after each of T2-T9's `Program.cs`-touching changes and reported 371 dotnet tests passed + 1 skip, 14 web tests passed. Citing that as prior evidence rather than re-deriving it here.

## Code Quality
No scope creep found. Spot-checked: `Program.cs`, `RealtimeEndpoints.cs`, `GlobalLayerBuilder.cs`/`CityLayerBuilder.cs`, `NpcTokenCatalog.cs`, `App.tsx`/`CityView.tsx`/`InteriorView.tsx`. All files stay within their task's declared "Where", comments consistently cite the phase/task/VTT-id, `NotYetModeled` fallback pattern reused consistently across T4/T5 instead of inventing data.

## Ranked Gaps (both fixed post-verification, commit `b60f6f6`)
1. **Fixed**: the T8 web client never wired the click/WASD move intent to `moveNpc()`/`POST /visual/player/{id}/move`. `web/src/api.ts:62` exported and unit-tested the call, but no component invoked it. Fix: `web/src/components/PlayerMoveControls.tsx` (directional buttons for click + a `window` keydown listener for WASD/arrow keys), rendered by `App.tsx` in City scope when `mode=Player`, computing the target cell from the player's own resident marker in the current `CitySnapshot`. Covered by `web/tests/PlayerMoveControls.test.tsx` (click posts the right target, keydown posts the right target, missing-own-npc shows a note instead of controls).
2. **Fixed**: `PlayerMovementValidatorTests.Validate_fails_for_a_cell_more_than_one_step_away` was confounded with the map-bounds check (see sensor row 1). Fix: target a cell exactly 2 steps away, clamped to stay in-bounds regardless of where the NPC spawns, with an explicit `world.Map.TryGetCell(...)` precondition assertion documenting the isolation. Re-verified by re-injecting the identical `>1`→`>5` mutation: the fixed test now fails as expected (mutant killed).

## Summary
**Verdict: PASS** (post-fix). Both findings from this verification pass were fixed and re-verified within the same session (`b60f6f6`): the discrimination sensor now shows 3/3 mutations killed, and the spec-anchored AC check for "Modo personagem com FOW" AC1 moves from PARTIAL to PASS (`web/src/components/PlayerMoveControls.tsx` + tests). Post-fix quick gate: 68 dotnet + 17 web tests passing. The four items already listed in `context.md` (ActiveEvents always empty, 5 layers `NotYetModeled`, FOW radius-only with no persistent discovered-cells memory, no `/interact` endpoint) remain accurately documented deferrals — deliberate scope decisions for a future phase, not oversights. No lessons recorded: this repo has no `scripts/lessons.py`, and both findings were resolved within the same verification pass rather than left as standing guidance for future features.
