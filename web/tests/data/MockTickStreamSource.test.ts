import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { MockClock } from "../../src/data/mock/MockClock";
import { MockTickStreamSource } from "../../src/data/mock/MockTickStreamSource";
import type { ScopeTickDelta } from "../../src/data/contracts";

const NPCS = { world: [{ npcId: 1, location: { x: 0, y: 0 } }] };

describe("MockTickStreamSource", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("emits a ScopeTickDelta at the configured tick rate and advances the shared clock", () => {
    const clock = new MockClock();
    clock.setSpeed(1); // 1 tick/segundo
    const source = new MockTickStreamSource(clock, NPCS, 20);
    const deltas: ScopeTickDelta[] = [];

    source.subscribe({ kind: "World" }, (d) => deltas.push(d));
    vi.advanceTimersByTime(1000);

    expect(deltas.length).toBe(1);
    expect(deltas[0].tick).toBe(1);
    expect(deltas[0].moved).toEqual([{ npcId: 1, location: { x: 1, y: 0 } }]);
    expect(clock.tick).toBe(1);
  });

  it("stops emitting once the clock is paused", () => {
    const clock = new MockClock();
    clock.setSpeed(1);
    const source = new MockTickStreamSource(clock, NPCS, 20);
    const deltas: ScopeTickDelta[] = [];

    source.subscribe({ kind: "World" }, (d) => deltas.push(d));
    vi.advanceTimersByTime(1000);
    expect(deltas.length).toBe(1);

    clock.pause();
    vi.advanceTimersByTime(3000);
    expect(deltas.length).toBe(1); // nenhum delta novo enquanto pausado
  });

  it("emits proportionally more deltas when setSpeed increases ticksPerSecond", () => {
    const clock = new MockClock();
    clock.setSpeed(1);
    const source = new MockTickStreamSource(clock, NPCS, 20);
    const deltas: ScopeTickDelta[] = [];

    source.subscribe({ kind: "World" }, (d) => deltas.push(d));
    clock.setSpeed(4);
    vi.advanceTimersByTime(1000);

    expect(deltas.length).toBe(4);
  });

  it("stops emitting after unsubscribe", () => {
    const clock = new MockClock();
    clock.setSpeed(1);
    const source = new MockTickStreamSource(clock, NPCS, 20);
    const deltas: ScopeTickDelta[] = [];

    const unsubscribe = source.subscribe({ kind: "World" }, (d) => deltas.push(d));
    vi.advanceTimersByTime(1000);
    unsubscribe();
    vi.advanceTimersByTime(3000);

    expect(deltas.length).toBe(1);
  });

  it("returns a no-op unsubscribe for a scope with no fixture NPCs", () => {
    const clock = new MockClock();
    const source = new MockTickStreamSource(clock, {}, 20);
    const deltas: ScopeTickDelta[] = [];

    const unsubscribe = source.subscribe({ kind: "City", cityId: "empty" }, (d) => deltas.push(d));
    vi.advanceTimersByTime(1000);

    expect(deltas.length).toBe(0);
    expect(() => unsubscribe()).not.toThrow();
  });
});
