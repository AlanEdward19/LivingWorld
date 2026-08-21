import { Profiler } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, fireEvent } from "@testing-library/react";
import { MapView } from "../src/components/MapView";
import { SimulationStore } from "../src/state/simulationStore";
import { ViewStore } from "../src/state/viewStore";
import { SelectionStore } from "../src/state/selectionStore";
import { MockPortalSource } from "../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../src/types";
import type { SnapshotSource, TickStreamSource } from "../src/data/sources";
import type { SpaceId } from "../src/map-engine/types";

const VIEWPORT = { width: 200, height: 200 };
const CITY_A: SpaceId = { kind: "City", cityId: "city-a" };
const WORLD: SpaceId = { kind: "World" };
const CELLS = { width: 100, height: 100, colorAt: () => undefined };

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

function cityASnapshotSource(): SnapshotSource {
  return {
    load: async () => ({
      scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
      activeLayers: [],
      payload: { residents: [{ id: { value: 1 }, location: { x: 50, y: 50 }, currentAction: null }] },
    }),
  };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

async function buildStores() {
  const simulationStore = new SimulationStore(cityASnapshotSource(), neverStreamingTickSource());
  const viewStore = new ViewStore(new MockPortalSource([]));
  viewStore.recordCamera(CITY_A, { center: { x: 50, y: 50 }, scale: 10 }); // câmera determinística
  const selectionStore = new SelectionStore();
  await simulationStore.observeSpace(CITY_A);
  return { simulationStore, viewStore, selectionStore };
}

describe("MapView", () => {
  beforeEach(() => {
    HTMLCanvasElement.prototype.getContext = () => null;
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("wheel zooms without issuing any HTTP request", async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal("fetch", fetchSpy);
    const { simulationStore, viewStore, selectionStore } = await buildStores();

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.wheel(canvas, { deltaY: -100, clientX: 100, clientY: 100 });

    const after = viewStore.cameraFor(CITY_A, { center: { x: 0, y: 0 }, scale: 1 });
    expect(after.scale).toBeGreaterThan(10);
    expect(fetchSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("dragging on empty space pans the camera without issuing any HTTP request", async () => {
    const fetchSpy = vi.fn();
    vi.stubGlobal("fetch", fetchSpy);
    const { simulationStore, viewStore, selectionStore } = await buildStores();

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.mouseDown(canvas, { clientX: 10, clientY: 10 });
    fireEvent.mouseMove(canvas, { clientX: 40, clientY: 10 });
    fireEvent.mouseUp(canvas, { clientX: 40, clientY: 10 });

    const after = viewStore.cameraFor(CITY_A, { center: { x: 0, y: 0 }, scale: 1 });
    expect(after.center.x).not.toBe(50);
    expect(fetchSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("a single click on an entity selects it without navigating", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    const enterSpy = vi.spyOn(viewStore, "enter");

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        resolveNavigationTarget={() => WORLD}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 100, clientY: 100 }); // entidade projeta no centro (câmera em 50,50)

    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: CITY_A });
    expect(enterSpy).not.toHaveBeenCalled();
  });

  // Feedback do usuário (2026-08-21, 2ª rodada): clicar num NPC dentro de uma cidade quase nunca
  // "pegava" — o raio de acerto usava uma folga fixa (1.3x) que não cobria o quanto o pawn
  // realmente cresce em espaço de cidade (`npcVisualScale`: 1.65x). Clique a 9px do centro (fora
  // do raio antigo de 7.8px, dentro do raio correto de 9.9px) prova que o raio de acerto agora
  // usa o multiplicador exato do espaço observado, não uma aproximação.
  it("hits an NPC near the edge of its city-scaled token, not just dead-center", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 109, clientY: 100 }); // 9px do centro (100,100)

    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: CITY_A });
  });

  // Feedback do usuário (2026-08-21, 3ª rodada, verificado ao vivo no browser): mesmo com o raio
  // de acerto igualando o raio de DESENHO, clicar no NPC ainda falhava — o pawn desenha um
  // retângulo alto (topo a 1.25r acima do centro), não um círculo de raio r. Entidade em (50,50)
  // ancora em tela (105,105) (posição + 0.5 de meia-célula, câmera centrada em 50,50 escala 10).
  // Clique 12px acima disso (fora do raio antigo de 9.9px, dentro da cobertura correta de
  // ~15.85px) simula clicar na cabeça/torso visível do personagem, não só no pé.
  it("hits an NPC when clicking its visible head/torso, above the tile-center anchor", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 105, clientY: 93 }); // 12px acima do ponto de ancoragem (105,105)

    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: CITY_A });
  });

  it("a double click on a navigable entity calls ViewStore.enter with the resolved target", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    const enterSpy = vi.spyOn(viewStore, "enter");
    const resolveNavigationTarget = vi.fn(() => WORLD);

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        resolveNavigationTarget={resolveNavigationTarget}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.doubleClick(canvas, { clientX: 100, clientY: 100 });

    expect(resolveNavigationTarget).toHaveBeenCalledWith({ kind: "npc", id: "1", space: CITY_A });
    expect(enterSpy).toHaveBeenCalledWith(WORLD);
  });

  it("clicking empty space (no entity under the cursor) selects nothing", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 5, clientY: 5 }); // longe da entidade em (100,100)

    expect(selectionStore.current()).toBeNull();
  });

  // Feedback do usuário (2026-08-07): clicar em espaço vazio precisa DESSELECIONAR — antes
  // disto o único jeito de fechar o inspector era o botão X (que ficava embaixo da hud-bar).
  it("clicking empty space clears an existing selection", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    selectionStore.select({ kind: "npc", id: "1", space: CITY_A });

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.click(canvas, { clientX: 5, clientY: 5 }); // longe da entidade em (100,100)

    expect(selectionStore.current()).toBeNull();
  });

  it("Esc clears the current selection", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    selectionStore.select({ kind: "npc", id: "1", space: CITY_A });

    render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    fireEvent.keyDown(document, { key: "Escape" });

    expect(selectionStore.current()).toBeNull();
  });

  it("renders no DOM node per entity — the canvas is the only child", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();

    const { container } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(container.children).toHaveLength(1);
    expect(container.firstElementChild?.tagName).toBe("CANVAS");
  });

  it("does not re-render when the SimulationStore notifies a tick", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    const onRender = vi.fn();

    render(
      <Profiler id="mapview-test" onRender={onRender}>
        <MapView
          space={CITY_A}
          viewport={VIEWPORT}
          cells={CELLS}
          layers={[]}
          lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
          simulationStore={simulationStore}
          viewStore={viewStore}
          selectionStore={selectionStore}
        />
      </Profiler>,
    );

    expect(onRender).toHaveBeenCalledTimes(1); // só o commit do mount

    simulationStore.applyDelta({ tick: 1, moved: [{ npcId: 1, location: { x: 51, y: 50 } }], removed: [] });
    simulationStore.applyDelta({ tick: 2, moved: [{ npcId: 1, location: { x: 52, y: 50 } }], removed: [] });

    expect(onRender).toHaveBeenCalledTimes(1); // nenhum commit novo por causa dos deltas
  });

  it("tracks the followed entity's authoritative position every frame", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    viewStore.startFollow({ kind: "npc", id: "1", space: CITY_A });

    render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    // move o NPC seguido para bem longe do centro de câmera semeado em buildStores (50,50)
    simulationStore.applyDelta({ tick: 1, moved: [{ npcId: 1, location: { x: 900, y: 900 } }], removed: [] });

    await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));

    const camera = viewStore.cameraFor(CITY_A, { center: { x: 0, y: 0 }, scale: 1 });
    expect(camera.center).toEqual({ x: 900, y: 900 });
  });

  it("dragging cancels an active follow instead of fighting the pan", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    viewStore.startFollow({ kind: "npc", id: "1", space: CITY_A });

    const { getByTestId } = render(
      <MapView
        space={CITY_A}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.mouseDown(canvas, { clientX: 10, clientY: 10 });
    fireEvent.mouseMove(canvas, { clientX: 40, clientY: 10 });

    expect(viewStore.followedEntity()).toBeNull();
  });

  it("paints every crossed cell while the pointer is dragged instead of panning", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    const onPaintDrag = vi.fn((_cell: { x: number; y: number }) => true);
    const { getByTestId } = render(
      <MapView space={CITY_A} viewport={VIEWPORT} cells={CELLS} layers={[]} lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore} viewStore={viewStore} selectionStore={selectionStore} onPaintDrag={onPaintDrag} />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.mouseDown(canvas, { clientX: 100, clientY: 100 });
    fireEvent.mouseMove(canvas, { clientX: 120, clientY: 100 });
    fireEvent.mouseUp(canvas);

    expect(onPaintDrag.mock.calls.map(([cell]) => cell)).toEqual([
      { x: 50, y: 50 }, { x: 51, y: 50 }, { x: 52, y: 50 },
    ]);
    expect(viewStore.cameraFor(CITY_A, { center: { x: 0, y: 0 }, scale: 1 }).center).toEqual({ x: 50, y: 50 });
  });

  it("drags an existing entity to a new world cell when an entity mover is supplied", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    const onEntityMove = vi.fn((_ref: unknown, _cell: { x: number; y: number }) => true);
    const { getByTestId } = render(
      <MapView space={CITY_A} viewport={VIEWPORT} cells={CELLS} layers={[]} lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore} viewStore={viewStore} selectionStore={selectionStore} onEntityMove={onEntityMove} />,
    );
    const canvas = getByTestId("map-view-canvas") as HTMLCanvasElement;
    stubRect(canvas);

    fireEvent.mouseDown(canvas, { clientX: 105, clientY: 105 });
    fireEvent.mouseMove(canvas, { clientX: 125, clientY: 105 });
    fireEvent.mouseUp(canvas);

    expect(onEntityMove).toHaveBeenLastCalledWith(
      { kind: "npc", id: "1", space: CITY_A },
      { x: 52, y: 50 },
    );
    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: CITY_A });
  });
});
