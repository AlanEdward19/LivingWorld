import type { ProcessVisual } from "../data/contracts";
import type { AuthoritativeEntity } from "./types";
import { actionVisualFromSpec, animationSpecForProcess, type ActionVisual } from "./npcAnimationCatalog";

const COLOCATED_KINDS = new Set(["rest", "food", "water", "crop"]);

const PROCESS_KIND_ALIASES: Record<string, string> = {
  rest: "sleep-bed",
  food: "eat-prepared",
  water: "carry-water",
  crop: "plant-crop",
  cook: "cook-food",
};

export function processCueVisual(kind: string, descriptorKey: string): ActionVisual {
  const fromDescriptor = animationSpecForProcess(descriptorKey);
  if (fromDescriptor.key !== "unknown") {
    return actionVisualFromSpec(fromDescriptor);
  }
  const alias = PROCESS_KIND_ALIASES[kind];
  if (alias) {
    return actionVisualFromSpec(animationSpecForProcess(alias));
  }
  return actionVisualFromSpec(fromDescriptor);
}

export function processAccessibleLabel(process: ProcessVisual): string {
  const cue = processCueVisual(process.kind, process.descriptorKey);
  const pct = Math.round(Math.max(0, Math.min(1, process.progress)) * 100);
  return `${cue.label}, ${pct}%`;
}

function sameCell(
  a: { x: number; y: number } | null | undefined,
  b: { x: number; y: number },
): boolean {
  return !!a && a.x === b.x && a.y === b.y;
}

export function overlayProcessOnNpc(
  entity: AuthoritativeEntity,
  processes: readonly ProcessVisual[],
): AuthoritativeEntity {
  const npcId = Number(entity.ref.id);
  const match = processes.find((process) => {
    if (process.targetId === npcId) return true;
    return COLOCATED_KINDS.has(process.kind) && sameCell(process.location, entity.position);
  });
  if (!match) return entity;
  return {
    ...entity,
    process: {
      kind: match.kind,
      progress: match.progress,
      accessibleLabel: processAccessibleLabel(match),
      descriptorKey: match.descriptorKey,
    },
  };
}
