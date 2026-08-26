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

describe("IsoProjection", () => {
  it("round-trips the origin (0, 0)", () => {
    expectRoundTrip(0, 0);
  });

  it("round-trips an interior point (5, 5)", () => {
    expectRoundTrip(5, 5);
  });

  it("round-trips the top-left grid corner (0, 19)", () => {
    expectRoundTrip(0, 19);
  });

  it("round-trips the top-right grid corner (19, 0)", () => {
    expectRoundTrip(19, 0);
  });

  it("round-trips the bottom-right grid corner (19, 19)", () => {
    expectRoundTrip(19, 19);
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

  it("projects a known point to the exact expected screen coordinates", () => {
    // gridX=1, gridY=0 → topo do diamante: x desloca meia largura, y desloca meia altura
    expect(toScreen(1, 0, TILE_W, TILE_H)).toEqual({ x: 32, y: 16 });
  });

  it("raises the screen Y for taller blocks without affecting X", () => {
    const ground = toScreen(2, 2, TILE_W, TILE_H, 0);
    const raised = toScreen(2, 2, TILE_W, TILE_H, 3);
    expect(raised.x).toBe(ground.x);
    expect(raised.y).toBe(ground.y - 3 * TILE_H);
  });

  it("does not require height to round-trip through toGrid (height is a render-only offset)", () => {
    // Um bloco alto em (2,2) pode visualmente sobrepor um bloco baixo em (2,3) na tela —
    // toGrid não recebe height, então quem faz hit-test precisa desfazer o offset de height
    // antes de chamar toGrid (IsoTileRenderer, T8). Aqui confirmamos que, sem esse offset
    // (height=0 na ida), o round-trip continua exato mesmo perto de outro bloco alto.
    expectRoundTrip(2, 2);
    expectRoundTrip(2, 3);
  });
});
