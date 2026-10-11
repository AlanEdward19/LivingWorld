# Godot client experience verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 3562bc7..3bb1844 (HEAD). Round 6 scope is 15d2097..3bb1844; `73d8fa6` touches only API tests and docs, so the client change is `3bb1844` (amended from `439d56d`, identical tree).
**Round**: 6 - scoped (judges new C14 / AC 5.1-5.2 and the NpcActor, WorldView, InteriorView, App refactor; C1-C13 carried from 15d2097 with every proof re-run at 3bb1844)
**Verifier**: independent sub-agent (author != verifier)

## Round 6 - scoped

Verified at `3bb1844`. Proofs: one batch `bash scripts/client-test.sh`, exit 0, `client-test: 45 passed, 0 failed`
(44 + C14). Each of the 17 `Proof:` lines printed `ok` once; no SCRIPT ERROR (engine leak messages and the minimap
anchor warning at exit, as before). Checks edit: C14 added, nothing removed. Plan edits: E5 added; two Assumption rows.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C14 | 200 NPCs + far camera: all simple (no arms, legs nor shadow); near returns detail; 20 NPCs never simplify | batch `ok test_ui_modules::test_crowd_uses_simple_actors_when_far_and_detail_when_near` @3bb1844 | `test_ui_modules.gd:332` 200 far_lod; `:334` arms[0] and legs[0] hidden; `:335` torso shadow OFF; `:339`, `:340` near restores; `:347` 20 NPCs keep detail. Production `world_view.gd:438-451`, `npc_actor.gd:260-268`. BUT head and hair (`npc_actor.gd:126`, `:134`) keep default cast_shadow ON under far LOD - AC 5.1 "sem sombra própria" is false in production; the proof asserts only the torso | FAIL |

Actor wiring (per-node `_process` removed): `rg 'NpcActor.new'` finds two production sites. `world_view.gd:382`
(`_spawn_npc`) - every entry of `npc_nodes` is visited each frame by `world_view.gd:443-451` (advance or
advance_cheap). `interior_view.gd:135` - every visible occupant gets `advance` at `interior_view.gd:200-203`;
hidden-floor occupants are not animated (intended). No orphaned actor. Test-only sites
(`test_npc_animation.gd:31`, `:43`, `test_camera_rig.gd:52`, `test_ui_modules.gd:172`) call what they need directly.
But neither driver is proven by any test - see Faults M-C3, M-C6.

## Binding sources

Not run (profile `light`; the plan marks no binding source). Carried from 15d2097.

## Checks

C1-C13 carried from 15d2097 (verdicts unchanged); proofs re-run at `3bb1844`, all `ok`. C9 proof was rewritten to
`label_visible()` and `label_text_shown()` (lazy label) and re-judged; it still fails if the label shows without
hover or selection. The C9 hunk removed one line, so citations after `:186` moved by -1 (refreshed).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | tick 12 noon, windows off; tick 7 weaker sun; tick 23 moonlight, windows on | batch `ok` @3bb1844 | `test_ui_modules.gd:34`, `:35`, `:37`, `:47`-`:50`, `:55`, `:57` (carried from 15d2097) | PASS |
| C2 | paused hour frozen; 4 ticks/s gives +4 h in 1 s | batch `ok` @3bb1844 | `test_ui_modules.gd:68`, `:72`, `:75` (carried from 15d2097) | PASS |
| C3 | tooltip: NPC name + server label, floors, population | batch `ok` @3bb1844 | `test_ui_modules.gd:155`-`:158` (carried from 15d2097) | PASS |
| C4 | minimap click requests the cell; mapping reversible | batch `ok` @3bb1844 | `test_ui_modules.gd:92`, `:101`, `:102` (carried from 15d2097) | PASS |
| C5 | delta event becomes toast with server text | batch `ok` @3bb1844 | `test_ui_modules.gd:167` (carried from 15d2097) | PASS |
| C6 | at most 5 visible toasts, full history | batch `ok` @3bb1844 | `test_ui_modules.gd:111`, `:113` (carried from 15d2097) | PASS |
| C7 | loading until first snapshot; GET /npcs name reaches the NPC | batch, three proofs `ok` @3bb1844 | `test_ui_modules.gd:227`, `:233`, `:203`, `:142`, `:145` (carried from 15d2097, lines refreshed) | PASS |
| C8 | five bars 90/80/70/60/50 from GET /npcs/7 | batch `ok` @3bb1844 | `test_panels.gd:57`, `:60`-`:64` (carried from 15d2097) | PASS |
| C9 | name label only on hover or selection | batch `ok` @3bb1844 | `test_ui_modules.gd:175`, `:178`, `:179`, `:181`, `:184`, `:186`; production `npc_actor.gd:294-307` (re-judged at 3bb1844) | PASS |
| C10 | status tick 30 drives sky to 06:00; minimap pan reaches camera | batch `ok` @3bb1844 | `test_ui_modules.gd:211`, `:217` (carried from 15d2097, lines refreshed) | PASS |
| C11 | lamp off at noon, lit at 23 h, ramp at 18 h and 5 h | batch `ok` @3bb1844 | `test_ui_modules.gd:304`, `:309`, `:310`, `:312` (carried from 15d2097, lines refreshed) | PASS |
| C12 | clouds drift, shadow-only, wrap | batch `ok` @3bb1844 | `test_ui_modules.gd:280`, `:286` (carried from 15d2097, lines refreshed) | PASS |
| C13 | birds day, crickets night, wind; mute by player volume and M | batch, two proofs `ok` @3bb1844 | `test_ui_modules.gd:242`-`:244`, `:253`, `:255`, `:266`, `:267` (carried from 15d2097, lines refreshed) | PASS |

