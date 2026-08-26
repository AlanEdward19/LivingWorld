import { toScreen } from "./IsoProjection";
import { paletteForBuildingKind } from "./isoPalette";
import type { BuildingKind } from "../fixture/types";

export const TILE_WIDTH = 64;
export const TILE_HEIGHT = 32;
const FLOOR_HEIGHT = 24;

export interface IsoTileProps {
  gridX: number;
  gridY: number;
  height?: number;
  kind: BuildingKind;
  onClick?: (gridX: number, gridY: number) => void;
}

function points(pairs: [number, number][]): string {
  return pairs.map(([x, y]) => `${x},${y}`).join(" ");
}

/**
 * Um bloco isométrico — 3 faces (top/left/right) desenhadas como `<polygon>` SVG,
 * flat-shaded fixo por face (design.md § Tech Decisions: isométrico simplificado, sem
 * textura/gradiente/animação).
 */
export function IsoTile({ gridX, gridY, height = 1, kind, onClick }: IsoTileProps) {
  const palette = paletteForBuildingKind(kind);
  const { x, y } = toScreen(gridX, gridY, TILE_WIDTH, TILE_HEIGHT);
  const hw = TILE_WIDTH / 2;
  const hh = TILE_HEIGHT / 2;
  const raise = height * FLOOR_HEIGHT;

  // Diamante da face de topo, elevado por `raise`
  const north: [number, number] = [x, y - raise];
  const east: [number, number] = [x + hw, y + hh - raise];
  const south: [number, number] = [x, y + 2 * hh - raise];
  const west: [number, number] = [x - hw, y + hh - raise];

  // Corners no chão (mesmo x/y, sem elevação) — pé das paredes
  const groundSouth: [number, number] = [x, y + 2 * hh];
  const groundEast: [number, number] = [x + hw, y + hh];
  const groundWest: [number, number] = [x - hw, y + hh];

  return (
    <g
      data-testid="iso-tile"
      data-grid-x={gridX}
      data-grid-y={gridY}
      onClick={() => onClick?.(gridX, gridY)}
      style={{ cursor: onClick ? "pointer" : undefined }}
    >
      <polygon data-face="left" points={points([west, south, groundSouth, groundWest])} fill={palette.left} />
      <polygon data-face="right" points={points([south, east, groundEast, groundSouth])} fill={palette.right} />
      <polygon data-face="top" points={points([north, east, south, west])} fill={palette.top} />
    </g>
  );
}
