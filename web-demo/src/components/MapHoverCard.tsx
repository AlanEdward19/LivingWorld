import type { WorldFixture } from "../fixture/types";

export type MapHoverTarget =
  | { kind: "settlement"; id: string; x: number; y: number }
  | { kind: "agent"; id: string; x: number; y: number }
  | { kind: "building"; id: string; x: number; y: number };

export interface MapHoverCardProps {
  fixture: WorldFixture;
  hover: MapHoverTarget | null;
}

/**
 * Card de hover LOD (redesign doc World Map §42: "Hover é a primeira camada informacional" —
 * pedido do usuário 2026-08-27: popup perto do mouse, nunca cobrindo a tela, com menos
 * informação que a sidebar cheia). Reusado tanto no `WorldStage` (settlement/agent) quanto no
 * `SettlementStage` (building/agent) — mesmo componente, o `kind` decide o conteúdo.
 * `pointer-events: none` (via CSS) — é só leitura, nunca deveria roubar o clique do que está
 * embaixo dele.
 */
export function MapHoverCard({ fixture, hover }: MapHoverCardProps) {
  if (!hover) return null;

  let title: string;
  let lines: string[];

  if (hover.kind === "settlement") {
    const settlement = fixture.settlements.find((s) => s.id === hover.id);
    if (!settlement) return null;
    title = settlement.name;
    lines = [`Population ${settlement.population} · ${settlement.populationTrend}`, `Food ${settlement.food}`, `Employment ${settlement.employment}`];
  } else if (hover.kind === "agent") {
    const agent = fixture.agents.find((a) => a.id === hover.id);
    if (!agent) return null;
    title = agent.name;
    lines = [`${agent.age} · ${agent.profession}`, agent.currentIntent];
  } else {
    const building = fixture.settlements.flatMap((s) => s.buildings).find((b) => b.id === hover.id);
    if (!building) return null;
    const occupants = fixture.agents.filter((a) => a.indoorLocation?.buildingId === building.id).length;
    title = building.name;
    lines = [building.kind, `${occupants} ${occupants === 1 ? "occupant" : "occupants"}`];
  }

  return (
    <div className="map-hover-card" style={{ left: hover.x + 16, top: hover.y - 8 }}>
      <strong>{title}</strong>
      {lines.map((line) => (
        <span key={line}>{line}</span>
      ))}
    </div>
  );
}
