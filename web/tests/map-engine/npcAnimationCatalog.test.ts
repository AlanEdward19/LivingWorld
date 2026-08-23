import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  animationSpecForAction,
  animationSpecForEvent,
  animationSpecForProcess,
  animationSpecForUnknown,
} from "../../src/map-engine/npcAnimationCatalog";

const REQUIRED_ACTION_TYPE_IDS = [0, 1, 2, 3, 4, 5, 6] as const;
const REQUIRED_STAGE4_PROCESS_DESCRIPTORS = [
  "sleep-ground",
  "sleep-dwelling",
  "sleep-bed",
  "eat-raw",
  "eat-prepared",
  "cook-food",
  "collect-water",
  "carry-water",
  "deliver-water",
  "plant-crop",
  "water-crop",
  "harvest-crop",
  "construction",
] as const;
const REQUIRED_LWV07_EVENT_KINDS = [0, 1, 2, 9, 10, 11, 12, 13, 14] as const;
import { actionVisualFor } from "../../src/map-engine/actionVisuals";
import { processCueVisual } from "../../src/map-engine/cityNpcOverlay";

const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "../../src/styles/global.css"), "utf8");

function assertCompleteSpec(spec: {
  key: string;
  keyframes: string;
  durationMs: number;
  a11yLabel: string;
  reducedMotionFallback: string;
}) {
  expect(spec.key.length).toBeGreaterThan(0);
  expect(spec.keyframes.length).toBeGreaterThan(0);
  expect(spec.durationMs).toBeGreaterThanOrEqual(0);
  expect(spec.a11yLabel.length).toBeGreaterThan(0);
  expect(spec.reducedMotionFallback).toBe("static-icon");
}

describe("NpcAnimationCatalog (T23 / LWV-07.1/4/5)", () => {
  it("maps every ActionType id to exactly one animation spec", () => {
    const keys = REQUIRED_ACTION_TYPE_IDS.map((id) => animationSpecForAction(id).key);
    expect(new Set(keys).size).toBe(REQUIRED_ACTION_TYPE_IDS.length);
    for (const id of REQUIRED_ACTION_TYPE_IDS) {
      assertCompleteSpec(animationSpecForAction(id));
    }
  });

  it("maps every Stage 4 process descriptor to exactly one animation spec", () => {
    const keys = REQUIRED_STAGE4_PROCESS_DESCRIPTORS.map((descriptor) => animationSpecForProcess(descriptor).key);
    expect(new Set(keys).size).toBe(REQUIRED_STAGE4_PROCESS_DESCRIPTORS.length);
    for (const descriptor of REQUIRED_STAGE4_PROCESS_DESCRIPTORS) {
      const spec = animationSpecForProcess(descriptor);
      assertCompleteSpec(spec);
      expect(spec.key).toBe(descriptor);
    }
  });

  it("maps every LWV-07 lifecycle event kind to exactly one animation spec", () => {
    const keys = REQUIRED_LWV07_EVENT_KINDS.map((kind) => animationSpecForEvent(kind).key);
    expect(new Set(keys).size).toBe(REQUIRED_LWV07_EVENT_KINDS.length);
    for (const kind of REQUIRED_LWV07_EVENT_KINDS) {
      const spec = animationSpecForEvent(kind);
      assertCompleteSpec(spec);
      expect(spec.a11yLabel).not.toBe(String(kind));
    }
  });

  it("falls back to a static icon for unknown actions, processes, and events — never a blank tile", () => {
    const unknown = animationSpecForUnknown("mystery");
    assertCompleteSpec(unknown);
    expect(unknown.keyframes).toBe("none");
    expect(unknown.durationMs).toBe(0);
    expect(animationSpecForAction(99).key).toBe("unknown");
    expect(animationSpecForProcess("not-a-real-process").key).toBe("unknown");
    expect(animationSpecForEvent(999).key).toBe("unknown");
  });

  it("keeps the cue visible under reduced motion by using a static-icon fallback, never hiding it", () => {
    const sleep = animationSpecForAction(1);
    expect(sleep.reducedMotionFallback).toBe("static-icon");
    expect(sleep.a11yLabel).toBe("Dormindo");
    expect(actionVisualFor(1).hidden).toBe(false);
    expect(actionVisualFor(1).animated).toBe(true);
    expect(actionVisualFor(1).label).toBe(sleep.a11yLabel);
  });

  it("uses the existing sleep Zzz keyframes as the catalog source of truth", () => {
    const sleep = animationSpecForAction(1);
    expect(sleep.keyframes).toBe("npc-rest-zzz");
    expect(sleep.durationMs).toBe(1800);
    expect(css).toContain("@keyframes npc-rest-zzz");
    expect(css).toContain("prefers-reduced-motion");
  });

  it("feeds T20 process overlays from the same catalog instead of a parallel table", () => {
    expect(processCueVisual("food", "eat-prepared").label).toBe(animationSpecForProcess("eat-prepared").a11yLabel);
    expect(processCueVisual("water", "carry-water").label).toBe(animationSpecForProcess("carry-water").a11yLabel);
    expect(processCueVisual("rest", "sleep-bed").label).toBe(animationSpecForProcess("sleep-bed").a11yLabel);
  });
});
