// Fase 15.1, T8 (LWV-02) + T23 (LWV-07): pistas visuais por ação. A tabela vive em
// `npcAnimationCatalog.ts` — este módulo reexporta o contrato que inspector/renderer já consomem.
import {
  animationSpecForAction,
  actionVisualFromSpec,
  type ActionIcon as CatalogIcon,
} from "./npcAnimationCatalog";

export type ActionIcon = CatalogIcon;
export type { ActionVisual } from "./npcAnimationCatalog";

function unknownActionVisual(actionId: number) {
  return actionVisualFromSpec(animationSpecForAction(actionId));
}

export function actionVisualFor(actionId: number) {
  return unknownActionVisual(actionId);
}

/** Reusado por `NpcInspector` (era uma tabela duplicada só de labels ali). */
export const ACTION_LABELS: Readonly<Record<number, string>> = Object.fromEntries(
  [0, 1, 2, 3, 4, 5, 6].map((id) => [id, animationSpecForAction(id).a11yLabel]),
);
