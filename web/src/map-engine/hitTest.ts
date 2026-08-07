// Fase 15.1, T9: hit-test tela -> entidade via câmera (design.md "Components" ->
// `SpatialContext`/hit-test). Generaliza o hit-test por raio de
// `web/src/components/GridCanvas.tsx:141-150` (que comparava distância em pixels de tela) para
// o espaço de coordenadas da `Camera` do map engine — LOD-agnóstico: quem chama decide o raio
// de acerto em pixels de tela (T6/T13 decidem esse número, não este módulo).
import type { Camera } from "./Camera";
import type { AuthoritativeEntity, EntityRef, Vec2 } from "./types";

function screenDistance(a: Vec2, b: Vec2): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

/**
 * Devolve a `EntityRef` mais próxima do ponto de clique (em coordenadas de tela) dentro de
 * `hitRadiusPx`, ou `null` se nenhuma entidade estiver ao alcance.
 */
export function hitTest(
  screenPoint: Vec2,
  camera: Camera,
  entities: AuthoritativeEntity[],
  hitRadiusPx: number,
): EntityRef | null {
  let closest: { ref: EntityRef; distance: number } | null = null;

  for (const entity of entities) {
    const entityScreenPoint = camera.worldToScreen(entity.position);
    const distance = screenDistance(screenPoint, entityScreenPoint);
    if (distance <= hitRadiusPx && (!closest || distance < closest.distance)) {
      closest = { ref: entity.ref, distance };
    }
  }

  return closest?.ref ?? null;
}
