import type { KeyboardEvent } from "react";
import type { WorldFixture } from "../fixture/types";
import { IsoTile, TILE_HEIGHT, TILE_WIDTH } from "./IsoTileRenderer";
import { toScreen } from "./IsoProjection";
import { SETTLEMENT_PALETTE } from "./isoPalette";
import { NpcToken } from "../npc/NpcToken";

export type ZoomLevel = "world" | "district" | "agent";

export interface SemanticZoomMapProps {
  fixture: WorldFixture;
  /** Nível atual — decidido pela view que monta o mapa (World/Settlement/Agent), não por um
   * controle de zoom interno ao mapa nesta demo. Default "world" (World View). */
  level?: ZoomLevel;
  /** Obrigatório pra "district"/"agent" — escopa prédios/NPCs ao assentamento selecionado. */
  settlementId?: string;
  onSelectSettlement: (settlementId: string) => void;
  onSelectNpc: (agentId: string) => void;
}

const DEFAULT_HALF_EXTENT = TILE_WIDTH * 3;

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
 * Câmera centralizada no conteúdo (doc §192 Map QA "consigo saber onde olhar?") — em vez de um
 * `viewBox` fixo que deixa o conteúdo excêntrico quando o grid não cobre 800×600 inteiro, o
 * `viewBox` é calculado a partir do bounding box real dos pontos renderizados, com padding.
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

/** Ids de settlement que têm algum evento pertencente a um Story Thread — base do marcador de
 * evento importante (doc §103). Sem estado de "já visto" nesta demo: o pulso roda uma vez por
 * montagem do mapa, depois o marcador fica discreto e estático (CSS, respeita reduced-motion). */
function notableSettlementIds(fixture: WorldFixture): Set<string> {
  const notableEventIds = new Set(fixture.storyThreads.flatMap((t) => t.eventIds));
  return new Set(fixture.events.filter((e) => notableEventIds.has(e.eventId)).map((e) => e.settlementId));
}

function notableAgentIds(fixture: WorldFixture): Set<string> {
  const notableEventIds = new Set(fixture.storyThreads.flatMap((t) => t.eventIds));
  return new Set(
    fixture.events
      .filter((e) => notableEventIds.has(e.eventId))
      .flatMap((e) => e.affectedAgentIds),
  );
}

/**
 * Mapa com zoom semântico — cada nível troca DENSIDADE de informação renderizada, não só
 * escala (spec P1b AC1-3):
 * - "world" (T9): só assentamentos/rótulos, nenhum prédio nem NPC.
 * - "district" (T10): prédios do assentamento selecionado, ainda sem NPC.
 * - "agent" (T10): NPCs individuais do assentamento, clicáveis.
 */
export function SemanticZoomMap({ fixture, level = "world", settlementId, onSelectSettlement, onSelectNpc }: SemanticZoomMapProps) {
  if (level === "world") {
    const points = fixture.settlements.map((s) => toScreen(s.gridPosition.x, s.gridPosition.y, TILE_WIDTH, TILE_HEIGHT));
    const notable = notableSettlementIds(fixture);
    return (
      <svg data-testid="semantic-zoom-map" data-zoom-level="world" viewBox={centeredViewBox(points, TILE_WIDTH * 2)}>
        {fixture.settlements.map((settlement) => {
          const { x, y } = toScreen(settlement.gridPosition.x, settlement.gridPosition.y, TILE_WIDTH, TILE_HEIGHT);
          return (
            <g
              key={settlement.id}
              data-testid="settlement-marker"
              onClick={() => onSelectSettlement(settlement.id)}
              onKeyDown={activateOnKey(() => onSelectSettlement(settlement.id))}
              role="button"
              tabIndex={0}
              aria-label={`Open ${settlement.name}`}
              style={{ cursor: "pointer" }}
            >
              <circle cx={x} cy={y} r={16} fill={SETTLEMENT_PALETTE.top} stroke={SETTLEMENT_PALETTE.right} strokeWidth={2} />
              {notable.has(settlement.id) && (
                <circle data-testid="event-marker" cx={x + 12} cy={y - 12} r={4} fill="var(--warning, #c69b58)" />
              )}
              <text x={x} y={y + 30} textAnchor="middle" fontSize={12}>
                {settlement.name}
              </text>
            </g>
          );
        })}
      </svg>
    );
  }

  const settlement = fixture.settlements.find((s) => s.id === settlementId);
  if (!settlement) return null;

  if (level === "district") {
    const points = settlement.buildings.map((b) => toScreen(b.gridPosition.x, b.gridPosition.y, TILE_WIDTH, TILE_HEIGHT));
    return (
      <svg data-testid="semantic-zoom-map" data-zoom-level="district" viewBox={centeredViewBox(points, TILE_WIDTH * 2)}>
        {settlement.buildings.map((building) => (
          <IsoTile
            key={building.id}
            gridX={building.gridPosition.x}
            gridY={building.gridPosition.y}
            height={building.height}
            kind={building.kind}
          />
        ))}
      </svg>
    );
  }

  // level === "agent"
  const agents = fixture.agents.filter((agent) => agent.settlementId === settlementId);
  const points = agents.map((a) => toScreen(a.gridPosition.x, a.gridPosition.y, TILE_WIDTH, TILE_HEIGHT));
  const notable = notableAgentIds(fixture);
  return (
    <svg data-testid="semantic-zoom-map" data-zoom-level="agent" viewBox={centeredViewBox(points, TILE_WIDTH * 2)}>
      {agents.map((agent) => {
        const { x, y } = toScreen(agent.gridPosition.x, agent.gridPosition.y, TILE_WIDTH, TILE_HEIGHT);
        return (
          <g key={agent.id}>
            <foreignObject
              data-testid="agent-marker"
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
            {notable.has(agent.id) && <circle data-testid="event-marker" cx={x + 14} cy={y - 14} r={3.5} fill="var(--warning, #c69b58)" />}
          </g>
        );
      })}
    </svg>
  );
}
