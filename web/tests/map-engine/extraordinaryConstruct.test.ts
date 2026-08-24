import { describe, expect, it } from "vitest";
import { extraordinaryConstructEntity } from "../../src/map-engine/extraordinaryConstruct";
import type { ProcessVisual } from "../../src/data/contracts";

describe("extraordinaryConstructEntity", () => {
  it("renders the authoritative footprint and keeps it non-selectable", () => {
    const process: ProcessVisual = {
      id: -8,
      kind: "extraordinary-construct",
      targetId: 1,
      progress: 0.5,
      descriptorKey: "green-energy",
      location: { x: 10, y: 20 },
      footprint: [{ x: 10, y: 20 }, { x: 11, y: 20 }],
      appearanceToken: "green-energy",
    };

    const entity = extraordinaryConstructEntity(process, { kind: "World" });

    expect(entity).toMatchObject({
      ref: { kind: "building", id: "construct:7" },
      position: { x: 10, y: 20 },
      size: { w: 2, h: 1 },
      decorative: true,
    });
    expect(entity?.footprintCells).toEqual([
      expect.objectContaining({ x: 0, y: 0 }),
      expect.objectContaining({ x: 1, y: 0 }),
    ]);
    expect(entity?.footprintCells?.[0]?.color).toContain("hsla(");
  });

  it("does not invent a construct without an authoritative footprint", () => {
    expect(extraordinaryConstructEntity({
      id: -1, kind: "extraordinary-construct", targetId: 1,
      progress: 1, descriptorKey: "energy", location: { x: 0, y: 0 },
    }, { kind: "World" })).toBeNull();
  });
});
