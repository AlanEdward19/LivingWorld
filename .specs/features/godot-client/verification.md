# godot-client verification

**Verdict**: FAIL
**Profile**: light
**Diff range**: 68ea475..3562bc7 (HEAD)
**Round**: 1 - full
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

None. The profile is `light` and the plan marks no source as binding. ADR-0022 and the readiness plan are listed as context only.

## Checks

Proofs ran at HEAD in one batch: `bash scripts/client-test.sh`, exit 0. All 22 named tests appear individually as `ok`. The suite also ran a 23rd test, `test_interior_view::test_stage_enter_and_leave_toggle_dimming`, which has no check. The filter works: `--filter "test_cursor_stream::test_gap_triggers_replay_and_discards_delta"` ran 1 test and exited 0. A filter that matches nothing exits 1 (`run.gd:49`).

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | snapshot first, then deltas in `sequence` order | batch, `ok` | `client/godot/tests/test_cursor_stream.gd:35` - `eq(events, ["snapshot", 1, 2])` | PASS |
| C2 | a gap calls `/visual/replay` from the last cursor and drops the delta | batch + alone, `ok` | `test_cursor_stream.gd:51` - `eq(count_calls("GET /visual/replay"), 1)`; `:52` checks `sinceSequence=0`/`sinceTick=10`; `:53` - `eq(sequences(applied), [1, 2])` | PASS |
| C3 | a dropped channel reconnects and replays from the cursor | batch, `ok` | `test_cursor_stream.gd:70` - `eq(channels.size(), 2)`; `:71` checks replay with `sinceSequence=1`; `:72` - `eq(sequences(applied), [1, 2])` | PASS |
| C4 | `403` becomes `access_denied` and no payload reaches the store | batch, `ok` | `test_cursor_stream.gd:88` - `eq(denied.size(), 1)`; `:89` - `eq(snapshots.size(), 0)`; `:91` - `eq(stream.state, "denied")` | PASS |
| C5 | a response with a missing or mistyped field is rejected | batch, `ok` | Validator unit: `test_shape_validator.gd:20` (missing) and `:25` (mistyped), on the first key of `simulation-status` only. Real stream: `:36` - `eq(snapshots.size(), 0)`, for a MISSING field only. No stream-level mistyped case. In production only subscribe snapshots are validated (`Contract/cursor_stream.gd:93`). Deltas (`:120-130`), `/simulation/status` (`Scenes/panels.gd:181`), `/npcs` (`panels.gd:72`), the catalog (`Scenes/app.gd:59`) and the interior poll (`app.gd:234-236`, written straight to the store) are never validated, although `shape.simulation-status/npc-directory/action-catalog.json` exist. | FAIL |
| C6 | one node per city at its `location` and one per external NPC | batch, `ok` | `test_world_view.gd:54` - `eq(city_nodes.size(), 2)`; `:55` - `eq(npc_nodes.size(), 3)`; `:56` - `eq(npc_nodes[7].position, Vector3(2.5,0,2.5))` | PASS |
| C7 | pan, orbit and zoom do not call the transport | batch, `ok` | `test_camera_rig.gd:22` - `eq(requests, [])`, so no scope request is emitted. `:21` - `eq(transport.calls, [])` CANNOT FAIL, because the rig never receives the transport. The claim holds by construction: `rg transport Scenes/camera_rig.gd` finds only the comment at `camera_rig.gd:4`. | PASS |
| C8 | zooming below the threshold requests the focused city scope | batch, `ok` | `test_camera_rig.gd:40` - `eq(requests, [["City","city-a"]])`; `:46` covers the way back to World | PASS |
| C9 | only the affected NPC moves, and mid-step it sits between the two cells | batch, `ok` | `test_world_view.gd:77` - `check(halfway.x > 2.5 and halfway.x < 3.5)`; `:78` - the other NPC is unchanged; `:80` - final cell. Mutant M2 killed. | PASS |
| C10 | each of the 8 `ActionType` values has a mapped animation | batch, `ok` | `test_npc_animation.gd:22` - `check(Anim.is_mapped(action))` over `range(8)`; `:26` - `eq(seen.size(), 8)`. Mutant M3 killed. Ids match `Domain/Behavior/ActionType.cs:9-18`. | PASS |
| C11 | unknown action 999 uses the neutral pose and shows a label | batch, `ok` | `test_npc_animation.gd:35` - `eq(actor.pose, "idle")`; `:36` - `eq(label_text, "ação 999")` | PASS |
| C12 | the label comes from the received catalog | batch, `ok` | `test_npc_animation.gd:47` - `eq(label_text, "RÓTULO-DO-SERVIDOR")`, which settles the claim. The literal scan at `:49-50` covers only 4 words in 2 files, so it is weak. An independent `rg` over the non-test client found no hardcoded action text. The only label path is `npc_actor.gd:91`, via `label_for`. | PASS |
| C13 | entering subscribes to the interior and dims and blurs the exterior | batch, `ok` | `test_app.gd:53` checks `subscribe?scope=Interior&refId=4`; `:54` - `eq(dim_rect.visible, true)`. The blur and darkening live in the `dim_rect` shader (`interior_stage.gd:26-38`). | PASS |
| C14 | with floor N visible, the roof and upper floors are hidden | batch, `ok` | `test_interior_view.gd:40` - `eq(floor_nodes[2].visible, false)`; `:41` - `eq(roof.visible, false)` | PASS |
| C15 | switching floors shows only that floor's occupants | batch, `ok` | `test_interior_view.gd:53` - `eq(visible_occupant_ids(), [2, 3])`; `:55`, `:57` | PASS |
| C16 | leaving restores the exterior AND the previous subscription | batch, `ok` | `test_app.gd:68` - `eq(dim_rect.visible, false)`; `:70` - World resubscribe count +1; `:71` - `eq(store.kind, "World")`. The previous scope is World only. Mutant M6 (always restore World, which ignores the previous scope) SURVIVED, so the "previous scope" part is unproven when that scope is a City. | FAIL |
| C17 | the list pages `/npcs` with `q`/`skip`/`take ≤ 200` and shows "nenhum NPC" | batch, `ok` | `test_panels.gd:23` - `take=50`/`skip=0`; `:26` - `skip=50`; `:28` - `q=ana` and `skip=0`; `:32` - `eq(empty_label.visible, true)`; `:33` - `PAGE_SIZE <= 200`. Precision gap: AC 5.1 says "do escopo atual", but the check omits it and `NpcListPanel.city_filter` (`panels.gd:19`) is never set anywhere. | PASS |
| C18 | selecting an NPC fetches `/npcs/{id}` and highlights it | batch, `ok` | `test_panels.gd:48` - `eq(calls, ["GET /npcs/7"])`; `:49` - `eq(highlighted, [7])`. Only the signal is asserted. The scene-side highlight is wired at `app.gd:129` → `world_view.gd:241-246` and is unasserted. | PASS |
| C19 | follow tracks the NPC until released | batch, `ok` | `test_camera_rig.gd:59` - `check(target.distance_to(actor.position) < 0.05)`; `:66` - `eq(rig.target, parked)` | PASS |
| C20 | the city panel shows name, population and indicators | batch, `ok` | `test_panels.gd:60` checks that "Alfa", "120", "5000" and "0.31" are in `panel.text` | PASS |
| C21 | pause, resume, speed and step call `/simulation/*`, and status comes from `/simulation/status` | batch, `ok` | `test_clock_panel.gd:20` - `eq(posts, [pause, resume, speed, step])`; `:21` checks `"PAUSADO"`/`"tick 48"` in `status_text` | PASS |
| C22 | offline shows "sem conexão" and retries after 1, 2, 4, 8 s, capped at 8 | batch, `ok` | Cap: `test_cursor_stream.gd:110` - `eq(delays, [1,2,4,8,8,8])`, and mutant M4 (cap 16) was killed. The text "sem conexão" is asserted nowhere: `rg "sem conex" tests/` finds 0 hits, and `:111` only checks `state == "offline"`. Mutant M7 removed the banner and status text and the suite stayed 23/23 green. | FAIL |

