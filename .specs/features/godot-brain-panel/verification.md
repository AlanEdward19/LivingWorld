# Verification — godot-brain-panel (issue #58, fatia 1)

Verifier independente (contexto em branco, não autor), 2026-10-14, branch `feat/godot-brain-panel` HEAD `892e8a3`.
Entradas: `gh issue view 58` (fatia 1), `plan.md`, `git diff origin/develop...HEAD`. Fatia 2 fora do escopo.

**Veredito: PASS (todos os critérios).** Nenhuma mutação sobreviveu. `git status` limpo ao fim.

## Gates rodados pelo Verifier

| Comando | Resultado |
|---|---|
| `bash scripts/client-test.sh` | `client-test: 104 passed, 0 failed`, exit 0 |
| `bash scripts/verify.sh` | `Passed! Failed: 0, Passed: 2193, Skipped: 3` · `verify: OK` · exit 0 |
| `bash scripts/test.sh --project integration --filter "…ObservationToCognitionTraceTests\|…ApiContractTests"` | `Passed! Failed: 0, Passed: 10` |

## Critérios

| Critério | Veredito | Evidência (teste · código) | Mutação → teste que reprova |
|---|---|---|---|
| S1.1 escopo da câmera World/City/Building | PASS | `test_app::test_camera_scope_is_published_to_the_observation_registry` · `app.gd` `_publish_observation` | City→World; Building sem buildingId; remover `_publish_observation(kind)` (gate reprova: erro de script, exit 1) |
| S1.2 heartbeat 10 s; `scope:null` ao sair | PASS | `test_brain_panel::test_scope_observer_heartbeat_timer_fires_every_ten_seconds`, `…publishes_scope_changes_and_heartbeat_and_clears`, `test_app::test_returning_to_the_menu_retires_the_observation_source` · `scope_observer.gd:10,17,39-41`, `boot.gd:27` | remover `timer.timeout.connect(heartbeat)`; `interval := 1000.0`; `clear()` sem POST; remover `await app.release_observation()` |
| S1.3 escopo City → rastro; sem escopo vazio (.NET) | PASS | `ObservationToCognitionTraceTests.Trace_is_empty_without_…` | `ShouldRecordCognitionTrace` sempre true; sempre false → ambos reprovam |
| AC1 3 decisões, recente primeiro, rótulo | PASS | `test_timeline_lists_decisions_newest_first_with_action_labels` · `brain_panel.gd:163-164,213` | remover `entries.reverse()` (3 FAIL); rótulo → `str(winner)` |
| AC2 N/M/K nós + vencedora destacada | PASS | `test_selecting_a_decision_draws_the_fixture_counts_and_highlights_the_winner` (3/2/2 e 2/1/2) | remover `stimulus.add_child(node)`; remover `alternative_nodes.append`; remover meta `winner`; alternativas sem esmaecer |
| AC3 rastro vazio → mensagem, sem fluxo; morto/arquivado → própria | PASS | `test_empty_trace_shows_message_and_no_flow_nodes`, `test_dead_or_archived_npc_has_its_own_message` | remover `_show_empty(EMPTY_NO_TRACE)`; desligar ramo `lod == 1` |
| AC4 Vigiar POST / Parar DELETE / 4xx toast | PASS | `test_watch_posts_then_deletes_and_a_4xx_warns_without_breaking` · `api_client.gd:71-73`, `app.gd` `warning→toasts.push` | trocar POST↔DELETE; `warning.emit`→`print`; ignorar `response.ok` |
| AC5 fechado sem requisição periódica | PASS | `test_closed_panel_makes_no_periodic_requests_and_open_one_refreshes_on_new_hour` | remover `not is_open`; remover dedupe `tick == _last_tick` |
| AC6 FPS `stress:500` ±3 com painel aberto | PASS | reproduzido (abaixo) | n/a (medição) |
| AC7 shape valida 1 item de `cognitionTrace` | PASS | `test_contract_shape_validates_a_cognition_trace_item`; `ApiContractTests.Npc_inspection_shape_matches_…`; `test_shape_validator.gd` verde | `winner` "number"→"string" (cliente reprova); `winner`→"null" e `previousIntent`→"number" (.NET reprova); renomear chave `cognitionTrace` (gate cliente reprova) |
| Item 1 botão Cérebro + atalho B (README § Controles) | PASS | `test_brain_button_and_b_key_open_and_close_the_panel_with_dated_timeline` · `panels.gd:140-143`, `app.gd` KEY_B, README linha "Cérebro do NPC" | remover `brain.pressed.connect`; remover conexão `brain_requested`; remover `KEY_B` |
| Item 2 data pelo `/world/calendar` | PASS | `test_open_brain_panel_refreshes_on_clock_tick_and_dates_use_the_server_calendar` (calendário 10 h/5 d/2 m → "Ano 1 · Mês 1 · Dia 3 · 07:00") | remover `brain_panel.date_of = _date_text`; forçar 24 h (2 FAIL) |
| Item 6 ao vivo por hora; só redesenha se mudou | PASS | `test_open_brain_panel_refreshes_on_clock_tick…`, `test_refresh_redraws_only_when_the_latest_entry_changed` | remover `brain_panel.on_world_tick(...)`; desligar early-return de "nada novo" |
| Item 7 strings PT + tema | PASS | strings em `brain_panel.gd:9-12,81,195-196`; painel é filho de `_ui` com `_ui.theme = UiTheme.build()` (`app.gd:146`) | n/a (inspeção) |

Total: 34 mutações (30 rodadas no `client-test.sh`, 4 no .NET); cada uma revertida com `git checkout -- <arquivo>`.
29/30 reprovaram no cliente. A sobrevivente no cliente — `winner` "number"→"null" no shape ("null" = qualquer
valor no validador GDScript) — reprova no .NET (`ApiContractTests`, comparação exata do contrato): coberta.

## AC6 — FPS reproduzido pelo Verifier

API: `LW_DB=/tmp/lw-ver3.db bash scripts/play.sh --api-only` (encerrada com `pkill -f LivingWorld.Api`).
Godot 4.7.2 mono, M1 Pro, `--path client/godot -- --steps …`, intercalado:

| Rodada | sem painel (`wait:2;wait:2;stress:500;quit`) | com painel (`wait:2;brain:16;wait:2;stress:500;quit`) |
|---|---|---|
| 1 | 78,5 | 79,1 |
| 2 | 78,8 | 76,2 |
| 3 | 80,1 | 77,6 |
| média | 79,1 | 77,6 (Δ −1,5 FPS, dentro de ±3) |

Após as rodadas, `GET /npcs/16` tinha `cognitionTrace` com 15 entradas (rastro de fato gravado com o cliente
publicando o escopo). README § Desempenho registra 79,0 vs 77,0 — consistente.

## Observações (não bloqueiam)

- `on_world_tick` é chamado a cada status do relógio e deduplica por tick (=hora). Em velocidade alta o número de
  buscas fica limitado pela cadência do status do relógio, não por frame: atende o "por hora, não por frame".
- Uma mutação (remover `_publish_observation`) e outra (renomear a chave do shape) são pegas pelo gate como erro de
  script (indexação de `bodies`/chave ausente), não por asserção; o gate sai 1, então contam como reprovadas.
