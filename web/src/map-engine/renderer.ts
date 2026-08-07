// Fase 15.1, T8: renderer puro por viewport (design.md "Components" -> `Renderer`). Extraído do
// corpo de desenho de `web/src/components/GridCanvas.tsx:70-130` (fill de célula, grid lines,
// dot/token com anel) e generalizado para: (a) desenhar só o que `Camera.visibleWorldRect()`
// cobre — nunca a grade inteira (VTT2-03); (b) decidir dot/token/aggregate por `LodPolicy`
// em vez do binário antigo; (c) nunca redimensionar o canvas — o tamanho é do container, quem
// decide isso é quem monta o `<canvas>` (T13), não o renderer (VTT2-35, substitui
// `MAX_CANVAS_PX` de `web/src/gridFit.ts:5`).
//
// Feedback do usuário (2026-08-07): entidade com footprint real (`size.w>1 || size.h>1` —
// cidade/prédio) desenha como ÁREA do grid (retângulo wireframe), nunca como círculo num ponto
// (master prompt §6/§7 — "cidade não deve ser só um marcador"). Círculo continua reservado a
// entidade de ponto (NPC).
import { Camera } from "./Camera";
import { aggregate, levelFor, type LodThresholds } from "./lod";
import { SELECTION_HIGHLIGHT_COLOR } from "./categoryColors";
import type { AuthoritativeEntity, CameraState, Vec2 } from "./types";

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

function isAreaEntity(entity: AuthoritativeEntity): boolean {
  return entity.size.w > 1 || entity.size.h > 1;
}

function intersectsRect(entity: AuthoritativeEntity, visible: { x: number; y: number; width: number; height: number }): boolean {
  const left = entity.position.x;
  const top = entity.position.y;
  const right = left + (isAreaEntity(entity) ? entity.size.w : 0);
  const bottom = top + (isAreaEntity(entity) ? entity.size.h : 0);
  return right >= visible.x && left <= visible.x + visible.width && bottom >= visible.y && top <= visible.y + visible.height;
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

  const visibleEntities = frame.entities.filter((e) => intersectsRect(e, visible));
  const level = levelFor(scale, frame.lodThresholds);
  const areaEntities = visibleEntities.filter(isAreaEntity);
  const pointEntities = visibleEntities.filter((e) => !isAreaEntity(e));

  // Áreas (cidade/prédio) sempre desenham como footprint real — LOD só afeta entidade de ponto.
  for (const entity of areaEntities) {
    drawAreaEntity(ctx, camera, entity, entity.ref.id === frame.highlightId);
  }

  if (level === "aggregate") {
    drawClusters(ctx, camera, pointEntities, scale);
  } else {
    for (const entity of pointEntities) {
      drawPointEntity(ctx, camera, entity, scale, level !== "dot", entity.ref.id === frame.highlightId);
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

/** Cidade/prédio: retângulo do footprint real, nunca um círculo num ponto (feedback do usuário). */
function drawAreaEntity(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  isHighlighted: boolean,
): void {
  const topLeft = camera.worldToScreen(entity.position);
  const bottomRight = camera.worldToScreen({ x: entity.position.x + entity.size.w, y: entity.position.y + entity.size.h });
  const width = bottomRight.x - topLeft.x;
  const height = bottomRight.y - topLeft.y;

  ctx.fillStyle = `${entity.color}26`; // preenchimento bem sutil — a borda é o dado, não o fill
  ctx.fillRect(topLeft.x, topLeft.y, width, height);

  ctx.lineWidth = isHighlighted ? 3 : 2;
  ctx.strokeStyle = isHighlighted ? SELECTION_HIGHLIGHT_COLOR : entity.color;
  // Footprint derivado (não autorado no domínio) ganha traço tracejado — mesma regra de T8.
  ctx.setLineDash(entity.sizeIsDerived ? [6, 4] : []);
  ctx.strokeRect(topLeft.x, topLeft.y, width, height);
  ctx.setLineDash([]);

  drawLabel(ctx, entity, { x: topLeft.x + 4, y: topLeft.y + 12 }, "left");
}

function drawLabel(ctx: CanvasRenderingContext2D, entity: AuthoritativeEntity, at: Vec2, align: CanvasTextAlign): void {
  ctx.font = "11px sans-serif";
  ctx.textAlign = align;
  ctx.fillStyle = "#e8e6df";
  ctx.fillText(`${entity.ref.kind} ${entity.ref.id.slice(0, 8)}`, at.x, at.y);
}

function drawPointEntity(
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
    // Rótulo só a partir do nível "token" (master prompt §4: dot fica só com a forma; rótulo é
    // "informação adicional" do zoom próximo) — feedback do usuário pedia identificar o NPC.
    drawLabel(ctx, entity, { x: center.x, y: center.y + r + 12 }, "center");
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
    ctx.strokeStyle = SELECTION_HIGHLIGHT_COLOR;
    ctx.lineWidth = 2;
    ctx.stroke();
  }
}
