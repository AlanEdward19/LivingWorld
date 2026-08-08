import { describe, expect, it } from "vitest";
import { architecturePalette, cityRoofPalette } from "../../src/map-engine/architectureAppearance";

describe("architectureAppearance", () => {
  it("keeps each building deterministic and separates roof, wall and trim", () => {
    const first = architecturePalette("building-2002");
    expect(architecturePalette("building-2002")).toEqual(first);
    expect(new Set([first.roof, first.roofLight, first.wall, first.trim]).size).toBe(4);
  });

  it("gives a settlement visibly varied roof materials", () => {
    const roofs = cityRoofPalette("city-a").map((palette) => palette.roof);
    expect(new Set(roofs).size).toBeGreaterThan(2);
  });
});
