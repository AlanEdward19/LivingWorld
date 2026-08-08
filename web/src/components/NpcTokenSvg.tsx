import { npcPawnDataUrl } from "../npcAppearance";

export interface NpcTokenSvgProps {
  npcId: string;
  currentAction?: number | null;
  className?: string;
}

export function NpcTokenSvg({ npcId, currentAction, className }: NpcTokenSvgProps) {
  return (
    <img
      className={className}
      src={npcPawnDataUrl({ id: npcId, currentAction })}
      alt={`Aparência visual do NPC ${npcId}`}
      draggable={false}
    />
  );
}
