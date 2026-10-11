# Godot client experience verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 3562bc7..15d2097 (HEAD). Round 5 scope is ca7d493..15d2097 (the fix commit).
**Round**: 5 - scoped (re-judges C13, the only non-PASS verdict of round 4, and C11 whose proof and claim the fix touched; C1-C10 and C12 carried from ca7d493 with every proof re-run at 15d2097)
**Verifier**: independent sub-agent (author != verifier)

## Round 5 - scoped

Verified at `15d2097`. The fix touches only `client/godot/tests/test_ui_modules.gd` (C11 proof +6 lines, C13 proof
+17 lines incl. a new test) and `.specs/` (`checks.md` C11 and C13 text, +1 Proof line on C13; `plan.md` AC 4.1
rewritten). No production file under `client/godot/Scenes/` changed, so carried production citations hold.

Checks edits vs round 4: C11 and C13 claims only tightened (C11 adds 18 h and 5 h intermediate brightness; C13
replaces "M silencia" with a player-volume drop and return, and adds the M-key proof). Nothing removed.
Plan edit is NOT a tightening: the old AC 4.1 was a switch (lit for hour >= 19 or < 5, off by day); the new AC 4.1
describes the ramp the code already had (`day_cycle.gd:92`), under which 18 h and 5 h are lit. It brings the plan
to the implementation rather than the reverse - see Gaps 1.

Proofs: one batch `bash scripts/client-test.sh` at `15d2097`, exit 0, `client-test: 44 passed, 0 failed` (43 in
round 4 + `test_m_key_toggles_ambience_mute`); each of the 16 `Proof:` lines of `checks.md` printed `ok`
individually; no SCRIPT ERROR (engine leak messages at exit and the minimap anchor warning, as in earlier rounds).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C11 | door lamp off at noon, lit at 23 h, intermediate at 18 h and 5 h | batch `ok test_ui_modules::test_door_gets_a_lamp_that_lights_at_night` @15d2097 | `client/godot/tests/test_ui_modules.gd:305` - `eq(view.lamp_material.emission_energy_multiplier, 0.0, ...)`; `:310` - `check(night > 2.0, ...)`; `:311` - `check(dusk > 0.0 and dusk < night, ...)` after `sync(18.0, ...)`; `:313` - same open interval after `sync(5.0, ...)`. Production `client/godot/Scenes/day_cycle.gd:92`. Mutants M-B8, M-B9 killed | PASS |
| C13 | birds by day, crickets at night, more wind at night; player volume drops on mute and returns on unmute; M toggles it | batch `ok ...test_ambience_levels_follow_daylight_and_mute` and `ok ...test_m_key_toggles_ambience_mute` @15d2097 | Levels `test_ui_modules.gd:243`-`:245`. Mute: `:248` adds Ambience to the tree; `:254` - `check(player.volume_db <= -70.0, ...)`; `:256` - `eq(player.volume_db, audible_db, ...)`. M key: `:267` - `eq(app.ambience.muted, true, "M silencia")` after `app._unhandled_input(key)`; `:268` - `check(app.ambience._player.volume_db <= -70.0, ...)`; `:270` second press unmutes. Production `client/godot/Scenes/ambience.gd:44`, `client/godot/Scenes/app.gd:489-490`. Round-4 survivors M-B6, M-B7 now killed | PASS |

## Binding sources

Not run (profile `light`; the plan marks no binding source). Carried from ca7d493.

## Checks

