export interface CameraState {
  /** Centro da câmera, em coordenadas de mundo (pixels do renderer, não grid). */
  x: number;
  y: number;
  zoom: number;
  focusBuildingId: string | null;
}

export const DEFAULT_ZOOM = 1;
export const MIN_ZOOM = 0.35;
export const MAX_ZOOM = 3.5;
/** Zoom aplicado ao focar um prédio (doc: "aproximar a câmera" em vez de navegar pra outra
 * página) — suficiente pra ler paredes/móveis/NPCs do interior revelado. */
export const FOCUS_ZOOM = 2.4;

export function initialCamera(centerX: number, centerY: number): CameraState {
  return { x: centerX, y: centerY, zoom: DEFAULT_ZOOM, focusBuildingId: null };
}

const clampZoom = (zoom: number): number => Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));

/** Zoom centrado na tela (não no cursor) — mais simples que zoom-to-pointer e suficiente pro
 * pedido do usuário ("consigo dar zoom"); câmera continua olhando pro mesmo ponto de mundo. */
export function zoomBy(state: CameraState, factor: number): CameraState {
  return { ...state, zoom: clampZoom(state.zoom * factor) };
}

/** `dx`/`dy` já em coordenadas de mundo (deslocamento de tela dividido pelo zoom atual, feito
 * por quem chama) — arrastar a câmera nunca muda o zoom. */
export function panBy(state: CameraState, dx: number, dy: number): CameraState {
  return { ...state, x: state.x - dx, y: state.y - dy };
}

/** Foca um prédio — câmera aproxima pro centro dele (`targetX`/`targetY`, já em coordenadas de
 * mundo). Marcar `focusBuildingId` é o que o renderer usa pra decidir revelar o interior
 * (roof cutaway) desse prédio especificamente. */
export function focusOn(state: CameraState, buildingId: string, targetX: number, targetY: number): CameraState {
  return { x: targetX, y: targetY, zoom: FOCUS_ZOOM, focusBuildingId: buildingId };
}

/** Sai do foco, volta pro overview do settlement (`centerX`/`centerY` = centro do settlement). */
export function unfocus(state: CameraState, centerX: number, centerY: number): CameraState {
  return { x: centerX, y: centerY, zoom: DEFAULT_ZOOM, focusBuildingId: null };
}
