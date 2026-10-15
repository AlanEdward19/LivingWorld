# Verificação independente — #59 fatia G (tecnologias da cultura)

Verifier de contexto em branco (não escreveu o código). HEAD `5183ba2` (`feat/godot-technologies`, detached).
Diff: `git diff feat/godot-economy...HEAD` (10 arquivos, nenhum em `src/` → nenhuma rota nova).
Entradas: `gh issue view 59`, `plan.md`, o diff. Mutações aplicadas por script (troca de trecho → `client-test.sh
--filter test_technologies` → restauração do original); `git status` limpo ao fim (exceto este arquivo).

## Veredito: **PASS** (5/5 critérios do plano; padrões da issue atendidos)

## Gates (rodados pelo Verifier)
- `bash scripts/client-test.sh` → exit 0, `client-test: 209 passed, 0 failed`, sem SCRIPT ERROR.
- `bash scripts/client-test.sh --filter test_technologies` → `10 passed, 0 failed`.
- `bash scripts/verify.sh` → exit 0, `check-docs: OK`, Unit `Passed: 2193, Skipped: 3`, `verify: OK`.
- `dotnet test tests/LivingWorld.Tests.Integration --filter "TechnologyContractTests|ApiContractTests"` → `Passed: 12, Failed: 0`.

## Critérios
| # | Critério (plan.md) | Evidência | Resultado |
|---|---|---|---|
| 1 | Botão só com itens; vazio/erro oculta; trocar NPC reavalia | `app.gd` `_probe_technologies` (`response.ok and not body.is_empty()`, guarda `culture_id == npc_panel.culture_id`); `panels.gd` zera `tech_button.visible` em `show_npc` e emite `culture_shown`; testes `test_the_button_appears_only…`, `test_a_probe_error…`, `test_a_server_error_in_the_probe…` | PASS |
| 2 | `GET /cultures/{id}/technologies`; chave, "variação de", categoria, data do calendário, adoção, 3 barras ritmo/carga/alcance na escala do maior eixo | `api_client.gd` `technologies()` → `_fetch(..., "culture-technologies")`; `technology_window.gd` `_tech_node`; `app.gd` injeta `technology.date_of = _date_text`; ordem `["pace","load","reach"]` confere com `EmergenceRules.cs:25`; rota existe em `CultureTechnologyEndpoints.cs:12` | PASS |
| 3 | Vazio / 404 / 5xx com mensagem própria (5xx avisa); corpo fora do contrato vira aviso | `_render`: 404 → "Esta cultura não existe mais.", !ok → "Não foi possível carregar (erro N / fora do formato)" + `warning`; vazio → "ainda não descobriu…"; `test_empty_404_and_error_states_and_contract_check` | PASS |
| 4 | Só GET; janela fechada não pede nada; Esc fecha | `close()` + guarda `mine != _request or not is_open`; sem `_process`; Esc em `app.gd _unhandled_input`; testes de só-leitura, por frame, Fechar/Esc | PASS |
| 5 | `stress:500` com janela aberta ±3 FPS | Evidência do autor em `client/godot/README.md` § Desempenho e `plan.md`: sem 75,5/75,0/74,9, com 77,0/75,8/74,5. **Não reproduzido** (instrução: não subir API na 5214) | PASS (evidência do autor) |

## Mutações (todas reprovam POR ASSERT salvo nota)
| Mutação | Teste que reprova (mensagem) |
|---|---|
| 5xx na checagem deixa o botão visível (`app.gd`) | `test_a_server_error_in_the_probe…` "5xx na checagem: sem botão esperado false, veio true" |
| Remover só `mine != _request` (`technology_window.gd`) | `test_overlapping_requests…` "esperado 1, veio 2" |
| Remover `technology.date_of = _date_text` | `test_the_app_formats_technology_dates…` "descoberta em tick 50" |
| Remover `culture_id == npc_panel.culture_id` | `test_a_probe_error…` "resposta velha da cultura 1… esperado false, veio true" |
| Eixo "Carga"→"Peso" | `test_technologies_show_key_axes…` nomes dos eixos |
| Botão Fechar desligado | `test_close_button…` "o botão Fechar fecha esperado false" |
| Desenhar após `close()` (guarda inteira `if false`) | `test_close_button…` "esperado 0, veio 2" + overlapping |
| `_process` pedindo com a janela aberta | `test_an_open_window…` "esperado 1, veio 6" |
| Botão sempre visível / nunca visível / sem `culture_shown` | 3 testes do app reprovam em cada um |
| Não zerar o botão em `show_npc` | `test_a_probe_error…` "erro na checagem: botão oculto" |
| Cultura errada (`culture_id + 1` no painel; `show_culture(2)` no app) | `test_the_button_appears…` "abre a cultura do NPC esperado 1, veio 2" |
| Título sem chave / sem "variação de" | `test_technologies_show_key_axes…` + overlapping |
| Eixos trocados (`vector[2 - i]`) | "barras = eixos do vetor esperado [2,4,6], veio [6,4,2]" |
| Escala fixa 10 / escala sem máximo | "escala = maior eixo esperado 6.0, veio 10.0" / barras [1,1,1] |
| Plural sempre "s" / nunca "s" | "1 cidades" / "3 cidade" |
| Mensagem vazia "" / sem ramo 404 / 5xx "" / sem aviso | `test_empty_404…` reprova em cada um |
| Shape ignorado (`""`) | `test_empty_404…` "corpo fora do contrato vira aviso esperado 2, veio 1" (também SCRIPT ERROR, mas o assert reprova) |
| Esc desligado | `test_the_button_appears…` "Esc fecha esperado false, veio true" |
| Escrita: POST extra / GET trocado por POST | `["POST …","GET …"]` e "só leitura esperado []" / lista de chamadas `["POST …"]` (com SCRIPT ERROR adicional) |
| `shape.culture-technologies.json` `key` string→number | `TechnologyContractTests.Culture_technologies_shape…` Failed (`ApiContractTests.cs:218`) |

## Padrões da issue
- HTTP só em `api_client.gd` (cenas usam `api.technologies`); shape novo + teste .NET via `ApiContractTests.ShapeOf/AssertMatchesContract`
  (em classe própria `TechnologyContractTests`, não dentro de `ApiContractTests` — aceito, usa o mesmo mecanismo). PASS
- Textos em português, avisos por `toasts` (`technology.warning → toasts.push`). PASS
- Datas via `/world/calendar` (`_date_text`, mutação acima). PASS
- Testes headless (rota+parâmetros, fixture desenhada, 404/5xx, sem requisição por frame). PASS
- Atalho/controle documentado em README § Controles (linha "Tecnologias"); medição registrada. PASS
- Critério comum 5 (grep de todas as rotas / atualizar `godot-main-menu/plan.md`): não se aplica — G não é a última fatia (H aberta).

## Observações (não bloqueantes)
- Mutante equivalente sobrevive: remover `_request += 1` em `close()` — `not is_open` já descarta a resposta tardia e
  `show_culture` sempre incrementa `_request`; sem efeito observável.
- Em `test_a_probe_error…` a sub-asserção do 500 é fraca (o `release` é emitido antes da requisição; o `false` vem do
  reset em `show_npc`). Coberto de verdade por `test_a_server_error_in_the_probe_hides_the_button_for_real`.
- Shape congela `variationOfKey` como `"null"` (aceita qualquer valor): o tipo da chave de origem não é verificado; decisão
  registrada no `plan.md`.
- `verify.sh` roda só Unit; os testes de contrato (Integration) foram rodados à parte (12/12).
