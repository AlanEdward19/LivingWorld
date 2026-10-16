# Godot zoom LOD [Estado: Implementado no cliente (2026-10-13)]

## Problem

O zoom ia de 3 a 48, saltava a cada passo da roda e o assentamento mal se distinguia do mapa-múndi
(mesmo mato, mesmas cores). De longe os 500 NPCs ainda eram corpos inteiros.

Quando isto entrar, o zoom é contínuo e acompanha o tamanho do mapa, o mapa-múndi afastado mostra NPCs
como bolhas e o assentamento tem chão próprio (rua de pedra e terra batida sob os prédios).

## Flow

Só cliente; presentation-only sobre a WorldStore (ADR-0022).

1. `camera_rig.gd` (exists, extended) - `zoom_smooth` (roda), `fit_to_map`, `bubble_size`
2. `app.gd` (exists, extended) - liga `bubble_zoom` só no escopo Mundo acima de `bubble_size`
3. `world_view.gd` (exists, extended) - `_settlement_cells`: cor do chão dentro do assentamento
4. `npc_actor.gd` (exists, extended) - `set_bubble`: esfera na cor da camisa, sem corpo nem animação

## Impact

| Front | What changes |
| --- | --- |
| domain | termos: bolha, chão do assentamento |
| stored data | none - hash idêntico, nada é escrito |

## Landing

- Nada aqui é difícil de reverter

## Criteria

### S1: Zoom (P1)

1. WHEN a roda gira THEN o zoom SHALL aproximar do alvo aos poucos, sem saltar
2. WHEN o mapa carrega THEN o zoom out máximo SHALL ser `max(48, 1,4 x lado do mapa)`
3. WHEN o zoom sai de `city_exit_size` na cidade THEN o escopo SHALL voltar ao mapa-múndi (já existia)

### S2: LOD e assentamento (P1)

4. WHILE no mapa-múndi acima de `bubble_size` THEN o NPC SHALL ser bolha, sem animar
5. WHILE no escopo cidade THEN o chão dentro dos limites SHALL ser rua e terra batida; no mundo, natural
6. The system SHALL manter o FPS de `--steps "stress:500"` igual ao de antes (sem regressão)

**Independent test:** `bash scripts/client-test.sh --filter test_zoom_lod`.

## Out of scope

| Excluded | Why |
| --- | --- |
| cercas, ruas desenhadas, iluminação própria do assentamento | arte; o chão já separa os dois escopos |
| zoom em direção ao cursor | melhoria futura |

## Sources

- `client/godot/Scenes/camera_rig.gd`, `world_view.gd`, `npc_actor.gd`