## Faults injected

Optional under `light`. All runs were in a scratch `git worktree` at HEAD, since removed. The real tree's porcelain matched the baseline before and after.

| Mutation | Location | Killed |
| --- | --- | --- |
| M1 gap delta applied instead of replay | `Contract/cursor_stream.gd:126-128` | yes (C2) |
| M2 NPC snaps to destination instead of lerping | `Scenes/npc_actor.gd:116` | yes (C9) |
| M3 ActionType 7 mapping removed | `Scenes/animation_catalog.gd:9` | yes (C10) |
| M4 backoff cap raised to 16 s | `Contract/cursor_stream.gd:14` | yes (C22 cap) |
| M5 exit does not resubscribe | `Scenes/app.gd:227` | yes (C16) |
| M6 exit always restores World, ignoring the previous scope | `Scenes/app.gd:227` | no - survived (C16) |
| M7 offline banner and "sem conexão" text removed | `Scenes/app.gd:188-190`, `Scenes/panels.gd:183` | no - survived (C22) |

## Plan conformance

- Landing, engine classes: holds. `rg HTTPRequest|WebSocketPeer|HTTPClient` outside `tests/` hits only `Contract/http_transport.gd`. The camera rig has no transport dependency.
- Landing, "cenas só leem WorldStore": does not hold. `Scenes/app.gd:59,234,306` and `Scenes/panels.gd:72,103,172,177,181` build HTTP routes and read status codes through `transport.request`, which bypasses `CursorStream` and the shape validator. This is also the root cause of the C5 failure.
- Observable "loading state" (screen mundo, AC 1.1): not implemented. `rg -i "carregando|loading"` finds 0 hits and no check covers it.
- Observable "error state" (lista de NPCs): partly implemented. A failed `/npcs` sets `total = 0` and shows "nenhum NPC" (`panels.gd:75-83`). That reads as an empty list, not an error. A stream outage does show the banner (`app.gd:190`).
- Observable "empty state": implemented, with the text at `panels.gd:38`. Only its visibility is asserted.

## Gate

`bash scripts/client-test.sh` - 23 passed, 0 failed (exit 0). Verdict is FAIL on C5, C16 and C22, plus 2 surviving mutants.
