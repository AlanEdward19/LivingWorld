import { describe, expect, it } from "vitest";
import { citySide } from "../../src/map-engine/citySizing";

describe("citySide", () => {
  it("matches CityBoundsResolver.Resolve's floor/ceiling for a small population on a small map", () => {
    // src/LivingWorld.Domain/Cities/CityBoundsResolver.cs: pop=0 -> side=3 (piso), mapa 10x10.
    expect(citySide(0, 10, 10)).toBe(3);
  });

  it("grows with population within the map limit", () => {
    expect(citySide(20, 100, 100)).toBe(3);
    expect(citySide(100, 100, 100)).toBe(5);
    expect(citySide(576, 100, 100)).toBe(12);
  });

  it("never exceeds the cap of 12 even for a huge population", () => {
    expect(citySide(1_000_000, 100, 100)).toBe(12);
  });

  it("never exceeds half of the smaller map dimension", () => {
    // src/LivingWorld.Domain/Cities/CityBoundsResolver.cs bugfix round 2: mapa 20x20, pop 150
    // não pode produzir lado > 10.
    expect(citySide(150, 20, 20)).toBeLessThanOrEqual(10);
  });
});
