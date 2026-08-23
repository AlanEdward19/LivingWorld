import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it, vi } from "vitest";
import { draw, type RenderFrame } from "../../src/map-engine/renderer";
import type { AuthoritativeEntity, CameraState } from "../../src/map-engine/types";
import { animationSpecForAction, animationSpecForProcess } from "../../src/map-engine/npcAnimationCatalog";
import { progressRingEndAngle } from "../../src/map-engine/npcAnimationCue";

const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "../../src/styles/global.css"), "utf8");

function fakeCtx(canvas: { width: number; height: number }) {
  return {
    canvas,
    fillStyle: "",
    strokeStyle: "",
    lineWidth: 1,
    shadowColor: "",
    shadowBlur: 0,
    globalAlpha: 1,
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
    quadraticCurveTo: vi.fn(),
    setLineDash: vi.fn(),
    save: vi.fn(),
    restore: vi.fn(),
    translate: vi.fn(),
    rotate: vi.fn(),
  } as unknown as CanvasRenderingContext2D & {
    fillRect: ReturnType<typeof vi.fn>;
    arc: ReturnType<typeof vi.fn>;
    fillText: ReturnType<typeof vi.fn>;
    drawImage: ReturnType<typeof vi.fn>;
  };
}

function npc(id: string, overrides: Partial<AuthoritativeEntity> = {}): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space: { kind: "City", cityId: "city-a" } },
    position: { x: 5, y: 5 },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#abc",
    ...overrides,
  };
}

function frame(entities: AuthoritativeEntity[]): RenderFrame {
  const camera: CameraState = { center: { x: 5.5, y: 5.5 }, scale: 20 };
  return {
    camera,
    cells: { width: 1000, height: 1000, colorAt: () => "#222" },
    layers: [],
    entities,
    lodThresholds: { aggregate: 4, token: 10, detail: 18 },
  };
}

function readyImage() {
  class ReadyImage {
    complete = true;
    naturalWidth = 100;
    src = "";
  }
  vi.stubGlobal("Image", ReadyImage);
}

function ringEndAngles(ctx: { arc: ReturnType<typeof vi.fn> }): number[] {
  return ctx.arc.mock.calls
    .map((args) => args[4] as number)
    .filter((end) => end !== Math.PI * 2);
}

describe("work & craft animations (T24 / LWV-07.2)", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("draws an animated Work cue from the unified catalog", () => {
    readyImage();
    const spec = animationSpecForAction(2);
    expect(spec.animated).toBe(true);
    expect(spec.keyframes).toBe("npc-work-hammer");
    expect(spec.durationMs).toBeGreaterThan(0);
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, frame([npc("worker", { currentAction: 2 })]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(1);
    expect(css).toContain(`@keyframes ${spec.keyframes}`);
  });

  it("drives the cook-food progress ring from ProcessVisual.progress (low)", () => {
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });
    const entity = npc("cook", {
      currentAction: 2,
      process: { kind: "food", progress: 0.25, accessibleLabel: "Cozinhando, 25%", descriptorKey: "cook-food" },
    });

    draw(ctx, frame([entity]));

    expect(ringEndAngles(ctx)).toContain(progressRingEndAngle(0.25));
    expect(animationSpecForProcess("cook-food").animated).toBe(true);
  });

  it("drives the cook-food progress ring from ProcessVisual.progress (high)", () => {
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, frame([npc("cook", {
      process: { kind: "food", progress: 0.9, accessibleLabel: "Cozinhando, 90%", descriptorKey: "cook-food" },
    })]));

    expect(ringEndAngles(ctx)).toContain(progressRingEndAngle(0.9));
    expect(ringEndAngles(ctx)).not.toContain(progressRingEndAngle(0.25));
  });

  it("scales the construction scaffold bar with ProcessVisual.progress", () => {
    const low = fakeCtx({ width: 400, height: 400 });
    const high = fakeCtx({ width: 400, height: 400 });
    const site = (progress: number): AuthoritativeEntity => ({
      ref: { kind: "building", id: `construction:${progress}`, space: { kind: "City", cityId: "a" } },
      position: { x: 5, y: 5 },
      size: { w: 2, h: 2 },
      sizeIsDerived: true,
      color: "#8a6a3a",
      process: { kind: "construction", progress, accessibleLabel: `Obra ${progress}` },
    });

    draw(low, frame([site(0.2)]));
    draw(high, frame([site(0.8)]));

    const filledBar = (ctx: { fillRect: ReturnType<typeof vi.fn> }) => {
      const bars = ctx.fillRect.mock.calls.filter((args) => (args[3] as number) < 8);
      const track = Math.max(...bars.map((args) => args[2] as number));
      return Math.max(...bars.map((args) => args[2] as number).filter((width) => width < track - 0.01));
    };
    expect(filledBar(high)).toBeGreaterThan(filledBar(low));
    expect(animationSpecForProcess("construction").keyframes).toBe("npc-build-scaffold");
    expect(css).toContain("@keyframes npc-build-scaffold");
  });

  it("draws collect-water, carry-water, and deliver-water cues", () => {
    readyImage();
    for (const descriptorKey of ["collect-water", "carry-water", "deliver-water"] as const) {
      const spec = animationSpecForProcess(descriptorKey);
      expect(spec.animated).toBe(true);
      expect(spec.keyframes).not.toBe("none");
      expect(css).toContain(`@keyframes ${spec.keyframes}`);
      const ctx = fakeCtx({ width: 400, height: 400 });
      draw(ctx, frame([npc(descriptorKey, {
        process: { kind: "water", progress: 0.4, accessibleLabel: spec.a11yLabel, descriptorKey },
      })]));
      expect(ctx.arc.mock.calls.length, descriptorKey).toBeGreaterThanOrEqual(1);
    }
  });

  it("does not mutate motor process progress when drawing the staged cue", () => {
    readyImage();
    const process = { kind: "food", progress: 0.5, accessibleLabel: "Cozinhando, 50%", descriptorKey: "cook-food" };
    const entity = npc("cook", { process });
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, frame([entity]));

    expect(entity.process?.progress).toBe(0.5);
    expect(process.progress).toBe(0.5);
  });

  it("keeps the work/craft cue visible under prefers-reduced-motion", () => {
    readyImage();
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, frame([npc("cook", {
      currentAction: 2,
      process: { kind: "food", progress: 0.6, accessibleLabel: "Cozinhando, 60%", descriptorKey: "cook-food" },
    })]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(1);
    expect(ringEndAngles(ctx)).toContain(progressRingEndAngle(0.6));
  });
});
