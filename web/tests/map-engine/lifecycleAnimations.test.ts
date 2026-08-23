import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it, vi } from "vitest";
import { draw, type RenderFrame } from "../../src/map-engine/renderer";
import type { AuthoritativeEntity, CameraState } from "../../src/map-engine/types";
import { animationSpecForEvent } from "../../src/map-engine/npcAnimationCatalog";
import { LIFECYCLE_EVENT_KINDS, resolveLifecycleMoments } from "../../src/map-engine/lifecycleMoments";

const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "../../src/styles/global.css"), "utf8");

const LABELS: Record<number, string> = {
  0: "Um novo habitante nasceu",
  1: "Um habitante faleceu",
  2: "A fome causou uma morte",
  13: "Uma mãe faleceu durante o parto",
  14: "Uma gestação terminou sem nascimento vivo",
};

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
    ellipse: vi.fn(),
    setLineDash: vi.fn(),
    save: vi.fn(),
    restore: vi.fn(),
    translate: vi.fn(),
    rotate: vi.fn(),
  } as unknown as CanvasRenderingContext2D & {
    arc: ReturnType<typeof vi.fn>;
    fillStyle: string;
    strokeStyle: string;
    drawImage: ReturnType<typeof vi.fn>;
  };
}

function npc(id: string, x = 5, y = 5): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space: { kind: "City", cityId: "city-a" } },
    position: { x, y },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#abc",
    currentAction: 4,
  };
}

function frame(
  entities: AuthoritativeEntity[],
  events: { kind: number; location?: { x: number; y: number } }[] = [],
): RenderFrame {
  const camera: CameraState = { center: { x: 5.5, y: 5.5 }, scale: 20 };
  return {
    camera,
    cells: { width: 1000, height: 1000, colorAt: () => "#222" },
    layers: [],
    entities,
    events,
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

describe("life-cycle animations (T26 / LWV-07.3)", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("maps Birth, Death, Starvation, MaternalDeath, and StillBirth to timed audience-safe bursts", () => {
    expect([...LIFECYCLE_EVENT_KINDS]).toEqual([0, 1, 2, 13, 14]);
    for (const kind of LIFECYCLE_EVENT_KINDS) {
      const spec = animationSpecForEvent(kind);
      expect(spec.a11yLabel).toBe(LABELS[kind]);
      expect(spec.animated).toBe(true);
      expect(spec.durationMs).toBeGreaterThan(0);
      expect(spec.keyframes).not.toBe("none");
      expect(spec.a11yLabel.toLowerCase()).not.toMatch(/sangue|gore|cadáver|osso|corpse|blood/);
      expect(css).toContain(`@keyframes ${spec.keyframes}`);
    }
  });

  it("places a Birth burst at the event location, not at an unrelated NPC", () => {
    const moments = resolveLifecycleMoments(
      [{ kind: 0, location: { x: 8, y: 5 } }],
      [npc("elsewhere", 2, 2)],
    );
    expect(moments).toEqual([{ kind: 0, position: { x: 8, y: 5 } }]);
    expect(moments[0]?.position).not.toEqual({ x: 2, y: 2 });
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([npc("elsewhere", 2, 2)], [{ kind: 0, location: { x: 8, y: 5 } }]));
    const xs = ctx.arc.mock.calls.map((args) => args[0] as number);
    expect(xs.some((x) => x > 220)).toBe(true);
  });

  it("falls back to the first NPC only when the event has no cell", () => {
    const moments = resolveLifecycleMoments([{ kind: 1 }], [npc("only", 2, 2)]);
    expect(moments).toEqual([{ kind: 1, position: { x: 2, y: 2 } }]);
  });

  it("places Death, Starvation, MaternalDeath, and StillBirth bursts at their locations", () => {
    for (const kind of [1, 2, 13, 14] as const) {
      const moments = resolveLifecycleMoments([{ kind, location: { x: 3, y: 4 } }], []);
      expect(moments[0]?.position).toEqual({ x: 3, y: 4 });
      readyImage();
      const ctx = fakeCtx({ width: 400, height: 400 });
      draw(ctx, frame([], [{ kind, location: { x: 3, y: 4 } }]));
      expect(ctx.arc.mock.calls.length, String(kind)).toBeGreaterThanOrEqual(1);
    }
  });

  it("never uses gore colors for life-cycle bursts", () => {
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([npc("n")], [{ kind: 1, location: { x: 5, y: 5 } }]));
    const styles = [ctx.fillStyle, ctx.strokeStyle].map((value) => String(value).toLowerCase());
    expect(styles.join(" ")).not.toMatch(/#f{2}0{4}|#8b0000|crimson|blood/);
  });

  it("does not advance or hide the cue under prefers-reduced-motion", () => {
    readyImage();
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([npc("n")], [{ kind: 0, location: { x: 5, y: 5 } }]));
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(1);
    expect(animationSpecForEvent(0).reducedMotionFallback).toBe("static-icon");
  });

  it("keeps SettlementFounded out of the life-cycle burst family (T22 owns founding)", () => {
    expect(resolveLifecycleMoments([{ kind: 20, location: { x: 1, y: 1 } }], [])).toEqual([]);
    expect(animationSpecForEvent(20).key).toBe("unknown");
  });
});
