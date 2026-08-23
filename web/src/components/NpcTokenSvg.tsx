import { npcPawnDataUrl } from "../npcAppearance";
import { actionVisualFor } from "../map-engine/actionVisuals";
import { ActionBadge } from "./ActionBadge";

export interface NpcTokenSvgProps {
  npcId: string;
  currentAction?: number | null;
  className?: string;
  /** T13: detalhe acessível extra (qualidade/lugar/duração) — o badge visual fica aria-hidden. */
  accessibleDetail?: string;
}

export function NpcTokenSvg({ npcId, currentAction, className, accessibleDetail }: NpcTokenSvgProps) {
  // Feedback do usuário (2026-08-21): a pista de ação deixou de ser desenhada DENTRO do SVG da
  // aparência (fonte de uma travada real no canvas) — aqui ela é um badge sobreposto ao token,
  // igual em espírito ao ícone que `renderer.ts` desenha no mapa.
  const visual = currentAction == null ? null : actionVisualFor(currentAction);
  const actionSuffix = accessibleDetail
    ? ` — ${accessibleDetail}`
    : visual ? ` — ${visual.label}` : "";
  return (
    <span className="npc-token-wrap">
      <img
        className={className}
        src={npcPawnDataUrl({ id: npcId })}
        alt={`Aparência visual do NPC ${npcId}${actionSuffix}`}
        draggable={false}
      />
      {visual && !visual.hidden && <ActionBadge visual={visual} />}
    </span>
  );
}
