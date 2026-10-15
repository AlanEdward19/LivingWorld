# Verificação independente — #59 fatia A (conversa com NPC)

Verifier com contexto em branco (não escreveu o código), 2026-10-14, branch `feat/godot-npc-conversation`
(HEAD `51df1da`), diff `feat/godot-brain-panel...HEAD`. Evidence-or-zero. **Resultado: PASS (7/7 + padrões).**

## Gates

| Comando | Saída |
| --- | --- |
| `bash scripts/client-test.sh` | `client-test: 121 passed, 0 failed`, exit 0 |
| `bash scripts/client-test.sh --filter test_conversation` | `17 passed, 0 failed` |
| `bash scripts/verify.sh` | `check-docs: OK` … Unit `Passed: 2193, Skipped: 3` · `verify: OK`, exit 0 |
| `dotnet test tests/LivingWorld.Tests.Integration --filter ApiContractTests` | `Passed: 10, Failed: 0` |

## Critérios (mutação aplicada → teste que reprova por assert; tudo revertido com `git checkout --`)

| # | Evidência no código | Mutação → teste reprovado | Veredito |
| --- | --- | --- | --- |
| 1 | `api_client.gd:84-86` POST `{npcId}`; `conversation_window.gd:89-97` só `Accepted` abre, `REFUSALS` em pt | recusa abre a janela → `test_start_posts…` ("RejectedBusy: a janela não abre"); texto hostil trocado → idem; corpo `{id}` → idem ("pede a conversa") | PASS |
| 2 | `send()` `:110-133` corpo `{sessionId,message}`, "pensando…", `_line_node` `:183-196` nome+cor | cor `happy`→`c7ced9` e override removido → `test_emotion…` ("cor da emoção"); sem "pensando" → `test_thinking…`; corpo `text` → `test_send…` | PASS |
| 3 | `REPLY_TIMEOUT := 8.0` `:9`; `_deadline.start()` `:121`; guarda `mine != _request` `:123` | `80.0` → `test_reply_deadline…` (esperado 8.0, veio 80.0); sem `_deadline.start()` → `test_thinking…` ("prazo de 8 s foi armado"); `if false: return` → "resposta tardia não entra" (esperado 1, veio 2) | PASS |
| 4 | `:129-132` 404 e 409 → `_finish`, `npc-dead` × `session-ended` distintos | 404 só avisa → `test_404_and_409…` ("404 fecha a janela"); 409 idem; `"npc-dead" in …`→`true` → "409 avisa: [Ana morreu.]" | PASS |
| 5 | `end_conversation()` `:136-141` idempotente; Esc `app.gd:618`, botão `:52`, campo `:70-73`, menu `app.gd:425-426` | sem `/end` → 5 testes (asserts + SCRIPT ERROR); `/end` duplicado → 6 testes ("fechar de novo não reenvia" 1≠2); sem guarda → idem; sessão errada → `[{sessionId:-1}]`; botão Encerrar desligado → `test_end_button…`; Esc no campo sem `end` → `test_esc_inside…`; Esc do app desligado → `test_app_button…` ("Esc encerra"); `release_observation` sem `await end` → `test_returning_to_the_menu…` (esperado 1, veio 0) | PASS |
| 6 | só 3 rotas em `api_client.gd:84-96`; `proposedActions` não é lido | POST `/simulation/pause` em `start()`, `send()` (2 testes cada) e `end_conversation()` (`test_full_cycle…`); aplicar `proposedActions` como POST `/players/1/action` → `test_send…` + `test_full_cycle…` | PASS |
| 7 | sem `_process` na janela; nada por frame | `_process` chamando `conversation_start` → `test_open_window…` (esperado 1, veio 6); FPS reproduzido abaixo | PASS |

Contrato: nome do shape `"conversation-start"`/`"conversation-send"` → `""` em `api_client.gd` →
`test_api_rejects_start_and_send…` reprova (start/send "reprova esperado false, veio true"). No .NET,
`shape.conversation-send.json` `intent: number` e `shape.conversation-start.json` `decision: number` →
`Conversation_start_and_send_shapes_match_the_committed_contract` **Failed** (`ApiContractTests.cs:201`).

Entradas (pedidas): botão "Conversar (T)" desligado em `panels.gd` → `test_app_button…` ("o botão Conversar
abre"); sinal não ligado em `app.gd` → idem; tecla T desligada → "T conversa com o NPC selecionado".

5xx no envio: remover o `else: warning.emit` → `test_a_5xx_on_send…` reprova por assert ("avisa o erro"
esperado 1, veio 0) **e** também gera SCRIPT ERROR (`warnings[0]` fora do limite) — o assert vem antes.

## Padrões da issue

- HTTP só em `Contract/api_client.gd` (grep: nenhum `transport.` em `Scenes/conversation_window.gd`/`panels.gd`). PASS
- Shapes novos `tests/contracts/shape.conversation-{start,send}.json` + entrada no `ApiContractTests`, validados no cliente (`_checked`). PASS
- Textos em pt, avisos por `toasts` (`app.gd` `conversation.warning → toasts.push`); 4xx/5xx viram aviso. PASS
- Atalho T em README § Controles (`README.md:28`) e na linha de ajuda; FPS em § Desempenho. PASS
- Nenhuma rota de servidor nova (diff só toca `client/godot`, `tests/`, `.specs`). `check-docs: OK`; `plan.md` 49 linhas. PASS

## Desempenho (critério 7) — reproduzido

API local (`ADMIN_TOKEN=… LW_DB=/tmp/lw-verA4.db bash scripts/play.sh --api-only`, mundo pausado às 05:00 para o NPC 1
ficar acordado; a 1ª tentativa às 23:40 recusou com "não está disponível" — recusa correta, descartada).
Janela confirmada aberta por captura (`talk:1;say:olá`, resposta "(irritado)" colorida). `stress:500`, M1 Pro:

| Lote | sem janela | com janela | só NPC selecionado |
| --- | --- | --- | --- |
| 1 (intercalado) | 88,2 / 89,3 / 86,1 | 84,6 / 85,2 / 84,1 | — |
| 2 (intercalado) | 86,9 / 86,5 / 88,1 | 87,1 / 86,4 / 86,2 | 86,8 / 86,6 / 85,6 |

Média 6 rodadas: sem 87,5, com 85,6 (Δ 1,9, dentro de ±3). O lote 1 isolado deu Δ 3,2 (fora por 0,2); o lote 2
com braço de controle (ficha aberta, sem janela) deu Δ ≤ 0,8, então o lote 1 é ruído/confusão da ficha aberta.
Teto sem vsync diferente do autor (~79), por isso números absolutos maiores. API encerrada (`pkill`).

## Observações (não bloqueiam)

- Mutação extra sobrevivente: tirar `not conversation.input.has_focus()` do atalho T não reprova nenhum teste. Na
  prática o `LineEdit` focado consome a tecla antes de `_unhandled_input`, então o risco é baixo; sem teste.
- O teste .NET força `sessionId` a `"null"` (qualquer valor), então o tipo de `sessionId` não é congelado — declarado em Landing.
- `sessionId` 0 é válido no servidor (visto na API real); o cliente usa `-1` como sentinela, compatível.

`git status` limpo ao fim, exceto este arquivo.