## Assumptions (plan rows added in 3bb1844)

- Brilho dos postes (ramp): matches `day_cycle.gd:92` and C11; still `Confirmed? n` - the user never signed off
  on the AC 4.1 rewrite (round 5 gap 1, open).
- Desempenho 77 / 52 FPS: reproduced on this machine (Apple M1 Pro, API up at localhost:5214, HTTP 200).
  `--steps "wait:2;stress:500;quit"` (default zoom 22, far) gave 77.7 FPS twice; with `zoom:0.7` (15.4, near)
  48.6 FPS. Roughly reproduced; the plan does not say which zoom "perto" means. The 500 NPCs are synthetic
  (`dev_steps.gd:81-102`), added on top of the server snapshot, not served by the API.

## Faults injected

Scratch `git worktree add --detach` of `3bb1844` in the session scratchpad; each mutant applied by sed, diff
confirmed, proof run, then `git checkout -- .`. Real tree porcelain empty before and after; worktree removed and
pruned (`git worktree list` back to one).

| Mutation | Location | Killed |
| --- | --- | --- |
| M-C1 `set_far` never hides limbs (`limb.visible = true`) | `npc_actor.gd:265` | yes (C14 `:334` "sem braços e pernas") |
| M-C2 crowd threshold 150 to 5000 | `world_view.gd:440` | yes (C14 `:332` "esperado 200, veio 0") |
| M-C3 WorldView never calls `advance` for near actors (advance_cheap instead) | `world_view.gd:451` | survived (full suite 45 passed) |
| M-C4 crowd threshold 150 to 100 | `world_view.gd:440` | survived (boundary 150 not pinned; proof uses 200 and 20) |
| M-C5 torso shadow never changed | `npc_actor.gd:266` | yes (C14 `:335`) |
| M-C6 InteriorView `_process` never calls `advance` | `interior_view.gd:203` | survived (full suite 45 passed) |

## Gaps (ranked)

1. AC 5.1 not met: under far LOD the head and hair meshes (`npc_actor.gd:126`, `:134`) still cast shadows, so the
   NPC keeps a shadow. C14 asserts only `_torso.cast_shadow` (`test_ui_modules.gd:335`). Fix both code and proof.
2. Crowd timing defect (outside AC 5.x but a regression of movement interpolation): with more than 150 NPCs, near
   actors alternate `advance_cheap(delta)` and `advance(2*delta)` (`world_view.gd:448-451`), so `_t` and `_clock`
   advance 3*delta per 2 frames - walks finish 1.5x faster than the server step. Not covered by any test.
3. No test proves world or interior actors animate each frame since per-node `_process` was removed (M-C3, M-C6
   survived). Wiring is correct today by reading, but unguarded.
4. Threshold boundary 150 unpinned (M-C4); AC 5.2 "150 ou menos" only checked at 20.
5. Carried: AC 4.1 rewrite unconfirmed by the user; C11 precision at one point per ramp; C13 `_process` mute
   return unasserted; CI job `client` unverified locally; wiring `app.gd:123`, `:277`, `:324` untested. New App
   feed `app.gd:274-277` (view_center, view_radius, far_zoom from the rig) is also untested.
6. This is round 6, past the skill's three-round bound - escalate to the user.

## Gate

`bash scripts/client-test.sh` at 3bb1844 - 45 passed, 0 failed, exit 0. Verdict FAIL is on C14's claim, not on the gate.
