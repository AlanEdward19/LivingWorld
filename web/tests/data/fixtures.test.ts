import { describe, expect, it } from "vitest";
import {
  cityASnapshotEnvelope,
  cityBSnapshotEnvelope,
  portalFixtures,
  TOTAL_MOCK_NPC_COUNT,
  worldSnapshotEnvelope,
} from "../../src/data/mock/fixtures";

describe("mock fixtures", () => {
  it("cover at least 2 cities in the world snapshot", () => {
    expect(worldSnapshotEnvelope.payload?.cities.length).toBeGreaterThanOrEqual(2);
  });

  it("cover at least 20 NPCs across world + city fixtures", () => {
    const total =
      (worldSnapshotEnvelope.payload?.externalNpcs.length ?? 0) +
      (cityASnapshotEnvelope.payload?.residents.length ?? 0) +
      (cityBSnapshotEnvelope.payload?.residents.length ?? 0);

    expect(total).toBeGreaterThanOrEqual(20);
    expect(TOTAL_MOCK_NPC_COUNT).toBe(total);
  });

  it("declare at least 2 portals for the same World<->City pair", () => {
    const sameCityPair = portalFixtures.filter(
      (p) =>
        (p.from.space === "World" && p.to.space === "City" && p.to.refId === "city-a") ||
        (p.to.space === "World" && p.from.space === "City" && p.from.refId === "city-a"),
    );

    expect(sameCityPair.length).toBeGreaterThanOrEqual(2);
    const distinctLabels = new Set(sameCityPair.map((p) => p.label));
    expect(distinctLabels.size).toBe(sameCityPair.length);
  });

  it("mark at least one layer as NotYetModeled in the world snapshot", () => {
    const layers = worldSnapshotEnvelope.payload!.layers;
    const notYetModeled = Object.values(layers).filter((l) => !l.isModeled);

    expect(notYetModeled.length).toBeGreaterThan(0);
  });

  it("mark city footprints as derived, not authored", () => {
    for (const marker of worldSnapshotEnvelope.payload!.cities) {
      expect(marker.boundsAreDerived).toBe(true);
    }
  });
});
