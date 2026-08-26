import type { KeyboardEvent } from "react";
import type { AgentFixture, WorldFixture } from "../fixture/types";
import { TILE_HEIGHT, TILE_WIDTH, toScreen } from "./IsoProjection";
import { SETTLEMENT_PALETTE } from "./isoPalette";
import { appearanceForNpc } from "../npc/appearance";
import { usePatrolPosition, type GridPoint } from "./usePatrolPosition";

/**
 * World View (doc `LivingWorld_Frontend_Final.md` §14/§46, AD-018) — assentamentos + todo NPC
 * como ponto pequeno, perto do seu assentamento; agents nunca somem por causa do zoom.
 *
 * AD-020: o nível "settlement" que existia aqui (prédios isométricos via `IsoTile`) foi
 * REMOVIDO — Settlement View agora é o renderer Canvas/WebGL dedicado
 * (`render/SettlementStage.tsx`), não mais um SVG declarativo. Este componente só cobre o
 * mapa-múndi (nível "world"); World/Continent View redesign completo (terreno estilizado,
 * estradas, rios, veículos) fica no backlog da mesma fase (ver spec.md).
 */
export interface SemanticZoomMapProps {
  fixture: WorldFixture;
  onSelectSettlement: (settlementId: string) => void;
  onSelectNpc: (agentId: string) => void;
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

export function SemanticZoomMap({ fixture, onSelectSettlement, onSelectNpc }: SemanticZoomMapProps) {
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
