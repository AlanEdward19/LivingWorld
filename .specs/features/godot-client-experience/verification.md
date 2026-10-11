# Godot client experience verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 3562bc7..8ecaff6 (HEAD). The fix is 9b6e892..8ecaff6.
**Round**: 2 - scoped (re-judged C1, C7, C8, new C9, new C10 and the round-1 wiring notes; C2-C6 carried from 9b6e892 with their proofs re-run at 8ecaff6)
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

Not run (profile `light`; the plan marks no binding source). Carried from 9b6e892.

## Checks

Proofs: one batch `bash scripts/client-test.sh` at 8ecaff6, exit 0, 39 passed / 0 failed; all 11 `Proof:` lines of `checks.md` printed `ok` individually. Single run `--filter "test_ui_modules::test_status_drives_sky_and_minimap_drives_camera"` exit 0 (1 passed). The fix touched only `tests/test_ui_modules.gd`, `tests/test_panels.gd` and `.specs/` files (it also committed the round-1 report and rewrote `.specs/features/godot-client/verification.md`, outside this feature); no production file changed. `checks.md` edits only tighten: C1, C7 and C8 claims grew, C7 gained a proof, C9 and C10 were added, nothing was weakened or removed.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | tick 12 strong sun, light sky, windows off; tick 7 weaker and warmer sun; tick 23 weak bluish moon, dark sky, sun moved, windows on | batch `ok` @8ecaff6 | `client/godot/tests/test_ui_modules.gd:34` - `check(sun.light_energy < noon_energy - 0.2, ...)`; `:35` - `sun.light_color.g < 0.97`; `:37` - `background_color.is_equal_approx(Color("a9c7d8"))`; `:47` - `sun.light_energy < 0.6`; `:48` - `light_color.b > light_color.r`; `:49` - `background_color.v < 0.3`; `:50` - `sun.rotation != noon_rotation`; `:39`/`:44` windows. Round-1 mutant (energy writes removed) now killed, plus colour, rotation and background mutants | PASS |
| C2 | paused: hour frozen; 4 ticks/s: +4 h in 1 s, wraps day | batch `ok` @8ecaff6 | `test_ui_modules.gd:64` - `eq(cycle.hour, 10.0, ...)`; `:68` - `eq(cycle.hour, 14.0, ...)`; `:71` - `eq(cycle.hour, 3.0, ...)` (carried from 9b6e892, lines refreshed) | PASS |
| C3 | tooltip: NPC name + server label, floors, population, empty without target | batch `ok` @8ecaff6 | `test_ui_modules.gd:151` - `eq(app.describe_hit({"npc_id": 7}), "Ana\nTrabalhando", ...)`; `:152`; `:153`; `:154` (carried from 9b6e892, lines refreshed) | PASS |
| C4 | minimap click requests the clicked cell; mapping reversible | batch `ok` @8ecaff6 | `test_ui_modules.gd:88` - `back.distance_to(point) < 0.01`; `:97`; `:98` (carried from 9b6e892, lines refreshed) | PASS |
| C5 | delta event becomes toast with server text | batch `ok` @8ecaff6 | `test_ui_modules.gd:163` - `eq(app.toasts.visible_texts(), ["Nasceu uma criança"], ...)` (carried from 9b6e892, lines refreshed) | PASS |
| C6 | at most 5 visible toasts, history keeps all | batch `ok` @8ecaff6 | `test_ui_modules.gd:107` - size 5; `:109` - history 7 (carried from 9b6e892, lines refreshed) | PASS |
| C7 | loading visible without snapshot, hidden after the first; GET /npcs name reaches the NPC | batch, both proofs `ok` @8ecaff6 | `test_ui_modules.gd:200` - `eq(seen, [true], ...)` (sampled in the reconnect sleeper after a failed subscribe); `:201` - `eq(app.loading.visible, false, ...)`; `:138`; `:141` - `display_name == "Ana"`. Mutant M9 (offline path hides loading) killed. Mutant M10 (loading hidden right after it is built, `Scenes/app.gd:234`) SURVIVED: the normal start, server up and no snapshot yet, is never sampled; it is visible only by Godot's default `visible = true` | FAIL |
| C8 | five bars: health 90, hunger 80, thirst 70, sleep 60, social 50 from GET /npcs/7 | batch `ok` @8ecaff6 | `client/godot/tests/test_panels.gd:57` - `eq(transport.calls, ["GET /npcs/7"], ...)`; `:60` Fome 80; `:61` Saúde 90; `:62` - `eq(panel.bars["Sede"].value, 70.0)`; `:63` - `eq(panel.bars["Sono"].value, 60.0)`; `:64` Social 50. Mutant M11 (thirst and sleep swapped) killed | PASS |
| C9 | NPC name label shows only on hover or selection, with the name | batch `ok` @8ecaff6 | `test_ui_modules.gd:172` - hidden by default; `:175` - `eq(label.visible, true, "sob o cursor ...")`; `:176` - `"Ana" in label.text`; `:178`; `:181` - visible when selected; `:183`. Mutant M2 (label always hidden) killed | PASS |
| C10 | status tick 30 drives sky and top bar to 06:00 Amanhecer; minimap pan reaches the camera | batch + alone `ok` @8ecaff6 | `test_ui_modules.gd:208` - `eq(app.day_cycle.hour, 6.0, ...)`; `:209` - `"06:00"` and `"Amanhecer"` in `time_label.text`; `:214` - `app.rig.target.distance_to(Vector3(12.0, 0, 9.0)) < 0.1`. Mutants M7 (status not synced, `app.gd:250`) and M8 (minimap not wired, `app.gd:198`) killed | PASS |

