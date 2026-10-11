# Verification - godot-api-readiness

**Verdict**: PASS
**Profile**: light
**Diff range**: a39e0bf..15d2097 (S4 lands in 36187d2; the round-5 fix is ca7d493..15d2097)
**Round**: 5 - scoped (re-judges C20, whose claim, proof and AC the fix touched; C1-C12, C18, C19 carried from 0cff1cc with every proof re-run at 15d2097)
**Verifier**: independent sub-agent (author != verifier)

## Round 5 - scoped

Verified at `15d2097`. The fix touches only the C20 test (+7 lines: a City-scope subscriber and three City
assertions), `checks.md` C20 text (World to World and City) and `plan.md` AC 4.1 ("cada escopo com assinante" to
"cada escopo Mundo ou Cidade com assinante", Interior excluded). No `src/` file changed.

Edits vs round 4: the C20 claim is tightened (adds City). The AC 4.1 edit narrows the plan's promise to what the
code does (`TickLoopService.cs:82-83` skips Interior, pre-existing) - a correction of an over-promise, not new
behaviour, but a requirement change all the same (see Gaps 2).

Regression at `15d2097`: `bash scripts/build.sh` exit 0; unit batch (6 proofs) `Passed 8, Failed 0`; integration
batch (10 proofs incl. C20) `Passed 17, Failed 0`. The background loop is off in the integration factory
(`src/LivingWorld.Api/Program.cs:128`), so deltas read by C20 can only come from the endpoints (carried from ca7d493).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C20 | With World and City subscribers, step and advance-year each deliver a delta whose `ToCursor.Sequence` is the previous plus 1 | integration batch, `...SimulationControlEndpointsTests.Step_and_advance_year_publish_a_delta_to_subscribers` in the 17 passed | World: `tests/LivingWorld.Tests.Integration/Simulation/SimulationControlEndpointsTests.cs:172` - `Assert.True(reader.TryRead(out var afterStep), ...)`; `:173` - `Assert.Equal(afterStep!.FromCursor.Sequence + 1, afterStep.ToCursor.Sequence)`; `:176`, `:177` same for advance-year. City: `:178` - `Assert.True(cityReader.TryRead(out var cityAfterStep), ...)`; `:179` - `Assert.Equal(cityScope.ScopeKey, cityAfterStep!.ToCursor.ScopeKey)`; `:180` - second `TryRead`. City sequence +1 is NOT asserted; it rests on the scope-agnostic `src/LivingWorld.Api/Realtime/RealtimeGateway.cs:90-92` (see Gaps 1). Production `src/LivingWorld.Api/Simulation/SimulationControlEndpoints.cs:46`, `:56`; `TickLoopService.cs:74`, `:82` | PASS |

### Faults (round 5)

Scratch `git worktree add --detach` of `15d2097` in the session scratchpad; build then C20 per mutant; `git checkout
-- .` in the scratch after each. Real tree `git status --porcelain` empty before and after; worktree removed and
pruned. M-A2 (advance-year) carried from ca7d493 (killed).

| Mutant | Location | Proof | Killed |
| --- | --- | --- | --- |
| M-A1 delete `loop.PublishDeltas();` from step | `SimulationControlEndpoints.cs:46` | C20 failed at `:172` "step publica um delta para quem assina" (World assertion fires first) | yes |
| M-A1b same mutant, World assertions `:172-173`, `:176-177` removed from the scratch test only, so City runs alone | `SimulationControlEndpoints.cs:46` | C20 failed at the City `:180` "e o do advance-year" (only 1 City delta) - the City assertions catch the missing step publish on their own | yes |
| M-A3 City scopes skipped in `PublishDeltas` (`if (scopeKey != "world")`) | `src/LivingWorld.Api/Simulation/TickLoopService.cs:82` | C20 failed at `:178` "o escopo Cidade também recebe o delta do step" | yes |

## Checks

