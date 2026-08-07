import { describe, expect, it } from "vitest";
import { render, screen, fireEvent, act } from "@testing-library/react";
import { EntityInspector } from "../../src/components/inspector/EntityInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { SelectionStore } from "../../src/state/selectionStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../../src/types";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";

const WORLD_SPACE = { kind: "World" as const };

function worldSnapshotSource(): SnapshotSource {
  return {
    load: async () => ({
      scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "world", sequence: 0 },
      activeLayers: [],
      payload: {
        width: 10,
        height: 10,
        cities: [{ id: { value: "city-a" }, location: { x: 3, y: 4 }, population: 340 }],
        externalNpcs: [{ id: { value: 9 }, location: { x: 1, y: 1 } }],
        activeEvents: [],
        layers: {},
      },
    }),
  };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

async function buildStores() {
  const simulationStore = new SimulationStore(worldSnapshotSource(), neverStreamingTickSource());
  const viewStore = new ViewStore(new MockPortalSource([]));
  const selectionStore = new SelectionStore();
  await simulationStore.observeSpace(WORLD_SPACE);
  return { simulationStore, viewStore, selectionStore };
}

describe("EntityInspector", () => {
  it("renders nothing when no entity is selected", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();

    render(<EntityInspector selectionStore={selectionStore} simulationStore={simulationStore} viewStore={viewStore} />);

    expect(screen.queryByTestId("entity-inspector")).not.toBeInTheDocument();
  });

  it("switches content when a new entity is selected, without closing the panel", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    selectionStore.select({ kind: "npc", id: "9", space: WORLD_SPACE });

    render(<EntityInspector selectionStore={selectionStore} simulationStore={simulationStore} viewStore={viewStore} />);
    expect(screen.getByRole("heading", { name: "NPC 9" })).toBeInTheDocument();

    act(() => selectionStore.select({ kind: "city", id: "city-a", space: WORLD_SPACE }));

    expect(screen.getByTestId("entity-inspector")).toBeInTheDocument(); // nunca desmontou
    expect(screen.getByRole("heading", { name: /Cidade/ })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "NPC 9" })).not.toBeInTheDocument();
  });

  it("the close (X) button clears the selection", async () => {
    const { simulationStore, viewStore, selectionStore } = await buildStores();
    selectionStore.select({ kind: "npc", id: "9", space: WORLD_SPACE });

    render(<EntityInspector selectionStore={selectionStore} simulationStore={simulationStore} viewStore={viewStore} />);
    fireEvent.click(screen.getByLabelText("fechar-painel"));

    expect(selectionStore.current()).toBeNull();
    expect(screen.queryByTestId("entity-inspector")).not.toBeInTheDocument();
  });
});
