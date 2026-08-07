import { describe, expect, it } from "vitest";
import { addSettlement, eraseCell, paintTerrainCell, paintWaterCell } from "../../../src/components/creator/tools/paint";

// Comportamento de autoria espacial como funções puras: mesma entrada, mesma saída.
describe("paint tools", () => {
  it("paints a cell with the selected terrain id, defaulting altitude/water", () => {
    expect(paintTerrainCell({}, 3, 4, 2, 1)).toEqual({
      "3,4": { terrain: 2, biome: 1, altitude: 0, water: false },
    });
  });

  it("terrain paint preserves an existing water flag instead of clearing it", () => {
    const cells = { "3,4": { terrain: 1, biome: 1, altitude: 0, water: true } };
    expect(paintTerrainCell(cells, 3, 4, 2, 1)).toEqual({
      "3,4": { terrain: 2, biome: 1, altitude: 0, water: true },
    });
  });

  it("water paint keeps the existing terrain id instead of overwriting it", () => {
    const cells = { "3,4": { terrain: 5, biome: 1, altitude: 0, water: false } };
    expect(paintWaterCell(cells, 3, 4, 1, 1)).toEqual({
      "3,4": { terrain: 5, biome: 1, altitude: 0, water: true },
    });
  });

  it("water paint on an unpainted cell falls back to the selected terrain/biome", () => {
    expect(paintWaterCell({}, 3, 4, 2, 1)).toEqual({
      "3,4": { terrain: 2, biome: 1, altitude: 0, water: true },
    });
  });

  it("erases a painted cell", () => {
    expect(eraseCell({ "3,4": { terrain: 1, biome: 1, altitude: 0, water: false } }, 3, 4)).toEqual({});
  });

  it("adds a settlement with an auto-generated name", () => {
    expect(addSettlement([], 2, 2)).toEqual([{ name: "assentamento-1", x: 2, y: 2 }]);
    expect(addSettlement([{ name: "vila", x: 0, y: 0 }], 2, 2)).toEqual([
      { name: "vila", x: 0, y: 0 },
      { name: "assentamento-2", x: 2, y: 2 },
    ]);
  });
});
