import { toScreen } from "./IsoProjection";
import { paletteForBuildingKind } from "./isoPalette";
import type { BuildingKind } from "../fixture/types";

export const TILE_WIDTH = 48;
export const TILE_HEIGHT = 48;

export interface IsoTileProps {
  gridX: number;
  gridY: number;
  kind: BuildingKind;
  onClick?: (gridX: number, gridY: number) => void;
}

/**
 * Um prédio no mapa — quadrado top-down (AD-019), NÃO mais um bloco isométrico de 3 faces.
 * Nome do arquivo/componente ficou do design anterior; mantido pra não espalhar o rename por
 * `SemanticZoomMap`/testes que só referenciam o testid `iso-tile`, não o nome do componente.
 */
export function IsoTile({ gridX, gridY, kind, onClick }: IsoTileProps) {
  const palette = paletteForBuildingKind(kind);
  const { x, y } = toScreen(gridX, gridY, TILE_WIDTH, TILE_HEIGHT);
  const hw = TILE_WIDTH / 2;
  const hh = TILE_HEIGHT / 2;

  return (
    <g
      data-testid="iso-tile"
      data-grid-x={gridX}
      data-grid-y={gridY}
      onClick={() => onClick?.(gridX, gridY)}
      style={{ cursor: onClick ? "pointer" : undefined }}
    >
      <rect
        x={x - hw + 2}
        y={y - hh + 2}
        width={TILE_WIDTH - 4}
        height={TILE_HEIGHT - 4}
        rx={4}
        fill={palette.top}
        stroke={palette.right}
        strokeWidth={2}
      />
    </g>
  );
}
