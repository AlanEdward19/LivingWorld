import { describe, expect, it } from "vitest";
import { LAYER_Z_ORDER, sortActiveLayers } from "../../src/map-engine/layers";
import type { VisualLayerName } from "../../src/types";

describe("LAYER_Z_ORDER", () => {
  it("declares every VisualLayerName exactly once", () => {
    const expected: VisualLayerName[] = [
      "Terrain",
      "Biome",
      "Rivers",
      "Mountains",
      "Resources",
      "Roads",
      "Borders",
      "Kingdoms",
      "Cities",
      "Villages",
      "Routes",
      "Migrations",
      "Conflicts",
      "Climate",
    ];
    expect(new Set(LAYER_Z_ORDER)).toEqual(new Set(expected));
    expect(LAYER_Z_ORDER).toHaveLength(expected.length);
  });
});

describe("sortActiveLayers", () => {
  it("reorders layers to match the declared z-order regardless of input order", () => {
    const input = [
      { id: "Rivers", overlayPoints: [] },
      { id: "Terrain", overlayPoints: [] },
      { id: "Biome", overlayPoints: [] },
    ];
    expect(sortActiveLayers(input).map((l) => l.id)).toEqual(["Terrain", "Biome", "Rivers"]);
  });

  it("does not mutate the input array", () => {
    const input = [
      { id: "Rivers", overlayPoints: [] },
      { id: "Terrain", overlayPoints: [] },
    ];
    const copy = [...input];
    sortActiveLayers(input);
    expect(input).toEqual(copy);
  });

  it("puts unknown layer ids last, stable relative to each other", () => {
    const input = [
      { id: "Mystery", overlayPoints: [] },
      { id: "Terrain", overlayPoints: [] },
    ];
    expect(sortActiveLayers(input).map((l) => l.id)).toEqual(["Terrain", "Mystery"]);
  });
});
