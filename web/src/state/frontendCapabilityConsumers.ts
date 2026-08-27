import type {
  BuildingVisual,
  CityVisual,
  IndicatorUpdate,
  LivingScopeStateWire,
  NotableVisualEvent,
  NpcVisual,
  ProcessVisual,
  ScopeTickDelta,
} from "../data/contracts";

export interface LivingViewState {
  tick: number;
  npcs: ReadonlyMap<number, NpcVisual>;
  cities: ReadonlyMap<string, CityVisual>;
  buildings: ReadonlyMap<number, BuildingVisual>;
  processes: ReadonlyMap<number, ProcessVisual>;
  indicators: ReadonlyMap<string, IndicatorUpdate>;
  events: readonly NotableVisualEvent[];
}

type ConsumerSlice = "clock" | "npcs" | "cities" | "buildings" | "processes" | "indicators" | "events";

export const frontendCapabilityConsumers: Record<string, ConsumerSlice> = {
  "hud.clock": "clock",
  "map.geography": "cities",
  "inspector.npc.needs": "npcs",
  "map.npc.action": "npcs",
  "map.npc.rest": "processes",
  "inspector.npc.food": "processes",
  "map.crop": "processes",
  "map.fauna": "events",
  "map.water": "processes",
  "inspector.employment": "events",
  "inspector.production": "buildings",
  "inspector.market": "indicators",
  "timeline.wages": "events",
  "timeline.money": "events",
  "inspector.npc.skills": "npcs",
  "inspector.npc.relationships": "npcs",
  "timeline.birth": "events",
  "timeline.death": "events",
  "inspector.history.archive": "events",
  "inspector.city.growth": "indicators",
  "inspector.city.construction": "buildings",
  "map.migration": "cities",
  "hud.materialization": "npcs",
  "map.founding": "cities",
  "timeline.knowledge": "events",
  "timeline.books": "events",
  "inspector.narrative": "events",
  "interaction.conversation": "events",
  "hud.period": "events",
  "inspector.npc.extraordinary": "npcs",
  "map.extraordinary.construct": "processes",
  "inspector.npc.authoring": "events",
};

// Instância única e imutável: `useSyncExternalStore` (LivingTimeline) exige que getSnapshot
// devolva a mesma referência quando nada mudou, senão React entra em loop de re-render infinito.
const EMPTY_LIVING_VIEW_STATE: LivingViewState = {
  tick: 0,
  npcs: new Map(), cities: new Map(), buildings: new Map(), processes: new Map(),
  indicators: new Map(), events: [],
};

export function emptyLivingViewState(): LivingViewState {
  return EMPTY_LIVING_VIEW_STATE;
}

export function livingViewStateFromWire(wire?: LivingScopeStateWire): LivingViewState {
  if (!wire) return emptyLivingViewState();
  return {
    tick: 0,
    npcs: new Map(wire.npcs.map((item) => [item.id.value, item])),
    cities: new Map(wire.cities.map((item) => [item.id.value, item])),
    buildings: new Map(wire.buildings.map((item) => [item.id.value, item])),
    processes: new Map(wire.processes.map((item) => [item.id, item])),
    indicators: new Map(wire.indicators.map((item) => [item.key, item])),
    events: wire.events,
  };
}

export function applyLivingDelta(state: LivingViewState, delta: ScopeTickDelta): LivingViewState {
  const npcs = new Map(state.npcs);
  const cities = new Map(state.cities);
  const buildings = new Map(state.buildings);
  const processes = new Map(state.processes);

  for (const moved of delta.moved ?? []) {
    const previous = npcs.get(moved.npcId);
    npcs.set(moved.npcId, previous
      ? { ...previous, location: moved.location }
      : { id: { value: moved.npcId }, location: moved.location, currentAction: null });
  }
  for (const item of delta.npcUpserts ?? []) npcs.set(item.id.value, item);
  for (const id of delta.removed ?? []) npcs.delete(id);
  for (const id of delta.npcRemoved ?? []) npcs.delete(id.value);
  for (const item of delta.cityUpserts ?? []) cities.set(item.id.value, item);
  for (const id of delta.cityRemoved ?? []) cities.delete(id.value);
  for (const item of delta.buildingUpserts ?? []) buildings.set(item.id.value, item);
  for (const id of delta.buildingRemoved ?? []) buildings.delete(id.value);
  for (const item of delta.processUpserts ?? []) processes.set(item.id, item);
  for (const id of delta.processRemoved ?? []) processes.delete(id);

  return {
    tick: delta.tick,
    npcs, cities, buildings, processes,
    indicators: delta.indicators === undefined
      ? state.indicators
      : new Map(delta.indicators.map((item) => [item.key, item])),
    events: delta.events ?? state.events,
  };
}

export function readCapabilityConsumer(key: string, state: LivingViewState): unknown {
  const slice = frontendCapabilityConsumers[key];
  if (!slice) throw new Error(`unknown frontend capability consumer: ${key}`);
  return slice === "clock" ? state.tick : state[slice];
}
