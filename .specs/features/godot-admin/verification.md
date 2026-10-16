# Verificação independente — godot-admin (#59 fatia E) [Resultado: PASS]

Verifier de contexto em branco (não escreveu o código), 2026-10-15. HEAD `597a446` (`git checkout --detach feat/godot-admin`).
Diff: `git diff feat/godot-player...HEAD` (16 arquivos, nenhum em `src/`). Entradas: `gh issue view 59`, `plan.md`, o diff.

## Gates (rodados pelo Verifier)

| Gate | Saída |
| --- | --- |
| Godot `--import` (cache ausente no worktree) | exit 0 |
| `client-test` completo (runner à mão: o harness recusou o script com redirecionamento) | `client-test: 190 passed, 0 failed`, exit 0, 0 `SCRIPT ERROR`/`Parse Error` |
| `bash scripts/client-test.sh --filter test_admin` | `21 passed, 0 failed` |
| `bash scripts/verify.sh` | `Passed! 2193, Skipped 3` · `verify: OK` |
| `dotnet test tests/LivingWorld.Tests.Integration --filter "AdminContractTests\|ApiContractTests"` | `Passed! 12, Failed 0` |

## Mutações (56 no GDScript + 3 em shapes). Cada uma aplicada por script, suíte rodada, arquivo restaurado

Todas reprovaram POR ASSERT (mensagem `esperado X, veio Y` / `check`). Só M21b e M21c tiveram também 1 `SCRIPT ERROR`
ao lado da falha de assert (o `client-test.sh` reprova SCRIPT ERROR de qualquer forma): gap cosmético, não FAIL.

| Mutação | Teste que reprova (test_admin::) |
| --- | --- |
| M01 `admin_enabled := true` fixo (app.gd) | the_app_reads_the_admin_token_from_the_environment |
| M02 `calendar_of` do console fixo 24 h (app.gd) | the_app_feeds_the_console_the_calendar_and_dates (300≠720) |
| M03 `actor_input` desconectado de `actor` | typing_the_operator_changes_the_header… ("maria"≠"alan") |
| M04 KINDS name↔profession trocados | typing_the_operator…every_kind_maps… (kind=profession≠name) |
| M05 sem `admin_window.date_of = _date_text` | the_app_feeds_the_console… ("tick 12" ≠ "Ano 0 · Mês 1 · Dia 2") |
| M06 sem `bar.add_child(pause_label)` | the_pause_label_lives_in_the_top_bar… |
| M07a/b/c `_fetch`/`_admin`/`request()` sem `extra_headers` | search_sends…, audit_table…, pause_sends…, request_hands_its_extra_headers… |
| M08 velocidade em `/world/speed` | resume_clears…_and_speed_posts_the_value |
| M09a/b/c `build_headers` sem token / sem extras / token sem variável | the_real_transport_sends_the_admin_token_only_when_configured / …operator_header_and_the_token |
| M10a/b botão / F8 sem token | without_a_token_the_console_does_not_appear_or_open |
| M11 motivo não exigido | pause_is_validated_on_the_client_before_any_request |
| M12a/b/c teto 31 dias / sem limite 1 / sem limite superior | pause_is_validated… (+ app_feeds… em M12a) |
| M13a–e `X-Admin-Actor` ausente em pause/resume/speed/search/audit (uma por rota) | pause_sends…, resume_clears… (×2), search_sends…, audit_table… |
| M14 body da pausa `max` em vez de `maxTicks` | pause_sends_reason_ceiling_and_the_operator_header |
| M15a/b/c/d 403/400/5xx sem mensagem; falha sem aviso | server_refusals_are_shown_and_warn (+ search_result…, audit_table…, responses_are_checked…) |
| M16 velocidade não positiva enviada | resume_clears… ("velocidade não positiva nem sai" 1≠2) |
| M17a/b `q` sem codificar / `kind` fixo | search_sends_query_kind_and_header… (+ typing… em M17b) |
| M18a/b clique abre outro hit / app não abre a ficha | search_result_opens_the_npc_sheet…, with_a_token_f8…results_open_the_sheet (sem `GET /npcs/9`) |
| M19a–f auditoria sem quando/quem/ação/alvo/detalhe; `fromTick` fixo 0 | audit_table_has_who_when_action_and_target |
| M20a–e faixa ausente / sem detalhe / não some / `note_running` vazio / app não chama | everyone_sees_the_paused_banner…, banner_names_who_paused… |
| M21a/b/c shape `""` em admin-pause/search/audit | responses_are_checked_against_frozen_shapes (3≠2 avisos) |
| M22 `close()` faz requisição | closed_window_asks_nothing |
| M23 `_process` com requisição | an_open_console_makes_no_requests_while_frames_pass (5 GETs) |
| M24a/b/c F8 / Esc / botão desligados | with_a_token_f8_and_the_button_toggle_and_esc_closes… |
| M26a/b pausa / retomada fora de `/admin` | pause_sends…, server_refusals…, resume_clears… |
| S1/S2/S3 `shape.admin-pause` isPaused→string; `admin-search` sem professionId; `admin-audit` detail→number | `AdminContractTests` Failed (linhas 27, 36, 40) |

