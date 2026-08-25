import { describe, expect, it } from "vitest";
import { cityBuildingEntityFromMarker } from "../../src/map-engine/cityBuildingPlacement";
import { generateBuildingFootprint } from "../../src/map-engine/buildingFootprint";
import type { CityBuildingMarker } from "../../src/types";
import type { SpaceId } from "../../src/map-engine/types";

const SPACE: SpaceId = { kind: "City", cityId: "city-1" };

function marker(overrides: Partial<CityBuildingMarker> = {}): CityBuildingMarker {
  return {
    id: { value: 8 },
    buildingTypeId: 2,
    location: { x: 4, y: -2 },
    locationIsDerived: true,
    orientation: 0,
    ...overrides,
  };
}

describe("cityBuildingEntityFromMarker (T18 / LWV-04.5)", () => {
  it("uses the API location as the footprint origin, not a client ring around the city", () => {
    const entity = cityBuildingEntityFromMarker(marker({ location: { x: 4, y: -2 } }), SPACE, 0);

    expect(entity.position).toEqual({ x: 4, y: -2 });
  });

  it("does not place a lone building on the historical ring cell (city + (6,0))", () => {
    const cityLocation = { x: 0, y: 0 };
    const ringOnly = { x: cityLocation.x + 6, y: cityLocation.y + 0 };
    const entity = cityBuildingEntityFromMarker(
      marker({ location: { x: 2, y: 3 } }),
      SPACE,
      0,
    );

    expect(entity.position).toEqual({ x: 2, y: 3 });
    expect(entity.position).not.toEqual(ringOnly);
  });

  it("keeps generateBuildingFootprint cells relative to that origin", () => {
    const building = marker();
    const entity = cityBuildingEntityFromMarker(building, SPACE, 0);
    const footprint = generateBuildingFootprint(String(building.id.value), building.buildingTypeId, 0);

    expect(entity.footprintCells?.map((c) => ({ x: c.x, y: c.y }))).toEqual(
      footprint.map((c) => ({ x: c.x, y: c.y })),
    );
    expect(entity.size.w).toBe(Math.max(...footprint.map((c) => c.x)) + 1);
    expect(entity.size.h).toBe(Math.max(...footprint.map((c) => c.y)) + 1);
  });

  it("applies the authoritative orientation to the rendered footprint", () => {
    const building = marker({ buildingTypeId: -1, orientation: 90 });
    const entity = cityBuildingEntityFromMarker(building, SPACE, 0);
    const footprint = generateBuildingFootprint(String(building.id.value), -1, 0, 90);

    expect(entity.footprintCells?.map(({ x, y }) => ({ x, y }))).toEqual(
      footprint.map(({ x, y }) => ({ x, y })),
    );
    expect(entity.size).toEqual({
      w: Math.max(...footprint.map((c) => c.x)) + 1,
      h: Math.max(...footprint.map((c) => c.y)) + 1,
    });
  });

  it("marks sizeIsDerived when the API location is derived", () => {
    const entity = cityBuildingEntityFromMarker(marker({ locationIsDerived: true }), SPACE, 0);

    expect(entity.sizeIsDerived).toBe(true);
  });

  it("marks sizeIsDerived false when the API location is authored", () => {
    const entity = cityBuildingEntityFromMarker(marker({ locationIsDerived: false }), SPACE, 0);

    expect(entity.sizeIsDerived).toBe(false);
  });

  it.each([-1, 1, 2, 77])("preserves building type %i for type-aware rendering", (buildingTypeId) => {
    const entity = cityBuildingEntityFromMarker(marker({ buildingTypeId }), SPACE, 0);

    expect(entity.buildingTypeId).toBe(buildingTypeId);
    expect(entity.footprintCells?.length).toBeGreaterThan(0);
  });
});
