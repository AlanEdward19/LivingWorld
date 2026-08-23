import { describe, expect, it } from "vitest";
import { overlayProcessOnNpc, processCueVisual } from "../../src/map-engine/cityNpcOverlay";
import type { ProcessVisual } from "../../src/data/contracts";
import type { AuthoritativeEntity } from "../../src/map-engine/types";

const SPACE = { kind: "City" as const, cityId: "city-1" };

function npc(action: number | null = 2): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id: "9", space: SPACE },
    position: { x: 3, y: 4 },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#abc",
    currentAction: action,
  };
}

function process(overrides: Partial<ProcessVisual> = {}): ProcessVisual {
  return {
    id: 90,
    kind: "rest",
    targetId: 9,
    progress: 0.5,
    descriptorKey: "rest",
    location: { x: 3, y: 4 },
    ...overrides,
  };
}

describe("cityNpcOverlay (T20 / LWV-02)", () => {
  it("attaches a rest process to the NPC that owns the target id", () => {
    const entity = overlayProcessOnNpc(npc(1), [process()]);

    expect(entity.process?.kind).toBe("rest");
    expect(entity.position).toEqual({ x: 3, y: 4 });
  });

  it("attaches food/water/crop processes colocated with the NPC", () => {
    const crop = process({ kind: "crop", targetId: 77, descriptorKey: "plant", location: { x: 3, y: 4 } });
    const entity = overlayProcessOnNpc(npc(2), [crop]);

    expect(entity.process?.kind).toBe("crop");
  });

  it("maps work/rest/food/water/crop to a visible cue icon, never empty", () => {
    expect(processCueVisual("rest", "rest").icon).toBe("moon");
    expect(processCueVisual("food", "eat-prepared").icon).toBe("apple");
    expect(processCueVisual("water", "carry-water").icon).toBe("waves");
    expect(processCueVisual("crop", "plant").icon).toBe("tool");
    expect(processCueVisual("odd", "mystery").icon).toBe("question");
    expect(processCueVisual("odd", "mystery").hidden).toBe(false);
  });
});
