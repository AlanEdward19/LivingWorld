// Fase 15.1, T25: ferramentas puras de escala WORLD por clique no `MapView`.
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
