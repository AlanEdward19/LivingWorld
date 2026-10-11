# godot-client verification

**Verdict**: PASS
**Profile**: light
**Diff range**: 68ea475..9b6e892 (HEAD). The fix is 3562bc7..9b6e892.
**Round**: 2 - scoped (re-judged C5, C16, C17, C22 and the Landing; every other row is carried from 3562bc7 with its proof re-run at 9b6e892)
**Verifier**: independent sub-agent (author != verifier)

## Binding sources

None. The profile is `light` and the plan marks no source as binding. Carried from 3562bc7.

## Checks

Every proof ran again at 9b6e892 in one batch: `bash scripts/client-test.sh`, exit 0, 36 passed and 0 failed. All 28 `Proof:` lines in `checks.md` (C5, C16, C17 and C22 now have several) show up one by one as `ok` in the output. The other 8 tests that ran belong to `test_ui_modules` and `test_interior_view::test_stage_enter_and_leave_toggle_dimming`, and none of them is a proof here. Line numbers were refreshed for the test files the fix touched: `test_app`, `test_panels`, `test_shape_validator` and `test_clock_panel`. The fix did not touch `test_cursor_stream`, `test_world_view`, `test_camera_rig`, `test_npc_animation` or `test_interior_view`, so their citations carry from 3562bc7.

| Check | Claim | Proof run | Evidence | Result |
| --- | --- | --- | --- | --- |
| C1 | snapshot first, then deltas in `sequence` order | batch `ok` @9b6e892 | `client/godot/tests/test_cursor_stream.gd:35` - `eq(events, ["snapshot", 1, 2])` (carried from 3562bc7) | PASS |
| C2 | a gap calls replay from the last cursor and drops the delta | batch `ok` @9b6e892 | `test_cursor_stream.gd:51` - `eq(count_calls("GET /visual/replay"), 1)`; `:53` - `eq(sequences(applied), [1, 2])` (carried from 3562bc7) | PASS |
| C3 | a dropped channel reconnects and replays | batch `ok` @9b6e892 | `test_cursor_stream.gd:70` - `eq(channels.size(), 2)`; `:72` (carried from 3562bc7) | PASS |
| C4 | `403` becomes `access_denied` with no payload | batch `ok` @9b6e892 | `test_cursor_stream.gd:89` - `eq(snapshots.size(), 0)`; `:91` - `eq(stream.state, "denied")` (carried from 3562bc7) | PASS |
| C5 | a missing or mistyped field from `shape.*.json` is rejected | 3 proofs, batch `ok` @9b6e892 | `client/godot/tests/test_shape_validator.gd:21` checks the missing field, `:26` the mistyped one. `:66` - `eq(response.ok, false)` for `isPaused:"sim"` through `ApiClient.clock_status`, `:67` checks the announced error and `:71` checks that a valid response passes. `:85` - `eq(snapshots.size(), 0)` for `width:"trinta"` on the stream. In production, `Contract/api_client.gd:37,49,58,66,70` validate status, catalog, `/npcs`, interior and city against `simulation-status`, `action-catalog`, `npc-directory`, `interior-subscribe` and `city-subscribe`. `rg "transport.request" client/godot/Scenes` finds 0 hits. Mutant M8 was killed. | PASS |
| C6 | one node per city and one per external NPC | batch `ok` @9b6e892 | `test_world_view.gd:54-56` (carried from 3562bc7) | PASS |
| C7 | navigation never calls the transport | batch `ok` @9b6e892 | `test_camera_rig.gd:22` - `eq(requests, [])` (carried from 3562bc7) | PASS |
| C8 | zooming past the threshold requests the City scope | batch `ok` @9b6e892 | `test_camera_rig.gd:40` - `eq(requests, [["City","city-a"]])` (carried from 3562bc7) | PASS |
| C9 | only that NPC moves, interpolated | batch `ok` @9b6e892 | `test_world_view.gd:77` - `check(halfway.x > 2.5 and halfway.x < 3.5)`; `:78` (carried from 3562bc7) | PASS |
| C10 | all 8 `ActionType` values are mapped | batch `ok` @9b6e892 | `test_npc_animation.gd:22`; `:26` - `eq(seen.size(), 8)` (carried from 3562bc7) | PASS |
| C11 | unknown 999 falls back to the neutral pose and a label | batch `ok` @9b6e892 | `test_npc_animation.gd:35` - `eq(actor.pose, "idle")`; `:36` (carried from 3562bc7) | PASS |
| C12 | the label comes from the catalog | batch `ok` @9b6e892 | `test_npc_animation.gd:47` - `eq(label_text, "RÓTULO-DO-SERVIDOR")` (carried from 3562bc7) | PASS |
| C13 | entering subscribes to Interior and dims the exterior | batch `ok` @9b6e892 | `client/godot/tests/test_app.gd:53` checks `subscribe?scope=Interior&refId=4`; `:54` - `eq(app.stage.dim_rect.visible, true)` (line numbers refreshed) | PASS |
| C14 | the roof and upper floors are hidden | batch `ok` @9b6e892 | `test_interior_view.gd:40-41` (carried from 3562bc7) | PASS |
| C15 | a floor switch shows only that floor's occupants | batch `ok` @9b6e892 | `test_interior_view.gd:53` - `eq(visible_occupant_ids(), [2, 3])` (carried from 3562bc7) | PASS |
| C16 | leaving restores the exterior and the previous scope | 2 proofs, batch `ok` @9b6e892 | World case: `test_app.gd:117` - `eq(dim_rect.visible, false)`; `:119` - World resubscribe count +1; `:120` - `eq(store.kind, "World")`. City case: `:80` - `eq(count_calls("GET /visual/subscribe?scope=City"), before + 1)`; `:81` - `eq(app.store.kind, "City")`. The round-1 survivor M6 is now killed. | PASS |
| C17 | the list pages `/npcs` with `q`/`skip`/`take` ≤ 200 in the current scope, says "nenhum NPC" when empty and shows an error, not empty, on failure | 3 proofs, batch `ok` @9b6e892 | `client/godot/tests/test_panels.gd:31` checks `take=50`/`skip=0`; `:34` - `skip=50`; `:36` - `q=ana`; `:41` - `eq(empty_label.text, "nenhum NPC")`; `:42` - `PAGE_SIZE <= 200`. Error state: `:86` - `eq(empty_label.text, "erro ao carregar a lista")`. Scope: `:99` checks `cityId=city-a`, and the wiring from the app is checked at `test_app.gd:82` - `eq(list_panel.city_filter, "city-a")`. The API accepts `cityId` (`NpcDirectoryEndpoints.cs:19,28`). M9 and M10 were killed. | PASS |
| C18 | select fetches `/npcs/{id}` and highlights | batch `ok` @9b6e892 | `test_panels.gd:57` - `eq(transport.calls, ["GET /npcs/7"])`; `:58` - `eq(highlighted, [7])` (line numbers refreshed) | PASS |
| C19 | follow tracks the NPC until released | batch `ok` @9b6e892 | `test_camera_rig.gd:59`; `:66` (carried from 3562bc7) | PASS |
| C20 | the city panel shows name, population and indicators | batch `ok` @9b6e892 | `test_panels.gd:72` checks that "Alfa", "120", "5000" and "0.31" are in `panel.text` (line numbers refreshed) | PASS |
| C21 | clock buttons call `/simulation/*` and the shown status comes from `/simulation/status` | batch `ok` @9b6e892 | `client/godot/tests/test_clock_panel.gd:23` - `eq(posts, [pause, resume, speed, step])`; `:24` - `"PAUSADO"`/`"tick 48"` in `status_text` (line numbers refreshed) | PASS |
| C22 | offline shows "sem conexão" and backs off 1, 2, 4, 8 s, capped at 8 | 2 proofs, batch `ok` @9b6e892 | Cap: `test_cursor_stream.gd:110` - `eq(delays, [1,2,4,8,8,8])` (carried from 3562bc7). Text: `test_app.gd:102` - `check(seen[0].begins_with("true` + pipe + `sem conexão"))`, so the banner is visible and its text starts with "sem conexão"; `:103` checks "nova tentativa em 1 s"; `:104` checks "em 2 s"; `:105` - the banner hides again after reconnecting. Source: `Scenes/app.gd:361`. M7 was killed. | PASS |

