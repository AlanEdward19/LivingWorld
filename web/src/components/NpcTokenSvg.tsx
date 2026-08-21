import { npcPawnDataUrl } from "../npcAppearance";
import { actionVisualFor } from "../map-engine/actionVisuals";

export interface NpcTokenSvgProps {
  npcId: string;
  currentAction?: number | null;
  className?: string;
}

export function NpcTokenSvg({ npcId, currentAction, className }: NpcTokenSvgProps) {
  // T8 (LWV-02): equivalente textual/ARIA da pista visual — a mesma ação que o glifo mostra no
  // canvas do mapa também vira texto aqui, para quem usa leitor de tela.
  const actionSuffix = currentAction == null ? "" : ` — ${actionVisualFor(currentAction).label}`;
  return (
    <img
      className={className}
      src={npcPawnDataUrl({ id: npcId, currentAction })}
      alt={`Aparência visual do NPC ${npcId}${actionSuffix}`}
      draggable={false}
    />
  );
}
