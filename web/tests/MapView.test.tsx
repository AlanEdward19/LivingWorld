import { Profiler } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, fireEvent, waitFor } from "@testing-library/react";
import { MapView } from "../src/components/MapView";
import { SimulationStore } from "../src/state/simulationStore";
import { ViewStore } from "../src/state/viewStore";
import { SelectionStore } from "../src/state/selectionStore";
import { MockPortalSource } from "../src/data/mock/MockPortalSource";
import { MockNpcInspectionSource } from "../src/data/mock/MockNpcInspectionSource";
import { VisualScopeKind, ViewerMode } from "../src/types";
import type { SnapshotSource, TickStreamSource } from "../src/data/sources";
import type { SpaceId } from "../src/map-engine/types";
import { POOLED_LOD, type NpcInspection } from "../src/data/contracts";

const VIEWPORT = { width: 200, height: 200 };
const CITY_A: SpaceId = { kind: "City", cityId: "city-a" };
const CITY_B: SpaceId = { kind: "City", cityId: "city-b" };
const WORLD: SpaceId = { kind: "World" };
const CELLS = { width: 100, height: 100, colorAt: () => undefined };

// Mesma forma mínima usada pelos testes do NpcInspector (tests/inspector/NpcInspector.test.tsx)
// — só os campos que o próprio tipo exige, sem nenhum dado que este teste não vá checar.
const POOLED_INSPECTION: NpcInspection = {
  id: { value: 1 }, name: "Lina", sex: 1, ageYears: 27,
  culture: { id: 2 }, city: { value: "city-b" }, household: null,
  motherId: null, fatherId: null, spouse: null,
  profession: { id: 0 }, employer: null, health: 0,
  hunger: 0, thirst: 0, sleep: 0, social: 0, personality: {},
  skills: { values: {} }, currentLocation: { x: 0, y: 0 },
  currentAction: null, actionStartedAtTick: 0,
  actionTarget: null, lod: POOLED_LOD, memories: [], beliefs: [], powerIds: [],
  currentScope: { kind: 1, cityId: { value: "city-b" } },
};

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

  // Feedback do usuário (2026-08-21, 4ª rodada, verificado ao vivo no browser): o bug de clique
  // voltava assim que o tempo andava (acelerar tick) e desaparecia parado em 1x sem tocar em
  // nada. Causa real: o loop de desenho usa a posição INTERPOLADA (visual) do NPC, mas o clique
  // comparava contra `entitiesRef.current` — a posição AUTORITATIVA crua. Em trânsito (qualquer
  // tick recente), o pawn desenhado e o ponto do hit-test divergiam. Aqui o NPC salta de (50,50)
  // pra (60,50) e o clique chega no meio da transição (t=0.1 de 1000ms) — tem que acertar onde
  // ele está DESENHADO (perto de 50,50 ainda), não onde a posição autoritativa mais recente diz.
  it("hits the NPC at its visually-interpolated position mid-transition, not its raw authoritative position", async () => {
    // performance.now() também é usado pelo scheduler interno do React (commits/effects) —
    // não dá pra fixar valores por ÍNDICE de chamada (varia entre versões/ambientes). Em vez
    // disso, cada chamada avança um pouco (jitter irrelevante frente às janelas de 1000ms/100ms
    // abaixo) e um "salto" explícito (`advanceBy`) entre as fases simula o tempo real decorrido.
    const nowSpy = vi.spyOn(performance, "now");
    let base = 0;
    let offset = 0;
    nowSpy.mockImplementation(() => { base += 1; return base + offset; });
    function advanceBy(ms: number) { offset += ms; }

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

    advanceBy(1000); // 1s até o próximo tick autoritativo -> duração da transição (50,50)->(60,50)
    simulationStore.applyDelta({ tick: 1, moved: [{ npcId: 1, location: { x: 60, y: 50 } }], removed: [] });

    advanceBy(100); // clique 100ms depois -> t ~= 0.1 de 1000ms, visual ainda perto de (50,50)
    // ancora visual esperada: from(50.5,50.5) + 0.1*(to(60.5,50.5)-from) = (51.5,50.5) -> tela (115,105)
    fireEvent.click(canvas, { clientX: 115, clientY: 105 });

    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: CITY_A });
    nowSpy.mockRestore();
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

  // T50 bug report (usuário, ao vivo): seguir um NPC que cruza de escopo (cidade -> mundo)
  // fazia a câmera continuar seguindo, mas o anel de seleção e o inspector desapareciam. Causa
  // real: `syncWithSpace` via `refreshEntities` disparava com a lista do NOVO espaço ainda
  // vazia (snapshot em voo), via de regra chamado no MESMO commit em que `space` muda —
  // limpando a seleção antes do snapshot de verdade chegar.
  it("keeps a followed selection alive through a scope transition until the new space's snapshot loads", async () => {
    let resolveWorldLoad!: (envelope: Awaited<ReturnType<SnapshotSource["load"]>>) => void;
    const source: SnapshotSource = {
      load: (space) => {
        if (space.kind === "City") {
          return Promise.resolve({
            scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
            mode: ViewerMode.Spectator,
            cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
            activeLayers: [],
            payload: { residents: [{ id: { value: 1 }, location: { x: 50, y: 50 }, currentAction: null }] },
          });
        }
        return new Promise((resolve) => { resolveWorldLoad = resolve; });
      },
    };
    const simulationStore = new SimulationStore(source, neverStreamingTickSource());
    const viewStore = new ViewStore(new MockPortalSource([]));
    viewStore.recordCamera(CITY_A, { center: { x: 50, y: 50 }, scale: 10 });
    const selectionStore = new SelectionStore();
    await simulationStore.observeSpace(CITY_A);
    selectionStore.select({ kind: "npc", id: "1", space: CITY_A });

    const { rerender } = render(
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

    // Cruza de escopo: WORLD ainda não carregou (load fica pendurado em `resolveWorldLoad`).
    const worldObserved = simulationStore.observeSpace(WORLD);
    rerender(
      <MapView
        space={WORLD}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    // Snapshot do WORLD ainda em voo — a seleção não pode ter sido apagada.
    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: CITY_A });

    // Snapshot real chega, com o NPC presente no WORLD também.
    resolveWorldLoad({
      scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "world", sequence: 0 },
      activeLayers: [],
      payload: { externalNpcs: [{ id: { value: 1 }, location: { x: 10, y: 10 }, currentAction: null }] },
    });
    await worldObserved;

    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: WORLD });
  });

  // Mesma transição de escopo, mas o NPC de fato não existe no novo espaço (morreu, ou nunca
  // esteve lá) — depois que o snapshot real do novo espaço chega, a seleção deve continuar
  // sendo limpa normalmente. O fix acima só atrasa o `syncWithSpace`, nunca o pula de vez.
  it("still clears the selection once the new space's snapshot confirms the entity is really gone", async () => {
    let resolveWorldLoad!: (envelope: Awaited<ReturnType<SnapshotSource["load"]>>) => void;
    const source: SnapshotSource = {
      load: (space) => {
        if (space.kind === "City") {
          return Promise.resolve({
            scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
            mode: ViewerMode.Spectator,
            cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
            activeLayers: [],
            payload: { residents: [{ id: { value: 1 }, location: { x: 50, y: 50 }, currentAction: null }] },
          });
        }
        return new Promise((resolve) => { resolveWorldLoad = resolve; });
      },
    };
    const simulationStore = new SimulationStore(source, neverStreamingTickSource());
    const viewStore = new ViewStore(new MockPortalSource([]));
    viewStore.recordCamera(CITY_A, { center: { x: 50, y: 50 }, scale: 10 });
    const selectionStore = new SelectionStore();
    await simulationStore.observeSpace(CITY_A);
    selectionStore.select({ kind: "npc", id: "1", space: CITY_A });

    const { rerender } = render(
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

    const worldObserved = simulationStore.observeSpace(WORLD);
    rerender(
      <MapView
        space={WORLD}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    resolveWorldLoad({
      scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "world", sequence: 0 },
      activeLayers: [],
      payload: { externalNpcs: [] }, // o NPC 1 não existe no WORLD
    });
    await worldObserved;

    expect(selectionStore.current()).toBeNull();
  });

  // T50 round 3 (bug ao vivo): seguir um NPC pra uma cidade onde ele é um id reservado do pool
  // agregado (City.PoolNpcIds, ainda não materializado) limpava a seleção/follow assim que o
  // snapshot da cidade nova chegava -- `entitiesOf`/`staticEntities` nunca desenham um marcador
  // pra um pooled (não existe até materializar), então `syncWithSpace` via `refreshEntities`
  // via de regra tratava "sem marcador" como "não existe mais". Causa real: pooled não é
  // "gone" -- é o mesmo estado que o NpcInspector já trata (Lod.Pooled, com botão de
  // materializar). O fix consulta a mesma inspeção antes de decidir limpar.
  it("keeps a followed selection alive when the new scope's snapshot shows the NPC as pooled instead of materialized", async () => {
    const source: SnapshotSource = {
      load: async (space) => {
        if (space.kind === "City" && space.cityId === "city-a") {
          return {
            scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
            mode: ViewerMode.Spectator,
            cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
            activeLayers: [],
            payload: { residents: [{ id: { value: 1 }, location: { x: 50, y: 50 }, currentAction: null }] },
          };
        }
        // city-b: NPC 1 não tem marcador nenhum aqui -- é um id reservado no pool, não um resident.
        return {
          scope: { kind: VisualScopeKind.City, refId: "city-b", scopeKey: "city:city-b" },
          mode: ViewerMode.Spectator,
          cursor: { tick: 0, scopeKey: "city:city-b", sequence: 0 },
          activeLayers: [],
          payload: { residents: [] },
        };
      },
    };
    const npcInspectionSource = new MockNpcInspectionSource(new Map([[1, POOLED_INSPECTION]]));
    const simulationStore = new SimulationStore(source, neverStreamingTickSource(), npcInspectionSource);
    const viewStore = new ViewStore(new MockPortalSource([]));
    viewStore.recordCamera(CITY_A, { center: { x: 50, y: 50 }, scale: 10 });
    const selectionStore = new SelectionStore();
    await simulationStore.observeSpace(CITY_A);
    selectionStore.select({ kind: "npc", id: "1", space: CITY_A });

    const { rerender } = render(
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

    await simulationStore.observeSpace(CITY_B);
    rerender(
      <MapView
        space={CITY_B}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    // A inspeção que confirma "pooled" é assíncrona -- espera ela resolver e o efeito reagir.
    await waitFor(() => {
      expect(selectionStore.current()).not.toBeNull();
    });

    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: CITY_B });
  });

  // Mesma transição pra uma cidade onde o NPC não é materializado NEM pooled (morreu, ou nunca
  // esteve lá) -- a consulta de inspeção que evita a limpeza precoce no teste acima não pode virar
  // um jeito de nunca mais limpar a seleção quando a entidade de fato se foi.
  it("still clears the selection when the new scope's inspection confirms the NPC is neither materialized nor pooled", async () => {
    const source: SnapshotSource = {
      load: async (space) => {
        if (space.kind === "City" && space.cityId === "city-a") {
          return {
            scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
            mode: ViewerMode.Spectator,
            cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
            activeLayers: [],
            payload: { residents: [{ id: { value: 1 }, location: { x: 50, y: 50 }, currentAction: null }] },
          };
        }
        return {
          scope: { kind: VisualScopeKind.City, refId: "city-b", scopeKey: "city:city-b" },
          mode: ViewerMode.Spectator,
          cursor: { tick: 0, scopeKey: "city:city-b", sequence: 0 },
          activeLayers: [],
          payload: { residents: [] },
        };
      },
    };
    // Mapa vazio: `MockNpcInspectionSource.load` devolve `null` pro NPC 1 -- nem materializado
    // nem pooled, o mesmo "genuinamente sumiu" que o NpcInspector já mostra como tal.
    const npcInspectionSource = new MockNpcInspectionSource(new Map());
    const simulationStore = new SimulationStore(source, neverStreamingTickSource(), npcInspectionSource);
    const viewStore = new ViewStore(new MockPortalSource([]));
    viewStore.recordCamera(CITY_A, { center: { x: 50, y: 50 }, scale: 10 });
    const selectionStore = new SelectionStore();
    await simulationStore.observeSpace(CITY_A);
    selectionStore.select({ kind: "npc", id: "1", space: CITY_A });

    const { rerender } = render(
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

    await simulationStore.observeSpace(CITY_B);
    rerender(
      <MapView
        space={CITY_B}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    await waitFor(() => {
      expect(selectionStore.current()).toBeNull();
    });
  });

  // T50 round 4: cruzar de uma cidade pro World não é "sumiu" -- um residente comum (não
  // viajante) nunca é desenhado em World por design (`entitiesOf` ali só lista viajantes/pontos
  // de migração), então a inspeção confirma que o NPC existe (materializado, lod != POOLED_LOD)
  // mesmo sem marcador algum neste escopo -- a mesma regra "só limpa se a inspeção resolver
  // null" do teste acima cobre este caso, não só o pooled.
  it("keeps a followed selection alive when it transitions into World scope, where ordinary residents are never drawn", async () => {
    const source: SnapshotSource = {
      load: async (space) => {
        if (space.kind === "City") {
          return {
            scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
            mode: ViewerMode.Spectator,
            cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
            activeLayers: [],
            payload: { residents: [{ id: { value: 1 }, location: { x: 50, y: 50 }, currentAction: null }] },
          };
        }
        return {
          scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
          mode: ViewerMode.Spectator,
          cursor: { tick: 0, scopeKey: "world", sequence: 0 },
          activeLayers: [],
          payload: { externalNpcs: [] }, // NPC 1 é residente comum -- World nunca desenha ele
        };
      },
    };
    const materializedInspection: NpcInspection = {
      ...POOLED_INSPECTION,
      lod: 0,
      currentScope: { kind: 0, cityId: null },
    };
    const npcInspectionSource = new MockNpcInspectionSource(new Map([[1, materializedInspection]]));
    const simulationStore = new SimulationStore(source, neverStreamingTickSource(), npcInspectionSource);
    const viewStore = new ViewStore(new MockPortalSource([]));
    viewStore.recordCamera(CITY_A, { center: { x: 50, y: 50 }, scale: 10 });
    const selectionStore = new SelectionStore();
    await simulationStore.observeSpace(CITY_A);
    selectionStore.select({ kind: "npc", id: "1", space: CITY_A });

    const { rerender } = render(
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

    await simulationStore.observeSpace(WORLD);
    rerender(
      <MapView
        space={WORLD}
        viewport={VIEWPORT}
        cells={CELLS}
        layers={[]}
        lodThresholds={{ aggregate: 4, token: 10, detail: 18 }}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    await waitFor(() => {
      expect(selectionStore.current()).not.toBeNull();
    });

    expect(selectionStore.current()).toEqual({ kind: "npc", id: "1", space: WORLD });
  });
});
