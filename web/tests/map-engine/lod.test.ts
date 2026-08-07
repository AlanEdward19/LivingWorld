import { describe, expect, it } from "vitest";
import { aggregate, levelFor, type LodThresholds } from "../../src/map-engine/lod";
import type { AuthoritativeEntity } from "../../src/map-engine/types";

const THRESHOLDS: LodThresholds = { aggregate: 4, token: 10, detail: 18 };

function npc(id: string, x: number, y: number): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space: { kind: "World" } },
    position: { x, y },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#000",
  };
}

describe("levelFor", () => {
  it("returns aggregate strictly below the aggregate threshold", () => {
    expect(levelFor(3.9, THRESHOLDS)).toBe("aggregate");
  });

  it("returns dot exactly at the aggregate threshold", () => {
    expect(levelFor(4, THRESHOLDS)).toBe("dot");
  });

  it("returns dot strictly between the aggregate and token thresholds", () => {
    expect(levelFor(7, THRESHOLDS)).toBe("dot");
  });

  it("returns token exactly at the token threshold", () => {
    expect(levelFor(10, THRESHOLDS)).toBe("token");
  });

  it("returns token strictly between the token and detail thresholds", () => {
    expect(levelFor(14, THRESHOLDS)).toBe("token");
  });

  it("returns token-detail exactly at the detail threshold", () => {
    expect(levelFor(18, THRESHOLDS)).toBe("token-detail");
  });

  it("returns token-detail strictly above the detail threshold", () => {
    expect(levelFor(25, THRESHOLDS)).toBe("token-detail");
  });
});

describe("aggregate", () => {
  it("groups entities by deterministic spatial bucket and preserves the total count", () => {
    const entities = [npc("a", 1, 1), npc("b", 2, 2), npc("c", 11, 1), npc("d", 50, 50)];
    const clusters = aggregate(entities, 10);

    const total = clusters.reduce((sum, c) => sum + c.count, 0);
    expect(total).toBe(entities.length);
  });

  it("puts entities in the same cellSize-sized region into the same bucket", () => {
    const entities = [npc("a", 1, 1), npc("b", 2, 2)];
    const clusters = aggregate(entities, 10);

    expect(clusters).toHaveLength(1);
    expect(clusters[0]).toMatchObject({ bucketX: 0, bucketY: 0, count: 2 });
  });

  it("is deterministic — same positions produce the same bucket coordinates every call", () => {
    const entities = [npc("a", 23, 47)];
    const first = aggregate(entities, 10);
    const second = aggregate(entities, 10);

    expect(first).toEqual(second);
    expect(first[0]).toMatchObject({ bucketX: 2, bucketY: 4 });
  });

  it("preserves entity identity — every input EntityRef is retrievable from its cluster", () => {
    const a = npc("a", 1, 1);
    const b = npc("b", 2, 2);
    const clusters = aggregate([a, b], 10);

    expect(clusters[0].refs).toEqual([a.ref, b.ref]);
  });
});
