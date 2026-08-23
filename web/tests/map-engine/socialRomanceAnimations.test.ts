import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { afterEach, describe, expect, it, vi } from "vitest";
import { draw, type RenderFrame } from "../../src/map-engine/renderer";
import type { AuthoritativeEntity, CameraState } from "../../src/map-engine/types";
import { animationSpecForAction, animationSpecForEvent } from "../../src/map-engine/npcAnimationCatalog";
import { resolveSocialLinks } from "../../src/map-engine/socialRomance";

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
    ellipse: vi.fn(),
    setLineDash: vi.fn(),
    save: vi.fn(),
    restore: vi.fn(),
    translate: vi.fn(),
    rotate: vi.fn(),
  } as unknown as CanvasRenderingContext2D & {
    arc: ReturnType<typeof vi.fn>;
    moveTo: ReturnType<typeof vi.fn>;
    lineTo: ReturnType<typeof vi.fn>;
    setLineDash: ReturnType<typeof vi.fn>;
    drawImage: ReturnType<typeof vi.fn>;
  };
}

function npc(id: string, x: number, y: number, action: number | null = 3): AuthoritativeEntity {
  return {
    ref: { kind: "npc", id, space: { kind: "City", cityId: "city-a" } },
    position: { x, y },
    size: { w: 1, h: 1 },
    sizeIsDerived: false,
    color: "#abc",
    currentAction: action,
  };
}

function frame(entities: AuthoritativeEntity[], events: { kind: number }[] = []): RenderFrame {
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

describe("social & romance animations (T25 / LWV-07.3)", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("animates Socialize from the unified catalog", () => {
    const spec = animationSpecForAction(3);
    expect(spec.animated).toBe(true);
    expect(spec.keyframes).toBe("npc-social-chat");
    expect(spec.durationMs).toBeGreaterThan(0);
    expect(css).toContain("@keyframes npc-social-chat");
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([npc("talker", 5, 5, 3)]));
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(1);
  });

  it("animates courtship and marriage event specs without gore", () => {
    for (const kind of [9, 10, 11, 12]) {
      const spec = animationSpecForEvent(kind);
      expect(spec.animated).toBe(true);
      expect(spec.keyframes).not.toBe("none");
      expect(spec.durationMs).toBeGreaterThan(0);
      expect(spec.a11yLabel.toLowerCase()).not.toMatch(/sangue|gore|cadáver|osso/);
      expect(css).toContain(`@keyframes ${spec.keyframes}`);
    }
  });

  it("links two Socialize NPCs when both are materialized and adjacent", () => {
    const a = npc("a", 4, 5, 3);
    const b = npc("b", 5, 5, 3);
    expect(resolveSocialLinks([a, b])).toEqual([{ fromId: "a", toId: "b" }]);
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([a, b]));
    expect(ctx.lineTo).toHaveBeenCalled();
    expect(ctx.setLineDash.mock.calls.some((args) => (args[0] as number[]).length > 0)).toBe(true);
  });

  it("does not link two Socialize NPCs sitting on distant, non-adjacent tiles", () => {
    const a = npc("a", 4, 5, 3);
    const b = npc("b", 6, 5, 3);
    expect(resolveSocialLinks([a, b])).toEqual([]);
  });

  it("does not draw a two-NPC link when only one Socialize token is present", () => {
    const alone = npc("solo", 5, 5, 3);
    expect(resolveSocialLinks([alone])).toEqual([]);
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([alone]));
    const socialDashes = ctx.setLineDash.mock.calls.filter((args) => {
      const dash = args[0] as number[];
      return dash.length === 2 && dash[0] === 4 && dash[1] === 6;
    });
    expect(socialDashes).toHaveLength(0);
  });

  it("links two materialized NPCs when a courtship or marriage event fires this tick", () => {
    const a = npc("left", 4, 5, 2);
    const b = npc("right", 5, 5, 2);
    expect(resolveSocialLinks([a, b], [{ kind: 12 }])).toEqual([{ fromId: "left", toId: "right" }]);
    expect(resolveSocialLinks([a], [{ kind: 9 }])).toEqual([]);
    readyImage();
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([a, b], [{ kind: 9 }]));
    expect(ctx.lineTo).toHaveBeenCalled();
  });

  it("keeps the social cue and link visible under prefers-reduced-motion", () => {
    readyImage();
    vi.stubGlobal("matchMedia", (query: string) => ({
      matches: query.includes("prefers-reduced-motion"),
      media: query,
      addEventListener: () => {},
      removeEventListener: () => {},
    }));
    const ctx = fakeCtx({ width: 400, height: 400 });
    draw(ctx, frame([npc("a", 4, 5, 3), npc("b", 5, 5, 3)]));
    expect(ctx.drawImage).toHaveBeenCalled();
    expect(ctx.lineTo).toHaveBeenCalled();
    expect(ctx.arc.mock.calls.length).toBeGreaterThanOrEqual(1);
  });
});
