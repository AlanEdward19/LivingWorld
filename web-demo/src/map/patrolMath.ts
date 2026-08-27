export interface GridPoint {
  x: number;
  y: number;
}

/** Duração decorativa de cada perna do trajeto — não vem de nenhum relógio de simulação
 * (AD-018: esta demo não tem simulação real rodando, o movimento é scripted). */
export const PATROL_STEP_DURATION_MS = 4000;

function interpolate(a: GridPoint, b: GridPoint, t: number): GridPoint {
  return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
}

/**
 * Posição de um NPC ao longo de um trajeto de patrulha em loop, numa timestamp arbitrária
 * (AD-018 — movimento decorativo/scripted, não derivado de simulação real). Extraída de
 * `usePatrolPosition` (AD-020) pra poder ser chamada a cada frame de um ticker Pixi (60fps,
 * fora do ciclo de re-render do React) em vez de só a cada 200ms via `setInterval`+`setState`.
 * 0 pontos → origem; 1 ponto → parado nesse ponto; 2+ pontos → interpola em loop contínuo.
 */
export function patrolPositionAt(points: GridPoint[], now: number, stepDurationMs: number = PATROL_STEP_DURATION_MS): GridPoint {
  if (points.length === 0) return { x: 0, y: 0 };
  if (points.length === 1) return points[0];

  const totalDuration = stepDurationMs * points.length;
  const elapsed = ((now % totalDuration) + totalDuration) % totalDuration;
  const segment = Math.floor(elapsed / stepDurationMs);
  const progress = (elapsed % stepDurationMs) / stepDurationMs;
  const from = points[segment];
  const to = points[(segment + 1) % points.length];
  return interpolate(from, to, progress);
}
