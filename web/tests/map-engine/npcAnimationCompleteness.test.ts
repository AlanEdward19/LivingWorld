import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import {
  animationSpecForAction,
  animationSpecForEvent,
  animationSpecForProcess,
} from "../../src/map-engine/npcAnimationCatalog";
import { actionVisualFor } from "../../src/map-engine/actionVisuals";
import { cueFromSpec } from "../../src/map-engine/npcAnimationCue";

const css = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "../../src/styles/global.css"), "utf8");

/** Motor `ActionType` ids — contract lives here, not in the catalog's exported list. */
const REQUIRED_ACTION_TYPE_IDS = [0, 1, 2, 3, 4, 5, 6] as const;
const TRAVEL = 4;

/** Projector / Stage 4 process descriptor keys. Duplicated so deleting catalog list constants still fails. */
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

/** LWV-07 `WorldEventKind` subset (Birth..StillBirth family). */
const REQUIRED_LWV07_EVENT_KINDS = [0, 1, 2, 9, 10, 11, 12, 13, 14] as const;

function assertAnimatedCue(spec: {
  key: string;
  keyframes: string;
  durationMs: number;
  a11yLabel: string;
  reducedMotionFallback: string;
  hidden: boolean;
  animated: boolean;
  icon: string;
}) {
  expect(spec.key).not.toBe("unknown");
  expect(spec.keyframes).not.toBe("none");
  expect(spec.durationMs).toBeGreaterThan(0);
  expect(spec.animated).toBe(true);
  expect(spec.hidden).toBe(false);
  expect(spec.a11yLabel.length).toBeGreaterThan(0);
  expect(spec.icon.length).toBeGreaterThan(0);
  expect(spec.reducedMotionFallback).toBe("static-icon");
  expect(css).toContain(`@keyframes ${spec.keyframes}`);
}

describe("NPC animation completeness (T28 / LWV-07.5)", () => {
  it("requires every ActionType to be animated except hidden Travel", () => {
    const hidden = REQUIRED_ACTION_TYPE_IDS.filter((id) => animationSpecForAction(id).hidden);
    expect(hidden).toEqual([TRAVEL]);

    for (const id of REQUIRED_ACTION_TYPE_IDS) {
      const spec = animationSpecForAction(id);
      if (id === TRAVEL) {
        expect(spec.hidden).toBe(true);
        expect(spec.animated).toBe(false);
        expect(spec.key).toBe("travel");
        continue;
      }
      assertAnimatedCue(spec);
    }
  });

  it("keeps Travel hidden because the map route is the travel cue", () => {
    const spec = animationSpecForAction(TRAVEL);
    expect(spec.hidden).toBe(true);
    expect(spec.animated).toBe(false);
    expect(spec.key).toBe("travel");
    const renderer = readFileSync(join(dirname(fileURLToPath(import.meta.url)), "../../src/map-engine/renderer.ts"), "utf8");
    expect(renderer).toContain("drawRelocationRoute");
    expect(renderer).toContain("travelDestination");
  });

  it("requires every Stage 4 process descriptor to have a non-orphan animated spec", () => {
    const missing = REQUIRED_STAGE4_PROCESS_DESCRIPTORS.filter((key) => animationSpecForProcess(key).key === "unknown");
    expect(missing).toEqual([]);
    for (const descriptor of REQUIRED_STAGE4_PROCESS_DESCRIPTORS) {
      const spec = animationSpecForProcess(descriptor);
      expect(spec.key).toBe(descriptor);
      assertAnimatedCue(spec);
    }
  });

  it("requires every LWV-07 event kind to have a non-orphan animated spec", () => {
    const missing = REQUIRED_LWV07_EVENT_KINDS.filter((kind) => animationSpecForEvent(kind).key === "unknown");
    expect(missing).toEqual([]);
    for (const kind of REQUIRED_LWV07_EVENT_KINDS) {
      assertAnimatedCue(animationSpecForEvent(kind));
    }
  });

  it("treats an injected unmapped key as a static unknown icon, never a blank tile", () => {
    const spec = animationSpecForAction(77);
    expect(spec.key).toBe("unknown");
    expect(spec.icon).toBe("question");
    expect(spec.animated).toBe(false);
    expect(spec.hidden).toBe(false);
    expect(spec.keyframes).toBe("none");
    expect(spec.a11yLabel).toBe("Atividade 77");
    expect(actionVisualFor(77).label).toBe("Atividade 77");
  });

  it("stops motion under prefers-reduced-motion without hiding the cue", () => {
    for (const id of REQUIRED_ACTION_TYPE_IDS.filter((actionId) => actionId !== TRAVEL)) {
      const spec = animationSpecForAction(id);
      const cue = cueFromSpec(spec, { reducedMotion: true, progress: 0.4 });
      expect(cue.motion).toBe(false);
      expect(cue.opacity).toBe(1);
      expect(spec.hidden).toBe(false);
      expect(actionVisualFor(id).hidden).toBe(false);
    }
    expect(css).toMatch(/prefers-reduced-motion: reduce[\s\S]*animation:\s*none/);
    expect(css).not.toMatch(/prefers-reduced-motion: reduce[\s\S]*\.npc-anim-[\s\S]*display:\s*none/);
  });
});
