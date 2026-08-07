import { describe, expect, it } from "vitest";
import { MockClock } from "../../src/data/mock/MockClock";
import { MockTimeControlSource } from "../../src/data/mock/MockTimeControlSource";

describe("MockTimeControlSource", () => {
  it("reflects pause/resume/speed changes in status()", async () => {
    const clock = new MockClock();
    const source = new MockTimeControlSource(clock);

    await source.pause();
    expect((await source.status()).isPaused).toBe(true);

    await source.resume();
    expect((await source.status()).isPaused).toBe(false);

    await source.setSpeed(4);
    expect((await source.status()).ticksPerSecond).toBe(4);
  });

  it("rejects setSpeed with a non-positive value", async () => {
    const source = new MockTimeControlSource(new MockClock());
    await expect(source.setSpeed(0)).rejects.toThrow();
  });

  it("advances exactly one tick on step() while paused", async () => {
    const clock = new MockClock();
    const source = new MockTimeControlSource(clock);

    await source.pause();
    await source.step();

    expect((await source.status()).tick).toBe(1);
  });

  it("rejects step() while not paused, matching the real endpoint's 409 semantics", async () => {
    const source = new MockTimeControlSource(new MockClock());
    await expect(source.step()).rejects.toThrow();
  });
});
