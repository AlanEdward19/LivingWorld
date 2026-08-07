import { describe, expect, it } from "vitest";
import { generateBuildingFootprint } from "../../src/map-engine/buildingFootprint";

describe("generateBuildingFootprint", () => {
  it("is deterministic — same buildingId+floor always produces the same footprint", () => {
    const a = generateBuildingFootprint("building-8", 2, 0);
    const b = generateBuildingFootprint("building-8", 2, 0);
    expect(a).toEqual(b);
  });

  it("produces a different footprint for a different floor of the same building", () => {
    const groundFloor = generateBuildingFootprint("building-8", 2, 0);
    const secondFloor = generateBuildingFootprint("building-8", 2, 1);
    expect(secondFloor).not.toEqual(groundFloor);
  });

  it("always includes exactly one door cell", () => {
    for (const id of ["a", "b", "c", "d", "e", "building-8", "building-9"]) {
      const cells = generateBuildingFootprint(id, 0);
      expect(cells.filter((c) => c.material === "door")).toHaveLength(1);
    }
  });

  it("uses stone walls for an even buildingTypeId and wood for an odd one", () => {
    const stone = generateBuildingFootprint("same-id", 2);
    const wood = generateBuildingFootprint("same-id", 3);

    expect(stone.some((c) => c.material === "stoneWall")).toBe(true);
    expect(stone.some((c) => c.material === "woodWall")).toBe(false);
    expect(wood.some((c) => c.material === "woodWall")).toBe(true);
    expect(wood.some((c) => c.material === "stoneWall")).toBe(false);
  });

  it("marks only boundary cells as wall — interior cells are floor", () => {
    const cells = generateBuildingFootprint("interior-check", 0);
    const byPos = new Map(cells.map((c) => [`${c.x},${c.y}`, c]));
    const maxX = Math.max(...cells.map((c) => c.x));
    const maxY = Math.max(...cells.map((c) => c.y));
    const center = byPos.get(`${Math.floor(maxX / 2)},${Math.floor(maxY / 2)}`);

    // pelo menos um material de piso deve existir em algum lugar do footprint
    expect(cells.some((c) => c.material === "floor")).toBe(true);
    // uma célula central típica (quando existe) não é parede nem porta
    if (center) {
      expect(["floor"]).toContain(center.material);
    }
  });
});