## Faults injected

Optional under `light`. Faults were re-injected on the surfaces the fix touched or created. Each ran in a scratch `git worktree` at 9b6e892, and the narrowest covering proof was run alone. The worktree was removed afterwards. The real tree's `git status --porcelain` was empty before and after, so it matched. M1-M5 are carried from 3562bc7, all killed.

| Mutation | Location | Killed |
| --- | --- | --- |
| M6 exit always restores World and ignores the previous scope | `Scenes/app.gd:403` | yes - `test_exit_returns_to_the_previous_city_scope` FAIL |
| M7 banner text emptied | `Scenes/app.gd:361` | yes - `test_offline_shows_banner_text_and_recovers` FAIL |
| M8 ApiClient keeps `ok=true` on shape errors | `Contract/api_client.gd:30` | yes - `test_api_client_rejects_mistyped_status` FAIL |
| M9 a failed list shows "nenhum NPC" | `Scenes/panels.gd:80` | yes - `test_failed_list_shows_error_not_empty` FAIL |
| M10 `npc_page` drops `cityId` | `Contract/api_client.gd:56` | yes - `test_list_filters_by_city_scope` FAIL |

## Plan conformance (verified at 9b6e892)

- Landing, "Contract/ is the only code that talks HTTP/WS": holds. `rg "transport.request" client/godot/Scenes client/godot/Store` finds 0 hits. Engine networking classes appear only in `Contract/http_transport.gd`. `app.gd:66-74` holds the transport only to build `ApiClient` and `CursorStream`.
- Landing, "scenes only read WorldStore": this holds for world, city and interior state. The interior poll goes through `store.apply_snapshot` (`app.gd:409-411`). The query panels (list, detail, clock, city summary) read `ApiClient` results `{ok, body}` directly, not the store. They no longer build routes or read HTTP codes (`panels.gd:68,130,225`; `app.gd:524`). This is a residual deviation from the literal wording. No check covers it.
- Observable "error state" in the NPC list is now distinct from empty (C17). The "loading state" Observable belongs to `godot-client-experience` and is not judged here.

## Gate

`bash scripts/client-test.sh` - 36 passed, 0 failed (exit 0) at 9b6e892. Every check has a located assertion, and all 10 mutants (M1-M10) were killed.

## Residual risks (not check failures)

1. Shape validation turns itself off when the contract file is missing (`api_client.gd:27`, `cursor_stream.gd:101-103`). AC 1.5 is therefore enforced only where `tests/contracts/` is present, which means not in an exported build.
2. `GET /npcs/{id}` and the stream deltas have no frozen `shape.*.json`, so they are not validated. This is outside C5's literal scope.
3. Panels read `ApiClient` results directly, not through `WorldStore` (see Landing above).
