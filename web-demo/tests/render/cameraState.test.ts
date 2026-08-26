import { describe, expect, it } from "vitest";
import { DEFAULT_ZOOM, FOCUS_ZOOM, MAX_ZOOM, MIN_ZOOM, focusOn, initialCamera, panBy, unfocus, zoomBy } from "../../src/render/cameraState";

describe("initialCamera", () => {
  it("starts at the given center, default zoom, unfocused", () => {
    expect(initialCamera(10, 20)).toEqual({ x: 10, y: 20, zoom: DEFAULT_ZOOM, focusBuildingId: null });
  });
});

describe("zoomBy", () => {
  it("multiplies the zoom by the given factor", () => {
    const state = initialCamera(0, 0);
    expect(zoomBy(state, 2).zoom).toBeCloseTo(DEFAULT_ZOOM * 2, 5);
  });

  it("clamps to MAX_ZOOM on the way up", () => {
    const state = initialCamera(0, 0);
    expect(zoomBy(state, 999).zoom).toBe(MAX_ZOOM);
  });

  it("clamps to MIN_ZOOM on the way down", () => {
    const state = initialCamera(0, 0);
    expect(zoomBy(state, 0.0001).zoom).toBe(MIN_ZOOM);
  });

  it("never changes x/y/focusBuildingId", () => {
    const state = { x: 5, y: 7, zoom: 1, focusBuildingId: "bld-x" };
    const zoomed = zoomBy(state, 1.5);
    expect(zoomed.x).toBe(5);
    expect(zoomed.y).toBe(7);
    expect(zoomed.focusBuildingId).toBe("bld-x");
  });
});

describe("panBy", () => {
  it("shifts x/y by the given world-space delta, without touching zoom/focus", () => {
    const state = { x: 10, y: 10, zoom: 2, focusBuildingId: null };
    expect(panBy(state, 3, -4)).toEqual({ x: 7, y: 14, zoom: 2, focusBuildingId: null });
  });
});

describe("focusOn / unfocus", () => {
  it("focusOn centers the camera on the target and jumps to FOCUS_ZOOM", () => {
    const state = initialCamera(0, 0);
    expect(focusOn(state, "bld-bakery", 100, 200)).toEqual({ x: 100, y: 200, zoom: FOCUS_ZOOM, focusBuildingId: "bld-bakery" });
  });

  it("unfocus returns to the settlement center at DEFAULT_ZOOM and clears focusBuildingId", () => {
    const focused = focusOn(initialCamera(0, 0), "bld-bakery", 100, 200);
    expect(unfocus(focused, 0, 0)).toEqual({ x: 0, y: 0, zoom: DEFAULT_ZOOM, focusBuildingId: null });
  });
});
