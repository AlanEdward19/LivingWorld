# Godot client experience verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 3562bc7..ca7d493 (HEAD). Round 4 scope is 999ed06..ca7d493 (E4 and CI land in ca7d493).
**Round**: 4 - scoped (judges the new slice E4 / C11-C13, the phase-boundary assertions added to C1's proof, and the CI job; C1-C10 carried from 29cc198 with every proof re-run at ca7d493 and citations refreshed)
**Verifier**: independent sub-agent (author != verifier)

## Round 4 - scoped

Verified at `ca7d493`. Commit `ca7d493` adds `Scenes/ambience.gd`, `Scenes/sky_life.gd`, a lamp post per door in
`Scenes/world_view.gd`, `lamp_material` and a 17-19 Entardecer / Noite-from-19 boundary in `Scenes/day_cycle.gd`,
wiring in `Scenes/app.gd` (and removes the client re-subscribe after step / +1 ano, now that the server publishes,
see `godot-api-readiness` C20), 3 new tests plus 2 phase assertions in `tests/test_ui_modules.gd`, and a `client`
job in `.github/workflows/ci.yml`.

Proofs: one batch `bash scripts/client-test.sh` at `ca7d493`, exit 0, `client-test: 43 passed, 0 failed` (40 in
round 3 + 3 new); each of the 15 `Proof:` lines of `checks.md` printed `ok` individually; no SCRIPT ERROR.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 (new assertions) | phase boundary: 18 h is Entardecer, 19.25 h is Noite | batch `ok test_ui_modules::test_day_cycle_follows_server_hour` @ca7d493 | `client/godot/tests/test_ui_modules.gd:55` - `eq(cycle.phase_name(), "Entardecer")` after `sync(18.0, ...)`; `:57` - `eq(cycle.phase_name(), "Noite", ...)` after `sync(19.25, ...)`. Mutant M-B5 killed | PASS |
| C11 | a door gets a lamp post, off at tick 12, lit at tick 23 | batch `ok test_ui_modules::test_door_gets_a_lamp_that_lights_at_night` | `client/godot/tests/test_ui_modules.gd:278` - `check(node.get_node_or_null("Lamp") != null, ...)`; `:284` - `eq(view.lamp_material.emission_energy_multiplier, 0.0, "poste apagado de dia")`; `:286` - `check(view.lamp_material.emission_energy_multiplier > 2.0, "poste aceso à noite")`. Production `client/godot/Scenes/day_cycle.gd:91-92`, `client/godot/Scenes/world_view.gd:240` | PASS |
| C12 | clouds drift, are shadow-only, and re-enter from the opposite side | batch `ok test_ui_modules::test_clouds_drift_and_wrap` | `client/godot/tests/test_ui_modules.gd:260` - `eq(puff.cast_shadow, SHADOW_CASTING_SETTING_SHADOWS_ONLY, ...)` per puff of cloud 0; `:263` - `check(sky.clouds[0].position.x > before, "a nuvem andou")`; `:266` - `check(sky.clouds[0].position.x < 0.0, ...)` after leaving the map | PASS |
| C13 | birds by day without crickets, crickets at night without birds, more wind at night, and M mutes | batch `ok test_ui_modules::test_ambience_levels_follow_daylight_and_mute` | Levels proven: `client/godot/tests/test_ui_modules.gd:243`, `:244`, `:245`. Mute NOT proven: `:249` - `eq(ambience.muted, true)` only reads back the flag that `set_muted(true)` at `:248` just wrote, on a node outside the tree (no player). Neither the M key (`client/godot/Scenes/app.gd:489-490`) nor the silencing (`client/godot/Scenes/ambience.gd:44` volume, `:48` early return) is exercised; mutants M-B6 and M-B7 survive | FAIL |

### Faults (round 4)

Scratch `git worktree add --detach` of `ca7d493` in the session scratchpad; each mutant applied, its proof run with
`--filter`, then `git checkout -- .` inside the scratch. Real tree `git status --porcelain` empty before and after;
worktree removed and pruned.

| Mutation | Location | Killed |
| --- | --- | --- |
| M-B2 lamp emission never set (delete the `lamp_material` branch) | `client/godot/Scenes/day_cycle.gd:91-92` | yes (C11 `:286` "poste aceso à noite"; note `:284` alone would pass, the material defaults to 0) |
| M-B3 clouds cast full shadows (`SHADOWS_ONLY` to `ON`) | `client/godot/Scenes/sky_life.gd:32` | yes (C12 `:260` "esperado 3, veio 1") |
| M-B4 birds at night (`"birds": 1.0`) | `client/godot/Scenes/ambience.gd:23` | yes (C13 `:244` "de noite: grilos, sem pássaros") |
| M-B5 Entardecer back to `< 20.0` | `client/godot/Scenes/day_cycle.gd:58` | yes (C1 `:57` "esperado Noite, veio Entardecer") |
| M-B6 M key no longer toggles (`set_muted(ambience.muted)`) | `client/godot/Scenes/app.gd:490` | no |
| M-B7 mute has no audible effect (volume stays -14 dB, `_process` ignores `muted`) | `client/godot/Scenes/ambience.gd:44`, `:48` | no |

### CI job review (not exercisable locally)

`.github/workflows/ci.yml` job `client` runs `GODOT_BIN="$HOME/bin/godot" bash scripts/client-test.sh` with no
filter, which matches the script's contract (`GODOT_BIN` override, exit 2 when missing, SCRIPT ERROR grep). It
could not be run here: it needs GitHub Actions / ubuntu and a download, so it is unverified. Risks read from the
file: (1) CI uses the non-mono `Godot_v4.7.2-stable_linux.x86_64` while local runs use `4.7.2.stable.mono`; the
project has no C# and no `dotnet` feature in `client/godot/project.godot:8`, so this should be fine; (2) the
release asset URL and zip layout are assumed, not checked; (3) no import step, but `client/godot/.godot/` is
gitignored and absent locally too, so local runs already prove a cold-cache run; (4) the audio driver on a
headless runner is not exercised by any test (ambience is never added to a tree in tests); (5) the job is not
in `scripts/verify.sh` by design (ADR-0022).

### Round 4 notes (non-blocking)

1. C11 lamp brightness is a continuous ramp, not the AC 4.1 switch at 19 h / 5 h: at 06:00 and 18:00 the lamp emits about 1.7 of 3.0 (`day_cycle.gd:92`, daylight 0.43). Only ticks 12 and 23 are asserted - precision gap in AC 4.1 vs C11.
2. Wiring untested (level notes, same class as round 3's): `app.gd:123` passes `lamp_material` to the cycle, `app.gd:277` feeds `ambience.daylight`, `app.gd:324` rebuilds clouds at the real map size.
3. `ambience.gd:29` uses `randomize()` - presentation only, outside `LivingWorld.Simulation`, so not a determinism violation.

## Binding sources

Not run (profile `light`; the plan marks no binding source). Carried from 29cc198.

## Checks

C1-C10 carried from 29cc198 (verdicts unchanged); proofs re-run at `ca7d493` in the batch above, all `ok`.
Citations in `test_ui_modules.gd` after line 53 refreshed (+4 lines from the C1 phase assertions);
`test_panels.gd` unchanged since 29cc198.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | tick 12 strong sun, light sky, windows off; tick 7 weaker warmer sun; tick 23 weak bluish moon, dark sky, sun moved, windows on | batch `ok` @ca7d493 | `client/godot/tests/test_ui_modules.gd:34`, `:35`, `:37`, `:47`-`:50`, `:39`/`:44` (carried from 29cc198) | PASS |
| C2 | paused: hour frozen; 4 ticks/s: +4 h in 1 s, wraps day | batch `ok` @ca7d493 | `test_ui_modules.gd:68`, `:72`, `:75` (carried from 29cc198, lines refreshed) | PASS |
| C3 | tooltip: NPC name + server label, floors, population, empty without target | batch `ok` @ca7d493 | `test_ui_modules.gd:155`-`:158` (carried from 29cc198, lines refreshed) | PASS |
| C4 | minimap click requests the clicked cell; mapping reversible | batch `ok` @ca7d493 | `test_ui_modules.gd:92`, `:101`, `:102` (carried from 29cc198, lines refreshed) | PASS |
| C5 | delta event becomes toast with server text | batch `ok` @ca7d493 | `test_ui_modules.gd:167` (carried from 29cc198, lines refreshed) | PASS |
| C6 | at most 5 visible toasts, history keeps all | batch `ok` @ca7d493 | `test_ui_modules.gd:111`, `:113` (carried from 29cc198, lines refreshed) | PASS |
| C7 | loading visible without snapshot, hidden after the first; GET /npcs name reaches the NPC | batch, all three proofs `ok` @ca7d493 | `test_ui_modules.gd:228`, `:234`, `:235`, `:204`, `:142`, `:145` (carried from 29cc198, lines refreshed) | PASS |
| C8 | five bars: health 90, hunger 80, thirst 70, sleep 60, social 50 from GET /npcs/7 | batch `ok` @ca7d493 | `client/godot/tests/test_panels.gd:57`, `:60`-`:64` (carried from 29cc198) | PASS |
| C9 | NPC name label shows only on hover or selection, with the name | batch `ok` @ca7d493 | `test_ui_modules.gd:176`-`:187` (carried from 29cc198, lines refreshed) | PASS |
| C10 | status tick 30 drives sky and top bar to 06:00 Amanhecer; minimap pan reaches the camera | batch `ok` @ca7d493 | `test_ui_modules.gd:212`, `:213`, `:218` (carried from 29cc198, lines refreshed) | PASS |

## Coverage

Carried from 29cc198 except the new rows (verified at ca7d493).

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| tooltip targets (3) | AC 2.1 | NPC C3 · building C3 · city C3 (carried from 29cc198) | - |
| day phases asserted (5) | `day_cycle.gd:53-60` | Dia C1 · Noite C1 · Amanhecer C1 · Entardecer C1 `:55` · Noite boundary C1 `:57` | - |
| loading states (3) | AC 2.4 | offline C7 · connecting C7 · hidden after snapshot C7 (carried from 29cc198) | - |
| name display (2) | AC 2.5 | hover C9 · selected C9 (carried from 29cc198) | - |
| detail bars (5) | AC 3.1 | all five C8 (carried from 29cc198) | - |
| ambience obligations (4) | AC 4.3 | birds by day C13 · crickets at night C13 · wind C13 · mute by M | mute by M (C13 `:249` reads a flag only; M-B6, M-B7 survive) |

## Faults injected (round 3, carried from 29cc198)

| Mutation | Location | Killed |
| --- | --- | --- |
| M10 loading hidden right after it is built | `client/godot/Scenes/app.gd:234` (at 29cc198) | yes |
| M12 first snapshot does not hide loading | `client/godot/Scenes/app.gd:313` (at 29cc198) | yes |

## Gaps (ranked)

1. C13 "M silencia" unproven - `client/godot/tests/test_ui_modules.gd:249` asserts only the flag; mutants M-B6 (`app.gd:490`) and M-B7 (`ambience.gd:44`, `:48`) survive. Fix: assert the player's `volume_db` (or that `_process` pushes no frames) after `set_muted(true)` with the node in the tree, and drive the M key through `App._unhandled_input`.
2. AC 4.1 vs C11 precision gap: lamp is a ramp, half-lit at 06:00 and 18:00; no boundary hour asserted.
3. CI job `client` unverified locally (download URL, non-mono build, headless audio).

## Gate

`bash scripts/client-test.sh` at ca7d493 - 43 passed, 0 failed, exit 0 (all green; the FAIL is a proof that cannot fail, not a red test).
