import { describe, expect, it } from "vitest";
import { MockPortalSource } from "../../src/data/mock/MockPortalSource";
import { portalFixtures } from "../../src/data/mock/fixtures";

describe("MockPortalSource", () => {
  const source = new MockPortalSource(portalFixtures);

  it("returns every portal touching a City space, by refId", () => {
    const portals = source.portalsOf({ kind: "City", cityId: "city-a" });

    expect(portals.length).toBeGreaterThanOrEqual(2);
    for (const p of portals) {
      expect(p.to.refId === "city-a" || p.from.refId === "city-a").toBe(true);
    }
  });

  it("returns every portal touching the World space", () => {
    const portals = source.portalsOf({ kind: "World" });
    expect(portals.length).toBe(portalFixtures.length);
  });

  it("returns an empty list for a space with no declared portal", () => {
    const portals = source.portalsOf({ kind: "Building", buildingId: "999", cityId: "city-a" });
    expect(portals).toEqual([]);
  });
});
