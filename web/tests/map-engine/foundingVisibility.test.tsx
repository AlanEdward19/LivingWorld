import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { mergeWorldCityMarkers } from "../../src/map-engine/worldCityMarkers";
import { LivingTimeline } from "../../src/components/LivingTimeline";
import { CityInspector } from "../../src/components/inspector/CityInspector";
import { SimulationStore } from "../../src/state/simulationStore";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { VisualScopeKind, ViewerMode } from "../../src/types";
import type { SnapshotSource, TickStreamSource } from "../../src/data/sources";
import { frontendCapabilityConsumers } from "../../src/state/frontendCapabilityConsumers";

const WORLD = { kind: "World" as const };
const ticks: TickStreamSource = { subscribe: () => () => {} };

const MOTHER = {
  id: { value: "mother" },
  name: "Aldeia Mãe",
  location: { x: 5, y: 5 },
  population: 20,
  bounds: { x: 4, y: 4, width: 3, height: 3 },
  boundsAreDerived: true,
};

describe("settlement founding visibility (LWV-04.6)", () => {
  it("shows a new city marker from cityUpsert even when the snapshot started with one city", () => {
    const markers = mergeWorldCityMarkers([MOTHER], [
      {
        id: { value: "daughter" },
        name: "Colônia Nova",
        location: { x: 12, y: 3 },
        population: 20,
        bounds: { x: 11, y: 2, width: 3, height: 3 },
        foundedFromCityId: { value: "mother" },
      },
    ], 0);

    expect(markers.map((m) => m.ref.id).sort()).toEqual(["daughter", "mother"]);
    const daughter = markers.find((m) => m.ref.id === "daughter")!;
    expect(daughter.position).toEqual({ x: 11, y: 2 });
    expect(daughter.position).not.toEqual({ x: 4, y: 4 });
    expect(daughter.label).toBe("Colônia Nova");
  });

  it("does not hide founding behind a two-city snapshot guard", () => {
    expect([MOTHER]).toHaveLength(1);
    const markers = mergeWorldCityMarkers([MOTHER], [
      {
        id: { value: "daughter" },
        name: "Colônia Nova",
        location: { x: 12, y: 3 },
        population: 20,
        bounds: { x: 11, y: 2, width: 3, height: 3 },
        foundedFromCityId: { value: "mother" },
      },
    ], 0);

    expect(markers).toHaveLength(2);
    expect(frontendCapabilityConsumers["map.founding"]).toBe("cities");
  });

  it("renders the founding on the timeline from living events", async () => {
    const source: SnapshotSource = {
      load: async () => ({
        scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
        mode: ViewerMode.Spectator,
        cursor: { tick: 0, scopeKey: "world", sequence: 0 },
        activeLayers: [],
        payload: {
          livingState: {
            npcs: [],
            cities: [],
            buildings: [],
            processes: [],
            indicators: [],
            events: [{ tick: 48, kind: 20, label: "Um novo assentamento foi fundado" }],
          },
        },
      }),
    };
    const store = new SimulationStore(source, ticks);
    await store.observeSpace(WORLD);
    render(<LivingTimeline space={WORLD} simulationStore={store} />);

    expect(screen.getByText("Um novo assentamento foi fundado")).toBeInTheDocument();
  });

  it("makes pool transfer legible on the city inspector", async () => {
    const source: SnapshotSource = {
      load: async () => ({
        scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
        mode: ViewerMode.Spectator,
        cursor: { tick: 0, scopeKey: "world", sequence: 0 },
        activeLayers: [],
        payload: {
          width: 20,
          height: 20,
          cities: [MOTHER],
          externalNpcs: [],
          activeEvents: [],
          layers: {},
          livingState: {
            npcs: [],
            cities: [
              {
                id: { value: "daughter" },
                name: "Colônia Nova",
                location: { x: 12, y: 3 },
                population: 20,
                bounds: { x: 11, y: 2, width: 3, height: 3 },
                foundedFromCityId: { value: "mother" },
              },
              {
                id: { value: "mother" },
                name: "Aldeia Mãe",
                location: { x: 5, y: 5 },
                population: 0,
                bounds: { x: 4, y: 4, width: 3, height: 3 },
              },
            ],
            buildings: [],
            processes: [],
            indicators: [],
            events: [],
          },
        },
      }),
    };
    const store = new SimulationStore(source, ticks);
    await store.observeSpace(WORLD);
    render(
      <CityInspector
        cityId="daughter"
        simulationStore={store}
        viewStore={new ViewStore(new MockPortalSource([]))}
      />,
    );

    expect(screen.getByText("20")).toBeInTheDocument();
    expect(screen.getByText(/fundado a partir/i)).toBeInTheDocument();
  });
});
