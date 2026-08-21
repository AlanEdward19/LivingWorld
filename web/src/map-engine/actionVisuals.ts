// Fase 15.1, T8 (LWV-02): catálogo data-driven de pistas visuais por ação existente
// (`ActionType` em `src/LivingWorld.Domain/Behavior/ActionType.cs`, valores 0-6 estáveis).
// Única fonte de verdade para label/glyph — `NpcInspector` e `npcAppearance.ts` (mapa + token
// do inspector) leem daqui, nunca duplicam a tabela. Ação desconhecida (id fora do catálogo)
// cai num descritor genérico legível, nunca expõe o enum bruto.
export interface ActionVisual {
  key: string;
  label: string;
  glyph: string;
  animated: boolean;
}

const KNOWN_ACTION_VISUALS: Record<number, ActionVisual> = {
  0: { key: "eat", label: "Comendo", glyph: "Com", animated: false },
  1: { key: "sleep", label: "Dormindo", glyph: "Zzz", animated: true },
  2: { key: "work", label: "Trabalhando", glyph: "Trab", animated: false },
  3: { key: "socialize", label: "Socializando", glyph: "Soc", animated: false },
  4: { key: "travel", label: "Viajando", glyph: "Via", animated: false },
  5: { key: "rest", label: "Descansando", glyph: "Desc", animated: false },
  6: { key: "buy", label: "Comprando", glyph: "$", animated: false },
};

function unknownActionVisual(actionId: number): ActionVisual {
  return { key: "unknown", label: `Atividade ${actionId}`, glyph: "?", animated: false };
}

export function actionVisualFor(actionId: number): ActionVisual {
  return KNOWN_ACTION_VISUALS[actionId] ?? unknownActionVisual(actionId);
}

/** Reusado por `NpcInspector` (era uma tabela duplicada só de labels ali). */
export const ACTION_LABELS: Readonly<Record<number, string>> = Object.fromEntries(
  Object.entries(KNOWN_ACTION_VISUALS).map(([id, visual]) => [Number(id), visual.label]),
);
