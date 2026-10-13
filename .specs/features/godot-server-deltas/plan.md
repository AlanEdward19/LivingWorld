# Godot server deltas [Estado: Spec - plano sem checks nem código (specs-closeout 2026-10-12)]

## Problem

`STATE.md` registra pendências do cliente Godot: o servidor não publica delta de interior nem de `step`, então o cliente relê o snapshot do interior a cada 1 s (`.specs/features/godot-client/plan.md`, premissa "Interior ao vivo"), e `GET /npcs/{id}` e os deltas não têm forma congelada em `tests/contracts/`. O calendário do cenário (meses) também não é servido, e o cliente assume dia de 24 h.

O custo é tráfego e latência a mais por cliente e risco de o contrato mudar sob o cliente. Evidência: `STATE.md` (Handoff), planos `godot-client` e `godot-client-experience`.

Quando isto entrar, o servidor empurra delta de interior e de passo, serve o calendário do cenário, e o contrato dessas rotas fica congelado.

## Flow

Reaproveita o canal realtime existente de mundo e cidade; só soma tipos de delta e congela o contrato.

1. `VisualDeltaPublisher` (exists, extended - door 1) - publica delta de interior e de passo no mesmo canal
2. `GET /world/calendar` (new, door 2) - meses por ano, dias por mês, horas por dia
3. `GET /npcs/{id}` (exists, extended, door 2) - forma congelada
4. `tests/contracts/` (exists) - snapshot do contrato por rota
5. `world_store.gd` (exists, extended) - consome o delta e deixa de reler o interior

## Impact

| Front | What changes |
| --- | --- |
| domain | termo: delta de interior; quem lê hoje o snapshot do interior: `interior_view.gd` e `world_store.gd` |
| stored data | none - projeção, nada canônico; hash idêntico |

## Relations

None - nenhuma entidade persistida; só projeção.

## Surface

| Route | In | Out | Status |
| --- | --- | --- | --- |
| `GET /world/calendar` | nenhum | meses por ano, dias por mês, horas por dia | 200 |
| `GET /npcs/{id}` | `id` | NPC com forma congelada | 200, 404 |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| 1. forma do delta | delta de interior com `buildingId`, andar e lista de NPCs com ação e posição, mesmo envelope dos deltas existentes | reler o snapshot: o que o cliente faz hoje, custo por cliente |
| 2. contrato congelado | rota e DTO entram em `tests/contracts/` e só mudam por AD | contrato implícito: quebra o cliente sem aviso |

- Nada mais aqui Ã© difÃ­cil de reverter

## Criteria

### S1: Delta de interior e passo (P1)

**Acceptance Criteria**

1. WHEN um NPC muda de ação ou posição dentro de um prédio observado THEN the system SHALL publicar o delta de interior no canal em até 1 tick
2. The system SHALL manter o hash canônico igual com e sem cliente conectado

**Independent test:** cliente de teste conectado por 1 h simulada.

### S2: Calendário e NPC (P1)

**Acceptance Criteria**

3. WHEN `GET /world/calendar` é chamado THEN the system SHALL responder 200 com a duração do dia e do mês do cenário
4. IF o NPC não existe THEN the system SHALL responder 404
5. The system SHALL falhar `ApiContractTests` se a forma de uma rota congelada mudar
6. WHEN o delta chega THEN the system SHALL atualizar o interior sem reler o snapshot
7. The system SHALL manter os testes de `scripts/client-test.sh` verdes

**Independent test:** chamada HTTP e teste de contrato. `bash scripts/client-test.sh`.

## Out of scope

| Excluded | Why |
| --- | --- |
| arte final e som | plano `godot-art-audio-pass` |
| modo jogador | plano `player-incarnation` |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| canal | o mesmo WebSocket atual | não cria canal novo | n |

**Open questions:** none - all resolved or logged above.

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| API `/world/calendar` e `/npcs/{id}` | error shape and codes | AC 4 |

## Sources

- `STATE.md` - Handoff do cliente Godot
- `.specs/features/godot-client/plan.md`
