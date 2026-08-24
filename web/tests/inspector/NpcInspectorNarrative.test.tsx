import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { NpcInspector } from "../../src/components/inspector/NpcInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { MockNpcInspectionSource } from "../../src/data/mock/MockNpcInspectionSource";
import { MockBiographySource } from "../../src/data/mock/MockBiographySource";
import { MockConversationSource } from "../../src/data/mock/MockConversationSource";
import type { NpcInspection } from "../../src/data/contracts";
import type { NarrativeSources } from "../../src/data/sources";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";
import type { EntityRef } from "../../src/map-engine/types";

// Fase 15.1, T7 (LWV-05): biografia + conversa em NpcInspector — arquivo próprio (não estende
// NpcInspector.test.tsx de T5) para não tocar os testes de outra task.
const CITY_SPACE = { kind: "City" as const, cityId: "city-a" };
const REF: EntityRef = { kind: "npc", id: "3", space: CITY_SPACE };

const BASE_INSPECTION: NpcInspection = {
  id: { value: 3 }, name: "Lina", sex: 1, ageYears: 27,
  culture: { id: 2 }, city: { value: "city-a" }, household: { value: 41 },
  motherId: { value: 1 }, fatherId: { value: 2 }, spouse: { value: 4 },
  profession: { id: 6 }, employer: { value: 52 }, health: 91,
  hunger: 63, thirst: 72, sleep: 81, social: 54, personality: {},
  skills: { values: {} }, currentLocation: { x: 1, y: 1 },
  currentAction: 2, actionStartedAtTick: 9,
  actionTarget: { kind: "workplace", id: "52" }, lod: 0, memories: [],
  beliefs: [], powerIds: [], currentScope: { kind: 1, cityId: { value: "city-a" } },
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

async function renderInspector(narrativeSources: NarrativeSources) {
  const store = new SimulationStore(snapshotSource(), ticks, new MockNpcInspectionSource(new Map([[3, BASE_INSPECTION]])));
  await store.observeSpace(CITY_SPACE);
  render(<NpcInspector
    entityRef={REF}
    simulationStore={store}
    viewStore={new ViewStore(new MockPortalSource([]))}
    narrativeSources={narrativeSources}
  />);
  await screen.findByText("Lina");
}

describe("NpcInspector narrative and conversation surfaces (T7)", () => {
  beforeEach(() => vi.stubGlobal("fetch", vi.fn(() => { throw new Error("component must not fetch directly"); })));
  afterEach(() => vi.unstubAllGlobals());

  it("narrates the biography loaded from the injected source, not from fetch", async () => {
    await renderInspector({
      biography: new MockBiographySource(new Map([[3, { prose: "Lina cresceu na vila e aprendeu a colher trigo." }]])),
      chronicle: { load: async () => ({ prose: "" }) },
      conversation: new MockConversationSource(),
    });

    expect(await screen.findByText("Lina cresceu na vila e aprendeu a colher trigo.")).toBeInTheDocument();
  });

  it("states honestly when the selected npc has no recorded biography yet", async () => {
    await renderInspector({
      biography: new MockBiographySource(new Map()),
      chronicle: { load: async () => ({ prose: "" }) },
      conversation: new MockConversationSource(),
    });

    expect(await screen.findByText("Nenhum evento registrado ainda.")).toBeInTheDocument();
  });

  it("starts a conversation and renders the dialogue turn returned by the source", async () => {
    await renderInspector({
      biography: new MockBiographySource(new Map()),
      chronicle: { load: async () => ({ prose: "" }) },
      conversation: new MockConversationSource(),
    });

    fireEvent.click(await screen.findByRole("button", { name: "Iniciar conversa" }));
    await waitFor(() => expect(screen.getByLabelText("Mensagem")).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText("Mensagem"), { target: { value: "oi" } });
    fireEvent.click(screen.getByRole("button", { name: "Enviar" }));

    expect(await screen.findByText("[mock] oi")).toBeInTheDocument();
  });

  it("shows a readable rejection reason when the conversation ends mid-session and blocks further sends", async () => {
    const conversation = new MockConversationSource();
    await renderInspector({
      biography: new MockBiographySource(new Map()),
      chronicle: { load: async () => ({ prose: "" }) },
      conversation,
    });

    fireEvent.click(await screen.findByRole("button", { name: "Iniciar conversa" }));
    await waitFor(() => expect(screen.getByLabelText("Mensagem")).toBeInTheDocument());
    await conversation.end(1);

    fireEvent.change(screen.getByLabelText("Mensagem"), { target: { value: "ainda aí?" } });
    fireEvent.click(screen.getByRole("button", { name: "Enviar" }));

    expect(await screen.findByText("Sessão de conversa não encontrada.")).toBeInTheDocument();
  });
});
