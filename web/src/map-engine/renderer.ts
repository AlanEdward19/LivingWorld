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
// (master prompt §6/§7 — "cidade não deve ser só um marcador"). Entidades de ponto continuam
// no fluxo de token, e NPCs usam o pawn SVG determinístico no LOD próximo.
import { Camera } from "./Camera";
import { aggregate, levelFor, type LodThresholds } from "./lod";
import { SELECTION_HIGHLIGHT_COLOR } from "./categoryColors";
import type { AuthoritativeEntity, CameraState, Vec2 } from "./types";
import { npcPawnDataUrl } from "../npcAppearance";
import { cloudPuffs, type GroundVisual } from "./worldVisuals";
import {
  architectureHash,
  buildingAppearanceForType,
  cityRoofPalette,
  type ArchitecturePalette,
} from "./architectureAppearance";
import { fanOutOffsets } from "./fanOut";
import { npcVisualScale, tokenRadiusPx } from "./tokenSize";
import { actionVisualFor } from "./actionVisuals";
import { processCueVisual } from "./cityNpcOverlay";
import { drawActionIcon } from "./actionIcon";
import { prefersReducedMotion } from "../reducedMotion";
import { animationSpecForAction, animationSpecForEvent, animationSpecForProcess } from "./npcAnimationCatalog";
import { cueFromSpec, PROGRESS_RING_START, progressRingEndAngle } from "./npcAnimationCue";
import { resolveSocialLinks, SOCIAL_EVENT_KINDS } from "./socialRomance";
import { resolveLifecycleMoments } from "./lifecycleMoments";

const npcPawnImages = new Map<string, HTMLImageElement>();

export interface CellSource {
  width: number;
  height: number;
  minX?: number;
  minY?: number;
  backgroundColor?: string;
  atmosphereSeed?: string;
  showGrid?: boolean;
  colorAt: (x: number, y: number) => string | undefined;
  detailAt?: (x: number, y: number) => GroundVisual | undefined;
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
  /** Living timeline events for this tick — cosmetic overlays only. */
  events?: readonly { kind: number; location?: { x: number; y: number } | null }[];
}

function clampRange(min: number, max: number, lo: number, hi: number): [number, number] {
  return [Math.max(lo, Math.floor(min)), Math.min(hi, Math.ceil(max))];
}

function isAreaEntity(entity: AuthoritativeEntity): boolean {
  return entity.size.w > 1 || entity.size.h > 1;
}

function cellInRect(x: number, y: number, visible: { x: number; y: number; width: number; height: number }): boolean {
  return x + 1 >= visible.x && x <= visible.x + visible.width && y + 1 >= visible.y && y <= visible.y + visible.height;
}

function intersectsRect(entity: AuthoritativeEntity, visible: { x: number; y: number; width: number; height: number }): boolean {
  const left = entity.position.x;
  const top = entity.position.y;
  const right = left + (isAreaEntity(entity) ? entity.size.w : 0);
  const bottom = top + (isAreaEntity(entity) ? entity.size.h : 0);
  if (right >= visible.x && left <= visible.x + visible.width && bottom >= visible.y && top <= visible.y + visible.height) {
    return true;
  }
  return !!entity.travelDestination && cellInRect(entity.travelDestination.x, entity.travelDestination.y, visible);
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

  ctx.fillStyle = frame.cells.backgroundColor ?? "#7fa8b2";
  ctx.fillRect(0, 0, viewport.width, viewport.height);

  const minX = frame.cells.minX ?? 0;
  const minY = frame.cells.minY ?? 0;
  const [x0, x1] = clampRange(visible.x, visible.x + visible.width, minX, minX + frame.cells.width);
  const [y0, y1] = clampRange(visible.y, visible.y + visible.height, minY, minY + frame.cells.height);

  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const color = frame.cells.colorAt(x, y);
      if (!color) {
        continue;
      }
      const topLeft = camera.worldToScreen({ x, y });
      ctx.fillStyle = color;
      ctx.fillRect(topLeft.x, topLeft.y, scale, scale);
      const detail = frame.cells.detailAt?.(x, y);
      if (detail && scale >= 11) {
        drawGroundDetail(ctx, topLeft, scale, detail);
      }
    }
  }

  if (frame.cells.showGrid !== false && scale >= 10) {
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
      const topLeft = camera.worldToScreen({ x: p.x, y: p.y });
      if (layer.id === "Rivers") {
        ctx.fillStyle = p.color;
        ctx.fillRect(topLeft.x, topLeft.y, scale, scale);
        if (scale >= 6) {
          ctx.strokeStyle = "rgba(190,225,226,0.45)";
          ctx.beginPath();
          ctx.moveTo(topLeft.x + scale * 0.15, topLeft.y + scale * 0.4);
          ctx.lineTo(topLeft.x + scale * 0.78, topLeft.y + scale * 0.55);
          ctx.stroke();
        }
      }
    }
  }

  if (frame.cells.atmosphereSeed) {
    drawClouds(ctx, frame.cells.atmosphereSeed, viewport.width, viewport.height);
  }

  const visibleEntities = frame.entities.filter((e) => intersectsRect(e, visible));
  const level = levelFor(scale, frame.lodThresholds);
  const areaEntities = visibleEntities.filter(isAreaEntity);
  const pointEntities = visibleEntities.filter((e) => !isAreaEntity(e));

  for (const entity of pointEntities) {
    drawRelocationRoute(ctx, camera, entity);
  }

  // Áreas (cidade/prédio) sempre desenham como footprint real — LOD só afeta entidade de ponto.
  for (const entity of areaEntities) {
    drawAreaEntity(ctx, camera, entity, scale, entity.ref.id === frame.highlightId);
  }

  if (level === "aggregate") {
    drawClusters(ctx, camera, pointEntities, scale);
  } else {
    // Vários residentes de um mesmo domicílio legitimamente compartilham 1 tile (é a mesma casa
    // física) — sem isso os sprites desenham exatamente empilhados e viram um rótulo ilegível
    // (LIVE-POLISH). O deslocamento é só de desenho: nunca toca a posição autoritativa.
    const fanOut = fanOutOffsets(pointEntities);
    for (const entity of pointEntities) {
      const offset = fanOut.get(entity.ref.id);
      const drawEntity = offset
        ? { ...entity, position: { x: entity.position.x + offset.x, y: entity.position.y + offset.y } }
        : entity;
      drawPointEntity(ctx, camera, drawEntity, scale, level !== "dot", entity.ref.id === frame.highlightId);
    }
    drawSocialOverlays(ctx, camera, pointEntities, frame.events ?? []);
    drawLifecycleBursts(ctx, camera, frame.entities, frame.events ?? []);
  }
}

