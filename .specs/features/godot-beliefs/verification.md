# Verificação — Godot crenças da cidade (#59 fatia C)

Verifier independente (contexto em branco, não escreveu o código), 2026-10-14.
HEAD `42ab327` (`git checkout --detach feat/godot-beliefs`); diff `git diff feat/godot-narratives...HEAD` (9 arquivos).
Entradas: `gh issue view 59`, `plan.md`, o diff. Mutações aplicadas por substituição exata e revertidas com
`git checkout -- <arquivo>`; `git status --short` vazio ao fim (exceto este arquivo).

**Veredito: PASS (5/5 critérios; padrões da issue atendidos; FPS não reproduzido, herdado da fatia B).**

## Gates

| Comando | Saída |
|---|---|
| `bash scripts/client-test.sh` | `client-test: 149 passed, 0 failed`, sem SCRIPT ERROR |
| `bash scripts/client-test.sh --filter test_beliefs` | `6 passed, 0 failed` |
| `bash scripts/verify.sh` | `Passed: 2193, Skipped: 3` · `verify: OK` |
| `dotnet test tests/LivingWorld.Tests.Integration --filter "NarrativeContractTests\|ApiContractTests"` | `Passed: 13, Failed: 0` |

## Critérios

| # | Critério | Evidência | Status |
|---|---|---|---|
| 1 | Só `GET /history/beliefs?communityId=<cidade>`, nunca `/history/truth` | `api_client.gd:103-104` (`uri_encode`, shape `history-beliefs`); `test_beliefs.gd:30` compara a lista exata de chamadas; `grep history/truth client/godot/Contract client/godot/Scenes` só acha o comentário `api_client.gd:102` | PASS |
| 2 | Texto, magnitude distorcida, data (calendário), barra 0–100% limitada | `narrative_window.gd:204-236`; `app.gd:258` liga `date_of = _date_text` (calendário de `/world/calendar`, `app.gd:105`, `day_cycle.gd:32`); testes `test_beliefs.gd:44-47,126` | PASS |
| 3 | Vazia / 404 / 5xx com mensagem própria e aviso; corpo fora do contrato vira aviso | `narrative_window.gd:172-176,206-207`; `test_beliefs.gd:51-71,74-85` | PASS |
| 4 | Só leitura (GET) | nenhuma chamada de transporte em cenas (`grep transport.request\|HTTPRequest` vazio em `narrative_window.gd`/`panels.gd`); `test_beliefs.gd:88-95` filtra tudo que não é `GET /history/beliefs` | PASS |
| 5 | `stress:500` com a janela aberta ±3 FPS | Mesmo componente/janela da fatia B; evidência herdada de `godot-narratives/plan.md:47` (sem janela 75,0/72,4/71,8; com 73,4/72,6/73,1). Sem `_process` em `narrative_window.gd` (sem requisição por frame). **Não reproduzido** aqui (API na 5214 não subida, por instrução) | PASS (herdado) |

## Mutações (todas revertidas)

| Mutação | Arquivo | Teste que reprovou (assert) |
|---|---|---|
| rota → `/history/truth` | api_client.gd | `..._never_the_truth_route` (veio `GET /history/truth?...`), `test_only_the_beliefs_route_is_read` + 2 |
| sem `uri_encode` | api_client.gd | `..._never_the_truth_route` (veio `communityId=city a`) |
| sem `communityId` | api_client.gd | `..._never_the_truth_route`, `test_city_sheet_button_...` (esperado 1, veio 0) |
| shape `""` | api_client.gd | `test_a_body_outside_the_frozen_shape_...` (esperado 1 aviso, veio 0) |
| magnitude ← `distanceFromFact` | narrative_window.gd | `test_each_belief_...` (veio `magnitude 0.35`) |
| data sem `date_of` (`tick N`) | narrative_window.gd | `test_each_belief_...` + `test_city_sheet_button_...` |
| `app.gd` sem `narrative.date_of = _date_text` | app.gd | `test_city_sheet_button_...` ("data pelo calendário do app: ... tick 12") |
| barra sem `*100` | narrative_window.gd | `test_each_belief_...` (esperado 35.0, veio 0.35) |
| barra sem clamp + `max_value=1000` | narrative_window.gd | `test_each_belief_...` (esperado 100.0, veio 180.0) |
| mensagem vazia `""` | narrative_window.gd | `test_empty_error_and_404_states` |
| mensagem de erro `""` | narrative_window.gd | `test_empty_error_and_404_states` (`"erro 404" in empty_text`) |
| 404/5xx sem `warning.emit` | narrative_window.gd | `test_empty_error_and_404_states` (1→0), `test_a_body_outside_...` |
| só 5xx sem aviso | narrative_window.gd | `test_empty_error_and_404_states` (esperado 2, veio 1) |
| botão sem `pressed.connect` | panels.gd | `test_city_sheet_button_...` (modo vazio; 0 chamadas) |
| botão emite outro id | panels.gd | `test_city_sheet_button_...` (esperado 1, veio 0) |
| `open_beliefs` passa `""` | app.gd | `test_city_sheet_button_...` |
| sinal `beliefs_requested` não conectado | app.gd | `test_city_sheet_button_...` |
| `shape.history-beliefs.json` `distortedTick: string` | contrato | `.NET History_beliefs_shape_matches_the_committed_contract` (Failed 1) |
| shape sem `distanceFromFact` | contrato | idem (Failed 1) |
| escrita extra `POST /history/beliefs/seen` | narrative_window.gd | `..._never_the_truth_route`, `test_only_the_beliefs_route_is_read` |
| título → "Crônica de Alfa" | narrative_window.gd | `..._never_the_truth_route` (esperado `O que Alfa acredita`) |

**Mutante equivalente (registrado, não é FAIL):** remover só o `clampf` mantendo `max_value = 100` passa
os 6 testes porque `ProgressBar`/`Range` já limita `value` a `[min_value, max_value]`; o comportamento observável
(barra e rótulo em 100%) não muda. A variante não-equivalente (clamp removido e teto alterado) é pega.

## Padrões da issue

- HTTP novo só em `api_client.gd` (`beliefs`), shape congelado `tests/contracts/shape.history-beliefs.json` +
  `NarrativeContractTests.cs:23-38` (semeia relato no cânone e compara forma) — confere com `BeliefResponse`
  (`src/LivingWorld.Api/Narrative/HistoryEndpoints.cs:7`).
- Textos em PT; avisos via sinal `warning` → `toasts.push` (`app.gd:259`). Data pelo calendário do servidor.
- Testes headless cobrem rota/parâmetros, dados da fixture, 404/5xx/contrato; sem requisição por frame.
- README § Controles atualizado (`client/godot/README.md:29`). Nenhuma rota de servidor nova.

## Observações (não bloqueiam)

- Cliente: alterar o JSON do shape não reprova testes Godot (só o .NET); a fixture inválida falta campos inteiros.
- FPS da fatia C não medido isoladamente; depende da equivalência com a janela da fatia B.
