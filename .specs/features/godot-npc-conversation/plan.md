# Godot conversa com NPC [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Issue #59, fatia A. Fecha o objetivo técnico #3 ("conversar com NPC") no cliente. Nenhuma rota nova.

## Problem

`/conversations/start|send|end` existe e é testada, mas o cliente Godot nunca as chama: o jogador não
consegue conversar com um NPC.

## Flow

Só cliente + 1 teste de contrato .NET; o servidor decide aceite, resposta e emoção (llm-boundary).

1. `Contract/api_client.gd` (extended) - `conversation_start|send|end`
2. `Scenes/conversation_window.gd` (new) - janela de chat, "pensando…", prazo de 8 s, 404/409
3. `Scenes/panels.gd` / `app.gd` (extended) - botão "Conversar" e atalho T, Esc encerra
4. `tests/contracts/shape.conversation-{start,send}.json` - formas congeladas (`ApiContractTests`)

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - o cliente não aplica `proposedActions` |
| contract | 2 shapes novos; nenhum campo novo na API |

## Landing

- `sessionId` fica `"null"` no contrato: recusa devolve nulo.

## Criteria

1. WHEN o jogador pede conversa THEN o cliente SHALL enviar `POST /conversations/start {npcId}`; só `Accepted` abre a janela, `RejectedBusy|Hostile|Unavailable` mostram o motivo em português
2. WHEN envia uma fala THEN SHALL enviar `{sessionId, message}`, mostrar "pensando…" e depois `dialogue` e `emotion` (cor e nome)
3. WHILE aguarda THEN SHALL desistir após 8 s (5 s do servidor + margem) e descartar resposta tardia
4. WHEN o servidor responde 404 ou 409 THEN SHALL encerrar a janela com aviso (`npc-dead` e `session-ended` distintos)
5. WHEN a janela fecha (Esc, botão, voltar ao menu) THEN SHALL enviar `POST /conversations/end {sessionId}` uma vez
6. The system SHALL NOT chamar nenhuma rota de escrita além das três; `proposedActions` é ignorado
7. The system SHALL manter `stress:500` com a janela aberta dentro de ±3 FPS

**Independent test:** `bash scripts/client-test.sh --filter test_conversation`.

## Out of scope

Provider real (#60), `AllowedActions` reais, histórico persistido entre sessões, múltiplas conversas.

## Performance

M1 Pro, `stress:500`, 2026-10-14: sem janela 79,4 / 78,2 / 78,8; com janela aberta 78,2 / 77,7 / 77,8.
