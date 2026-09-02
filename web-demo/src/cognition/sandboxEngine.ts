import { WakeReason, type CognitionTraceEntry, type DecisionTrace, type Opportunity, type Pressure } from "./types";

/** Conceptual mirror of the backend `DecisionSandbox.cs` (Fase 28 P3): a synthetic decision
 * context that never touches real world state. Here it's a demo generator, not the real utility
 * engine — see plan "fora de escopo". Pure `tick()`, no timers/RNG-as-side-effect; the caller
 * (`useSandboxEngine`) owns the interval. */

const PRESSURE_POOL: Pressure[] = [
  { kind: "Hunger", intensity: 0.8, factors: ["low food stock", "missed last meal"] },
  { kind: "Fatigue", intensity: 0.55, factors: ["long shift", "poor sleep"] },
  { kind: "SocialIsolation", intensity: 0.4, factors: ["no visits this week"] },
  { kind: "FinancialStrain", intensity: 0.65, factors: ["grain price up", "low wages"] },
];

const OPPORTUNITY_POOL: Opportunity[] = [
  { kind: "BuyFood", attractiveness: 0.7, detail: "market stall nearby" },
  { kind: "Rest", attractiveness: 0.5, detail: "bed available" },
  { kind: "VisitFriend", attractiveness: 0.35, detail: "friend is home" },
  { kind: "WorkShift", attractiveness: 0.6, detail: "employer is hiring" },
];

const ACTIONS = ["Buy Food", "Rest", "Visit Friend", "Work Shift", "Idle"];

const WAKE_REASONS = [WakeReason.UrgentNeed, WakeReason.ActionCompleted, WakeReason.EventRouted, WakeReason.Scheduled];

function pick<T>(pool: T[], seed: number): T {
  return pool[Math.floor(seed * pool.length) % pool.length];
}

/** Deterministic-per-seed pseudo-random in [0, 1) — no `Math.random()` needed for a demo. */
function rand(seed: number): number {
  const x = Math.sin(seed * 12.9898) * 43758.5453;
  return x - Math.floor(x);
}

/** Produces the next synthetic trace entry from the previous tick. Pure — same `(tick, previous)`
 * always yields the same entry. */
export function tick(tickNumber: number, previous: DecisionTrace | null): CognitionTraceEntry {
  const r1 = rand(tickNumber);
  const r2 = rand(tickNumber + 0.37);
  const r3 = rand(tickNumber + 0.71);

  const topPressures = [pick(PRESSURE_POOL, r1), pick(PRESSURE_POOL, r2)].filter(
    (pressure, index, all) => all.findIndex((other) => other.kind === pressure.kind) === index,
  );
  const knownOpportunities = [pick(OPPORTUNITY_POOL, r2), pick(OPPORTUNITY_POOL, r3)].filter(
    (opportunity, index, all) => all.findIndex((other) => other.kind === opportunity.kind) === index,
  );
  const winner = pick(ACTIONS, r3);
  const alternatives = ACTIONS.filter((action) => action !== winner);

  const trace: DecisionTrace = {
    wakeReason: pick(WAKE_REASONS, r1),
    previousIntent: previous?.winner ?? null,
    topPressures,
    knownOpportunities,
    winner,
    winningUtility: Math.round(rand(tickNumber + 0.13) * 100) / 100,
    topPositiveFactors: topPressures.slice(0, 1).flatMap((pressure) => pressure.factors),
    topNegativeFactors: knownOpportunities.length === 0 ? ["no known opportunities"] : [],
    blockingFactors: r1 > 0.85 ? ["resource unavailable"] : [],
    knownAlternatives: alternatives.slice(0, 2),
  };

  return { tick: tickNumber, trace };
}
