import type { KeyboardEvent } from "react";
import type { AgentFixture, WorldFixture } from "../fixture/types";
import { IsoTile, TILE_HEIGHT, TILE_WIDTH } from "./IsoTileRenderer";
import { toScreen } from "./IsoProjection";
import { SETTLEMENT_PALETTE } from "./isoPalette";
import { NpcToken } from "../npc/NpcToken";
import { appearanceForNpc } from "../npc/appearance";
import { usePatrolPosition, type GridPoint } from "./usePatrolPosition";

/**
 * Doc `LivingWorld_Frontend_Final.md` §14/§46 (AD-018) — Agents nunca somem por causa do zoom.
 * Só 2 níveis restam: "world" (assentamentos + todo NPC como ponto pequeno, perto do seu
 * assentamento) e "settlement" (prédios E NPCs do assentamento juntos, na mesma cena — nunca um
 * toggle excludente entre "ver prédios" e "ver gente").
 */
export type ZoomLevel = "world" | "settlement";

export interface SemanticZoomMapProps {
  fixture: WorldFixture;
  /** Nível atual — decidido pela view que monta o mapa, não por um controle de zoom interno ao
   * mapa nesta demo. Default "world". */
  level?: ZoomLevel;
  /** Obrigatório pra "settlement" — escopa prédios/NPCs ao assentamento selecionado. */
  settlementId?: string;
  onSelectSettlement: (settlementId: string) => void;
  onSelectNpc: (agentId: string) => void;
  /** Só chamado pra prédios com `floors.length > 0` — só esses têm interior pra entrar. */
  onSelectBuilding?: (buildingId: string) => void;
}

const DEFAULT_HALF_EXTENT = TILE_WIDTH * 3;
/** Quanto o trajeto local do NPC encolhe pra caber como um pontinho perto do seu assentamento
 * no nível "world" (doc §14: "zoom distante: bolinhas pequenas"). */
const WORLD_DOT_SHRINK = 5;
/** Centro aproximado do grid de um settlement — usado só pra centralizar o encolhimento acima,
 * não é um dado real de nenhum sistema. */
const SETTLEMENT_LOCAL_CENTER: GridPoint = { x: 1.5, y: 1.5 };

/** § 149 Accessibility — "keyboard navigation obrigatório": marcadores do mapa (SVG) não são
 * nativamente operáveis por teclado com só `onClick`; ativam também em Enter/Space. */
function activateOnKey(action: () => void) {
  return (event: KeyboardEvent) => {
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      action();
    }
  };
}

/**
 * Câmera centralizada no conteúdo (doc §46 Map visual hierarchy / QA "consigo saber onde
 * olhar?") — em vez de um `viewBox` fixo, calculado a partir do bounding box real dos pontos
 * renderizados, com padding.
 */
function centeredViewBox(points: { x: number; y: number }[], padding: number): string {
  if (points.length === 0) {
    return `${-DEFAULT_HALF_EXTENT} ${-DEFAULT_HALF_EXTENT} ${DEFAULT_HALF_EXTENT * 2} ${DEFAULT_HALF_EXTENT * 2}`;
  }
  const xs = points.map((p) => p.x);
  const ys = points.map((p) => p.y);
  const minX = Math.min(...xs) - padding;
  const maxX = Math.max(...xs) + padding;
  const minY = Math.min(...ys) - padding;
  const maxY = Math.max(...ys) + padding;
  const width = Math.max(maxX - minX, DEFAULT_HALF_EXTENT);
  const height = Math.max(maxY - minY, DEFAULT_HALF_EXTENT);
  return `${minX} ${minY} ${width} ${height}`;
}

/** Ids de settlement/agent que têm algum evento pertencente a um Story Thread — base do
 * marcador de evento importante (doc §48). Sem estado de "já visto" nesta demo: o pulso roda
 * uma vez por montagem do mapa, depois o marcador fica discreto e estático (CSS, respeita
 * reduced-motion). */
function notableSettlementIds(fixture: WorldFixture): Set<string> {
  const notableEventIds = new Set(fixture.storyThreads.flatMap((t) => t.eventIds));
  return new Set(fixture.events.filter((e) => notableEventIds.has(e.eventId)).map((e) => e.settlementId));
}

function notableAgentIds(fixture: WorldFixture): Set<string> {
  const notableEventIds = new Set(fixture.storyThreads.flatMap((t) => t.eventIds));
  return new Set(fixture.events.filter((e) => notableEventIds.has(e.eventId)).flatMap((e) => e.affectedAgentIds));
}

interface WorldAgentDotProps {
  agent: AgentFixture;
  settlementScreen: { x: number; y: number };
  notable: boolean;
  onSelectNpc: (agentId: string) => void;
}

/** Um NPC no nível "world" — ponto pequeno com a cor estável do seu fenótipo (doc §15: "usar
 * identidade visual estável do Agent"), orbitando perto do seu assentamento conforme seu
 * trajeto local de patrulha (AD-018, movimento decorativo). */
