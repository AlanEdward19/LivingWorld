import { afterEach, describe, expect, it, vi } from "vitest";
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
    closePath: vi.fn(),
    arc: vi.fn(),
    fill: vi.fn(),
    stroke: vi.fn(),
    fillText: vi.fn(),
    drawImage: vi.fn(),
    setLineDash: vi.fn(),
    save: vi.fn(),
    restore: vi.fn(),
    translate: vi.fn(),
    rotate: vi.fn(),
  } as unknown as CanvasRenderingContext2D & {
    fillRect: ReturnType<typeof vi.fn>;
    strokeRect: ReturnType<typeof vi.fn>;
    arc: ReturnType<typeof vi.fn>;
    setLineDash: ReturnType<typeof vi.fn>;
    fillText: ReturnType<typeof vi.fn>;
    drawImage: ReturnType<typeof vi.fn>;
    rotate: ReturnType<typeof vi.fn>;
  };
}

function npc(
  id: string,
  x: number,
  y: number,
  sizeIsDerived = false,
  space: AuthoritativeEntity["ref"]["space"] = { kind: "World" },
): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space },
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
  afterEach(() => vi.unstubAllGlobals());

  it("draws the deterministic SVG pawn at token LOD when its cached image is ready", () => {
    class ReadyImage {
      complete = true;
      naturalWidth = 100;
      src = "";
    }
    vi.stubGlobal("Image", ReadyImage);
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame({ center: { x: 2, y: 2 }, scale: 12 }, [npc("npc-svg-ready", 2, 2)]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
  });

  it("renders a river as a water tile instead of a tiny point", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const frame = baseFrame({ center: { x: 2, y: 2 }, scale: 10 });
    frame.cells = { width: 5, height: 5, colorAt: () => undefined };
    frame.layers = [{ id: "Rivers", overlayPoints: [{ x: 2, y: 2, color: "#3a7bd5" }] }];

    draw(ctx, frame);

    expect(ctx.fillRect.mock.calls.some(([, , width, height]) => width === 10 && height === 10)).toBe(true);
  });

  it("draws stable cloud puffs when the space enables atmosphere", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const frame = baseFrame({ center: { x: 2, y: 2 }, scale: 10 });
    frame.cells = { width: 5, height: 5, atmosphereSeed: "world", colorAt: () => undefined };

    draw(ctx, frame);

    expect(ctx.arc).toHaveBeenCalledTimes(15);
  });

  it("does not draw cell grid lines when the current space disables its grid", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const frame = baseFrame({ center: { x: 2, y: 2 }, scale: 12 });
    frame.cells = { width: 5, height: 5, showGrid: false, colorAt: () => "#567" };

    draw(ctx, frame);

    expect((ctx.stroke as unknown as ReturnType<typeof vi.fn>)).not.toHaveBeenCalled();
  });

  it("adds distinct top-down details to roof and door materials", () => {
    const ctx = fakeCtx({ width: 100, height: 100 });
    const building: AuthoritativeEntity = {
      ref: { kind: "building", id: "house", space: { kind: "City", cityId: "a" } },
      position: { x: 1, y: 1 },
      size: { w: 2, h: 1 },
      sizeIsDerived: true,
      color: "#765",
      footprintCells: [
        { x: 0, y: 0, color: "#765", material: "roof" },
        { x: 1, y: 0, color: "#432", material: "door" },
      ],
    };

    draw(ctx, baseFrame({ center: { x: 2, y: 2 }, scale: 12 }, [building]));

    expect(ctx.fillRect.mock.calls.length).toBeGreaterThanOrEqual(5);
    expect(ctx.arc).toHaveBeenCalledOnce();
  });

  it("renders a city as a composed settlement instead of outlining every footprint tile", () => {
    const ctx = fakeCtx({ width: 300, height: 300 });
    const city: AuthoritativeEntity = {
      ref: { kind: "city", id: "city-a", space: { kind: "World" } },
      position: { x: 1, y: 1 }, size: { w: 10, h: 8 }, sizeIsDerived: false, color: "#999",
      footprintCells: Array.from({ length: 80 }, (_, index) => ({
        x: index % 10, y: Math.floor(index / 10), color: "#765", material: "roof" as const,
      })),
    };

    draw(ctx, baseFrame({ center: { x: 6, y: 5 }, scale: 12 }, [city]));

    expect(ctx.strokeRect.mock.calls.length).toBe(0);
    expect(ctx.arc.mock.calls.length).toBe(4);
  });

  it("renders an authoring settlement without the outer wall markers", () => {
    const ctx = fakeCtx({ width: 300, height: 300 });
    const city: AuthoritativeEntity = {
      ref: { kind: "city", id: "draft-city", space: { kind: "World" } },
      position: { x: 1, y: 1 }, size: { w: 4, h: 4 }, sizeIsDerived: false, color: "#999",
      showBoundary: false,
    };

    draw(ctx, baseFrame({ center: { x: 3, y: 3 }, scale: 24 }, [city]));

    expect(ctx.arc).not.toHaveBeenCalled();
    expect(ctx.strokeRect).not.toHaveBeenCalled();
  });

  it("rotates creator architecture around its visual center", () => {
    const ctx = fakeCtx({ width: 300, height: 300 });
    const city: AuthoritativeEntity = {
      ref: { kind: "city", id: "rotated-city", space: { kind: "World" } },
      position: { x: 1, y: 1 }, size: { w: 4, h: 4 }, sizeIsDerived: false, color: "#999",
      showBoundary: false, rotation: 90,
    };

    draw(ctx, baseFrame({ center: { x: 3, y: 3 }, scale: 24 }, [city]));

    expect(ctx.rotate).toHaveBeenCalledWith(Math.PI / 2);
  });

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

  it("caps an NPC token at ten screen pixels even under extreme zoom", () => {
    class PendingImage {
      complete = false;
      naturalWidth = 0;
      src = "";
    }
    vi.stubGlobal("Image", PendingImage);
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 100 }, [npc("tiny-person", 5, 5)]));

    expect(ctx.arc.mock.calls[0]?.[2]).toBe(10);
  });

  it("renders NPCs progressively larger in city and building spaces without changing world scale", () => {
    class PendingImage {
      complete = false;
      naturalWidth = 0;
      src = "";
    }
    vi.stubGlobal("Image", PendingImage);

    const radiusFor = (entity: AuthoritativeEntity) => {
      const ctx = fakeCtx({ width: 400, height: 400 });
      draw(ctx, baseFrame({ center: { x: 5.5, y: 5.5 }, scale: 20 }, [entity]));
      return ctx.arc.mock.calls[0]?.[2] as number;
    };

    const worldRadius = radiusFor(npc("world-person", 5, 5));
    const cityRadius = radiusFor(npc("city-person", 5, 5, false, { kind: "City", cityId: "city-a" }));
    const buildingRadius = radiusFor(npc("home-person", 5, 5, false, {
      kind: "Building", buildingId: "home-a", cityId: "city-a",
    }));

    expect(worldRadius).toBe(5);
    expect(cityRadius).toBeGreaterThan(worldRadius);
    expect(buildingRadius).toBeGreaterThan(cityRadius);
  });

  it("culls entities outside the visible rect from drawing", () => {
    const camera: CameraState = { center: { x: 5, y: 5 }, scale: 10 };
    const inView = npc("in", 5, 5);
    const farAway = npc("far", 900, 900);
    const ctx = fakeCtx({ width: 100, height: 100 });

    draw(ctx, baseFrame(camera, [inView, farAway]));

    // token (scale 10 >= threshold): disco + glifo (cabeça + ombros) da entidade visível = 3
    // arcs; a distante é cullada antes de chegar em drawPointEntity.
    expect(ctx.arc.mock.calls.length).toBe(3);
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
