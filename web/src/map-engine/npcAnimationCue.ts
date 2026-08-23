import type { NpcAnimationSpec } from "./npcAnimationCatalog";

/** Start of the catalog-driven progress ring (12 o'clock). */
export const PROGRESS_RING_START = -Math.PI / 2;

export function clampProgress(progress: number): number {
  return Math.max(0, Math.min(1, progress));
}

export function progressRingEndAngle(progress: number): number {
  return PROGRESS_RING_START + clampProgress(progress) * Math.PI * 2;
}

/** Three visual stages from motor progress — cosmetic only. */
export function progressStage(progress: number): 0 | 1 | 2 {
  const p = clampProgress(progress);
  if (p < 1 / 3) return 0;
  if (p < 2 / 3) return 1;
  return 2;
}

export interface AnimationCueDraw {
  opacity: number;
  scale: number;
  ringProgress: number;
  stage: 0 | 1 | 2;
  motion: boolean;
}

export function cueFromSpec(
  spec: NpcAnimationSpec,
  options: { progress?: number; reducedMotion?: boolean; nowMs?: number } = {},
): AnimationCueDraw {
  const reducedMotion = options.reducedMotion === true;
  const hasProgress = options.progress != null;
  const ringProgress = hasProgress ? clampProgress(options.progress!) : 0;
  const stage = hasProgress ? progressStage(options.progress!) : 0;
  const motion = spec.animated && !reducedMotion && spec.durationMs > 0 && spec.keyframes !== "none";

  let opacity = 1;
  let scale = 1;
  if (motion) {
    if (hasProgress) {
      opacity = 0.62 + 0.38 * ringProgress;
      scale = 0.88 + 0.14 * ringProgress;
    } else {
      const now = options.nowMs ?? 0;
      const t = (now % spec.durationMs) / spec.durationMs;
      opacity = 0.55 + 0.45 * (0.5 - 0.5 * Math.cos(t * Math.PI * 2));
    }
  }

  return { opacity, scale, ringProgress, stage, motion };
}
