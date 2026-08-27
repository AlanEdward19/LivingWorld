export interface ScreenPoint {
  x: number;
  y: number;
}

export interface GridPoint {
  x: number;
  y: number;
}

/** Tamanho de tile do mapa "mundo" (SVG, `SemanticZoomMap`) — não usado pelo renderer Pixi do
 * Settlement View, que define sua própria escala em `render/constants.ts` (AD-020). */
export const TILE_WIDTH = 48;
export const TILE_HEIGHT = 48;

/**
 * Projeção top-down ortogonal — grid coord → screen coord, 1:1 escalado por tile.
 * SUBSTITUIU a projeção isométrica 2:1 original (AD-019): usuário reportou que o visual
 * isométrico "não está funcionando bem" e pediu top-down explícito (RimWorld-style, igual ao
 * que já existe em `BuildingInterior.tsx`) — exterior e interior agora usam a mesma lógica de
 * projeção (identidade escalada), não duas por acaso divergentes.
 */
export function toScreen(gridX: number, gridY: number, tileWidth: number, tileHeight: number): ScreenPoint {
  return { x: gridX * tileWidth, y: gridY * tileHeight };
}

/** Inverso de `toScreen`. */
export function toGrid(screenX: number, screenY: number, tileWidth: number, tileHeight: number): GridPoint {
  return { x: screenX / tileWidth, y: screenY / tileHeight };
}
