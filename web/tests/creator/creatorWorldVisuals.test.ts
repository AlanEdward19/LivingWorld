import { describe, expect, it } from "vitest";
import { creatorGroundAt, creatorPaintColor } from "../../src/components/creator/creatorWorldVisuals";

describe("creatorWorldVisuals", () => {
  it("reproduces a landscape from its seed and changes it for another seed", () => {
    const first = Array.from({ length: 48 }, (_, index) => creatorGroundAt(7, index % 8, Math.floor(index / 8)));
    const replay = Array.from({ length: 48 }, (_, index) => creatorGroundAt(7, index % 8, Math.floor(index / 8)));
    const other = Array.from({ length: 48 }, (_, index) => creatorGroundAt(8, index % 8, Math.floor(index / 8)));

    expect(replay).toEqual(first);
    expect(other).not.toEqual(first);
    expect(new Set(first.map((cell) => cell.kind))).toEqual(new Set(["grass", "soil", "water"]));
  });
});

describe("creatorPaintColor", () => {
  it("keeps painted water and terrain inside the procedural palette", () => {
    const water = creatorPaintColor(7, 3, 4, { terrain: 1, biome: 0, altitude: 0, water: true });
    const grass = creatorPaintColor(7, 3, 4, { terrain: 1, biome: 0, altitude: 0, water: false });
    expect(water).toMatch(/^#[0-9a-f]{6}$/i);
    expect(grass).toMatch(/^#[0-9a-f]{6}$/i);
    expect(water).not.toBe("#3a7bd5");
    expect(grass).not.toBe("#42b883");
  });
});
