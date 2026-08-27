export interface ScreenPoint {
  x: number;
  y: number;
}

export interface GridPoint {
  x: number;
  y: number;
}

/**
 * Projeção isométrica 2:1 pura — grid coord → screen coord.
 * `height` (nº de "andares" isométricos, opcional) só desloca o Y visual pra empilhar blocos
 * mais altos "na frente" — não faz parte da matemática de grid↔screen em si, por isso `toGrid`
 * não recebe/precisa desfazer esse deslocamento (quem faz hit-test com blocos altos subtrai o
 * deslocamento de `height` do screenY ANTES de chamar `toGrid` — ver IsoTileRenderer, T8).
 */
export function toScreen(gridX: number, gridY: number, tileWidth: number, tileHeight: number, height = 0): ScreenPoint {
  return {
    x: (gridX - gridY) * (tileWidth / 2),
    y: (gridX + gridY) * (tileHeight / 2) - height * tileHeight,
  };
}

/** Inverso de `toScreen` (sem `height` — ver nota acima). */
export function toGrid(screenX: number, screenY: number, tileWidth: number, tileHeight: number): GridPoint {
  const a = screenX / (tileWidth / 2);
  const b = screenY / (tileHeight / 2);
  return {
    x: (a + b) / 2,
    y: (b - a) / 2,
  };
}
