import { describe, expect, it, vi } from "vitest";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { portalFixtures } from "../../src/data/mock/fixtures";
import type { PortalSource } from "../../src/data/sources";
import type { SpatialPortalDto } from "../../src/data/contracts";
import type { SpaceId } from "../../src/map-engine/types";

const CITY_A: SpaceId = { kind: "City", cityId: "city-a" };
const WORLD: SpaceId = { kind: "World" };

describe("ViewStore", () => {
  it("restores the exact camera when leaving and re-entering a visited space", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures));
    const saved = { center: { x: 12, y: 34 }, scale: 5 };

    store.recordCamera(CITY_A, saved);
    store.enter(WORLD);
    store.enter(CITY_A);

    expect(store.cameraFor(CITY_A, { center: { x: 0, y: 0 }, scale: 1 })).toEqual(saved);
  });

  it("gives a never-visited space the fit-to-screen fallback, not a stored camera", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures));
    const fallback = { center: { x: 99, y: 99 }, scale: 8 };

    expect(store.cameraFor({ kind: "City", cityId: "never-visited" }, fallback)).toEqual(fallback);
  });

  it("resolves both gates to the same city with the same generic code, no per-entry branch", () => {
    const storeNorth = new ViewStore(new MockPortalSource(portalFixtures));
    const targetNorth = storeNorth.enterViaPortal("portal-city-a-north");

    const storeSouth = new ViewStore(new MockPortalSource(portalFixtures));
    const targetSouth = storeSouth.enterViaPortal("portal-city-a-south");

    expect(targetNorth).toEqual(CITY_A);
    expect(targetSouth).toEqual(CITY_A);
    expect(storeNorth.currentSpace()).toEqual(CITY_A);
    expect(storeSouth.currentSpace()).toEqual(CITY_A);
  });

  it("resolves a portal back to World from inside the city it leads to", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures));
    store.enterViaPortal("portal-city-a-north"); // World -> City(city-a)

    const target = store.enterViaPortal("portal-city-a-north"); // mesma entrada, sentido inverso

    expect(target).toEqual(WORLD);
  });

  it("is decoupled from the portal source's origin — a different implementation behaves identically", () => {
    // Implementação deliberadamente diferente da MockPortalSource (ignora `space` e devolve
    // sempre a lista inteira) — só pra provar que o ViewStore não conhece nem depende de COMO
    // a fonte filtra, só do formato dos objetos que ela devolve.
    class ReturnsEverythingPortalSource implements PortalSource {
      constructor(private readonly portals: SpatialPortalDto[]) {}
      portalsOf(): SpatialPortalDto[] {
        return this.portals;
      }
    }

    const store = new ViewStore(new ReturnsEverythingPortalSource(portalFixtures));

    const target = store.enterViaPortal("portal-city-a-north");

    expect(target).toEqual(CITY_A);
  });

  it("throws a clear error for a portal id not reachable from the current space", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures));

    expect(() => store.enterViaPortal("does-not-exist")).toThrow();
  });

  it("never issues an HTTP request from any method", () => {
    const fetchSpy = vi.fn(() => {
      throw new Error("fetch must never be called by ViewStore");
    });
    vi.stubGlobal("fetch", fetchSpy);

    const store = new ViewStore(new MockPortalSource(portalFixtures));
    store.recordCamera(CITY_A, { center: { x: 0, y: 0 }, scale: 1 });
    store.cameraFor(CITY_A, { center: { x: 0, y: 0 }, scale: 1 });
    store.enter(WORLD);
    store.enterViaPortal("portal-city-a-north");
    store.setLayerActive("Terrain", true);
    store.startFollow({ kind: "npc", id: "1", space: WORLD });
    store.stopFollow();

    expect(fetchSpy).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it("toggles a layer's active state, queryable back", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures));

    expect(store.isLayerActive("Terrain")).toBe(false);
    store.setLayerActive("Terrain", true);
    expect(store.isLayerActive("Terrain")).toBe(true);
    store.setLayerActive("Terrain", false);
    expect(store.isLayerActive("Terrain")).toBe(false);
  });
});