function drawGroundDetail(ctx: CanvasRenderingContext2D, topLeft: Vec2, scale: number, detail: GroundVisual): void {
  const dx = ((detail.variant >>> 4) % 60) / 100;
  const dy = ((detail.variant >>> 10) % 50) / 100;
  if (detail.detail === "soil") {
    ctx.fillStyle = "rgba(48,35,24,0.28)";
    ctx.fillRect(topLeft.x + scale * (0.2 + dx), topLeft.y + scale * (0.25 + dy), Math.max(1, scale * 0.08), Math.max(1, scale * 0.08));
    return;
  }
  ctx.strokeStyle = "rgba(37,66,35,0.55)";
  ctx.lineWidth = Math.max(1, scale * 0.05);
  ctx.beginPath();
  ctx.moveTo(topLeft.x + scale * (0.2 + dx), topLeft.y + scale * (0.75 + dy * 0.2));
  ctx.lineTo(topLeft.x + scale * (0.16 + dx), topLeft.y + scale * (0.5 + dy * 0.2));
  ctx.moveTo(topLeft.x + scale * (0.2 + dx), topLeft.y + scale * (0.75 + dy * 0.2));
  ctx.lineTo(topLeft.x + scale * (0.28 + dx), topLeft.y + scale * (0.56 + dy * 0.2));
  ctx.stroke();
}

