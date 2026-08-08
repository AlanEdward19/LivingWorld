import { describe, expect, it } from "vitest";
import { cityGroundAt, cityGroundBounds, cloudPuffs } from "../../src/map-engine/worldVisuals";

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
    expect(cityGroundBounds({ x: 10, y: 20 })).toEqual({ width: 34, height: 24, minX: -7, minY: 8 });
  });
});
