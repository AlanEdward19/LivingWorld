import { npcPawnDataUrl } from "./appearance";

// SPEC_DEVIATION: web/src/components/NpcTokenSvg.tsx also renders an action badge
// (`currentAction`/`ActionBadge`/`actionVisuals`) driven by a live simulation's ActionType.
// Reason: this demo's fixture has no action-tracking concept (design.md's stated interface is
// `<NpcToken id size />` only) — the badge machinery isn't ported since there is nothing in the
// static fixture to drive it. The identity rendering itself (`npcPawnDataUrl`) is the literal
// port (see appearance.ts).
export interface NpcTokenProps {
  id: string;
  size?: number;
}

export function NpcToken({ id, size = 100 }: NpcTokenProps) {
  return (
    <img
      src={npcPawnDataUrl({ id })}
      alt={`Aparência visual do NPC ${id}`}
      width={size}
      height={size * 1.2}
      draggable={false}
    />
  );
}
