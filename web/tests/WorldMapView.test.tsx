import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { WorldMapView } from "../src/components/WorldMapView";
import { SimulationStore } from "../src/state/simulationStore";
import { ViewStore } from "../src/state/viewStore";
import { SelectionStore } from "../src/state/selectionStore";
import { MockPortalSource } from "../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../src/types";
import type { FutureGlobalSnapshot } from "../src/data/contracts";
import type { SnapshotSource, TickStreamSource } from "../src/data/sources";

const VIEWPORT = { width: 200, height: 200 };
const WORLD_KEY = "world";

function makeSnapshot(): FutureGlobalSnapshot {
  return {
    width: 10,
    height: 10,
    cities: [
      {
        id: { value: "city-1" },
        location: { x: 3, y: 4 },
        population: 42,
        bounds: { x: 3, y: 4, width: 2, height: 2 },
        boundsAreDerived: true,
      },
    ],
    externalNpcs: [{ id: { value: 9 }, location: { x: 1, y: 1 } }],
    activeEvents: [],
    layers: {
      Terrain: { isModeled: true, payload: [] },
      Biome: { isModeled: true, payload: [] },
      Rivers: { isModeled: true, payload: [] },
      Mountains: { isModeled: false, payload: null },
      Resources: { isModeled: true, payload: [] },
      Roads: { isModeled: false, payload: null },
      Borders: { isModeled: false, payload: null },
      Kingdoms: { isModeled: false, payload: null },
      Cities: { isModeled: false, payload: null },
      Villages: { isModeled: false, payload: null },
      Routes: { isModeled: false, payload: null },
      Migrations: { isModeled: false, payload: null },
      Conflicts: { isModeled: false, payload: null },
      Climate: { isModeled: false, payload: null },
    },
  };
}

function stubRect(canvas: HTMLCanvasElement) {
  vi.spyOn(canvas, "getBoundingClientRect").mockReturnValue({
    left: 0,
    top: 0,
    width: canvas.width,
    height: canvas.height,
    right: canvas.width,
    bottom: canvas.height,
    x: 0,
    y: 0,
    toJSON: () => "",
  });
}

function worldSnapshotSource(snapshot: FutureGlobalSnapshot): SnapshotSource {
  return {
    load: async () => ({
      scope: { kind: VisualScopeKind.World, refId: "", scopeKey: WORLD_KEY },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: WORLD_KEY, sequence: 0 },
      activeLayers: [],
      payload: snapshot,
    }),
  };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

async function buildStores(snapshot: FutureGlobalSnapshot) {
  const simulationStore = new SimulationStore(worldSnapshotSource(snapshot), neverStreamingTickSource());
  const viewStore = new ViewStore(new MockPortalSource([]));
  const selectionStore = new SelectionStore();
  await simulationStore.observeSpace({ kind: "World" });
  return { simulationStore, viewStore, selectionStore };
}

describe("WorldMapView", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("selects a city on click, using its real world coordinates", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    render(
      <WorldMapView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    // câmera de fit inicial: mundo 10x10 num viewport 200x200 -> scale 20; cidade em (3,4)
    // projeta em ((3-5)*20+100, (4-5)*20+100) = (60,80)
    fireEvent.click(canvas, { clientX: 60, clientY: 80 });

    expect(selectionStore.current()).toEqual({ kind: "city", id: "city-1", space: { kind: "World" } });
  });

  it("navigates into the city on double click", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    const enterSpy = vi.spyOn(viewStore, "enter");
    render(
      <WorldMapView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.doubleClick(canvas, { clientX: 60, clientY: 80 });

    expect(enterSpy).toHaveBeenCalledWith({ kind: "City", cityId: "city-1" });
  });

  it("selects an external NPC on click, and it does not navigate on double click", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    const enterSpy = vi.spyOn(viewStore, "enter");
    render(
      <WorldMapView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    // npc em (1,1) -> centro do token em (1.5,1.5) -> ((1.5-5)*20+100, (1.5-5)*20+100) = (30,30)
    // (hitTest.ts mira o centro da célula, igual ao renderer — não o canto cru)
    fireEvent.click(canvas, { clientX: 30, clientY: 30 });
    expect(selectionStore.current()).toEqual({ kind: "npc", id: "9", space: { kind: "World" } });

    fireEvent.doubleClick(canvas, { clientX: 30, clientY: 30 });
    expect(enterSpy).not.toHaveBeenCalled();
  });

  it("labels not-yet-modeled layers distinctly from available ones, behind the collapsible legend", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    render(
      <WorldMapView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.queryByText(/Terrain: dispon/)).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Camadas/ }));

    expect(screen.getByText(/Terrain: dispon/)).toBeInTheDocument();
    expect(screen.getByText(/Roads: ainda não modelada/)).toBeInTheDocument();
  });

  it("renders only the map's own single canvas — no per-marker DOM node", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    render(
      <WorldMapView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.getAllByTestId("map-view-canvas")).toHaveLength(1);
  });
});
