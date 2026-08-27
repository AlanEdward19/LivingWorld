import { useEffect, useState } from "react";
import { patrolPositionAt, type GridPoint } from "./patrolMath";

export type { GridPoint };

/**
 * Posição atual de um NPC ao longo de um trajeto de patrulha em loop (AD-018 — movimento
 * decorativo/scripted, não derivado de simulação real). Wrapper React de `patrolPositionAt`
 * (AD-020) — reavalia a cada 200ms via `setInterval`/`setState`; quem precisa de granularidade
 * de frame (o renderer Pixi do Settlement View) chama `patrolPositionAt` direto num ticker.
 */
export function usePatrolPosition(points: GridPoint[]): GridPoint {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (points.length < 2) return;
    const id = setInterval(() => setNow(Date.now()), 200);
    return () => clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [points.length]);

  return patrolPositionAt(points, now);
}
