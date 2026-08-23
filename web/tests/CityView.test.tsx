import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { CityView } from "../src/components/CityView";
import { SimulationStore } from "../src/state/simulationStore";
import { ViewStore } from "../src/state/viewStore";
import { SelectionStore } from "../src/state/selectionStore";
import { MockPortalSource } from "../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../src/types";
import type { CitySnapshot } from "../src/types";
import type { SnapshotSource, TickStreamSource } from "../src/data/sources";
import { cityBuildingEntityFromMarker } from "../src/map-engine/cityBuildingPlacement";
import { computeFitZoom } from "../src/gridFit";

const VIEWPORT = { width: 200, height: 200 };
const CITY_SCOPE_KEY = "city:city-1";

function makeSnapshot(): CitySnapshot {
  return {
    id: { value: "city-1" },
    name: "Cidade Um",
    location: { x: 0, y: 0 },
    aggregatePool: { count: 5, wealthSum: 500, healthSum: 400 },
    residents: [{ id: { value: 3 }, location: { x: 1, y: 1 }, currentAction: null }],
    pendingResidentIds: [42, 43],
    buildings: [{ id: { value: 8 }, buildingTypeId: 2, location: { x: 2, y: 3 }, locationIsDerived: true }],
    layers: {} as CitySnapshot["layers"],
    bounds: { x: -8, y: -8, width: 16, height: 16 },
    boundsAreDerived: true,
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

function citySnapshotSource(snapshot: CitySnapshot): SnapshotSource {
  return {
    load: async () => ({
      scope: { kind: VisualScopeKind.City, refId: "city-1", scopeKey: CITY_SCOPE_KEY },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: CITY_SCOPE_KEY, sequence: 0 },
      activeLayers: [],
      payload: snapshot,
    }),
  };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

async function buildStores(snapshot: CitySnapshot) {
  const simulationStore = new SimulationStore(citySnapshotSource(snapshot), neverStreamingTickSource());
  const viewStore = new ViewStore(new MockPortalSource([]));
  const selectionStore = new SelectionStore();
  await simulationStore.observeSpace({ kind: "City", cityId: "city-1" });
  return { simulationStore, viewStore, selectionStore };
}

describe("CityView", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("selects a resident on click, at its real absolute position", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    render(
      <CityView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    // câmera inicial: center = snapshot.location (0,0), scale 8; resident em (1,1) tem centro
    // de token em (1.5,1.5) -> ((1.5-0)*8+100, (1.5-0)*8+100) = (112,112) (hitTest.ts mira o
    // centro da célula, igual ao renderer — não o canto cru)
    fireEvent.click(canvas, { clientX: 112, clientY: 112 });

    expect(selectionStore.current()).toEqual({
      kind: "npc",
      id: "3",
      space: { kind: "City", cityId: "city-1" },
    });
  });

  it("navigates into the building on double click (drill-down to interior)", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    const enterSpy = vi.spyOn(viewStore, "enter");
    render(
      <CityView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    const snapshot = makeSnapshot();
    const entity = cityBuildingEntityFromMarker(
      snapshot.buildings[0],
      { kind: "City", cityId: "city-1" },
      0,
    );
    const scale = computeFitZoom(snapshot.bounds.width, snapshot.bounds.height, VIEWPORT.width, VIEWPORT.height);
    fireEvent.doubleClick(canvas, {
      clientX: (entity.position.x + 0.5) * scale + VIEWPORT.width / 2,
      clientY: (entity.position.y + 0.5) * scale + VIEWPORT.height / 2,
    });

    expect(enterSpy).toHaveBeenCalledWith({ kind: "Building", buildingId: "8", cityId: "city-1" });
  });

  it("does not treat the historical ring cell as the completed building", async () => {
    const snapshot = makeSnapshot();
    const { simulationStore, viewStore, selectionStore } = await buildStores(snapshot);
    const enterSpy = vi.spyOn(viewStore, "enter");
    render(
      <CityView
        snapshot={snapshot}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    const scale = computeFitZoom(snapshot.bounds.width, snapshot.bounds.height, VIEWPORT.width, VIEWPORT.height);
    fireEvent.doubleClick(canvas, {
      clientX: (6 + 0.5) * scale + VIEWPORT.width / 2,
      clientY: (0 + 0.5) * scale + VIEWPORT.height / 2,
    });

    expect(enterSpy).not.toHaveBeenCalled();
  });

  it("marks the building as a derived (approximate) position, unlike a resident's real one", async () => {
    const { simulationStore } = await buildStores(makeSnapshot());
    const entities = simulationStore.entitiesOf({ kind: "City", cityId: "city-1" });

    expect(entities.find((e) => e.ref.id === "3")?.sizeIsDerived).toBe(false);
  });

  it("shows the aggregate pool indicators in the HUD", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    render(
      <CityView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.getByText(/500/)).toBeInTheDocument();
  });

  it("shows the authored city name in its heading instead of the raw id", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores(makeSnapshot());
    render(
      <CityView
        snapshot={makeSnapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.getByText("Cidade Cidade Um")).toBeInTheDocument();
  });

  it("shows construction progress in the city HUD from livingState processes", async () => {
    const snapshot = makeSnapshot();
    const source: SnapshotSource = {
      load: async () => ({
        scope: { kind: VisualScopeKind.City, refId: "city-1", scopeKey: CITY_SCOPE_KEY },
        mode: ViewerMode.Spectator,
        cursor: { tick: 0, scopeKey: CITY_SCOPE_KEY, sequence: 0 },
        activeLayers: [],
        payload: {
          ...snapshot,
          livingState: {
            npcs: [],
            cities: [],
            buildings: [],
            processes: [
              {
                id: 0,
                kind: "construction",
                targetId: 2,
                progress: 0.25,
                descriptorKey: "construction",
                location: { x: 2, y: 3 },
              },
            ],
            indicators: [],
            events: [],
          },
        },
      }),
    };
    const simulationStore = new SimulationStore(source, neverStreamingTickSource());
    const viewStore = new ViewStore(new MockPortalSource([]));
    const selectionStore = new SelectionStore();
    await simulationStore.observeSpace({ kind: "City", cityId: "city-1" });

    render(
      <CityView
        snapshot={snapshot}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.getByRole("status")).toHaveTextContent("Construção em andamento, 25%");
  });
});
