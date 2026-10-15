# Godot crônicas, biografias e relatos [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Issue #59, fatia B. Só cliente (+ contratos congelados); nenhuma rota nova.

## Problem

`/narratives/biographies|chronicles|reports` existe (Fase 12) mas o cliente nunca as chama: o jogador não lê a história
que a comunidade acredita.

## Flow

1. `Contract/api_client.gd` (extended) - `biography`, `chronicle`, `reports`; shapes `narrative-*` congelados
2. `Scenes/narrative_window.gd` (new) - biografia, crônica com seletor de período e relatos com barra de confiança
3. `Scenes/panels.gd` / `app.gd` (extended) - botões Biografia (ficha do NPC) e Crônica (ficha da cidade), tecla R, Esc fecha
4. `NarrativeContractTests` (new .NET) - semeia cidade, fato e relato e congela as 3 formas

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - só leitura (GET) |
| contract | 3 shapes novos; nenhum campo novo na API |

## Landing

- Nada difícil de reverter.

## Criteria

1. WHEN o jogador abre a biografia THEN o cliente SHALL chamar `GET /narratives/biographies/{npcId}` e mostrar prosa e afirmações com os eventos que as sustentam
2. WHEN abre a crônica da cidade THEN SHALL chamar `GET /narratives/chronicles?location&periodStart&periodEnd&topK=5` com o período (mês/ano/década) convertido pelo calendário do servidor e o tick do relógio, e recarregar ao trocar o período
3. WHEN abre os relatos (R) THEN SHALL listar `GET /narratives/reports` com meio em português e barra de confiança proporcional (0 a 100%)
4. IF vazio, 404 (biografia) ou 5xx THEN SHALL mostrar mensagem própria; 5xx também avisa; resposta fora do contrato vira aviso
5. WHILE fechada THEN SHALL NOT fazer requisição; resposta tardia de outro assunto é descartada
6. The system SHALL NOT usar nenhuma rota além das três de leitura
7. The system SHALL manter `stress:500` com a janela aberta dentro de ±3 FPS

**Independent test:** `bash scripts/client-test.sh --filter test_narrative`.

## Out of scope

Edição de narrativa, `/history/truth` (rota de motor), paginação.

## Performance

M1 Pro (máquina ocupada com verificadores), `stress:500`, 2026-10-14: sem janela 75,0 / 72,4 / 71,8; com a biografia aberta 73,4 / 72,6 / 73,1.
