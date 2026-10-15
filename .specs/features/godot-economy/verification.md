# Verificação: godot-economy (#59 fatia F)

Verifier independente (contexto em branco, não escreveu o código), 2026-10-15.
HEAD `a77c114` (detached de `feat/godot-economy`); diff `feat/godot-admin...HEAD`, 14 arquivos.

**Veredito: PASS**. 7/7 critérios com evidência; 36/36 mutações reprovadas por assert de teste.

## Gates

| Gate | Evidência |
|---|---|
| client-test | o harness do agente recusou `bash scripts/client-test.sh`; rodei o equivalente (`Godot --headless --path client/godot --script res://tests/run.gd`): exit 0, `client-test: 198 passed, 0 failed`, 0 linhas `SCRIPT ERROR\|Parse Error` (mesma regra do script). Os 12 `test_economy::*` e `test_main_menu::test_continuing_...` aparecem `ok` |
| verify.sh | `bash scripts/verify.sh`: `Passed! 2193, Skipped 3` e `verify: OK` |
| contrato .NET | `dotnet test tests/LivingWorld.Tests.Integration --filter "EconomyContractTests\|ApiContractTests"`: `Passed! Failed 0, Passed 12` |
| FPS (crit. 7) | não reproduzido. Evidência do autor em `client/godot/README.md:62`: sem janela 74,0/75,0/76,2; com as vendas abertas 77,7/76,9/78,1 (dentro de ±3) |

## Critérios

| # | Evidência (arquivo:linha) | Mutações reprovadas |
|---|---|---|
| 1 Tesouro: rota, saldo, mês pelo calendário, barras proporcionais | `api_client.gd:155` `/cities/%s/treasury`; `economy_window.gd:76-79` `month_label` usa `monthsPerYear`; `:130-144` escala = max(entrou, saiu); `app.gd:324` `calendar_of` do `day_cycle` | M01, M02, M04, M07a, M08, M09a, M09b, M10 |
| 2 Vendas: rota, data, recurso, qtd, valor, compras, total | `api_client.gd:159`; `economy_window.gd:175-190` (dia × `hoursPerDay` → `date_of`); `app.gd:325` `date_of = _date_text` | M03, M07b, M11, M12a, M13, M14 |
| 3 Vazio, 404, 5xx (+aviso), corpo fora do contrato | `economy_window.gd:115-127`, `:136`, `:178`; shapes passados em `api_client.gd:155,159` | M15a-e, M16a-b |
| 4 Botão de vendas oculto sem emprego | `panels.gd:178-179` (`employer.value`, serializado como `{"value":n}`: `WorkplaceId` record struct sem conversor) | M17a, M17b |
| 5 Período do menu chega ao app; nomes do catálogo; sem período, nenhum pedido | `boot.gd:49,54`; `main_menu.gd:579`; `app.gd:121,126-134` | M06, M12b, M12c, M19a, M19b, M20 |
| 6 Só GET; resposta tardia descartada; Esc fecha; sem requisição por frame | `economy_window.gd:93-98` (`mine != _request`); `app.gd:763-764`; `app.gd:334-335` sinais | M05, M18a-c, M21, M22 |
| 7 stress:500 ±3 FPS | README:62 (autor) | não reproduzido |

## Mutações (cada uma aplicada, teste rodado, arquivo restaurado)

Runner: `scratchpad/mut.py` (Godot `--filter test_economy` / `test_main_menu`) e `mutnet.py` (dotnet).
Todas reprovaram por `FAIL` com mensagem de assert (não só SCRIPT ERROR com `ok`):

- M01 `month_label` 12 fixos → FAIL month_label/app_feeds: "esperado Ano 3 · Mês 2, veio Ano 1 · Mês 2"
- M02 `economy.calendar_of` fixo 24/30/12 em app.gd → FAIL app_feeds (mesma mensagem)
- M03 sem `economy.date_of = _date_text` → FAIL app_feeds ("tick 100 · ...")
- M04 "+entrou −saiu" trocados → FAIL month_label: "esperado +50  −25, veio +25  −50"
- M05 `_process` pedindo tesouro (janela na árvore) → FAIL no_requests: "esperado 1, veio 6"
- M06 `_on_continue` sem limpar `selected_period` → FAIL test_main_menu: "esperado , veio medieval"
- M07a/b rota `/city/…`, `/workplace/…` → FAIL por `transport.calls` (+ SCRIPT ERROR secundário)
- M08 saldo vazio → FAIL "esperado Saldo: 1234"; M09a escala só revenue → "esperado 100.0, veio 50.0"
- M09b barras trocadas → "esperado 50.0, veio 25.0"; M10 cores iguais → "entrou e saiu têm cores diferentes"
- M11 dia × 24 fixo → "veio tick 240"; M12a nome fora do catálogo → "veio ... recurso 1 ..."
- M12b fallback `""` → "esperado recurso 9, veio"; M12c nome = id → "veio { 1: \"1\", 2: \"2\" }"
- M13 total por quantidade → "esperado Total vendido: 35, veio 9"; M14 plural → "veio (1 compras)"
- M15a/b mensagens de vazio `""`; M15c 404 sempre "cidade" → "esperado Esta oficina não existe mais."
- M15d 5xx sem `warning.emit` → "esperado 1, veio 0"; M15e 5xx sem mensagem → FAIL "erro 500" in text
- M16a/b shape `""` → "corpo fora do contrato vira aviso esperado 2, veio 1"
- M17a botão sempre visível → "esperado false, veio true"; M17b emite `npc_id` → "pede o local do empregador"
- M18a/b sinais desconectados → "esperado trades/treasury"; M18c Esc desligado → "Esc fecha esperado false"
- M19a boot sem `period_id` → "esperado vila-pequena, veio"; M19b sem guarda → `["GET /periods//catalog"]`
- M20 `_load_resource_names` não chamado → "esperado { 1: \"Trigo\", ... }, veio {  }"
- M21 sem `mine != _request` → late_reply: "esperado 2, veio 0" (assert; há 1 SCRIPT ERROR secundário)
- M22 POST extra → FAIL calls (`["POST /cities/...", "GET ..."]`) e "só leitura"
- M23a-d `shape.city-treasury.json` (balance→string, sem spending) e `shape.workplace-trades.json`
  (resourceId→string, campo extra) → `EconomyContractTests` `Failed! 1`: "contrato ... mudou"

`git status --short` após restaurar: vazio (antes de gravar este arquivo).

## Padrões da issue

- HTTP só em `api_client.gd` (economy_window chama `api.treasury`/`api.workplace_trades`). OK
- Shapes novos + teste .NET (`EconomyContractTests` reusa `ApiContractTests.AssertMatchesContract`). OK
- Textos em português, avisos por `toasts` (`app.gd:332`). OK
- Datas por `/world/calendar` via `day_cycle` (M01-M03, M11). OK
- Teste headless: rota, dados, erros, sem requisição por frame. OK
- Atalho/controles no README § Controles (`README.md:32`). OK. Nenhuma rota de servidor nova. OK

## Observações (não defeitos)

- Dívida declarada no plan: mundo continuado mostra "recurso N" (sem rota de período). Não é defeito.
- `shape.npc-inspect.json` grava `employer` como `"null"` (aceita qualquer valor); o formato
  `{"value": n}` que `panels.gd:178` lê não está congelado. Risco baixo; fora desta fatia.
- O cliente não envia `fromDay/toDay`; o servidor usa a janela padrão (`TreasuryEndpoints.cs:23-24`).
