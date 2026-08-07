import { describe, expect, it } from "vitest";
import { Camera } from "../../src/map-engine/Camera";
import { computeFitZoom } from "../../src/gridFit";

const VIEWPORT = { width: 200, height: 200 };

function camera(center = { x: 50, y: 50 }, scale = 2, viewport = VIEWPORT) {
  return new Camera({ center, scale }, viewport);
}

describe("Camera", () => {
  it("worldToScreen and screenToWorld are exact inverses", () => {
    const c = camera();
    const p = { x: 30, y: 70 };
    const roundTripped = c.screenToWorld(c.worldToScreen(p));

    expect(roundTripped.x).toBeCloseTo(p.x);
    expect(roundTripped.y).toBeCloseTo(p.y);
  });

  it("zoomAt keeps screenToWorld(point) invariant across 3 points and 2 factors", () => {
    const points = [
      { x: 0, y: 0 },
      { x: 100, y: 100 },
      { x: 150, y: 40 },
    ];
    const factors = [1.5, 3];

    for (const point of points) {
      for (const factor of factors) {
        const c = camera();
        const worldBefore = c.screenToWorld(point);
        c.zoomAt(point, factor);
        const worldAfter = c.screenToWorld(point);

        expect(worldAfter.x).toBeCloseTo(worldBefore.x, 9);
        expect(worldAfter.y).toBeCloseTo(worldBefore.y, 9);
      }
    }
  });

  it("zoomAt multiplies the scale by the given factor", () => {
    const c = camera(undefined, 2);
    c.zoomAt({ x: 0, y: 0 }, 3);

    expect(c.snapshot().scale).toBe(6);
  });

  it("zoomAt rejects a non-positive factor", () => {
    const c = camera();

    expect(() => c.zoomAt({ x: 0, y: 0 }, 0)).toThrow();
    expect(() => c.zoomAt({ x: 0, y: 0 }, -1)).toThrow();
  });

  it("panBy shifts the camera center opposite to the screen drag direction", () => {
    const c = camera({ x: 50, y: 50 }, 1);
    c.panBy({ x: 10, y: 0 });

    expect(c.snapshot().center).toEqual({ x: 40, y: 50 });
  });

  it("panBy scales the world shift by 1/scale", () => {
    const c = camera({ x: 50, y: 50 }, 2);
    c.panBy({ x: 10, y: 0 });

    expect(c.snapshot().center.x).toBe(45);
  });

  it("clampTo clamps the center within bounds when the space is larger than the viewport", () => {
    const c = camera({ x: -1000, y: -1000 }, 2); // halfW = halfH = 50
    c.clampTo({ width: 300, height: 300 });

    expect(c.snapshot().center).toEqual({ x: 50, y: 50 });

    const c2 = camera({ x: 9999, y: 9999 }, 2);
    c2.clampTo({ width: 300, height: 300 });
    expect(c2.snapshot().center).toEqual({ x: 250, y: 250 });
  });

  it("clampTo centers the axis when the space is smaller than the viewport", () => {
    const c = camera({ x: 999, y: 999 }, 2); // halfW = halfH = 50
    c.clampTo({ width: 50, height: 50 });

    expect(c.snapshot().center).toEqual({ x: 25, y: 25 });
  });

  it("visibleWorldRect returns exactly the world rect covered by the viewport", () => {
    const c = camera({ x: 50, y: 50 }, 2);

    expect(c.visibleWorldRect()).toEqual({ x: 0, y: 0, width: 100, height: 100 });
  });

  it("snapshot/restore round-trips exactly, discarding intermediate mutations", () => {
    const c = camera({ x: 50, y: 50 }, 2);
    const saved = c.snapshot();

    c.panBy({ x: 20, y: 5 });
    c.zoomAt({ x: 0, y: 0 }, 4);
    c.restore(saved);

    expect(c.snapshot()).toEqual(saved);
  });

  it("Camera.initial derives the initial scale from computeFitZoom and centers the grid", () => {
    const initial = Camera.initial(100, 50, { width: 400, height: 400 });

    expect(initial.scale).toBe(computeFitZoom(100, 50, 400, 400));
    expect(initial.center).toEqual({ x: 50, y: 25 });
  });
});
