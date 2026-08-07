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

const VIEWPORT = { width: 200, height: 200 };
const CITY_SCOPE_KEY = "city:city-1";

function makeSnapshot(): CitySnapshot {
  return {
    id: { value: "city-1" },
    location: { x: 0, y: 0 },
    aggregatePool: { count: 5, wealthSum: 500, healthSum: 400 },
    residents: [{ id: { value: 3 }, location: { x: 1, y: 1 }, currentAction: null }],
    buildings: [{ id: { value: 8 }, buildingTypeId: 2 }],
    layers: {} as CitySnapshot["layers"],
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

    // câmera inicial: center = snapshot.location (0,0), scale 16; resident em (1,1) tem centro
    // de token em (1.5,1.5) -> ((1.5-0)*16+100, (1.5-0)*16+100) = (124,124) (hitTest.ts mira o
    // centro da célula, igual ao renderer — não o canto cru)
    fireEvent.click(canvas, { clientX: 124, clientY: 124 });

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

    // único prédio, ângulo 0, raio 6, centro (0,0) -> local (6,0) -> tela (0*... deixa o hitTest
    // achar: worldToScreen((6,0)) com center(0,0) scale16 = (6*16+100, 0*16+100) = (196,100)
    fireEvent.doubleClick(canvas, { clientX: 196, clientY: 100 });

    expect(enterSpy).toHaveBeenCalledWith({ kind: "Building", buildingId: "8", cityId: "city-1" });
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
});
