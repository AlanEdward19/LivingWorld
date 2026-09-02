/** TS mirror of the real Phase 28 cognition contract (`src/LivingWorld.Domain/Cognition/*.cs`).
 * Field names/shapes match the backend exactly; `Winner`/`PreviousIntent`/`KnownAlternatives` are
 * `string` here (not the C# `ActionType` int enum) because web-demo has no such enum — NPCs use
 * free-text intents (`AgentFixture.currentIntent`). No network, no real backend — fixture/sandbox
 * only (see plan). */

/** Mirrors `WakeReason` (`DecisionTrace.cs`) — values match the backend enum exactly. */
export enum WakeReason {
  Unknown = 0,
  UrgentNeed = 1,
  ActionCompleted = 2,
  EventRouted = 3,
  Scheduled = 4,
}

export const WAKE_REASON_LABELS: Record<WakeReason, string> = {
  [WakeReason.Unknown]: "Unknown",
  [WakeReason.UrgentNeed]: "Urgent need",
  [WakeReason.ActionCompleted]: "Action completed",
  [WakeReason.EventRouted]: "Event routed",
  [WakeReason.Scheduled]: "Scheduled",
};

/** Mirrors `Pressure.cs` — "why act?" */
export interface Pressure {
  kind: string;
  intensity: number;
  factors: string[];
}

/** Mirrors `Opportunity.cs` — "what can I do?" */
export interface Opportunity {
  kind: string;
  attractiveness: number;
  detail?: string;
}

/** Mirrors `DecisionTrace.cs`. */
export interface DecisionTrace {
  wakeReason: WakeReason;
  previousIntent: string | null;
  topPressures: Pressure[];
  knownOpportunities: Opportunity[];
  winner: string;
  winningUtility: number;
  topPositiveFactors: string[];
  topNegativeFactors: string[];
  blockingFactors: string[];
  knownAlternatives: string[];
}

/** Mirrors `TraceEntry.cs` (`NpcCognitionLog.cs`). */
export interface CognitionTraceEntry {
  tick: number;
  trace: DecisionTrace;
}
