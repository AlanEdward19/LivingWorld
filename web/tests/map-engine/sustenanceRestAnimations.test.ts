import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it, vi } from "vitest";
import { draw, type RenderFrame } from "../../src/map-engine/renderer";
import type { AuthoritativeEntity, CameraState } from "../../src/map-engine/types";
import { animationSpecForAction, animationSpecForProcess } from "../../src/map-engine/npcAnimationCatalog";
import { cueFromSpec, progressRingEndAngle } from "../../src/map-engine/npcAnimationCue";

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

describe("sustenance & rest animations (T27 / LWV-07.1/2/4)", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("extends the sleep Zzz keyframes to every rest kind, including Idle/rest", () => {
    const restKinds = [
      animationSpecForAction(1),
      animationSpecForAction(5),
      animationSpecForProcess("sleep-ground"),
      animationSpecForProcess("sleep-dwelling"),
      animationSpecForProcess("sleep-bed"),
    ];
    for (const spec of restKinds) {
      expect(spec.keyframes).toBe("npc-rest-zzz");
      expect(spec.durationMs).toBe(1800);
      expect(spec.animated).toBe(true);
      expect(spec.hidden).toBe(false);
      expect(spec.reducedMotionFallback).toBe("static-icon");
    }
    expect(css).toContain("@keyframes npc-rest-zzz");
    expect(css).toContain(".npc-anim-rest");
  });

  it("draws an animated Eat cue from the unified catalog", () => {
    readyImage();
    const spec = animationSpecForAction(0);
    expect(spec.animated).toBe(true);
    expect(spec.keyframes).toBe("npc-eat-bite");
    expect(spec.durationMs).toBeGreaterThan(0);
    expect(spec.a11yLabel).toBe("Comendo");
    expect(css).toContain(`@keyframes ${spec.keyframes}`);

    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([npc("eater", { currentAction: 0 })]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
  });

  it("keeps eat-raw and eat-prepared as distinct animated food cues", () => {
    readyImage();
    const raw = animationSpecForProcess("eat-raw");
    const prepared = animationSpecForProcess("eat-prepared");
    expect(raw.a11yLabel).toBe("Comendo cru");
    expect(prepared.a11yLabel).toBe("Comendo refeição");
    expect(raw.a11yLabel).not.toBe(prepared.a11yLabel);
    expect(raw.keyframes).toBe("npc-eat-bite");
    expect(prepared.keyframes).toBe("npc-eat-bite");
    expect(raw.animated).toBe(true);
    expect(prepared.animated).toBe(true);

    for (const descriptorKey of ["eat-raw", "eat-prepared"] as const) {
      const spec = animationSpecForProcess(descriptorKey);
      const ctx = fakeCtx({ width: 400, height: 400 });
      draw(ctx, frame([npc(descriptorKey, {
        process: { kind: "food", progress: 0.4, accessibleLabel: spec.a11yLabel, descriptorKey },
      })]));
      expect(ctx.arc.mock.calls.length, descriptorKey).toBeGreaterThanOrEqual(1);
    }
  });

  it("drives the eat-prepared progress ring from ProcessVisual.progress", () => {
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });

    draw(ctx, frame([npc("eater", {
      process: { kind: "food", progress: 0.7, accessibleLabel: "Comendo refeição", descriptorKey: "eat-prepared" },
    })]));

    expect(ringEndAngles(ctx)).toContain(progressRingEndAngle(0.7));
    expect(animationSpecForProcess("eat-prepared").animated).toBe(true);
  });

  it("animates plant-crop, water-crop, and harvest-crop staged cues", () => {
    readyImage();
    const expected: Record<string, string> = {
      "plant-crop": "npc-crop-plant",
      "water-crop": "npc-crop-water",
      "harvest-crop": "npc-crop-harvest",
    };
    for (const [descriptorKey, keyframes] of Object.entries(expected)) {
      const spec = animationSpecForProcess(descriptorKey);
      expect(spec.animated).toBe(true);
      expect(spec.keyframes).toBe(keyframes);
      expect(spec.durationMs).toBeGreaterThan(0);
      expect(css).toContain(`@keyframes ${keyframes}`);
      const ctx = fakeCtx({ width: 400, height: 400 });
      draw(ctx, frame([npc(descriptorKey, {
        process: { kind: "crop", progress: 0.5, accessibleLabel: spec.a11yLabel, descriptorKey },
      })]));
      expect(ctx.arc.mock.calls.length, descriptorKey).toBeGreaterThanOrEqual(1);
    }
  });

  it("keeps eat and rest cues visible under prefers-reduced-motion without hiding the icon", () => {
    readyImage();
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    const eat = animationSpecForAction(0);
    const rest = animationSpecForAction(5);
    expect(cueFromSpec(eat, { reducedMotion: true }).motion).toBe(false);
    expect(cueFromSpec(eat, { reducedMotion: true }).opacity).toBe(1);
    expect(cueFromSpec(rest, { reducedMotion: true }).motion).toBe(false);
    expect(cueFromSpec(rest, { reducedMotion: true }).opacity).toBe(1);
    expect(css).toMatch(/prefers-reduced-motion[\s\S]*\.npc-anim-eat[\s\S]*animation:\s*none/);
    expect(css).toMatch(/prefers-reduced-motion[\s\S]*\.npc-anim-rest[\s\S]*animation:\s*none/);

    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([npc("resting", {
      currentAction: 5,
      process: { kind: "food", progress: 0.3, accessibleLabel: "Comendo cru", descriptorKey: "eat-raw" },
    })]));

    expect(ctx.drawImage).toHaveBeenCalledOnce();
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(1);
    expect(ringEndAngles(ctx)).toContain(progressRingEndAngle(0.3));
  });
});