Carried from 0cff1cc (round 3), re-run green at `15d2097` in the batches above; no cited file changed since.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | `Floors` >= 1, pure, order-independent | unit batch | `tests/LivingWorld.Tests.Unit/Cities/BuildingLayoutGeneratorTests.cs:18`, `:24` (carried from 0cff1cc) | PASS |
| C2 | Interior `layout` lists cells + material per floor (unit + HTTP) | unit + integration batch | `tests/LivingWorld.Tests.Unit/Visual/Projection/InteriorProjectorTests.cs:67`; `tests/LivingWorld.Tests.Integration/Visual/ApiContractTests.cs:67` (carried from 0cff1cc) | PASS |
| C3 | Floors > 1: one `Stair` per floor at same (x,y) | unit batch | `BuildingLayoutGeneratorTests.cs:41`, `:42` (carried from 0cff1cc) | PASS |
| C4 | every occupant `Floor` in `[0, Floors-1]` | unit batch | `InteriorProjectorTests.cs:96-97` (carried from 0cff1cc) | PASS |
| C5 | out-of-range floor fails, hash unchanged | unit batch | `tests/LivingWorld.Tests.Unit/Cities/InteriorEntryTests.cs:43`, `:45` (carried from 0cff1cc) | PASS |
| C6 | City marker has `floors` + ground footprint (unit + HTTP) | unit + integration batch | `tests/LivingWorld.Tests.Unit/Visual/Projection/CityProjectorTests.cs:242`; `ApiContractTests.cs:59-60` (carried from 0cff1cc) | PASS |
| C7 | `/npcs?cityId=X` living only, sorted name then id; empty city 0 items | integration batch, both proofs | `tests/LivingWorld.Tests.Integration/NpcDirectory/NpcDirectoryEndpointTests.cs:68`, `:70-71`, `:98-99`, `:100-103` (carried from 0cff1cc) | PASS |
| C8 | `q=ana` finds "Ana" and "ANA" | integration batch | `NpcDirectoryEndpointTests.cs:114`-`:116` (carried from 0cff1cc) | PASS |
| C9 | `take=201`, `skip=-1` give 400 | integration batch | `NpcDirectoryEndpointTests.cs:127` over `:120`, `:121` (carried from 0cff1cc) | PASS |
| C10 | `total` independent of paging | integration batch | `NpcDirectoryEndpointTests.cs:138-141` (carried from 0cff1cc) | PASS |
| C11 | catalog lists every `ActionType` once (8) | integration batch | `tests/LivingWorld.Tests.Integration/NpcDirectory/ActionLabelCatalogTests.cs:23-24` (carried from 0cff1cc) | PASS |
| C12 | `ActionType` without label fails coverage | integration batch | `ActionLabelCatalogTests.cs:32`, `:34` (carried from 0cff1cc) | PASS |
| C18 | recorded contract matches new routes/responses | integration batch (`ApiContractTests`) | `ApiContractTests.cs:110`; `tests/contracts/shape.npc-directory.json:5`; `tests/contracts/openapi.v1.json:955,965` (carried from 0cff1cc) | PASS |
| C19 | Player+World 403 intact | integration batch | `tests/LivingWorld.Tests.Integration/Visual/RealtimeGatewayEndpointTests.cs:43`, `:45` (carried from 0cff1cc) | PASS |

## Faults (carried from 0cff1cc)

| Mutant | Location | Proof | Killed |
| --- | --- | --- | --- |
| delete `.ThenBy(n => n.Id.Value)` | `src/LivingWorld.Api/NpcDirectory/NpcDirectoryEndpoints.cs:31` | C7 tie test | yes |
| delete `.Where(n => n.IsAlive)` | `NpcDirectoryEndpoints.cs:27` | C7 dead-NPC test | yes |

## Swept (existing) re-read

Carried from 0cff1cc - no `src/` file changed in the fix. Authorization row found at
`src/LivingWorld.Api/Realtime/RealtimeGateway.cs:25-28`.

## Gaps (ranked)

None blocking. Residual:
1. C20 claim says City deltas carry `Sequence` previous + 1, but the City half asserts delivery and `ScopeKey` only (`:178-180`). Low risk (sequence code is shared and scope-agnostic), one-line fix: assert `cityAfterStep.FromCursor.Sequence + 1 == cityAfterStep.ToCursor.Sequence`.
2. Plan AC 4.1 narrowed by the author (Interior excluded) to match the code; correct, but a plan change that should get explicit user sign-off. Round 5 also exceeds the skill's three-round bound.
3. C20 does not assert the delta carries the post-step tick (`ToCursor.Tick`) (carried from ca7d493).
4. Tie-break test relies on `WorldState` insert order; C9 also asserts `take=0` beyond AC 2.3 (carried from 0cff1cc).

## Gate

Round 5 at `15d2097`: `bash scripts/build.sh` exit 0; `bash scripts/test.sh --filter "<6 unit proofs>"` - 8 passed,
0 failed; `bash scripts/test.sh --project integration --filter "<10 integration proofs incl. C20>"` - 17 passed,
0 failed. `scripts/verify.sh` not run (out of verifier scope).
