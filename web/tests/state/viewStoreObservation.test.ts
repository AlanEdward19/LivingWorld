import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ViewStore } from "../../src/state/viewStore";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { portalFixtures } from "../../src/data/mock/fixtures";
import type { SpaceId } from "../../src/map-engine/types";

const SOURCE_ID = "spectator-test";
const CITY_A: SpaceId = { kind: "City", cityId: "city-a" };
const WORLD: SpaceId = { kind: "World" };
const BUILDING: SpaceId = { kind: "Building", buildingId: "9001", cityId: "city-a" };

describe("ViewStore observation scope", () => {
  let fetchSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    fetchSpy = vi.fn().mockResolvedValue(new Response(null, { status: 200 }));
    vi.stubGlobal("fetch", fetchSpy);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function lastRequestBody(): { sourceId: string; scope: Record<string, string> } {
    const init = fetchSpy.mock.calls.at(-1)![1] as RequestInit;
    return JSON.parse(init.body as string);
  }

  it("posts World scope when enter navigates to World", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures), SOURCE_ID);
    store.enter(CITY_A);
    fetchSpy.mockClear();

    store.enter(WORLD);

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(String(fetchSpy.mock.calls[0][0])).toContain("/observation/scope");
    expect(lastRequestBody()).toEqual({ sourceId: SOURCE_ID, scope: { kind: "World" } });
  });

  it("posts City scope with cityId when enter navigates into a city", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures), SOURCE_ID);
    fetchSpy.mockClear();

    store.enter(CITY_A);

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(lastRequestBody()).toEqual({
      sourceId: SOURCE_ID,
      scope: { kind: "City", cityId: "city-a" },
    });
  });

  it("posts scope when enterViaPortal changes the observed space", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures), SOURCE_ID);
    fetchSpy.mockClear();

    store.enterViaPortal("portal-city-a-north");

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(lastRequestBody()).toEqual({
      sourceId: SOURCE_ID,
      scope: { kind: "City", cityId: "city-a" },
    });
  });

  it("posts scope when goToAncestor changes the observed space", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures), SOURCE_ID);
    store.enter(CITY_A);
    fetchSpy.mockClear();

    store.goToAncestor(WORLD);

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(lastRequestBody()).toEqual({ sourceId: SOURCE_ID, scope: { kind: "World" } });
  });

  it("posts Building scope with cityId and buildingId", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures), SOURCE_ID);
    fetchSpy.mockClear();

    store.enter(BUILDING);

    expect(fetchSpy).toHaveBeenCalledOnce();
    expect(lastRequestBody()).toEqual({
      sourceId: SOURCE_ID,
      scope: { kind: "Building", cityId: "city-a", buildingId: "9001" },
    });
  });

  it("does not post again when navigation stays in the same scope", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures), SOURCE_ID);
    store.enter(WORLD);
    fetchSpy.mockClear();

    store.enter(WORLD);

    expect(fetchSpy).not.toHaveBeenCalled();
  });

  it("does not post from camera or layer changes that do not change SpaceId", () => {
    const store = new ViewStore(new MockPortalSource(portalFixtures), SOURCE_ID);
    fetchSpy.mockClear();

    store.recordCamera(CITY_A, { center: { x: 1, y: 2 }, scale: 3 });
    store.setLayerActive("Terrain", true);
    store.startFollow({ kind: "npc", id: "1", space: WORLD });
    store.stopFollow();

    expect(fetchSpy).not.toHaveBeenCalled();
  });
});
