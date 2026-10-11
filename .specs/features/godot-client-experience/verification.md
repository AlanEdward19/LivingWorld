# Godot client experience verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 3562bc7..29cc198 (HEAD). The round-3 fix is 8ecaff6..29cc198.
**Round**: 3 - scoped (re-judged C7, the only non-PASS verdict of round 2, plus the surface the fix created; C1-C6 and C8-C10 carried from 8ecaff6 with their proofs re-run at 29cc198)
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

Not run (profile `light`; the plan marks no binding source). Carried from 8ecaff6.

## Checks

Proofs: one batch `bash scripts/client-test.sh` at 29cc198, exit 0, 40 passed / 0 failed; all 12 `Proof:` lines of `checks.md` printed `ok` individually (engine leak warnings at exit and a minimap anchor warning are not failures; no SCRIPT ERROR). The fix touched only `client/godot/tests/test_ui_modules.gd` (+17, one new test) and `.specs/` files (`checks.md` +1 proof line on C7; the round-2 report committed). No production file changed. `checks.md` only tightened: nothing weakened or removed.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | tick 12 strong sun, light sky, windows off; tick 7 weaker warmer sun; tick 23 weak bluish moon, dark sky, sun moved, windows on | batch `ok` @29cc198 | `client/godot/tests/test_ui_modules.gd:34`, `:35`, `:37`, `:47`-`:50`, `:39`/`:44` (carried from 8ecaff6) | PASS |
| C2 | paused: hour frozen; 4 ticks/s: +4 h in 1 s, wraps day | batch `ok` @29cc198 | `test_ui_modules.gd:64`, `:68`, `:71` (carried from 8ecaff6) | PASS |
| C3 | tooltip: NPC name + server label, floors, population, empty without target | batch `ok` @29cc198 | `test_ui_modules.gd:151`-`:154` (carried from 8ecaff6) | PASS |
| C4 | minimap click requests the clicked cell; mapping reversible | batch `ok` @29cc198 | `test_ui_modules.gd:88`, `:97`, `:98` (carried from 8ecaff6) | PASS |
| C5 | delta event becomes toast with server text | batch `ok` @29cc198 | `test_ui_modules.gd:163` (carried from 8ecaff6) | PASS |
| C6 | at most 5 visible toasts, history keeps all | batch `ok` @29cc198 | `test_ui_modules.gd:107`, `:109` (carried from 8ecaff6) | PASS |
| C7 | loading visible without snapshot, hidden after the first; GET /npcs name reaches the NPC | batch, all three proofs `ok` @29cc198 | New: `test_ui_modules.gd:224` - `seen.append(app.loading.visible)` runs inside the `GET /visual/subscribe?scope=World` handler, i.e. while the server is up and before `_on_snapshot` (`Scenes/app.gd:313`) hides it; `:230` - `eq(seen, [true], ...)`; `:231` - `eq(app.loading.visible, false, "some quando o snapshot chega")`. Kept: `:200` offline sample, `:138`, `:141` name reaches the actor. Round-2 survivor M10 now killed (see Faults) | PASS |
| C8 | five bars: health 90, hunger 80, thirst 70, sleep 60, social 50 from GET /npcs/7 | batch `ok` @29cc198 | `client/godot/tests/test_panels.gd:57`, `:60`-`:64` (carried from 8ecaff6) | PASS |
| C9 | NPC name label shows only on hover or selection, with the name | batch `ok` @29cc198 | `test_ui_modules.gd:172`-`:183` (carried from 8ecaff6) | PASS |
| C10 | status tick 30 drives sky and top bar to 06:00 Amanhecer; minimap pan reaches the camera | batch `ok` @29cc198 | `test_ui_modules.gd:208`, `:209`, `:214` (carried from 8ecaff6) | PASS |

## Coverage

Recomputed from AC 2.4 for the loading row, the only authority the fix touched; other rows carried from 8ecaff6.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| tooltip targets (3) | AC 2.1 | NPC C3 · building C3 · city C3 (carried from 8ecaff6) | - |
| day phases asserted on scene (3) | AC 1.1 | noon C1 · morning C1 · night C1 (carried from 8ecaff6) | - |
| loading states (3) | AC 2.4 | shown while offline before first snapshot C7 · shown while connecting with the server up C7 (`:230`) · hidden after first snapshot C7 (`:231`) | - |
| name display (2) | AC 2.5 | on hover C9 · when selected C9 (carried from 8ecaff6) | - |
| detail bars (5) | AC 3.1 | Saúde · Fome · Sede · Sono · Social, all C8 (carried from 8ecaff6) | - |

## Faults injected

Round 3: scratch `git worktree add --detach` of 29cc198 in the session scratchpad; real tree `git status --porcelain` empty before and after; worktree removed and pruned. Each mutant ran against the new proof `test_loading_visible_during_normal_startup`; assertion messages read. M1-M9 and M11 carried from 8ecaff6 (all killed there; no production file changed since).

| Mutation | Location | Killed |
| --- | --- | --- |
| M10 loading hidden right after it is built (`loading.visible = false` after `_ui.add_child(loading)`) | `client/godot/Scenes/app.gd:234` | yes (C7 `:230`, "esperado [true], veio [false]"; the older offline proof still passes under it, as in round 2) |
| M12 first snapshot does not hide loading | `client/godot/Scenes/app.gd:313` | yes (C7 `:231`, "esperado false, veio true") |

## Plan conformance notes

- Landing `None` still holds: the fix changed no production file. Carried from 8ecaff6.
- Still untested wiring (level notes, not failures), carried from 8ecaff6: `app.gd:463` raycast -> `describe_hit` -> tooltip, and `world_view.gd:396` hover -> `set_hovered` (C9 drives `NpcActor` directly).
- Visual-only behaviour without automated proof unchanged (water, roofs, scenery, poses, toast fade, minimap drawing, theme). Carried from 8ecaff6.

## Gate

`bash scripts/client-test.sh` at 29cc198 - 40 passed, 0 failed, exit 0
