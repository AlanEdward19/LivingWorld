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

export function initialCamera(centerX: number, centerY: number, zoom: number = DEFAULT_ZOOM): CameraState {
  return { x: centerX, y: centerY, zoom, focusBuildingId: null };
}

const clampZoom = (zoom: number): number => Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));

/**
 * Zoom que caberia o bounding box inteiro (prédios espalhados, AD-022) na viewport — nunca
 * amplia além de 1x (`DEFAULT_ZOOM`) pra um settlement pequeno/vazio não vir "grudado" na tela;
 * só encolhe quando o settlement é grande demais pra caber. `margin` deixa uma borda de respiro.
 */
export function fitZoom(boundingWidth: number, boundingHeight: number, viewportWidth: number, viewportHeight: number, margin = 0.85): number {
  if (boundingWidth <= 0 || boundingHeight <= 0) return DEFAULT_ZOOM;
  const fit = Math.min(viewportWidth / boundingWidth, viewportHeight / boundingHeight) * margin;
  return clampZoom(Math.min(DEFAULT_ZOOM, fit));
}

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

/** Sai do foco, volta pro overview do settlement (`centerX`/`centerY` = centro do settlement,
 * `zoom` = o overview zoom real desse settlement — ver `fitZoom` — não sempre `DEFAULT_ZOOM`). */
export function unfocus(state: CameraState, centerX: number, centerY: number, zoom: number = DEFAULT_ZOOM): CameraState {
  return { x: centerX, y: centerY, zoom, focusBuildingId: null };
}