## Coverage

Recomputed from the plan's AC text for the rows the fix touched; the tooltip row is carried from 9b6e892.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| tooltip targets (3) | AC 2.1 | NPC C3 · building C3 · city C3 (carried from 9b6e892) | - |
| day phases asserted on scene (3) | AC 1.1 | noon light and windows C1 · morning light C1 · night light and windows C1 | - |
| loading states (3) | AC 2.4 | shown while offline before first snapshot C7 · hidden after first snapshot C7 · shown while connecting normally none (M10 survived) | shown while connecting with the server up |
| name display (2) | AC 2.5 | label visible on hover C9 · label visible when selected C9 | - |
| detail bars (5) | AC 3.1 | Saúde C8 · Fome C8 · Sede C8 · Sono C8 · Social C8 | - |

## Faults injected

Scratch `git worktree add --detach` of 8ecaff6; real tree `git status --porcelain` empty before and after; worktree removed and pruned. Each mutant ran against its targeted proof; each kill reported the named test as FAIL with "0 passed, 1 failed"; the assertion message was read for M1, M3, M7, M8 and M11.

| Mutation | Location | Killed |
| --- | --- | --- |
| M1 sun `light_energy` writes removed (day and night) | `client/godot/Scenes/day_cycle.gd:76,81` | yes (C1, `:34` and `:47`) |
| M2 `_refresh_label` always hides the label | `client/godot/Scenes/npc_actor.gd:219` | yes (C9) |
| M3 day sun colour fixed to the neutral tone | `client/godot/Scenes/day_cycle.gd:80` | yes (C1, `:35`) |
| M4 night sun colour warm instead of blue | `client/godot/Scenes/day_cycle.gd:75` | yes (C1) |
| M5 sun rotation never written | `client/godot/Scenes/day_cycle.gd:74,78` | yes (C1) |
| M6 sky background never written | `client/godot/Scenes/day_cycle.gd:82,84` | yes (C1) |
| M7 status not passed to `day_cycle.sync` | `client/godot/Scenes/app.gd:250` | yes (C10) |
| M8 minimap `pan_requested` not connected to `rig.focus` | `client/godot/Scenes/app.gd:198` | yes (C10) |
| M9 offline before first snapshot hides loading | `client/godot/Scenes/app.gd:362` | yes (C7) |
| M10 loading hidden right after it is built | `client/godot/Scenes/app.gd:234` | no |
| M11 thirst and sleep values swapped | `client/godot/Scenes/panels.gd:140` | yes (C8) |

## Plan conformance notes

- Landing `None` still holds: the fix changed no production file. Carried from 9b6e892.
- Round-1 wiring notes: status -> `day_cycle.sync` and minimap -> `rig.focus` are now proven (C10). Still untested (level notes, not failures): `app.gd:463` raycast -> `describe_hit` -> tooltip, and `world_view.gd:396` hover -> `set_hovered` (C9 drives `NpcActor` directly).
- Visual-only behaviour without automated proof is unchanged (water, roofs, scenery, poses, toast fade, minimap drawing, theme). Carried from 9b6e892.

## Gate

`bash scripts/client-test.sh` at 8ecaff6 - 39 passed, 0 failed, exit 0
