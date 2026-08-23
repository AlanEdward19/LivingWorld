import { describe, expect, it } from "vitest";
import { constructionSiteEntityFromProcess } from "../../src/map-engine/constructionSite";
import type { ProcessVisual } from "../../src/data/contracts";
import type { SpaceId } from "../../src/map-engine/types";

const SPACE: SpaceId = { kind: "City", cityId: "city-1" };

function process(overrides: Partial<ProcessVisual> = {}): ProcessVisual {
  return {
    id: 0,
    kind: "construction",
    targetId: 2,
    progress: 0.4,
    descriptorKey: "construction",
    location: { x: 5, y: 7 },
    ...overrides,
  };
}

describe("constructionSiteEntityFromProcess (T19 / LWV-04.4)", () => {
  it("places the scaffold at the process location before a completed building exists", () => {
    const entity = constructionSiteEntityFromProcess(process(), SPACE);
    expect(entity).not.toBeNull();

    expect(entity!.position).toEqual({ x: 5, y: 7 });
    expect(entity!.process?.kind).toBe("construction");
    expect(entity!.process?.progress).toBe(0.4);
  });

  it("still emits a site when progress is zero (queued, not started)", () => {
    const entity = constructionSiteEntityFromProcess(process({ progress: 0 }), SPACE);
    expect(entity).not.toBeNull();

    expect(entity!.process?.progress).toBe(0);
    expect(entity!.position).toEqual({ x: 5, y: 7 });
  });

  it("exposes an accessible progress label", () => {
    const entity = constructionSiteEntityFromProcess(process({ progress: 0.4 }), SPACE);
    expect(entity).not.toBeNull();

    expect(entity!.process?.accessibleLabel).toMatch(/40%/);
    expect(entity!.label).toMatch(/40%/);
  });
});
