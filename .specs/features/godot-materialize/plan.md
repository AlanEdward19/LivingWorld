# Godot materializar NPC do agregado [Estado: Implementado, aguardando Verifier independente (2026-10-15)]

Issue #59, fatia H (última). Só cliente; nenhuma rota nova.

## Problem

Moradores do pool agregado só têm id e cidade (`lod` 2 em `GET /npcs/{id}`). O GET nunca materializa; `POST /npcs/{id}/materialize` é o comando explícito, e o cliente não o chama.

## Flow

1. `Contract/api_client.gd` (extended) - `materialize_npc` (POST) validado pelo shape `npc-inspect`
2. `Scenes/panels.gd` (extended) - ficha com `lod` 2 avisa "anônimo" e oferece "Ver em detalhe"; ao responder, mostra a ficha completa
3. `Scenes/app.gd` (extended) - avisos do painel viram toast
4. `ApiContractTests` (extended) - o POST devolve a mesma forma congelada da ficha

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | o servidor materializa o NPC (comando explícito do jogador, já existia); cliente não grava nada |
| contract | nenhum shape novo (reusa `npc-inspect`) |

## Landing

- Nada difícil de reverter.

## Criteria

1. WHEN a ficha é de um morador anônimo (`lod` 2) THEN SHALL mostrar o aviso e "Ver em detalhe"; detalhado ou arquivado SHALL NOT mostrar
2. WHEN o GET mostra o anônimo THEN SHALL NOT materializar (só o comando explícito)
3. WHEN o jogador aperta o botão THEN SHALL `POST /npcs/{id}/materialize` uma vez e trocar para a ficha completa; já detalhado não repete
4. IF 404/5xx ou corpo fora do contrato THEN SHALL avisar e manter a ficha anônima; resposta tardia de outro NPC é descartada
5. The system SHALL manter `stress:500` dentro de ±3 FPS (sem janela nova: só um botão na ficha)

**Independent test:** `bash scripts/client-test.sh --filter test_materialize`.

## Out of scope

Marcador de anônimo na lista de pessoas (o diretório não informa `lod`).

## Performance

Sem janela nova nem `_process`: um botão na ficha existente.
