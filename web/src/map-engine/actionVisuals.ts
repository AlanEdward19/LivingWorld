// Fase 15.1, T8 (LWV-02): catálogo data-driven de pistas visuais por ação existente
// (`ActionType` em `src/LivingWorld.Domain/Behavior/ActionType.cs`, valores 0-6 estáveis).
// Única fonte de verdade para label/ícone — `NpcInspector`, `renderer.ts` (mapa) e
// `NpcTokenSvg.tsx` (token do inspector) leem daqui, nunca duplicam a tabela.
//
// Feedback do usuário (2026-08-21): a primeira versão usava um glifo de TEXTO (ex.: "Trab",
// "Soc") — ilegível no tamanho de um badge na cabeça do NPC. Ícones (`ActionIcon`) substituem o
// texto; `icon.ts` desenha cada um (canvas e SVG). Ação de andar pela vila (`Travel`) não ganha
// pista nenhuma — pedido explícito do usuário, é o estado "padrão" e não precisa de destaque.
export type ActionIcon = "moon" | "apple" | "tool" | "chat" | "coin" | "waves" | "question";

export interface ActionVisual {
  key: string;
  label: string;
  icon: ActionIcon;
  animated: boolean;
  /** Ação comum demais pra merecer destaque visual constante (ex.: andar pela vila). */
  hidden: boolean;
}

const KNOWN_ACTION_VISUALS: Record<number, ActionVisual> = {
  0: { key: "eat", label: "Comendo", icon: "apple", animated: false, hidden: false },
  1: { key: "sleep", label: "Dormindo", icon: "moon", animated: true, hidden: false },
  2: { key: "work", label: "Trabalhando", icon: "tool", animated: false, hidden: false },
  3: { key: "socialize", label: "Socializando", icon: "chat", animated: false, hidden: false },
  4: { key: "travel", label: "Viajando", icon: "question", animated: false, hidden: true },
  5: { key: "rest", label: "Descansando", icon: "waves", animated: false, hidden: false },
  6: { key: "buy", label: "Comprando", icon: "coin", animated: false, hidden: false },
};

function unknownActionVisual(actionId: number): ActionVisual {
  return { key: "unknown", label: `Atividade ${actionId}`, icon: "question", animated: false, hidden: false };
}

export function actionVisualFor(actionId: number): ActionVisual {
  return KNOWN_ACTION_VISUALS[actionId] ?? unknownActionVisual(actionId);
}

/** Reusado por `NpcInspector` (era uma tabela duplicada só de labels ali). */
export const ACTION_LABELS: Readonly<Record<number, string>> = Object.fromEntries(
  Object.entries(KNOWN_ACTION_VISUALS).map(([id, visual]) => [Number(id), visual.label]),
);
