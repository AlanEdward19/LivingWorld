import type { ActionVisual } from "../map-engine/actionVisuals";

const BADGE_FILL = "#171b20";
const BADGE_ACCENT = "#f0c96a";

/** Ícone por ação (mesma linguagem visual de `map-engine/actionIcon.ts`, redesenhado em SVG/JSX
 * porque este badge é DOM, não canvas). `aria-hidden`: o equivalente textual já vive no `alt`
 * do `<img>` vizinho (`NpcTokenSvg`) — repetir aqui seria redundante pro leitor de tela. */
function ActionIconShape({ icon }: { icon: ActionVisual["icon"] }) {
  switch (icon) {
    case "moon":
      return <>
        <circle cx="9" cy="10" r="6.2" fill={BADGE_ACCENT} />
        <circle cx="12" cy="9" r="5.4" fill={BADGE_FILL} />
      </>;
    case "apple":
      return <>
        <circle cx="10" cy="11" r="5.5" fill={BADGE_ACCENT} />
        <line x1="10" y1="5.5" x2="11.2" y2="3" stroke={BADGE_ACCENT} strokeWidth="1.4" />
      </>;
    case "tool":
      return <g transform="rotate(45 10 10)">
        <rect x="8.7" y="3.5" width="2.6" height="13" fill={BADGE_ACCENT} />
        <rect x="6.5" y="3.5" width="7" height="3" fill={BADGE_ACCENT} />
      </g>;
    case "chat":
      return <>
        <ellipse cx="10" cy="9" rx="6.2" ry="4.4" fill={BADGE_ACCENT} />
        <polygon points="8,12.5 6.5,15.5 10.5,13" fill={BADGE_ACCENT} />
      </>;
    case "coin":
      return <>
        <circle cx="10" cy="10" r="6" fill={BADGE_ACCENT} />
        <text x="10" y="13" fontSize="8" fontWeight="bold" textAnchor="middle" fill={BADGE_FILL}>$</text>
      </>;
    case "waves":
      return <>
        {[4, 10, 16].map((y) => (
          <path key={y} d={`M3 ${y} Q7 ${y - 3} 10 ${y} T17 ${y}`} fill="none" stroke={BADGE_ACCENT} strokeWidth="1.6" />
        ))}
      </>;
    case "question":
      return <text x="10" y="14" fontSize="11" fontWeight="bold" textAnchor="middle" fill={BADGE_ACCENT}>?</text>;
  }
}

export function ActionBadge({ visual }: { visual: ActionVisual }) {
  return (
    <svg
      className={`npc-action-badge${visual.animated ? " npc-action-badge-pulse" : ""}`}
      viewBox="0 0 20 20"
      width="20"
      height="20"
      aria-hidden="true"
    >
      <circle cx="10" cy="10" r="9.5" fill={BADGE_FILL} stroke={BADGE_ACCENT} strokeWidth="1.5" />
      <ActionIconShape icon={visual.icon} />
    </svg>
  );
}
