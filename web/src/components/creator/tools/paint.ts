// Fase 15.1, T25: ferramentas de escala WORLD por clique no `MapView`, portadas 1:1 da lógica de
// `MapGridEditor.paintCell` (`web/src/components/MapGridEditor.tsx:43-65`) — mesmo comportamento,
// só que puro (sem `GridCanvas`) pra operar sobre o clique real do map engine em `WorldEditor`.
import type { PaintedCell, SettlementRow } from "../../../scenarioDefaults";

export type PaintTool = "select" | "terrain" | "water" | "erase" | "settlement";

export function paintTerrainCell(
  cells: Record<string, PaintedCell>,
  x: number,
  y: number,
  terrainId: number,
  biomeId: number,
): Record<string, PaintedCell> {
  const key = `${x},${y}`;
  const existing = cells[key];
  return {
    ...cells,
    [key]: {
      terrain: terrainId,
      biome: existing?.biome ?? biomeId,
      altitude: existing?.altitude ?? 0,
      water: existing?.water ?? false,
    },
  };
}

export function paintWaterCell(
  cells: Record<string, PaintedCell>,
  x: number,
  y: number,
  terrainId: number,
  biomeId: number,
): Record<string, PaintedCell> {
  const key = `${x},${y}`;
  const existing = cells[key];
  return {
    ...cells,
    [key]: {
      terrain: existing?.terrain ?? terrainId,
      biome: existing?.biome ?? biomeId,
      altitude: existing?.altitude ?? 0,
      water: true,
    },
  };
}

export function eraseCell(cells: Record<string, PaintedCell>, x: number, y: number): Record<string, PaintedCell> {
  const next = { ...cells };
  delete next[`${x},${y}`];
  return next;
}

export function addSettlement(settlements: SettlementRow[], x: number, y: number): SettlementRow[] {
  return [...settlements, { name: `assentamento-${settlements.length + 1}`, x, y }];
}