`git status` limpo após todas as mutações (só este arquivo novo).

## Critérios do plan.md

| # | Evidência | Status |
| --- | --- | --- |
| 1 sem token não aparece | app.gd:56 (`admin_enabled` do ambiente), :199 (botão), :583 (`_toggle_admin`); M01, M10a/b | PASS |
| 2 pausa: motivo, 1..30 dias do calendário, `{reason,maxTicks}` + ator, 400/403/5xx | admin_window.gd:144-159, :196-213; api_client.gd:132; M02, M11-M15 | PASS |
| 3 retomar/velocidade na rota certa com ator; não positiva não sai | admin_window.gd:162-179; api_client.gd:136-141; M08, M13b/c, M16, M26b | PASS |
| 4 busca `q`/`kind` codificados, kinds = `AdminSearch.Kinds` (`name,city,profession`, AdminSearch.cs:12), clique abre ficha, vazio/400/403 | admin_window.gd:12, :243-266; app.gd:303; M04, M17, M18 | PASS |
| 5 auditoria `fromTick`, quando (calendário)/quem/ação/alvo/detalhe | admin_window.gd:303-323; M05, M19a-f | PASS |
| 6 faixa para todos na barra do topo, detalhe se este cliente pausou, some ao voltar | app.gd:188-194, :367-369; admin_window.gd:183-193; M06, M20a-e | PASS |
| 7 resposta fora do contrato vira aviso; janela fechada não pede nada | api_client.gd:24-34; M21a-c, M22, M23 | PASS |
| 8 `stress:500` ±3 FPS | Evidência do autor (README/plan): sem 71,0/70,7/76,8; com 72,6/73,0/72,7. **Não reproduzido** pelo Verifier (API 5214 não subida, por instrução) | PASS (evidência do autor) |

## Padrões da issue

- HTTP só em `Contract/api_client.gd` (rotas `/admin/*` em api_client.gd:132-149; `admin_window.gd` não usa transporte/HTTPRequest). PASS
- 3 shapes novos + `AdminContractTests` (.NET) + `shape_validator.gd` no cliente (M21, S1-S3). PASS
- Avisos por `toasts` (app.gd: `admin_window.warning → toasts.push`); textos em português; datas via `_date_text` do calendário (M05). PASS
- Testes headless: rota/parâmetros, fixture desenhada, 400/403/5xx, sem requisição por frame (M23). PASS
- Atalho F8 documentado em README § Controles; FPS registrado em README § Desempenho. PASS
- Nenhuma rota de servidor nova: `git diff --stat feat/godot-player...HEAD -- src` vazio. PASS
- Dívida registrada (não defeito): "Pausado por X desde <data>: <motivo>" para todos exige ator/motivo em rota pública; hoje outros clientes veem "Mundo pausado" (plan.md § Landing).

## Defeitos

Nenhum. Gap menor: M21b/M21c geram 1 `SCRIPT ERROR` junto da falha por assert.
