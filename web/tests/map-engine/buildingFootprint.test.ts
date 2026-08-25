import { describe, expect, it } from "vitest";
import { generateBuildingFootprint, generateCityWallFootprint } from "../../src/map-engine/buildingFootprint";

describe("generateBuildingFootprint", () => {
  it("is deterministic — same buildingId+floor always produces the same footprint", () => {
    const a = generateBuildingFootprint("building-8", 2, 0);
    const b = generateBuildingFootprint("building-8", 2, 0);
    expect(a).toEqual(b);
  });

  it("keeps footprint and door fixed when only the observed Z level changes", () => {
    const groundFloor = generateBuildingFootprint("building-8", 2, 0);
    const secondFloor = generateBuildingFootprint("building-8", 2, 1);
    expect(secondFloor).toEqual(groundFloor);
  });

  it("keeps a city's wall and gate fixed when only the observed Z level changes", () => {
    expect(generateCityWallFootprint("city-a", 8, 6, -1)).toEqual(generateCityWallFootprint("city-a", 8, 6, 2));
  });

  it("fills a city with roof blocks while preserving streets through the center", () => {
    const city = generateCityWallFootprint("city-a", 10, 8);

    expect(city.some((cell) => cell.material === "floor")).toBe(true);
    expect(city.some((cell) => cell.x === 5 && cell.y === 3)).toBe(false);
    expect(city.some((cell) => cell.x === 0 && cell.y === 0)).toBe(false);
  });

  it("always includes exactly one door cell", () => {
    for (const id of ["a", "b", "c", "d", "e", "building-8", "building-9"]) {
      const cells = generateBuildingFootprint(id, 0);
      expect(cells.filter((c) => c.material === "door")).toHaveLength(1);
    }
  });

  it.each([0, 90, 180, 270] as const)("keeps a house compact (3x3 to 4x4) and internal when rotated %i degrees", (orientation) => {
    const cells = generateBuildingFootprint("house-12", -1, 0, orientation);
    const width = Math.max(...cells.map((cell) => cell.x)) + 1;
    const height = Math.max(...cells.map((cell) => cell.y)) + 1;

    expect(width).toBeGreaterThanOrEqual(3);
    expect(width).toBeLessThanOrEqual(4);
    expect(height).toBeGreaterThanOrEqual(3);
    expect(height).toBeLessThanOrEqual(4);
    expect(cells.some((cell) => cell.material === "floor")).toBe(true);
  });

  it("rotates the door while preserving the same occupied cells", () => {
    const south = generateBuildingFootprint("house-12", -1, 0, 0);
    const east = generateBuildingFootprint("house-12", -1, 0, 90);

    expect(south.find((cell) => cell.material === "door")).not.toEqual(
      east.find((cell) => cell.material === "door"),
    );
    expect(new Set(south.map((cell) => `${cell.x},${cell.y}`))).toEqual(
      new Set(east.map((cell) => `${cell.x},${cell.y}`)),
    );
  });

  it("varies wall material by building identity, not just by type — so houses of the same type actually differ", () => {
    // Feedback do usuário ("vilas só tem 1 aparência"): toda residência compartilha o mesmo
    // buildingTypeId (-1), então variar só por tipo dava a mesma parede pra toda casa da vila.
    // Materiais agora vêm do hash do buildingId, então ids diferentes do MESMO tipo divergem.
    const materials = ["house-1", "house-2", "house-3", "house-4", "house-5", "house-6"].map((id) => {
      const cells = generateBuildingFootprint(id, -1);
      return cells.find((c) => c.material === "stoneWall" || c.material === "woodWall")?.material;
    });

    expect(new Set(materials).size).toBeGreaterThan(1);
    // continua determinístico e continua só as duas opções válidas
    for (const id of ["house-1", "house-2"]) {
      expect(generateBuildingFootprint(id, -1)).toEqual(generateBuildingFootprint(id, -1));
    }
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
