import { describe, expect, it } from "vitest";
import { Camera } from "../../src/map-engine/Camera";
import { hitTest } from "../../src/map-engine/hitTest";
import type { AuthoritativeEntity } from "../../src/map-engine/types";

const VIEWPORT = { width: 200, height: 200 };

function npc(id: string, x: number, y: number): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space: { kind: "World" } },
    position: { x, y },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#000",
  };
}

describe("hitTest", () => {
  it("hits the entity under the cursor at a low zoom level (scale=2)", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 2 }, VIEWPORT);
    const entity = npc("npc-1", 50, 50); // projeta exatamente no centro da tela
    const screenPoint = camera.worldToScreen(entity.position);

    const hit = hitTest(screenPoint, camera, [entity], 8);

    expect(hit).toEqual(entity.ref);
  });

  it("hits the entity under the cursor at a high zoom level (scale=8)", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 8 }, VIEWPORT);
    const entity = npc("npc-1", 50, 50);
    const screenPoint = camera.worldToScreen(entity.position);

    const hit = hitTest(screenPoint, camera, [entity], 8);

    expect(hit).toEqual(entity.ref);
  });

  it("returns null when clicking empty space", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 2 }, VIEWPORT);
    const entity = npc("npc-1", 50, 50);

    const hit = hitTest({ x: 0, y: 0 }, camera, [entity], 8);

    expect(hit).toBeNull();
  });

  it("returns the closest entity when two are within the hit radius", () => {
    const camera = new Camera({ center: { x: 50, y: 50 }, scale: 2 }, VIEWPORT);
    const near = npc("near", 50, 50);
    const far = npc("far", 52, 50); // mais longe do centro da tela, mesmo raio de acerto

    const hit = hitTest(camera.worldToScreen({ x: 50, y: 50 }), camera, [far, near], 20);

    expect(hit).toEqual(near.ref);
  });
});
