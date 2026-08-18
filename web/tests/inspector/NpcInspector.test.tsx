import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { NpcInspector } from "../../src/components/inspector/NpcInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { MockNpcInspectionSource } from "../../src/data/mock/MockNpcInspectionSource";
import type { NpcInspection } from "../../src/data/contracts";
import type { NpcInspectionSource, SnapshotSource, TickStreamSource } from "../../src/data/sources";
import type { EntityRef } from "../../src/map-engine/types";

const CITY_SPACE = { kind: "City" as const, cityId: "city-a" };
const REF: EntityRef = { kind: "npc", id: "3", space: CITY_SPACE };

const BASE_INSPECTION: NpcInspection = {
  id: { value: 3 }, name: "Lina", sex: 1, ageYears: 27,
  culture: { id: 2 }, city: { value: "city-a" }, household: { value: 41 },
  motherId: { value: 1 }, fatherId: { value: 2 }, spouse: { value: 4 },
  profession: { id: 6 }, employer: { value: 52 }, health: 91,
  hunger: 63, thirst: 72, sleep: 81, social: 54, personality: {},
  skills: { values: { "7": 12.5 } }, currentLocation: { x: 1, y: 1 },
  currentAction: 2, actionStartedAtTick: 9,
  actionTarget: { kind: "workplace", id: "52" }, lod: 0, memories: [],
  beliefs: ["Dizem que a colheita trouxe esperança"],
  currentScope: { kind: 1, cityId: { value: "city-a" } },
};

function snapshotSource(): SnapshotSource {
  return { load: async () => ({
    scope: { kind: 1, refId: "city-a", scopeKey: "city:city-a" }, mode: 0,
    cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 }, activeLayers: [],
    payload: { id: { value: "city-a" }, location: { x: 0, y: 0 },
      aggregatePool: { count: 4, wealthSum: 40, healthSum: 320 },
      residents: [{ id: { value: 3 }, location: { x: 1, y: 1 }, currentAction: 2 }], buildings: [], layers: {} },
  }) };
}

const ticks: TickStreamSource = { subscribe: () => () => {} };

async function renderInspector(source: NpcInspectionSource) {
  const store = new SimulationStore(snapshotSource(), ticks, source);
  await store.observeSpace(CITY_SPACE);
  render(<NpcInspector entityRef={REF} simulationStore={store} viewStore={new ViewStore(new MockPortalSource([]))} />);
  await screen.findByText("Lina");
  return store;
}

describe("NpcInspector living view", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn(() => { throw new Error("component must not fetch directly"); })));
  afterEach(() => vi.unstubAllGlobals());

  it("renders identity, family, needs, health, job, skill, action, target and materialized LOD", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, BASE_INSPECTION]])));

    expect(screen.getByText("27 anos · cultura 2")).toBeInTheDocument();
    expect(screen.getByText("Trabalhando")).toBeInTheDocument();
    expect(screen.getByText("Local de trabalho 52")).toBeInTheDocument();
    expect(screen.getByText("Materializado")).toBeInTheDocument();
    expect(screen.getByLabelText("Saúde")).toHaveAttribute("value", "91");
    expect(screen.getByLabelText("Fome")).toHaveAttribute("value", "63");
    expect(screen.getByText("Habilidade 7: 12.5")).toBeInTheDocument();
    expect(screen.getAllByText("41").length).toBeGreaterThan(0);
    expect(screen.getByText("Dizem que a colheita trouxe esperança")).toBeInTheDocument();
  });

  it("loads details only for the selected materialized identity without direct fetch", async () => {
    const source = new MockNpcInspectionSource(new Map([[3, BASE_INSPECTION]]));
    const load = vi.spyOn(source, "load");

    await renderInspector(source);

    expect(load).toHaveBeenCalledWith(3);
    expect(fetch).not.toHaveBeenCalled();
  });

  it("refreshes selected detail after a tick delta", async () => {
    let current = BASE_INSPECTION;
    const source: NpcInspectionSource = { load: vi.fn(async () => current) };
    const store = await renderInspector(source);
    current = { ...BASE_INSPECTION, hunger: 44, currentAction: 1,
      actionTarget: { kind: "household", id: "41" }, actionStartedAtTick: 10 };

    store.applyDelta({ tick: 10, moved: [], removed: [] });

    await waitFor(() => expect(screen.getByLabelText("Fome")).toHaveAttribute("value", "44"));
    expect(screen.getByText("Dormindo")).toBeInTheDocument();
    expect(screen.getByText("Domicílio 41")).toBeInTheDocument();
  });

  it("keeps an anonymous aggregate resident as a count instead of inventing an inspector identity", async () => {
    const store = new SimulationStore(snapshotSource(), ticks, new MockNpcInspectionSource(new Map()));
    await store.observeSpace(CITY_SPACE);
    render(<NpcInspector entityRef={{ ...REF, id: "999" }} simulationStore={store} viewStore={new ViewStore(new MockPortalSource([]))} />);

    expect(await screen.findByRole("note")).toHaveTextContent("não está materializado");
    expect(screen.queryByText("NPC 999")).not.toBeInTheDocument();
  });

  it("shows a readable fallback for an unknown action and absent target", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, {
      ...BASE_INSPECTION, currentAction: 99, actionTarget: null,
    }]])));

    expect(screen.getByText("Atividade 99")).toBeInTheDocument();
    expect(screen.getByText("Sem alvo definido")).toBeInTheDocument();
  });

  it("states explicitly when the NPC has no developed skills", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, {
      ...BASE_INSPECTION, skills: { values: {} },
    }]])));

    expect(screen.getByText("Nenhuma habilidade desenvolvida.")).toBeInTheDocument();
  });

  it("retains follow but never offers spatial navigation for an NPC", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, BASE_INSPECTION]])));

    fireEvent.click(screen.getByRole("button", { name: "Seguir" }));
    expect(screen.getByRole("button", { name: "Parar de seguir" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Abrir" })).not.toBeInTheDocument();
  });
});
