// Fase 15.1, T34: prova de que os campos reais de projeção (T20 footprint, T30 indicadores)
// chegam intactos ao consumidor via `RealSnapshotSource` + `SimulationStore` — sem nenhuma
// tradução ad-hoc no caminho, exatamente o que faz WorldMapView/CityInspector (já escritos
// contra `FutureGlobalSnapshot`/`FutureCitySnapshot`, T15/T28) funcionarem sem alteração de
// assert quando a fonte deixa de ser a fixture do Estágio 1.
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RealSnapshotSource } from "../../../src/data/real/snapshotSource";
import { RealTickStreamSource } from "../../../src/data/real/tickStreamSource";
import { SimulationStore } from "../../../src/state/simulationStore";
import { VisualScopeKind, ViewerMode } from "../../../src/types";
import type { FutureCitySnapshot, FutureGlobalSnapshot } from "../../../src/data/contracts";

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } });
}

describe("real footprint/indicator fields end-to-end through SimulationStore", () => {
  beforeEach(() => {
    vi.stubGlobal("WebSocket", vi.fn(() => ({ close() {} })) as unknown as typeof WebSocket);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("carries GlobalCityMarker.bounds/boundsAreDerived (T20) untranslated into currentPayload", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse({
          scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
          mode: ViewerMode.Spectator,
          cursor: { tick: 0, scopeKey: "world", sequence: 0 },
          activeLayers: [],
          payload: {
            width: 20,
            height: 20,
            cities: [
              {
                id: { value: "city-a" },
                location: { x: 5, y: 5 },
                population: 12,
                bounds: { x: 4, y: 4, width: 3, height: 3 },
                boundsAreDerived: false,
              },
            ],
            externalNpcs: [],
            activeEvents: [],
            layers: {},
            portals: [],
          },
        }),
      ),
    );

    const store = new SimulationStore(new RealSnapshotSource(), new RealTickStreamSource());
    await store.observeSpace({ kind: "World" });
    const payload = store.currentPayload<FutureGlobalSnapshot>({ kind: "World" });

    expect(payload?.cities[0].bounds).toEqual({ x: 4, y: 4, width: 3, height: 3 });
    expect(payload?.cities[0].boundsAreDerived).toBe(false);
  });

  it("carries CityIndicators (T30, all 6 fields) untranslated into currentPayload", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse({
          scope: { kind: VisualScopeKind.City, refId: "city-a", scopeKey: "city:city-a" },
          mode: ViewerMode.Spectator,
          cursor: { tick: 0, scopeKey: "city:city-a", sequence: 0 },
          activeLayers: [],
          payload: {
            id: { value: "city-a" },
            location: { x: 5, y: 5 },
            aggregatePool: { count: 0, wealthSum: 0, healthSum: 0 },
            residents: [],
            buildings: [],
            layers: {},
            portals: [],
            indicators: { population: 40, wealth: 100, health: 90, inequality: 0.2, economy: 55, housing: 80 },
          },
        }),
      ),
    );

    const store = new SimulationStore(new RealSnapshotSource(), new RealTickStreamSource());
    await store.observeSpace({ kind: "City", cityId: "city-a" });
    const payload = store.currentPayload<FutureCitySnapshot>({ kind: "City", cityId: "city-a" });

    expect(payload?.indicators).toEqual({
      population: 40,
      wealth: 100,
      health: 90,
      inequality: 0.2,
      economy: 55,
      housing: 80,
    });
  });

  it("no Mock*Source fixture data leaks in when the real snapshot omits a city entirely", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        jsonResponse({
          scope: { kind: VisualScopeKind.World, refId: "", scopeKey: "world" },
          mode: ViewerMode.Spectator,
          cursor: { tick: 0, scopeKey: "world", sequence: 0 },
          activeLayers: [],
          payload: { width: 5, height: 5, cities: [], externalNpcs: [], activeEvents: [], layers: {}, portals: [] },
        }),
      ),
    );

    const store = new SimulationStore(new RealSnapshotSource(), new RealTickStreamSource());
    await store.observeSpace({ kind: "World" });
    const payload = store.currentPayload<FutureGlobalSnapshot>({ kind: "World" });

    expect(payload?.cities).toEqual([]);
  });
});
