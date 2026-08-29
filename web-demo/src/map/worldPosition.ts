import type { AgentFixture, WorldFixture } from "../fixture/types";
import { patrolPositionAt } from "./patrolMath";

export interface WorldPoint {
  x: number;
  y: number;
}

/**
 * Pedido do usuário 2026-08-27: "no backend não temos separação de posição de mundo/cidade/
 * casa — é uma posição X/Y absoluta, o NPC dentro de casa ocupa uma posição específica do mapa
 * mundi". Esta demo ainda guarda posição HIERÁRQUICA (settlement.gridPosition em unidades de
 * mapa-múndi; building/patrol/indoorLocation em unidades LOCAIS de settlement, `render/
 * constants.ts`'s `TILE`) porque é isso que o Settlement/Building renderer precisa pra desenhar.
 * Este módulo é o único lugar que RESOLVE as duas escalas pra uma posição absoluta única — a
 * fronteira exata onde plugar uma API real que já mande X/Y absoluto (o resto do código só
 * chamaria `agent.worldPosition` em vez de `agentWorldPosition(fixture, agent, now)`, mesmo
 * formato de retorno).
 */
export const LOCAL_UNITS_PER_WORLD_TILE = 20;

/** Um settlement.gridPosition JÁ é a posição absoluta dele — é a origem do grid local dele. */
export function settlementWorldOrigin(settlement: { gridPosition: WorldPoint }): WorldPoint {
  return settlement.gridPosition;
}

/**
 * Posição absoluta (grid do mapa mundi) de um agent agora, dentro ou fora de casa. Indoor usa a
 * posição do PRÉDIO (não o offset exato do cômodo) — no LOD do mapa mundi essa diferença é
 * imperceptível (doc §79: "objetos pequenos não existem visualmente em World Zoom"), não vale
 * duplicar aqui a transform de interior que já existe em `SettlementStage`.
 */
export function agentWorldPosition(fixture: WorldFixture, agent: AgentFixture, now: number): WorldPoint {
  const settlement = fixture.settlements.find((s) => s.id === agent.settlementId);
  if (!settlement) return { x: 0, y: 0 };
  const origin = settlementWorldOrigin(settlement);

  let local: WorldPoint;
  if (agent.indoorLocation) {
    const building = settlement.buildings.find((b) => b.id === agent.indoorLocation!.buildingId);
    local = building ? building.gridPosition : { x: 0, y: 0 };
  } else {
    local = patrolPositionAt(agent.patrolPoints, now);
  }

  return { x: origin.x + local.x / LOCAL_UNITS_PER_WORLD_TILE, y: origin.y + local.y / LOCAL_UNITS_PER_WORLD_TILE };
}
