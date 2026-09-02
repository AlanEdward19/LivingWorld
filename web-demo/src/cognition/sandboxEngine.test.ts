import { describe, expect, it } from "vitest";
import { tick } from "./sandboxEngine";

describe("sandboxEngine.tick", () => {
  it("is pure — same (tickNumber, previous) always yields the same entry", () => {
    const a = tick(7, null);
    const b = tick(7, null);
    expect(a).toEqual(b);
  });

  it("carries the previous winner forward as previousIntent", () => {
    const first = tick(0, null);
    const second = tick(1, first.trace);
    expect(second.trace.previousIntent).toBe(first.trace.winner);
  });

  it("produces a decision-trace shape matching the real contract's field names", () => {
    const entry = tick(3, null);
    expect(entry).toHaveProperty("tick", 3);
    expect(entry.trace).toEqual(
      expect.objectContaining({
        wakeReason: expect.any(Number),
        topPressures: expect.any(Array),
        knownOpportunities: expect.any(Array),
        winner: expect.any(String),
        winningUtility: expect.any(Number),
        topPositiveFactors: expect.any(Array),
        topNegativeFactors: expect.any(Array),
        blockingFactors: expect.any(Array),
        knownAlternatives: expect.any(Array),
      }),
    );
  });
});
