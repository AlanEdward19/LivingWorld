import { describe, expect, it } from "vitest";
import { render, screen, fireEvent } from "@testing-library/react";
import { InteriorView } from "../src/components/InteriorView";
import { EntityInspector } from "../src/components/inspector/EntityInspector";
import { SimulationStore } from "../src/state/simulationStore";
import { ViewStore } from "../src/state/viewStore";
import { SelectionStore } from "../src/state/selectionStore";
import { MockPortalSource } from "../src/data/mock/MockPortalSource";
import type { InteriorSnapshot } from "../src/types";
import type { SnapshotSource, TickStreamSource } from "../src/data/sources";

const VIEWPORT = { width: 200, height: 200 };

function snapshot(occupancyModeled = false): InteriorSnapshot {
  return { id: { value: 8 }, city: { value: "city-1" }, buildingTypeId: 2, occupancyModeled };
}

function neverResolvingSnapshotSource(): SnapshotSource {
  return { load: () => new Promise(() => {}) };
}

function neverStreamingTickSource(): TickStreamSource {
  return { subscribe: () => () => {} };
}

function buildStores() {
  const simulationStore = new SimulationStore(neverResolvingSnapshotSource(), neverStreamingTickSource());
  const viewStore = new ViewStore(new MockPortalSource([]));
  const selectionStore = new SelectionStore();
  return { simulationStore, viewStore, selectionStore };
}

describe("InteriorView", () => {
  it("shows the unmodeled-occupancy note when OccupancyModeled is false", () => {
    const { simulationStore, viewStore, selectionStore } = buildStores();

    render(
      <InteriorView
        snapshot={snapshot(false)}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.getByRole("note")).toHaveTextContent("ainda não é modelada");
  });

  it("does not show the note when OccupancyModeled is true", () => {
    const { simulationStore, viewStore, selectionStore } = buildStores();

    render(
      <InteriorView
        snapshot={snapshot(true)}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.queryByRole("note")).not.toBeInTheDocument();
  });

  it("renders the generated wireframe floor plan as the map's own canvas", () => {
    const { simulationStore, viewStore, selectionStore } = buildStores();

    render(
      <InteriorView
        snapshot={snapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );

    expect(screen.getByTestId("map-view-canvas")).toBeInTheDocument();
  });

  // Bug real corrigido ao vivo (2026-08-07): selecionar o prédio na cidade e depois entrar nele
  // derrubava o app — `BuildingInspector` assumia `entityRef.space.kind === "City"`, mas a
  // seleção sobrevivia (mesmo kind/id) com `space` já apontando pro próprio Building. Renderiza
  // junto com `EntityInspector` (como `App.tsx` faz de verdade) — é lá que o crash acontecia.
  it("does not crash when a building selected from the city carries over into its own interior", () => {
    const { simulationStore, viewStore, selectionStore } = buildStores();
    selectionStore.select({ kind: "building", id: "8", space: { kind: "City", cityId: "city-1" } });

    expect(() =>
      render(
        <>
          <InteriorView
            snapshot={snapshot()}
            viewport={VIEWPORT}
            simulationStore={simulationStore}
            viewStore={viewStore}
            selectionStore={selectionStore}
          />
          <EntityInspector selectionStore={selectionStore} simulationStore={simulationStore} viewStore={viewStore} />
        </>,
      ),
    ).not.toThrow();
  });

  it("starts at the ground floor and moves up/down via the floor selector", () => {
    const { simulationStore, viewStore, selectionStore } = buildStores();

    render(
      <InteriorView
        snapshot={snapshot()}
        viewport={VIEWPORT}
        simulationStore={simulationStore}
        viewStore={viewStore}
        selectionStore={selectionStore}
      />,
    );
    expect(screen.getByTestId("floor-label")).toHaveTextContent("Térreo");

    fireEvent.click(screen.getByLabelText("andar-acima"));
    expect(screen.getByTestId("floor-label")).toHaveTextContent("1º andar acima");

    fireEvent.click(screen.getByLabelText("andar-abaixo"));
    fireEvent.click(screen.getByLabelText("andar-abaixo"));
    expect(screen.getByTestId("floor-label")).toHaveTextContent("1º subsolo");
  });
});
