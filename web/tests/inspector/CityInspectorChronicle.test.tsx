import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { CityInspector } from "../../src/components/inspector/CityInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { MockNpcInspectionSource } from "../../src/data/mock/MockNpcInspectionSource";
import { MockChronicleSource } from "../../src/data/mock/MockChronicleSource";
import type { FutureCitySnapshot } from "../../src/data/contracts";
import type { NarrativeSources, SnapshotSource, TickStreamSource } from "../../src/data/sources";

// Fase 15.1, T7 (LWV-05): crônica em CityInspector — não existia arquivo próprio para
// CityInspector ainda; este cobre só a seção nova.
const CITY_SPACE = { kind: "City" as const, cityId: "city-a" };

const CITY_PAYLOAD: FutureCitySnapshot = {
  id: { value: "city-a" }, name: "Vilarena", location: { x: 0, y: 0 },
  aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 }, pendingResidentIds: [],
  residents: [], buildings: [], layers: {} as FutureCitySnapshot["layers"],
  bounds: { x: 0, y: 0, width: 16, height: 16 }, boundsAreDerived: true,
  indicators: { population: 10, wealth: 1, health: 1, inequality: 0, economy: 1, housing: 1 },
};

function snapshotSource(): SnapshotSource {
  return { load: async () => ({
    scope: { kind: 1, refId: "city-a", scopeKey: "city:city-a" }, mode: 0,
    cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 }, activeLayers: [],
    payload: CITY_PAYLOAD,
  }) };
}

const ticks: TickStreamSource = { subscribe: () => () => {} };

async function renderCityInspector(narrativeSources: NarrativeSources) {
  const store = new SimulationStore(snapshotSource(), ticks, new MockNpcInspectionSource(new Map()));
  await store.observeSpace(CITY_SPACE);
  render(<CityInspector
    cityId="city-a"
    simulationStore={store}
    viewStore={new ViewStore(new MockPortalSource([]))}
    narrativeSources={narrativeSources}
  />);
  await screen.findByText(/Vilarena/);
}

describe("CityInspector chronicle surface (T7)", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn(() => { throw new Error("component must not fetch directly"); })));
  afterEach(() => vi.unstubAllGlobals());

  it("narrates the chronicle loaded from the injected source for the open city, not from fetch", async () => {
    await renderCityInspector({
      biography: { load: async () => null },
      chronicle: new MockChronicleSource(new Map([["city-a", { prose: "A colheita foi farta este ano." }]])),
      conversation: { start: async () => ({ accepted: false, reason: "unused" }), send: async () => ({ ok: false, reason: "session-not-found" }), end: async () => {} },
    });

    expect(await screen.findByText("A colheita foi farta este ano.")).toBeInTheDocument();
  });

  it("falls back to an honest empty narration for a city with no chronicle yet", async () => {
    await renderCityInspector({
      biography: { load: async () => null },
      chronicle: new MockChronicleSource(new Map()),
      conversation: { start: async () => ({ accepted: false, reason: "unused" }), send: async () => ({ ok: false, reason: "session-not-found" }), end: async () => {} },
    });

    expect(await screen.findByText("sem registros ancorados para este período.")).toBeInTheDocument();
  });

  it("omits the chronicle section entirely when no narrative source is injected", async () => {
    const store = new SimulationStore(snapshotSource(), ticks, new MockNpcInspectionSource(new Map()));
    await store.observeSpace(CITY_SPACE);
    render(<CityInspector cityId="city-a" simulationStore={store} viewStore={new ViewStore(new MockPortalSource([]))} />);

    await screen.findByText(/Vilarena/);
    expect(screen.queryByText("Crônica")).not.toBeInTheDocument();
  });
});
