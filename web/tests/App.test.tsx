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
import { MockNpcInspectionSource } from "../src/data/mock/MockNpcInspectionSource";
import type { NpcInspection } from "../src/data/contracts";

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

function cityEnvelopeFor(
  cityId: string,
  name: string,
  residents: Array<{ id: { value: number }; location: { x: number; y: number }; currentAction: number | null }>,
) {
  return {
    scope: { kind: VisualScopeKind.City, refId: cityId, scopeKey: `city:${cityId}` },
    mode: ViewerMode.Spectator,
    cursor: { tick: 0, scopeKey: `city:${cityId}`, sequence: 0 },
    activeLayers: [],
    payload: {
      id: { value: cityId },
      name,
      location: { x: 0, y: 0 },
      aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
      residents,
      pendingResidentIds: [],
      buildings: [],
      layers: {} as CitySnapshot["layers"],
      bounds: { x: -1, y: -1, width: 2, height: 2 },
      boundsAreDerived: true,
    },
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

  // Bug report (usuário, ao vivo, pós-497a09d): seguir um NPC que cruza de escopo (aqui, de
  // uma cidade pra outra) deixa a navegação manual pelo breadcrumb morta depois — clicar em
  // "Mundo" não faz nada, exige reload. O NPC seguido cruza escopo sozinho (via
  // `resolveFollowedSpaceIfLost`/`viewStore.enter`, disparado de dentro do loop de
  // `requestAnimationFrame` do MapView, não de um clique) ANTES do usuário tentar navegar.
  it("still lets a breadcrumb click navigate after a followed NPC crosses into another city on its own", async () => {
    const inspection: NpcInspection = {
      id: { value: 1 }, name: "Lina", sex: 1, ageYears: 27,
      culture: { id: 2 }, city: { value: "city-b" }, household: null,
      motherId: null, fatherId: null, spouse: null,
      profession: { id: 6 }, employer: null, health: 91,
      hunger: 63, thirst: 72, sleep: 81, social: 54, personality: {},
      skills: { values: {} }, currentLocation: { x: 1, y: 1 },
      currentAction: null, actionStartedAtTick: 0,
      actionTarget: null, lod: 0, memories: [], beliefs: [],
      currentScope: { kind: 1, cityId: { value: "city-b" } },
    };
    // city-b's own load is held open on purpose (`resolveCityB`) so the breadcrumb click below
    // lands WHILE the follow-triggered transition into city-b is still in flight -- the exact
    // overlap the live bug report needs (a manual `goToAncestor` racing an in-flight
    // follow-triggered `observeSpace`), not just two transitions completing one after another.
    let resolveCityB!: (envelope: Awaited<ReturnType<SnapshotSource["load"]>>) => void;
    const snapshotSource: SnapshotSource = {
      load: (space: SpaceId) => {
        if (space.kind === "World") return Promise.resolve(worldEnvelope());
        if (space.cityId === "city-a") return Promise.resolve(cityEnvelopeFor("city-a", "Cidade A", [])); // NPC já saiu
        return new Promise((resolve) => { resolveCityB = resolve; });
      },
    };
    const simulationStore = new SimulationStore(
      snapshotSource,
      neverStreamingTickSource(),
      new MockNpcInspectionSource(new Map([[1, inspection]])),
    );
    const viewStore = new ViewStore(new MockPortalSource([]));
    const selectionStore = new SelectionStore();
    const timeControlSource = new MockTimeControlSource(new MockClock());

    viewStore.enter({ kind: "City", cityId: "city-a" });
    viewStore.startFollow({ kind: "npc", id: "1", space: { kind: "City", cityId: "city-a" } });

    render(
      <App
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        timeControlSource={timeControlSource}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));

    await screen.findByTestId("city-view");

    // O loop de follow nota que o NPC 1 não está mais em city-a e resolve o escopo real
    // (city-b) -- o `viewStore.enter(cityB)` já aconteceu, mas o snapshot de city-b ainda
    // está em voo (segurado por `resolveCityB`), então a tela mostra "Carregando…".
    await waitFor(() => expect(viewStore.currentSpace()).toEqual({ kind: "City", cityId: "city-b" }));

    // O usuário, vendo o breadcrumb (que não depende do snapshot ter chegado), clica "Mundo"
    // ENQUANTO o observeSpace(city-b) do follow ainda não resolveu.
    fireEvent.click(screen.getByRole("button", { name: "Mundo" }));

    // Só agora o snapshot de city-b (stale) chega.
    resolveCityB(cityEnvelopeFor("city-b", "Cidade B", [{ id: { value: 1 }, location: { x: 1, y: 1 }, currentAction: null }]));

    await screen.findByTestId("world-map-view");
    expect(viewStore.currentSpace()).toEqual({ kind: "World" });
  });

  // Bug report (usuário, ao vivo, pós-497a09d): depois de seguir um NPC que cruza de escopo,
  // clicar no breadcrumb "Mundo" não faz NADA -- precisa recarregar a página. Repro: uma
  // resolução de `resolveFollowedSpaceIfLost` iniciada ANTES do clique (o NPC seguido já não
  // está na cidade atual, então o loop de follow já está checando o escopo real dele) só
  // termina DEPOIS do clique -- e ela só olha se o NPC seguido continua o mesmo (nunca se o
  // usuário navegou manualmente nesse meio tempo), então ela força a navegação de volta pro
  // escopo antigo assim que resolve, desfazendo o clique.
  it("does not let a stale in-flight follow resolution undo a breadcrumb click that happened while it was pending", async () => {
    let resolveInspection!: (inspection: NpcInspection) => void;
    const npcInspectionSource = {
      load: () => new Promise<NpcInspection | null>((resolve) => { resolveInspection = resolve; }),
    };
    const snapshotSource: SnapshotSource = {
      load: async (space: SpaceId) => {
        if (space.kind === "World") return worldEnvelope();
        if (space.cityId === "city-a") return cityEnvelopeFor("city-a", "Cidade A", []); // NPC já saiu
        return cityEnvelopeFor("city-b", "Cidade B", [{ id: { value: 1 }, location: { x: 1, y: 1 }, currentAction: null }]);
      },
    };
    const simulationStore = new SimulationStore(snapshotSource, neverStreamingTickSource(), npcInspectionSource);
    const viewStore = new ViewStore(new MockPortalSource([]));
    const selectionStore = new SelectionStore();
    const timeControlSource = new MockTimeControlSource(new MockClock());

    viewStore.enter({ kind: "City", cityId: "city-a" });
    viewStore.startFollow({ kind: "npc", id: "1", space: { kind: "City", cityId: "city-a" } });

    render(
      <App
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
        timeControlSource={timeControlSource}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
    await screen.findByTestId("city-view");

    // Dá tempo pro loop de `requestAnimationFrame` do MapView notar que o NPC 1 não está em
    // city-a e disparar `resolveFollowedSpaceIfLost` -- que fica pendurado em `resolveInspection`.
    await waitFor(() => expect(resolveInspection).toBeDefined());

    // O usuário navega manualmente pro Mundo ENQUANTO essa resolução ainda está em voo.
    fireEvent.click(screen.getByRole("button", { name: "Mundo" }));
    await screen.findByTestId("world-map-view");
    expect(viewStore.currentSpace()).toEqual({ kind: "World" });

    // Só agora a resolução (antiga, de antes do clique) chega, dizendo que o NPC está em city-b.
    resolveInspection({
      id: { value: 1 }, name: "Lina", sex: 1, ageYears: 27,
      culture: { id: 2 }, city: { value: "city-b" }, household: null,
      motherId: null, fatherId: null, spouse: null,
      profession: { id: 6 }, employer: null, health: 91,
      hunger: 63, thirst: 72, sleep: 81, social: 54, personality: {},
      skills: { values: {} }, currentLocation: { x: 1, y: 1 },
      currentAction: null, actionStartedAtTick: 0,
      actionTarget: null, lod: 0, memories: [], beliefs: [],
      currentScope: { kind: 1, cityId: { value: "city-b" } },
    });

    // A navegação manual do usuário não pode ter sido desfeita por essa resolução obsoleta.
    await waitFor(() => expect(screen.queryByTestId("world-map-view")).toBeInTheDocument());
    expect(viewStore.currentSpace()).toEqual({ kind: "World" });
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
