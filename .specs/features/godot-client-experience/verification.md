# Godot client experience verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 3562bc7..876bfa0 (HEAD). Round 7 scope is 3bb1844..876bfa0 (one commit, `876bfa0`: `npc_actor.gd`, `world_view.gd`, `interior_view.gd`, `test_ui_modules.gd`, checks.md, verification.md).
**Round**: 7 - scoped (re-judges C14 and the four round-6 gaps; new C15, C16, C17 judged; C1-C13 carried from 3bb1844 with every proof re-run at 876bfa0)
**Verifier**: independent sub-agent (author != verifier)

## Round 7 - scoped

Verified at `876bfa0`. Proofs: one batch `bash scripts/client-test.sh`, exit 0, `client-test: 48 passed, 0 failed`
(45 + C15, C16, C17). Each of the 20 `Proof:` lines in checks.md printed `ok` exactly once; no SCRIPT ERROR (only the
known engine leak messages at exit). Checks edit: C14 claim widened to torso, head and hair; C15-C17 added; nothing removed.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C14 | 200 NPCs + far camera: all simple, no arms or legs, torso, head and hair without shadow; near restores; 20 never simplify | batch `ok` @876bfa0 | `test_ui_modules.gd:332`, `:334`, `:336` (loop over `_shadow_parts`), `:337` (size 3), `:349`. Production: `npc_actor.gd:118`, `:129`, `:137` register torso, head, hair; `npc_actor.gd:269` turns all off in `set_far`. Gap 1 of round 6 closed | PASS |
| C15 | 150 NPCs far keep detail, 151 become simple | batch `ok` @876bfa0 | `test_ui_modules.gd:366`, `:372`; production `world_view.gd:440` (`> 150`). Kills both 100 and `>=` 150 | PASS |
| C16 | a 1 s walk reaches half step in 0.5 s, alone and in a crowd of 300 | batch `ok` @876bfa0 | `test_ui_modules.gd:376`, `:384`, `:385`; production `npc_actor.gd:363` (`tick` calls `advance_motion(dt)` once per frame), `world_view.gd:446` (one `tick` per actor per frame) | PASS |
| C17 | world and interior animate actors every frame; interior skips other floors | batch `ok` @876bfa0 | `test_ui_modules.gd:390`, `:395`, `:410`, `:411`; production `world_view.gd:446`, `interior_view.gd:202-204` | PASS |

Timing read (round-6 gap 2): `rg` finds three production callers - `world_view.gd:446` (`tick`), `interior_view.gd:202`
(`advance_motion`) and `:204` (`animate`); `advance_cheap` is gone and `advance` (`npc_actor.gd:357`) is now test-only.
`tick` (`npc_actor.gd:363`) advances `_t` by `dt` exactly once per frame for every actor, near or not, due or not;
`animate` (`npc_actor.gd:351`) only touches `_clock` and pose, receiving the accumulated skipped time once, then the
accumulator resets. Off-view actors reset the accumulator, so `_clock` pauses (cosmetic, not double-counted). Interior
calls motion and pose once each per frame. No remaining double or triple counting of motion or animation time.

## Binding sources

Not run (profile `light`; the plan marks no binding source). Carried from 3bb1844.

## Checks