function WorldAgentDot({ agent, settlementScreen, notable, onSelectNpc }: WorldAgentDotProps) {
  const position = usePatrolPosition(agent.patrolPoints);
  const offset = toScreen(
    position.x - SETTLEMENT_LOCAL_CENTER.x,
    position.y - SETTLEMENT_LOCAL_CENTER.y,
    TILE_WIDTH / WORLD_DOT_SHRINK,
    TILE_HEIGHT / WORLD_DOT_SHRINK,
  );
  const x = settlementScreen.x + offset.x;
  const y = settlementScreen.y + offset.y;
  const color = appearanceForNpc(agent.id).skin;

  return (
    <g
      data-testid="agent-marker"
      data-zoom-scale="world"
      onClick={() => onSelectNpc(agent.id)}
      onKeyDown={activateOnKey(() => onSelectNpc(agent.id))}
      role="button"
      tabIndex={0}
      aria-label={`Open ${agent.name}`}
      style={{ cursor: "pointer" }}
    >
      <circle cx={x} cy={y} r={2.5} fill={color} stroke="#0b0e12" strokeWidth={0.75} />
      {notable && <circle data-testid="event-marker" cx={x + 3} cy={y - 3} r={1.6} fill="var(--warning, #c69b58)" />}
    </g>
  );
}

interface SettlementAgentMarkerProps {
  agent: AgentFixture;
  notable: boolean;
  onSelectNpc: (agentId: string) => void;
}

/** Um NPC no nível "settlement" — token completo (NpcToken), posicionado no mesmo espaço de
 * grid dos prédios, movendo-se pelo seu trajeto de patrulha (AD-018). */
function SettlementAgentMarker({ agent, notable, onSelectNpc }: SettlementAgentMarkerProps) {
  const position = usePatrolPosition(agent.patrolPoints);
  const { x, y } = toScreen(position.x, position.y, TILE_WIDTH, TILE_HEIGHT);

  return (
    <g>
      <foreignObject
        data-testid="agent-marker"
        data-zoom-scale="settlement"
        x={x - 16}
        y={y - 16}
        width={32}
        height={38}
        onClick={() => onSelectNpc(agent.id)}
        onKeyDown={activateOnKey(() => onSelectNpc(agent.id))}
        role="button"
        tabIndex={0}
        aria-label={`Open ${agent.name}`}
        style={{ cursor: "pointer" }}
      >
        <NpcToken id={agent.id} size={32} />
      </foreignObject>
      {notable && <circle data-testid="event-marker" cx={x + 14} cy={y - 14} r={3.5} fill="var(--warning, #c69b58)" />}
    </g>
  );
}

export function SemanticZoomMap({ fixture, level = "world", settlementId, onSelectSettlement, onSelectNpc, onSelectBuilding }: SemanticZoomMapProps) {
  if (level === "world") {
    const settlementPoints = fixture.settlements.map((s) => toScreen(s.gridPosition.x, s.gridPosition.y, TILE_WIDTH, TILE_HEIGHT));
    const notableSettlements = notableSettlementIds(fixture);
    const notableAgents = notableAgentIds(fixture);

    return (
      <svg data-testid="semantic-zoom-map" data-zoom-level="world" viewBox={centeredViewBox(settlementPoints, TILE_WIDTH * 2)}>
        {fixture.settlements.map((settlement) => {
          const settlementScreen = toScreen(settlement.gridPosition.x, settlement.gridPosition.y, TILE_WIDTH, TILE_HEIGHT);
          const residents = fixture.agents.filter((a) => a.settlementId === settlement.id);
          return (
            <g key={settlement.id}>
              <g
                data-testid="settlement-marker"
                onClick={() => onSelectSettlement(settlement.id)}
                onKeyDown={activateOnKey(() => onSelectSettlement(settlement.id))}
                role="button"
                tabIndex={0}
                aria-label={`Open ${settlement.name}`}
                style={{ cursor: "pointer" }}
              >
                <circle
                  cx={settlementScreen.x}
                  cy={settlementScreen.y}
                  r={16}
                  fill={SETTLEMENT_PALETTE.top}
                  stroke={SETTLEMENT_PALETTE.right}
                  strokeWidth={2}
                />
                {notableSettlements.has(settlement.id) && (
                  <circle data-testid="event-marker" cx={settlementScreen.x + 12} cy={settlementScreen.y - 12} r={4} fill="var(--warning, #c69b58)" />
                )}
                <text x={settlementScreen.x} y={settlementScreen.y + 30} textAnchor="middle" fontSize={12}>
                  {settlement.name}
                </text>
              </g>
              {residents.map((agent) => (
                <WorldAgentDot
                  key={agent.id}
                  agent={agent}
                  settlementScreen={settlementScreen}
                  notable={notableAgents.has(agent.id)}
                  onSelectNpc={onSelectNpc}
                />
              ))}
            </g>
          );
        })}
      </svg>
    );
  }

  // level === "settlement"
  const settlement = fixture.settlements.find((s) => s.id === settlementId);
  if (!settlement) return null;

  const agents = fixture.agents.filter((agent) => agent.settlementId === settlementId);
  const notableAgents = notableAgentIds(fixture);
  const boundsPoints = [
    ...settlement.buildings.map((b) => toScreen(b.gridPosition.x, b.gridPosition.y, TILE_WIDTH, TILE_HEIGHT)),
    ...agents.flatMap((a) => a.patrolPoints.map((p) => toScreen(p.x, p.y, TILE_WIDTH, TILE_HEIGHT))),
  ];

  return (
    <svg data-testid="semantic-zoom-map" data-zoom-level="settlement" viewBox={centeredViewBox(boundsPoints, TILE_WIDTH * 2)}>
      {settlement.buildings.map((building) => (
        <IsoTile
          key={building.id}
          gridX={building.gridPosition.x}
          gridY={building.gridPosition.y}
          height={building.height}
          kind={building.kind}
          onClick={building.floors.length > 0 ? () => onSelectBuilding?.(building.id) : undefined}
        />
      ))}
      {agents.map((agent) => (
        <SettlementAgentMarker key={agent.id} agent={agent} notable={notableAgents.has(agent.id)} onSelectNpc={onSelectNpc} />
      ))}
    </svg>
  );
}
