// Fase 15.1, T8: renderer puro por viewport (design.md "Components" -> `Renderer`). Extraído do
// corpo de desenho de `web/src/components/GridCanvas.tsx:70-130` (fill de célula, grid lines,
// dot/token com anel) e generalizado para: (a) desenhar só o que `Camera.visibleWorldRect()`
// cobre — nunca a grade inteira (VTT2-03); (b) decidir dot/token/aggregate por `LodPolicy`
// em vez do binário antigo; (c) nunca redimensionar o canvas — o tamanho é do container, quem
// decide isso é quem monta o `<canvas>` (T13), não o renderer (VTT2-35, substitui
// `MAX_CANVAS_PX` de `web/src/gridFit.ts:5`).
import { Camera } from "./Camera";
import { aggregate, levelFor, type LodThresholds } from "./lod";
import { colorById } from "../colorById";
import type { AuthoritativeEntity, CameraState } from "./types";

export interface CellSource {
  width: number;
  height: number;
  colorAt: (x: number, y: number) => string | undefined;
}

export interface ActiveLayer {
  id: string;
  overlayPoints: { x: number; y: number; color: string }[];
}

export interface RenderFrame {
  camera: CameraState;
  cells: CellSource;
  layers: ActiveLayer[];
  entities: AuthoritativeEntity[];
  lodThresholds: LodThresholds;
  highlightId?: string;
}

function clampRange(min: number, max: number, lo: number, hi: number): [number, number] {
  return [Math.max(lo, Math.floor(min)), Math.min(hi, Math.ceil(max))];
}

/** Desenha um frame no contexto — nada além de leitura de `frame`; nunca toca `canvas.width/height`. */
export function draw(ctx: CanvasRenderingContext2D | null, frame: RenderFrame): void {
  if (!ctx) {
    return;
  }

  const viewport = { width: ctx.canvas.width, height: ctx.canvas.height };
  const camera = new Camera(frame.camera, viewport);
  const scale = frame.camera.scale;
  const visible = camera.visibleWorldRect();

  ctx.fillStyle = "#1a1f2c";
  ctx.fillRect(0, 0, viewport.width, viewport.height);

  const [x0, x1] = clampRange(visible.x, visible.x + visible.width, 0, frame.cells.width);
  const [y0, y1] = clampRange(visible.y, visible.y + visible.height, 0, frame.cells.height);

  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const color = frame.cells.colorAt(x, y);
      if (!color) {
        continue;
      }
      const topLeft = camera.worldToScreen({ x, y });
      ctx.fillStyle = color;
      ctx.fillRect(topLeft.x, topLeft.y, scale, scale);
    }
  }

  if (scale >= 10) {
    ctx.strokeStyle = "rgba(255,255,255,0.05)";
    ctx.lineWidth = 1;
    for (let x = x0; x <= x1; x++) {
      const top = camera.worldToScreen({ x, y: y0 });
      const bottom = camera.worldToScreen({ x, y: y1 });
      ctx.beginPath();
      ctx.moveTo(top.x, top.y);
      ctx.lineTo(bottom.x, bottom.y);
      ctx.stroke();
    }
    for (let y = y0; y <= y1; y++) {
      const left = camera.worldToScreen({ x: x0, y });
      const right = camera.worldToScreen({ x: x1, y });
      ctx.beginPath();
      ctx.moveTo(left.x, left.y);
      ctx.lineTo(right.x, right.y);
      ctx.stroke();
    }
  }

  for (const layer of frame.layers) {
    for (const p of layer.overlayPoints) {
      if (p.x < x0 || p.x >= x1 || p.y < y0 || p.y >= y1) {
        continue;
      }
      const center = camera.worldToScreen({ x: p.x + 0.5, y: p.y + 0.5 });
      ctx.fillStyle = p.color;
      ctx.beginPath();
      ctx.arc(center.x, center.y, Math.max(1.5, scale * 0.12), 0, Math.PI * 2);
      ctx.fill();
    }
  }

  const visibleEntities = frame.entities.filter(
    (e) => e.position.x >= visible.x && e.position.x <= visible.x + visible.width &&
      e.position.y >= visible.y && e.position.y <= visible.y + visible.height,
  );
  const level = levelFor(scale, frame.lodThresholds);

  if (level === "aggregate") {
    drawClusters(ctx, camera, visibleEntities, scale);
  } else {
    for (const entity of visibleEntities) {
      drawEntity(ctx, camera, entity, scale, level !== "dot", entity.ref.id === frame.highlightId);
    }
  }
}

function drawClusters(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entities: AuthoritativeEntity[],
  scale: number,
): void {
  for (const cluster of aggregate(entities, Math.max(1, 10 / scale))) {
    const center = camera.worldToScreen({ x: cluster.bucketX + 0.5, y: cluster.bucketY + 0.5 });
    const radius = Math.max(3, Math.min(14, 3 + Math.sqrt(cluster.count)));
    ctx.beginPath();
    ctx.arc(center.x, center.y, radius, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(232,230,223,0.35)";
    ctx.fill();
  }
}

function drawEntity(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
  isToken: boolean,
  isHighlighted: boolean,
): void {
  const center = camera.worldToScreen({ x: entity.position.x + 0.5, y: entity.position.y + 0.5 });

  if (isToken) {
    const r = Math.max(4, scale * 0.35);
    ctx.beginPath();
    ctx.arc(center.x, center.y, r, 0, Math.PI * 2);
    ctx.fillStyle = entity.color;
    ctx.fill();
    ctx.lineWidth = Math.max(1, r * 0.18);
    ctx.strokeStyle = "#e8e6df";
    // Entidade com footprint/posição derivada (não autorada no domínio) ganha traço
    // tracejado, nunca o mesmo anel sólido de uma entidade real (Done-when de T8).
    ctx.setLineDash(entity.sizeIsDerived ? [r * 0.3, r * 0.25] : []);
    ctx.stroke();
    ctx.setLineDash([]);
  } else {
    const r = Math.max(1.5, scale * 0.15);
    ctx.beginPath();
    ctx.arc(center.x, center.y, r, 0, Math.PI * 2);
    ctx.fillStyle = entity.color;
    ctx.shadowColor = entity.color;
    ctx.shadowBlur = r * 2;
    ctx.fill();
    ctx.shadowBlur = 0;
    if (entity.sizeIsDerived) {
      ctx.lineWidth = Math.max(0.5, r * 0.3);
      ctx.strokeStyle = "#e8e6df";
      ctx.setLineDash([r * 0.4, r * 0.3]);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  if (isHighlighted) {
    ctx.beginPath();
    ctx.arc(center.x, center.y, Math.max(6, scale * 0.5), 0, Math.PI * 2);
    ctx.strokeStyle = colorById(1, 80, 70);
    ctx.lineWidth = 2;
    ctx.stroke();
  }
}
