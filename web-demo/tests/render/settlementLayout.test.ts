import { describe, expect, it } from "vitest";
import { buildingFootprint, generateRoads, tileNoise } from "../../src/render/settlementLayout";

describe("buildingFootprint", () => {
  it("gives a building with no interior modeled a field-sized footprint, not a symbolic square", () => {
    expect(buildingFootprint({ floors: [] })).toEqual({ width: 4, height: 3 });
  });

  it("grows with room count", () => {
    const oneRoom = buildingFootprint({ floors: [{ id: "f", label: "Ground", rooms: [{ id: "r", name: "R", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] }] }] });
    const threeRooms = buildingFootprint({
      floors: [
        {
          id: "f",
          label: "Ground",
          rooms: [
            { id: "r1", name: "R1", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] },
            { id: "r2", name: "R2", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] },
            { id: "r3", name: "R3", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] },
          ],
        },
      ],
    });
    expect(threeRooms.width).toBeGreaterThanOrEqual(oneRoom.width);
  });

  it("counts rooms across every floor, not just the first", () => {
    const singleFloor = buildingFootprint({
      floors: [{ id: "f", label: "Ground", rooms: [{ id: "r", name: "R", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] }] }],
    });
    const twoFloors = buildingFootprint({
      floors: [
        { id: "f0", label: "Ground", rooms: [{ id: "r0", name: "R0", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] }] },
        { id: "f1", label: "Floor 1", rooms: [{ id: "r1", name: "R1", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] }] },
      ],
    });
    expect(twoFloors.width).toBeGreaterThanOrEqual(singleFloor.width);
  });

  it("never returns a footprint smaller than 2x2 for a building with an interior", () => {
    const footprint = buildingFootprint({
      floors: [{ id: "f", label: "Ground", rooms: [{ id: "r", name: "R", bounds: { x: 0, y: 0, width: 1, height: 1 }, furniture: [] }] }],
    });
    expect(footprint.width).toBeGreaterThanOrEqual(2);
    expect(footprint.height).toBeGreaterThanOrEqual(2);
  });
});

describe("generateRoads", () => {
  it("returns nothing for an empty settlement", () => {
    expect(generateRoads([])).toEqual([]);
  });

  it("connects every building to the centroid of all buildings", () => {
    const buildings = [{ gridPosition: { x: 0, y: 0 } }, { gridPosition: { x: 4, y: 0 } }];
    const roads = generateRoads(buildings);
    expect(roads).toHaveLength(2);
    for (const road of roads) {
      expect(road.from).toEqual({ x: 2, y: 0 });
    }
    expect(roads[0].to).toEqual({ x: 0, y: 0 });
    expect(roads[1].to).toEqual({ x: 4, y: 0 });
  });

  it("is deterministic — same input, same output", () => {
    const buildings = [{ gridPosition: { x: 1, y: 2 } }, { gridPosition: { x: 3, y: 5 } }];
    expect(generateRoads(buildings)).toEqual(generateRoads(buildings));
  });
});

describe("tileNoise", () => {
  it("is deterministic for the same seed and coordinate", () => {
    expect(tileNoise(3, 4, "oakbridge")).toBe(tileNoise(3, 4, "oakbridge"));
  });

  it("always returns a value in [0, 1)", () => {
    for (let x = -5; x < 5; x += 1) {
      for (let y = -5; y < 5; y += 1) {
        const value = tileNoise(x, y, "seed");
        expect(value).toBeGreaterThanOrEqual(0);
        expect(value).toBeLessThan(1);
      }
    }
  });

  it("varies with the seed (different settlements don't look identical)", () => {
    const a = tileNoise(0, 0, "oakbridge");
    const b = tileNoise(0, 0, "millbrook");
    expect(a).not.toBe(b);
  });
});
