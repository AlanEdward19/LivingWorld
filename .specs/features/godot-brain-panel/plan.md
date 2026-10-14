# Godot brain panel (COG-11) [Estado: Implementado, aguardando Verifier independente (2026-10-14)]

Issue #58, fatia 1. A fatia 2 (sandbox "E se…?") fica de fora: exige rota nova (contrato público → perguntar + ADR).

## Problem

Objetivo técnico #5: ver estímulo → ponderação → decisão de um NPC "em dados e em visual". A API já
devolve `cognitionTrace`, mas o cliente Godot não tem painel. Pior: o rastro só é gravado para NPC em
escopo observado (`cidade`/`interior`) e o cliente nunca chama `POST /observation/scope`, então o painel
nasceria vazio. Provado em 2026-10-14: com escopo `City` publicado, NPCs da cidade acumulam rastro; sem
escopo (ou após `scope:null`) o rastro para de crescer.

## Flow

Só cliente + 1 teste .NET; nenhuma rota nova, nada escrito no mundo além de watchlist/escopo (já existem).

1. `Contract/scope_observer.gd` (new) - publica o escopo da câmera, heartbeat de 10 s, `null` ao sair
2. `Contract/api_client.gd` (extended) - `watch_npc`, `unwatch_npc`, `observation_scope`
3. `Scenes/brain_panel.gd` (new) - linha do tempo + fluxo em 3 colunas + Vigiar + estados vazios
4. `Scenes/panels.gd` / `app.gd` (extended) - botão "Cérebro", atalho B, refresh por hora do mundo
5. `tests/contracts/shape.npc-inspect.json` - passa a validar um item de `cognitionTrace`

## Impact

| Front | What changes |
| --- | --- |
| domain | none |
| stored data | none - rastro é volátil, hash idêntico |
| contract | `shape.npc-inspect.json` mais estrito (item do rastro); sem campo novo na API |

## Landing

- Contrato congelado: regravar só com `LW_RECORD_CONTRACT=1` e o teste grava um rastro antes.

## Criteria

### S1: Escopo observado (P1)

1. WHEN a câmera entra em mundo/cidade/interior THEN o cliente SHALL enviar `POST /observation/scope` com `World` / `City`+cityId / `Building`+cityId+buildingId
2. WHILE há escopo THEN o cliente SHALL reenviá-lo a cada 10 s; ao sair SHALL enviar `scope: null`
3. WHEN há escopo `City` e passam ticks THEN NPC da cidade SHALL ter `cognitionTrace` não vazio; sem escopo, vazio (.NET)

### S2: Painel (P1) - AC1..AC7 da issue

4. AC1 3 entradas → 3 decisões, mais recente primeiro, com rótulo da ação vencedora
5. AC2 selecionar desenha N pressões, M oportunidades, K alternativas da fixture; vencedora destacada
6. AC3 `cognitionTrace: []` → mensagem de estado vazio e nenhum nó de fluxo; morto/arquivado → mensagem própria
7. AC4 Vigiar → `POST /npcs/{id}/watchlist`; Parar → `DELETE`; 4xx → aviso sem quebrar
8. AC5 painel fechado não faz requisição periódica
9. AC6 `stress:500` com painel aberto dentro de ±3 FPS (README § Desempenho)
10. AC7 shape valida um item de `cognitionTrace`; `ApiContractTests` e `test_shape_validator.gd` verdes

**Independent test:** `bash scripts/client-test.sh --filter test_brain_panel`.

## Out of scope

Sandbox de decisão (fatia 2), recalcular utilidade no cliente, persistir ou gravar rastro de NPC não observado.

## Performance

Antes (M1 Pro, `stress:500`, 2026-10-14): 77,3 / 78,3 / 75,6 FPS. Depois, sem painel 79,4 / 80,0 / 77,7; com painel aberto 77,2 / 77,5 / 76,4 (AC6 ok). Registro em `client/godot/README.md`.

## Achado

`shape.npc-inspect.json` exigia `spouse`, `household` e `currentScope.cityId` objetos: a ficha de qualquer NPC solteiro reprovava no contrato e nunca abria no cliente real. Agora esses campos opcionais são `"null"` (qualquer valor), com teste.
