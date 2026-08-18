import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { App } from "../src/App";
import { SimulationStore } from "../src/state/simulationStore";
import { ViewStore } from "../src/state/viewStore";
import { SelectionStore } from "../src/state/selectionStore";
import { MockPortalSource } from "../src/data/mock/MockPortalSource";
import { MockClock } from "../src/data/mock/MockClock";
import { MockTimeControlSource } from "../src/data/mock/MockTimeControlSource";
import { VisualScopeKind, ViewerMode } from "../src/types";
import type { GlobalSnapshot, CitySnapshot } from "../src/types";
import type { SnapshotSource, TickStreamSource } from "../src/data/sources";
import type { SpaceId } from "../src/map-engine/types";

function worldEnvelope() {
  return {
    scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey: "world", sequence: 0 },
    activeLayers: [],
    payload: {
      width: 10,
      height: 10,
      cities: [
        {
          id: { value: "city-1" },
          location: { x: 3, y: 4 },
          population: 10,
          bounds: { x: 3, y: 4, width: 2, height: 2 },
          boundsAreDerived: true,
        },
      ],
      externalNpcs: [],
      activeEvents: [],
      layers: {} as GlobalSnapshot["layers"],
    },
  };
}

function cityEnvelope() {
  return {
    scope: { kind: VisualScopeKind.City, refId: "city-1", scopeKey: "city:city-1" },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey: "city:city-1", sequence: 0 },
    activeLayers: [],
    payload: {
      id: { value: "city-1" },
      name: "Cidade Um",
      location: { x: 0, y: 0 },
      aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
      residents: [],
      pendingResidentIds: [],
      buildings: [],
      layers: {} as CitySnapshot["layers"],
      bounds: { x: -1, y: -1, width: 2, height: 2 },
      boundsAreDerived: true,
    },
  };
}

function multiScopeSnapshotSource(): SnapshotSource {
  return {
    load: async (space: SpaceId) => (space.kind === "World" ? worldEnvelope() : cityEnvelope()),
  };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
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

function buildStores() {
  const simulationStore = new SimulationStore(multiScopeSnapshotSource(), neverStreamingTickSource());
  const viewStore = new ViewStore(new MockPortalSource([]));
  const selectionStore = new SelectionStore();
  const timeControlSource = new MockTimeControlSource(new MockClock());
  return { simulationStore, viewStore, selectionStore, timeControlSource };
}

describe("App", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  it("renders the world map after the mock snapshot resolves, then drills into a city on double click", async () => {
    const { simulationStore, viewStore, selectionStore, timeControlSource } = buildStores();
    render(<App simulationStore={simulationStore} viewStore={viewStore} selectionStore={selectionStore} timeControlSource={timeControlSource} />);

    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    await screen.findByTestId("world-map-view");
    const canvas = screen.getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    // fit-to-screen: mundo 10x10, viewport = innerWidth x (innerHeight-40) do jsdom (1024x728),
    // scale = min(1024/10,728/10) piso = 72; centro do grid (5,5); cidade em (3,4) ->
    // ((3-5)*72 + 1024/2, (4-5)*72 + 728/2)
    const scale = Math.floor(Math.min(1024 / 10, 728 / 10));
    const x = (3 - 5) * scale + 1024 / 2;
    const y = (4 - 5) * scale + 728 / 2;
    fireEvent.doubleClick(canvas, { clientX: x, clientY: y });

    await screen.findByTestId("city-view");
    await waitFor(() => expect(viewStore.currentSpace()).toEqual({ kind: "City", cityId: "city-1" }));
  });

  it("shows a breadcrumb that navigates back to World from a City", async () => {
    const { simulationStore, viewStore, selectionStore, timeControlSource } = buildStores();
    viewStore.enter({ kind: "City", cityId: "city-1" });
    render(<App simulationStore={simulationStore} viewStore={viewStore} selectionStore={selectionStore} timeControlSource={timeControlSource} />);
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    await screen.findByTestId("city-view");
    expect(screen.getByRole("navigation", { name: "breadcrumb" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Mundo" }));

    await screen.findByTestId("world-map-view");
  });

  it("starts on the start menu and navigates to settings and back", () => {
    const { simulationStore, viewStore, selectionStore, timeControlSource } = buildStores();
    render(<App simulationStore={simulationStore} viewStore={viewStore} selectionStore={selectionStore} timeControlSource={timeControlSource} />);

    expect(screen.getByTestId("start-menu")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Configurações" }));
    expect(screen.getByTestId("settings-view")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "← menu" }));
    expect(screen.getByTestId("start-menu")).toBeInTheDocument();
  });

  it("opens the visual WorldEditor after choosing a creation preset", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("[]", { status: 200 })));
    const { simulationStore, viewStore, selectionStore, timeControlSource } = buildStores();
    render(
      <App
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        timeControlSource={timeControlSource}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    expect(screen.getByTestId("preset-start")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("preset-name"), { target: { value: "Aldeia" } });
    fireEvent.click(screen.getByRole("button", { name: "Começar" }));

    expect(await screen.findByTestId("world-editor")).toBeInTheDocument();
    expect(screen.queryByTestId("create-world-form")).not.toBeInTheDocument();
  });

  it("cancelling world creation from the start menu returns to the start menu, not the map", () => {
    const { simulationStore, viewStore, selectionStore, timeControlSource } = buildStores();
    render(
      <App
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        timeControlSource={timeControlSource}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    expect(screen.getByTestId("preset-start")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.getByTestId("start-menu")).toBeInTheDocument();
  });

  it("cancelling a new creator after visiting an existing world still returns to the start menu", async () => {
    const { simulationStore, viewStore, selectionStore, timeControlSource } = buildStores();
    render(
      <App
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        timeControlSource={timeControlSource}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
    await screen.findByTestId("world-map-view");
    fireEvent.click(screen.getByRole("button", { name: "☰ menu" }));
    fireEvent.click(screen.getByRole("button", { name: "Criar mundo" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(screen.getByTestId("start-menu")).toBeInTheDocument();
    expect(screen.queryByTestId("world-map-view")).not.toBeInTheDocument();
  });

  it("does not offer a 'Criar mundo' button while already playing a world — only the menu button", async () => {
    const { simulationStore, viewStore, selectionStore, timeControlSource } = buildStores();
    render(
      <App
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        timeControlSource={timeControlSource}
      />,
    );

    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
    await screen.findByTestId("world-map-view");

    expect(screen.queryByRole("button", { name: "Criar mundo" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "☰ menu" })).toBeInTheDocument();
  });
});
