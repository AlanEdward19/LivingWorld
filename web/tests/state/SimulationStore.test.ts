import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SimulationStore } from "../../src/state/simulationStore";
import { MockClock } from "../../src/data/mock/MockClock";
import { MockSnapshotSource } from "../../src/data/mock/MockSnapshotSource";
import { MockTickStreamSource } from "../../src/data/mock/MockTickStreamSource";
import { cityASnapshotEnvelope, npcsByScope, snapshotsByScope, worldSnapshotEnvelope } from "../../src/data/mock/fixtures";

const CITY_A = { kind: "City" as const, cityId: "city-a" };
const WORLD = { kind: "World" as const };

function buildStore() {
  const clock = new MockClock();
  clock.setSpeed(20); // rápido pra gerar deltas rápido nos testes
  const snapshotSource = new MockSnapshotSource(snapshotsByScope);
  const tickStreamSource = new MockTickStreamSource(clock, npcsByScope, 20);
  const store = new SimulationStore(snapshotSource, tickStreamSource);
  return { store, snapshotSource, tickStreamSource, clock };
}

describe("SimulationStore", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("loads the snapshot once and applies the next 10 deltas incrementally, without re-loading", async () => {
    const { store, snapshotSource } = buildStore();
    const loadSpy = vi.spyOn(snapshotSource, "load");

    await store.observeSpace(CITY_A);
    expect(loadSpy).toHaveBeenCalledTimes(1);

    for (let i = 0; i < 10; i++) {
      vi.advanceTimersByTime(50); // speed=20 tps -> 1 tick a cada 50ms
    }

    expect(loadSpy).toHaveBeenCalledTimes(1);
  });

  it("discards a snapshot envelope for a different scope than the one observed", async () => {
    const { store } = buildStore();

    await store.observeSpace(CITY_A);
    const before = store.entitiesOf(CITY_A);

    store.applySnapshot(worldSnapshotEnvelope); // escopo errado (world, não city-a)

    expect(store.entitiesOf(CITY_A)).toEqual(before);
  });

  it("rehydrates via SnapshotSource.load with backoff after a stream drop", async () => {
    const { store, snapshotSource, tickStreamSource } = buildStore();
    const loadSpy = vi.spyOn(snapshotSource, "load");

    await store.observeSpace(CITY_A);
    expect(loadSpy).toHaveBeenCalledTimes(1);

    tickStreamSource.simulateDrop(CITY_A);
    expect(loadSpy).toHaveBeenCalledTimes(1); // ainda não — o backoff não passou

    vi.advanceTimersByTime(500);
    await Promise.resolve(); // deixa a promise do reload assentar

    expect(loadSpy).toHaveBeenCalledTimes(2);
  });

  it("never constructs WebSocket nor calls fetch through the full observe/delta/drop/reload flow", async () => {
    const fetchSpy = vi.fn(() => {
      throw new Error("fetch must never be called by SimulationStore");
    });
    const webSocketSpy = vi.fn(() => {
      throw new Error("WebSocket must never be constructed by SimulationStore");
    });
    vi.stubGlobal("fetch", fetchSpy);
    vi.stubGlobal("WebSocket", webSocketSpy);

    const { store, tickStreamSource } = buildStore();
    await store.observeSpace(CITY_A);
    vi.advanceTimersByTime(200);
    tickStreamSource.simulateDrop(CITY_A);
    vi.advanceTimersByTime(500);
    await Promise.resolve();

    expect(fetchSpy).not.toHaveBeenCalled();
    expect(webSocketSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("notifies subscribed listeners synchronously on delta application, without React state", () => {
    const { store } = buildStore();
    const listener = vi.fn();
    store.subscribe(listener);

    store.applyDelta({ tick: 1, moved: [{ npcId: 3000, location: { x: 1, y: 1 } }], removed: [] });

    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("merges a moved NPC's new location into entitiesOf without touching the snapshot", async () => {
    const { store } = buildStore();
    await store.observeSpace(CITY_A);

    const before = store.entitiesOf(CITY_A).find((e) => e.ref.id === "3000");
    expect(before?.position).toEqual(cityASnapshotEnvelope.payload!.residents[0].location);

    store.applyDelta({ tick: 1, moved: [{ npcId: 3000, location: { x: 999, y: 999 } }], removed: [] });

    const after = store.entitiesOf(CITY_A).find((e) => e.ref.id === "3000");
    expect(after?.position).toEqual({ x: 999, y: 999 });
  });

  it("removes an NPC from entitiesOf once its id appears in a delta's removed list", async () => {
    const { store } = buildStore();
    await store.observeSpace(CITY_A);
    expect(store.entitiesOf(CITY_A).some((e) => e.ref.id === "3000")).toBe(true);

    store.applyDelta({ tick: 1, moved: [], removed: [3000] });

    expect(store.entitiesOf(CITY_A).some((e) => e.ref.id === "3000")).toBe(false);
  });
});
