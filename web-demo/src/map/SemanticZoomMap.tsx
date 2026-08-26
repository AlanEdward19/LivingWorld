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

/**
 * Mapa com zoom semântico — cada nível troca DENSIDADE de informação renderizada, não só
 * escala (spec P1b AC1-3):
 * - "world" (T9): só assentamentos/rótulos, nenhum prédio nem NPC.
 * - "district" (T10): prédios do assentamento selecionado, ainda sem NPC.
 * - "agent" (T10): NPCs individuais do assentamento, clicáveis.
 */
export function SemanticZoomMap({ fixture, level = "world", settlementId, onSelectSettlement, onSelectNpc }: SemanticZoomMapProps) {
  if (level === "world") {
    return (
      <svg data-testid="semantic-zoom-map" data-zoom-level="world" viewBox="0 0 800 600">
        {fixture.settlements.map((settlement) => {
          const { x, y } = toScreen(settlement.gridPosition.x, settlement.gridPosition.y, TILE_WIDTH, TILE_HEIGHT);
          return (
            <g
              key={settlement.id}
              data-testid="settlement-marker"
              onClick={() => onSelectSettlement(settlement.id)}
              style={{ cursor: "pointer" }}
            >
              <circle cx={x} cy={y} r={16} fill={SETTLEMENT_PALETTE.top} stroke={SETTLEMENT_PALETTE.right} strokeWidth={2} />
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
    return (
      <svg data-testid="semantic-zoom-map" data-zoom-level="district" viewBox="0 0 800 600">
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
  return (
    <svg data-testid="semantic-zoom-map" data-zoom-level="agent" viewBox="0 0 800 600">
      {agents.map((agent) => {
        const { x, y } = toScreen(agent.gridPosition.x, agent.gridPosition.y, TILE_WIDTH, TILE_HEIGHT);
        return (
          <foreignObject
            key={agent.id}
            data-testid="agent-marker"
            x={x - 16}
            y={y - 16}
            width={32}
            height={38}
            onClick={() => onSelectNpc(agent.id)}
            style={{ cursor: "pointer" }}
          >
            <NpcToken id={agent.id} size={32} />
          </foreignObject>
        );
      })}
    </svg>
  );
}
