import { describe, expect, it, vi, beforeEach, afterEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { NpcInspector } from "../../src/components/inspector/NpcInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../../src/types";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";
import type { EntityRef } from "../../src/map-engine/types";

const CITY_SPACE = { kind: "City" as const, cityId: "city-a" };
const WORLD_SPACE = { kind: "World" as const };

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
        residents: [{ id: { value: 3 }, location: { x: 1, y: 1 }, currentAction: 5 }],
        buildings: [],
        layers: {},
      },
    }),
  };
}

function worldSnapshotSource(): SnapshotSource {
  return {
    load: async () => ({
      scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "world", sequence: 0 },
      activeLayers: [],
      payload: {
        width: 10,
        height: 10,
        cities: [],
        externalNpcs: [{ id: { value: 9 }, location: { x: 2, y: 2 } }], // sem currentAction
        activeEvents: [],
        layers: {},
      },
    }),
  };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

describe("NpcInspector", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "fetch",
      vi.fn(() => {
        throw new Error("NpcInspector must never call fetch");
      }),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("shows the position from entitiesOf and the current action when present (resident)", async () => {
    const simulationStore = new SimulationStore(citySnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "npc", id: "3", space: CITY_SPACE };

    render(<NpcInspector entityRef={ref} simulationStore={simulationStore} viewStore={new ViewStore(new MockPortalSource([]))} />);

    expect(screen.getByText("(1, 1)")).toBeInTheDocument();
    expect(screen.getByText("5")).toBeInTheDocument();
  });

  it("omits 'Ação atual' for an external NPC that has no such field", async () => {
    const simulationStore = new SimulationStore(worldSnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(WORLD_SPACE);
    const ref: EntityRef = { kind: "npc", id: "9", space: WORLD_SPACE };

    render(<NpcInspector entityRef={ref} simulationStore={simulationStore} viewStore={new ViewStore(new MockPortalSource([]))} />);

    expect(screen.getByText("(2, 2)")).toBeInTheDocument();
    expect(screen.queryByText("Ação atual")).not.toBeInTheDocument();
  });

  it("does not reveal full details until 'Ver detalhes' is clicked", async () => {
    const simulationStore = new SimulationStore(citySnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "npc", id: "3", space: CITY_SPACE };

    render(<NpcInspector entityRef={ref} simulationStore={simulationStore} viewStore={new ViewStore(new MockPortalSource([]))} />);
    expect(screen.queryByRole("note")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Ver detalhes" }));

    expect(screen.getByRole("note")).toHaveTextContent("ainda não modelado");
  });

  it("never issues a fetch, neither on selection nor on the 'Ver detalhes' toggle", async () => {
    const simulationStore = new SimulationStore(citySnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "npc", id: "3", space: CITY_SPACE };

    render(<NpcInspector entityRef={ref} simulationStore={simulationStore} viewStore={new ViewStore(new MockPortalSource([]))} />);
    fireEvent.click(screen.getByRole("button", { name: "Ver detalhes" }));

    expect(fetch).not.toHaveBeenCalled();
  });

  it("never renders an 'Abrir' action — NPCs are not navigable", async () => {
    const simulationStore = new SimulationStore(citySnapshotSource(), neverStreamingTickSource());
    await simulationStore.observeSpace(CITY_SPACE);
    const ref: EntityRef = { kind: "npc", id: "3", space: CITY_SPACE };

    render(<NpcInspector entityRef={ref} simulationStore={simulationStore} viewStore={new ViewStore(new MockPortalSource([]))} />);

    expect(screen.queryByRole("button", { name: "Abrir" })).not.toBeInTheDocument();
  });
});
