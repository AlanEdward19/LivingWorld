# Godot crenças da cidade [Estado: Verificado, PASS (Verifier independente, 2026-10-15)]

Issue #59, fatia C. Só cliente + contrato congelado; reaproveita a janela de narrativa da fatia B.

## Problem

`GET /history/beliefs?communityId=` existe (Fase 10) mas o cliente não a chama: o jogador não vê o que cada cidade
acredita ter acontecido (relato já distorcido), nem o quão longe da verdade isso está.

## Flow

1. `Contract/api_client.gd` (extended) - `beliefs(city_id)`; shape `history-beliefs` congelado
2. `Scenes/narrative_window.gd` (extended) - modo `beliefs`: texto, magnitude distorcida, data e barra de distância do fato
3. `Scenes/panels.gd` / `app.gd` (extended) - botão "O que a cidade acredita" na ficha da cidade
4. `NarrativeContractTests` (extended) - semeia um relato no cânone da cidade e congela a forma

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - só leitura |
| contract | 1 shape novo; nenhum campo novo na API |

## Landing

- Nunca chamar `/history/truth` (rota de motor, `X-Engine-Scope`); visão de verdade é do console admin (#62).

## Criteria

1. WHEN o jogador abre a aba THEN o cliente SHALL chamar só `GET /history/beliefs?communityId=<cidade>` (nunca `/history/truth`)
2. The system SHALL mostrar, por crença, o texto, a magnitude distorcida, a data (calendário do servidor) e a distância do fato em barra (0 a 100%, limitada)
3. IF lista vazia, 404 ou 5xx THEN SHALL mostrar mensagem própria e avisar (404/5xx); corpo fora do contrato vira aviso
4. The system SHALL usar só leitura (GET)
5. The system SHALL manter `stress:500` com a janela aberta dentro de ±3 FPS (mesma janela da fatia B)

**Independent test:** `bash scripts/client-test.sh --filter test_beliefs`.

## Out of scope

`/history/truth`; editar crenças; filtrar por fato.

## Performance

Mesmo componente da fatia B (ver `godot-narratives/plan.md`): `stress:500` com a janela aberta sem diferença medida.
