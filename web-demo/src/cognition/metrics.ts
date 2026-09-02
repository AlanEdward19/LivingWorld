import { WAKE_REASON_LABELS, type CognitionTraceEntry } from "./types";

export interface WakeReasonCount {
  label: string;
  count: number;
}

export interface CognitionMetrics {
  totalDecisions: number;
  averageWinningUtility: number;
  topWinner: string | null;
  wakeReasonBreakdown: WakeReasonCount[];
}

const EMPTY_METRICS: CognitionMetrics = {
  totalDecisions: 0,
  averageWinningUtility: 0,
  topWinner: null,
  wakeReasonBreakdown: [],
};

/** Pure aggregation over a trace window — same data whether it came from a fixture or the
 * sandbox engine. No network, no timers. */
export function computeMetrics(entries: CognitionTraceEntry[]): CognitionMetrics {
  if (entries.length === 0) return EMPTY_METRICS;

  const wakeReasonCounts = new Map<number, number>();
  const winnerCounts = new Map<string, number>();
  let utilitySum = 0;

  for (const entry of entries) {
    const { wakeReason, winner, winningUtility } = entry.trace;
    wakeReasonCounts.set(wakeReason, (wakeReasonCounts.get(wakeReason) ?? 0) + 1);
    winnerCounts.set(winner, (winnerCounts.get(winner) ?? 0) + 1);
    utilitySum += winningUtility;
  }

  let topWinner: string | null = null;
  let topWinnerCount = 0;
  for (const [winner, count] of winnerCounts) {
    if (count > topWinnerCount) {
      topWinner = winner;
      topWinnerCount = count;
    }
  }

  const wakeReasonBreakdown = [...wakeReasonCounts.entries()]
    .sort((a, b) => b[1] - a[1])
    .map(([reason, count]) => ({ label: WAKE_REASON_LABELS[reason as keyof typeof WAKE_REASON_LABELS], count }));

  return {
    totalDecisions: entries.length,
    averageWinningUtility: utilitySum / entries.length,
    topWinner,
    wakeReasonBreakdown,
  };
}
