import { describe, expect, it } from "vitest";
import {
  architecturePalette,
  buildingAppearanceForType,
  cityRoofPalette,
} from "../../src/map-engine/architectureAppearance";

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

  it.each([
    [-1, "residence"],
    [1, "agriculture"],
    [2, "forge"],
    [77, "generic"],
  ] as const)("maps building type %i to its visible %s appearance", (buildingTypeId, kind) => {
    expect(buildingAppearanceForType(buildingTypeId, "building-8").kind).toBe(kind);
  });

  it("keeps an unknown building type visible and deterministic", () => {
    const first = buildingAppearanceForType(77, "future-building");

    expect(first).toEqual(buildingAppearanceForType(77, "future-building"));
    expect(first.palette).toBeDefined();
  });
});
