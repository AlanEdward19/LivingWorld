import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CityInspector } from "../../src/components/inspector/CityInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../../src/types";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";
import type { SpaceId } from "../../src/map-engine/types";

function worldEnvelope() {
  return {
    scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey: "world", sequence: 0 },
    activeLayers: [],
    payload: {
      width: 10,
      height: 10,
      cities: [{ id: { value: "city-a" }, location: { x: 3, y: 4 }, population: 340, knownCarrierCount: 3 }],
      externalNpcs: [],
      activeEvents: [],
      layers: {},
    },
  };
}

function cityEnvelope() {
  return {
    scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
    activeLayers: [],
    payload: {
      id: { value: "city-a" },
      location: { x: 3, y: 4 },
      aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
      residents: [],
      buildings: [],
      layers: {},
      indicators: { population: 340, wealth: 62, health: 71, inequality: 0.34, economy: 58, housing: 44 },
      livingState: {
        npcs: [],
        cities: [],
        buildings: [],
        processes: [
          {
            id: 0,
            kind: "construction",
            targetId: 1,
            progress: 0.4,
            descriptorKey: "construction",
            location: { x: 4, y: 5 },
          },
        ],
        indicators: [],
        events: [],
      },
    },
  };
}

function multiScopeSource(): SnapshotSource {
  return { load: async (space: SpaceId) => (space.kind === "World" ? worldEnvelope() : cityEnvelope()) };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

describe("CityInspector", () => {
  it("shows only population, with an explanatory note, when the city's own snapshot isn't loaded", async () => {
    const simulationStore = new SimulationStore(multiScopeSource(), neverStreamingTickSource());
    const viewStore = new ViewStore(new MockPortalSource([]));
    await simulationStore.observeSpace({ kind: "World" }); // observando o mundo, não a cidade

    render(<CityInspector cityId="city-a" simulationStore={simulationStore} viewStore={viewStore} />);

    expect(screen.getByText("340")).toBeInTheDocument(); // população
    expect(screen.getByText("Portadores extraordinários conhecidos")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByRole("note")).toHaveTextContent("ao abrir a cidade");
    expect(screen.queryByText("Riqueza")).not.toBeInTheDocument();
  });

  it("shows all 6 CityPopulationQuery indicators when the city's own snapshot is loaded", async () => {
    const simulationStore = new SimulationStore(multiScopeSource(), neverStreamingTickSource());
    const viewStore = new ViewStore(new MockPortalSource([]));
    await simulationStore.observeSpace({ kind: "City", cityId: "city-a" });

    render(<CityInspector cityId="city-a" simulationStore={simulationStore} viewStore={viewStore} />);

    expect(screen.getByText("62")).toBeInTheDocument(); // wealth
    expect(screen.getByText("71")).toBeInTheDocument(); // health
    expect(screen.getByText("0.34")).toBeInTheDocument(); // inequality
    expect(screen.getByText("58")).toBeInTheDocument(); // economy
    expect(screen.getByText("44")).toBeInTheDocument(); // housing
    expect(screen.queryByRole("note")).not.toBeInTheDocument();
  });

  it("'Abrir' calls ViewStore.enter with the city's space", async () => {
    const simulationStore = new SimulationStore(multiScopeSource(), neverStreamingTickSource());
    const viewStore = new ViewStore(new MockPortalSource([]));
    const enterSpy = vi.spyOn(viewStore, "enter");
    await simulationStore.observeSpace({ kind: "World" });

    render(<CityInspector cityId="city-a" simulationStore={simulationStore} viewStore={viewStore} />);
    fireEvent.click(screen.getByRole("button", { name: "Abrir" }));

    expect(enterSpy).toHaveBeenCalledWith({ kind: "City", cityId: "city-a" });
  });

  it("shows queued construction progress in the city inspector before completion", async () => {
    const simulationStore = new SimulationStore(multiScopeSource(), neverStreamingTickSource());
    const viewStore = new ViewStore(new MockPortalSource([]));
    await simulationStore.observeSpace({ kind: "City", cityId: "city-a" });

    render(<CityInspector cityId="city-a" simulationStore={simulationStore} viewStore={viewStore} />);

    expect(screen.getByRole("status")).toHaveTextContent("Construção em andamento, 40%");
  });
});
