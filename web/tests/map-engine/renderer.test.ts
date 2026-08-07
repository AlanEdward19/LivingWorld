import { describe, expect, it, vi } from "vitest";
import { draw, type RenderFrame } from "../../src/map-engine/renderer";
import type { AuthoritativeEntity, CameraState } from "../../src/map-engine/types";
import type { LodThresholds } from "../../src/map-engine/lod";

const THRESHOLDS: LodThresholds = { aggregate: 4, token: 10, detail: 18 };

function fakeCtx(canvas: { width: number; height: number }) {
  return {
    canvas,
    fillStyle: "",
    strokeStyle: "",
    lineWidth: 1,
    shadowColor: "",
    shadowBlur: 0,
    font: "",
    textAlign: "left",
    fillRect: vi.fn(),
    strokeRect: vi.fn(),
    beginPath: vi.fn(),
    moveTo: vi.fn(),
    lineTo: vi.fn(),
    arc: vi.fn(),
    fill: vi.fn(),
    stroke: vi.fn(),
    fillText: vi.fn(),
    setLineDash: vi.fn(),
  } as unknown as CanvasRenderingContext2D & {
    fillRect: ReturnType<typeof vi.fn>;
    strokeRect: ReturnType<typeof vi.fn>;
    arc: ReturnType<typeof vi.fn>;
    setLineDash: ReturnType<typeof vi.fn>;
    fillText: ReturnType<typeof vi.fn>;
  };
}

function npc(id: string, x: number, y: number, sizeIsDerived = false): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space: { kind: "World" } },
    position: { x, y },
    size: { w: 1, h: 1 },
    sizeIsDerived,
    color: "#abc",
  };
}

function baseFrame(camera: CameraState, entities: AuthoritativeEntity[] = []): RenderFrame {
  return {
    camera,
    cells: { width: 1000, height: 1000, colorAt: () => "#222" },
    layers: [],
    entities,
    lodThresholds: THRESHOLDS,
  };
}

describe("renderer.draw", () => {
  it("only fills the cells covered by the camera's visible rect, not the whole 1000x1000 grid", () => {
    // scale=10 px/tile, viewport 100x100px -> visible world rect é ~10x10 tiles -> ~100 fillRect
    const camera: CameraState = { center: { x: 500, y: 500 }, scale: 10 };
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame(camera));

    // 1 fillRect de fundo + no máximo 10*10=100 de célula — nunca perto de 1_000_000
    expect(ctx.fillRect.mock.calls.length).toBeGreaterThan(1);
    expect(ctx.fillRect.mock.calls.length).toBeLessThan(200);
  });

  it("returns early without touching the context when ctx is null (jsdom getContext('2d'))", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 10 };
    expect(() => draw(null, baseFrame(camera))).not.toThrow();
  });

  it("never reassigns canvas.width/height — sizing belongs to whoever mounts the canvas", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 10 };
    const canvas = { width: 333, height: 222 };
    let widthWrites = 0;
    Object.defineProperty(canvas, "width", {
      get: () => 333,
      set: () => {
        widthWrites += 1;
      },
    });
    const ctx = fakeCtx(canvas);

    draw(ctx, baseFrame(camera));

    expect(widthWrites).toBe(0);
  });

  it("marks a derived-size entity with a distinct (dashed) stroke, unlike an authored one", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 12 }; // >= token threshold
    const derived = npc("derived", 5, 5, true);
    const authored = npc("authored", 6, 5, false);
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, baseFrame(camera, [derived, authored]));

    const dashCalls = ctx.setLineDash.mock.calls.map((args: unknown[]) => args[0] as number[]);
    expect(dashCalls.some((pattern) => pattern.length > 0)).toBe(true); // o derivado usou tracejado
    expect(dashCalls.some((pattern) => pattern.length === 0)).toBe(true); // o autorado usou traço sólido
  });

  it("culls entities outside the visible rect from drawing", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 10 };
    const inView = npc("in", 5, 5);
    const farAway = npc("far", 900, 900);
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame(camera, [inView, farAway]));

    // 1 arc só pela entidade visível (dot, sem token/anel nesse zoom => 1 arc por entidade)
    expect(ctx.arc.mock.calls.length).toBe(1);
  });

  it("aggregates entities into clusters below the aggregate threshold instead of drawing each one", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 2 }; // < THRESHOLDS.aggregate (4)
    const entities = [npc("a", 5, 5), npc("b", 5, 5), npc("c", 6, 5)];
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame(camera, entities));

    // 3 entidades agregadas em no máximo 2 buckets -> bem menos arcs que 3 desenhos individuais
    expect(ctx.arc.mock.calls.length).toBeLessThan(entities.length);
    expect(ctx.arc.mock.calls.length).toBeGreaterThan(0);
  });
});