function drawClouds(ctx: CanvasRenderingContext2D, seed: string, width: number, height: number): void {
  ctx.fillStyle = "rgba(229,239,232,0.10)";
  for (const cloud of cloudPuffs(seed, width, height)) {
    for (const [dx, dy, size] of [[0, 0, 1], [-0.7, 0.15, 0.72], [0.72, 0.18, 0.8]] as const) {
      ctx.beginPath();
      ctx.arc(cloud.x + cloud.radius * dx, cloud.y + cloud.radius * dy, cloud.radius * size, 0, Math.PI * 2);
      ctx.fill();
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

/**
 * Cidade/prédio: footprint real, nunca um círculo num ponto (feedback do usuário). Com
 * `footprintCells` (planta por material — `buildingFootprint.ts`), desenha célula a célula em
 * vez do retângulo único de `size` — é assim que formas em L/wireframe aparecem.
 */
function drawAreaEntity(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
  isHighlighted: boolean,
): void {
  if (entity.process?.kind === "construction") {
    drawConstructionScaffold(ctx, camera, entity, scale, isHighlighted);
    return;
  }
  if (entity.ref.kind === "city") {
    drawCityArchitecture(ctx, camera, entity, scale, isHighlighted);
    return;
  }
  if (entity.ref.kind === "building" && entity.footprintCells) {
    drawBuildingArchitecture(ctx, camera, entity, scale, isHighlighted);
    return;
  }
  if (entity.footprintCells) {
    for (const cell of entity.footprintCells) {
      const topLeft = camera.worldToScreen({ x: entity.position.x + cell.x, y: entity.position.y + cell.y });
      ctx.fillStyle = cell.color;
      ctx.fillRect(topLeft.x, topLeft.y, scale, scale);
      drawMaterialDetail(ctx, topLeft, scale, cell.material);
      if (!entity.decorative) {
        ctx.strokeStyle = "rgba(0,0,0,0.3)";
        ctx.lineWidth = 1;
        ctx.strokeRect(topLeft.x, topLeft.y, scale, scale);
      }
    }
    // Entidade decorativa (fundo ambiente, feedback do usuário) nunca ganha realce de seleção
    // nem rótulo — não é selecionável (hitTest.ts pula `decorative`), rótulo só confundiria.
    if (entity.decorative) {
      return;
    }
    if (isHighlighted) {
      const topLeft = camera.worldToScreen(entity.position);
      const bottomRight = camera.worldToScreen({ x: entity.position.x + entity.size.w, y: entity.position.y + entity.size.h });
      ctx.strokeStyle = SELECTION_HIGHLIGHT_COLOR;
      ctx.lineWidth = 3;
      ctx.strokeRect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);
    }
    drawLabel(ctx, entity, { x: camera.worldToScreen(entity.position).x + 4, y: camera.worldToScreen(entity.position).y - 4 }, "left");
    return;
  }

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

function drawConstructionScaffold(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
  isHighlighted: boolean,
): void {
  const topLeft = camera.worldToScreen(entity.position);
  const width = entity.size.w * scale;
  const height = entity.size.h * scale;
  const timber = entity.color || "#8a6a3a";

  ctx.setLineDash([6, 4]);
  ctx.strokeStyle = timber;
  ctx.lineWidth = Math.max(2, scale * 0.12);
  ctx.strokeRect(topLeft.x, topLeft.y, width, height);
  ctx.setLineDash([]);

  ctx.strokeStyle = timber;
  ctx.lineWidth = Math.max(1.5, scale * 0.1);
  ctx.beginPath();
  ctx.moveTo(topLeft.x + scale * 0.2, topLeft.y + height);
  ctx.lineTo(topLeft.x + scale * 0.2, topLeft.y + scale * 0.25);
  ctx.lineTo(topLeft.x + scale * 0.55, topLeft.y + scale * 0.45);
  ctx.moveTo(topLeft.x + width - scale * 0.2, topLeft.y + height);
  ctx.lineTo(topLeft.x + width - scale * 0.2, topLeft.y + scale * 0.25);
  ctx.lineTo(topLeft.x + width - scale * 0.55, topLeft.y + scale * 0.45);
  ctx.stroke();

  const progress = Math.max(0, Math.min(1, entity.process?.progress ?? 0));
  const constructionCue = cueFromSpec(animationSpecForProcess("construction"), {
    progress,
    reducedMotion: prefersReducedMotion(),
  });
  const barY = topLeft.y + height - scale * 0.28;
  ctx.fillStyle = "rgba(40, 32, 24, 0.55)";
  ctx.fillRect(topLeft.x + scale * 0.15, barY, width - scale * 0.3, scale * 0.16);
  ctx.fillStyle = "#c4a15a";
  ctx.fillRect(topLeft.x + scale * 0.15, barY, (width - scale * 0.3) * constructionCue.ringProgress, scale * 0.16);

  const label = entity.label ?? entity.process?.accessibleLabel ?? "Obra";
  ctx.fillStyle = "#f0e6d2";
  ctx.font = `${Math.max(10, scale * 0.35)}px "Iowan Old Style", "Palatino Linotype", serif`;
  ctx.textAlign = "left";
  ctx.fillText(label, topLeft.x + 4, topLeft.y - 4);

  if (isHighlighted) {
    ctx.strokeStyle = SELECTION_HIGHLIGHT_COLOR;
    ctx.lineWidth = 3;
    ctx.strokeRect(topLeft.x, topLeft.y, width, height);
  }
}

function drawBuildingArchitecture(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
  isHighlighted: boolean,
): void {
  const appearance = buildingAppearanceForType(entity.buildingTypeId, entity.ref.id);
  if (appearance.kind === "agriculture") {
    drawAgriculturalArchitecture(ctx, camera, entity, scale, isHighlighted, appearance.palette);
    return;
  }

  drawRoofedArchitecture(ctx, camera, entity, scale, isHighlighted, appearance.palette);
  if (appearance.kind === "forge") {
    drawForgeDetails(ctx, camera, entity, scale);
  }
}

function drawRoofedArchitecture(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
  isHighlighted: boolean,
  palette: ArchitecturePalette,
): void {
  const cells = entity.footprintCells ?? [];
  const split = Math.floor(entity.size.w / 2);
  const topLeft = camera.worldToScreen(entity.position);
  const bottomRight = camera.worldToScreen({ x: entity.position.x + entity.size.w, y: entity.position.y + entity.size.h });
  const rotated = beginEntityRotation(ctx, entity, topLeft, bottomRight);

  // Sombra e massa contínua: não há stroke por tile, portanto o telhado lê como um volume.
  for (const cell of cells.filter((item) => item.material !== "door")) {
    const at = camera.worldToScreen({ x: entity.position.x + cell.x, y: entity.position.y + cell.y });
    ctx.fillStyle = "rgba(35,28,23,0.34)";
    ctx.fillRect(at.x + scale * 0.16, at.y + scale * 0.2, scale, scale);
    ctx.fillStyle = palette.wall;
    ctx.fillRect(at.x, at.y, scale + 0.5, scale + 0.5);
  }
  for (const cell of cells.filter((item) => item.material !== "door")) {
    const at = camera.worldToScreen({ x: entity.position.x + cell.x, y: entity.position.y + cell.y });
    const hasCellBelow = cells.some((other) => other.x === cell.x && other.y === cell.y + 1 && other.material !== "door");
    ctx.fillStyle = cell.x < split ? palette.roofLight : palette.roof;
    ctx.fillRect(at.x, at.y, scale + 0.5, scale * (hasCellBelow ? 1.05 : 0.78));
  }

  ctx.strokeStyle = palette.trim;
  ctx.lineWidth = Math.max(1.5, scale * 0.14);
  ctx.beginPath();
  ctx.moveTo(topLeft.x + split * scale, topLeft.y + scale * 0.18);
  ctx.lineTo(topLeft.x + split * scale, bottomRight.y - scale * 0.18);
  ctx.stroke();

  const door = cells.find((cell) => cell.material === "door");
  if (door) {
    const at = camera.worldToScreen({ x: entity.position.x + door.x, y: entity.position.y + door.y });
    ctx.fillStyle = palette.wall;
    ctx.fillRect(at.x, at.y, scale, scale);
    ctx.fillStyle = palette.trim;
    ctx.fillRect(at.x + scale * 0.2, at.y + scale * 0.18, scale * 0.6, scale * 0.82);
    ctx.fillStyle = "#d6ad58";
    ctx.beginPath();
    ctx.arc(at.x + scale * 0.65, at.y + scale * 0.58, Math.max(1, scale * 0.06), 0, Math.PI * 2);
    ctx.fill();
  }

  // Chaminé assimétrica quebra a aparência de retângulo pintado.
  const chimneyX = topLeft.x + scale * (0.7 + (architectureHash(entity.ref.id) % Math.max(1, entity.size.w - 1)));
  ctx.fillStyle = "#59483d";
  ctx.fillRect(chimneyX, topLeft.y + scale * 0.45, scale * 0.42, scale * 0.55);

  if (isHighlighted) {
    ctx.strokeStyle = SELECTION_HIGHLIGHT_COLOR;
    ctx.lineWidth = 3;
    ctx.strokeRect(topLeft.x, topLeft.y, bottomRight.x - topLeft.x, bottomRight.y - topLeft.y);
  }
  if (rotated) ctx.restore();
  drawLabel(ctx, entity, { x: topLeft.x + 4, y: topLeft.y - 4 }, "left");
}

function drawAgriculturalArchitecture(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
  isHighlighted: boolean,
  palette: ArchitecturePalette,
): void {
  const topLeft = camera.worldToScreen(entity.position);
  const bottomRight = camera.worldToScreen({ x: entity.position.x + entity.size.w, y: entity.position.y + entity.size.h });
  const width = bottomRight.x - topLeft.x;
  const height = bottomRight.y - topLeft.y;
  const rotated = beginEntityRotation(ctx, entity, topLeft, bottomRight);

  ctx.fillStyle = "#6b5130";
  ctx.fillRect(topLeft.x, topLeft.y, width, height);
  ctx.strokeStyle = palette.roofLight;
  ctx.lineWidth = Math.max(1.5, scale * 0.12);
  for (let row = 1; row <= 4; row += 1) {
    const y = topLeft.y + (height * row) / 5;
    ctx.beginPath();
    ctx.moveTo(topLeft.x + scale * 0.25, y);
    ctx.lineTo(bottomRight.x - scale * 0.25, y);
    ctx.stroke();
  }

  // Pequeno galpão deixa a leitura inequívoca mesmo em zoom reduzido.
  ctx.fillStyle = palette.wall;
  ctx.fillRect(topLeft.x + width * 0.62, topLeft.y + height * 0.12, width * 0.25, height * 0.28);
  ctx.fillStyle = palette.roof;
  ctx.fillRect(topLeft.x + width * 0.59, topLeft.y + height * 0.08, width * 0.31, height * 0.1);

  if (isHighlighted) {
    ctx.strokeStyle = SELECTION_HIGHLIGHT_COLOR;
    ctx.lineWidth = 3;
    ctx.strokeRect(topLeft.x, topLeft.y, width, height);
  }
  if (rotated) ctx.restore();
  drawLabel(ctx, entity, { x: topLeft.x + 4, y: topLeft.y - 4 }, "left");
}

function drawForgeDetails(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
): void {
  const topLeft = camera.worldToScreen(entity.position);
  const furnace = {
    x: topLeft.x + entity.size.w * scale * 0.72,
    y: topLeft.y + entity.size.h * scale * 0.68,
  };
  ctx.fillStyle = "#302a27";
  ctx.beginPath();
  ctx.arc(furnace.x, furnace.y, Math.max(2, scale * 0.34), 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#e56d32";
  ctx.beginPath();
  ctx.arc(furnace.x, furnace.y, Math.max(1, scale * 0.16), 0, Math.PI * 2);
  ctx.fill();
}

function drawCityArchitecture(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entity: AuthoritativeEntity,
  scale: number,
  isHighlighted: boolean,
): void {
  const topLeft = camera.worldToScreen(entity.position);
  const bottomRight = camera.worldToScreen({ x: entity.position.x + entity.size.w, y: entity.position.y + entity.size.h });
  const width = bottomRight.x - topLeft.x;
  const height = bottomRight.y - topLeft.y;
  const inset = Math.max(2, Math.min(width, height) * 0.08);
  const chamfer = Math.max(inset, Math.min(width, height) * 0.13);
  const wall = "#aaa38d";
  const rotated = beginEntityRotation(ctx, entity, topLeft, bottomRight);

  if (entity.showBoundary !== false) {
    ctx.fillStyle = "rgba(62,47,31,0.28)";
    cityOutlinePath(ctx, topLeft.x + inset / 2, topLeft.y + inset / 2, width - inset, height - inset, chamfer);
    ctx.fill();
  }

  // Vias largas e legíveis substituem os buracos verdes em cruz da antiga matriz de tiles.
  ctx.fillStyle = "#a99c7d";
  ctx.fillRect(topLeft.x + inset, topLeft.y + height * 0.44, width - inset * 2, Math.max(3, height * 0.12));
  ctx.fillRect(topLeft.x + width * 0.45, topLeft.y + inset, Math.max(3, width * 0.1), height - inset * 2);

  const roofs = cityRoofPalette(entity.ref.id, 8);
  const plots = [
    [0.13, 0.14, 0.23, 0.2], [0.61, 0.13, 0.24, 0.22],
    [0.12, 0.65, 0.25, 0.2], [0.62, 0.64, 0.23, 0.21],
    [0.2, 0.36, 0.17, 0.12], [0.64, 0.36, 0.18, 0.12],
    [0.38, 0.16, 0.08, 0.16], [0.53, 0.67, 0.08, 0.17],
  ] as const;
  plots.forEach(([x, y, w, h], index) => {
    const palette = roofs[index];
    const px = topLeft.x + width * x;
    const py = topLeft.y + height * y;
    const pw = width * w;
    const ph = height * h;
    ctx.fillStyle = palette.wall;
    ctx.fillRect(px - scale * 0.08, py + ph * 0.18, pw + scale * 0.16, ph * 0.9);
    ctx.fillStyle = palette.roof;
    ctx.fillRect(px, py, pw, ph);
    ctx.fillStyle = palette.roofLight;
    ctx.fillRect(px, py, pw, ph * 0.42);
    ctx.strokeStyle = palette.trim;
    ctx.lineWidth = Math.max(1, scale * 0.08);
    ctx.beginPath();
    ctx.moveTo(px, py + ph * 0.43);
    ctx.lineTo(px + pw, py + ph * 0.43);
    ctx.stroke();
  });

  if (entity.showBoundary !== false) {
    // Muralha espessa, quatro torres e portão: usada no mundo observado, não no marcador autoral.
    ctx.strokeStyle = isHighlighted ? SELECTION_HIGHLIGHT_COLOR : wall;
    ctx.lineWidth = Math.max(2, scale * 0.45);
    cityOutlinePath(ctx, topLeft.x + inset / 2, topLeft.y + inset / 2, width - inset, height - inset, chamfer);
    ctx.stroke();
    ctx.fillStyle = wall;
    for (const [x, y] of [[inset, inset], [width - inset, inset], [inset, height - inset], [width - inset, height - inset]]) {
      ctx.beginPath();
      ctx.arc(topLeft.x + x, topLeft.y + y, Math.max(2, scale * 0.48), 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.fillStyle = "#493126";
    ctx.fillRect(topLeft.x + width * 0.45, bottomRight.y - inset, width * 0.1, inset + scale * 0.16);
  } else if (isHighlighted) {
    ctx.strokeStyle = SELECTION_HIGHLIGHT_COLOR;
    ctx.lineWidth = Math.max(2, scale * 0.12);
    ctx.setLineDash([Math.max(3, scale * 0.25), Math.max(2, scale * 0.16)]);
    ctx.beginPath();
    ctx.ellipse(topLeft.x + width / 2, topLeft.y + height / 2, width * 0.48, height * 0.44, 0, 0, Math.PI * 2);
    ctx.stroke();
    ctx.setLineDash([]);
  }
  if (rotated) ctx.restore();
  drawLabel(ctx, entity, { x: topLeft.x + 4, y: topLeft.y - 4 }, "left");
}

function beginEntityRotation(
  ctx: CanvasRenderingContext2D,
  entity: AuthoritativeEntity,
  topLeft: { x: number; y: number },
  bottomRight: { x: number; y: number },
): boolean {
  if (!entity.rotation) return false;
  const centerX = (topLeft.x + bottomRight.x) / 2;
  const centerY = (topLeft.y + bottomRight.y) / 2;
  ctx.save();
  ctx.translate(centerX, centerY);
  ctx.rotate(entity.rotation * Math.PI / 180);
  ctx.translate(-centerX, -centerY);
  return true;
}

function cityOutlinePath(ctx: CanvasRenderingContext2D, x: number, y: number, width: number, height: number, cut: number): void {
  ctx.beginPath();
  ctx.moveTo(x + cut, y);
  ctx.lineTo(x + width - cut, y);
  ctx.lineTo(x + width, y + cut);
  ctx.lineTo(x + width, y + height - cut);
  ctx.lineTo(x + width - cut, y + height);
  ctx.lineTo(x + cut, y + height);
  ctx.lineTo(x, y + height - cut);
  ctx.lineTo(x, y + cut);
  ctx.closePath();
}

function drawMaterialDetail(
  ctx: CanvasRenderingContext2D,
  topLeft: Vec2,
  scale: number,
  material: NonNullable<AuthoritativeEntity["footprintCells"]>[number]["material"],
): void {
  if (!material || scale < 4) return;
  if (material === "roof") {
    ctx.fillStyle = "rgba(238,188,126,0.22)";
    ctx.fillRect(topLeft.x, topLeft.y, scale, Math.max(1, scale * 0.16));
    ctx.strokeStyle = "rgba(57,31,29,0.38)";
    ctx.beginPath();
    ctx.moveTo(topLeft.x, topLeft.y + scale * 0.55);
    ctx.lineTo(topLeft.x + scale, topLeft.y + scale * 0.55);
    ctx.stroke();
  } else if (material === "woodWall") {
    ctx.strokeStyle = "rgba(55,32,18,0.45)";
    ctx.beginPath();
    ctx.moveTo(topLeft.x + scale * 0.5, topLeft.y);
    ctx.lineTo(topLeft.x + scale * 0.5, topLeft.y + scale);
    ctx.stroke();
  } else if (material === "stoneWall") {
    ctx.fillStyle = "rgba(232,224,199,0.2)";
    ctx.fillRect(topLeft.x, topLeft.y, scale, Math.max(1, scale * 0.18));
  } else if (material === "door") {
    ctx.fillStyle = "#3d251b";
    ctx.fillRect(topLeft.x + scale * 0.22, topLeft.y + scale * 0.12, scale * 0.56, scale * 0.88);
    ctx.fillStyle = "#d6ad58";
    ctx.beginPath();
    ctx.arc(topLeft.x + scale * 0.65, topLeft.y + scale * 0.58, Math.max(1, scale * 0.06), 0, Math.PI * 2);
    ctx.fill();
  }
}

function drawLabel(ctx: CanvasRenderingContext2D, entity: AuthoritativeEntity, at: Vec2, align: CanvasTextAlign): void {
  ctx.font = "11px sans-serif";
  ctx.textAlign = align;
  ctx.fillStyle = "#e8e6df";
  ctx.fillText(entity.label ?? `${entity.ref.kind} ${entity.ref.id.slice(0, 8)}`, at.x, at.y);
}

/** Silhueta simples (cabeça + ombros) dentro do disco do token — sem asset/lib, só canvas 2D. */
function drawTokenGlyph(ctx: CanvasRenderingContext2D, center: Vec2, r: number): void {
  ctx.fillStyle = "rgba(20,24,32,0.55)";
  ctx.beginPath();
  ctx.arc(center.x, center.y - r * 0.32, r * 0.26, 0, Math.PI * 2);
  ctx.fill();
  ctx.beginPath();
  ctx.arc(center.x, center.y + r * 0.3, r * 0.5, Math.PI, 0, true);
  ctx.fill();
}

// Feedback do usuário (2026-08-21): a chave incluía a ação (`id:action`) — toda troca de ação
// criava e decodificava uma `Image` NOVA por NPC, sem nunca liberar as antigas (suspeito real da
// travada). Aparência é só identidade agora (`npcPawnSvg`), então a chave é só o id — no máximo
// 1 imagem cacheada por NPC, reusada pra sempre.
function drawNpcPawn(ctx: CanvasRenderingContext2D, entity: AuthoritativeEntity, center: Vec2, r: number): boolean {
  if (entity.ref.kind !== "npc" || typeof Image === "undefined") {
    return false;
  }

  let image = npcPawnImages.get(entity.ref.id);
  if (!image) {
    image = new Image();
    image.src = npcPawnDataUrl({ id: entity.ref.id });
    npcPawnImages.set(entity.ref.id, image);
  }
  if (!image.complete || image.naturalWidth === 0) {
    return false;
  }

  ctx.drawImage(image, center.x - r, center.y - r * 1.25, r * 2, r * 2.4);
  return true;
}

/** Alfa do badge de ação: pulsa só quando `animated` e o usuário não pediu `prefers-reduced-motion`
 * — cálculo puro por `Date.now()`, nunca uma imagem redesenhada (custo desprezível a 60fps). */
function actionBadgeOpacity(animated: boolean): number {
  if (!animated || prefersReducedMotion()) return 1;
  return 0.55 + 0.45 * Math.sin(Date.now() / 450);
}

function drawProgressRing(
  ctx: CanvasRenderingContext2D,
  cx: number,
  cy: number,
  radius: number,
  progress: number,
  opacity: number,
): void {
  ctx.save();
  ctx.globalAlpha = opacity;
  ctx.strokeStyle = "#c4a15a";
  ctx.lineWidth = Math.max(1.5, radius * 0.22);
  ctx.beginPath();
  ctx.arc(cx, cy, radius, PROGRESS_RING_START, progressRingEndAngle(progress));
  ctx.stroke();
  ctx.restore();
}

/**
 * Um tile representa distâncias físicas diferentes em cada nível espacial. O pawn acompanha
 * essa semântica só no desenho: mundo mantém a escala compacta; cidade aproxima a pessoa; o
 * interior aproxima mais uma vez. O footprint autoritativo continua 1x1 e o hit-test não muda.
 */
function pointVisualScale(entity: AuthoritativeEntity): number {
  if (entity.ref.kind !== "npc") return 1;
  const manifestationScale = entity.extraordinary?.isManifested
    ? entity.extraordinary.scaleMultiplier
    : 1;
  return npcVisualScale(entity.ref.space.kind) * manifestationScale;
}

const RELOCATION_ROUTE_COLOR = "#c4a574";

const SOCIAL_LINK_COLOR = "#c9899a";
const SOCIAL_LINK_DASH: [number, number] = [4, 6];

function drawSocialOverlays(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entities: readonly AuthoritativeEntity[],
  events: readonly { kind: number }[],
): void {
  const byId = new Map(entities.filter((entity) => entity.ref.kind === "npc").map((entity) => [entity.ref.id, entity]));
  const links = resolveSocialLinks(entities, events);
  for (const link of links) {
    const from = byId.get(link.fromId);
    const to = byId.get(link.toId);
    if (!from || !to) continue;
    const a = camera.worldToScreen({ x: from.position.x + 0.5, y: from.position.y + 0.5 });
    const b = camera.worldToScreen({ x: to.position.x + 0.5, y: to.position.y + 0.5 });
    ctx.save();
    ctx.strokeStyle = SOCIAL_LINK_COLOR;
    ctx.lineWidth = 2;
    ctx.setLineDash(SOCIAL_LINK_DASH);
    ctx.beginPath();
    ctx.moveTo(a.x, a.y);
    ctx.lineTo(b.x, b.y);
    ctx.stroke();
    ctx.setLineDash([]);
    ctx.restore();
  }

  const socialEvents = events.filter((event) => (SOCIAL_EVENT_KINDS as readonly number[]).includes(event.kind));
  if (socialEvents.length === 0) return;
  const reduced = prefersReducedMotion();
  const anchor = links[0]
    ? midpointOf(byId.get(links[0].fromId), byId.get(links[0].toId))
    : entities.find((entity) => entity.ref.kind === "npc")?.position;
  if (!anchor) return;
  const at = camera.worldToScreen({ x: anchor.x + 0.5, y: anchor.y + 0.5 });
  for (const event of socialEvents) {
    const spec = animationSpecForEvent(event.kind);
    const cue = cueFromSpec(spec, { reducedMotion: reduced });
    drawActionIcon(ctx, at.x, at.y - 14, 6 * cue.scale, spec.icon, cue.opacity);
  }
}

function drawLifecycleBursts(
  ctx: CanvasRenderingContext2D,
  camera: Camera,
  entities: readonly AuthoritativeEntity[],
  events: readonly { kind: number; location?: { x: number; y: number } | null }[],
): void {
  const reduced = prefersReducedMotion();
  for (const moment of resolveLifecycleMoments(events, entities)) {
    const spec = animationSpecForEvent(moment.kind);
    const cue = cueFromSpec(spec, { reducedMotion: reduced });
    const at = camera.worldToScreen({ x: moment.position.x + 0.5, y: moment.position.y + 0.5 });
    const slate = moment.kind === 0 ? "#7eb8a2" : "#8a8f9a";
    ctx.save();
    ctx.globalAlpha = cue.opacity;
    ctx.strokeStyle = slate;
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(at.x, at.y, 10 * cue.scale, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
    drawActionIcon(ctx, at.x, at.y, 6 * cue.scale, spec.icon, cue.opacity);
  }
}

function midpointOf(
  a: AuthoritativeEntity | undefined,
  b: AuthoritativeEntity | undefined,
): { x: number; y: number } | undefined {
  if (!a || !b) return undefined;
  return { x: (a.position.x + b.position.x) / 2, y: (a.position.y + b.position.y) / 2 };
}

function drawRelocationRoute(ctx: CanvasRenderingContext2D, camera: Camera, entity: AuthoritativeEntity): void {
  if (entity.ref.kind !== "npc" || !entity.travelDestination) return;
  const from = camera.worldToScreen({ x: entity.position.x + 0.5, y: entity.position.y + 0.5 });
  const to = camera.worldToScreen({ x: entity.travelDestination.x + 0.5, y: entity.travelDestination.y + 0.5 });
  ctx.save();
  ctx.strokeStyle = RELOCATION_ROUTE_COLOR;
  ctx.lineWidth = 2;
  ctx.setLineDash([7, 5]);
  ctx.beginPath();
  ctx.moveTo(from.x, from.y);
  ctx.lineTo(to.x, to.y);
  ctx.stroke();
  ctx.setLineDash([]);
  ctx.restore();
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
  const visualScale = pointVisualScale(entity);
  // Feedback do usuário (2026-08-21): o token tinha raio fixo por nível de LOD, então dar zoom
  // pra ver o NPC de perto não mudava nada — agora o raio de token acompanha `scale` (piso/teto
  // em `tokenRadiusPx`). Dot continua fixo: é o marcador de "zoom afastado", nunca o alvo do
  // "quero ver de perto".
  const r = (isToken ? tokenRadiusPx(scale) : 3) * visualScale;
  drawExtraordinaryTrail(ctx, entity, center, r);
  drawExtraordinaryFlightCue(ctx, entity, center, r);

  if (isToken) {
    // T35: pawn SVG original e determinístico, carregado uma vez por identidade e desenhado
    // no canvas. O glifo simples permanece como fallback enquanto a imagem termina de carregar.
    if (!drawNpcPawn(ctx, entity, center, r)) {
      ctx.beginPath();
      ctx.arc(center.x, center.y, r, 0, Math.PI * 2);
      ctx.fillStyle = entity.color;
      ctx.fill();
      ctx.lineWidth = Math.max(1.5, r * 0.2);
      ctx.strokeStyle = "#e8e6df";
      ctx.setLineDash(entity.sizeIsDerived ? [r * 0.3, r * 0.25] : []);
      ctx.stroke();
      ctx.setLineDash([]);
      drawTokenGlyph(ctx, center, r);
    }
    drawExtraordinaryTint(ctx, entity, center, r);
    if (entity.ref.kind === "npc" && entity.currentAction != null) {
      const visual = actionVisualFor(entity.currentAction);
      if (!visual.hidden) {
        const actionSpec = animationSpecForAction(entity.currentAction);
        const reduced = prefersReducedMotion();
        const opacity = actionSpec.key === "sleep"
          ? actionBadgeOpacity(visual.animated)
          : cueFromSpec(actionSpec, { reducedMotion: reduced, nowMs: Date.now() }).opacity;
        drawActionIcon(
          ctx, center.x + r * 0.62, center.y - r * 0.72, Math.max(4, r * 0.42),
          visual.icon, opacity,
        );
      }
    }
    if (entity.ref.kind === "npc" && entity.process && entity.process.kind !== "construction") {
      const descriptor = entity.process.descriptorKey ?? entity.process.kind;
      const cue = processCueVisual(entity.process.kind, descriptor);
      if (!cue.hidden) {
        const spec = animationSpecForProcess(descriptor);
        const resolved = spec.key === "unknown" ? animationSpecForProcess(cue.key) : spec;
        const drawCue = cueFromSpec(resolved, {
          progress: entity.process.progress,
          reducedMotion: prefersReducedMotion(),
        });
        const iconR = Math.max(4, r * 0.42) * drawCue.scale;
        const iconX = center.x - r * 0.62;
        const iconY = center.y - r * 0.72;
        drawActionIcon(ctx, iconX, iconY, iconR, cue.icon, drawCue.opacity);
        drawProgressRing(ctx, iconX, iconY, iconR * 1.28, drawCue.ringProgress, drawCue.opacity);
      }
    }
    // Rótulo só a partir do nível "token" (master prompt §4: dot fica só com a forma; rótulo é
    // "informação adicional" do zoom próximo) — feedback do usuário pedia identificar o NPC.
    drawLabel(ctx, entity, { x: center.x, y: center.y + r + 12 }, "center");
  } else {
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
    drawExtraordinaryTint(ctx, entity, center, r);
  }

  if (isHighlighted) {
    ctx.beginPath();
    ctx.arc(center.x, center.y, r + Math.max(2, r * 0.25), 0, Math.PI * 2);
    ctx.strokeStyle = SELECTION_HIGHLIGHT_COLOR;
    ctx.lineWidth = 2;
    ctx.stroke();
  }
}

function extraordinaryColor(token: string, alpha: number): string {
  let hash = 0;
  for (let index = 0; index < token.length; index += 1) {
    hash = (hash * 31 + token.charCodeAt(index)) | 0;
  }
  return `hsla(${Math.abs(hash) % 360}, 72%, 62%, ${alpha})`;
}

function drawExtraordinaryTint(
  ctx: CanvasRenderingContext2D,
  entity: AuthoritativeEntity,
  center: Vec2,
  radius: number,
): void {
  const appearance = entity.extraordinary;
  if (!appearance?.isManifested || !appearance.skinTint) return;
  ctx.save();
  ctx.fillStyle = extraordinaryColor(appearance.skinTint, 0.3);
  ctx.beginPath();
  ctx.arc(center.x, center.y, radius * 0.9, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

function drawExtraordinaryFlightCue(
  ctx: CanvasRenderingContext2D,
  entity: AuthoritativeEntity,
  center: Vec2,
  radius: number,
): void {
  const extraordinary = entity.extraordinary;
  if (!extraordinary?.isManifested || !extraordinary.canFly) return;
  ctx.save();
  ctx.fillStyle = "rgba(18, 24, 38, 0.28)";
  ctx.beginPath();
  ctx.ellipse(center.x, center.y + radius * 1.25, radius * 0.7, radius * 0.24, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

function drawExtraordinaryTrail(
  ctx: CanvasRenderingContext2D,
  entity: AuthoritativeEntity,
  center: Vec2,
  radius: number,
): void {
  const appearance = entity.extraordinary;
  if (!appearance?.isManifested || !appearance.movementTrail || entity.currentAction !== 4) return;
  let hash = 0;
  const identity = `${entity.ref.id}:${appearance.movementTrail}`;
  for (let index = 0; index < identity.length; index += 1) {
    hash = (hash * 31 + identity.charCodeAt(index)) | 0;
  }
  const angle = (Math.abs(hash) % 360) * Math.PI / 180;
  const dx = Math.cos(angle);
  const dy = Math.sin(angle);
  ctx.save();
  ctx.strokeStyle = extraordinaryColor(appearance.movementTrail, 0.55);
  ctx.lineWidth = Math.max(1.5, radius * 0.2);
  for (let streak = 0; streak < 3; streak += 1) {
    const near = radius * (0.8 + streak * 0.45);
    const far = radius * (1.5 + streak * 0.65);
    ctx.beginPath();
    ctx.moveTo(center.x - dx * near, center.y - dy * near);
    ctx.lineTo(center.x - dx * far, center.y - dy * far);
    ctx.stroke();
  }
  ctx.restore();
}
