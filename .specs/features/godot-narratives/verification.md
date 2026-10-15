# Verificação independente — #59 fatia B (narrativas) [Resultado: PASS]

Verifier com contexto em branco (não escreveu o código), 2026-10-14. HEAD `adeb6f6` (`feat/godot-narratives`,
detached). Diff: `git diff feat/decision-sandbox...HEAD` (13 arquivos, +751/-5). Entradas: issue #59 (fatia B,
padrões e critérios comuns), `plan.md`, o diff. Cada mutação foi aplicada por script, rodou
`godot --headless --path client/godot --script res://tests/run.gd -- --filter test_narrative` e o arquivo foi restaurado;
todas as falhas são de assert (nenhum `SCRIPT ERROR`).

## Gates

| Gate | Evidência | Resultado |
| --- | --- | --- |
| client-test | `godot --headless --script res://tests/run.gd` (corpo de `scripts/client-test.sh`; o harness do agente recusa `bash scripts/*.sh`) → `client-test: 145 passed, 0 failed`, 0 `SCRIPT ERROR/Parse Error`; 14/14 `test_narrative` ok | PASS |
| verify.sh | passos rodados um a um: check-docs (maior .md = 100 linhas; plan.md 47) · `dotnet build -c Release -warnaserror` 0 erros · `dotnet format --verify-no-changes` exit 0 · Unit `Category!=Scenario` 2193 ok / 0 falhas / 3 skip | PASS |
| Contrato .NET | `dotnet test tests/LivingWorld.Tests.Integration --filter "NarrativeContractTests\|ApiContractTests"` → 12 ok, 0 falhas | PASS |

## Critérios do plan.md

| # | Critério | Evidência | Mutação → teste que reprova | Resultado |
| --- | --- | --- | --- | --- |
| 1 | Biografia: `GET /narratives/biographies/{id}`, prosa + afirmações com eventos | `api_client.gd:90-91`, `narrative_window.gd:171-187` | M19 escrita extra → `test_biography_calls_the_route...` | PASS |
| 2 | Crônica `location/periodStart/periodEnd/topK=5`, calendário do servidor + tick do relógio, recarrega ao trocar | `api_client.gd:94-95`, `narrative_window.gd:107-113`, `app.gd:251-252` | M1 24×30 fixo e M2 12 meses fixo (janela) → `test_period_ticks...`, `test_chronicle_sends...`, `test_app_buttons...`; M3 `calendar_of` 24/30/12 em app.gd → `test_app_buttons...` ("usa o tick do relógio e o calendário"); M4 mês↔ano, M5 década=ano → `test_period_ticks...`; M6 sem topK, M7 sem location, M8 sem periodEnd → `test_chronicle_sends...`; M9 sem recarga → `test_chronicle_sends...` | PASS |
| 3 | Relatos (R): meio em português, barra 0–100% | `narrative_window.gd:190-213` | M10 sem `*100` e M11 meio cru → `test_reports_list_with_confidence_bars_and_labels` | PASS |
| 4 | Vazio/404/5xx com mensagem própria; 5xx avisa; fora do contrato avisa | `narrative_window.gd:158-175,192` | M12a/b/c msgs vazias → `test_biography_empty_404...`, `test_chronicle_empty_period_message`, `test_reports_empty_state`; M12d msg 404 vazia e M13 404 vira aviso ("404 não é erro de servidor esperado 0, veio 1") → `test_biography_empty_404...`; M14 5xx sem aviso → 3 testes; M15a/b/c shape `""` em biography/chronicle/reports → `test_responses_are_checked_against_frozen_shapes` / `test_chronicle_outside_the_contract...` | PASS |
| 5 | Fechada não requisita; resposta tardia descartada | `narrative_window.gd:93,97-103,128-129` | M16a `is_open` removido de `set_period` → `test_chronicle_outside_the_contract_and_closed_period_change` ("esperado 1, veio 2"); M16b descarte removido e M16c descarte só por `is_open` → `test_a_reply_that_arrives_after_switching_subject_is_ignored`; M20 requisição em `_process` → `test_closed_window_makes_no_requests...` ("esperado 1, veio 6") | PASS |
| 6 | Só as três rotas de leitura | `api_client.gd:88-99` (só `_fetch` = GET); nenhuma cena fala HTTP (`grep transport.request` vazio em `narrative_window.gd`/`panels.gd`) | M19 `api.conversation_end` em `refresh` → `test_only_get_routes_are_used` (+3) | PASS |
| 7 | `stress:500` ±3 FPS | Evidência do autor (README:56, plan.md): sem janela 75,0/72,4/71,8; com 73,4/72,6/73,1 (médias 73,1 vs 73,0). **Não reproduzido** pelo Verifier (API na porta 5214 não subida, por instrução) | n/a (critério de medição) | PASS (evidência do autor, não reproduzida) |

## Fiação da UI e padrões da issue

| Item | Evidência | Mutação → teste | Resultado |
| --- | --- | --- | --- |
| Botão Biografia / Crônica | `panels.gd:150-153,232-235`, `app.gd:260-261` | M17a, M17b, M17c (sinal não ligado) → `test_app_buttons_and_r_key...` (+`test_chronicle_button...`) | PASS |
| Tecla R / Esc | `app.gd:649-651,663-664` | M17d R desligada, M17e Esc → `test_app_buttons_and_r_key...` | PASS |
| `show_city` com `id` nos dois caminhos | escopo cidade `app.gd:413` (`store.city` traz `id`, `world_store.gd:96`); resumo do mundo `app.gd:732` | M18a sem id no escopo cidade e M18b sem id em `_show_city_summary` → `test_chronicle_button_gets_the_city_id_from_both_real_paths` | PASS |
| Shapes congelados + `ApiContractTests` | `tests/contracts/shape.narrative-{biography,chronicle,reports}.json`, `NarrativeContractTests.cs` | `prose`→number, `id`→string, `confidence`→string (um por vez) → `NarrativeContractTests` falha "contrato shape.narrative-*.json mudou" | PASS |
| HTTP só em `api_client.gd`; textos em português; avisos por toasts | `app.gd:258` liga `warning` a `toasts.push` | coberto por M14/M15 | PASS |
| Datas pelo calendário (nunca 24 h/30 d) | default de `calendar_of` (`narrative_window.gd:14`) é só fallback; app sobrescreve com `day_cycle` | M3 | PASS |
| Sem requisição por frame | sem `_process` na janela | M20 | PASS |
| Nenhuma rota de servidor nova | diff não toca `src/` | — | PASS |
| Atalho no README § Controles; FPS em § Desempenho | `client/godot/README.md:29,56` | — | PASS |

## Observações (não bloqueiam)

- Crônica sem cidade (`location` ausente) não é alcançável: `open_chronicle` ignora `city_id == ""` (`app.gd:529`).
- 400 da crônica cai no ramo genérico "erro 400" + aviso (`narrative_window.gd:163-166`); não há teste dedicado de 400,
  mas o ramo é o mesmo do 5xx, coberto por M14.
- Critério comum 5 (grep de todas as rotas da issue) só se aplica à última fatia.

## Estado final

`git status` limpo exceto este arquivo; nenhuma outra edição, nenhum commit.
