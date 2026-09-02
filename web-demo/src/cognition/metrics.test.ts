import { describe, expect, it } from "vitest";
import { computeMetrics } from "./metrics";
import { WakeReason, type CognitionTraceEntry } from "./types";

function entry(tick: number, winner: string, winningUtility: number, wakeReason: WakeReason): CognitionTraceEntry {
  return {
    tick,
    trace: {
      wakeReason,
      previousIntent: null,
      topPressures: [],
      knownOpportunities: [],
      winner,
      winningUtility,
      topPositiveFactors: [],
      topNegativeFactors: [],
      blockingFactors: [],
      knownAlternatives: [],
    },
  };
}

describe("computeMetrics", () => {
  it("returns zeroed metrics for an empty window", () => {
    const metrics = computeMetrics([]);
    expect(metrics.totalDecisions).toBe(0);
    expect(metrics.topWinner).toBeNull();
    expect(metrics.wakeReasonBreakdown).toEqual([]);
  });

  it("aggregates count, average utility, top winner and wake-reason breakdown", () => {
    const entries = [
      entry(1, "Buy Food", 0.5, WakeReason.UrgentNeed),
      entry(2, "Buy Food", 0.7, WakeReason.UrgentNeed),
      entry(3, "Rest", 0.3, WakeReason.Scheduled),
    ];

    const metrics = computeMetrics(entries);

    expect(metrics.totalDecisions).toBe(3);
    expect(metrics.averageWinningUtility).toBeCloseTo(0.5, 5);
    expect(metrics.topWinner).toBe("Buy Food");
    expect(metrics.wakeReasonBreakdown).toEqual([
      { label: "Urgent need", count: 2 },
      { label: "Scheduled", count: 1 },
    ]);
  });
});
