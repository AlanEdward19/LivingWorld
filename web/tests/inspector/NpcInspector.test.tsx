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
  powerIds: [],
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

  it("shows generic extraordinary carrier state and active appearance descriptors when supplied", async () => {
    const inspection = {
      ...BASE_INSPECTION,
      powerIds: ["descriptor-1", "custom:storm-glass"],
      extraordinary: {
        powerIds: ["descriptor-1", "descriptor-2"],
        isManifested: true,
        manifestationState: "conditional-active",
        appearance: { scaleMultiplier: 1.4, skinTint: "#88ccff", movementTrail: "dust" },
        needSubstitution: { replacesNeed: "hunger", resourceId: 9, unitsPerUse: 2 },
        senescenceRateMultiplier: 0.5,
      },
    };

    await renderInspector(new MockNpcInspectionSource(new Map([[3, inspection]])));

    expect(screen.getByRole("heading", { name: "Extraordinário" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Poderes" })).toBeInTheDocument();
    expect(screen.getByText("custom:storm-glass")).toBeInTheDocument();
    expect(screen.getByText("descriptor-1, descriptor-2")).toBeInTheDocument();
    expect(screen.getByText("Manifestado · conditional-active")).toBeInTheDocument();
    expect(screen.getByText("1.4×")).toBeInTheDocument();
    expect(screen.getByText("#88ccff")).toBeInTheDocument();
    expect(screen.getByText("dust")).toBeInTheDocument();
    expect(screen.getByText("hunger → recurso 9 (2/unidade)")).toBeInTheDocument();
    expect(screen.getByText("0.5×")).toBeInTheDocument();
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

  it("shows rest quality, location, remaining duration and an accessible Zzz cue", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, {
      ...BASE_INSPECTION,
      currentAction: 1,
      actionTarget: { kind: "household", id: "41" },
      rest: { kind: 2, quality: 1, location: { x: 4, y: 5 }, remainingHours: 3, blocked: false },
    }]])));

    expect(screen.getByRole("heading", { name: "Descanso" })).toBeInTheDocument();
    expect(screen.getByText("Cama")).toBeInTheDocument();
    expect(screen.getByText("100%")).toBeInTheDocument();
    expect(screen.getByText("(4, 5)")).toBeInTheDocument();
    expect(screen.getByText("3 h")).toBeInTheDocument();
    expect(screen.getByLabelText(/Dormindo em Cama, qualidade 100%, 3 h restantes/)).toHaveTextContent("Zzz");
    expect(screen.getByRole("img")).toHaveAttribute(
      "alt",
      "Aparência visual do NPC 3 — Dormindo em Cama, qualidade 100%, 3 h restantes",
    );
  });

  it("shows blocked rest without applying a finished effect", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, {
      ...BASE_INSPECTION,
      currentAction: 1,
      rest: { kind: 1, quality: 0.7, location: { x: 9, y: 9 }, remainingHours: 8, blocked: true },
    }]])));

    expect(screen.getByRole("status")).toHaveTextContent("Descanso bloqueado — o lugar não é alcançável.");
    expect(screen.getByLabelText(/bloqueado/)).toBeInTheDocument();
  });

  it("shows food resource, raw vs prepared, and remaining duration while eating", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, {
      ...BASE_INSPECTION,
      currentAction: 0,
      food: { resourceId: 3, preparation: 1, remainingHours: 2, blocked: false },
    }]])));

    expect(screen.getByRole("heading", { name: "Alimentação" })).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("Preparado")).toBeInTheDocument();
    expect(screen.getByText("2 h")).toBeInTheDocument();
    expect(screen.getByRole("img")).toHaveAttribute(
      "alt",
      "Aparência visual do NPC 3 — Comendo recurso 3 (Preparado), 2 h restantes",
    );
  });

  it("names raw food distinctly from prepared food in the inspector", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, {
      ...BASE_INSPECTION,
      currentAction: 0,
      food: { resourceId: 8, preparation: 0, remainingHours: 1, blocked: false },
    }]])));

    expect(screen.getByText("Cru")).toBeInTheDocument();
    expect(screen.queryByText("Preparado")).not.toBeInTheDocument();
    expect(screen.getByRole("img")).toHaveAttribute(
      "alt",
      "Aparência visual do NPC 3 — Comendo recurso 8 (Cru), 1 h restantes",
    );
  });


  it("announces blocked food when only raw stock is available", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, {
      ...BASE_INSPECTION,
      currentAction: 0,
      food: { resourceId: 0, preparation: 0, remainingHours: 2, blocked: true },
    }]])));

    expect(screen.getByRole("status")).toHaveTextContent(
      "Refeição bloqueada — nenhum alimento comestível disponível.",
    );
  });

  it("retains follow but never offers spatial navigation for an NPC", async () => {
    await renderInspector(new MockNpcInspectionSource(new Map([[3, BASE_INSPECTION]])));

    fireEvent.click(screen.getByRole("button", { name: "Seguir" }));
    expect(screen.getByRole("button", { name: "Parar de seguir" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Abrir" })).not.toBeInTheDocument();
  });
});
