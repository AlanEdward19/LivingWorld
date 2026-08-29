import type { BuildingFixture } from "../fixture/types";
import { buildingFootprint } from "./settlementLayout";

export interface WorldRoadSegment {
  from: { x: number; y: number };
  to: { x: number; y: number };
}

/** Sem dados de prédio (Millbrook/Stonehaven nesta demo — doc §90 "fallback visual... claramente
 * isolado tecnicamente pra poder ser substituído pelo spatial state real depois"), o footprint
 * vira só uma função de população — cresce/encolhe com ela (pedido do usuário 2026-08-27). */
const FALLBACK_BASE_UNITS = 8;
const FALLBACK_PER_CAPITA_UNITS = 0.3;

export interface FootprintExtent {
  width: number; // unidades LOCAIS (mesma escala de `building.gridPosition`/`render/constants.ts`'s TILE)
  height: number;
}

/**
 * Extensão real (bounding box, em unidades LOCAIS de settlement) do que o settlement de fato
 * ocupa — soma o footprint de CADA prédio (não só o ponto central do `gridPosition`), pra dar a
 * área real construída. Pedido do usuário 2026-08-27: "uma cidade com 4 casas que ocupam 4x4
 * deve ocupar o mesmo terreno no mapa mundi" — o mapa mundi (`WorldStage`) converte isso pra
 * unidades de mundo via `LOCAL_UNITS_PER_WORLD_TILE` (`map/worldPosition.ts`), nunca inventa um
 * tamanho arbitrário desconectado da geometria real quando ela existe.
 */
export function settlementFootprintExtent(settlement: { buildings: Pick<BuildingFixture, "gridPosition" | "floors">[]; population: number }): FootprintExtent {
  if (settlement.buildings.length === 0) {
    const side = FALLBACK_BASE_UNITS + settlement.population * FALLBACK_PER_CAPITA_UNITS;
    return { width: side, height: side };
  }

  let minX = Infinity;
  let maxX = -Infinity;
  let minY = Infinity;
  let maxY = -Infinity;
  for (const building of settlement.buildings) {
    const footprint = buildingFootprint(building);
    minX = Math.min(minX, building.gridPosition.x - footprint.width / 2);
    maxX = Math.max(maxX, building.gridPosition.x + footprint.width / 2);
    minY = Math.min(minY, building.gridPosition.y - footprint.height / 2);
    maxY = Math.max(maxY, building.gridPosition.y + footprint.height / 2);
  }
  return { width: maxX - minX, height: maxY - minY };
}

/**
 * Rede viária entre settlements (World Map redesign doc §17: "estrada precisa conectar
 * visualmente os lugares", não um grafo abstrato). Árvore geradora mínima gulosa (Prim) — cada
 * settlement se conecta ao vizinho conectado mais próximo — em vez de hub-and-spoke (`settlement
 * Layout.generateRoads`, adequado pra prédios ao redor de UM hub central, mas leria como "teia"
 * numa escala de mundo com settlements espalhados) ou grafo completo (N² arestas, ilegível).
 * Determinístico: mesma entrada, mesma saída sempre.
 */
export function generateWorldRoads(settlements: { gridPosition: { x: number; y: number } }[]): WorldRoadSegment[] {
  if (settlements.length < 2) return [];

  const connected = [settlements[0]];
  const remaining = settlements.slice(1);
  const segments: WorldRoadSegment[] = [];

  while (remaining.length > 0) {
    let bestConnectedIndex = 0;
    let bestRemainingIndex = 0;
    let bestDistanceSq = Infinity;

    for (let c = 0; c < connected.length; c += 1) {
      for (let r = 0; r < remaining.length; r += 1) {
        const dx = connected[c].gridPosition.x - remaining[r].gridPosition.x;
        const dy = connected[c].gridPosition.y - remaining[r].gridPosition.y;
        const distanceSq = dx * dx + dy * dy;
        if (distanceSq < bestDistanceSq) {
          bestDistanceSq = distanceSq;
          bestConnectedIndex = c;
          bestRemainingIndex = r;
        }
      }
    }

    segments.push({ from: connected[bestConnectedIndex].gridPosition, to: remaining[bestRemainingIndex].gridPosition });
    connected.push(remaining[bestRemainingIndex]);
    remaining.splice(bestRemainingIndex, 1);
  }

  return segments;
}
