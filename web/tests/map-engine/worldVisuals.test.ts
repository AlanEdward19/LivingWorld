import { describe, expect, it } from "vitest";
import { cityGroundAt, cityGroundBounds, cloudPuffs } from "../../src/map-engine/worldVisuals";
import { naturalTerrainColor, riverOverlayPoints } from "../../src/worldMapData";

describe("worldVisuals", () => {
  it("returns the same living ground for the same space and coordinate", () => {
    expect(cityGroundAt("city-a", 12, -4)).toEqual(cityGroundAt("city-a", 12, -4));
    expect(cityGroundAt("city-a", 12, -4).color).not.toMatch(/gray|grey|#1a1f2c/i);
  });

  it("generates stable clouds per space and viewport", () => {
    const first = cloudPuffs("world", 800, 600);
    expect(cloudPuffs("world", 800, 600)).toEqual(first);
    expect(cloudPuffs("city-a", 800, 600)).not.toEqual(first);
    expect(first.length).toBeGreaterThan(2);
  });

  it("limits a city to a finite visual envelope", () => {
    expect(cityGroundBounds({ x: 10, y: 20 })).toEqual({ width: 16, height: 16, minX: 2, minY: 12 });
  });

  it("uses a varied natural terrain palette without arbitrary purple tiles", () => {
    const colors = Array.from({ length: 12 }, (_, id) => naturalTerrainColor(id));

    expect(new Set(colors).size).toBeGreaterThan(3);
    expect(colors.every((color) => !/purple|violet|#[456789a-f]0[0-5a-f][456789a-f]{3}/i.test(color))).toBe(true);
    expect(colors.every((color) => /^#[45678][0-9a-f]{5}$/i.test(color))).toBe(true);
  });

  it("keeps river overlay cells blue", () => {
    const points = riverOverlayPoints({
      width: 1, height: 1, cities: [], externalNpcs: [], activeEvents: [],
      layers: { Rivers: { isModeled: true, payload: [{ x: 0, y: 0 }] } } as never,
    });

    expect(points).toEqual([{ x: 0, y: 0, color: "#3a7bd5" }]);
  });
});
