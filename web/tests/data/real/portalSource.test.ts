import { beforeEach, describe, expect, it, vi } from "vitest";
import { RealPortalSource } from "../../../src/data/real/portalSource";
import { SimulationStore } from "../../../src/state/simulationStore";
import type { SnapshotSource, TickStreamSource } from "../../../src/data/sources";
import { VisualScopeKind, ViewerMode } from "../../../src/types";
import type { SpatialPortalDto } from "../../../src/data/contracts";

const PORTAL: SpatialPortalDto = {
  id: "portal-1",
  label: "Portão norte",
  from: { space: "World", refId: "", cell: { x: 1, y: 1 } },
  to: { space: "City", refId: "city-a", cell: { x: 0, y: 0 } },
};

function stubSnapshotSource(portals: SpatialPortalDto[]): SnapshotSource {
  return {
    load: vi.fn(async (space) => ({
      scope: {
        kind: space.kind === "World" ? VisualScopeKind.World : VisualScopeKind.City,
        refId: space.kind === "City" ? space.cityId : "",
        scopeKey: space.kind === "World" ? "world" : `city:${space.kind === "City" ? space.cityId : ""}`,
      },
      mode: ViewerMode.Spectator,
      cursor: { tick: 0, scopeKey: "world", sequence: 0 },
      activeLayers: [],
      payload: { portals },
    })),
  };
}

const noopTickStreamSource: TickStreamSource = { subscribe: () => () => {} };

describe("RealPortalSource", () => {
  let simulationStore: SimulationStore;

  beforeEach(() => {
    simulationStore = new SimulationStore(stubSnapshotSource([PORTAL]), noopTickStreamSource);
  });

  it("reads the portals field of the currently observed space's snapshot", async () => {
    await simulationStore.observeSpace({ kind: "World" });
    const source = new RealPortalSource(simulationStore);

    expect(source.portalsOf({ kind: "World" })).toEqual([PORTAL]);
  });

  it("never triggers a fetch/WebSocket of its own — it only reads currentPayload", async () => {
    const fetchSpy = vi.fn(() => {
      throw new Error("RealPortalSource must never fetch");
    });
    vi.stubGlobal("fetch", fetchSpy);

    await simulationStore.observeSpace({ kind: "World" });
    new RealPortalSource(simulationStore).portalsOf({ kind: "World" });

    expect(fetchSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("returns an empty list (not throwing) for a space with no snapshot loaded yet", () => {
    const source = new RealPortalSource(simulationStore);

    expect(source.portalsOf({ kind: "City", cityId: "never-observed" })).toEqual([]);
  });

  it("returns an empty list for an observed space whose snapshot declares no portals", async () => {
    simulationStore = new SimulationStore(stubSnapshotSource([]), noopTickStreamSource);
    await simulationStore.observeSpace({ kind: "World" });

    expect(new RealPortalSource(simulationStore).portalsOf({ kind: "World" })).toEqual([]);
  });
});