C1-C13 carried from 3bb1844 (verdicts unchanged); proofs re-run at `876bfa0`, all `ok`. The fix touched only lines
after `:331` of `test_ui_modules.gd`, so earlier citations are unchanged.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | tick 12 noon, windows off; tick 7 weaker sun; tick 23 moonlight, windows on | batch `ok` @876bfa0 | `test_ui_modules.gd:34`, `:35`, `:37`, `:47`-`:50`, `:55`, `:57` (carried from 3bb1844) | PASS |
| C2 | paused hour frozen; 4 ticks/s gives +4 h in 1 s | batch `ok` @876bfa0 | `test_ui_modules.gd:68`, `:72`, `:75` (carried from 3bb1844) | PASS |
| C3 | tooltip: NPC name + server label, floors, population | batch `ok` @876bfa0 | `test_ui_modules.gd:155`-`:158` (carried from 3bb1844) | PASS |
| C4 | minimap click requests the cell; mapping reversible | batch `ok` @876bfa0 | `test_ui_modules.gd:92`, `:101`, `:102` (carried from 3bb1844) | PASS |
| C5 | delta event becomes toast with server text | batch `ok` @876bfa0 | `test_ui_modules.gd:167` (carried from 3bb1844) | PASS |
| C6 | at most 5 visible toasts, full history | batch `ok` @876bfa0 | `test_ui_modules.gd:111`, `:113` (carried from 3bb1844) | PASS |
| C7 | loading until first snapshot; GET /npcs name reaches the NPC | batch, three proofs `ok` @876bfa0 | `test_ui_modules.gd:227`, `:233`, `:203`, `:142`, `:145` (carried from 3bb1844) | PASS |
| C8 | five bars 90/80/70/60/50 from GET /npcs/7 | batch `ok` @876bfa0 | `test_panels.gd:57`, `:60`-`:64` (carried from 3bb1844) | PASS |
| C9 | name label only on hover or selection | batch `ok` @876bfa0 | `test_ui_modules.gd:175`-`:186`; production `npc_actor.gd:294-307` (carried from 3bb1844) | PASS |
| C10 | status tick 30 drives sky to 06:00; minimap pan reaches camera | batch `ok` @876bfa0 | `test_ui_modules.gd:211`, `:217` (carried from 3bb1844) | PASS |
| C11 | lamp off at noon, lit at 23 h, ramp at 18 h and 5 h | batch `ok` @876bfa0 | `test_ui_modules.gd:304`, `:309`, `:310`, `:312` (carried from 3bb1844) | PASS |
| C12 | clouds drift, shadow-only, wrap | batch `ok` @876bfa0 | `test_ui_modules.gd:280`, `:286` (carried from 3bb1844) | PASS |
| C13 | birds day, crickets night, wind; mute by player volume and M | batch, two proofs `ok` @876bfa0 | `test_ui_modules.gd:242`-`:244`, `:253`, `:255`, `:266`, `:267` (carried from 3bb1844) | PASS |

## Assumptions

Carried from 3bb1844: lamp ramp and 77 / 52 FPS rows still `Confirmed? n`; the fix does not touch them.

## Faults injected

Scratch `git worktree add --detach` of `876bfa0` in the session scratchpad; each mutant applied by perl, diff shown,
full `bash scripts/client-test.sh` run, then `git checkout -- .` (porcelain 0 after each). Real tree porcelain empty
before and after; worktree removed and pruned (`git worktree list` back to one). Mutant "tick ignores due" skipped as
acceptable behaviour, per scope.

| Mutation | Location | Killed |
| --- | --- | --- |
| M-C1 `set_far` never hides limbs | `npc_actor.gd:266` | yes (C14) |
| M-C2 crowd threshold 150 to 5000 | `world_view.gd:440` | yes (C14, C15) |
| M-C3 WorldView never animates near actors (`near` forced false) | `world_view.gd:446` | yes (C17; was alive in round 6) |
| M-C4 crowd threshold 150 to 100 | `world_view.gd:440` | yes (C15; was alive in round 6) |
| M-C4b crowd threshold `>=` 150 | `world_view.gd:440` | yes (C15) |
| M-C5 torso skipped in shadow loop | `npc_actor.gd:269` | yes (C14) |
| M-C6 InteriorView never calls `animate` | `interior_view.gd:204` | yes (C17; was alive in round 6) |
| M-C7a head skipped in shadow loop | `npc_actor.gd:269` | yes (C14) |
| M-C7b head mesh not registered in `_shadow_parts` | `npc_actor.gd:129` | yes (C14 size 3) |
| M-C8 `tick` moves only on due frames (crowd loses motion) | `npc_actor.gd:364` | yes (C16) |
| M-C9 interior animates hidden-floor actors too | `interior_view.gd:203` | yes (C17) |
| M-C10 round-6 defect replay: crowd actors get 1.5x dt | `world_view.gd:446` | yes (C16) |

## Gaps (ranked, none blocking a check)

1. C14 trusts the actor's own `_shadow_parts` list plus a hard-coded size 3: a future mesh added to the body without
   registering it would cast a shadow under far LOD and still pass. Acceptable today (three meshes are all there is).
2. C16 covers one near actor with `far_zoom` false; off-view and far-LOD motion take the same `advance_motion` path by
   reading (`npc_actor.gd:363`) but are not asserted. Off-view actors also pause `_clock` (cosmetic only).
3. Carried from 3bb1844: AC 4.1 rewrite unconfirmed by the user; C11 one point per ramp; C13 `_process` mute return
   unasserted; CI job `client` unverified locally; `app.gd:123`, `:277`, `:324` and the App rig feed untested.
4. Round 7 is past the skill's three-round bound - the user should know it took seven rounds.

## Gate

`bash scripts/client-test.sh` at 876bfa0 - 48 passed, 0 failed, exit 0.