C1-C10 and C12 carried from ca7d493 (verdicts unchanged); proofs re-run at `15d2097` in the batch above, all `ok`.
The fix's hunks start at `test_ui_modules.gd:245`, so citations below 245 are unchanged; C12 lines refreshed (+21).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | tick 12 noon light, windows off; tick 7 weaker warmer sun; tick 23 moonlight, windows on; 18 h Entardecer, 19.25 h Noite | batch `ok` @15d2097 | `test_ui_modules.gd:34`, `:35`, `:37`, `:47`-`:50`, `:55`, `:57` (carried from ca7d493) | PASS |
| C2 | paused: hour frozen; 4 ticks/s: +4 h in 1 s, wraps day | batch `ok` @15d2097 | `test_ui_modules.gd:68`, `:72`, `:75` (carried from ca7d493) | PASS |
| C3 | tooltip: NPC name + server label, floors, population, empty without target | batch `ok` @15d2097 | `test_ui_modules.gd:155`-`:158` (carried from ca7d493) | PASS |
| C4 | minimap click requests the clicked cell; mapping reversible | batch `ok` @15d2097 | `test_ui_modules.gd:92`, `:101`, `:102` (carried from ca7d493) | PASS |
| C5 | delta event becomes toast with server text | batch `ok` @15d2097 | `test_ui_modules.gd:167` (carried from ca7d493) | PASS |
| C6 | at most 5 visible toasts, history keeps all | batch `ok` @15d2097 | `test_ui_modules.gd:111`, `:113` (carried from ca7d493) | PASS |
| C7 | loading visible without snapshot, hidden after the first; GET /npcs name reaches the NPC | batch, three proofs `ok` @15d2097 | `test_ui_modules.gd:228`, `:234`, `:235`, `:204`, `:142`, `:145` (carried from ca7d493) | PASS |
| C8 | five bars 90/80/70/60/50 from GET /npcs/7 | batch `ok` @15d2097 | `client/godot/tests/test_panels.gd:57`, `:60`-`:64` (carried from ca7d493) | PASS |
| C9 | NPC name label only on hover or selection | batch `ok` @15d2097 | `test_ui_modules.gd:176`-`:187` (carried from ca7d493) | PASS |
| C10 | status tick 30 drives sky and top bar to 06:00 Amanhecer; minimap pan reaches the camera | batch `ok` @15d2097 | `test_ui_modules.gd:212`, `:213`, `:218` (carried from ca7d493) | PASS |
| C12 | clouds drift, shadow-only, re-enter from the opposite side | batch `ok` @15d2097 | `test_ui_modules.gd:281`, `:284`, `:287` (carried from ca7d493, lines refreshed) | PASS |

## Coverage

Rows the fix touched recomputed at 15d2097; others carried from ca7d493.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| tooltip targets (3) | AC 2.1 | NPC C3 · building C3 · city C3 (carried from ca7d493) | - |
| day phases asserted (5) | `day_cycle.gd:53-60` | Dia · Noite · Amanhecer · Entardecer · Noite boundary, all C1 (carried from ca7d493) | - |
| loading states (3) | AC 2.4 | offline · connecting · hidden after snapshot, all C7 (carried from ca7d493) | - |
| name display (2) | AC 2.5 | hover C9 · selected C9 (carried from ca7d493) | - |
| detail bars (5) | AC 3.1 | all five C8 (carried from ca7d493) | - |
| lamp hours asserted (4) | C11 claim | noon `:305` · 23 h `:310` · 18 h `:311` · 5 h `:313` | - |
| ambience obligations (4) | AC 4.3 | birds C13 · crickets C13 · wind C13 · mute by M C13 `:254`, `:267`-`:268` | - |

## Faults injected

Round 5: scratch `git worktree add --detach` of `15d2097` in the session scratchpad; each mutant applied, its proof
run with `--filter`, then `git checkout -- .` in the scratch. Real tree `git status --porcelain` empty before and
after; worktree removed and pruned (`git worktree list` back to one). M-B2 to M-B5 carried from ca7d493 (killed).

| Mutation | Location | Killed |
| --- | --- | --- |
| M-B7 `set_muted` no longer changes `volume_db` | `client/godot/Scenes/ambience.gd:44` | yes (C13 `:254` "mudo: volume praticamente zero (-14.0 dB)"; also `:268` "e o volume cai" in the M-key test) |
| M-B6 `KEY_M` branch removed from `_unhandled_input` | `client/godot/Scenes/app.gd:489-490` | yes (C13 `:267` "M silencia esperado true, veio false") |
| M-B8 lamp hard switch `3.0 if light < 0.5 else 0.0` | `client/godot/Scenes/day_cycle.gd:92` | yes (C11 `:311` "3.0 < 3.0" and `:313`) |
| M-B9 lamp hard switch `3.0 if light <= 0.05 else 0.0` | `client/godot/Scenes/day_cycle.gd:92` | yes (C11 `:311` "0.0 < 3.0" and `:313`) |

## Gaps (ranked)

1. Plan AC 4.1 was rewritten by the author in the fix commit from a 19 h / 5 h switch to "lit at 23 h, off at noon, ramp at dusk and dawn" - the shape the code already had. The checks now match the plan, but the requirement itself changed after review; needs explicit user sign-off. Also round 5 exceeds the skill's three-round bound, which calls for user escalation anyway.
2. C11 precision: "crescente no entardecer / decrescente no amanhecer" is asserted at one point each (18 h, 5 h), not as monotonic over the interval.
3. C13: `_process` early return on `muted` (`ambience.gd:48`) is not asserted (volume -80 dB alone silences, so low risk); the second M press asserts the flag only, the volume return is proven via `set_muted(false)` at `:256`.
4. Carried from ca7d493: CI job `client` unverified locally; wiring `app.gd:123`, `:277`, `:324` untested.

## Gate

`bash scripts/client-test.sh` at 15d2097 - 44 passed, 0 failed, exit 0.
