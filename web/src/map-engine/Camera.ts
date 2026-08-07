// Fase 15.1, T5: câmera pura do map engine (design.md "Components" -> `Camera`). Nenhuma
// referência a DOM/canvas — worldToScreen/screenToWorld são a única fronteira com o viewport,
// e o viewport é passado por fora (não é `CameraState`: tamanho de tela é estado de render,
// câmera por espaço é o que se preserva ao entrar/sair — master prompt §33).
import type { CameraState, Rect, SpaceBounds, Vec2 } from "./types";
import { computeFitZoom } from "../gridFit";

export interface Viewport {
  width: number;
  height: number;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}

export class Camera {
  private center: Vec2;
  private scale: number;
  private viewport: Viewport;

  constructor(initial: CameraState, viewport: Viewport) {
    this.center = { ...initial.center };
    this.scale = initial.scale;
    this.viewport = { ...viewport };
  }

  /** Câmera inicial de fit-to-screen para um espaço nunca visitado (reusa `computeFitZoom`). */
  static initial(gridWidth: number, gridHeight: number, viewport: Viewport): CameraState {
    return {
      center: { x: gridWidth / 2, y: gridHeight / 2 },
      scale: computeFitZoom(gridWidth, gridHeight, viewport.width, viewport.height),
    };
  }

  setViewport(viewport: Viewport): void {
    this.viewport = { ...viewport };
  }

  worldToScreen(p: Vec2): Vec2 {
    return {
      x: (p.x - this.center.x) * this.scale + this.viewport.width / 2,
      y: (p.y - this.center.y) * this.scale + this.viewport.height / 2,
    };
  }

  screenToWorld(p: Vec2): Vec2 {
    return {
      x: (p.x - this.viewport.width / 2) / this.scale + this.center.x,
      y: (p.y - this.viewport.height / 2) / this.scale + this.center.y,
    };
  }

  /** Mantém `screenToWorld(screenPoint)` invariante — o mundo sob o cursor não "escorrega". */
  zoomAt(screenPoint: Vec2, factor: number): void {
    if (factor <= 0) {
      throw new Error("zoom factor must be > 0");
    }
    const worldUnderCursor = this.screenToWorld(screenPoint);
    this.scale *= factor;
    this.center = {
      x: worldUnderCursor.x - (screenPoint.x - this.viewport.width / 2) / this.scale,
      y: worldUnderCursor.y - (screenPoint.y - this.viewport.height / 2) / this.scale,
    };
  }

  panBy(screenDelta: Vec2): void {
    this.center = {
      x: this.center.x - screenDelta.x / this.scale,
      y: this.center.y - screenDelta.y / this.scale,
    };
  }

  /** Impede o espaço de saltar pra fora do viewport — centra o eixo se o espaço for menor que a tela. */
  clampTo(bounds: SpaceBounds): void {
    const halfW = this.viewport.width / 2 / this.scale;
    const halfH = this.viewport.height / 2 / this.scale;

    this.center = {
      x: bounds.width <= halfW * 2 ? bounds.width / 2 : clamp(this.center.x, halfW, bounds.width - halfW),
      y: bounds.height <= halfH * 2 ? bounds.height / 2 : clamp(this.center.y, halfH, bounds.height - halfH),
    };
  }

  visibleWorldRect(): Rect {
    const topLeft = this.screenToWorld({ x: 0, y: 0 });
    const bottomRight = this.screenToWorld({ x: this.viewport.width, y: this.viewport.height });
    return {
      x: topLeft.x,
      y: topLeft.y,
      width: bottomRight.x - topLeft.x,
      height: bottomRight.y - topLeft.y,
    };
  }

  snapshot(): CameraState {
    return { center: { ...this.center }, scale: this.scale };
  }

  restore(state: CameraState): void {
    this.center = { ...state.center };
    this.scale = state.scale;
  }
}
