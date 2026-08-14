import { describe, expect, it, vi } from "vitest";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";
import type { ScopeTickDelta } from "../../src/data/contracts";
import type { SpaceId } from "../../src/map-engine/types";
import { VisualScopeKind, ViewerMode, type VisualSnapshotEnvelope } from "../../src/types";
import {
  applyLivingDelta,
  emptyLivingViewState,
  frontendCapabilityConsumers,
  readCapabilityConsumer,
} from "../../src/state/frontendCapabilityConsumers";
import { SimulationStore } from "../../src/state/simulationStore";

const WORLD = { kind: "World" as const };
const comprehensiveDelta: ScopeTickDelta = {
  tick: 8,
  moved: [],
  removed: [],
  npcUpserts: [{ id: { value: 1 }, location: { x: 2, y: 3 }, currentAction: 2 }],
  npcRemoved: [],
  cityUpserts: [{ id: { value: "city-a" }, location: { x: 4, y: 5 }, population: 10, bounds: { x: 3, y: 4, width: 3, height: 3 } }],
  cityRemoved: [],
  buildingUpserts: [{ id: { value: 7 }, cityId: { value: "city-a" }, buildingTypeId: 2, location: { x: 5, y: 5 } }],
  buildingRemoved: [],
  processUpserts: [{ id: 3, kind: "rest", targetId: 7, progress: 0.5, descriptorKey: "sleep" }],
  processRemoved: [],
  indicators: [{ key: "population", value: 10 }],
  events: [{ tick: 8, kind: 0, label: "Um novo habitante nasceu" }],
};

describe("frontendCapabilityConsumers", () => {
  it("every registered consumer resolves to state changed by a representative living delta", () => {
    const before = emptyLivingViewState();
    const after = applyLivingDelta(before, comprehensiveDelta);

    for (const key of Object.keys(frontendCapabilityConsumers)) {
      expect(readCapabilityConsumer(key, after)).not.toEqual(readCapabilityConsumer(key, before));
    }
  });

  it("normalizes entity upserts and removals by typed id", () => {
    const populated = applyLivingDelta(emptyLivingViewState(), comprehensiveDelta);
    const removed = applyLivingDelta(populated, {
      ...comprehensiveDelta,
      tick: 9,
      npcUpserts: [], cityUpserts: [], buildingUpserts: [], processUpserts: [],
      npcRemoved: [{ value: 1 }], cityRemoved: [{ value: "city-a" }],
      buildingRemoved: [{ value: 7 }], processRemoved: [3],
    });

    expect([...removed.npcs, ...removed.cities, ...removed.buildings, ...removed.processes]).toEqual([]);
  });

  it("the same sequenced delta is idempotent in SimulationStore", async () => {
    const { store, stream } = buildSequencedStore();
    await store.observeSpace(WORLD);
    const listener = vi.fn();
    store.subscribe(listener);
    const delta = { ...comprehensiveDelta, fromSequence: 5, sequence: 6 };

    stream.emit(delta);
    stream.emit(delta);

    expect(store.livingStateOf(WORLD).npcs).toHaveLength(1);
    expect(listener).toHaveBeenCalledTimes(1);
  });

  it("a sequence gap reloads the authoritative snapshot instead of applying the delta", async () => {
    const { store, stream, snapshot } = buildSequencedStore();
    await store.observeSpace(WORLD);

    stream.emit({ ...comprehensiveDelta, fromSequence: 7, sequence: 8 });
    await Promise.resolve();

    expect(snapshot.load).toHaveBeenCalledTimes(2);
    expect(store.livingStateOf(WORLD).npcs).toHaveLength(0);
  });

  it("valid next-sequence deltas update normalized NPC, city, building, indicator and event state", async () => {
    const { store, stream } = buildSequencedStore();
    await store.observeSpace(WORLD);

    stream.emit({ ...comprehensiveDelta, fromSequence: 5, sequence: 6 });
    const state = store.livingStateOf(WORLD);

    expect([state.npcs.size, state.cities.size, state.buildings.size, state.indicators.size, state.events.length])
      .toEqual([1, 1, 1, 1, 1]);
  });
});

class ManualTickStream implements TickStreamSource {
  private listener: ((delta: ScopeTickDelta) => void) | null = null;
  subscribe(_space: SpaceId, onDelta: (delta: ScopeTickDelta) => void): () => void {
    this.listener = onDelta;
    return () => { this.listener = null; };
  }
  emit(delta: ScopeTickDelta): void { this.listener?.(delta); }
}

function buildSequencedStore() {
  const envelope: VisualSnapshotEnvelope<unknown> = {
    scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
    mode: ViewerMode.Spectator,
    cursor: { tick: 5, scopeKey: "world", sequence: 5 },
    activeLayers: [],
    payload: { livingState: { npcs: [], cities: [], buildings: [], processes: [], indicators: [], events: [] } },
  };
  const snapshot: SnapshotSource = { load: vi.fn(async () => envelope) };
  const stream = new ManualTickStream();
  return { store: new SimulationStore(snapshot, stream), stream, snapshot };
}
