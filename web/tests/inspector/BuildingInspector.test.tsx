import { describe, expect, it, vi } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { BuildingInspector } from "../../src/components/inspector/BuildingInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../../src/types";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";
import type { EntityRef } from "../../src/map-engine/types";

const CITY_SPACE = { kind: "City" as const, cityId: "city-a" };

function citySnapshotSource(): SnapshotSource {
  return {
    load: async () => ({
      scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
      activeLayers: [],
      payload: {
        id: { value: "city-a" },
        location: { x: 0, y: 0 },
        aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
        residents: [],
        buildings: [{ id: { value: 8 }, buildingTypeId: 2, location: { x: 2, y: 3 }, locationIsDerived: true }],
        layers: {},
      },
    }),
  };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

describe("BuildingInspector", () => {
  it("shows the building's type from the city's own payload", async () => {
    const simulationStore = new SimulationStore(citySnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "building", id: "8", space: CITY_SPACE };
    const viewStore = new ViewStore(new MockPortalSource([]));

    render(<BuildingInspector entityRef={ref} simulationStore={simulationStore} viewStore={viewStore} />);

    expect(screen.getByText("2")).toBeInTheDocument();
  });

  it("marks the position as approximate, matching the client-side ring layout", async () => {
    const simulationStore = new SimulationStore(citySnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "building", id: "8", space: CITY_SPACE };
    const viewStore = new ViewStore(new MockPortalSource([]));

    render(<BuildingInspector entityRef={ref} simulationStore={simulationStore} viewStore={viewStore} />);

    expect(screen.getByRole("note")).toHaveTextContent("layout aproximado");
  });

  it("does not mark an authored motor coordinate as approximate", async () => {
    const simulationStore = new SimulationStore(
      {
        load: async () => ({
          scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
          mode: ViewerMode.Spectator,
          cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
          activeLayers: [],
          payload: {
            id: { value: "city-a" },
            location: { x: 0, y: 0 },
            aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
            residents: [],
            buildings: [{ id: { value: 8 }, buildingTypeId: 2, location: { x: 4, y: 5 }, locationIsDerived: false }],
            layers: {},
          },
        }),
      },
      neverStreamingTickSource(),
    );
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "building", id: "8", space: CITY_SPACE };
    const viewStore = new ViewStore(new MockPortalSource([]));

    render(<BuildingInspector entityRef={ref} simulationStore={simulationStore} viewStore={viewStore} />);

    expect(screen.queryByRole("note")).toBeNull();
  });

  it("'Abrir' calls ViewStore.enter with the Building space", async () => {
    const simulationStore = new SimulationStore(citySnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "building", id: "8", space: CITY_SPACE };
    const viewStore = new ViewStore(new MockPortalSource([]));
    const enterSpy = vi.spyOn(viewStore, "enter");

    render(<BuildingInspector entityRef={ref} simulationStore={simulationStore} viewStore={viewStore} />);
    fireEvent.click(screen.getByRole("button", { name: "Abrir" }));

    expect(enterSpy).toHaveBeenCalledWith({ kind: "Building", buildingId: "8", cityId: "city-a" });
  });
});
