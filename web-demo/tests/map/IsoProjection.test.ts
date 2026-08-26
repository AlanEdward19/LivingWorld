import { describe, expect, it } from "vitest";
import { toGrid, toScreen } from "../../src/map/IsoProjection";

const TILE_W = 64;
const TILE_H = 32;

function expectRoundTrip(gridX: number, gridY: number) {
  const screen = toScreen(gridX, gridY, TILE_W, TILE_H);
  const back = toGrid(screen.x, screen.y, TILE_W, TILE_H);
  expect(back.x).toBeCloseTo(gridX, 10);
  expect(back.y).toBeCloseTo(gridY, 10);
}

describe("IsoProjection (top-down ortogonal, AD-019)", () => {
  it("round-trips the origin (0, 0)", () => {
    expectRoundTrip(0, 0);
  });

  it("round-trips an interior point (5, 5)", () => {
    expectRoundTrip(5, 5);
  });

  it("round-trips negative grid coordinates", () => {
    expectRoundTrip(-3, -7);
  });

  it("round-trips fractional tile sizes", () => {
    const screen = toScreen(4, 2, 48.5, 24.25);
    const back = toGrid(screen.x, screen.y, 48.5, 24.25);
    expect(back.x).toBeCloseTo(4, 10);
    expect(back.y).toBeCloseTo(2, 10);
  });

  it("projects a known point to the exact expected screen coordinates (identity scale, not isometric)", () => {
    expect(toScreen(1, 0, TILE_W, TILE_H)).toEqual({ x: 64, y: 0 });
    expect(toScreen(2, 3, TILE_W, TILE_H)).toEqual({ x: 128, y: 96 });
  });

  it("moving along Y only changes screen Y — no isometric X shear", () => {
    const a = toScreen(2, 1, TILE_W, TILE_H);
    const b = toScreen(2, 5, TILE_W, TILE_H);
    expect(b.x).toBe(a.x);
    expect(b.y).toBeGreaterThan(a.y);
  });
});
