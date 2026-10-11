# Godot client experience verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 3562bc7..9b6e892
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

Not run (profile `light`, and the plan marks no binding source; `Sources` names only the user's request and `godot-client/plan.md`).

## Checks

Proofs: one batch `bash scripts/client-test.sh` at 9b6e892, exit 0, 36 passed / 0 failed; every named test below printed `ok` individually. Single run `--filter "test_ui_modules::test_hover_text_comes_from_store_and_server_labels"` exit 0 (1 passed). A non-matching filter exits 1 ("0 passed"), so a filter cannot pass vacuously.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | tick 12 = midday light, windows off; tick 23 = night, windows on | batch, `test_ui_modules::test_day_cycle_follows_server_hour` ok | `client/godot/tests/test_ui_modules.gd:30` - `eq(window.emission_energy_multiplier, 0.0, ...)`; `:35` - `check(window.emission_energy_multiplier > 1.0, ...)`; `:28`/`:34` - `cycle.daylight() > 0.95` / `< 0.05`. The light half is not asserted on the scene: `:29` `sun.light_energy > 0.9` passes on Godot's default energy 1.0, and nothing asserts sun/environment at night. Mutant M2 (sun energy writes removed, `day_cycle.gd:76,81`) survived | FAIL |
| C2 | paused: hour frozen; 4 ticks/s: +4 h in 1 s, wraps day | batch, `test_day_cycle_runs_only_while_server_runs` ok | `client/godot/tests/test_ui_modules.gd:51` - `eq(cycle.hour, 10.0, "pausado...")`; `:55` - `eq(cycle.hour, 14.0, ...)`; `:58` - `eq(cycle.hour, 3.0, "vira o dia")` | PASS |
| C3 | tooltip: NPC name + server label, building floors, city population, empty when no target | batch, `test_hover_text_comes_from_store_and_server_labels` ok | `client/godot/tests/test_ui_modules.gd:138` - `eq(app.describe_hit({"npc_id": 7}), "Ana\nTrabalhando", ...)` ("Trabalhando" only exists in the fake catalog at `:109`; fallback is `"ação 2"`, `Store/action_labels.gd:17`); `:139` `"2 andares" in ...`; `:140` `"12 hab." in ...`; `:141` `eq(app.describe_hit({}), "")` | PASS |
| C4 | minimap click requests the clicked cell; mapping reversible | batch, `test_minimap_maps_cells_and_requests_pan` ok | `client/godot/tests/test_ui_modules.gd:75` - `back.distance_to(point) < 0.01`; `:84` - `eq(requested.size(), 1, ...)`; `:85` - `requested[0].distance_to(Vector3(5, 0, 7)) < 0.01` | PASS |
| C5 | delta event becomes toast with server text | batch, `test_delta_events_become_toasts_with_server_text` ok | `client/godot/tests/test_ui_modules.gd:150` - `eq(app.toasts.visible_texts(), ["Nasceu uma criança"], ...)` | PASS |
| C6 | at most 5 visible toasts, history keeps all | batch, `test_toasts_keep_five_and_full_history` ok | `client/godot/tests/test_ui_modules.gd:94` - `eq(toasts.visible_texts().size(), 5, ...)`; `:96` - `eq(toasts.history.size(), 7, ...)` | PASS |
| C7 | loading hides after first snapshot; GET /npcs name reaches the NPC in scene | batch, `test_loading_overlay_until_first_snapshot_and_names_load` ok | `client/godot/tests/test_ui_modules.gd:125` - `eq(app.loading.visible, false, ...)`; `:127` - `eq(app.store.names.get(7), "Ana", ...)`; `:128` - `eq(app.world_view.npc_nodes[7].display_name, "Ana", ...)` | PASS |
| C8 | bars show health 90, hunger 80, social 50 from GET /npcs/7 | batch, `test_panels::test_select_npc_fetches_and_highlights` ok | `client/godot/tests/test_panels.gd:57` - `eq(transport.calls, ["GET /npcs/7"], ...)`; `:60` - `eq(panel.bars["Fome"].value, 80.0, ...)`; `:61` Saúde 90.0; `:62` Social 50.0 | PASS |

## Coverage

Recomputed from the plan's AC text (the authority), not from the checks' table.

| Set (size) | Recomputed from | Member -> proof | Unproven |
| --- | --- | --- | --- |
| tooltip targets (3) | AC 2.1 | NPC C3 · building C3 · city C3 | - |
| day phases asserted on scene (2) | AC 1.1 | windows day/night C1 · scene light day/night none (daylight() model only) | scene light (sun/environment) at tick 12 and 23 |
| loading states (2) | AC 2.4 | hidden after snapshot C7 · shown before first snapshot none | loading visible before first snapshot |
| name display (2) | AC 2.5 "sobre o NPC selecionado ou sob o cursor" | name reaches actor C7 · label visible on hover/selected none (`npc_actor.gd:219` `_label.visible = _selected or _hovered`; `rg "_label\.visible|set_hovered|hover_npc|set_selected" client/godot/tests` = no hits) | label visibility on hover/selected |
| detail bars (5) | AC 3.1 | Saúde C8 · Fome C8 · Social C8 · Sede none · Sono none (fixture sends 70/60 at `test_panels.gd:49` but never asserts them) | Sede, Sono |

## Faults injected

Scratch `git worktree add --detach` of HEAD; real tree `git status --porcelain` empty before and after; worktree removed.

| Mutation | Location | Killed |
| --- | --- | --- |
| window emission always `0.0` | `client/godot/Scenes/day_cycle.gd:88` | yes (C1) |
| sun `light_energy` writes removed (day and night) | `client/godot/Scenes/day_cycle.gd:76,81` | no |
| `MAX_VISIBLE` 5 -> 6 | `client/godot/Scenes/toasts.gd:4` | yes (C6) |
| click emits `to_world(Vector2.ZERO)` regardless of position | `client/godot/Scenes/minimap.gd:77` | yes (C4) |
| `apply_names` does not call `set_display_name` | `client/godot/Scenes/world_view.gd:391` | yes (C7) |

## Plan conformance notes

- Landing `None` holds: no file outside `client/godot/` and `.specs/` changed in the range; `api_client.gd` only calls routes already in `godot-client` (`/simulation/*`, `/visual/catalog/actions`, `/npcs`, `/npcs/{id}`, `/visual/subscribe`).
- Untested wiring (level notes, not counted as failures): `app.gd:248` status -> `day_cycle.sync`; `app.gd:198` minimap -> `rig.focus`; `app.gd:451` raycast -> `describe_hit` -> tooltip.
- Visual-only behaviour with no automated proof (honest limitation, not a failure): water, roofs, trees/scenery (`scenery.gd`), articulated NPC poses beyond existing `test_npc_animation`, sky colours, toast fade, minimap drawing, UI theme.

## Gate

`bash scripts/client-test.sh` - 36 passed, 0 failed
