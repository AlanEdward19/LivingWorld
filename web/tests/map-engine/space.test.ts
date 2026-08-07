import { describe, expect, it } from "vitest";
import { ancestors, localToParent, parentToLocal, SCALE, toScopeKey } from "../../src/map-engine/space";
import type { SpaceId } from "../../src/map-engine/types";

describe("localToParent / parentToLocal", () => {
  it("round-trips exactly for City <-> World", () => {
    const space: SpaceId = { kind: "City", cityId: "city-a" };
    const local = { x: 37, y: 129 };

    const parent = localToParent(space, local);
    const back = parentToLocal(space, parent);

    expect(back.x).toBeCloseTo(local.x, 9);
    expect(back.y).toBeCloseTo(local.y, 9);
  });

  it("round-trips exactly for Building <-> City", () => {
    const space: SpaceId = { kind: "Building", buildingId: "b-1", cityId: "city-a" };
    const local = { x: 5, y: 11 };

    const parent = localToParent(space, local);
    const back = parentToLocal(space, parent);

    expect(back.x).toBeCloseTo(local.x, 9);
    expect(back.y).toBeCloseTo(local.y, 9);
  });

  it("applies the single exported SCALE constant, not a spread literal", () => {
    const space: SpaceId = { kind: "City", cityId: "city-a" };
    const parent = localToParent(space, { x: SCALE.worldTilesPerCityTile, y: 0 });

    expect(parent).toEqual({ x: 1, y: 0 });
  });

  it("throws for WorldSpace, which has no parent", () => {
    expect(() => localToParent({ kind: "World" }, { x: 0, y: 0 })).toThrow();
    expect(() => parentToLocal({ kind: "World" }, { x: 0, y: 0 })).toThrow();
  });
});

describe("ancestors", () => {
  it("returns just World for the World space", () => {
    expect(ancestors({ kind: "World" })).toEqual([{ kind: "World" }]);
  });

  it("returns the correct chain for a City space", () => {
    const city: SpaceId = { kind: "City", cityId: "city-a" };
    expect(ancestors(city)).toEqual([{ kind: "World" }, city]);
  });

  it("returns the correct chain for a Building space", () => {
    const building: SpaceId = { kind: "Building", buildingId: "b-1", cityId: "city-a" };
    expect(ancestors(building)).toEqual([{ kind: "World" }, { kind: "City", cityId: "city-a" }, building]);
  });
});

describe("toScopeKey", () => {
  it("matches VisualScope.ScopeKey's 3 formats: world, city:{id}, interior:{id}", () => {
    expect(toScopeKey({ kind: "World" })).toBe("world");
    expect(toScopeKey({ kind: "City", cityId: "abc-123" })).toBe("city:abc-123");
    expect(toScopeKey({ kind: "Building", buildingId: "42", cityId: "abc-123" })).toBe("interior:42");
  });
});
