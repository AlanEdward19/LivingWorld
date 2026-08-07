// Fase 15.1, T9: hit-test tela -> entidade via câmera (design.md "Components" ->
// `SpatialContext`/hit-test). Generaliza o hit-test por raio de
// `web/src/components/GridCanvas.tsx:141-150` (que comparava distância em pixels de tela) para
// o espaço de coordenadas da `Camera` do map engine — LOD-agnóstico: quem chama decide o raio
// de acerto em pixels de tela (T6/T13 decidem esse número, não este módulo).
//
// Feedback do usuário (2026-08-07): cidade virou área real (`size.w/h > 1`, WorldMapView) — um
// clique em QUALQUER ponto dentro do footprint precisa acertar, não só perto do canto
// (`position`). Entidades de ponto (NPC, `size` 1x1) continuam pelo raio em pixels de tela.
import type { Camera } from "./Camera";
import type { AuthoritativeEntity, EntityRef, Vec2 } from "./types";

function screenDistance(a: Vec2, b: Vec2): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function isAreaEntity(entity: AuthoritativeEntity): boolean {
  return entity.size.w > 1 || entity.size.h > 1;
}

/**
 * Devolve a `EntityRef` mais próxima do ponto de clique (em coordenadas de tela) dentro de
 * `hitRadiusPx` (entidades de ponto) ou cujo footprint contém o clique (entidades de área), ou
 * `null` se nenhuma entidade estiver ao alcance.
 */
export function hitTest(
  screenPoint: Vec2,
  camera: Camera,
  entities: AuthoritativeEntity[],
  hitRadiusPx: number,
): EntityRef | null {
  const worldPoint = camera.screenToWorld(screenPoint);
  let closest: { ref: EntityRef; distance: number } | null = null;

  for (const entity of entities) {
    if (entity.decorative) {
      continue;
    }
    if (isAreaEntity(entity)) {
      const withinX = worldPoint.x >= entity.position.x && worldPoint.x <= entity.position.x + entity.size.w;
      const withinY = worldPoint.y >= entity.position.y && worldPoint.y <= entity.position.y + entity.size.h;
      if (!withinX || !withinY) {
        continue;
      }
      const center = { x: entity.position.x + entity.size.w / 2, y: entity.position.y + entity.size.h / 2 };
      const distance = Math.hypot(worldPoint.x - center.x, worldPoint.y - center.y);
      if (!closest || distance < closest.distance) {
        closest = { ref: entity.ref, distance };
      }
      continue;
    }

    // BUG real corrigido (2026-08-07): renderer.ts desenha ponto no CENTRO da célula
    // (`position + 0.5`), mas aqui usava o canto cru — erro de meia-célula que cresce com o
    // zoom (0.5*scale px), passando o raio de acerto (~0.4*scale) em telas mais zoomadas.
    // Efeito visto pelo usuário: clique só "pegava" o NPC zoomado bem pra fora.
    const entityScreenPoint = camera.worldToScreen({ x: entity.position.x + 0.5, y: entity.position.y + 0.5 });
    const distance = screenDistance(screenPoint, entityScreenPoint);
    if (distance <= hitRadiusPx && (!closest || distance < closest.distance)) {
      closest = { ref: entity.ref, distance };
    }
  }

  return closest?.ref ?? null;
}
