import { useEffect, useState } from "react";

export interface GridPoint {
  x: number;
  y: number;
}

/** Duração decorativa de cada perna do trajeto — não vem de nenhum relógio de simulação
 * (AD-018: esta demo não tem simulação real rodando, o movimento é scripted). */
const STEP_DURATION_MS = 4000;

function interpolate(a: GridPoint, b: GridPoint, t: number): GridPoint {
  return { x: a.x + (b.x - a.x) * t, y: a.y + (b.y - a.y) * t };
}

/**
 * Posição atual de um NPC ao longo de um trajeto de patrulha em loop (AD-018 — movimento
 * decorativo/scripted, não derivado de simulação real). 0 pontos → origem; 1 ponto → parado
 * nesse ponto; 2+ pontos → interpola em loop contínuo entre eles.
 */
export function usePatrolPosition(points: GridPoint[]): GridPoint {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (points.length < 2) return;
    const id = setInterval(() => setNow(Date.now()), 200);
    return () => clearInterval(id);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [points.length]);

  if (points.length === 0) return { x: 0, y: 0 };
  if (points.length === 1) return points[0];

  const totalDuration = STEP_DURATION_MS * points.length;
  const elapsed = ((now % totalDuration) + totalDuration) % totalDuration;
  const segment = Math.floor(elapsed / STEP_DURATION_MS);
  const progress = (elapsed % STEP_DURATION_MS) / STEP_DURATION_MS;
  const from = points[segment];
  const to = points[(segment + 1) % points.length];
  return interpolate(from, to, progress);
}
