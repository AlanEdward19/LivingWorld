import { describe, expect, it } from "vitest";
import { paletteForBuildingKind, SETTLEMENT_PALETTE } from "../../src/map/isoPalette";
import { WORLD_FIXTURE } from "../../src/fixture/oakbridge";
import type { BuildingKind } from "../../src/fixture/types";

const HEX_COLOR = /^#[0-9a-f]{6}$/i;

function expectValidPalette(palette: { top: string; left: string; right: string }) {
  expect(palette.top).toMatch(HEX_COLOR);
  expect(palette.left).toMatch(HEX_COLOR);
  expect(palette.right).toMatch(HEX_COLOR);
}

describe("isoPalette", () => {
  const kindsInFixture = new Set<BuildingKind>(
    WORLD_FIXTURE.settlements.flatMap((s) => s.buildings.map((b) => b.kind)),
  );

  it("every BuildingKind used in the fixture has a defined palette", () => {
    expect(kindsInFixture.size).toBeGreaterThan(0);
    for (const kind of kindsInFixture) {
      expectValidPalette(paletteForBuildingKind(kind));
    }
  });

  it("every BuildingKind of the type union has a defined palette", () => {
    const allKinds: BuildingKind[] = ["residence", "agriculture", "forge", "generic"];
    for (const kind of allKinds) {
      expectValidPalette(paletteForBuildingKind(kind));
    }
  });

  it("the 3 faces of a building palette are distinct shades (flat-shaded, not a single flat color)", () => {
    const palette = paletteForBuildingKind("residence");
    const faces = new Set([palette.top, palette.left, palette.right]);
    expect(faces.size).toBe(3);
  });

  it("defines a settlement marker palette", () => {
    expectValidPalette(SETTLEMENT_PALETTE);
  });
});
